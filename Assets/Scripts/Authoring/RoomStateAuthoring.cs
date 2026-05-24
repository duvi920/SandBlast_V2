using Unity.Entities;
using UnityEngine;

// 서버 월드의 GameManager 오브젝트에 부착해 RoomStateSingleton을 굽는다.
public class RoomStateAuthoring : MonoBehaviour
{
    [Tooltip("카운트다운 시간 (초)")]
    public float CountdownDuration = 3f;

    class Baker : Baker<RoomStateAuthoring>
    {
        public override void Bake(RoomStateAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new RoomStateSingleton
            {
                Phase           = RoomPhase.Solo,
                CountdownTimer  = authoring.CountdownDuration,
                LastPlayerCount = 0,
            });
        }
    }
}
