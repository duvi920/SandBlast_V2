using Unity.Collections;
using UnityEngine;

namespace SandBlast
{
    // 매 틱 CA 그리드를 Texture2D 로 베이크해 SpriteRenderer 로 출력한다.
    // SpriteRenderer + Sprites/Default 셰이더는 URP / Built-in 모두 분홍색 없이 동작한다.
    // SOLID_RIGID / EMPTY 셀은 알파 0(투명) — 강체 스프라이트/메시가 아래에서 비쳐 보이게 함.
    [RequireComponent(typeof(SpriteRenderer))]
    public class PixelLayerRenderer : MonoBehaviour
    {
        Texture2D      tex;
        Color32[]      pixels;
        PixelGrid      grid;
        SpriteRenderer sr;

        static readonly Color32 C_EMPTY        = new Color32(0,   0,   0,   0);
        static readonly Color32 C_SOLID_STATIC = new Color32(100, 90,  80,  255);
        static readonly Color32 C_SOLID_RIGID  = new Color32(0,   0,   0,   0);   // 투명 — 강체가 자체 렌더링
        static readonly Color32 C_SOLID_DEBRIS = new Color32(130, 100, 70,  255);
        static readonly Color32 C_LIQUID_WATER = new Color32(30,  100, 200, 200);
        static readonly Color32 C_LIQUID_LAVA  = new Color32(220, 80,  20,  255);
        static readonly Color32 C_POWDER_SAND  = new Color32(200, 180, 100, 255);
        static readonly Color32 C_POWDER_ASH   = new Color32(160, 160, 155, 200);
        static readonly Color32 C_GAS_SMOKE    = new Color32(80,  80,  80,  150);

        // ECS 경로: PixelGridSingleton 치수로 Texture2D를 초기화한다
        public void Initialize(int width, int height, float pixelsPerUnit)
        {
            tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode   = TextureWrapMode.Clamp,
            };
            pixels = new Color32[width * height];

            sr = GetComponent<SpriteRenderer>();
            sr.sprite       = Sprite.Create(tex,
                                  new Rect(0, 0, width, height),
                                  new Vector2(0.5f, 0.5f),
                                  pixelsPerUnit);
            sr.sortingOrder = -1;
        }

        // ECS 경로: NativeArray<byte> 2채널로 Texture2D를 갱신한다 (PixelRenderSystem에서 호출)
        public void UpdateTexture(NativeArray<byte> type, NativeArray<byte> lifetime, int width, int height)
        {
            if (tex == null) Initialize(width, height, 16f); // 폴백: 아직 초기화 전이면 기본 PPU로 초기화

            int n = type.Length;
            if (pixels == null || pixels.Length != n)
                pixels = new Color32[n];

            for (int i = 0; i < n; i++)
                pixels[i] = CellColor((CellType)type[i], lifetime[i]);

            tex.SetPixels32(pixels);
            tex.Apply(false);
        }

        // 레거시 경로: PixelGrid(managed)로 초기화 — SandBlastEngine MonoBehaviour가 사용
        public void Initialize(PixelGrid g, float pixelsPerUnit)
        {
            grid = g;
            tex  = new Texture2D(g.Width, g.Height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode   = TextureWrapMode.Clamp,
            };
            pixels = new Color32[g.Width * g.Height];

            sr = GetComponent<SpriteRenderer>();
            // PPU 를 엔진과 동일하게 맞춰 스프라이트가 그리드 월드 크기와 1:1 대응하게 함
            sr.sprite       = Sprite.Create(tex,
                                  new Rect(0, 0, g.Width, g.Height),
                                  new Vector2(0.5f, 0.5f),
                                  pixelsPerUnit);
            sr.sortingOrder = -1; // 플레이어·강체보다 뒤에 렌더링
        }

        public void UpdateTexture()
        {
            if (grid == null) return;

            for (int i = 0; i < grid.Type.Length; i++)
                pixels[i] = CellColor((CellType)grid.Type[i], grid.Lifetime[i]);

            tex.SetPixels32(pixels);
            tex.Apply(false);
        }

        Color32 CellColor(CellType type, byte lifetime) => type switch
        {
            CellType.EMPTY        => C_EMPTY,
            CellType.SOLID_STATIC => C_SOLID_STATIC,
            CellType.SOLID_RIGID  => C_SOLID_RIGID,
            CellType.SOLID_DEBRIS => C_SOLID_DEBRIS,
            CellType.LIQUID_WATER => C_LIQUID_WATER,
            CellType.LIQUID_LAVA  => C_LIQUID_LAVA,
            CellType.POWDER_SAND  => C_POWDER_SAND,
            CellType.POWDER_ASH   => C_POWDER_ASH,
            CellType.FIRE         => FireColor(lifetime),
            CellType.GAS_SMOKE    => C_GAS_SMOKE,
            _                     => C_EMPTY,
        };

        // 불의 수명(lifetime) 비율에 따라 빨강 → 주황 → 노랑으로 색상 전환 (설계 문서 §6.5)
        static Color32 FireColor(byte lifetime)
        {
            float r = lifetime / (float)SimulationConstants.FIRE_LIFE_MAX;
            if (r > 0.6f) return new Color32(255, 50,  10,  255);
            if (r > 0.3f) return new Color32(255, 140, 20,  255);
            return                 new Color32(255, 220, 50,  255);
        }
    }
}
