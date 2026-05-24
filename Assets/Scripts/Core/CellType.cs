namespace SandBlast
{
    // 픽셀 그리드의 셀 타입. byte 열거체로 Type[] 배열에 직접 저장해 메모리를 최소화함 (설계 문서 §5.1)
    public enum CellType : byte
    {
        EMPTY        = 0, // 빈 공간

        // ── 고체 ──────────────────────────────────────────────
        SOLID_STATIC        = 1,  // 지형 셀(파괴 전) 또는 잔해가 정착해 굳은 상태 — CA 순회에서 제외
        SOLID_RIGID         = 2,  // 이동 강체(RigidBodyNode)가 점유 중인 영역 — 렌더링 시 투명 처리
        SOLID_DEBRIS        = 3,  // 강체에서 픽셀로 전환된 잔해 — 중력 적용, 슬리핑 후 SOLID_STATIC 전환
        SOLID_DIRT          = 10, // 흙 — 파괴 쉬움, 용암 접촉 시 SOLID_BASALT 로 변환
        SOLID_WOOD          = 11, // 나무 — 고인화성(flammability=80), 불로 지형 붕괴 연쇄
        SOLID_GUNPOWDER     = 12, // 화약 — 불/충격 시 주변 16px 반경 폭발
        SOLID_BASALT        = 14, // 현무암 — 용암+흙 반응 생성, 폭발 내성
        SOLID_INDESTRUCTIBLE = 15,// 파괴 불가 — 스폰 반경·핵심 구조물 보호용

        // ── 유체 ──────────────────────────────────────────────
        LIQUID_WATER  = 4,  // 물 — 수평 확산(dispersion=5), 압력 전파
        LIQUID_LAVA   = 5,  // 용암 — 점성(dispersion=1), 냉각 시 SOLID_STATIC 고체화
        LIQUID_POISON = 13, // 독  — 느린 확산(dispersion=3), 접촉 시 지속 피해

        // ── 분말 ──────────────────────────────────────────────
        POWDER_SAND  = 6, // 모래 — 안식각(대각 흐름), 정착 후 SOLID_STATIC 전환
        POWDER_ASH   = 7, // 재 — 모래와 동일하나 20% 확률로 위로 떠오름(열기류 연출)

        // ── 에너지 ────────────────────────────────────────────
        FIRE      = 8, // 불 — 인접 연료로 전파, 수명 소진 시 ASH 또는 EMPTY
        GAS_SMOKE = 9, // 연기 — 위로 상승, 수명 소진 시 EMPTY
    }

    // CellType 에 대한 분류 확장 메서드 — 시뮬레이터 전반에서 타입 판별에 사용
    // Burst IJob 에서도 호출 가능한 순수 값 비교 메서드만 포함
    public static class CellTypeExt
    {
        // 이동을 막는 고체 여부 (SOLID_RIGID 포함 — 강체 점유 영역도 이동 불가)
        public static bool IsSolid(this CellType t) =>
            t == CellType.SOLID_STATIC   || t == CellType.SOLID_RIGID  ||
            t == CellType.SOLID_DEBRIS   || t == CellType.SOLID_DIRT   ||
            t == CellType.SOLID_WOOD     || t == CellType.SOLID_GUNPOWDER ||
            t == CellType.SOLID_BASALT   || t == CellType.SOLID_INDESTRUCTIBLE;

        public static bool IsLiquid(this CellType t) =>
            t == CellType.LIQUID_WATER || t == CellType.LIQUID_LAVA || t == CellType.LIQUID_POISON;

        // 불이 옮겨붙을 수 있는 연료 여부
        public static bool IsFuel(this CellType t) =>
            t == CellType.POWDER_SAND || t == CellType.POWDER_ASH   ||
            t == CellType.SOLID_DEBRIS || t == CellType.SOLID_STATIC ||
            t == CellType.SOLID_WOOD  || t == CellType.SOLID_GUNPOWDER;

        // 폭발·투사체로 파괴할 수 없는 셀 여부
        public static bool IsIndestructible(this CellType t) =>
            t == CellType.SOLID_INDESTRUCTIBLE || t == CellType.SOLID_RIGID ||
            t == CellType.SOLID_BASALT;

        // 투사체 충격으로 제거 가능한 셀 여부 (유체·분말·연약 고체 포함, 정적 지형·잔해·불활성 제외)
        public static bool IsDestructibleByImpact(this CellType t) =>
            t.IsLiquid() ||
            t == CellType.POWDER_SAND || t == CellType.POWDER_ASH  ||
            t == CellType.SOLID_DIRT  || t == CellType.SOLID_WOOD  ||
            t == CellType.SOLID_GUNPOWDER || t == CellType.SOLID_DEBRIS;

        // 셀 타입별 기본 인화성 — SpawnCell 호출 시 flammability 인수로 활용
        public static byte DefaultFlammability(this CellType t) => t switch
        {
            CellType.SOLID_WOOD      => SimulationConstants.FLAMMABILITY_WOOD,
            CellType.SOLID_GUNPOWDER => SimulationConstants.FLAMMABILITY_EXPLOSIVE,
            CellType.SOLID_DEBRIS    => SimulationConstants.FLAMMABILITY_DEBRIS,
            CellType.SOLID_STATIC    => SimulationConstants.FLAMMABILITY_METAL,
            CellType.POWDER_SAND     => 20,
            CellType.POWDER_ASH      => 15,
            _                        => 0,
        };
    }
}
