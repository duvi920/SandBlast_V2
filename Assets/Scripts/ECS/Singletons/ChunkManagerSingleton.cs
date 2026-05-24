using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace SandBlast
{
    // 청크 슬리핑 상태를 NativeArray로 보관하는 ECS 싱글턴.
    // ChunkManager(managed class)를 대체한다.
    public struct ChunkManagerSingleton : IComponentData
    {
        public int ChunksX;
        public int ChunksY;
        public NativeArray<ChunkMeta> Metas;

        public int ChunkIndex(int cx, int cy) => cy * ChunksX + cx;

        public bool IsActive(int x, int y)
        {
            int cx = x / SimulationConstants.CHUNK_SIZE;
            int cy = y / SimulationConstants.CHUNK_SIZE;
            if ((uint)cx >= (uint)ChunksX || (uint)cy >= (uint)ChunksY) return false;
            return Metas[ChunkIndex(cx, cy)].State != ChunkState.SLEEPING;
        }

        // 셀이 이동하거나 상태가 바뀔 때 호출 — 소속 청크 및 경계 인접 청크를 깨움
        public void MarkMoved(int x, int y)
        {
            int cx = x / SimulationConstants.CHUNK_SIZE;
            int cy = y / SimulationConstants.CHUNK_SIZE;
            ActivateChunk(cx, cy);

            int lx = x % SimulationConstants.CHUNK_SIZE;
            int ly = y % SimulationConstants.CHUNK_SIZE;
            if (lx == 0)                                  WakeNeighbor(cx - 1, cy);
            if (lx == SimulationConstants.CHUNK_SIZE - 1) WakeNeighbor(cx + 1, cy);
            if (ly == 0)                                  WakeNeighbor(cx, cy - 1);
            if (ly == SimulationConstants.CHUNK_SIZE - 1) WakeNeighbor(cx, cy + 1);
        }

        // 강체 마스크 변경 시 호출 — DirtyFlag로 슬리핑 방지
        public void MarkRigidDirty(int x, int y)
        {
            int cx = x / SimulationConstants.CHUNK_SIZE;
            int cy = y / SimulationConstants.CHUNK_SIZE;
            if ((uint)cx >= (uint)ChunksX || (uint)cy >= (uint)ChunksY) return;

            int idx = ChunkIndex(cx, cy);
            var m = Metas[idx];
            m.DirtyFlag  = true;
            m.SyncDirty  = true;
            m.State      = ChunkState.ACTIVE;
            m.SleepTimer = 0;
            Metas[idx]   = m;
        }

        // 청크 틱 종료 시 호출 — anyMoved 없으면 슬리핑 카운터 증가
        public void EndTick(int cx, int cy, bool anyMoved)
        {
            if ((uint)cx >= (uint)ChunksX || (uint)cy >= (uint)ChunksY) return;

            int idx = ChunkIndex(cx, cy);
            var m   = Metas[idx];

            if (anyMoved || m.DirtyFlag || m.BorderDirty)
            {
                m.SleepTimer = 0;
                m.State      = ChunkState.ACTIVE;
            }
            else
            {
                if (m.SleepTimer < 255) m.SleepTimer++;
                m.State = m.SleepTimer >= SimulationConstants.CHUNK_SLEEP_THRESHOLD
                    ? ChunkState.SLEEPING
                    : ChunkState.SLEEP_PENDING;
            }

            m.DirtyFlag   = false;
            m.BorderDirty = false;
            Metas[idx]    = m;
        }

        void ActivateChunk(int cx, int cy)
        {
            if ((uint)cx >= (uint)ChunksX || (uint)cy >= (uint)ChunksY) return;
            int idx  = ChunkIndex(cx, cy);
            var m    = Metas[idx];
            m.SleepTimer = 0;
            m.State      = ChunkState.ACTIVE;
            m.SyncDirty  = true;
            Metas[idx]   = m;
        }

        void WakeNeighbor(int cx, int cy)
        {
            if ((uint)cx >= (uint)ChunksX || (uint)cy >= (uint)ChunksY) return;
            int idx  = ChunkIndex(cx, cy);
            var m    = Metas[idx];
            m.BorderDirty = true;
            if (m.State == ChunkState.SLEEPING)
            {
                m.State      = ChunkState.ACTIVE;
                m.SleepTimer = 0;
            }
            Metas[idx] = m;
        }
    }
}
