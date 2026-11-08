using ProtoBuf;
using System;

namespace Revora.Network.GamePackets
{
    public unsafe class ServerTime : Writer, Interfaces.IPacket
    {
        [ProtoContract]
        public class CMsgData
        {
            [ProtoMember(1, IsRequired = true)]
            public uint Type;
            [ProtoMember(2, IsRequired = true)]
            public uint Amount;
            [ProtoMember(3, IsRequired = true)]
            public uint Month;
            [ProtoMember(4, IsRequired = true)]
            public uint DayOfYear;
            [ProtoMember(5, IsRequired = true)]
            public uint DayOfMonth;
            [ProtoMember(6, IsRequired = true)]
            public uint Hour;
            [ProtoMember(7, IsRequired = true)]
            public uint Minute;
            [ProtoMember(8, IsRequired = true)]
            public uint Second;

        }
        public static void Process(byte[] packet, Client.GameState client)
        {
            ServerTime time = new ServerTime();
            time.Year = (uint)DateTime.Now.Year;
            time.Month = (uint)DateTime.Now.Month;
            time.DayOfYear = (uint)DateTime.Now.DayOfYear;
            time.DayOfMonth = (uint)DateTime.Now.Day;
            time.Hour = (uint)DateTime.Now.Hour;
            time.Minute = (uint)DateTime.Now.Minute;
            time.Second = (uint)DateTime.Now.Second;
            client.Send(time);
        }
        public ServerTime()
        {
            Buffer = new CMsgData();
        }
        ushort packetid = 1033;
        CMsgData Buffer;
        public uint Year
        {
            get { return Buffer.Amount; }
            set { Buffer.Amount = value - 1900; }
        }
        public uint Month
        {
            get { return Buffer.Month; }
            set { Buffer.Month = value - 1; }
        }
        public uint DayOfYear
        {
            get { return Buffer.DayOfYear; }
            set { Buffer.DayOfYear = value; }
        }
        public uint DayOfMonth
        {
            get { return Buffer.DayOfMonth; }
            set { Buffer.DayOfMonth = value; }
        }
        public uint Hour
        {
            get { return Buffer.Hour; }
            set { Buffer.Hour = value; }
        }
        public uint Minute
        {
            get { return Buffer.Minute; }
            set { Buffer.Minute = value; }
        }
        public uint Second
        {
            get { return Buffer.Second; }
            set { Buffer.Second = value; }
        }
        public void Deserialize(byte[] buffer)
        {
            this.Buffer = buffer.ConvertToProto<CMsgData>();
        }
        public byte[] ToArray()
        {
            return Buffer.ToConquerPacket(packetid);
        }
        public void Send(Client.GameState client)
        {
            client.Send(ToArray());
        }
    }
}
