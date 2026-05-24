namespace SandBlast
{
    // 강체 그래프 엣지의 상태. 끊어진(BROKEN) 엣지의 틈 너비가 FLOW_THRESHOLD 이상이면 유체가 통과한다 (설계 문서 §8)
    public enum EdgeState : byte { INTACT, BROKEN }

    // 인접 두 지형 노드 사이의 구조적 연결 (조인트/체인).
    // 데미지를 받으면 BROKEN 으로 전환되고, 연결된 모든 엣지가 BROKEN 이 되면 노드가 픽셀로 전환된다.
    public class Edge
    {
        public readonly int NodeA;
        public readonly int NodeB;
        public EdgeState    State    = EdgeState.INTACT;
        public float        GapWidth = 0f;  // 엣지가 끊길 때 해당 폴리곤 경계 길이를 저장 — 유체 침투 판정에 사용

        public Edge(int a, int b)
        {
            NodeA = a;
            NodeB = b;
        }
    }
}
