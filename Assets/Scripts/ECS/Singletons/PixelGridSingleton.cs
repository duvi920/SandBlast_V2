using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace SandBlast
{
    // 픽셀 그리드 전체를 NativeArray 5채널로 보관하는 ECS 싱글턴.
    // PixelGrid(managed class)를 대체한다.
    public struct PixelGridSingleton : IComponentData
    {
        public int    Width;
        public int    Height;
        public float  PixelsPerUnit;
        public float2 GridOrigin;

        // 셀 데이터 채널 (byte per cell)
        public NativeArray<byte> Type;
        public NativeArray<byte> Temperature;
        public NativeArray<byte> Lifetime;
        public NativeArray<byte> RigidId;
        public NativeArray<byte> Flammability;

        public int  Index(int x, int y) => y * Width + x;
        public bool InBounds(int x, int y) => (uint)x < (uint)Width && (uint)y < (uint)Height;

        // 범위 밖은 SOLID_STATIC 반환 — 그리드 테두리가 고체 벽으로 동작하게 함
        public CellType Get(int x, int y) =>
            InBounds(x, y) ? (CellType)Type[Index(x, y)] : CellType.SOLID_STATIC;

        // 월드 좌표 → 그리드 좌표
        public int2 WorldToGrid(float2 worldPos)
        {
            float2 local = worldPos - GridOrigin;
            return new int2(
                (int)math.floor(local.x * PixelsPerUnit),
                (int)math.floor(local.y * PixelsPerUnit));
        }

        // 그리드 좌표 → 그리드 셀 중심 월드 좌표
        public float2 GridToWorld(int gx, int gy) =>
            GridOrigin + new float2((gx + 0.5f) / PixelsPerUnit, (gy + 0.5f) / PixelsPerUnit);
    }
}
