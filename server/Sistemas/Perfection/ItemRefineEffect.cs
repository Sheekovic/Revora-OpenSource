using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Revora.Network.GamePackets
{
    public class CMsgRefineEffect
    {
        private byte[] _packet;
        public CMsgRefineEffect(uint UserId, RefineEffects refineEffect) 
        {
            _packet = FinalizeProtoBuf(new RefineEffectProto() { Id = UserId, dwParam = 0, Effect = refineEffect });
        }
        private static byte[] FinalizeProtoBuf(RefineEffectProto coatStorageArgs)
        {
            using (var memoryStream = new MemoryStream())
            {
                Serializer.SerializeWithLengthPrefix(memoryStream, coatStorageArgs, PrefixStyle.Fixed32);
                var pkt = new byte[8 + memoryStream.Length];
                memoryStream.ToArray().CopyTo(pkt, 0);
                Writer.WriteUshort((ushort)memoryStream.Length, 0, pkt);
                Writer.WriteUshort(3254, 2, pkt);
                return pkt;
            }
        }
        public static implicit operator byte[](CMsgRefineEffect effect)
        {
            return effect._packet;
        }
    }
    [ProtoContract]
    public class RefineEffectProto
    {
        [ProtoMember(1, IsRequired = true)]
        public uint Id;
        [ProtoMember(2, IsRequired = true)]
        public int dwParam;
        [ProtoMember(3, IsRequired = true)]
        public RefineEffects Effect;
    }
    public enum RefineEffects
    {
        ToxinEraserLevel,
        StrikeLockLevel,
        LuckyStrike,
        CalmWind,
        DrainingTouch,
        BloodSpawn,
        LightOfStamina,
        ShiledBreak,
        KillingFlash,
        MirrorOfSin,
        DivineGuard,
        CoreStrike,
        InvisbleArrow,
        FreeSoul,
        StraightLife,
        AbsoluteLuck
    }
}
