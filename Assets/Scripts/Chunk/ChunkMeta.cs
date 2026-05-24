namespace SandBlast
{
    // 청크 상태 전이: ACTIVE → SLEEP_PENDING → SLEEPING
    // SLEEPING 청크는 TickAll 에서 완전히 스킵된다 (설계 문서 §7.2)
    public enum ChunkState : byte { ACTIVE, SLEEP_PENDING, SLEEPING }

    // 청크 한 개의 슬리핑 상태를 추적하는 경량 구조체 (설계 문서 §7.1)
    public struct ChunkMeta
    {
        public ChunkState State;
        public byte       SleepTimer;  // 이동 셀 없는 연속 틱 카운트 — CHUNK_SLEEP_THRESHOLD 초과 시 SLEEPING
        public bool       DirtyFlag;   // 이번 틱에 강체 마스크가 변경됨 → 강제 ACTIVE 유지
        public bool       BorderDirty; // 인접 청크에서 셀이 유입됨 → 강제 ACTIVE 유지
        public bool       SyncDirty;   // 마지막 동기화 이후 픽셀이 변경됨 → PixelGridSyncSystem이 처리 후 해제
    }
}
