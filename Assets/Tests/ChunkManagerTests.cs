using NUnit.Framework;
using SandBlast;

namespace SandBlast.Tests
{
    // ChunkManager의 슬리핑 시스템을 검증한다.
    // ACTIVE → SLEEP_PENDING → SLEEPING 전환과 웨이크업 조건이 핵심이다.
    public class ChunkManagerTests
    {
        PixelGrid    grid;
        ChunkManager chunks;

        [SetUp]
        public void SetUp()
        {
            // 64×64 → CHUNK_SIZE=32 기준으로 정확히 2×2 청크 생성
            grid   = new PixelGrid(64, 64);
            chunks = new ChunkManager(grid);
        }

        // ── 초기 상태 ────────────────────────────────────────────────────

        [Test]
        public void AllChunks_AreActive_OnCreation()
        {
            // 생성 직후 모든 청크는 ACTIVE 상태여야 한다
            for (int cy = 0; cy < chunks.ChunksY; cy++)
            for (int cx = 0; cx < chunks.ChunksX; cx++)
                Assert.AreEqual(ChunkState.ACTIVE, chunks.GetChunk(cx, cy).State,
                    $"청크 ({cx},{cy})는 생성 시 ACTIVE여야 합니다");
        }

        // ── 슬리핑 전환 ──────────────────────────────────────────────────

        [Test]
        public void Chunk_TransitionsToSleeping_AfterThreshold_WithNoActivity()
        {
            // 이동 셀 없이 CHUNK_SLEEP_THRESHOLD 틱이 지나면 SLEEPING 상태가 되어야 한다
            for (int i = 0; i < SimulationConstants.CHUNK_SLEEP_THRESHOLD; i++)
                chunks.EndTick(0, 0, anyMoved: false);

            Assert.AreEqual(ChunkState.SLEEPING, chunks.GetChunk(0, 0).State);
        }

        [Test]
        public void Chunk_StaysActive_WhenMovementOccurs()
        {
            // 이동이 발생하면 슬립 전환이 취소되고 ACTIVE를 유지해야 한다
            chunks.EndTick(0, 0, anyMoved: false);
            chunks.EndTick(0, 0, anyMoved: true); // 이동 발생 → 타이머 리셋

            Assert.AreEqual(ChunkState.ACTIVE, chunks.GetChunk(0, 0).State);
        }

        [Test]
        public void SleepTimer_ResetsToZero_AfterMovement()
        {
            // 이동이 발생하면 SleepTimer가 0으로 초기화되어야 한다
            chunks.EndTick(0, 0, anyMoved: false);
            chunks.EndTick(0, 0, anyMoved: false);
            chunks.EndTick(0, 0, anyMoved: true); // 이동 발생

            Assert.AreEqual(0, chunks.GetChunk(0, 0).SleepTimer);
        }

        // ── MarkMoved ────────────────────────────────────────────────────

        [Test]
        public void MarkMoved_ResetsChunkSleepTimer()
        {
            // 셀이 이동하면 해당 청크의 슬립 타이머가 즉시 0으로 리셋되어야 한다
            chunks.EndTick(0, 0, anyMoved: false);
            chunks.EndTick(0, 0, anyMoved: false);

            chunks.MarkMoved(0, 0); // 청크 (0,0) 내부 셀 이동 신호

            ref var meta = ref chunks.GetChunk(0, 0);
            Assert.AreEqual(0, meta.SleepTimer);
            Assert.AreEqual(ChunkState.ACTIVE, meta.State);
        }

        // ── 웨이크업 ─────────────────────────────────────────────────────

        [Test]
        public void SleepingChunk_WakesUp_WhenBorderNeighborMoves()
        {
            // 청크 (0,0)을 슬립 상태로 만든다
            for (int i = 0; i < SimulationConstants.CHUNK_SLEEP_THRESHOLD; i++)
                chunks.EndTick(0, 0, anyMoved: false);
            Assert.AreEqual(ChunkState.SLEEPING, chunks.GetChunk(0, 0).State);

            // x=32는 청크 (1,0)의 왼쪽 경계 셀이다.
            // 이 셀이 이동하면 WakeNeighbor를 통해 인접 청크 (0,0)이 깨어나야 한다.
            chunks.MarkMoved(32, 0);

            Assert.AreNotEqual(ChunkState.SLEEPING, chunks.GetChunk(0, 0).State,
                "오른쪽 이웃 청크의 경계 셀이 이동하면 청크 (0,0)이 깨어나야 합니다");
        }

        // ── IsActive ─────────────────────────────────────────────────────

        [Test]
        public void IsActive_ReturnsFalse_ForSleepingChunk()
        {
            // SLEEPING 상태의 청크에 대해 IsActive는 false를 반환해야 한다
            for (int i = 0; i < SimulationConstants.CHUNK_SLEEP_THRESHOLD; i++)
                chunks.EndTick(0, 0, anyMoved: false);

            Assert.IsFalse(chunks.IsActive(0, 0));
        }

        [Test]
        public void IsActive_ReturnsTrue_ForActiveChunk()
        {
            // ACTIVE 상태의 청크에 대해 IsActive는 true를 반환해야 한다
            Assert.IsTrue(chunks.IsActive(0, 0));
        }

        // ── MarkRigidDirty ───────────────────────────────────────────────

        [Test]
        public void MarkRigidDirty_KeepsChunkActive()
        {
            // 강체 마스크 변경(DirtyFlag)이 있으면 청크는 슬립되지 않고 ACTIVE를 유지해야 한다
            chunks.EndTick(0, 0, anyMoved: false); // 슬립 타이머 부분 진행
            chunks.EndTick(0, 0, anyMoved: false);

            chunks.MarkRigidDirty(0, 0);

            ref var meta = ref chunks.GetChunk(0, 0);
            Assert.AreEqual(ChunkState.ACTIVE, meta.State);
            Assert.IsTrue(meta.DirtyFlag);
        }
    }
}
