using SandBlast;
using SandBlast.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

// 클라이언트+서버 예측 — 발 아래 5개 샘플 포인트에서 픽셀 그리드를 조회해 착지 여부를 판정한다.
[BurstCompile]
[UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
public partial struct GroundCheckSystem : ISystem
{
    private const float PlayerHalfHeight = 0.75f; // 콜라이더 반높이
    private const float SampleWidth      = 0.25f; // 발 샘플 반폭
    private const float FootOffset       = 0.04f; // 발 아래 여유

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PixelGridSingleton>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var grid = SystemAPI.GetSingleton<PixelGridSingleton>();

        foreach (var (transform, groundedState, ghostData) in SystemAPI
            .Query<RefRO<LocalTransform>, RefRW<GroundedState>, RefRW<PlayerGhostData>>()
            .WithAll<PlayerTag>())
        {
            float cx   = transform.ValueRO.Position.x;
            // 충돌 해소 후 footY가 셀 상단 경계에 정확히 놓여 WorldToGrid의 floor()가
            // 고체 셀이 아닌 바로 위 셀을 가리키는 off-by-one을 방지하기 위해
            // 반 픽셀만큼 내려서 샘플링한다.
            float footY = transform.ValueRO.Position.y - PlayerHalfHeight - FootOffset
                          - 0.5f / grid.PixelsPerUnit;

            bool grounded = false;
            for (int i = 0; i < 5 && !grounded; i++)
            {
                float wx = cx + math.lerp(-SampleWidth, SampleWidth, i / 4f);
                int2  gp = grid.WorldToGrid(new float2(wx, footY));
                if (grid.InBounds(gp.x, gp.y) &&
                    ((CellType)grid.Type[grid.Index(gp.x, gp.y)]).IsSolid())
                    grounded = true;
            }

            groundedState.ValueRW.IsGrounded = grounded;
            ghostData.ValueRW.IsGrounded     = grounded;
        }
    }
}
