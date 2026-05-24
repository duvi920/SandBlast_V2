using SandBlast;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

// 클라이언트 전용 — 로컬 플레이어 엔티티를 카메라가 부드럽게 추적한다.
// PixelGridSingleton 경계 안으로 카메라를 클램프해 그리드 밖이 보이지 않게 한다.
[UpdateInGroup(typeof(PresentationSystemGroup))]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public partial struct CameraFollowSystem : ISystem
{
    private const float SmoothSpeed = 5f;

    public void OnUpdate(ref SystemState state)
    {
        if (Camera.main == null) return;

        var dt = SystemAPI.Time.DeltaTime;

        foreach (var transform in SystemAPI
            .Query<RefRO<LocalTransform>>()
            .WithAll<GhostOwnerIsLocal, PlayerGhostData>())
        {
            var targetPos = transform.ValueRO.Position;
            var cam       = Camera.main.transform;

            float3 desired = new float3(targetPos.x, targetPos.y, cam.position.z);

            // ClampToBounds — PixelGridSingleton 경계 안으로 카메라 제한
            if (SystemAPI.HasSingleton<PixelGridSingleton>())
            {
                var grid  = SystemAPI.GetSingleton<PixelGridSingleton>();
                var camC  = Camera.main;
                float halfH = camC.orthographicSize;
                float halfW = halfH * camC.aspect;
                float gridW = grid.Width  / grid.PixelsPerUnit;
                float gridH = grid.Height / grid.PixelsPerUnit;

                desired.x = math.clamp(desired.x,
                    grid.GridOrigin.x + halfW,
                    grid.GridOrigin.x + gridW - halfW);
                desired.y = math.clamp(desired.y,
                    grid.GridOrigin.y + halfH,
                    grid.GridOrigin.y + gridH - halfH);
            }

            cam.position = Vector3.Lerp(
                cam.position,
                new Vector3(desired.x, desired.y, desired.z),
                SmoothSpeed * dt
            );
            break;
        }
    }
}
