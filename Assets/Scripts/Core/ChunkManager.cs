namespace SandBlast
{
    // 테스트 및 비-ECS 코드에서 사용하는 managed 청크 매니저.
    // ECS 환경에서는 ChunkManagerSingleton(NativeArray 기반)이 이를 대체한다.
    public class ChunkManager
    {
        public readonly int ChunksX;
        public readonly int ChunksY;

        ChunkMeta[] metas;

        public ChunkManager(PixelGrid grid)
        {
            int cs  = SimulationConstants.CHUNK_SIZE;
            ChunksX = (grid.Width  + cs - 1) / cs;
            ChunksY = (grid.Height + cs - 1) / cs;
            metas   = new ChunkMeta[ChunksX * ChunksY];
            for (int i = 0; i < metas.Length; i++)
                metas[i].State = ChunkState.ACTIVE;
        }

        public ref ChunkMeta GetChunk(int cx, int cy) => ref metas[cy * ChunksX + cx];

        public bool IsActive(int cx, int cy)
        {
            if ((uint)cx >= (uint)ChunksX || (uint)cy >= (uint)ChunksY) return false;
            return metas[cy * ChunksX + cx].State != ChunkState.SLEEPING;
        }

        public void EndTick(int cx, int cy, bool anyMoved)
        {
            if ((uint)cx >= (uint)ChunksX || (uint)cy >= (uint)ChunksY) return;
            ref var m = ref metas[cy * ChunksX + cx];

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
        }

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

        public void MarkRigidDirty(int x, int y)
        {
            int cx = x / SimulationConstants.CHUNK_SIZE;
            int cy = y / SimulationConstants.CHUNK_SIZE;
            if ((uint)cx >= (uint)ChunksX || (uint)cy >= (uint)ChunksY) return;
            ref var m    = ref metas[cy * ChunksX + cx];
            m.DirtyFlag  = true;
            m.State      = ChunkState.ACTIVE;
            m.SleepTimer = 0;
        }

        void ActivateChunk(int cx, int cy)
        {
            if ((uint)cx >= (uint)ChunksX || (uint)cy >= (uint)ChunksY) return;
            ref var m    = ref metas[cy * ChunksX + cx];
            m.SleepTimer = 0;
            m.State      = ChunkState.ACTIVE;
        }

        void WakeNeighbor(int cx, int cy)
        {
            if ((uint)cx >= (uint)ChunksX || (uint)cy >= (uint)ChunksY) return;
            ref var m     = ref metas[cy * ChunksX + cx];
            m.BorderDirty = true;
            if (m.State == ChunkState.SLEEPING)
            {
                m.State      = ChunkState.ACTIVE;
                m.SleepTimer = 0;
            }
        }
    }
}
