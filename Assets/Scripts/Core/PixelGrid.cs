namespace SandBlast
{
    // 테스트 및 비-ECS 코드에서 사용하는 managed 픽셀 그리드.
    // ECS 환경에서는 PixelGridSingleton(NativeArray 기반)이 이를 대체한다.
    public class PixelGrid
    {
        public readonly int Width;
        public readonly int Height;

        public byte[] Type;
        public byte[] Temperature;
        public byte[] Lifetime;
        public byte[] Flammability;

        public PixelGrid(int width, int height)
        {
            Width        = width;
            Height       = height;
            int size     = width * height;
            Type         = new byte[size];
            Temperature  = new byte[size];
            Lifetime     = new byte[size];
            Flammability = new byte[size];
        }

        public int  Index(int x, int y) => y * Width + x;
        public bool InBounds(int x, int y) => (uint)x < (uint)Width && (uint)y < (uint)Height;

        public CellType Get(int x, int y) =>
            InBounds(x, y) ? (CellType)Type[Index(x, y)] : CellType.SOLID_STATIC;

        public void Set(int x, int y, CellType t)
        {
            if (InBounds(x, y)) Type[Index(x, y)] = (byte)t;
        }
    }
}
