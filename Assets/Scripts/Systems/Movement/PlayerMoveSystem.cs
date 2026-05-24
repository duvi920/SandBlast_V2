using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using SandBlast;
using SandBlast.Components;

// 서버/로컬 권위적 이동 — 클라이언트가 보낸 IInputComponentData를 서버에서 소비해 위치를 갱신한다.
// 클라이언트 예측을 위해 PredictedSimulationSystemGroup에서 실행하며 ClientSimulation 필터를 포함한다.
// [BurstCompile]
[UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
public partial struct PlayerMoveSystem : ISystem
{
    private const float MoveSpeed        = 6f;
    private const float Acceleration     = 50f;   // 지상 가속도 (m/s²)
    private const float Deceleration     = 40f;   // 지상 감속도 (m/s²)
    private const float AirAcceleration  = 25f;   // 공중 가속도 (m/s²)
    private const float JumpForce        = 14f;
    private const float CoyoteTime       = 0.12f; // 지면 이탈 후 점프 허용 시간 (초)
    private const float JumpBufferTime   = 0.10f; // 착지 전 점프 입력 유지 시간 (초)
    private const float DashSpeed        = 20f;
    private const float DashDuration     = 0.14f;
    private const float DashCooldown     = 0.55f;
    private const float PlayerHalfHeight = 0.75f; // GroundCheckSystem과 반드시 동일해야 함
    private const float PlayerHalfWidth  = 0.30f; // 수평 벽 충돌 판정 반폭
    // SampleWidth: 발/머리 샘플 5개를 캐릭터 폭 ±SampleWidth 범위에 균등 배치
    private const float SampleWidth      = 0.25f;
    // FootOffset: 셀 경계와 정확히 겹치지 않도록 미세 여유값 추가
    private const float FootOffset       = 0.04f;
    // 모래 기둥 스프링: 수직으로 쌓인 POWDER_SAND 셀 수가 이 값 이상이면 솟구침 발동
    private const int   SandSpringMinStack = 3;
    private const float SandSpringForce    = 22f;

    // [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingleton<NetworkTime>(out var networkTime)) return;
        if (!networkTime.IsFirstTimeFullyPredictingTick) return;

        var dt = SystemAPI.Time.DeltaTime;
        var hasGrid = SystemAPI.HasSingleton<PixelGridSingleton>();
        var grid    = hasGrid ? SystemAPI.GetSingleton<PixelGridSingleton>() : default;

        int playerCount = 0;
        foreach (var (input, ghostData, groundedState, localState, playerState, transform) in SystemAPI
            .Query<RefRO<PlayerInput>, RefRW<PlayerGhostData>, RefRW<GroundedState>,
                   RefRW<PlayerLocalState>, RefRW<PlayerMovementData>, RefRW<LocalTransform>>()
            .WithAll<Simulate, PlayerTag>())
        {
            playerCount++;
            if (ghostData.ValueRO.IsDead)
                continue;

            // 기절 중이면 입력을 무시하고 관성만 유지
            if (ghostData.ValueRO.IsStunned)
            {
                var stunVel = ghostData.ValueRO.Velocity;
                float stunStep = 40f * dt;
                stunVel.x = math.abs(stunVel.x) <= stunStep ? 0f : stunVel.x - math.sign(stunVel.x) * stunStep;
                ghostData.ValueRW.Velocity = stunVel;
                var stunPos = ghostData.ValueRO.Position + stunVel * dt;
                ghostData.ValueRW.Position = stunPos;
                transform.ValueRW.Position = new float3(stunPos.x, stunPos.y, transform.ValueRO.Position.z);
                continue;
            }

            var vel = ghostData.ValueRO.Velocity;
            var ls  = localState.ValueRO;
            bool isGrounded = groundedState.ValueRO.IsGrounded;
            playerState.ValueRW.IsGrounded = isGrounded;

            ls.DashCooldownTimer -= dt;

            // 대시 시작
            if (input.ValueRO.Dash.IsSet && !ls.IsDashing && ls.DashCooldownTimer <= 0f)
            {
                ls.IsDashing         = true;
                ls.DashTimer         = DashDuration;
                ls.DashCooldownTimer = DashCooldown;
                float dirX = math.abs(input.ValueRO.MoveDirection.x) > 0.01f
                    ? math.sign(input.ValueRO.MoveDirection.x)
                    : (ghostData.ValueRO.IsFacingRight ? 1f : -1f);
                ls.DashDir = new float2(dirX, 0f);
                vel.y = 0f;
            }

            if (ls.IsDashing)
            {
                ls.DashTimer -= dt;
                if (ls.DashTimer <= 0f)
                {
                    ls.IsDashing = false;
                    vel.x = ls.DashDir.x * MoveSpeed;
                }
                else
                {
                    vel = ls.DashDir * DashSpeed;
                    ghostData.ValueRW.Velocity = vel;
                    var dashPos = ghostData.ValueRO.Position + vel * dt;
                    ghostData.ValueRW.Position = dashPos;
                    transform.ValueRW.Position = new float3(dashPos.x, dashPos.y, transform.ValueRO.Position.z);
                    
                    playerState.ValueRW.Value = PlayerMovementState.Dashing;
                    localState.ValueRW = ls;
                    continue;
                }
            }

            // 코요테 타임
            if (isGrounded) ls.CoyoteTimer = CoyoteTime;
            else            ls.CoyoteTimer = math.max(0f, ls.CoyoteTimer - dt);

            // 점프 버퍼
            if (input.ValueRO.Jump.IsSet) ls.JumpBufferTimer = JumpBufferTime;
            else                          ls.JumpBufferTimer = math.max(0f, ls.JumpBufferTimer - dt);

            if (ls.JumpBufferTimer > 0f && ls.CoyoteTimer > 0f)
            {
                vel.y              = JumpForce;
                ls.CoyoteTimer     = 0f;
                ls.JumpBufferTimer = 0f;
            }

            // 좌우 이동 (MoveTowards)
            float targetVx = input.ValueRO.MoveDirection.x * MoveSpeed;
            bool  hasInput  = math.abs(input.ValueRO.MoveDirection.x) > 0.01f;
            float accel     = isGrounded
                ? (hasInput ? Acceleration    : Deceleration)
                : (hasInput ? AirAcceleration : AirAcceleration * 0.3f);
            float diff = targetVx - vel.x;
            float step = accel * dt;
            vel.x = math.abs(diff) <= step ? targetVx : vel.x + math.sign(diff) * step;

            // 애니메이션 상태 결정
            if (!isGrounded)
            {
                playerState.ValueRW.Value = PlayerMovementState.Jumping;
            }
            else if (hasInput)
            {
                playerState.ValueRW.Value = PlayerMovementState.Moving;
            }
            else
            {
                playerState.ValueRW.Value = PlayerMovementState.Idle;
            }

            // 방향 전환
            if (hasInput)
                ghostData.ValueRW.IsFacingRight = input.ValueRO.MoveDirection.x > 0f;

            ghostData.ValueRW.Velocity = vel;
            localState.ValueRW = ls;

            var newPos = ghostData.ValueRO.Position + vel * dt;

            // ── 픽셀 지형 충돌: 하강 중 바닥 관통 방지 ─────────────────
            // 발 높이(footY)에 가로 5개 샘플 포인트를 균등 배치해 Solid 셀을 찾는다.
            // 여러 샘플이 고체에 닿을 경우 가장 높은 셀 상단(resolvedY)을 기준으로 보정한다.
            // 셀 상단 월드 좌표 = GridOrigin.y + (gy + 1) / PixelsPerUnit
            if (hasGrid && vel.y <= 0f)
            {
                float cx    = newPos.x;
                float footY = newPos.y - PlayerHalfHeight - FootOffset;

                float resolvedY = float.NegativeInfinity;
                bool   hit      = false;

                for (int i = 0; i < 5; i++)
                {
                    float wx = cx + math.lerp(-SampleWidth, SampleWidth, i / 4f);
                    int2  gp = grid.WorldToGrid(new float2(wx, footY));
                    if (!grid.InBounds(gp.x, gp.y)) continue;

                    if (((CellType)grid.Type[grid.Index(gp.x, gp.y)]).IsSolid())
                    {
                        float cellTopY = grid.GridOrigin.y + (gp.y + 1) / grid.PixelsPerUnit;
                        resolvedY = math.max(resolvedY, cellTopY + PlayerHalfHeight + FootOffset);
                        hit = true;
                    }
                }

                if (hit && newPos.y < resolvedY)
                {
                    newPos.y = resolvedY;
                    vel.y    = 0f;
                    ghostData.ValueRW.Velocity = vel;
                    playerState.ValueRW.IsGrounded = true;
                }

                // ── 모래 기둥 스프링 ──────────────────────────────────────
                // 벽 옆면 오감지를 막기 위해 중심점(cx) 한 곳만 단독 검사한다.
                // IsSolid() 바닥 충돌과 독립적으로 동작하므로
                // 솔리드 지형 위에 쌓인 모래 기둥도 감지된다.
                int2 sandGP = grid.WorldToGrid(new float2(cx, footY));
                // sandGP.y+1 셀이 EMPTY여야 "기둥 위에 착지"로 판별한다.
                // 벽 안쪽에 있으면 그 윗셀도 POWDER_SAND이므로 스프링 오발동을 막는다.
                bool cellAboveIsClear = !grid.InBounds(sandGP.x, sandGP.y + 1) ||
                    (CellType)grid.Type[grid.Index(sandGP.x, sandGP.y + 1)] == CellType.EMPTY;
                if (grid.InBounds(sandGP.x, sandGP.y) &&
                    (CellType)grid.Type[grid.Index(sandGP.x, sandGP.y)] == CellType.POWDER_SAND &&
                    cellAboveIsClear)
                {
                    int stackCount = 0;
                    for (int sy = sandGP.y; sy >= 0; sy--)
                    {
                        if (!grid.InBounds(sandGP.x, sy)) break;
                        if ((CellType)grid.Type[grid.Index(sandGP.x, sy)] == CellType.POWDER_SAND)
                            stackCount++;
                        else
                            break;
                    }

                    if (stackCount >= SandSpringMinStack)
                    {
                        float sandTopY = grid.GridOrigin.y + (sandGP.y + 1) / grid.PixelsPerUnit;
                        newPos.y = sandTopY + PlayerHalfHeight + FootOffset;
                        vel.y    = SandSpringForce;
                        vel.x    = 0f;
                        ghostData.ValueRW.Velocity     = vel;
                        playerState.ValueRW.IsGrounded = false;
                    }
                }
            }

            // ── 픽셀 지형 충돌: 상승 중 천장 관통 방지 ──────────────────
            // 머리 높이(headY)에 5개 샘플로 천장을 감지한다.
            // 여러 셀이 닿을 경우 가장 낮은 셀 하단(bestCeil)으로 위치를 고정한다.
            // 셀 하단 월드 좌표 = GridOrigin.y + gy / PixelsPerUnit
            if (hasGrid && vel.y > 0f)
            {
                float headY    = newPos.y + PlayerHalfHeight + FootOffset;
                float bestCeil = float.PositiveInfinity;
                bool  ceilHit  = false;

                for (int i = 0; i < 5; i++)
                {
                    float wx = newPos.x + math.lerp(-SampleWidth, SampleWidth, i / 4f);
                    int2  gp = grid.WorldToGrid(new float2(wx, headY));
                    if (!grid.InBounds(gp.x, gp.y)) continue;

                    if (((CellType)grid.Type[grid.Index(gp.x, gp.y)]).IsSolid())
                    {
                        float cellBottom = grid.GridOrigin.y + gp.y / grid.PixelsPerUnit;
                        bestCeil = math.min(bestCeil, cellBottom - PlayerHalfHeight - FootOffset);
                        ceilHit  = true;
                    }
                }

                if (ceilHit && newPos.y > bestCeil)
                {
                    newPos.y = bestCeil;
                    vel.y    = 0f;
                    ghostData.ValueRW.Velocity = vel;
                }
            }

            // ── 픽셀 지형 충돌: 수평 벽 관통 방지 ───────────────────────
            // 이동 방향 측면(sideX)에 높이 3개 샘플로 벽을 감지한다.
            // 오른쪽 이동: 가장 왼쪽에 있는 셀의 왼쪽 경계 - 반폭 = 최대 허용 X
            // 왼쪽 이동:  가장 오른쪽에 있는 셀의 오른쪽 경계 + 반폭 = 최소 허용 X
            if (hasGrid && math.abs(vel.x) > 0.01f)
            {
                float sideDir = math.sign(vel.x);
                float sideX   = newPos.x + sideDir * (PlayerHalfWidth + FootOffset);
                float best    = sideDir > 0f ? float.PositiveInfinity : float.NegativeInfinity;
                bool  wallHit = false;

                // 샘플 높이: 하체·중체·상체 (캡슐 70% 범위)
                for (int i = 0; i < 3; i++)
                {
                    float wy = newPos.y + math.lerp(-PlayerHalfHeight * 0.7f, PlayerHalfHeight * 0.7f, i / 2f);
                    int2  gp = grid.WorldToGrid(new float2(sideX, wy));
                    if (!grid.InBounds(gp.x, gp.y)) continue;

                    if (((CellType)grid.Type[grid.Index(gp.x, gp.y)]).IsSolid())
                    {
                        float resolved = sideDir > 0f
                            ? grid.GridOrigin.x + gp.x / grid.PixelsPerUnit - PlayerHalfWidth - FootOffset
                            : grid.GridOrigin.x + (gp.x + 1) / grid.PixelsPerUnit + PlayerHalfWidth + FootOffset;

                        best    = sideDir > 0f ? math.min(best, resolved) : math.max(best, resolved);
                        wallHit = true;
                    }
                }

                if (wallHit)
                {
                    bool blocked = sideDir > 0f ? newPos.x > best : newPos.x < best;
                    if (blocked)
                    {
                        newPos.x = best;
                        vel.x    = 0f;
                        ghostData.ValueRW.Velocity = vel;
                    }
                }
            }

            ghostData.ValueRW.Position = newPos;
            transform.ValueRW.Position = new float3(newPos.x, newPos.y, transform.ValueRO.Position.z);
        }

        if (playerCount == 0 && UnityEngine.Time.frameCount % 120 == 0)
        {
            // UnityEngine.Debug.Log($"[PlayerMoveSystem] No players found in {state.WorldUnmanaged.Name}");
        }
    }
}
