using Unity.Entities;
using Unity.NetCode;

// 서버 → 전체 클라이언트: 카운트다운 숫자 (3·2·1·0=GO)
public struct MatchCountdownRpc : IRpcCommand
{
    public int Count;
}
