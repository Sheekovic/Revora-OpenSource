// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

namespace Revora.Network.GamePackets
{
    public unsafe static class PacketBuilder
    {
        public static void AppendHeader(byte* ptr, int size, ushort type)
        {
            *((ushort*)ptr) = (ushort)(size - 8);
            *((ushort*)(ptr + 2)) = type;
        }
    }
}
