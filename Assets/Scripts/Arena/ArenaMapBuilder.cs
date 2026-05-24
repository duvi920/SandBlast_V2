using System.Collections.Generic;
using UnityEngine;

namespace SandBlast.Arena
{
    // 맵 빌드 유틸 — 규칙 목록을 PixelGridSingleton 에 적용하거나
    // 에디터 툴에서 프리셋 템플릿을 자동 생성한다.
    public static class ArenaMapBuilder
    {
        // ── 런타임: 규칙 적용 ─────────────────────────────────────────

        public static void Apply(
            MapTemplate template,
            ref PixelGridSingleton grid,
            ref ChunkManagerSingleton chunks)
        {
            // 1. 셀 규칙 적용
            foreach (var rule in template.CellRules)
                ApplyRule(rule, ref grid, ref chunks);

            // 2. 스폰 지점 주변 파괴 불가 영역
            int r = template.IndestructibleRadius;
            foreach (var sp in template.SpawnPoints)
                FillCircle(ref grid, ref chunks,
                    sp.GridCell.x, sp.GridCell.y, r,
                    CellType.SOLID_INDESTRUCTIBLE, 0);
        }

        static void ApplyRule(CellFillRule rule, ref PixelGridSingleton grid, ref ChunkManagerSingleton chunks)
        {
            switch (rule.Shape)
            {
                case FillShape.Rect:
                    FillRect(ref grid, ref chunks, rule.X, rule.Y, rule.W, rule.H, rule.CellType, rule.Flammability);
                    break;
                case FillShape.HLine:
                    FillRect(ref grid, ref chunks, rule.X, rule.Y, rule.W, 1, rule.CellType, rule.Flammability);
                    break;
                case FillShape.VLine:
                    FillRect(ref grid, ref chunks, rule.X, rule.Y, 1, rule.H, rule.CellType, rule.Flammability);
                    break;
            }
        }

        public static void FillRect(
            ref PixelGridSingleton grid, ref ChunkManagerSingleton chunks,
            int x, int y, int w, int h, CellType type, byte flammability)
        {
            for (int dy = 0; dy < h; dy++)
            for (int dx = 0; dx < w; dx++)
            {
                int gx = x + dx, gy = y + dy;
                if (!grid.InBounds(gx, gy)) continue;
                int idx = grid.Index(gx, gy);
                grid.Type[idx]        = (byte)type;
                grid.Flammability[idx]= flammability;
                grid.Temperature[idx] = 0;
                grid.Lifetime[idx]    = 0;
                chunks.MarkMoved(gx, gy);
            }
        }

        public static void FillCircle(
            ref PixelGridSingleton grid, ref ChunkManagerSingleton chunks,
            int cx, int cy, int r, CellType type, byte flammability)
        {
            for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
            {
                if (dx * dx + dy * dy > r * r) continue;
                int gx = cx + dx, gy = cy + dy;
                if (!grid.InBounds(gx, gy)) continue;
                int idx = grid.Index(gx, gy);
                grid.Type[idx]         = (byte)type;
                grid.Flammability[idx] = flammability;
                grid.Temperature[idx]  = 0;
                grid.Lifetime[idx]     = 0;
                chunks.MarkMoved(gx, gy);
            }
        }

        // ── 에디터: 프리셋 템플릿 생성 ────────────────────────────────

        // 1v1 아레나 프리셋 (512×256, 대칭 구조)
        public static MapTemplate CreateArena1v1Preset()
        {
            var t = ScriptableObject.CreateInstance<MapTemplate>();
            t.name       = "Arena_1v1";
            t.Mode       = MapMode.OneVsOne;
            t.GridWidth  = 512;
            t.GridHeight = 256;
            t.IndestructibleRadius = 6;

            int W = 512, H = 256;
            t.CellRules = new List<CellFillRule>
            {
                // 바닥 (8행)
                new() { Shape=FillShape.Rect, X=0,   Y=0,   W=W,  H=8,  CellType=CellType.SOLID_STATIC, Flammability=0 },
                // 천장 (4행)
                new() { Shape=FillShape.Rect, X=0,   Y=H-4, W=W,  H=4,  CellType=CellType.SOLID_STATIC, Flammability=0 },
                // 좌측 벽 (8px)
                new() { Shape=FillShape.Rect, X=0,   Y=0,   W=8,  H=H,  CellType=CellType.SOLID_STATIC, Flammability=0 },
                // 우측 벽 (8px)
                new() { Shape=FillShape.Rect, X=W-8, Y=0,   W=8,  H=H,  CellType=CellType.SOLID_STATIC, Flammability=0 },

                // 좌측 스폰 플랫폼 (흙)
                new() { Shape=FillShape.Rect, X=40,  Y=30,  W=80, H=10, CellType=CellType.SOLID_DIRT, Flammability=0 },
                // 우측 스폰 플랫폼 (흙)
                new() { Shape=FillShape.Rect, X=392, Y=30,  W=80, H=10, CellType=CellType.SOLID_DIRT, Flammability=0 },

                // 중앙 좌 나무 플랫폼
                new() { Shape=FillShape.Rect, X=168, Y=50,  W=60, H=8,  CellType=CellType.SOLID_WOOD,
                    Flammability=SimulationConstants.FLAMMABILITY_WOOD },
                // 중앙 우 나무 플랫폼
                new() { Shape=FillShape.Rect, X=284, Y=50,  W=60, H=8,  CellType=CellType.SOLID_WOOD,
                    Flammability=SimulationConstants.FLAMMABILITY_WOOD },

                // 중앙 화약 블록 (트랩)
                new() { Shape=FillShape.Rect, X=244, Y=8,   W=24, H=16, CellType=CellType.SOLID_GUNPOWDER,
                    Flammability=SimulationConstants.FLAMMABILITY_EXPLOSIVE },

                // 중앙 하단 용암 웅덩이
                new() { Shape=FillShape.Rect, X=200, Y=8,   W=112,H=6,  CellType=CellType.LIQUID_LAVA,  Flammability=0 },

                // 상단 중앙 자원 플랫폼 (흙)
                new() { Shape=FillShape.Rect, X=200, Y=130, W=112,H=10, CellType=CellType.SOLID_DIRT, Flammability=0 },
            };

            t.SpawnPoints = new[]
            {
                new MapSpawnPoint { GridCell = new Vector2Int(80,  45), TeamId = 0 },
                new MapSpawnPoint { GridCell = new Vector2Int(431, 45), TeamId = 1 },
            };

            t.WandSpawnPoints = new[]
            {
                new MapWandSpawn { GridCell = new Vector2Int(210, 145), WandPresetId = 1 }, // 폭발 완드
                new MapWandSpawn { GridCell = new Vector2Int(280, 145), WandPresetId = 2 }, // 용암 완드
                new MapWandSpawn { GridCell = new Vector2Int(100, 42),  WandPresetId = 0 }, // 기본 완드 (A스폰 근처)
                new MapWandSpawn { GridCell = new Vector2Int(411, 42),  WandPresetId = 3 }, // 관통 완드 (B스폰 근처)
            };

            return t;
        }

        // 2v2 아레나 프리셋 (1024×256, 팀 진영 분리)
        public static MapTemplate CreateArena2v2Preset()
        {
            var t = ScriptableObject.CreateInstance<MapTemplate>();
            t.name       = "Arena_2v2";
            t.Mode       = MapMode.TwoVsTwo;
            t.GridWidth  = 1024;
            t.GridHeight = 256;
            t.IndestructibleRadius = 8;

            int W = 1024, H = 256, M = W / 2;
            t.CellRules = new List<CellFillRule>
            {
                // 바닥·천장·벽
                new() { Shape=FillShape.Rect, X=0,   Y=0,   W=W,  H=8,  CellType=CellType.SOLID_STATIC, Flammability=0 },
                new() { Shape=FillShape.Rect, X=0,   Y=H-4, W=W,  H=4,  CellType=CellType.SOLID_STATIC, Flammability=0 },
                new() { Shape=FillShape.Rect, X=0,   Y=0,   W=8,  H=H,  CellType=CellType.SOLID_STATIC, Flammability=0 },
                new() { Shape=FillShape.Rect, X=W-8, Y=0,   W=8,  H=H,  CellType=CellType.SOLID_STATIC, Flammability=0 },

                // A팀 진영 흙 지형
                new() { Shape=FillShape.Rect, X=40,  Y=30,  W=120,H=12, CellType=CellType.SOLID_DIRT,    Flammability=0 },
                new() { Shape=FillShape.Rect, X=40,  Y=80,  W=80, H=12, CellType=CellType.SOLID_DIRT,    Flammability=0 },

                // B팀 진영 흙 지형 (대칭)
                new() { Shape=FillShape.Rect, X=W-160,Y=30, W=120,H=12, CellType=CellType.SOLID_DIRT,    Flammability=0 },
                new() { Shape=FillShape.Rect, X=W-120,Y=80, W=80, H=12, CellType=CellType.SOLID_DIRT,    Flammability=0 },

                // 중앙 나무 플랫폼 (수직 복층)
                new() { Shape=FillShape.Rect, X=M-80, Y=50, W=160,H=8,  CellType=CellType.SOLID_WOOD,
                    Flammability=SimulationConstants.FLAMMABILITY_WOOD },
                new() { Shape=FillShape.Rect, X=M-50, Y=110,W=100,H=8,  CellType=CellType.SOLID_WOOD,
                    Flammability=SimulationConstants.FLAMMABILITY_WOOD },

                // 중앙 화약 블록
                new() { Shape=FillShape.Rect, X=M-20, Y=8,  W=40, H=16, CellType=CellType.SOLID_GUNPOWDER,
                    Flammability=SimulationConstants.FLAMMABILITY_EXPLOSIVE },

                // 중앙 용암 웅덩이
                new() { Shape=FillShape.Rect, X=M-100,Y=8,  W=200,H=6,  CellType=CellType.LIQUID_LAVA,   Flammability=0 },

                // 우회 통로 흙 플랫폼 (상단)
                new() { Shape=FillShape.Rect, X=M-160,Y=150,W=320,H=10, CellType=CellType.SOLID_DIRT,    Flammability=0 },
            };

            t.SpawnPoints = new[]
            {
                new MapSpawnPoint { GridCell = new Vector2Int(80,   45), TeamId = 0 },
                new MapSpawnPoint { GridCell = new Vector2Int(180,  45), TeamId = 0 },
                new MapSpawnPoint { GridCell = new Vector2Int(W-80, 45), TeamId = 1 },
                new MapSpawnPoint { GridCell = new Vector2Int(W-180,45), TeamId = 1 },
            };

            t.WandSpawnPoints = new[]
            {
                new MapWandSpawn { GridCell = new Vector2Int(M,    125), WandPresetId = 1 },
                new MapWandSpawn { GridCell = new Vector2Int(M+30, 125), WandPresetId = 2 },
                new MapWandSpawn { GridCell = new Vector2Int(M-50, 165), WandPresetId = 0 },
                new MapWandSpawn { GridCell = new Vector2Int(M+50, 165), WandPresetId = 3 },
            };

            return t;
        }
    }
}
