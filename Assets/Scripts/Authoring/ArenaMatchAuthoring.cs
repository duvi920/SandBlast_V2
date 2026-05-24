using Unity.Entities;
using UnityEngine;

// 서버 월드의 GameManager 오브젝트에 부착해 ArenaMatchSingleton을 굽는다.
public class ArenaMatchAuthoring : MonoBehaviour
{
    [Tooltip("매치 전체 시간 (초)")]
    public float MatchDuration = 300f;  // 5분

    [Tooltip("용암 상승 속도 (픽셀/초)")]
    public float LavaRiseSpeed = 1.5f;

    class Baker : Baker<ArenaMatchAuthoring>
    {
        public override void Bake(ArenaMatchAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new ArenaMatchSingleton
            {
                TimeRemaining  = authoring.MatchDuration,
                MatchDuration  = authoring.MatchDuration,
                ScoreTeam0     = 0,
                ScoreTeam1     = 0,
                GameEnded      = false,
                WinnerTeamId   = -1,
                LavaRiseActive = false,
                LavaRiseY      = 0f,
                LavaRiseSpeed  = authoring.LavaRiseSpeed,
            });
        }
    }
}
