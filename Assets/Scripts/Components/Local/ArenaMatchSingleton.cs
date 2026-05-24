using Unity.Entities;

public struct ArenaMatchSingleton : IComponentData
{
    public float TimeRemaining;      // 남은 시간 (초)
    public float MatchDuration;      // 전체 매치 시간 (초)
    public int   ScoreTeam0;
    public int   ScoreTeam1;
    public bool  GameEnded;
    public int   WinnerTeamId;       // -1=미결, 0=A팀, 1=B팀, 2=무승부
    public bool  LavaRiseActive;
    public float LavaRiseY;          // 현재 용암 상승 높이 (픽셀)
    public float LavaRiseSpeed;      // 픽셀/초
}
