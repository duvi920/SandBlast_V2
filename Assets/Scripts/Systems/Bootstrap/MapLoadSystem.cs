using SandBlast;
using SandBlast.Arena;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

// 서버 및 로컬 월드 — 시작 직후 한 번만 MapTemplate을 PixelGrid에 적용한다.
// MapTemplateReference가 없으면 기본 바닥을 생성하고 MapReadyTag를 추가한다.
[UpdateInGroup(typeof(SimulationSystemGroup))]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
public partial class MapLoadSystem : SystemBase
{
    protected override void OnCreate()
    {
        // MapTemplateReference는 없어도 동작하도록 RequireForUpdate 제거
        RequireForUpdate<PixelGridSingleton>();
        RequireForUpdate<ChunkManagerSingleton>();
    }

    protected override void OnUpdate()
    {
        Enabled = false;

        var grid   = SystemAPI.GetSingleton<PixelGridSingleton>();
        var chunks = SystemAPI.GetSingleton<ChunkManagerSingleton>();

        var templateQuery = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<MapTemplateReference>());
        bool hasTemplate  = !templateQuery.IsEmpty;
        templateQuery.Dispose();

        if (hasTemplate)
        {
            var templateRef = SystemAPI.ManagedAPI.GetSingleton<MapTemplateReference>();
            if (templateRef?.Value != null)
            {
                ArenaMapBuilder.Apply(templateRef.Value, ref grid, ref chunks);
                SystemAPI.SetSingleton(grid);
                SystemAPI.SetSingleton(chunks);
                SpawnWandItems(templateRef.Value);
            }
        }
        else
        {
            BuildDefaultFloor(ref grid, ref chunks);
            SystemAPI.SetSingleton(grid);
            SystemAPI.SetSingleton(chunks);
        }

        // 맵 준비 완료 신호 — GoInGameSystem이 이 태그를 기다림
        EntityManager.CreateEntity(ComponentType.ReadWrite<MapReadyTag>());
    }

    // 맵 템플릿이 없을 때: 그리드 하단 4픽셀을 SOLID_STATIC 바닥으로, 좌우 벽 2픽셀로 채운다.
    static void BuildDefaultFloor(ref PixelGridSingleton grid, ref ChunkManagerSingleton chunks)
    {
        int w = grid.Width, h = grid.Height;

        for (int x = 0; x < w; x++)
        for (int y = 0; y < 4; y++)
        {
            int idx = grid.Index(x, y);
            grid.Type[idx]         = (byte)CellType.SOLID_STATIC;
            grid.Flammability[idx] = 0;
            chunks.MarkMoved(x, y);
        }

        // 좌우 벽
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < 2; x++)
            {
                int idx = grid.Index(x, y);
                grid.Type[idx] = (byte)CellType.SOLID_STATIC;
                chunks.MarkMoved(x, y);
            }
            for (int x = w - 2; x < w; x++)
            {
                int idx = grid.Index(x, y);
                grid.Type[idx] = (byte)CellType.SOLID_STATIC;
                chunks.MarkMoved(x, y);
            }
        }
    }

    void SpawnWandItems(MapTemplate template)
    {
        if (!SystemAPI.TryGetSingleton<GamePrefabs>(out var prefabs)) return;
        if (prefabs.WandItemPrefab == Entity.Null) return;

        var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

        foreach (var ws in template.WandSpawnPoints)
        {
            var grid   = SystemAPI.GetSingleton<PixelGridSingleton>();
            var worldPos = grid.GridToWorld(ws.GridCell.x, ws.GridCell.y);

            var item = ecb.Instantiate(prefabs.WandItemPrefab);
            ecb.SetComponent(item, new ItemGhostData
            {
                Position  = worldPos,
                ItemType  = 2,             // 완드
                ItemId    = ws.WandPresetId,
                IsPickedUp = false
            });
            ecb.SetComponent(item, Unity.Transforms.LocalTransform.FromPosition(
                new Unity.Mathematics.float3(worldPos.x, worldPos.y, 0f)));
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}
