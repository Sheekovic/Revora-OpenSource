using System;

namespace Revora.Network.GamePackets
{
    public class Vigor : Writer, Interfaces.IPacket
    {
        Revora.Network.GamePackets.ServerTime.CMsgData Buffer = null;
        ushort packetid = 1033;
        public Vigor(bool Create)
        {
            if (Create)
            {
                Buffer = new Revora.Network.GamePackets.ServerTime.CMsgData();
                Type = 2;
            }
        }

        public uint Type
        {
            get { return Buffer.Type; }
            set { Buffer.Type = value; }
        }

        public uint Amount
        {
            get { return Buffer.Amount; }
            set { Buffer.Amount = value; }
        }

        public void Deserialize(byte[] buffer)
        {
            Buffer = buffer.ConvertToProto<Revora.Network.GamePackets.ServerTime.CMsgData>();
        }
        public byte[] ToArray()
        {
            return Buffer.ToConquerPacket(packetid);
        }
        public void Send(Client.GameState client)
        {
            if (Buffer != null)
                client.Send(ToArray());
        }
    }
}