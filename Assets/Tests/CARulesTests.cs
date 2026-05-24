using NUnit.Framework;
using SandBlast;

namespace SandBlast.Tests
{
    // CA 시뮬레이션 핵심 규칙에 대한 EditMode 테스트.
    // System.Random 비결정성을 제거하기 위해 그리드 엣지 배치(OOB 벽이 이동 방향을 확정)나
    // Flammability=255(발화 확률 100%) 같은 확정적 조건만 사용한다.
    public class CARulesTests
    {
        PixelGrid        grid;
        ChunkManager     chunks;
        ForceAccumulator forces;
        PixelSimulator   sim;

        [SetUp]
        public void SetUp()
        {
            grid   = new PixelGrid(20, 20);
            chunks = new ChunkManager(grid);
            forces = new ForceAccumulator();
            sim    = new PixelSimulator(grid, chunks, forces);
        }

        // ── POWDER_SAND (모래) ───────────────────────────────────────────

        [Test]
        public void Sand_FallsDown_WhenEmptyBelow()
        {
            // 아래 칸이 비어 있으면 모래는 1틱 후 한 칸 아래로 이동해야 한다
            grid.Set(10, 10, CellType.POWDER_SAND);

            sim.Tick();

            Assert.AreEqual(CellType.EMPTY,       grid.Get(10, 10));
            Assert.AreEqual(CellType.POWDER_SAND, grid.Get(10,  9));
        }

        [Test]
        public void Sand_StaysInPlace_WhenBelowIsOutOfBounds()
        {
            // y=0에서는 아래(y=-1)가 OOB(고체 벽)이며 대각선도 모두 y=-1이므로 이동 불가
            grid.Set(10, 0, CellType.POWDER_SAND);

            sim.Tick();

            Assert.AreEqual(CellType.POWDER_SAND, grid.Get(10, 0));
        }

        [Test]
        public void Sand_BecomesStaticSolid_AfterSleepThresholdTicks()
        {
            // y=0의 모래는 이동할 수 없으므로 매 틱 슬립 카운터가 증가한다.
            // SLEEP_THRESHOLD 틱 후 SOLID_STATIC으로 전환되어야 한다.
            grid.Set(10, 0, CellType.POWDER_SAND);

            for (int i = 0; i < SimulationConstants.SLEEP_THRESHOLD; i++)
                sim.Tick();

            Assert.AreEqual(CellType.SOLID_STATIC, grid.Get(10, 0),
                $"모래는 {SimulationConstants.SLEEP_THRESHOLD}틱 동안 정지 후 SOLID_STATIC으로 변환되어야 합니다");
        }

        // ── LIQUID_WATER (물) ────────────────────────────────────────────

        [Test]
        public void Water_FallsDown_WhenEmptyBelow()
        {
            // 아래 칸이 비어 있으면 물은 1틱 후 한 칸 아래로 이동해야 한다
            grid.Set(10, 10, CellType.LIQUID_WATER);

            sim.Tick();

            Assert.AreEqual(CellType.EMPTY,        grid.Get(10, 10));
            Assert.AreEqual(CellType.LIQUID_WATER, grid.Get(10,  9));
        }

        [Test]
        public void Water_SpreadsSideways_WhenBlockedBelow()
        {
            // x=0에 물 배치: 왼쪽은 OOB(고체 벽), 아래와 대각선 오른쪽 아래는 SOLID로 막음.
            // 유일한 이동 경로가 오른쪽(x=1)뿐이므로 결과가 확정적이다.
            grid.Set(0, 5, CellType.LIQUID_WATER);
            grid.Set(0, 4, CellType.SOLID_STATIC); // 정면 아래 차단
            grid.Set(1, 4, CellType.SOLID_STATIC); // 대각선 오른쪽 아래 차단

            sim.Tick();

            Assert.AreEqual(CellType.EMPTY,        grid.Get(0, 5));
            Assert.AreEqual(CellType.LIQUID_WATER, grid.Get(1, 5));
        }

        // ── FIRE (불) ────────────────────────────────────────────────────

        [Test]
        public void Fire_BecomesEmptyOrAsh_WhenLifetimeReachesZero()
        {
            // 수명이 0인 불은 1틱 후 EMPTY(70%) 또는 POWDER_ASH(30%) 중 하나가 되어야 한다
            grid.Set(10, 10, CellType.FIRE);
            grid.Lifetime[grid.Index(10, 10)] = 0;

            sim.Tick();

            var result = grid.Get(10, 10);
            Assert.That(result,
                Is.EqualTo(CellType.EMPTY).Or.EqualTo(CellType.POWDER_ASH),
                "수명=0인 불은 EMPTY 또는 ASH가 되어야 합니다");
        }

        [Test]
        public void Fire_SpreadsToAdjacentFuel_WithMaxFlammability()
        {
            // Flammability=255이면 발화 확률 = 255/255 = 1.0으로 반드시 불이 옮겨붙는다
            grid.Set(10, 10, CellType.FIRE);
            grid.Lifetime[grid.Index(10, 10)] = 50;

            grid.Set(11, 10, CellType.POWDER_SAND);
            grid.Flammability[grid.Index(11, 10)] = 255;

            sim.Tick();

            Assert.AreEqual(CellType.FIRE, grid.Get(11, 10),
                "Flammability=255인 모래는 인접한 불에 의해 반드시 발화해야 합니다");
        }

        [Test]
        public void Fire_IsExtinguished_ByAdjacentWater()
        {
            // 물이 인접해 있으면 불은 즉시 꺼지고 재(ASH)가 된다
            grid.Set(10, 10, CellType.FIRE);
            grid.Lifetime[grid.Index(10, 10)] = 50;
            grid.Set(10, 11, CellType.LIQUID_WATER); // 불 바로 위에 물 배치

            sim.Tick();

            Assert.AreEqual(CellType.POWDER_ASH, grid.Get(10, 10),
                "물에 인접한 불은 소화되어 ASH가 되어야 합니다");
        }

        [Test]
        public void Fire_EmitsHeat_ToAdjacentCells()
        {
            // 불은 매 틱 인접 셀에 HEAT_EMISSION만큼 열을 전달해야 한다.
            // Flammability=0(기본값)이므로 고체가 자동 발화하지 않아 순수 열 전달만 검증된다.
            grid.Set(10, 10, CellType.FIRE);
            grid.Lifetime[grid.Index(10, 10)] = 50;

            grid.Set(11, 10, CellType.SOLID_STATIC);
            grid.Temperature[grid.Index(11, 10)] = 0;

            sim.Tick();

            Assert.GreaterOrEqual(
                (int)grid.Temperature[grid.Index(11, 10)],
                SimulationConstants.HEAT_EMISSION,
                "인접 셀은 1틱당 최소 HEAT_EMISSION만큼의 열을 받아야 합니다");
        }

        // ── GAS_SMOKE (연기) ─────────────────────────────────────────────

        [Test]
        public void Smoke_RisesUp_WhenEmptyAbove()
        {
            // 위 칸이 비어 있으면 연기는 1틱 후 한 칸 위로 상승해야 한다
            grid.Set(10, 5, CellType.GAS_SMOKE);
            grid.Lifetime[grid.Index(10, 5)] = 20;

            sim.Tick();

            Assert.AreEqual(CellType.EMPTY,     grid.Get(10, 5));
            Assert.AreEqual(CellType.GAS_SMOKE, grid.Get(10, 6));
        }

        [Test]
        public void Smoke_Disappears_WhenLifetimeReachesZero()
        {
            // 수명이 0인 연기는 1틱 후 EMPTY가 되어야 한다
            grid.Set(10, 10, CellType.GAS_SMOKE);
            grid.Lifetime[grid.Index(10, 10)] = 0;

            sim.Tick();

            Assert.AreEqual(CellType.EMPTY, grid.Get(10, 10));
        }
    }
}
