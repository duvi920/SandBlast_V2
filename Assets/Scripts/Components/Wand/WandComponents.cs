using Unity.Entities;
using Unity.NetCode;

namespace SandBlast.Wand
{
    public enum ProjectileType : byte
    {
        Bullet      = 0, // 기본 탄환
        Explosion   = 1, // 폭발 (BlastRadius 적용)
        LiquidSpray = 2, // 비행 중 액체 셀 살포
        Ice         = 3, // 냉동탄 (온도 급감)
        Sand        = 4, // 모래탄 (POWDER_SAND 생성)
        Lightning   = 5, // 번개 (즉발 관통)
    }

    public enum LiquidPayload : byte
    {
        None   = 0,
        Lava   = 1, // 용암 — LIQUID_LAVA 셀 생성
        Water  = 2, // 물 — LIQUID_WATER 셀 생성
        Poison = 3, // 독 — LIQUID_POISON 셀 생성
    }

    public enum SpellModifier : byte
    {
        None      = 0,
        MultiShot = 1, // ±15° 3방향 동시 발사
        Penetrate = 2, // 지형 관통 횟수 (PenetrateCount 만큼)
        Bounce    = 3, // 벽 반사 (Phase 4 예정)
    }

    // 플레이어 엔티티에 부착 — 완드 상태 (슬롯·쿨다운)
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct WandComponent : IComponentData
    {
        [GhostField] public int   ActiveSlot;   // 현재 슬롯 인덱스 (0~7)
        public float CastCooldown;               // 남은 쿨다운 (초)
        public float CastInterval;               // 기본 발사 간격 (초)
        public bool  IsAutoCast;                 // 유지 입력 연사 여부
    }

    // 완드 슬롯 1개의 주문 — DynamicBuffer 로 플레이어 엔티티에 최대 8개 부착
    public struct SpellData : IBufferElementData
    {
        public ProjectileType ProjectileType;
        public LiquidPayload  LiquidPayload;
        public SpellModifier  Modifier;
        public float          ManaCost;
        public float          Damage;
        public float          ProjectileSpeed;
        public float          BlastRadius;    // Explosion 전용
        public int            PenetrateCount; // Penetrate 전용
    }

    // 플레이어 엔티티에 부착 — 마나 상태 (클라이언트에 ghost 동기화해 HUD에 활용)
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct ManaComponent : IComponentData
    {
        [GhostField(Quantization = 10)] public float Current;
        [GhostField(Quantization = 10)] public float Max;
        public float RegenRate; // 초당 회복량
    }

    // 투사체 엔티티에 부착 — 서버 로컬 전용 (ghost 미동기화), SpellProjectileSystem 이 참조
    public struct ProjectileComponent : IComponentData
    {
        public ProjectileType Type;
        public LiquidPayload  Payload;
        public float          BlastRadius;
        public int            PenetrateCount; // 남은 관통 횟수 (0이면 비관통)
    }
}
