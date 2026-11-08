// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

namespace Revora.Network.GamePackets
{
    public unsafe class VipStatus
    {
        byte[] Packet;
        public VipStatus()
        {
            Packet = new byte[16];
            fixed (byte* Pack = Packet)
            {
                *((ushort*)(Pack)) = 8;
                *((ushort*)(Pack + 2)) = PacketID.VipStatus;
                *((uint*)(Pack + 4)) = 65535;
            }
        }
        public byte[] ToArray()
        {
            return Packet;
        }
    }
}