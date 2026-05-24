using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace SandBlast
{
    // 분말(POWDER_SAND, POWDER_ASH) CA 단계 — 설계 문서 §6.1, §6.2
    // 아래에서 위로 순회, 섞인 X축 순서로 좌우 편향을 방지함
    [BurstCompile]
    struct PixelPowderJob : IJob
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

                CellType ct = (CellType)Type[idx];
                if (ct != CellType.POWDER_SAND && ct != CellType.POWDER_ASH) continue;
                if (CellUpdateTick[idx] == CurrentTick) continue;
                CellUpdateTick[idx] = CurrentTick;

                // 재(ASH): 열기류 부력으로 상승
                if (ct == CellType.POWDER_ASH && Rng.NextFloat() < SimulationConstants.ASH_FLOAT_CHANCE)
                {
                    if (TryMove(x, y, x, y + 1))
                    {
                        Lifetime[y * Width + x + Width] = 0; // 이동 후 위쪽 셀 슬립 초기화
                        continue;
                    }
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

                // 3. 정지 — 슬립 카운터 증가 → SOLID_STATIC 전환
                if (Lifetime[idx] < 255) Lifetime[idx]++;
                if (Lifetime[idx] >= SimulationConstants.SLEEP_THRESHOLD)
                {
                    Type[idx]     = (byte)CellType.SOLID_STATIC;
                    Lifetime[idx] = 0;
                    PixelJobHelper.MarkMoved(ChunkMetas, ChunksX, ChunksY, x, y);
                }
            }
        }

        bool TryMove(int fx, int fy, int tx, int ty) =>
            PixelJobHelper.TryMove(
                Type, Temperature, Lifetime, Flammability,
                CellUpdateTick, ChunkMetas,
                Width, Height, ChunksX, ChunksY,
                CurrentTick, fx, fy, tx, ty);
    }
}
