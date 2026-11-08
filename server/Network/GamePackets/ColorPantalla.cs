using System;

namespace Revora.Network.GamePackets
{
    public class ScreenColor : Writer, Interfaces.IPacket//Ahmed~Zero
    {
        public ScreenColor(bool Create)
        {
            if (Create)
            {
                Buffer = new byte[45];
                WriteUInt16((ushort)(Buffer.Length - 8), 0, Buffer);
                WriteUInt16(10010, 2, Buffer);
            }
        }
        byte[] Buffer;

        public uint UID
        {
            get { return BitConverter.ToUInt32(Buffer, 4); }
            set { Writer.WriteUInt32(value, 4, Buffer); }
        }

        public uint dwParam
        {
            get { return BitConverter.ToUInt32(Buffer, 8); }
            set { WriteUInt32(value, 8, Buffer); }
        }

        public ushort ID
        {
            get { return BitConverter.ToUInt16(Buffer, 20); }
            set { WriteUInt16(value, 20, Buffer); }
        }

        public void Deserialize(byte[] buffer)
        {
            this.Buffer = buffer;
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

