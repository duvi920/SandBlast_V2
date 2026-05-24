using NUnit.Framework;
using SandBlast;

namespace SandBlast.Tests
{
    // CellType 확장 메서드(IsSolid, IsLiquid, IsFuel)가
    // 모든 셀 타입에 대해 올바른 분류 결과를 반환하는지 검증한다.
    public class CellTypeTests
    {
        // ── IsSolid ──────────────────────────────────────────────────────

        [Test]
        public void IsSolid_ReturnsTrue_ForAllSolidTypes()
        {
            // 세 가지 고체 타입 모두 IsSolid() == true 여야 한다
            Assert.IsTrue(CellType.SOLID_STATIC.IsSolid());
            Assert.IsTrue(CellType.SOLID_RIGID.IsSolid());
            Assert.IsTrue(CellType.SOLID_DEBRIS.IsSolid());
        }

        [Test]
        public void IsSolid_ReturnsFalse_ForNonSolidTypes()
        {
            // 고체가 아닌 타입은 IsSolid() == false 여야 한다
            Assert.IsFalse(CellType.EMPTY.IsSolid());
            Assert.IsFalse(CellType.LIQUID_WATER.IsSolid());
            Assert.IsFalse(CellType.LIQUID_LAVA.IsSolid());
            Assert.IsFalse(CellType.POWDER_SAND.IsSolid());
            Assert.IsFalse(CellType.POWDER_ASH.IsSolid());
            Assert.IsFalse(CellType.FIRE.IsSolid());
            Assert.IsFalse(CellType.GAS_SMOKE.IsSolid());
        }

        // ── IsLiquid ─────────────────────────────────────────────────────

        [Test]
        public void IsLiquid_ReturnsTrue_ForWaterAndLava()
        {
            // 물과 용암만 IsLiquid() == true 여야 한다
            Assert.IsTrue(CellType.LIQUID_WATER.IsLiquid());
            Assert.IsTrue(CellType.LIQUID_LAVA.IsLiquid());
        }

        [Test]
        public void IsLiquid_ReturnsFalse_ForNonLiquidTypes()
        {
            // 유체가 아닌 타입은 IsLiquid() == false 여야 한다
            Assert.IsFalse(CellType.EMPTY.IsLiquid());
            Assert.IsFalse(CellType.SOLID_STATIC.IsLiquid());
            Assert.IsFalse(CellType.SOLID_RIGID.IsLiquid());
            Assert.IsFalse(CellType.POWDER_SAND.IsLiquid());
            Assert.IsFalse(CellType.FIRE.IsLiquid());
            Assert.IsFalse(CellType.GAS_SMOKE.IsLiquid());
        }

        // ── IsFuel ───────────────────────────────────────────────────────

        [Test]
        public void IsFuel_ReturnsTrue_ForFuelTypes()
        {
            // 불이 옮겨붙을 수 있는 타입은 IsFuel() == true 여야 한다
            Assert.IsTrue(CellType.POWDER_SAND.IsFuel());
            Assert.IsTrue(CellType.POWDER_ASH.IsFuel());
            Assert.IsTrue(CellType.SOLID_DEBRIS.IsFuel());
            Assert.IsTrue(CellType.SOLID_STATIC.IsFuel());
        }

        [Test]
        public void IsFuel_ReturnsFalse_ForNonFuelTypes()
        {
            // 불이 옮겨붙지 않는 타입은 IsFuel() == false 여야 한다
            Assert.IsFalse(CellType.EMPTY.IsFuel());
            Assert.IsFalse(CellType.LIQUID_WATER.IsFuel());
            Assert.IsFalse(CellType.LIQUID_LAVA.IsFuel());
            Assert.IsFalse(CellType.FIRE.IsFuel());
            Assert.IsFalse(CellType.GAS_SMOKE.IsFuel());
            Assert.IsFalse(CellType.SOLID_RIGID.IsFuel()); // 강체 점유 영역은 연료 아님
        }
    }
}
