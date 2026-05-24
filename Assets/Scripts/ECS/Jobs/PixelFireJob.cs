using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace SandBlast
{
    // 불(FIRE) CA 단계 — 설계 문서 §6.5
    [BurstCompile]
    struct PixelFireJob : IJob
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
            for (int x = 0; x < Width;  x++)
            {
                int idx = y * Width + x;
                if (Type[idx] != (byte)CellType.FIRE) continue;
                if (CellUpdateTick[idx] == CurrentTick) continue;
                CellUpdateTick[idx] = CurrentTick;

                // 1. 수명 감소
                if (Lifetime[idx] == 0)
                {
                    Type[idx]        = Rng.NextFloat() < SimulationConstants.ASH_CHANCE
                        ? (byte)CellType.POWDER_ASH
                        : (byte)CellType.EMPTY;
                    Temperature[idx] = 0;
                    PixelJobHelper.MarkMoved(ChunkMetas, ChunksX, ChunksY, x, y);
                    continue;
                }
                Lifetime[idx]--;

                // 2. 인접 셀에 열 방출 + 자동 발화 판정
                EmitHeat(x, y);

                // 3. 인접 연료에 불 전파
                PropagateFireToNeighbors(x, y);

                // 4. 위쪽에 연기 생성
                if (y + 1 < Height)
                {
                    int aboveIdx = (y + 1) * Width + x;
                    if ((CellType)Type[aboveIdx] == CellType.EMPTY
                        && Rng.NextFloat() < SimulationConstants.SMOKE_CHANCE)
                    {
                        Type[aboveIdx]           = (byte)CellType.GAS_SMOKE;
                        Lifetime[aboveIdx]       = (byte)SimulationConstants.SMOKE_LIFE_MAX;
                        CellUpdateTick[aboveIdx] = CurrentTick;
                        PixelJobHelper.MarkMoved(ChunkMetas, ChunksX, ChunksY, x, y + 1);
                    }
                }

                // 5. 인접 물이 불을 끔
                if (AdjacentToWater(x, y))
                {
                    Type[idx]     = (byte)CellType.POWDER_ASH;
                    Lifetime[idx] = 0;
                    PixelJobHelper.MarkMoved(ChunkMetas, ChunksX, ChunksY, x, y);
                }
            }
        }

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

                if (Temperature[ni] >= SimulationConstants.AUTO_IGNITE_TEMP
                    && ((CellType)Type[ni]).IsFuel()
                    && (CellType)Type[ni] != CellType.FIRE)
                    IgniteCell(nx, ny);
            }
        }

        void PropagateFireToNeighbors(int x, int y)
        {
            int4 dx = new int4(0,  0, -1, 1);
            int4 dy = new int4(1, -1,  0, 0);
            for (int d = 0; d < 4; d++)
            {
                int nx = x + dx[d], ny = y + dy[d];
                if ((uint)nx >= (uint)Width || (uint)ny >= (uint)Height) continue;
                int      ni = ny * Width + nx;
                CellType nt = (CellType)Type[ni];
                if (!nt.IsFuel()) continue;
                if (Rng.NextFloat() < Flammability[ni] / 255f)
                    IgniteCell(nx, ny);
            }
        }

        void IgniteCell(int x, int y)
        {
            int idx = y * Width + x;

            // 화약은 폭발로 처리 — 직접 FIRE 로 전환하지 않음
            if ((CellType)Type[idx] == CellType.SOLID_GUNPOWDER)
            {
                GunpowderExplode(x, y);
                return;
            }

            Type[idx]       = (byte)CellType.FIRE;
            float variance  = Rng.NextFloat(-10f, 11f);
            Lifetime[idx]   = (byte)math.clamp(
                (int)(SimulationConstants.FIRE_LIFE_MAX + variance), 1, 255);
            CellUpdateTick[idx] = CurrentTick;
            PixelJobHelper.MarkMoved(ChunkMetas, ChunksX, ChunksY, x, y);
        }

        // 화약 폭발 — GUNPOWDER_BLAST_RADIUS(16px) 내 파괴 가능 셀을 제거하고 테두리에 불 생성
        void GunpowderExplode(int cx, int cy)
        {
            const int R = SimulationConstants.GUNPOWDER_BLAST_RADIUS;

            // 1단계: 반경 내 비-indestructible 셀 제거
            for (int dy = -R; dy <= R; dy++)
            for (int dx = -R; dx <= R; dx++)
            {
                if (dx * dx + dy * dy > R * R) continue;
                int gx = cx + dx, gy = cy + dy;
                if ((uint)gx >= (uint)Width || (uint)gy >= (uint)Height) continue;
                int ni = gy * Width + gx;
                if (((CellType)Type[ni]).IsIndestructible()) continue;

                Type[ni]        = (byte)CellType.EMPTY;
                Temperature[ni] = 0;
                Lifetime[ni]    = 0;
                Flammability[ni]= 0;
                PixelJobHelper.MarkMoved(ChunkMetas, ChunksX, ChunksY, gx, gy);
            }

            // 2단계: 테두리 반경 +2 에 불 생성
            int fireR = R + 2;
            for (int dy = -fireR; dy <= fireR; dy++)
            for (int dx = -fireR; dx <= fireR; dx++)
            {
                if (dx * dx + dy * dy > fireR * fireR) continue;
                if (dx * dx + dy * dy <= R * R) continue; // 이미 비운 내부 제외
                int gx = cx + dx, gy = cy + dy;
                if ((uint)gx >= (uint)Width || (uint)gy >= (uint)Height) continue;
                int ni = gy * Width + gx;
                if ((CellType)Type[ni] != CellType.EMPTY) continue;

                Type[ni]    = (byte)CellType.FIRE;
                Lifetime[ni]= (byte)SimulationConstants.FIRE_LIFE_MAX;
                CellUpdateTick[ni] = CurrentTick;
                PixelJobHelper.MarkMoved(ChunkMetas, ChunksX, ChunksY, gx, gy);
            }
        }

        bool AdjacentToWater(int x, int y)
        {
            return GetCell(x, y + 1) == CellType.LIQUID_WATER ||
                   GetCell(x, y - 1) == CellType.LIQUID_WATER ||
                   GetCell(x - 1, y) == CellType.LIQUID_WATER ||
                   GetCell(x + 1, y) == CellType.LIQUID_WATER;
        }

        CellType GetCell(int x, int y)
        {
            if ((uint)x >= (uint)Width || (uint)y >= (uint)Height) return CellType.SOLID_STATIC;
            return (CellType)Type[y * Width + x];
        }
    }
}
