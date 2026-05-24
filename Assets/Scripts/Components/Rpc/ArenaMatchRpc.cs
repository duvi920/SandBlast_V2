using Unity.Entities;
using Unity.NetCode;

// 서버 → 모든 클라이언트: 매치 종료 결과 전송.
public struct ArenaMatchEndRpc : IRpcCommand
{
    public int  WinnerTeamId;   // -1=미결, 0=A팀, 1=B팀, 2=무승부
    public int  ScoreTeam0;
    public int  ScoreTeam1;
    public bool IsDraw;
}

// 서버 → 모든 클라이언트: 매치 진행 중 주기적 상태 동기화.
public struct ArenaMatchTickRpc : IRpcCommand
{
    public float TimeRemaining;
    public int   ScoreTeam0;
    public int   ScoreTeam1;
    public float LavaRiseY;
}
