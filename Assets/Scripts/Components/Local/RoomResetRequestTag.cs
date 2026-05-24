using Unity.Entities;

// 서버 전용 — 이 컴포넌트가 존재하면 RoomResetSystem이 리셋을 실행한다.
public struct RoomResetRequestTag : IComponentData { }
