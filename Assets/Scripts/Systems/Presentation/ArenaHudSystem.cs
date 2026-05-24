using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

// 클라이언트 전용 — ArenaMatchTickRpc / ArenaMatchEndRpc 를 수신해
// ArenaHudBehaviour 싱글톤 MonoBehaviour에 최신 상태를 밀어 넣는다.
[UpdateInGroup(typeof(SimulationSystemGroup))]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public partial class ArenaHudSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var hud = ArenaHudBehaviour.Instance;

        // ── Tick RPC ─────────────────────────────────────────────────
        var tickQuery = SystemAPI.QueryBuilder()
            .WithAll<ArenaMatchTickRpc, ReceiveRpcCommandRequest>()
            .Build();

        if (!tickQuery.IsEmpty)
        {
            var tick = tickQuery.GetSingleton<ArenaMatchTickRpc>();
            if (hud != null)
            {
                hud.TimeRemaining = tick.TimeRemaining;
                hud.ScoreTeam0    = tick.ScoreTeam0;
                hud.ScoreTeam1    = tick.ScoreTeam1;
                hud.LavaRiseY     = tick.LavaRiseY;
            }
            EntityManager.DestroyEntity(tickQuery);
        }

        // ── End RPC ───────────────────────────────────────────────────
        var endQuery = SystemAPI.QueryBuilder()
            .WithAll<ArenaMatchEndRpc, ReceiveRpcCommandRequest>()
            .Build();

        if (!endQuery.IsEmpty)
        {
            var end = endQuery.GetSingleton<ArenaMatchEndRpc>();
            if (hud != null)
                hud.ShowResult(end.WinnerTeamId, end.ScoreTeam0, end.ScoreTeam1, end.IsDraw);
            EntityManager.DestroyEntity(endQuery);
        }

        // ── RoomReset RPC ─────────────────────────────────────────────
        var resetQuery = SystemAPI.QueryBuilder()
            .WithAll<RoomResetRpc, ReceiveRpcCommandRequest>()
            .Build();
        if (!resetQuery.IsEmpty)
        {
            if (hud != null) hud.OnRoomReset();
            EntityManager.DestroyEntity(resetQuery);
        }

        // ── MatchCountdown RPC ────────────────────────────────────────
        var cdQuery = SystemAPI.QueryBuilder()
            .WithAll<MatchCountdownRpc, ReceiveRpcCommandRequest>()
            .Build();
        if (!cdQuery.IsEmpty)
        {
            var cd = cdQuery.GetSingleton<MatchCountdownRpc>();
            if (hud != null) hud.OnCountdown(cd.Count);
            EntityManager.DestroyEntity(cdQuery);
        }

        // ── 로컬 플레이어 HP / 마나 / 완드 ───────────────────────────
        if (hud == null) return;
        foreach (var (ghost, mana, wand, spells) in SystemAPI
            .Query<RefRO<PlayerGhostData>, RefRO<SandBlast.Wand.ManaComponent>,
                   RefRO<SandBlast.Wand.WandComponent>, DynamicBuffer<SandBlast.Wand.SpellData>>()
            .WithAll<GhostOwnerIsLocal>())
        {
            hud.LocalHealth  = ghost.ValueRO.Health;
            hud.LocalManaMax = mana.ValueRO.Max;
            hud.LocalMana    = mana.ValueRO.Current;
            hud.LocalActiveSlot = wand.ValueRO.ActiveSlot;

            if (spells.Length > 0)
            {
                int slot = Unity.Mathematics.math.clamp(wand.ValueRO.ActiveSlot, 0, spells.Length - 1);
                var spell = spells[slot];
                string typeStr = spell.ProjectileType.ToString();
                if (spell.LiquidPayload != SandBlast.Wand.LiquidPayload.None)
                    typeStr += $" ({spell.LiquidPayload})";
                
                hud.LocalSpellType = typeStr;
            }
        }
    }
}
