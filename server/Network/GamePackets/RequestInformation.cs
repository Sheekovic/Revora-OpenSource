// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

namespace Revora.Network.GamePackets
{
    public unsafe class RequestInformation : Writer, Interfaces.IPacket
    {
        byte[] Buffer;
        public const uint Request = 1;
        public RequestInformation()
        {
            Buffer = new byte[40];
            WriteUInt16(32, 0, Buffer);
            WriteUInt16(2071, 2, Buffer);
        }
        public uint Requester
        {
            get { return BitConverter.ToUInt32(Buffer, 4); }
            set { WriteUInt32(value, 4, Buffer); }
        }
        public uint Receiver
        {
            get { return BitConverter.ToUInt32(Buffer, 8); }
            set { WriteUInt32(value, 8, Buffer); }
        }
        public uint Level
        {
            get { return BitConverter.ToUInt32(Buffer, 12); }
            set { WriteUInt32(value, 12, Buffer); }
        }
        public uint BattlePower
        {
            get { return BitConverter.ToUInt32(Buffer, 16); }
            set { WriteUInt32(value, 16, Buffer); }
        }
        public uint Spouse
        {
            get { return BitConverter.ToUInt32(Buffer, 20); }
            set { WriteUInt32(value, 20, Buffer); }
        }
        public uint Friends
        {
            get { return BitConverter.ToUInt32(Buffer, 21); }
            set { WriteUInt32(value, 21, Buffer); }
        }
        public uint TradePartner
        {
            get { return BitConverter.ToUInt32(Buffer, 22); }
            set { WriteUInt32(value, 22, Buffer); }
        }
        public uint Mentor
        {
            get { return BitConverter.ToUInt32(Buffer, 23); }
            set { WriteUInt32(value, 23, Buffer); }
        }
        public uint Apprentice
        {
            get { return BitConverter.ToUInt32(Buffer, 24); }
            set { WriteUInt32(value, 24, Buffer); }
        }
        public uint Team
        {
            get { return BitConverter.ToUInt32(Buffer, 25); }
            set { WriteUInt32(value, 25, Buffer); }
        }
        public uint GuildRank
        {
            get { return BitConverter.ToUInt32(Buffer, 26); }
            set { WriteUInt32(value, 26, Buffer); }
        }
        public uint Enemy
        {
            get { return BitConverter.ToUInt32(Buffer, 27); }
            set { WriteUInt32(value, 27, Buffer); }
        }
        public void Deserialize(byte[] buffer)
        {
            Buffer = buffer;
        }
        public byte[] ToArray()
        {
            return Buffer;
        }
        public void Send(Client.GameState client)
        {
            client.Send(Buffer);
        }
    }
}