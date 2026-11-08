using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Revora.Network.GamePackets
{
    public unsafe class MsgExchangeShop
    {
        private byte[] _packet;
        public MsgExchangeShop(byte[] incoming)
        {
            _packet = incoming;
        }
        public bool Read()
        {
            try
            {
                var dePacket = new byte[_packet.Length - 8];

                for (int i = 0; i < dePacket.Length; i++)
                {
                    dePacket[i] = _packet[i];
                }
                Data = Serializer.DeserializeWithLengthPrefix<ProtoExchangeShop>(new System.IO.MemoryStream(dePacket), PrefixStyle.Fixed32);
                return true;
            }
            catch
            {
                return false;
            }
        }
        public ProtoExchangeShop Data;
        public static implicit operator byte[] (MsgExchangeShop packet)
        {
            return packet._packet;
        }
        public static byte[] FinalizeProtoBuf(ProtoExchangeShop ExchangeShopArgs)
        {
            using (var memoryStream = new MemoryStream())
            {
                Serializer.SerializeWithLengthPrefix(memoryStream, ExchangeShopArgs, PrefixStyle.Fixed32);
                var pkt = new byte[8 + memoryStream.Length];
                memoryStream.ToArray().CopyTo(pkt, 0);
                Writer.WriteUshort((ushort)memoryStream.Length, 0, pkt);
                Writer.WriteUshort(2441, 2, pkt);
                return pkt;
            }
        }
    }
    [ProtoContract]
    public class ProtoExchangeShop
    {
        [ProtoMember(1, IsRequired = true)]
        public uint ShopID;
        [ProtoMember(2, IsRequired = true)]
        public uint Action;
        [ProtoMember(3, IsRequired = true)]
        public uint TimeIn;
        [ProtoMember(4, IsRequired = true)]
        public uint TimeInSeconds;
    }
}