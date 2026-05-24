using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;
using SandBlast;

[UpdateInGroup(typeof(GhostInputSystemGroup))]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
public partial struct GatherInputSystem : ISystem
{
    // SandBlastDebugPainter와 통신하기 위한 임시 스태틱 브릿지
    public static float DebugBrushRadius = 0.5f;
    public static byte  DebugSelectedType = (byte)CellType.POWDER_SAND;
    public static byte  DebugFlammability = 0;

    public void OnUpdate(ref SystemState state)
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;

        float moveX = 0f;
        if (kb != null)
        {
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) moveX += 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  moveX -= 1f;
        }

        bool jumpPressed     = kb != null && kb.spaceKey.wasPressedThisFrame;
        bool shootPressed    = kb != null && kb.fKey.isPressed;
        bool reloadPressed   = kb != null && kb.rKey.wasPressedThisFrame;
        bool interactPressed = kb != null && kb.gKey.wasPressedThisFrame;
        bool dashPressed     = kb != null && (kb.leftShiftKey.wasPressedThisFrame || kb.cKey.wasPressedThisFrame);
        bool meleePressed    = kb != null && kb.qKey.wasPressedThisFrame;
        bool shovelPressed   = kb != null && kb.eKey.wasPressedThisFrame;

        // 페인팅 디버그 기능 연동
        bool lmb = mouse != null && mouse.leftButton.isPressed;
        bool rmb = mouse != null && mouse.rightButton.isPressed;
        float2 mouseWorldPos = float2.zero;
        if (Camera.main != null && mouse != null)
        {
            var mouseScreen = mouse.position.ReadValue();
            var mouseWorld = Camera.main.ScreenToWorldPoint(new Vector3(mouseScreen.x, mouseScreen.y, 10f));
            mouseWorldPos = new float2(mouseWorld.x, mouseWorld.y);
        }

        int count = 0;
        int totalPlayers = 0;
        foreach (var _ in SystemAPI.Query<RefRO<PlayerInput>>()) { totalPlayers++; }

        foreach (var (input, transform, entity) in SystemAPI
            .Query<RefRW<PlayerInput>, RefRO<LocalTransform>>()
            .WithAll<GhostOwnerIsLocal>()
            .WithEntityAccess())
        {
            count++;
            input.ValueRW.MoveDirection = new float2(moveX, 0f);

            input.ValueRW.Jump = default;
            if (jumpPressed) input.ValueRW.Jump.Set();

            input.ValueRW.Shoot = default;
            if (shootPressed) input.ValueRW.Shoot.Set();

            input.ValueRW.Reload = default;
            if (reloadPressed) input.ValueRW.Reload.Set();

            input.ValueRW.Interact = default;
            if (interactPressed) input.ValueRW.Interact.Set();

            input.ValueRW.Dash = default;
            if (dashPressed) input.ValueRW.Dash.Set();

            input.ValueRW.MeleeAttack = default;
            if (meleePressed) input.ValueRW.MeleeAttack.Set();

            input.ValueRW.UseShovel = default;
            if (shovelPressed) input.ValueRW.UseShovel.Set();

            // 페인팅 데이터 전송 (디버그용)
            input.ValueRW.IsPainting = lmb || rmb;
            if (input.ValueRW.IsPainting)
            {
                input.ValueRW.PaintPos = mouseWorldPos;
                input.ValueRW.PaintRadius = DebugBrushRadius;
                input.ValueRW.PaintCellType = rmb ? (byte)CellType.EMPTY : DebugSelectedType;
                input.ValueRW.PaintFlammability = DebugFlammability;
            }

            if (Camera.main != null && mouse != null)
            {
                var diff = mouseWorldPos - transform.ValueRO.Position.xy;
                input.ValueRW.AimAngle = math.atan2(diff.y, diff.x);
            }
        }

        if (count == 0 && UnityEngine.Time.frameCount % 120 == 0)
        {
            UnityEngine.Debug.LogWarning($"[GatherInput] GhostOwnerIsLocal 없음 (World: {state.WorldUnmanaged.Name}, PlayerInput엔티티수: {totalPlayers})");
        }
    }
}
