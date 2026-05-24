using UnityEngine;

namespace SandBlast
{
    public static class GridUtils
    {
        // 레이 캐스팅(조르단 곡선 정리) 방식의 점-폴리곤 포함 판정.
        // 점에서 오른쪽으로 무한 광선을 쏴 폴리곤 변과 홀수 번 교차하면 내부.
        // 강체 → 픽셀 전환 시 폴리곤 래스터화와 고체 마스크 동기화에서 사용한다.
        public static bool PointInPolygon(Vector2 point, Vector2[] poly)
        {
            bool inside = false;
            int  n      = poly.Length;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                float xi = poly[i].x, yi = poly[i].y;
                float xj = poly[j].x, yj = poly[j].y;
                if (((yi > point.y) != (yj > point.y)) &&
                    point.x < (xj - xi) * (point.y - yi) / (yj - yi) + xi)
                    inside = !inside;
            }
            return inside;
        }
    }
}
