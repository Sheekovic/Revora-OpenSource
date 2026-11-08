using ProtoBuf;
using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Collections.Concurrent;
using System.Text;
using Revora;
using Revora.Database;
using System.Threading.Tasks;

namespace Revora.Network.GamePackets
{
    public class MsgFixedPezzi
    {
        public MsgFixedPezzi() { }
        public MsgFixedPezziProto Info;
        public MsgFixedPezziProto2 Info2;
        [ProtoContract]
        public class MsgFixedPezziProto
        {
            [ProtoMember(1, IsRequired = true)]
            public uint ActionId;
            [ProtoMember(2, IsRequired = true)]
            public uint ItemUID;
            [ProtoMember(3, IsRequired = true)]
            public string Signature;
            [ProtoMember(4, IsRequired = true)]
            public uint ItemPlusUID;
        }
        [ProtoContract]
        public class MsgFixedPezziProto2
        {
            [ProtoMember(1, IsRequired = true)]
            public uint ItemUID;
            [ProtoMember(2, IsRequired = true)]
            public uint EntityUID;
            [ProtoMember(3, IsRequired = true)]
            public uint Stars;
            [ProtoMember(4, IsRequired = true)]
            public uint Progress;
            [ProtoMember(5, IsRequired = true)]
            public uint OwnerUID;
            [ProtoMember(6, IsRequired = true)]
            public string OwnerName;
        }
        public double GetPerfectionProgress(uint value)
        {
            return (value * 1.25);
        }
        public uint GetPerfectionProgress(ConquerItem item)
        {
            if (item.PerfectionLevel == 0) return 30;
            if (item.PerfectionLevel == 1) return 60;
            if (item.PerfectionLevel == 2) return 100;
            if (item.PerfectionLevel == 3) return 200;
            if (item.PerfectionLevel == 4) return 350;
            if (item.PerfectionLevel == 5) return 600;
            if (item.PerfectionLevel == 6) return 1000;
            if (item.PerfectionLevel == 7) return 1500;
            if (item.PerfectionLevel == 8) return 2300;
            if (item.PerfectionLevel == 9) return 3500;
            if (item.PerfectionLevel == 10) return 5000;
            if (item.PerfectionLevel == 11) return 6500;
            if (item.PerfectionLevel == 12) return 8000;
            if (item.PerfectionLevel == 13) return 9500;
            if (item.PerfectionLevel == 14) return 11000;
            return 12000;
        }
        public uint GetPlusStonePoints(uint value)
        {
            if (value == 3009000) return 10;
            if (value == 3009001) return 100;
            if (value == 3009002) return 1000;
            if (value == 3009003) return 10000;
            if (value == 730001 || value == 1) return 1;
            if (value == 730002 || value == 2) return 1;
            if (value == 730003 || value == 3) return 1;
            if (value == 730004 || value == 4) return 1;
            if (value == 730005 || value == 5) return 1;
            if (value == 730006 || value == 6) return 1;
            if (value == 730007 || value == 7) return 1;
            if (value == 730008 || value == 8) return 2;
            if (value == 730009 || value == 9) return 1;
            // if (value == 9) return 87480;
            //if (value == 10) return 90180;
            //if (value == 11) return 95680;
            //if (value == 12) return 104680;
            return 0;
        }
        public uint GetTransferCost(Network.GamePackets.ConquerItem item)
        {
            if (item.PerfectionLevel == 2) return 30;
            if (item.PerfectionLevel == 3) return 70;
            if (item.PerfectionLevel == 4) return 150;
            if (item.PerfectionLevel == 5) return 300;
            if (item.PerfectionLevel == 6) return 500;
            if (item.PerfectionLevel == 7) return 800;
            if (item.PerfectionLevel == 8) return 1100;
            if (item.PerfectionLevel >= 9) return 1500;
            return 10;
        }
        public byte GetStars(byte Stars)
        {
            if (Stars >= 0 && Stars <= 8) return 9;
            if (Stars >= 9 && Stars <= 17) return 18;
            if (Stars >= 18 && Stars <= 26) return 27;
            if (Stars >= 27 && Stars <= 35) return 36;
            if (Stars >= 36 && Stars <= 44) return 45;
            if (Stars >= 45 && Stars <= 53) return 54;
            return 0;
        }
        public byte[] GetBytes(string packetString)
        {
            if (packetString != "" && packetString != null && packetString != string.Empty && packetString != " ")
            {
                byte[] bytes = new byte[(packetString.Length / 3) + 1];
                int t = 0;
                for (int i = 0; i < packetString.Length; i += 3)
                {
                    string cha = packetString.Substring(i, 2);
                    bytes[t] = byte.Parse(cha, System.Globalization.NumberStyles.HexNumber);
                    t++;
                }
                return bytes;
            }
            return null;
        }
        public bool Read(byte[] packet)
        {
            try
            {
                var mypkt = new byte[packet.Length - 4];
                Array.Copy(packet, 4, mypkt, 0, mypkt.Length);
                using (var memoryStream = new MemoryStream(packet))
                {
                    Info = Serializer.DeserializeWithLengthPrefix<MsgFixedPezziProto>(memoryStream, PrefixStyle.Fixed32);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                return false;
            }
            return true;
        }
    }
}
