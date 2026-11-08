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
    public class MsgGlobalLotteryRankList
    {
        public MsgGlobalLotteryRankList() { }
        public MsgGlobalLotteryRankListProto Info;
        [ProtoContract]
        public class MsgGlobalLotteryRankListProto
        {
            [ProtoMember(1, IsRequired = true)]
            public uint Page;
            [ProtoMember(2, IsRequired = true)]
            public uint CountinPage;
            [ProtoMember(3, IsRequired = true)]
            public uint AllCount;
            [ProtoMember(4, IsRequired = true)]
            public Picker[] Pickers;
        }
        [ProtoContract]
        public class Picker
        {
            [ProtoMember(1, IsRequired = true)]
            public uint PickItem;
            [ProtoMember(2, IsRequired = true)]
            public string Name;
        }
        public bool Read(byte[] packet)
        {
            using (var memoryStream = new MemoryStream(packet))
            {
                Info = Serializer.DeserializeWithLengthPrefix<MsgGlobalLotteryRankListProto>(memoryStream, PrefixStyle.Fixed32);
            }
            return true;
        }
        public void Handle(Client.GameState client)
        {
            switch (Info.Page)
            {
                default:// Page
                    {
                        var proto = new MsgGlobalLotteryRankListProto();
                        proto.CountinPage = Math.Min((uint)MsgGlobalLottery.Ranking.Count, (uint)10);
                        proto.AllCount = Math.Min((uint)MsgGlobalLottery.Ranking.Count, (uint)2500);
                        proto.Pickers = new Picker[Math.Min((uint)MsgGlobalLottery.Ranking.Count, (uint)2500)];
                        var array = MsgGlobalLottery.Ranking.ToArray();
                        for (int i = 0; i < proto.Pickers.Length; i++)
                        {
                            proto.Pickers[i] = new Picker();
                            proto.Pickers[i].Name = array[i].Key;
                            proto.Pickers[i].PickItem = array[i].Value;
                        }
                        client.Send(Kernel.FinalizeProtoBuf(proto, 3283));
                        break;
                    }
            }
        }
    }
}
