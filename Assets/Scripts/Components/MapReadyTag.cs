using Unity.Entities;

namespace SandBlast
{
    // 서버가 맵 로드를 완료했을 때 한 번 생성되는 태그 싱글턴.
    // GoInGameSystem은 이 태그가 생길 때까지 플레이어 스폰을 대기한다.
    public struct MapReadyTag : IComponentData { }
}
