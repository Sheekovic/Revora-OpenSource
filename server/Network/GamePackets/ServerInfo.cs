// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

namespace Revora.Network.GamePackets
{
    public unsafe class ServerInfo
    {
        byte[] Packet;
        public ServerInfo()
        {
            Packet = new byte[16];
            fixed (byte* Pack = Packet)
            {
                *(ushort*)(Pack + (Packet.Length - 8)) = 0;
                *((ushort*)(Pack + 2)) = PacketID.ServerInfo;
            }
        }
        public uint Time
        {
            get { return BitConverter.ToUInt32(Packet, 4); }
            set { fixed (byte* Pack = Packet) *((uint*)(Pack + 4)) = value; }
        }
        public byte Info
        {
            get { return (byte)Packet[9]; }
            set { fixed (byte* Pack = Packet) *((byte*)(Pack + 9)) = value; }
        }
        public byte[] ToArray()
        {
            fixed (void* Pack = Packet)
                return Packet;
        }
    }
}