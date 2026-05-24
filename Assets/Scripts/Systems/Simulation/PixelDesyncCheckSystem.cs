using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;

namespace SandBlast
{
    // 결정론적 동기화 검증 시스템.
    // 매 60틱마다 픽셀 그리드의 해시를 계산하여 서버-클라이언트 간 비교한다.
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PixelSimulationSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    public partial struct PixelDesyncCheckSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PixelGridSingleton>();
            state.RequireForUpdate<NetworkTime>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            uint currentTick = networkTime.ServerTick.RawValue;

            // 60틱마다 한 번씩 검사 (약 1초 간격)
            if (currentTick == 0 || currentTick % 60 != 0) return;

            var grid = SystemAPI.GetSingleton<PixelGridSingleton>();
            uint hash = CalculateHash(grid.Type);

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // 클라이언트는 서버로 자신의 해시를 보냄
            if (state.WorldUnmanaged.IsClient())
            {
                var rpc = ecb.CreateEntity();
                ecb.AddComponent(rpc, new PixelHashRpc { Tick = currentTick, Hash = hash });
                ecb.AddComponent<SendRpcCommandRequest>(rpc);
            }
            // 서버는 클라이언트로부터 받은 해시와 대조 (여기서는 간단히 로그만 남김)
            else if (state.WorldUnmanaged.IsServer())
            {
                foreach (var (rpc, request, entity) in SystemAPI.Query<RefRO<PixelHashRpc>, RefRO<ReceiveRpcCommandRequest>>().WithEntityAccess())
                {
                    if (rpc.ValueRO.Tick == currentTick)
                    {
                        if (rpc.ValueRO.Hash != hash)
                        {
                            Debug.LogError($"[DESYNC] Tick {currentTick}! Server:{hash:X8} != Client:{rpc.ValueRO.Hash:X8}");
                        }
                        else
                        {
                            // Debug.Log($"[SYNC OK] Tick {currentTick} Hash:{hash:X8}");
                        }
                    }
                    ecb.DestroyEntity(entity);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        [BurstCompile]
        static uint CalculateHash(NativeArray<byte> data)
            if (!data.IsCreated) return 0;
            
            // xxHash와 유사한 방식의 간단한 32비트 해싱
            uint hash = 0x811C9DC5; // FNV offset basis
            for (int i = 0; i < data.Length; i++)
            {
                hash ^= data[i];
                hash *= 0x01000193; // FNV prime
            }
            return hash;
        }
    }
}
