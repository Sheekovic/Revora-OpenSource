// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

namespace Revora.Network.GamePackets
{
    public unsafe class BlackSpotPacket : Writer
    {
        byte[] packet = new byte[20];
        public BlackSpotPacket()
        {
            WriteUInt16(12, 0, packet);
            WriteUInt16(2081, 2, packet);
        }
        public byte[] ToArray(bool show, uint uid)
        {
            if (show) WriteUInt32(0, 4, packet);
            else WriteUInt32(1, 4, packet);
            WriteUInt32(uid, 8, packet);
            return packet;
        }
    }
}