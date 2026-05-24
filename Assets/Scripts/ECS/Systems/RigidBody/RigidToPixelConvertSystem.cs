using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace SandBlast
{
    // PendingPixelConversionTag가 붙은 강체 엔티티를 SOLID_DEBRIS 픽셀로 변환한다.
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(RigidBodyGraphSystem))]
    [UpdateBefore(typeof(SolidMaskSyncSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    public partial class RigidToPixelConvertSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<PixelGridSingleton>();
            RequireForUpdate<ChunkManagerSingleton>();
            RequireForUpdate<NetworkTime>();
        }

        protected override void OnUpdate()
        {
            if (!SystemAPI.TryGetSingleton<NetworkTime>(out var networkTime)) return;
            if (!networkTime.IsFirstTimeFullyPredictingTick) return;

            var grid   = SystemAPI.GetSingleton<PixelGridSingleton>();
            var chunks = SystemAPI.GetSingleton<ChunkManagerSingleton>();
            var ecb    = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (node, ltw, vel, poly, entity) in
                     SystemAPI.Query<
                         RefRO<RigidBodyNodeComponent>,
                         RefRO<LocalToWorld>,
                         RefRO<RigidVelocityComponent>,
                         DynamicBuffer<PolygonVertex>>()
                     .WithAll<PendingPixelConversionTag>()
                     .WithEntityAccess())
            {
                // 로컬 → 월드 좌표 변환
                var worldPoly = new NativeArray<float2>(poly.Length, Allocator.Temp);
                for (int i = 0; i < poly.Length; i++)
                {
                    float4 wp = math.mul(ltw.ValueRO.Value,
                                         new float4(poly[i].LocalPos.x, poly[i].LocalPos.y, 0f, 1f));
                    worldPoly[i] = wp.xy;
                }

                // 바운딩 박스 계산
                float2 bMin = worldPoly[0], bMax = worldPoly[0];
                for (int i = 1; i < worldPoly.Length; i++)
                {
                    bMin = math.min(bMin, worldPoly[i]);
                    bMax = math.max(bMax, worldPoly[i]);
                }

                float2 origin = grid.GridOrigin;
                float  ppu    = grid.PixelsPerUnit;
                int gMinX = math.max(0,             (int)math.floor((bMin.x - origin.x) * ppu));
                int gMaxX = math.min(grid.Width  - 1,(int)math.floor((bMax.x - origin.x) * ppu));
                int gMinY = math.max(0,             (int)math.floor((bMin.y - origin.y) * ppu));
                int gMaxY = math.min(grid.Height - 1,(int)math.floor((bMax.y - origin.y) * ppu));

                // 래스터화 → SOLID_DEBRIS 기록
                for (int gy = gMinY; gy <= gMaxY; gy++)
                for (int gx = gMinX; gx <= gMaxX; gx++)
                {
                    float wx = origin.x + (gx + 0.5f) / ppu;
                    float wy = origin.y + (gy + 0.5f) / ppu;

                    if (!PointInPolygon(new float2(wx, wy), worldPoly)) continue;

                    int idx = grid.Index(gx, gy);
                    CellType ct = (CellType)grid.Type[idx];
                    if (ct != CellType.SOLID_RIGID && ct != CellType.EMPTY) continue;

                    grid.Type[idx]         = (byte)CellType.SOLID_DEBRIS;
                    grid.Lifetime[idx]     = 0;
                    grid.Flammability[idx] = SimulationConstants.FLAMMABILITY_DEBRIS;
                    chunks.MarkMoved(gx, gy);
                }

                worldPoly.Dispose();

                // SOLID_RIGID 마스크 제거 및 엔티티 파괴
                ecb.RemoveComponent<PendingPixelConversionTag>(entity);
                ecb.DestroyEntity(entity);
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }

        // 레이 캐스팅 점-폴리곤 포함 판정 (NativeArray 버전)
        static bool PointInPolygon(float2 point, NativeArray<float2> poly)
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
