using Unity.Entities;

namespace SandBlast.Arena
{
    // 관리형 IComponentData — 서버 월드의 MapLoader 엔티티에 부착.
    // ScriptableObject는 Burst/NativeArray 외부이므로 managed component로 보관한다.
    public class MapTemplateReference : IComponentData
    {
        public MapTemplate Value;
    }
}
