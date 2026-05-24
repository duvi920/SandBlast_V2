using System;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SandBlast
{
    // 런타임 디버그 페인터 — 마우스로 셀을 직접 배치해 CA 시뮬레이션을 즉시 확인할 수 있다.
    // 좌클릭 드래그  → 선택한 셀 타입으로 페인팅
    // 우클릭 드래그  → 지우기 (EMPTY)
    // 스크롤 휠      → 브러시 크기 조절
    // Tab            → 셀 타입 순환 선택
    // F1             → 패널 표시/숨김
    public class SandBlastDebugPainter : MonoBehaviour
    {
        CellType selectedType = CellType.POWDER_SAND;
        float    brushRadius  = 0.5f;
        bool     showPanel    = true;

        readonly CellType[] palette =
        {
            CellType.POWDER_SAND,
            CellType.POWDER_ASH,
            CellType.LIQUID_WATER,
            CellType.LIQUID_LAVA,
            CellType.FIRE,
            CellType.GAS_SMOKE,
            CellType.SOLID_STATIC,
            CellType.SOLID_DEBRIS,
            CellType.EMPTY,
        };
        int paletteIndex;

        Camera _cam;

        Camera GetCamera()
        {
            if (_cam != null) return _cam;
            _cam = Camera.main;
            if (_cam != null) return _cam;
            // MainCamera 태그가 없는 씬에서도 동작하도록 폴백
            _cam = UnityEngine.Object.FindFirstObjectByType<Camera>();
            return _cam;
        }

        static readonly Color[] paletteColors =
        {
            new Color(0.78f, 0.70f, 0.39f),  // 모래
            new Color(0.63f, 0.63f, 0.61f),  // 재
            new Color(0.12f, 0.39f, 0.78f),  // 물
            new Color(0.86f, 0.31f, 0.08f),  // 용암
            new Color(1.00f, 0.35f, 0.05f),  // 불
            new Color(0.31f, 0.31f, 0.31f),  // 연기
            new Color(0.39f, 0.35f, 0.31f),  // 고체 (정적)
            new Color(0.51f, 0.39f, 0.27f),  // 잔해
            new Color(0.10f, 0.10f, 0.10f),  // 빈 공간
        };

        void Update()
        {
            var kb    = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null || mouse == null) return;

            if (kb.f1Key.wasPressedThisFrame) showPanel = !showPanel;

            if (kb.tabKey.wasPressedThisFrame)
            {
                paletteIndex = (paletteIndex + 1) % palette.Length;
                selectedType = palette[paletteIndex];
            }

            float scroll = mouse.scroll.ReadValue().y;
            if (scroll != 0f)
                brushRadius = Mathf.Clamp(brushRadius + scroll * 0.05f, 0.1f, 8f);

            bool lmb = mouse.leftButton.isPressed;
            bool rmb = mouse.rightButton.isPressed;
            if (lmb || rmb)
            {
                var cam = GetCamera();
                if (cam == null) return;

                Vector2 screenPos = mouse.position.ReadValue();
                // Ortho 카메라에서 ScreenPointToRay().origin은 보통 카메라 위치라 커서 좌표가 아니다.
                // 화면 좌표를 z=0(2D 월드 평면)으로 투영해 월드 좌표를 얻는다.
                float   zToPlane  = -cam.transform.position.z;
                Vector3 world3    = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, zToPlane));
                Vector2 worldPos  = new Vector2(world3.x, world3.y);
                CellType paint = rmb ? CellType.EMPTY : selectedType;
                byte     flamm = paint == CellType.SOLID_STATIC
                    ? SimulationConstants.FLAMMABILITY_WOOD : (byte)0;

                // GatherInputSystem의 스태틱 필드 업데이트 (로컬 플레이어의 PlayerInput으로 전송됨)
                GatherInputSystem.DebugBrushRadius  = brushRadius;
                GatherInputSystem.DebugSelectedType = (byte)paint;
                GatherInputSystem.DebugFlammability = flamm;
            }
        }

        static bool IsClientOnlyProcess() =>
            Array.Exists(Environment.GetCommandLineArgs(), a => a == "--virtual-project-clone");

        static void SendPaintRpc(Vector2 worldPos, float radius, CellType cellType, byte flamm)
        {
            foreach (var world in World.All)
            {
                if (!world.IsCreated) continue;
                if ((world.Flags & WorldFlags.GameClient) == 0) continue;

                var em = world.EntityManager;
                var e  = em.CreateEntity();
                em.AddComponentData(e, new TerrainPaintRpc
                {
                    WorldPos      = new float2(worldPos.x, worldPos.y),
                    WorldRadius   = radius,
                    CellType      = (byte)cellType,
                    Flammability  = flamm,
                });
                em.AddComponent<SendRpcCommandRequest>(e);
                break;
            }
        }

        void OnGUI()
        {
            if (!showPanel)
            {
                GUI.Label(new Rect(10, 10, 180, 20), "[F1] Show SandBlast Panel");
                return;
            }

            float panelW = 190f;
            float rowH   = 24f;
            float rows   = palette.Length + 5;
            float panelH = rows * rowH + 16f;

            GUI.Box(new Rect(8, 8, panelW, panelH), "SandBlast Debug [F1]");

            float y = 30f;
            float x = 16f;

            GUI.Label(new Rect(x, y, 170, 18), "Cell Type  [Tab to cycle]");
            y += rowH;

            for (int i = 0; i < palette.Length; i++)
            {
                bool active = i == paletteIndex;

                Color prev = GUI.color;
                GUI.color = paletteColors[i];
                GUI.DrawTexture(new Rect(x, y + 3f, 14f, 14f), Texture2D.whiteTexture);
                GUI.color = prev;

                if (GUI.Toggle(new Rect(x + 18f, y, 160f, rowH), active, palette[i].ToString())
                    && !active)
                {
                    paletteIndex = i;
                    selectedType = palette[i];
                }
                y += rowH;
            }

            y += 4f;

            GUI.Label(new Rect(x, y, 170, 18), $"Brush radius: {brushRadius:F1}  [Scroll]");
            y += rowH - 4f;
            brushRadius = GUI.HorizontalSlider(new Rect(x, y, 160f, 18f), brushRadius, 0.1f, 8f);
            y += rowH;

            y += 4f;

            GUI.Label(new Rect(x, y, 170, 18), "LMB paint   RMB erase");
        }
    }
}
