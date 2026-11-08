using System.Text;

namespace Revora.Network.AuthPackets
{
    public unsafe class Forward : Interfaces.IPacket
    {
        byte[] Buffer;
        public enum ForwardType : byte
        {
            Ready = 2,
            InvalidInfo = 1,
            Banned = 25,
            WrongAccount = 57
        }
        public Forward()
        {
            Buffer = new byte[224];
            Writer.WriteUInt16(224, 0, Buffer);
            Writer.WriteUInt16(1637, 2, Buffer);
        }
        public uint Identifier
        {
            get { return BitConverter.ToUInt32(Buffer, 4); }
            set { Writer.WriteUInt32(value, 4, Buffer); }
        }
        public ForwardType Type
        {
            get
            {
                return (ForwardType)(byte)BitConverter.ToUInt32(Buffer, 12);
            }
            set
            {
                Writer.WriteUInt32((byte)value, 12, Buffer);
            }
        }
        public ushort Port
        {

            get
            {
                return (ushort)BitConverter.ToUInt32(Buffer, 16);
            }
            set
            {
                Writer.WriteUInt32(value, 16, Buffer);
            }
        }
        public uint ClientConfirmation
        {
            get { return BitConverter.ToUInt32(Buffer, 220); }
            set { Writer.WriteUInt32(value, 220, Buffer); }
        }
        public string IP
        {
            get
            {
                return Encoding.Default.GetString(Buffer, 24, 16);
            }
            set
            {
                Writer.WriteString(value, 24, Buffer);
            }
        }
        public byte[] ToArray()
        {
            return Buffer;
        }
        public void Deserialize(byte[] buffer)
        {

        }
        public void Send(Client.GameState client)
        {
            client.Send(Buffer);
        }
    }
}