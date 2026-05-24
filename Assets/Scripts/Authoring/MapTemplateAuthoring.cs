using SandBlast.Arena;
using Unity.Entities;
using UnityEngine;

// MapLoader 오브젝트에 부착 — MapTemplate 에셋을 ECS managed component로 굽는다.
public class MapTemplateAuthoring : MonoBehaviour
{
    public MapTemplate Template;

    class Baker : Baker<MapTemplateAuthoring>
    {
        public override void Bake(MapTemplateAuthoring authoring)
        {
            if (authoring.Template == null) return;
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponentObject(entity, new MapTemplateReference { Value = authoring.Template });
        }
    }
}
