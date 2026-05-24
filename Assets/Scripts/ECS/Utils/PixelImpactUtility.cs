using Unity.Mathematics;

namespace SandBlast
{
    // PixelGridSingleton에 직접 쓰는 Burst 호환 충격 유틸리티.
    // BulletTerrainHitSystem 등 ECS 시스템에서 호출한다.
    public static class PixelImpactUtility
    {
        // 반경 내 POWDER/LIQUID 셀 제거, SOLID_STATIC 인접 셀에 FIRE 생성
        public static void BulletImpact(
            ref PixelGridSingleton grid,
            ref ChunkManagerSingleton chunks,
            float2 worldPos,
            float  radius)
        {
            int pixelRadius = (int)math.ceil(radius * grid.PixelsPerUnit);
            int2 center     = grid.WorldToGrid(worldPos);

            for (int dy = -pixelRadius; dy <= pixelRadius; dy++)
            for (int dx = -pixelRadius; dx <= pixelRadius; dx++)
            {
                if (dx * dx + dy * dy > pixelRadius * pixelRadius) continue;
                int gx = center.x + dx;
                int gy = center.y + dy;
                if (!grid.InBounds(gx, gy)) continue;

                int      idx = grid.Index(gx, gy);
                CellType ct  = (CellType)grid.Type[idx];

                if (ct.IsDestructibleByImpact())
                {
                    grid.Type[idx]         = (byte)CellType.EMPTY;
                    grid.Temperature[idx]  = 0;
                    grid.Lifetime[idx]     = 0;
                    grid.Flammability[idx] = 0;
                    chunks.MarkMoved(gx, gy);
                }
            }

            // SOLID_STATIC 인접 셀에 FIRE 생성
            SpawnFireAround(ref grid, ref chunks, center, pixelRadius + 1);
        }

        // 원형 범위에 특정 셀 타입 스탬프 (EMPTY 공간에만 생성)
        public static void StampCircle(
            ref PixelGridSingleton grid,
            ref ChunkManagerSingleton chunks,
            float2 worldPos,
            float radius,
            CellType type,
            int temperature = 0)
        {
            int pixelRadius = (int)math.ceil(radius * grid.PixelsPerUnit);
            int2 center     = grid.WorldToGrid(worldPos);

            for (int dy = -pixelRadius; dy <= pixelRadius; dy++)
            for (int dx = -pixelRadius; dx <= pixelRadius; dx++)
            {
                if (dx * dx + dy * dy > pixelRadius * pixelRadius) continue;
                int gx = center.x + dx;
                int gy = center.y + dy;
                if (!grid.InBounds(gx, gy)) continue;

                int idx = grid.Index(gx, gy);
                if (grid.Type[idx] == (byte)CellType.EMPTY)
                {
                    grid.Type[idx]        = (byte)type;
                    grid.Temperature[idx] = (byte)temperature;
                    chunks.MarkMoved(gx, gy);
                }
            }
        }

        // FIRE 셀 소화 및 온도 초기화
        public static void ExtinguishCircle(
            ref PixelGridSingleton grid,
            ref ChunkManagerSingleton chunks,
            float2 worldPos,
            float radius)
        {
            int pixelRadius = (int)math.ceil(radius * grid.PixelsPerUnit);
            int2 center     = grid.WorldToGrid(worldPos);

            for (int dy = -pixelRadius; dy <= pixelRadius; dy++)
            for (int dx = -pixelRadius; dx <= pixelRadius; dx++)
            {
                if (dx * dx + dy * dy > pixelRadius * pixelRadius) continue;
                int gx = center.x + dx;
                int gy = center.y + dy;
                if (!grid.InBounds(gx, gy)) continue;

                int idx = grid.Index(gx, gy);
                if (grid.Type[idx] == (byte)CellType.FIRE)
                {
                    grid.Type[idx] = (byte)CellType.EMPTY;
                    chunks.MarkMoved(gx, gy);
                }
                grid.Temperature[idx] = 0;
            }
        }

        // 반경 내 온도 급감 및 액체 동결
        public static void FreezeCircle(
            ref PixelGridSingleton grid,
            ref ChunkManagerSingleton chunks,
            float2 worldPos,
            float radius)
        {
            int pixelRadius = (int)math.ceil(radius * grid.PixelsPerUnit);
            int2 center     = grid.WorldToGrid(worldPos);

            for (int dy = -pixelRadius; dy <= pixelRadius; dy++)
            for (int dx = -pixelRadius; dx <= pixelRadius; dx++)
            {
                if (dx * dx + dy * dy > pixelRadius * pixelRadius) continue;
                int gx = center.x + dx;
                int gy = center.y + dy;
                if (!grid.InBounds(gx, gy)) continue;

                int idx = grid.Index(gx, gy);
                grid.Temperature[idx] = 0;

                CellType ct = (CellType)grid.Type[idx];
                if (ct.IsLiquid())
                {
                    grid.Type[idx] = (byte)CellType.SOLID_STATIC;
                    chunks.MarkMoved(gx, gy);
                }
            }
        }

        // 라인을 따라 고온 발생 및 지형 파괴 (번개탄용)
        public static void ScorchLine(
            ref PixelGridSingleton grid,
            ref ChunkManagerSingleton chunks,
            float2 start,
            float2 end,
            float width)
        {
            int2 p1 = grid.WorldToGrid(start);
            int2 p2 = grid.WorldToGrid(end);

            int dx = math.abs(p2.x - p1.x), sx = p1.x < p2.x ? 1 : -1;
            int dy = -math.abs(p2.y - p1.y), sy = p1.y < p2.y ? 1 : -1;
            int err = dx + dy, e2;

            int2 current = p1;
            while (true)
            {
                // 충격 반경 적용
                BulletImpact(ref grid, ref chunks, grid.GridToWorld(current.x, current.y), width);

                // 고온 적용
                if (grid.InBounds(current.x, current.y))
                    grid.Temperature[grid.Index(current.x, current.y)] = 255;

                if (current.x == p2.x && current.y == p2.y) break;
                e2 = 2 * err;
                if (e2 >= dy) { err += dy; current.x += sx; }
                if (e2 <= dx) { err += dx; current.y += sy; }
            }
        }

        // 폭발 — BulletImpact보다 큰 반경, 모든 비정적 셀 제거
        public static void Explode(
            ref PixelGridSingleton grid,
            ref ChunkManagerSingleton chunks,
            float2 worldPos,
            float  radius,
            float  force)
        {
            int pixelRadius = (int)math.ceil(radius * grid.PixelsPerUnit);
            int2 center     = grid.WorldToGrid(worldPos);

            for (int dy = -pixelRadius; dy <= pixelRadius; dy++)
            for (int dx = -pixelRadius; dx <= pixelRadius; dx++)
            {
                if (dx * dx + dy * dy > pixelRadius * pixelRadius) continue;
                int gx = center.x + dx;
                int gy = center.y + dy;
                if (!grid.InBounds(gx, gy)) continue;

                int      idx = grid.Index(gx, gy);
                CellType ct  = (CellType)grid.Type[idx];

                // SOLID_INDESTRUCTIBLE·SOLID_BASALT·SOLID_RIGID 는 폭발로도 파괴 불가
                if (ct.IsIndestructible() || ct == CellType.EMPTY) continue;

                grid.Type[idx]         = (byte)CellType.EMPTY;
                grid.Temperature[idx]  = 0;
                grid.Lifetime[idx]     = 0;
                grid.Flammability[idx] = 0;
                chunks.MarkMoved(gx, gy);
            }

            SpawnFireAround(ref grid, ref chunks, center, pixelRadius + 2);
        }

        static void SpawnFireAround(
            ref PixelGridSingleton grid,
            ref ChunkManagerSingleton chunks,
            int2 center,
            int  radius)
        {
            for (int dy = -radius; dy <= radius; dy++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (dx * dx + dy * dy > radius * radius) continue;
                int gx = center.x + dx;
                int gy = center.y + dy;
                if (!grid.InBounds(gx, gy)) continue;

                int      idx = grid.Index(gx, gy);
                CellType ct  = (CellType)grid.Type[idx];

                if (ct == CellType.EMPTY)
                {
                    // SOLID_STATIC 이웃이 있을 때만 FIRE 생성
                    if (HasSolidStaticNeighbor(ref grid, gx, gy))
                    {
                        grid.Type[idx]    = (byte)CellType.FIRE;
                        grid.Lifetime[idx] = (byte)SimulationConstants.FIRE_LIFE_MAX;
                        chunks.MarkMoved(gx, gy);
                    }
                }
            }
        }

        static bool HasSolidStaticNeighbor(ref PixelGridSingleton grid, int gx, int gy)
        {
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                int nx = gx + dx, ny = gy + dy;
                if (!grid.InBounds(nx, ny)) continue;
                var ct = (CellType)grid.Type[grid.Index(nx, ny)];
                if (ct.IsSolid()) return true;
            }
            return false;
        }
    }
}
