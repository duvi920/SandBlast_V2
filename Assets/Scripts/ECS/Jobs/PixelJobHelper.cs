using Unity.Collections;

namespace SandBlast
{
    // Burst IJob에서 공통으로 사용하는 정적 유틸리티.
    // 관리형 타입 없음 — Burst 컴파일 가능.
    static class PixelJobHelper
    {
        // 셀 이동: (fx,fy) → (tx,ty). 대상이 EMPTY일 때만 4채널 전체를 스왑한다.
        public static bool TryMove(
            NativeArray<byte> type,
            NativeArray<byte> temperature,
            NativeArray<byte> lifetime,
            NativeArray<byte> flammability,
            NativeArray<int>  cellUpdateTick,
            NativeArray<ChunkMeta> chunkMetas,
            int width, int height, int chunksX, int chunksY,
            int currentTick,
            int fx, int fy, int tx, int ty)
        {
            if ((uint)tx >= (uint)width || (uint)ty >= (uint)height) return false;
            int toIdx = ty * width + tx;
            if ((CellType)type[toIdx] != CellType.EMPTY) return false;

            int fromIdx = fy * width + fx;

            byte t;
            t = type[fromIdx];        type[fromIdx]        = type[toIdx];        type[toIdx]        = t;
            t = temperature[fromIdx]; temperature[fromIdx] = temperature[toIdx]; temperature[toIdx] = t;
            t = lifetime[fromIdx];    lifetime[fromIdx]    = lifetime[toIdx];    lifetime[toIdx]    = t;
            t = flammability[fromIdx];flammability[fromIdx]= flammability[toIdx];flammability[toIdx]= t;

            cellUpdateTick[toIdx] = currentTick;
            MarkMoved(chunkMetas, chunksX, chunksY, tx, ty);
            MarkMoved(chunkMetas, chunksX, chunksY, fx, fy);
            return true;
        }

        // 셀 변경 시 청크를 깨우고 경계 인접 청크도 함께 깨운다
        public static void MarkMoved(NativeArray<ChunkMeta> metas, int chunksX, int chunksY, int x, int y)
        {
            int cx = x / SimulationConstants.CHUNK_SIZE;
            int cy = y / SimulationConstants.CHUNK_SIZE;
            ActivateChunk(metas, chunksX, chunksY, cx, cy);

            int lx = x % SimulationConstants.CHUNK_SIZE;
            int ly = y % SimulationConstants.CHUNK_SIZE;
            if (lx == 0)                                  WakeNeighbor(metas, chunksX, chunksY, cx - 1, cy);
            if (lx == SimulationConstants.CHUNK_SIZE - 1) WakeNeighbor(metas, chunksX, chunksY, cx + 1, cy);
            if (ly == 0)                                  WakeNeighbor(metas, chunksX, chunksY, cx, cy - 1);
            if (ly == SimulationConstants.CHUNK_SIZE - 1) WakeNeighbor(metas, chunksX, chunksY, cx, cy + 1);
        }

        static void ActivateChunk(NativeArray<ChunkMeta> metas, int chunksX, int chunksY, int cx, int cy)
        {
            if ((uint)cx >= (uint)chunksX || (uint)cy >= (uint)chunksY) return;
            int idx  = cy * chunksX + cx;
            var m    = metas[idx];
            m.SleepTimer = 0;
            m.State      = ChunkState.ACTIVE;
            m.SyncDirty  = true;
            metas[idx]   = m;
        }

        static void WakeNeighbor(NativeArray<ChunkMeta> metas, int chunksX, int chunksY, int cx, int cy)
        {
            if ((uint)cx >= (uint)chunksX || (uint)cy >= (uint)chunksY) return;
            int idx  = cy * chunksX + cx;
            var m    = metas[idx];
            m.BorderDirty = true;
            if (m.State == ChunkState.SLEEPING)
            {
                m.State      = ChunkState.ACTIVE;
                m.SleepTimer = 0;
            }
            metas[idx] = m;
        }
    }
}
