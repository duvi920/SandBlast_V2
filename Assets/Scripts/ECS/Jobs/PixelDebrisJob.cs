using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace SandBlast
{
    // 고체 잔해(SOLID_DEBRIS) CA 단계 — 설계 문서 §5.4
    // 분말과 동일한 이동 규칙, 인접 활성 셀에 의해 슬립에서 깨어남
    [BurstCompile]
    struct PixelDebrisJob : IJob
    {
        [NativeDisableContainerSafetyRestriction] public NativeArray<byte> Type;
        [NativeDisableContainerSafetyRestriction] public NativeArray<byte> Temperature;
        [NativeDisableContainerSafetyRestriction] public NativeArray<byte> Lifetime;
        [NativeDisableContainerSafetyRestriction] public NativeArray<byte> Flammability;
        [NativeDisableContainerSafetyRestriction] public NativeArray<int>  CellUpdateTick;
        [NativeDisableContainerSafetyRestriction] public NativeArray<ChunkMeta> ChunkMetas;
        [NativeDisableContainerSafetyRestriction] public NativeArray<int>  XShuffle;

        public int Width, Height, CurrentTick, ChunksX, ChunksY;
        public Unity.Mathematics.Random Rng;

        public void Execute()
        {
            for (int y = 0; y < Height; y++)
            for (int xi = 0; xi < Width; xi++)
            {
                int x   = XShuffle[xi];
                int idx = y * Width + x;
                if (Type[idx] != (byte)CellType.SOLID_DEBRIS) continue;
                if (CellUpdateTick[idx] == CurrentTick) continue;
                CellUpdateTick[idx] = CurrentTick;

                // 인접 활성 셀에 의해 슬립에서 깨어나면 카운터 초기화
                if (ShouldWake(x, y) && Lifetime[idx] >= SimulationConstants.SLEEP_THRESHOLD)
                    Lifetime[idx] = 0;

                // 슬립 한계 도달 → SOLID_STATIC 고체화
                if (Lifetime[idx] >= SimulationConstants.SLEEP_THRESHOLD)
                {
                    Type[idx]     = (byte)CellType.SOLID_STATIC;
                    Lifetime[idx] = 0;
                    PixelJobHelper.MarkMoved(ChunkMetas, ChunksX, ChunksY, x, y);
                    continue;
                }

                // 1. 수직 하강
                if (TryMove(x, y, x, y - 1))
                {
                    Lifetime[(y - 1) * Width + x] = 0;
                    continue;
                }

                // 2. 대각선 하강
                bool dl  = Rng.NextBool();
                int  dx1 = dl ? -1 : 1;
                int  dx2 = dl ?  1 : -1;
                if (TryMove(x, y, x + dx1, y - 1)) { Lifetime[(y - 1) * Width + x + dx1] = 0; continue; }
                if (TryMove(x, y, x + dx2, y - 1)) { Lifetime[(y - 1) * Width + x + dx2] = 0; continue; }

                // 3. 슬립 카운터 증가
                if (Lifetime[idx] < 255) Lifetime[idx]++;
            }
        }

        bool ShouldWake(int x, int y)
        {
            CellType n;
            n = GetCell(x, y + 1); if (n.IsLiquid() || n == CellType.POWDER_SAND || n == CellType.POWDER_ASH || n == CellType.FIRE) return true;
            n = GetCell(x, y - 1); if (n.IsLiquid() || n == CellType.POWDER_SAND || n == CellType.POWDER_ASH || n == CellType.FIRE) return true;
            n = GetCell(x - 1, y); if (n.IsLiquid() || n == CellType.POWDER_SAND || n == CellType.POWDER_ASH || n == CellType.FIRE) return true;
            n = GetCell(x + 1, y); if (n.IsLiquid() || n == CellType.POWDER_SAND || n == CellType.POWDER_ASH || n == CellType.FIRE) return true;
            return false;
        }

        CellType GetCell(int x, int y)
        {
            if ((uint)x >= (uint)Width || (uint)y >= (uint)Height) return CellType.SOLID_STATIC;
            return (CellType)Type[y * Width + x];
        }

        bool TryMove(int fx, int fy, int tx, int ty) =>
            PixelJobHelper.TryMove(
                Type, Temperature, Lifetime, Flammability,
                CellUpdateTick, ChunkMetas,
                Width, Height, ChunksX, ChunksY,
                CurrentTick, fx, fy, tx, ty);
    }
}
