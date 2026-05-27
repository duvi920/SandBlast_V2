using Unity.Entities;
using UnityEngine;

// 서버 월드의 GameManager 오브젝트에 부착해 RoomStateSingleton을 굽는다.
public class RoomStateAuthoring : MonoBehaviour
{
    [Tooltip("카운트다운 시간 (초)")]
    public float CountdownDuration = 3f;

    [Tooltip("카운트다운 시작에 필요한 최소 플레이어 수 (0 = 기본값 2)")]
    [Range(0, 8)]
    public int MinPlayers = 2;

    [Tooltip("방 최대 입장 인원 (0 = 기본값 4)")]
    [Range(0, 8)]
    public int MaxPlayers = 4;

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
                MinPlayers      = authoring.MinPlayers,
                MaxPlayers      = authoring.MaxPlayers,
                HostNetworkId   = 0,
            });
        }
    }
}
