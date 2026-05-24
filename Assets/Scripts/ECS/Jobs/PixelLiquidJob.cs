using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace SandBlast
{
    // 액체(LIQUID_WATER, LIQUID_LAVA) CA 단계 — 설계 문서 §6.3, §6.4
    // 아래에서 위로 순회, 행마다 X축 방향을 무작위로 설정해 수평 편향을 방지함
    [BurstCompile]
    struct PixelLiquidJob : IJob
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
            for (int y = 0; y < Height; y++)
            {
                bool leftFirst = Rng.NextBool();
                for (int xi = 0; xi < Width; xi++)
                {
                    int x   = leftFirst ? xi : (Width - 1 - xi);
                    int idx = y * Width + x;

                    CellType ct = (CellType)Type[idx];
                    if (!ct.IsLiquid()) continue;
                    if (CellUpdateTick[idx] == CurrentTick) continue;
                    CellUpdateTick[idx] = CurrentTick;

                    // 용암 냉각·고체화·흙 반응
                    if (ct == CellType.LIQUID_LAVA)
                    {
                        Temperature[idx] = (byte)math.max(0,
                            Temperature[idx] - SimulationConstants.COOLING_RATE);

                        if (Temperature[idx] <= SimulationConstants.LAVA_SOLIDIFY_THRESHOLD)
                        {
                            Type[idx] = (byte)CellType.SOLID_STATIC;
                            PixelJobHelper.MarkMoved(ChunkMetas, ChunksX, ChunksY, x, y);
                            continue;
                        }
                        EmitHeat(x, y);
                        ReactLavaWithDirt(x, y); // 용암+흙 → 현무암
                    }

                    // 1. 수직 하강
                    if (TryMove(x, y, x, y - 1)) continue;

                    // 2. 대각선 하강
                    bool diagLeft = Rng.NextBool();
                    if (TryMove(x, y, x + (diagLeft ? -1 : 1), y - 1)) continue;
                    if (TryMove(x, y, x + (diagLeft ?  1 : -1), y - 1)) continue;

                    // 3. 수평 확산 (액체 종류별 점성 적용)
                    int disp = ct == CellType.LIQUID_LAVA   ? SimulationConstants.LAVA_DISPERSION   :
                               ct == CellType.LIQUID_POISON ? SimulationConstants.POISON_DISPERSION :
                                                              SimulationConstants.WATER_DISPERSION;

                    bool goLeft = Rng.NextBool();
                    bool moved  = false;
                    for (int step = 1; step <= disp && !moved; step++)
                    {
                        if (TryMove(x, y, x + (goLeft ? -step :  step), y)) { moved = true; break; }
                        if (TryMove(x, y, x + (goLeft ?  step : -step), y)) { moved = true; break; }
                    }

                    // 4. 위쪽도 액체면 압력으로 역류 허용
                    if (!moved && GetCell(x, y + 1).IsLiquid())
                    {
                        bool pl = Rng.NextBool();
                        if (!TryMove(x, y, x + (pl ? -1 : 1), y))
                            TryMove(x, y, x + (pl ?  1 : -1), y);
                    }
                }
            }
        }

        // 용암이 인접 셀에 열을 방출한다 (자동 발화는 용암에서도 적용)
        void EmitHeat(int x, int y)
        {
            int4 dx = new int4(0,  0, -1, 1);
            int4 dy = new int4(1, -1,  0, 0);
            for (int d = 0; d < 4; d++)
            {
                int nx = x + dx[d], ny = y + dy[d];
                if ((uint)nx >= (uint)Width || (uint)ny >= (uint)Height) continue;
                int ni   = ny * Width + nx;
                int temp = Temperature[ni] + SimulationConstants.HEAT_EMISSION;
                Temperature[ni] = (byte)math.min(255, temp);
            }
        }

        // 용암이 인접한 SOLID_DIRT 를 SOLID_BASALT 로 굳힌다 (통로 차단 전술)
        void ReactLavaWithDirt(int x, int y)
        {
            int4 dx = new int4(0,  0, -1, 1);
            int4 dy = new int4(1, -1,  0, 0);
            for (int d = 0; d < 4; d++)
            {
                int nx = x + dx[d], ny = y + dy[d];
                if ((uint)nx >= (uint)Width || (uint)ny >= (uint)Height) continue;
                int ni = ny * Width + nx;
                if ((CellType)Type[ni] == CellType.SOLID_DIRT)
                {
                    Type[ni]        = (byte)CellType.SOLID_BASALT;
                    Temperature[ni] = 0;
                    PixelJobHelper.MarkMoved(ChunkMetas, ChunksX, ChunksY, nx, ny);
                }
            }
        }

        bool TryMove(int fx, int fy, int tx, int ty) =>
            PixelJobHelper.TryMove(
                Type, Temperature, Lifetime, Flammability,
                CellUpdateTick, ChunkMetas,
                Width, Height, ChunksX, ChunksY,
                CurrentTick, fx, fy, tx, ty);

        CellType GetCell(int x, int y)
        {
            if ((uint)x >= (uint)Width || (uint)y >= (uint)Height) return CellType.SOLID_STATIC;
            return (CellType)Type[y * Width + x];
        }
    }
}
