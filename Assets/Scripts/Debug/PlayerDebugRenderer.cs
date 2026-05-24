using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;
using SandBlast.Components;

namespace SandBlast
{
    // 서버/로컬 월드에서 플레이어 엔티티 위치를 읽어 Debug.DrawLine으로 사각형을 그린다.
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial struct PlayerDebugBoxSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            // 월드 종류에 따라 색상 지정
            // 서버: 주황색 (Authoritative), 클라이언트: 파란색 (Predicted)
            Color worldColor = state.WorldUnmanaged.IsServer()
                ? new Color(1.0f, 0.4f, 0.2f)  // 주황색 (서버 권위적)
                : new Color(0.2f, 0.6f, 1.0f); // 파란색 (클라이언트 예측)

            foreach (var (lt, ghostOwner) in SystemAPI
                .Query<RefRO<LocalTransform>, RefRO<GhostOwner>>()
                .WithAll<PlayerTag>())
            {
                float3 pos = lt.ValueRO.Position;
                
                // 본인 캐릭터인지 여부에 따라 선 굵기나 밝기 조절 (옵션)
                Color finalColor = worldColor;
                
                // 박스 그리기 (캐릭터 크기에 맞춤)
                DrawBox(pos.xy, 1f, 2f, finalColor);
                
                // 머리 위에 짧은 선으로 방향 표시 (IsFacingRight 등 확인용)
                Debug.DrawRay(new Vector3(pos.x, pos.y + 1.2f, 0f), Vector3.up * 0.2f, finalColor);
            }
        }

        static void DrawBox(float2 center, float w, float h, Color col)
        {
            float hw = w * 0.5f, hh = h * 0.5f;
            var tl = new Vector3(center.x - hw, center.y + hh, 0f);
            var tr = new Vector3(center.x + hw, center.y + hh, 0f);
            var bl = new Vector3(center.x - hw, center.y - hh, 0f);
            var br = new Vector3(center.x + hw, center.y - hh, 0f);
            
            Debug.DrawLine(tl, tr, col);
            Debug.DrawLine(tr, br, col);
            Debug.DrawLine(br, bl, col);
            Debug.DrawLine(bl, tl, col);
            // 대각선 추가 (박스 식별 용이)
            Debug.DrawLine(tl, br, col);
        }
    }
}
