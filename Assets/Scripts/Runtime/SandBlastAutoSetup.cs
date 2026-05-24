using UnityEngine;

namespace SandBlast
{
    /// <summary>
    /// 씬에 SandBlast 필수 오브젝트가 없을 때 런타임에 자동 생성한다.
    /// (Entities 베이킹/NetCode 월드 생성 타이밍 문제로 인해 싱글턴 생성이 누락되는 케이스를 방지)
    /// </summary>
    public static class SandBlastAutoSetup
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsureBootstrapExists()
        {
            var bootstrap = Object.FindFirstObjectByType<SandBlastBootstrapAuthoring>();

            if (bootstrap == null)
            {
                // 최소 구성: Bootstrap + DebugPainter
                var go = new GameObject("SandBlastBootstrap (Auto)");
                Object.DontDestroyOnLoad(go);

                bootstrap = go.AddComponent<SandBlastBootstrapAuthoring>();
                go.AddComponent<SandBlastDebugPainter>();
            }

            // 픽셀 레이어가 없으면 렌더러를 자동 생성 (안 보이면 "안 뿌려지는" 것처럼 보임)
            if (Object.FindFirstObjectByType<PixelLayerRenderer>() == null)
            {
                var layer = new GameObject("PixelLayer (Auto)");
                Object.DontDestroyOnLoad(layer);

                // 그리드 중심으로 배치 (SetupWindow 로직과 동일한 의도)
                float worldW = bootstrap.GridWidth  / bootstrap.PixelsPerUnit;
                float worldH = bootstrap.GridHeight / bootstrap.PixelsPerUnit;
                layer.transform.position = new Vector3(
                    bootstrap.GridOrigin.x + worldW * 0.5f,
                    bootstrap.GridOrigin.y + worldH * 0.5f,
                    0.1f);

                layer.AddComponent<SpriteRenderer>();
                layer.AddComponent<PixelLayerRenderer>();
            }

            // 렌더링/브러시에 필요한 카메라가 없으면 기본 카메라 생성
            if (Camera.main == null && Object.FindFirstObjectByType<Camera>() == null)
            {
                var camGo = new GameObject("Main Camera (Auto)");
                Object.DontDestroyOnLoad(camGo);
                var cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;

                float worldH = bootstrap.GridHeight / bootstrap.PixelsPerUnit;
                cam.orthographicSize = worldH * 0.5f;
                cam.transform.position = new Vector3(
                    bootstrap.GridOrigin.x + (bootstrap.GridWidth / bootstrap.PixelsPerUnit) * 0.5f,
                    bootstrap.GridOrigin.y + worldH * 0.5f,
                    -10f);
                camGo.tag = "MainCamera";
            }

            // 시차 배경 레이어 (없는 경우에만)
            if (Object.FindFirstObjectByType<ParallaxBackground>() == null)
            {
                float worldW = bootstrap.GridWidth  / bootstrap.PixelsPerUnit;
                float worldH = bootstrap.GridHeight / bootstrap.PixelsPerUnit;
                Camera cam   = Camera.main ?? Object.FindFirstObjectByType<Camera>();

                if (cam != null)
                {
                    var bgGo = new GameObject("ParallaxBackground (Auto)");
                    Object.DontDestroyOnLoad(bgGo);
                    var parallax = bgGo.AddComponent<ParallaxBackground>();
                    parallax.Initialize(cam, worldW, worldH,
                                        bootstrap.GridOrigin, bootstrap.PixelsPerUnit);
                }
            }

            // 강제로 한 번 호출해 런타임 싱글턴 부팅 트리거
            PixelGridAPI.TryGetGrid(out _, out _);
        }
    }
}

