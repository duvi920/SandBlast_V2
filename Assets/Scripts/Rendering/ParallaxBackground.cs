using UnityEngine;

namespace SandBlast
{
    // 카메라 이동 델타에 parallaxFactor를 곱해 배경 레이어를 느리게 스크롤시킨다.
    // SandBlastAutoSetup에서 자동 생성되며, Initialize() 호출 시 두 배경 GameObject를 직접 만든다.
    public class ParallaxBackground : MonoBehaviour
    {
        struct LayerInfo
        {
            public Transform transform;
            public float     parallaxFactor;
            public float     originX;
        }

        Camera      _camera;
        float       _cameraOriginX;
        bool        _originCaptured;
        LayerInfo[] _layers;

        // ── 레이어 정의 ──────────────────────────────────────────────────────────

        readonly struct LayerDef
        {
            public readonly float parallaxFactor;
            public readonly int   sortingOrder;
            public readonly float zPos;
            public readonly bool  isSky;
            public readonly string name;

            public LayerDef(float factor, int order, float z, bool sky, string n)
            {
                parallaxFactor = factor;
                sortingOrder   = order;
                zPos           = z;
                isSky          = sky;
                name           = n;
            }
        }

        static readonly LayerDef[] LayerDefs =
        {
            new LayerDef(0.15f, -10, 5f, true,  "ParallaxSky (Auto)"),
            new LayerDef(0.40f, -5,  3f, false, "ParallaxHills (Auto)"),
        };

        // ── 공개 API ─────────────────────────────────────────────────────────────

        public void Initialize(Camera cam, float worldW, float worldH,
                               Vector2 gridOrigin, float pixelsPerUnit)
        {
            _camera  = cam;
            _layers  = new LayerInfo[LayerDefs.Length];

            for (int i = 0; i < LayerDefs.Length; i++)
            {
                var def = LayerDefs[i];
                var go  = CreateLayer(def, worldW, worldH, gridOrigin, pixelsPerUnit);
                _layers[i] = new LayerInfo
                {
                    transform      = go.transform,
                    parallaxFactor = def.parallaxFactor,
                    originX        = go.transform.position.x,
                };
            }
            // 기준 X는 첫 LateUpdate에서 캡처 (CameraFollowSystem의 frame-1 snap 이후)
            _originCaptured = false;
        }

        // ── MonoBehaviour ────────────────────────────────────────────────────────

        void LateUpdate()
        {
            if (_camera == null || _layers == null) return;

            // CameraFollowSystem이 첫 프레임 snap을 마친 뒤에 기준점을 잡는다
            if (!_originCaptured)
            {
                _cameraOriginX = _camera.transform.position.x;
                for (int i = 0; i < _layers.Length; i++)
                    _layers[i].originX = _layers[i].transform.position.x;
                _originCaptured = true;
                return;
            }

            float camDeltaX = _camera.transform.position.x - _cameraOriginX;
            for (int i = 0; i < _layers.Length; i++)
            {
                ref var layer = ref _layers[i];
                var pos = layer.transform.position;
                layer.transform.position = new Vector3(
                    layer.originX + camDeltaX * layer.parallaxFactor,
                    pos.y,
                    pos.z);
            }
        }

        // ── 내부 헬퍼 ────────────────────────────────────────────────────────────

        GameObject CreateLayer(LayerDef def, float worldW, float worldH,
                               Vector2 gridOrigin, float pixelsPerUnit)
        {
            // 배경이 카메라 이동 범위 전체에서 화면을 완전히 덮을 수 있도록 충분히 크게 만든다
            int texW = Mathf.Clamp(Mathf.RoundToInt(worldW * 2.5f * pixelsPerUnit), 1, 16384);
            int texH = Mathf.Clamp(Mathf.RoundToInt(worldH * 2.0f * pixelsPerUnit), 1, 16384);

            var tex = def.isSky ? CreateSkyTexture(texW, texH) : CreateHillsTexture(texW, texH);

            var sprite = Sprite.Create(
                tex,
                new Rect(0, 0, texW, texH),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit);

            var go = new GameObject(def.name);
            DontDestroyOnLoad(go);
            go.transform.position = new Vector3(
                gridOrigin.x + worldW * 0.5f,
                gridOrigin.y + worldH * 0.5f,
                def.zPos);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = sprite;
            sr.sortingOrder = def.sortingOrder;

            return go;
        }

        // 수직 그라디언트: 하단 밝은 지평선 파랑 → 상단 짙은 하늘 파랑
        static Texture2D CreateSkyTexture(int texW, int texH)
        {
            var tex = new Texture2D(texW, texH, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode   = TextureWrapMode.Clamp,
            };

            var pixels = new Color32[texW * texH];
            var horizon = new Color32(135, 180, 220, 255);
            var zenith  = new Color32(30,  50,  120, 255);

            for (int y = 0; y < texH; y++)
            {
                float t = y / (float)(texH - 1);
                byte r = (byte)Mathf.RoundToInt(Mathf.Lerp(horizon.r, zenith.r, t));
                byte g = (byte)Mathf.RoundToInt(Mathf.Lerp(horizon.g, zenith.g, t));
                byte b = (byte)Mathf.RoundToInt(Mathf.Lerp(horizon.b, zenith.b, t));
                var c = new Color32(r, g, b, 255);
                int rowBase = y * texW;
                for (int x = 0; x < texW; x++)
                    pixels[rowBase + x] = c;
            }

            tex.SetPixels32(pixels);
            tex.Apply(false);
            return tex;
        }

        // 하단 33% 짙은 녹색 언덕 실루엣, 상단은 투명 (하늘 레이어가 비쳐 보임)
        static Texture2D CreateHillsTexture(int texW, int texH)
        {
            var tex = new Texture2D(texW, texH, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode   = TextureWrapMode.Clamp,
            };

            var pixels    = new Color32[texW * texH];
            var hillColor = new Color32(60, 90, 60, 255);
            var clear     = new Color32(0, 0, 0, 0);
            int hillTopY  = Mathf.RoundToInt(texH * 0.33f);

            for (int y = 0; y < texH; y++)
            {
                var c = y < hillTopY ? hillColor : clear;
                int rowBase = y * texW;
                for (int x = 0; x < texW; x++)
                    pixels[rowBase + x] = c;
            }

            tex.SetPixels32(pixels);
            tex.Apply(false);
            return tex;
        }
    }
}
