using Unity.Entities;
using Unity.NetCode;

namespace SandBlast
{
    // 검증용: 현재 틱의 픽셀 그리드 전체 상태 해시값
    public struct PixelHashRpc : IRpcCommand
    {
        public uint Tick;
        public uint Hash;
    }
}
