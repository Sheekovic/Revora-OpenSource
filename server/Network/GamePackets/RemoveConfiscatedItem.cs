// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

namespace Revora.Network.GamePackets
{
    public unsafe class UpdateConfiscatedItem : Writer, Interfaces.IPacket
    {
        byte[] Buffer;
        public UpdateConfiscatedItem(bool Create)
        {
            if (Create)
            {
                Buffer = new byte[28];
                WriteUInt16(20, 0, Buffer);
                WriteUInt16(1035, 2, Buffer);
            }
        }
        public uint Page
        {
            get { return BitConverter.ToUInt32(Buffer, 4); }
            set { WriteUInt32(value, 4, Buffer); }
        }
        public uint Update
        {
            get { return BitConverter.ToUInt32(Buffer, 8); }
            set { WriteUInt32(value, 8, Buffer); }
        }
        public uint ItemUID
        {
            get { return BitConverter.ToUInt32(Buffer, 12); }
            set { WriteUInt32(value, 12, Buffer); }
        }
        public uint DaysLeft
        {
            get { return (BitConverter.ToUInt32(Buffer, 16) + 7); }
            set { WriteUInt32(7 - value, 16, Buffer); }
        }
        public byte[] ToArray()
        {
            return Buffer;
        }
        public void Deserialize(byte[] buffer)
        {
            Buffer = buffer;
        }
        public void Send(Client.GameState client)
        {
            client.Send(Buffer);
        }
    }
}