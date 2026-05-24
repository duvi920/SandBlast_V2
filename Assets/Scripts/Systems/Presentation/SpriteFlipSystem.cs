using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

[UpdateInGroup(typeof(PresentationSystemGroup))]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
public partial class SpriteFlipSystem : SystemBase
{
    protected override void OnUpdate()
    {
        Entities
            .WithoutBurst()
            .WithAll<PlayerGhostData, SpriteRenderer>()
            .ForEach((SpriteRenderer sr, in PlayerGhostData ghostData) =>
            {
                if (sr == null) return;
                sr.flipX = !ghostData.IsFacingRight;
            }).Run();
    }
}
