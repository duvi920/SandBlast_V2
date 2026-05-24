using NUnit.Framework;
using SandBlast;

namespace SandBlast.Tests
{
    // PixelGrid의 경계 처리, 인덱스 변환, Set/Get 동작을 검증한다.
    // 범위 밖 접근이 SOLID_STATIC 벽으로 처리되는 것이 핵심 불변 조건이다.
    public class PixelGridTests
    {
        PixelGrid grid;

        [SetUp]
        public void SetUp() => grid = new PixelGrid(10, 10);

        // ── InBounds ─────────────────────────────────────────────────────

        [Test]
        public void InBounds_ReturnsTrue_ForValidCoordinates()
        {
            // 코너와 중앙 좌표 모두 유효 범위로 인식해야 한다
            Assert.IsTrue(grid.InBounds(0, 0));
            Assert.IsTrue(grid.InBounds(9, 9));
            Assert.IsTrue(grid.InBounds(5, 5));
        }

        [Test]
        public void InBounds_ReturnsFalse_ForOutOfBoundsCoordinates()
        {
            // 음수 및 Width/Height 이상 좌표는 범위 밖으로 판단해야 한다
            Assert.IsFalse(grid.InBounds(-1,  0));
            Assert.IsFalse(grid.InBounds( 0, -1));
            Assert.IsFalse(grid.InBounds(10,  0));
            Assert.IsFalse(grid.InBounds( 0, 10));
        }

        // ── Get ──────────────────────────────────────────────────────────

        [Test]
        public void Get_ReturnsSolidStatic_ForOutOfBoundsCoordinates()
        {
            // 범위 밖 셀은 SOLID_STATIC(고체 벽)으로 반환되어야 한다
            Assert.AreEqual(CellType.SOLID_STATIC, grid.Get(-1,  0));
            Assert.AreEqual(CellType.SOLID_STATIC, grid.Get( 0, -1));
            Assert.AreEqual(CellType.SOLID_STATIC, grid.Get(10,  0));
            Assert.AreEqual(CellType.SOLID_STATIC, grid.Get( 0, 10));
        }

        [Test]
        public void Get_ReturnsCorrectType_AfterSet()
        {
            // Set으로 저장한 타입을 Get이 정확히 반환해야 한다
            grid.Set(3, 4, CellType.POWDER_SAND);
            Assert.AreEqual(CellType.POWDER_SAND, grid.Get(3, 4));
        }

        // ── Set ──────────────────────────────────────────────────────────

        [Test]
        public void Set_IgnoresOutOfBoundsWrite()
        {
            // 범위 밖 Set은 예외 없이 무시되어야 한다
            Assert.DoesNotThrow(() => grid.Set(-1,  0, CellType.POWDER_SAND));
            Assert.DoesNotThrow(() => grid.Set( 0, -1, CellType.POWDER_SAND));
            Assert.DoesNotThrow(() => grid.Set(10,  0, CellType.POWDER_SAND));
        }

        // ── Index ────────────────────────────────────────────────────────

        [Test]
        public void Index_ReturnsCorrectLinearIndex()
        {
            // 선형 인덱스 = y * Width + x
            Assert.AreEqual( 0, grid.Index(0, 0));
            Assert.AreEqual(10, grid.Index(0, 1));  // 1*10 + 0
            Assert.AreEqual(15, grid.Index(5, 1));  // 1*10 + 5
        }

        // ── 초기 상태 ────────────────────────────────────────────────────

        [Test]
        public void NewGrid_IsAllEmpty()
        {
            // 새로 생성한 그리드의 모든 셀은 EMPTY 여야 한다
            for (int y = 0; y < 10; y++)
            for (int x = 0; x < 10; x++)
                Assert.AreEqual(CellType.EMPTY, grid.Get(x, y),
                    $"셀 ({x},{y})는 초기 상태에서 EMPTY여야 합니다");
        }
    }
}
