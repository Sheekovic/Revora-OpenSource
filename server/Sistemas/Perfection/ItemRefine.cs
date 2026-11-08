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
    public class CMsgItemRefine
    {
        private byte[] _packet;
        public CMsgItemRefine(ConquerItem item , GameState client)
        {
            _packet = FinalizeProtoBuf(new ItemRefineProto()
            {
                ItemUID = item.UID,
                UserId = client.Entity.UID,
                PerfectionLevel = item.PerfectionLevel,
                PerfectionProgress = item.PerfectionProgress,
                CurrentUserId = item.PerfectionOwnerGuid,
                CurrentUserName = item.PerfectionOwner
            });
        }
        private static byte[] FinalizeProtoBuf(ItemRefineProto coatStorageArgs)
        {
            using (var memoryStream = new MemoryStream())
            {
                Serializer.SerializeWithLengthPrefix(memoryStream, coatStorageArgs, PrefixStyle.Fixed32);
                var pkt = new byte[8 + memoryStream.Length];
                memoryStream.ToArray().CopyTo(pkt, 0);
                Writer.WriteUshort((ushort)memoryStream.Length, 0, pkt);
                Writer.WriteUshort(3250, 2, pkt);
                return pkt;
            }
        }
        public static implicit operator byte[](CMsgItemRefine packet)
        {
            return packet._packet;
        }
    }
    [ProtoContract]
    public class ItemRefineProto
    {
        [ProtoMember(1, IsRequired = true)]
        public uint ItemUID;
        [ProtoMember(2, IsRequired = true)]
        public uint UserId;
        [ProtoMember(3, IsRequired = true)]
        public uint PerfectionLevel;
        [ProtoMember(4, IsRequired = true)]
        public uint PerfectionProgress;
        [ProtoMember(5, IsRequired = true)]
        public uint CurrentUserId;
        [ProtoMember(6, IsRequired = true)]
        public string CurrentUserName;
    }
}
