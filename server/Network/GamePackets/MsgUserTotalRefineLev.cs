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
    public class MsgUserTotalRefineLev
    {
        public MsgUserTotalRefineLev() { }
        public MsgUserTotalRefineLevProto Info;
        [ProtoContract]
        public class MsgUserTotalRefineLevProto
        {
            [ProtoMember(1, IsRequired = true)]
            public uint ActionId;
            [ProtoMember(2, IsRequired = true)]
            public uint EntityUID;
            [ProtoMember(3, IsRequired = true)]
            public uint TotalPrefectionLevel;
        }
        
        public void Send(Client.GameState client)
        {
            client.Send(Kernel.FinalizeProtoBuf(new MsgUserTotalRefineLevProto()
            {
                ActionId = 0,
                EntityUID = client.Entity.UID,
                TotalPrefectionLevel = (uint)client.Equipment.GetTPL()
            }, 3252));
        }
    }
}