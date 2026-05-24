using Unity.Entities;

// 서버 전용 로컬 — 삽 아이템 남은 사용 횟수
public struct ShovelComponent : IComponentData
{
    public int UseCount; // 남은 사용 횟수 (기본값: 3)
}
