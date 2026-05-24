using UnityEngine;
using UnityEditor;

namespace SandBlast.Editor
{
    public class SandBlastSetupWindow : EditorWindow
    {
        int     gridWidth      = 320;
        int     gridHeight     = 180;
        float   pixelsPerUnit  = 16f;
        Vector2 gridOrigin     = Vector2.zero;
        float   ticksPerSecond = 30f;

        [MenuItem("SandBlast/Setup Scene %#s")]
        static void Open() => GetWindow<SandBlastSetupWindow>("SandBlast Setup");

        void OnGUI()
        {
            EditorGUILayout.LabelField("Grid Settings", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope(1))
            {
                gridWidth      = EditorGUILayout.IntField("Width (px)",        gridWidth);
                gridHeight     = EditorGUILayout.IntField("Height (px)",       gridHeight);
                pixelsPerUnit  = EditorGUILayout.FloatField("Pixels Per Unit", pixelsPerUnit);
                gridOrigin     = EditorGUILayout.Vector2Field("Origin (world)", gridOrigin);
                ticksPerSecond = EditorGUILayout.FloatField("Ticks / Second",  ticksPerSecond);
            }

            float worldW = gridWidth  / pixelsPerUnit;
            float worldH = gridHeight / pixelsPerUnit;
            EditorGUILayout.HelpBox(
                $"World size: {worldW:F1} × {worldH:F1} units", MessageType.None);

            EditorGUILayout.Space(6);

            bool bootstrapExists = FindFirstObjectByType<SandBlastBootstrapAuthoring>() != null;
            if (bootstrapExists)
            {
                EditorGUILayout.HelpBox(
                    "SandBlastBootstrapAuthoring already exists in scene.", MessageType.Warning);
                using (new EditorGUI.DisabledScope(true))
                    GUILayout.Button("Setup Scene");
            }
            else
            {
                if (GUILayout.Button("Setup Scene", GUILayout.Height(36)))
                    SetupScene();
            }

            EditorGUILayout.Space(4);

            using (new EditorGUI.DisabledScope(!bootstrapExists))
            {
                if (GUILayout.Button("Remove SandBlast Objects"))
                    TeardownScene();
            }

            EditorGUILayout.Space(8);

            EditorGUILayout.LabelField("Player (ECS)", EditorStyles.boldLabel);
            bool playerExists = FindFirstObjectByType<SandBlast.Authoring.PlayerAuthoring>() != null;
            using (new EditorGUI.DisabledScope(playerExists || !bootstrapExists))
            {
                if (GUILayout.Button(playerExists ? "ECS Player Already Exists" : "Add ECS Player"))
                    AddPlayer();
            }

            EditorGUILayout.Space(8);

            EditorGUILayout.LabelField("Quick Spawn (Play Mode)", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Water"))  SpawnCenter(CellType.LIQUID_WATER);
                    if (GUILayout.Button("Lava"))   SpawnCenter(CellType.LIQUID_LAVA);
                    if (GUILayout.Button("Sand"))   SpawnCenter(CellType.POWDER_SAND);
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Fire"))   SpawnCenter(CellType.FIRE);
                    if (GUILayout.Button("Smoke"))  SpawnCenter(CellType.GAS_SMOKE);
                    if (GUILayout.Button("Debris")) SpawnCenter(CellType.SOLID_DEBRIS);
                }
                if (GUILayout.Button("Spawn Floor"))
                    SpawnFloor();

                if (GUILayout.Button("Clear Grid"))
                    PixelGridAPI.ClearGrid();
            }
        }

        void SetupScene()
        {
            float worldW = gridWidth  / pixelsPerUnit;
            float worldH = gridHeight / pixelsPerUnit;

            // ECS 부트스트랩 Authoring 게임오브젝트
            GameObject bootstrapGO = new GameObject("SandBlastBootstrap");
            Undo.RegisterCreatedObjectUndo(bootstrapGO, "Setup SandBlast Scene");

            var authoring = bootstrapGO.AddComponent<SandBlastBootstrapAuthoring>();
            authoring.GridWidth      = gridWidth;
            authoring.GridHeight     = gridHeight;
            authoring.PixelsPerUnit  = pixelsPerUnit;
            authoring.GridOrigin     = gridOrigin;
            authoring.TicksPerSecond = ticksPerSecond;

            bootstrapGO.AddComponent<SandBlastDebugPainter>();

            // PixelLayer — SpriteRenderer + 렌더러 스크립트
            GameObject pixelLayerGO = new GameObject("PixelLayer");
            Undo.RegisterCreatedObjectUndo(pixelLayerGO, "Setup SandBlast Scene");
            pixelLayerGO.transform.position = new Vector3(
                gridOrigin.x + worldW * 0.5f,
                gridOrigin.y + worldH * 0.5f,
                0.1f);

            pixelLayerGO.AddComponent<SpriteRenderer>();
            pixelLayerGO.AddComponent<PixelLayerRenderer>();

            Camera cam = Camera.main;
            if (cam != null)
            {
                Undo.RecordObject(cam.transform, "Setup SandBlast Scene");
                Undo.RecordObject(cam,           "Setup SandBlast Scene");
                cam.orthographic     = true;
                cam.orthographicSize = worldH * 0.5f;
                cam.transform.position = new Vector3(
                    gridOrigin.x + worldW * 0.5f,
                    gridOrigin.y + worldH * 0.5f,
                    -10f);
            }

            Selection.activeGameObject = bootstrapGO;
            Debug.Log($"[SandBlast] Scene setup complete. Grid: {gridWidth}×{gridHeight} px  " +
                      $"({worldW:F1}×{worldH:F1} world units). Press Play to simulate.");
        }

        void TeardownScene()
        {
            var bootstrap = FindFirstObjectByType<SandBlastBootstrapAuthoring>();
            if (bootstrap != null)
                Undo.DestroyObjectImmediate(bootstrap.gameObject);

            var pixelLayer = FindFirstObjectByType<PixelLayerRenderer>();
            if (pixelLayer != null)
                Undo.DestroyObjectImmediate(pixelLayer.gameObject);
        }

        void AddPlayer()
        {
            float worldW = gridWidth  / pixelsPerUnit;
            float worldH = gridHeight / pixelsPerUnit;

            Vector3 spawnPos = new Vector3(
                gridOrigin.x + worldW * 0.5f,
                gridOrigin.y + worldH * 0.35f,
                0f);

            // Play 모드에서는 Baker가 돌지 않으므로(=베이킹 없음) Authoring을 붙여도 ECS 엔티티가 생기지 않는다.
            // 따라서 런타임에는 즉시 ECS 엔티티를 생성해 "생성하자마자 이동"이 되게 한다.
            if (Application.isPlaying)
            {
                var (world, entity) = SandBlast.RuntimePlayerSpawner.SpawnLocalPlayer(spawnPos);

                // 화면에서 확실히 보이도록 엔티티를 따라다니는 간단한 비주얼을 생성
                var visualRoot = new GameObject("Player_Runtime");
                visualRoot.transform.position = spawnPos;

                var follower = visualRoot.AddComponent<SandBlast.EntityTransformFollower>();
                follower.WorldNameHint = world?.Name;
                follower.EntityIndex   = entity.Index;
                follower.EntityVersion = entity.Version;

                var runtimeSr = visualRoot.AddComponent<SpriteRenderer>();
                runtimeSr.color        = new Color(0.3f, 0.8f, 1f, 1f);
                runtimeSr.sortingOrder = 2;
                var runtimeTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                runtimeTex.SetPixel(0, 0, Color.white);
                runtimeTex.Apply();
                runtimeSr.sprite = Sprite.Create(runtimeTex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);

                Selection.activeGameObject = visualRoot;
                Debug.Log("[SandBlast] (Play Mode) Runtime ECS Player 생성 완료. A/D 이동, Space 점프.");
                return;
            }

            GameObject playerGO = new GameObject("Player_ECS");
            Undo.RegisterCreatedObjectUndo(playerGO, "Add ECS Player");
            playerGO.transform.position = spawnPos;

            // 1. ECS Player Authoring 추가
            var playerAuth = playerGO.AddComponent<SandBlast.Authoring.PlayerAuthoring>();
            playerAuth.MoveSpeed = 6.0f;
            playerAuth.JumpForce = 14.0f;
            
            // 2. 비주얼 (플레이스홀더)
            GameObject visual = new GameObject("Visual");
            Undo.RegisterCreatedObjectUndo(visual, "Add ECS Player");
            visual.transform.SetParent(playerGO.transform, worldPositionStays: false);
            visual.transform.localScale = new Vector3(0.75f, 1.5f, 1f);

            var sr = visual.AddComponent<SpriteRenderer>();
            sr.color        = new Color(0.3f, 0.8f, 1f, 1f);
            sr.sortingOrder = 1;

            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);

            // 3. (선택적) SPUM 구성 요소가 자식에 있다면 자동으로 연결 시도 가능
            // playerAuth.SpumPrefabs = playerGO.GetComponentInChildren<SPUM.SPUM_Prefabs>();

            Camera cam = Camera.main;
            if (cam != null)
            {
                Undo.RecordObject(cam.gameObject, "Add ECS Player");
                var pc = cam.gameObject.GetComponent<PlayerCamera>()
                      ?? cam.gameObject.AddComponent<PlayerCamera>();
                pc.Target = playerGO.transform;

                Undo.RecordObject(cam, "Add ECS Player");
                cam.orthographicSize = Mathf.Min(worldH * 0.5f, 8f);
                cam.transform.position = new Vector3(spawnPos.x, spawnPos.y, cam.transform.position.z);
            }

            Selection.activeGameObject = playerGO;
            Debug.Log("[SandBlast] ECS Player 추가 완료.\n" +
                      "Authoring: PlayerAuthoring 이 추가되었습니다.\n" +
                      "SPUM 사용 시: 자식에 SPUM 프리팹을 넣고 PlayerAuthoring의 SpumPrefabs 필드에 할당하세요.");
        }

        static void SpawnFloor()
        {
            if (!PixelGridAPI.TryGetGrid(out var grid, out var chunks)) return;

            int floorPx = Mathf.RoundToInt(3f * grid.PixelsPerUnit);
            int wallPx  = Mathf.RoundToInt(1f * grid.PixelsPerUnit);
            int w = grid.Width;
            int h = grid.Height;

            for (int y = 0; y < floorPx && y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int idx = grid.Index(x, y);
                grid.Type[idx]         = (byte)CellType.SOLID_STATIC;
                grid.Flammability[idx] = 0;
                chunks.MarkMoved(x, y);
            }

            for (int y = 0; y < h; y++)
            for (int x = 0; x < wallPx; x++)
            {
                int il = grid.Index(x, y);
                int ir = grid.Index(w - 1 - x, y);
                grid.Type[il] = (byte)CellType.SOLID_STATIC;
                grid.Type[ir] = (byte)CellType.SOLID_STATIC;
                chunks.MarkMoved(x, y);
                chunks.MarkMoved(w - 1 - x, y);
            }
        }

        static void SpawnCenter(CellType type)
        {
            if (!PixelGridAPI.TryGetGrid(out var grid, out _)) return;
            var origin = grid.GridOrigin;
            Vector2 center = new Vector2(
                origin.x + grid.Width  / grid.PixelsPerUnit * 0.5f,
                origin.y + grid.Height / grid.PixelsPerUnit * 0.5f);
            PixelGridAPI.SpawnCircle(center, 2f, type);
        }
    }
}
