using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace SandBlast
{
    // 연기(GAS_SMOKE) CA 단계 — 설계 문서 §6.6
    // 위에서 아래로 순회해 상승하는 연기가 이번 틱에 두 번 이동하지 않도록 함
    [BurstCompile]
    struct PixelSmokeJob : IJob
    {
        [NativeDisableContainerSafetyRestriction] public NativeArray<byte> Type;
        [NativeDisableContainerSafetyRestriction] public NativeArray<byte> Temperature;
        [NativeDisableContainerSafetyRestriction] public NativeArray<byte> Lifetime;
        [NativeDisableContainerSafetyRestriction] public NativeArray<byte> Flammability;
        [NativeDisableContainerSafetyRestriction] public NativeArray<int>  CellUpdateTick;
        [NativeDisableContainerSafetyRestriction] public NativeArray<ChunkMeta> ChunkMetas;

        public int Width, Height, CurrentTick, ChunksX, ChunksY;
        public Unity.Mathematics.Random Rng;

        public void Execute()
        {
            for (int y = Height - 1; y >= 0; y--)
            for (int x = 0; x < Width; x++)
            {
                int idx = y * Width + x;
                if (Type[idx] != (byte)CellType.GAS_SMOKE) continue;
                if (CellUpdateTick[idx] == CurrentTick) continue;
                CellUpdateTick[idx] = CurrentTick;

                // 1. 수명 소진 → 소멸
                if (Lifetime[idx] == 0)
                {
                    Type[idx] = (byte)CellType.EMPTY;
                    PixelJobHelper.MarkMoved(ChunkMetas, ChunksX, ChunksY, x, y);
                    continue;
                }
                Lifetime[idx]--;

                // 2. 위로 상승
                if (TryMove(x, y, x, y + 1)) continue;

                // 3. 수평 표류
                bool left = Rng.NextBool();
                int  dx1  = left ? -1 : 1;
                int  dx2  = left ?  1 : -1;
                if (!TryMove(x, y, x + dx1, y))
                    TryMove(x, y, x + dx2, y);
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
