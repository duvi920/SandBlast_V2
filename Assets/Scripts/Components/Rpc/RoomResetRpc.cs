using Unity.Entities;
using Unity.NetCode;

// 서버 → 전체 클라이언트: 방이 리셋되었음을 알린다.
public struct RoomResetRpc : IRpcCommand { }
