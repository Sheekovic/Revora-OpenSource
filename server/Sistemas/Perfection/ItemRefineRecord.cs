using Revora.Client;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Revora.Network.GamePackets
{
    public class CMsgItemRefineRecord
    {
        private byte[] _packet;
        public CMsgItemRefineRecord(byte[] incoming)
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
                Data = Serializer.DeserializeWithLengthPrefix<ItemRefineRecordProto>(new MemoryStream(dePacket), PrefixStyle.Fixed32);
                return true;
            }
            catch
            {
                return false;
            }
        }
        public void Handle(GameState client)
        {
            switch (Data.CurrentAction)
            {
                case ItemRefineRecordProto.Action.PerfectionStats:
                case ItemRefineRecordProto.Action.Operation:
                    {
                        client.Send(FinalizeProtoBuf(new ItemRefineRecordProto() { CurrentAction = ItemRefineRecordProto.Action.Operation, dwParam = 54 }));
                    }
                    break;
            }
        }
        private static byte[] FinalizeProtoBuf(ItemRefineRecordProto coatStorageArgs)
        {
            using (var memoryStream = new MemoryStream())
            {
                Serializer.SerializeWithLengthPrefix(memoryStream, coatStorageArgs, PrefixStyle.Fixed32);
                var pkt = new byte[8 + memoryStream.Length];
                memoryStream.ToArray().CopyTo(pkt, 0);
                Writer.WriteUshort((ushort)memoryStream.Length, 0, pkt);
                Writer.WriteUshort(3255, 2, pkt);
                return pkt;
            }
        }
        public ItemRefineRecordProto Data;
    }
    [ProtoContract]
    public class ItemRefineRecordProto
    {
        public enum Action
        {
            Operation = 0,
            PerfectionStats = 1
        }
        [ProtoMember(1, IsRequired = true)]
        public Action CurrentAction;
        [ProtoMember(2, IsRequired = true)]
        public uint dwParam;
        [ProtoMember(3, IsRequired = true)]
        public uint dwParam1;
    }
}
