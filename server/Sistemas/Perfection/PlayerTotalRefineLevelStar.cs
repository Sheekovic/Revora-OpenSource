using Revora.Client;
using Revora.Database;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Revora.Systems_OverPower.PerfectionSystem
{
     [ProtoContract]
    public class CMsgUserTotalRefineLev_3252
    {
        [ProtoMember(1, IsRequired = true)]
        public byte type;
        [ProtoMember(2, IsRequired = true)]
        public uint EntityID;
        [ProtoMember(3, IsRequired = true)]
        public uint StarLeve;
    }
}
