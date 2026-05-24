namespace SandBlast
{
    public static class SimulationConstants
    {
        // ─── 청크 슬리핑 (§7) ───────────────────────────────
        // 권장: 32×32. 16이면 경계 전파 빈도가 높고, 64이면 보스 전투 시 거의 전체 맵이 활성화됨
        public const int CHUNK_SIZE            = 32;
        // 이 틱 수 동안 이동 셀이 없으면 청크를 SLEEPING으로 전환
        public const int CHUNK_SLEEP_THRESHOLD = 5;

        // ─── 네트워크 동기화 ──────────────────────────────────
        // 픽셀 그리드 델타 RPC 전송 주기 (틱 단위). 30 TPS 기준 5틱 = ~167ms
        public const int SYNC_INTERVAL_TICKS  = 5;

        // ─── 분말·잔해 슬리핑 (§5.4, §6.1) ─────────────────
        // 이 틱 수 동안 이동하지 않으면 SOLID_STATIC으로 전환해 순회에서 제외
        public const int SLEEP_THRESHOLD = 10;

        // ─── 액체 수평 확산 (§6.3, §6.4) ────────────────────
        // 물: 빠른 확산 / 용암: 점성 표현을 위해 1로 제한
        public const int WATER_DISPERSION = 5;
        public const int LAVA_DISPERSION  = 1;

        // ─── 불·연기 (§6.5, §6.6) ────────────────────────────
        public const int   FIRE_LIFE_MAX  = 80;   // 불의 최대 수명(틱)
        public const int   SMOKE_LIFE_MAX = 40;   // 연기의 최대 수명(틱)
        public const float SMOKE_CHANCE   = 0.15f; // 불 위쪽에 연기 생성 확률 (15%)
        public const float ASH_CHANCE     = 0.30f; // 불이 꺼질 때 재(ASH)로 남을 확률 (30%)

        // ─── 온도 (§6.4, §6.5) ───────────────────────────────
        public const int AUTO_IGNITE_TEMP        = 200; // 이 온도 이상이면 연료 자동 발화
        public const int HEAT_EMISSION           = 10;  // 불·용암이 인접 셀에 매 틱 전달하는 열량
        public const int COOLING_RATE            = 2;   // 매 틱 자연 냉각량
        public const int LAVA_SOLIDIFY_THRESHOLD = 30;  // 이 온도 이하면 용암이 SOLID_STATIC으로 굳음
        public const int LAVA_INITIAL_TEMP       = 240; // 용암 생성 시 초기 온도

        // ─── 인화성 프리셋 (§6.5) ────────────────────────────
        // 재질별 기본값. SpawnCell/SpawnCircle 호출 시 flammability 인자로 전달
        public const byte FLAMMABILITY_WOOD      = 80;  // 나무
        public const byte FLAMMABILITY_METAL     = 5;   // 금속
        public const byte FLAMMABILITY_EXPLOSIVE = 255; // 폭발물
        public const byte FLAMMABILITY_DEBRIS    = 40;  // 강체 → 픽셀 전환 잔해 기본값

        // ─── PvP 물질 (Phase 3) ──────────────────────────────
        public const int   POISON_DISPERSION       = 3;    // 독 수평 확산 — 물(5)과 용암(1) 사이
        public const int   GUNPOWDER_BLAST_RADIUS  = 16;   // 화약 폭발 반경 (픽셀 단위, ≈1 유닛)
        public const float POISON_DAMAGE_PER_SECOND = 2f;  // 독 접촉 시 초당 HP 감소

        // ─── 재(ASH) 부유 (§6.2) ─────────────────────────────
        // 매 틱 이 확률로 위로 떠오름 (열기류 연출)
        public const float ASH_FLOAT_CHANCE = 0.20f;

        // ─── 힘 배치 처리 (§9) ───────────────────────────────
        // 매 픽셀마다 AddForce 호출을 막고 틱 끝에 일괄 전달하는 스케일 계수
        public const float EXPLOSION_FORCE_SCALE = 0.5f;
        public const float LIQUID_PRESSURE_SCALE = 0.01f;

        // ─── 유체 틈새 침투 (§8) ─────────────────────────────
        // 끊긴 엣지의 gapWidth 가 이 값 이상이어야 유체가 통과함 (권장: 1~2픽셀)
        public const float FLOW_THRESHOLD = 1.5f;

        // ─── 기절(Stun) 시스템 ───────────────────────────────
        public const float StunThreshold      = 100f;  // 게이지 최댓값
        public const float StunDecayRate      = 10f;   // 초당 자동 감소량
        public const float StunDuration       = 3.0f;  // 기절 지속 시간 (초)
        public const float MeleeStunPower     = 40f;   // 근접 공격 스턴 게이지 증가량
        public const float BulletStunPower    = 15f;   // 투사체 명중 스턴 게이지 증가량
        public const float ExplosionStunPower = 20f;   // 폭발 범위 스턴 게이지 증가량

        // ─── 근접 공격 ───────────────────────────────────────
        public const float MeleeDamage         = 8f;   // 근접 공격 데미지
        public const float MeleeAttackCooldown = 0.6f; // 근접 공격 쿨다운 (초)

        // ─── 삽(Shovel) 아이템 ───────────────────────────────
        public const int   ShovelUseCount = 3;   // 삽 최대 사용 횟수
        public const float ShovelRadiusPx = 6f;  // StampCircle 반경 (픽셀)

        // ─── 매몰 판정 ───────────────────────────────────────
        public const float BurialSolidRatio = 0.8f; // 즉사 Solid 비율 (3×5 = 15개 중 12개)
    }
}
