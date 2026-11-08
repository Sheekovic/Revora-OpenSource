using Revora.Client;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Revora.Network.GamePackets.Perfection
{
    public class CMsgUserTotalRefineLevel
    {
        private byte[] _packet;
        public CMsgUserTotalRefineLevel(GameState client)
        {
            _packet = FinalizeProtoBuf(new UserTotalRefineLevelProto() { Id = client.Entity.UID, Level = client.Equipment.GetTPL() });
        }
        private static byte[] FinalizeProtoBuf(UserTotalRefineLevelProto coatStorageArgs)
        {
            using (var memoryStream = new MemoryStream())
            {
                Serializer.SerializeWithLengthPrefix(memoryStream, coatStorageArgs, PrefixStyle.Fixed32);
                var pkt = new byte[8 + memoryStream.Length];
                memoryStream.ToArray().CopyTo(pkt, 0);
                Writer.WriteUshort((ushort)memoryStream.Length, 0, pkt);
                Writer.WriteUshort(3252, 2, pkt);
                return pkt;
            }
        }
        public static implicit operator byte[](CMsgUserTotalRefineLevel packet)
        {
            return packet._packet;
        }
    }
    [ProtoContract]
    public class UserTotalRefineLevelProto
    {
        [ProtoMember(1, IsRequired = true)]
        public int Type;
        [ProtoMember(2, IsRequired = true)]
        public uint Id;
        [ProtoMember(3, IsRequired = true)]
        public short Level;
    }
}
