using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace SandBlast
{
    // RigidBodyNode ECS 엔티티의 LocalToWorld를 읽어 SOLID_RIGID 마스크를 픽셀 레이어에 동기화한다.
    // SolidMaskSyncManager(managed class)를 대체한다.
    //
    // 동작 원리:
    //  1. 매 프레임 각 노드의 LocalToWorld 행렬을 이전 프레임과 비교
    //  2. 변경된 노드만 폴리곤 래스터화 → XOR diff → CoverCell / UncoverCell
    //  3. TransformUsageFlags.Dynamic으로 베이크된 엔티티는 LocalToWorld가
    //     Rigidbody2D가 구동하는 GameObject Transform과 자동 동기화된다
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(PixelSimulationSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial class SolidMaskSyncSystem : SystemBase
    {
        // nodeId → (이전 프레임 LocalToWorld, 이전 프레임 픽셀 인덱스 집합)
        readonly Dictionary<int, float4x4>    _prevLtw  = new();
        readonly Dictionary<int, HashSet<int>> _prevMask = new();

        // 래스터화 결과를 매 노드마다 재사용하는 스크래치 버퍼 — 프레임당 할당 없음
        HashSet<int> _rasterScratch = new(512);

        protected override void OnCreate()
        {
            RequireForUpdate<PixelGridSingleton>();
            RequireForUpdate<ChunkManagerSingleton>();
        }

        protected override void OnUpdate()
        {
            var grid   = SystemAPI.GetSingleton<PixelGridSingleton>();
            var chunks = SystemAPI.GetSingleton<ChunkManagerSingleton>();

            // 모든 리지드 바디 속도를 한 번만 수집하여 O(N²) → O(N) 으로 개선
            var velocityMap = new NativeHashMap<int, float2>(16, Allocator.Temp);
            foreach (var (node, vel) in
                     SystemAPI.Query<RefRO<RigidBodyNodeComponent>, RefRO<RigidVelocityComponent>>())
            {
                velocityMap[node.ValueRO.NodeId] = vel.ValueRO.Velocity;
            }

            foreach (var (node, ltw, poly) in
                     SystemAPI.Query<
                         RefRO<RigidBodyNodeComponent>,
                         RefRO<LocalToWorld>,
                         DynamicBuffer<PolygonVertex>>())
            {
                int    nodeId  = node.ValueRO.NodeId;
                float4x4 curLtw = ltw.ValueRO.Value;

                // 변환 행렬이 바뀌지 않았으면 생략
                if (_prevLtw.TryGetValue(nodeId, out float4x4 prevLtw)
                    && curLtw.Equals(prevLtw))
                    continue;

                _prevLtw[nodeId] = curLtw;

                // 스크래치 버퍼를 클리어하고 래스터화 결과를 채움 — new HashSet 없음
                _rasterScratch.Clear();
                RasterizePoly(poly, curLtw, grid, _rasterScratch);

                if (!_prevMask.TryGetValue(nodeId, out var prev))
                    prev = new HashSet<int>();

                var rigidVel = velocityMap.TryGetValue(nodeId, out var v) ? v : float2.zero;

                // 새로 덮인 셀
                foreach (int idx in _rasterScratch)
                    if (!prev.Contains(idx)) CoverCell(idx, nodeId, rigidVel, grid, chunks);

                // 더 이상 덮지 않는 셀
                foreach (int idx in prev)
                    if (!_rasterScratch.Contains(idx)) UncoverCell(idx, nodeId, grid, chunks);

                // 스왑: _rasterScratch(현재)를 prevMask에 저장하고
                // 이전 prev를 _rasterScratch로 재사용 — 할당 없음
                (_rasterScratch, _prevMask[nodeId]) = (prev, _rasterScratch);
            }

            velocityMap.Dispose();
        }

        // 노드 제거 시 호출 — RigidToPixelConvertSystem이 엔티티 파괴 전에 호출 가능
        public void RemoveNode(int nodeId, PixelGridSingleton grid, ChunkManagerSingleton chunks)
        {
            if (!_prevMask.TryGetValue(nodeId, out HashSet<int> prev)) return;
            foreach (int idx in prev) UncoverCell(idx, nodeId, grid, chunks);
            _prevMask.Remove(nodeId);
            _prevLtw.Remove(nodeId);
        }

        // ─── 셀 조작 ─────────────────────────────────────────────

        static void CoverCell(int idx, int nodeId, float2 rigidVel,
                               PixelGridSingleton grid, ChunkManagerSingleton chunks)
        {
            int x = idx % grid.Width;
            int y = idx / grid.Width;

            CellType existing = (CellType)grid.Type[idx];
            if (existing != CellType.EMPTY && existing != CellType.SOLID_RIGID)
                DisplaceCell(x, y, rigidVel, grid, chunks);

            grid.Type[idx]    = (byte)CellType.SOLID_RIGID;
            grid.RigidId[idx] = (byte)(nodeId & 0xFF);
            chunks.MarkRigidDirty(x, y);
        }

        static void UncoverCell(int idx, int nodeId, PixelGridSingleton grid, ChunkManagerSingleton chunks)
        {
            if ((CellType)grid.Type[idx] == CellType.SOLID_RIGID &&
                grid.RigidId[idx] == (byte)(nodeId & 0xFF))
            {
                grid.Type[idx]    = (byte)CellType.EMPTY;
                grid.RigidId[idx] = 0;
                chunks.MarkRigidDirty(idx % grid.Width, idx / grid.Width);
            }
        }

        static void DisplaceCell(int x, int y, float2 vel,
                                  PixelGridSingleton grid, ChunkManagerSingleton chunks)
        {
            float2 dir = math.lengthsq(vel) > 0.01f ? math.normalize(vel) : new float2(0f, 1f);
            int bx = (int)math.round(dir.x);
            int by = (int)math.round(dir.y);

            Span<int> tryX = stackalloc int[] { bx, 0, -bx, 1, -1 };
            Span<int> tryY = stackalloc int[] { by, 1,  by, 0,  0 };

            int from = y * grid.Width + x;
            for (int i = 0; i < tryX.Length; i++)
            {
                int nx = x + tryX[i];
                int ny = y + tryY[i];
                if (!grid.InBounds(nx, ny)) continue;
                int to = ny * grid.Width + nx;
                if ((CellType)grid.Type[to] != CellType.EMPTY) continue;

                grid.Type[to]         = grid.Type[from];
                grid.Temperature[to]  = grid.Temperature[from];
                grid.Lifetime[to]     = grid.Lifetime[from];
                grid.Flammability[to] = grid.Flammability[from];
                grid.Type[from]       = (byte)CellType.EMPTY;
                chunks.MarkMoved(nx, ny);
                return;
            }
        }

        // ─── 래스터화 ─────────────────────────────────────────────

        // result는 호출 전 Clear()된 상태로 전달됨 — 내부에서 new 없음
        static void RasterizePoly(
            DynamicBuffer<PolygonVertex> localPoly,
            float4x4 ltw,
            PixelGridSingleton grid,
            HashSet<int> result)
        {
            // 로컬 → 월드 좌표 변환
            var worldPts = new float2[localPoly.Length];
            for (int i = 0; i < localPoly.Length; i++)
            {
                float4 wp = math.mul(ltw, new float4(localPoly[i].LocalPos.x,
                                                     localPoly[i].LocalPos.y, 0f, 1f));
                worldPts[i] = wp.xy;
            }

            // 바운딩 박스
            float2 bMin = worldPts[0], bMax = worldPts[0];
            for (int i = 1; i < worldPts.Length; i++)
            {
                bMin = math.min(bMin, worldPts[i]);
                bMax = math.max(bMax, worldPts[i]);
            }

            float2 origin = grid.GridOrigin;
            float  ppu    = grid.PixelsPerUnit;
            int gMinX = math.max(0,             (int)math.floor((bMin.x - origin.x) * ppu));
            int gMaxX = math.min(grid.Width  - 1,(int)math.floor((bMax.x - origin.x) * ppu));
            int gMinY = math.max(0,             (int)math.floor((bMin.y - origin.y) * ppu));
            int gMaxY = math.min(grid.Height - 1,(int)math.floor((bMax.y - origin.y) * ppu));

            for (int gy = gMinY; gy <= gMaxY; gy++)
            for (int gx = gMinX; gx <= gMaxX; gx++)
            {
                float wx = origin.x + (gx + 0.5f) / ppu;
                float wy = origin.y + (gy + 0.5f) / ppu;
                if (PointInPolygon(new float2(wx, wy), worldPts))
                    result.Add(gy * grid.Width + gx);
            }
        }

        static bool PointInPolygon(float2 point, float2[] poly)
        {
            bool inside = false;
            int  n      = poly.Length;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                float2 vi = poly[i], vj = poly[j];
                if (((vi.y > point.y) != (vj.y > point.y)) &&
                    point.x < (vj.x - vi.x) * (point.y - vi.y) / (vj.y - vi.y) + vi.x)
                    inside = !inside;
            }
            return inside;
        }
    }
}
