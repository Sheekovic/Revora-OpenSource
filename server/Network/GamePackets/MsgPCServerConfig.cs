using System;

namespace Revora.Network.GamePackets
{
    public class MsgPCServerConfig : Writer, Interfaces.IPacket
    {
        byte[] Buffer;
        public MsgPCServerConfig(bool Create)
        {
            if (Create)
            {
                Buffer = new byte[13 + 8];
                Writer.Write(13, 0, Buffer);
                Writer.Write(1049, 2, Buffer);
            }
        }
        public byte Type
        {
            get { return Buffer[9]; }
            set { Buffer[9] = value; }
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
