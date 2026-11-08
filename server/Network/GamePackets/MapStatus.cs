using ProtoBuf;

namespace Revora.Network.GamePackets
{
    public class MapStatus : Writer, Interfaces.IPacket
    {
        [ProtoContract]
        public class CMsgMapInfo
        {
            [ProtoMember(1, IsRequired = true)]
            public uint ID;
            [ProtoMember(2, IsRequired = false)]
            public uint BaseID;
            [ProtoMember(3, IsRequired = false)]
            public ulong Status;
            [ProtoMember(4, IsRequired = false)]
            public uint Weather;
        }
        private CMsgMapInfo Buffer;
        public MapStatus()
        {
            Buffer = new CMsgMapInfo();
        }
        public uint ID
        {
            get { return Buffer.ID; }
            set { Buffer.ID = value; }
        }
        public uint BaseID
        {
            get { return Buffer.BaseID; }
            set { Buffer.BaseID = value; }
        }
        public ulong Status
        {
            get { return Buffer.Status; }
            set { Buffer.Status = value; }
        }
        public uint Weather
        {
            get { return Buffer.Weather; }
            set { Buffer.Weather = value; }
        }
        public byte[] ToArray()
        {

            return Buffer.ToConquerPacket(1110);
        }
        public void Deserialize(byte[] buffer)
        {
        }
        public void Send(Client.GameState client)
        {
            client.Send(ToArray());
        }
    }
}