// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

namespace Revora.Network.GamePackets
{
    internal unsafe class Lottery
    {
        private byte[] Buffer;
        public enum LotteryTypes
        {
            Accept = 0,
            AddJade = 1,
            Continue = 2,
            ShowGUI = 259
        }
        public Lottery()
        {
            Buffer = new byte[34];
            Writer.WriteUshort(26, 0, Buffer);
            Writer.WriteUshort((ushort)1314, 2, Buffer);
            Writer.WriteUshort((byte)1, 6, Buffer);
        }
        public LotteryTypes Type
        {
            get { return (LotteryTypes)BitConverter.ToUInt16(Buffer, 4); }
            set { Writer.WriteUshort((ushort)value, 4, Buffer); }
        }
        public byte SocketOne
        {
            get { return Buffer[7]; }
            set { Buffer[7] = value; }
        }
        public byte SocketTwo
        {
            get { return Buffer[8]; }
            set { Buffer[8] = value; }
        }
        public byte Plus
        {
            get { return Buffer[9]; }
            set { Buffer[9] = value; }
        }
        public Game.Enums.Color Color
        {
            get { return (Game.Enums.Color)Buffer[10]; }
            set { Buffer[10] = (byte)value; }
        }
        public byte AddJadeChances
        {
            get { return Buffer[11]; }
            set { Buffer[11] = value; }
        }
        public uint Prize
        {
            get { return BitConverter.ToUInt32(Buffer, 12); }
            set { Writer.WriteUInt32(value, 12, Buffer); }
        }
        public byte[] ToArray()
        {
            return Buffer;
        }
        public void Deserialize(byte[] buffer)
        {
            this.Buffer = buffer;
        }
        public void Send(Client.GameState client)
        {
            client.Send(Buffer);
        }
    }
}
