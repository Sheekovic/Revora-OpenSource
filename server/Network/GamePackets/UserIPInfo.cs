// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

namespace Revora.Network.GamePackets
{
    public unsafe class UserIPInfo : Writer
    {
        byte[] Packet;
        public enum UserIPInfoTypes
        {
            LastLogin,
            DifferentCity,
            DifferentPlace
        }
        public UserIPInfo(uint data)
        {
            Packet = new byte[20];
            fixed (byte* Pack = Packet)
            {
                *(ushort*)(Pack + (Packet.Length - 8)) = 0;
                *((ushort*)(Pack + 2)) = PacketID.UserIPInfo;
                *((uint*)(Pack + 4)) = data;
            }
        }
        public bool IP
        {
            get { return BitConverter.ToBoolean(Packet, 8); }
            set { fixed (byte* Pack = Packet) *((bool*)(Pack + 8)) = value; }
        }
        public byte LastIP
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