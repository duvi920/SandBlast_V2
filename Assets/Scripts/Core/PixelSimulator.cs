using System;

namespace SandBlast
{
    // 테스트 및 비-ECS 환경에서 한 틱의 CA 시뮬레이션을 실행한다.
    // PixelFireJob → PixelSmokeJob → PixelLiquidJob → PixelPowderJob → PixelDebrisJob 순서를 반영한다.
    public class PixelSimulator
    {
        readonly PixelGrid    grid;
        readonly ChunkManager chunks;
        readonly int[]        cellUpdateTick;
        readonly int[]        xShuffle;
        readonly Random       rng = new Random(42);
        int                   currentTick;

        public PixelSimulator(PixelGrid grid, ChunkManager chunks, ForceAccumulator forces)
        {
            this.grid      = grid;
            this.chunks    = chunks;
            cellUpdateTick = new int[grid.Width * grid.Height];
            xShuffle       = new int[grid.Width];
            for (int i = 0; i < xShuffle.Length; i++) xShuffle[i] = i;
        }

        public void Tick()
        {
            currentTick++;
            ShuffleX();
            TickFire();
            TickSmoke();
            TickLiquid();
            TickPowder();
            TickDebris();
        }

        // ── 내부 헬퍼 ────────────────────────────────────────────────────

        int  W => grid.Width;
        int  H => grid.Height;

        bool InBounds(int x, int y) => (uint)x < (uint)W && (uint)y < (uint)H;
        int  Idx(int x, int y)      => y * W + x;

        CellType GetCell(int x, int y) =>
            InBounds(x, y) ? (CellType)grid.Type[Idx(x, y)] : CellType.SOLID_STATIC;

        bool TryMove(int fx, int fy, int tx, int ty)
        {
            if (!InBounds(tx, ty)) return false;
            int toIdx = Idx(tx, ty);
            if ((CellType)grid.Type[toIdx] != CellType.EMPTY) return false;
            int fromIdx = Idx(fx, fy);

            Swap(grid.Type,         fromIdx, toIdx);
            Swap(grid.Temperature,  fromIdx, toIdx);
            Swap(grid.Lifetime,     fromIdx, toIdx);
            Swap(grid.Flammability, fromIdx, toIdx);

            cellUpdateTick[toIdx] = currentTick;
            chunks.MarkMoved(tx, ty);
            chunks.MarkMoved(fx, fy);
            return true;
        }

        static void Swap(byte[] arr, int a, int b)
        {
            byte t = arr[a]; arr[a] = arr[b]; arr[b] = t;
        }

        void ShuffleX()
        {
            for (int i = xShuffle.Length - 1; i > 0; i--)
            {
                int j = rng.Next(0, i + 1);
                int t = xShuffle[i]; xShuffle[i] = xShuffle[j]; xShuffle[j] = t;
            }
        }

        // ── FIRE (불) ────────────────────────────────────────────────────

        void TickFire()
        {
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                int idx = Idx(x, y);
                if (grid.Type[idx] != (byte)CellType.FIRE) continue;
                if (cellUpdateTick[idx] == currentTick) continue;
                cellUpdateTick[idx] = currentTick;

                if (grid.Lifetime[idx] == 0)
                {
                    grid.Type[idx]        = (byte)(rng.NextDouble() < SimulationConstants.ASH_CHANCE
                        ? CellType.POWDER_ASH : CellType.EMPTY);
                    grid.Temperature[idx] = 0;
                    chunks.MarkMoved(x, y);
                    continue;
                }
                grid.Lifetime[idx]--;

                EmitHeat(x, y);
                PropagateFireToNeighbors(x, y);

                if (y + 1 < H)
                {
                    int aboveIdx = Idx(x, y + 1);
                    if ((CellType)grid.Type[aboveIdx] == CellType.EMPTY
                        && rng.NextDouble() < SimulationConstants.SMOKE_CHANCE)
                    {
                        grid.Type[aboveIdx]      = (byte)CellType.GAS_SMOKE;
                        grid.Lifetime[aboveIdx]  = (byte)SimulationConstants.SMOKE_LIFE_MAX;
                        cellUpdateTick[aboveIdx] = currentTick;
                        chunks.MarkMoved(x, y + 1);
                    }
                }

                if (AdjacentToWater(x, y))
                {
                    grid.Type[idx]     = (byte)CellType.POWDER_ASH;
                    grid.Lifetime[idx] = 0;
                    chunks.MarkMoved(x, y);
                }
            }
        }

        void EmitHeat(int x, int y)
        {
            int[] dx = {  0,  0, -1, 1 };
            int[] dy = {  1, -1,  0, 0 };
            for (int d = 0; d < 4; d++)
            {
                int nx = x + dx[d], ny = y + dy[d];
                if (!InBounds(nx, ny)) continue;
                int ni   = Idx(nx, ny);
                int temp = grid.Temperature[ni] + SimulationConstants.HEAT_EMISSION;
                grid.Temperature[ni] = (byte)Math.Min(255, temp);

                if (grid.Temperature[ni] >= SimulationConstants.AUTO_IGNITE_TEMP
                    && ((CellType)grid.Type[ni]).IsFuel()
                    && (CellType)grid.Type[ni] != CellType.FIRE)
                    IgniteCell(nx, ny);
            }
        }

        void PropagateFireToNeighbors(int x, int y)
        {
            int[] dx = {  0,  0, -1, 1 };
            int[] dy = {  1, -1,  0, 0 };
            for (int d = 0; d < 4; d++)
            {
                int nx = x + dx[d], ny = y + dy[d];
                if (!InBounds(nx, ny)) continue;
                int      ni = Idx(nx, ny);
                CellType nt = (CellType)grid.Type[ni];
                if (!nt.IsFuel()) continue;
                if (rng.NextDouble() < grid.Flammability[ni] / 255.0)
                    IgniteCell(nx, ny);
            }
        }

        void IgniteCell(int x, int y)
        {
            int idx         = Idx(x, y);
            grid.Type[idx]  = (byte)CellType.FIRE;
            int variance    = rng.Next(-10, 11);
            int life        = SimulationConstants.FIRE_LIFE_MAX + variance;
            grid.Lifetime[idx]   = (byte)Math.Max(1, Math.Min(255, life));
            cellUpdateTick[idx]  = currentTick;
            chunks.MarkMoved(x, y);
        }

        bool AdjacentToWater(int x, int y) =>
            GetCell(x, y + 1) == CellType.LIQUID_WATER ||
            GetCell(x, y - 1) == CellType.LIQUID_WATER ||
            GetCell(x - 1, y) == CellType.LIQUID_WATER ||
            GetCell(x + 1, y) == CellType.LIQUID_WATER;

        // ── GAS_SMOKE (연기) ─────────────────────────────────────────────

        void TickSmoke()
        {
            for (int y = H - 1; y >= 0; y--)
            for (int x = 0; x < W; x++)
            {
                int idx = Idx(x, y);
                if (grid.Type[idx] != (byte)CellType.GAS_SMOKE) continue;
                if (cellUpdateTick[idx] == currentTick) continue;
                cellUpdateTick[idx] = currentTick;

                if (grid.Lifetime[idx] == 0)
                {
                    grid.Type[idx] = (byte)CellType.EMPTY;
                    chunks.MarkMoved(x, y);
                    continue;
                }
                grid.Lifetime[idx]--;

                if (TryMove(x, y, x, y + 1)) continue;

                bool left = rng.NextDouble() < 0.5;
                if (!TryMove(x, y, x + (left ? -1 :  1), y))
                     TryMove(x, y, x + (left ?  1 : -1), y);
            }
        }

        // ── LIQUID (액체) ────────────────────────────────────────────────

        void TickLiquid()
        {
            for (int y = 0; y < H; y++)
            {
                bool leftFirst = rng.NextDouble() < 0.5;
                for (int xi = 0; xi < W; xi++)
                {
                    int      x   = leftFirst ? xi : (W - 1 - xi);
                    int      idx = Idx(x, y);
                    CellType ct  = (CellType)grid.Type[idx];
                    if (!ct.IsLiquid()) continue;
                    if (cellUpdateTick[idx] == currentTick) continue;
                    cellUpdateTick[idx] = currentTick;

                    if (ct == CellType.LIQUID_LAVA)
                    {
                        int t = grid.Temperature[idx] - SimulationConstants.COOLING_RATE;
                        grid.Temperature[idx] = (byte)Math.Max(0, t);
                        if (grid.Temperature[idx] <= SimulationConstants.LAVA_SOLIDIFY_THRESHOLD)
                        {
                            grid.Type[idx] = (byte)CellType.SOLID_STATIC;
                            chunks.MarkMoved(x, y);
                            continue;
                        }
                        EmitHeat(x, y);
                    }

                    if (TryMove(x, y, x, y - 1)) continue;

                    bool diagLeft = rng.NextDouble() < 0.5;
                    if (TryMove(x, y, x + (diagLeft ? -1 :  1), y - 1)) continue;
                    if (TryMove(x, y, x + (diagLeft ?  1 : -1), y - 1)) continue;

                    int  disp   = ct == CellType.LIQUID_LAVA
                        ? SimulationConstants.LAVA_DISPERSION
                        : SimulationConstants.WATER_DISPERSION;
                    bool goLeft = rng.NextDouble() < 0.5;
                    bool moved  = false;
                    for (int step = 1; step <= disp && !moved; step++)
                    {
                        if (TryMove(x, y, x + (goLeft ? -step :  step), y)) { moved = true; break; }
                        if (TryMove(x, y, x + (goLeft ?  step : -step), y)) { moved = true; break; }
                    }

                    if (!moved && GetCell(x, y + 1).IsLiquid())
                    {
                        bool pl = rng.NextDouble() < 0.5;
                        if (!TryMove(x, y, x + (pl ? -1 :  1), y))
                             TryMove(x, y, x + (pl ?  1 : -1), y);
                    }
                }
            }
        }

        // ── POWDER (분말) ─────────────────────────────────────────────────

        void TickPowder()
        {
            for (int y = 0; y < H; y++)
            for (int xi = 0; xi < W; xi++)
            {
                int      x   = xShuffle[xi];
                int      idx = Idx(x, y);
                CellType ct  = (CellType)grid.Type[idx];
                if (ct != CellType.POWDER_SAND && ct != CellType.POWDER_ASH) continue;
                if (cellUpdateTick[idx] == currentTick) continue;
                cellUpdateTick[idx] = currentTick;

                if (ct == CellType.POWDER_ASH && rng.NextDouble() < SimulationConstants.ASH_FLOAT_CHANCE)
                {
                    if (TryMove(x, y, x, y + 1))
                    {
                        grid.Lifetime[Idx(x, y + 1)] = 0;
                        continue;
                    }
                }

                if (TryMove(x, y, x, y - 1))
                {
                    grid.Lifetime[Idx(x, y - 1)] = 0;
                    continue;
                }

                bool dl  = rng.NextDouble() < 0.5;
                int  dx1 = dl ? -1 : 1;
                int  dx2 = dl ?  1 : -1;
                if (TryMove(x, y, x + dx1, y - 1))
                {
                    if (InBounds(x + dx1, y - 1)) grid.Lifetime[Idx(x + dx1, y - 1)] = 0;
                    continue;
                }
                if (TryMove(x, y, x + dx2, y - 1))
                {
                    if (InBounds(x + dx2, y - 1)) grid.Lifetime[Idx(x + dx2, y - 1)] = 0;
                    continue;
                }

                if (grid.Lifetime[idx] < 255) grid.Lifetime[idx]++;
                if (grid.Lifetime[idx] >= SimulationConstants.SLEEP_THRESHOLD)
                {
                    grid.Type[idx]     = (byte)CellType.SOLID_STATIC;
                    grid.Lifetime[idx] = 0;
                    chunks.MarkMoved(x, y);
                }
            }
        }

        // ── SOLID_DEBRIS (잔해) ───────────────────────────────────────────

        void TickDebris()
        {
            for (int y = 0; y < H; y++)
            for (int xi = 0; xi < W; xi++)
            {
                int x   = xShuffle[xi];
                int idx = Idx(x, y);
                if (grid.Type[idx] != (byte)CellType.SOLID_DEBRIS) continue;
                if (cellUpdateTick[idx] == currentTick) continue;
                cellUpdateTick[idx] = currentTick;

                if (ShouldWakeDebris(x, y) && grid.Lifetime[idx] >= SimulationConstants.SLEEP_THRESHOLD)
                    grid.Lifetime[idx] = 0;

                if (grid.Lifetime[idx] >= SimulationConstants.SLEEP_THRESHOLD)
                {
                    grid.Type[idx]     = (byte)CellType.SOLID_STATIC;
                    grid.Lifetime[idx] = 0;
                    chunks.MarkMoved(x, y);
                    continue;
                }

                if (TryMove(x, y, x, y - 1))
                {
                    grid.Lifetime[Idx(x, y - 1)] = 0;
                    continue;
                }

                bool dl  = rng.NextDouble() < 0.5;
                int  dx1 = dl ? -1 : 1;
                int  dx2 = dl ?  1 : -1;
                if (TryMove(x, y, x + dx1, y - 1))
                {
                    if (InBounds(x + dx1, y - 1)) grid.Lifetime[Idx(x + dx1, y - 1)] = 0;
                    continue;
                }
                if (TryMove(x, y, x + dx2, y - 1))
                {
                    if (InBounds(x + dx2, y - 1)) grid.Lifetime[Idx(x + dx2, y - 1)] = 0;
                    continue;
                }

                if (grid.Lifetime[idx] < 255) grid.Lifetime[idx]++;
            }
        }

        bool ShouldWakeDebris(int x, int y)
        {
            CellType n;
            n = GetCell(x, y + 1); if (n.IsLiquid() || n == CellType.POWDER_SAND || n == CellType.POWDER_ASH || n == CellType.FIRE) return true;
            n = GetCell(x, y - 1); if (n.IsLiquid() || n == CellType.POWDER_SAND || n == CellType.POWDER_ASH || n == CellType.FIRE) return true;
            n = GetCell(x - 1, y); if (n.IsLiquid() || n == CellType.POWDER_SAND || n == CellType.POWDER_ASH || n == CellType.FIRE) return true;
            n = GetCell(x + 1, y); if (n.IsLiquid() || n == CellType.POWDER_SAND || n == CellType.POWDER_ASH || n == CellType.FIRE) return true;
            return false;
        }
    }
}
