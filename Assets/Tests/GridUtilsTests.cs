using NUnit.Framework;
using UnityEngine;
using SandBlast;

namespace SandBlast.Tests
{
    // GridUtils.PointInPolygon (조르단 곡선 정리 기반 점-폴리곤 판별)을 검증한다.
    // 볼록 다각형(정사각형, 삼각형)과 오목 다각형(L자)을 모두 테스트한다.
    public class GridUtilsTests
    {
        // 단위 정사각형: (0,0)→(1,0)→(1,1)→(0,1)
        static readonly Vector2[] Square = new[]
        {
            new Vector2(0, 0), new Vector2(1, 0),
            new Vector2(1, 1), new Vector2(0, 1),
        };

        // 이등변삼각형: 밑변 (0,0)→(4,0), 꼭짓점 (2,4)
        static readonly Vector2[] Triangle = new[]
        {
            new Vector2(0, 0), new Vector2(4, 0), new Vector2(2, 4),
        };

        // L자형(오목): 오른쪽 위 모서리가 잘린 2×2 사각형
        static readonly Vector2[] LShape = new[]
        {
            new Vector2(0, 0), new Vector2(2, 0), new Vector2(2, 1),
            new Vector2(1, 1), new Vector2(1, 2), new Vector2(0, 2),
        };

        // ── 정사각형 ─────────────────────────────────────────────────────

        [Test]
        public void PointInPolygon_ReturnsTrue_ForCentreOfSquare()
        {
            // 정사각형 중심점은 내부로 판정되어야 한다
            Assert.IsTrue(GridUtils.PointInPolygon(new Vector2(0.5f, 0.5f), Square));
        }

        [Test]
        public void PointInPolygon_ReturnsFalse_ForPointOutsideSquare()
        {
            // 정사각형 외부의 여러 방향 점들은 외부로 판정되어야 한다
            Assert.IsFalse(GridUtils.PointInPolygon(new Vector2( 2f,  2f), Square)); // 오른쪽 위
            Assert.IsFalse(GridUtils.PointInPolygon(new Vector2(-1f, 0.5f), Square)); // 왼쪽
            Assert.IsFalse(GridUtils.PointInPolygon(new Vector2(0.5f, 2f), Square)); // 위쪽
        }

        // ── 삼각형 ───────────────────────────────────────────────────────

        [Test]
        public void PointInPolygon_ReturnsTrue_ForPointInsideTriangle()
        {
            // 삼각형 내부 점은 내부로 판정되어야 한다
            Assert.IsTrue(GridUtils.PointInPolygon(new Vector2(2f, 1f), Triangle));
        }

        [Test]
        public void PointInPolygon_ReturnsFalse_ForPointOutsideTriangle()
        {
            // 삼각형 외부 점들(왼쪽 위, 오른쪽 위, 꼭짓점 너머)은 외부로 판정되어야 한다
            Assert.IsFalse(GridUtils.PointInPolygon(new Vector2(0.1f, 3f), Triangle));
            Assert.IsFalse(GridUtils.PointInPolygon(new Vector2(3.9f, 3f), Triangle));
            Assert.IsFalse(GridUtils.PointInPolygon(new Vector2(2f,   5f), Triangle));
        }

        // ── L자형(오목 다각형) ────────────────────────────────────────────

        [Test]
        public void PointInPolygon_ReturnsTrue_ForPointInsideLShape()
        {
            // L자 내부의 두 점(아래 부분, 왼쪽 위 부분)은 내부로 판정되어야 한다
            Assert.IsTrue(GridUtils.PointInPolygon(new Vector2(0.5f, 0.5f), LShape)); // 아래 구간
            Assert.IsTrue(GridUtils.PointInPolygon(new Vector2(0.5f, 1.5f), LShape)); // 위 구간
        }

        [Test]
        public void PointInPolygon_ReturnsFalse_ForPointInNotchOfLShape()
        {
            // (1.5, 1.5)는 L자의 오른쪽 위 잘린 부분(오목 홈)으로 외부여야 한다
            Assert.IsFalse(GridUtils.PointInPolygon(new Vector2(1.5f, 1.5f), LShape));
        }
    }
}
