using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using SandBlast.Components;

namespace SandBlast.Systems
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial struct EnsureLocalPlayerSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;

            // 1. 모든 플레이어 고스트 검색
            var q = em.CreateEntityQuery(typeof(PlayerGhostData));
            using var entities = q.ToEntityArray(Allocator.Temp);

            // 아직 고스트가 하나도 없으면 대기 (서버로부터 스냅샷이 올 때까지)
            if (entities.Length == 0) return;

            Entity localPlayer = Entity.Null;
            
            // 2. 본인의 NetworkId 찾기
            int myNetId = -1;
            if (SystemAPI.TryGetSingleton<NetworkId>(out var netId))
            {
                myNetId = netId.Value;
            }

            // 3. 이미 GhostOwnerIsLocal을 가진 엔티티 확인
            for (int i = 0; i < entities.Length; i++)
            {
                if (em.HasComponent<GhostOwnerIsLocal>(entities[i]))
                {
                    localPlayer = entities[i];
                    break;
                }
            }

            // 4. GhostOwnerIsLocal이 없다면, NetworkId 매칭 시도
            if (localPlayer == Entity.Null && myNetId != -1)
            {
                for (int i = 0; i < entities.Length; i++)
                {
                    if (em.HasComponent<GhostOwner>(entities[i]))
                    {
                        var owner = em.GetComponentData<GhostOwner>(entities[i]);
                        if (owner.NetworkId == myNetId)
                        {
                            localPlayer = entities[i];
                            em.AddComponent<GhostOwnerIsLocal>(localPlayer);
                            UnityEngine.Debug.Log($"[EnsureLocalPlayer] NetId {myNetId}에 해당하는 플레이어를 찾아 GhostOwnerIsLocal 부여함");
                            break;
                        }
                    }
                }
            }

            // 5. 싱글플레이어 보정 (NetworkId가 없는 경우)
            if (localPlayer == Entity.Null && myNetId == -1)
            {
                localPlayer = entities[0];
                em.AddComponent<GhostOwnerIsLocal>(localPlayer);
                UnityEngine.Debug.Log("[EnsureLocalPlayer] 싱글플레이어 모드로 판단하여 첫 번째 플레이어에 권한 부여");
            }

            // 로컬 플레이어 확정 시 필수 컴포넌트 최종 점검
            if (localPlayer != Entity.Null)
            {
                bool fixedAny = false;
                if (!em.HasComponent<PlayerInput>(localPlayer)) { em.AddComponentData(localPlayer, default(PlayerInput)); fixedAny = true; }
                if (!em.HasComponent<LocalTransform>(localPlayer)) { em.AddComponentData(localPlayer, LocalTransform.Identity); fixedAny = true; }
                if (!em.HasComponent<Simulate>(localPlayer)) { em.AddComponent<Simulate>(localPlayer); fixedAny = true; }
                
                // 필요한 다른 컴포넌트들도 유사하게 보정...
                if (!em.HasComponent<PlayerLocalState>(localPlayer)) em.AddComponentData(localPlayer, default(PlayerLocalState));
                if (!em.HasComponent<GroundedState>(localPlayer)) em.AddComponentData(localPlayer, default(GroundedState));
                if (!em.HasComponent<PlayerMovementData>(localPlayer)) em.AddComponentData(localPlayer, default(PlayerMovementData));
                if (!em.HasComponent<PlayerTag>(localPlayer)) em.AddComponent<PlayerTag>(localPlayer);

                if (fixedAny) UnityEngine.Debug.Log($"[EnsureLocalPlayer] 로컬 플레이어 {localPlayer}의 누락된 컴포넌트 보정 완료");
                
                state.Enabled = false; // 보정 완료 후 이 시스템은 더 이상 실행 안함
            }
            else if (UnityEngine.Time.frameCount % 120 == 0)
            {
                UnityEngine.Debug.LogWarning($"[EnsureLocalPlayer] 로컬 플레이어를 찾는 중... (고스트 수: {entities.Length}, MyNetId: {myNetId})");
            }
        }
    }
}
