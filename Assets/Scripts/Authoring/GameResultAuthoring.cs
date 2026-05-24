using Unity.Entities;
using UnityEngine;

public class GameResultAuthoring : MonoBehaviour
{
    class Baker : Baker<GameResultAuthoring>
    {
        public override void Bake(GameResultAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new GameResultSingleton
            {
                WinnerNetworkId = -1,
                GameEnded = false,
                IsDraw = false
            });
        }
    }
}
