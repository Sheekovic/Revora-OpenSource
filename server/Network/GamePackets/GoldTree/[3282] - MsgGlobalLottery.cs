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
    public class MsgGlobalLottery
    {
        byte[] Buffer;
        public MsgGlobalLottery(uint count = 0)
        {
            Buffer = new byte[259 + count];
            Buffer[0] = 251;
        }

        public MsgGlobalLottery() { }
        public MsgGlobalLotteryProtoBlossom Info;
        [ProtoContract]
        public class MsgGlobalLotteryProtoBlossom
        {
            [ProtoMember(1, IsRequired = true)]
            public uint Type;
            [ProtoMember(2, IsRequired = true)]
            public uint PrizesType;
            [ProtoMember(3, IsRequired = true)]
            public uint GUI;
            [ProtoMember(4, IsRequired = true)]
            public uint Time;
            [ProtoMember(5, IsRequired = true)]
            public uint Remain;
            [ProtoMember(6, IsRequired = true)]
            public uint TotalCount;
        }
        [ProtoContract]
        public class MsgGlobalLotteryProtoPick
        {
            [ProtoMember(1, IsRequired = true)]
            public uint Type;
            [ProtoMember(2, IsRequired = true)]
            public uint UID;
            [ProtoMember(3, IsRequired = true)]
            public uint GUI;
            [ProtoMember(4, IsRequired = true)]
            public uint PickItemID;
            [ProtoMember(5, IsRequired = true)]
            public string Name;
            [ProtoMember(6, IsRequired = true)]
            public uint Remain;
            [ProtoMember(7, IsRequired = true)]
            public uint TotalCount;
        }
        public bool Read(byte[] packet)
        {
            using (var MemoryStream = new MemoryStream(packet))
            {
                Info = Serializer.DeserializeWithLengthPrefix<MsgGlobalLotteryProtoBlossom>(MemoryStream, PrefixStyle.Fixed32);
            }
            return true;
        }
        public static Dictionary<string, uint> Ranking;
        public const double GoldenTreeMinutes = 10;//Minutos
        public const uint Max = 2500;
        public void Handle(Client.GameState client)
        {
            if (Kernel.GoldenTree == null) return;
            if (Ranking == null) Ranking = new Dictionary<string, uint>();
            switch (Info.Type)
            {
                case 1:
                    {
                        if (client.Inventory.Remove(Kernel.GoldenTree.CostItemID, 1))
                        {
                            Kernel.GoldenTreeTimes++;
                            var proto = new MsgGlobalLotteryProtoPick();
                            proto.UID = client.Entity.UID;
                            proto.Name = client.Entity.Name;
                            proto.Remain = (uint)(Max - Kernel.GoldenTreeTimes);
                            proto.TotalCount = Max;
                            proto.Type = 1;
                            proto.PickItemID = Kernel.RandomTree(Revora.Database.GoldenTreeTable.Pools.Values.Where(i => i.Type == Kernel.GoldenTree.Type).ToArray());
                            #region Fixed Stones / Piedras +1 - + 8
                            if (Revora.Database.GoldenTreeTable.Pools[proto.PickItemID].ItemID == 730001)
                            {
                                client.Inventory.AddBound(Revora.Database.GoldenTreeTable.Pools[proto.PickItemID].ItemID, 1, 1);
                            }
                            else if (Revora.Database.GoldenTreeTable.Pools[proto.PickItemID].ItemID == 730002)
                            {
                                client.Inventory.AddBound(Revora.Database.GoldenTreeTable.Pools[proto.PickItemID].ItemID, 2, 1);
                            }
                            else if (Revora.Database.GoldenTreeTable.Pools[proto.PickItemID].ItemID == 730003)
                            {
                                client.Inventory.AddBound(Revora.Database.GoldenTreeTable.Pools[proto.PickItemID].ItemID, 3, 1);
                            }
                            else if (Revora.Database.GoldenTreeTable.Pools[proto.PickItemID].ItemID == 730004)
                            {
                                client.Inventory.AddBound(Revora.Database.GoldenTreeTable.Pools[proto.PickItemID].ItemID, 4, 1);
                            }
                            else if (Revora.Database.GoldenTreeTable.Pools[proto.PickItemID].ItemID == 730005)
                            {
                                client.Inventory.AddBound(Revora.Database.GoldenTreeTable.Pools[proto.PickItemID].ItemID, 5, 1);
                            }
                            else if (Revora.Database.GoldenTreeTable.Pools[proto.PickItemID].ItemID == 730006)
                            {
                                client.Inventory.AddBound(Revora.Database.GoldenTreeTable.Pools[proto.PickItemID].ItemID, 6, 1);
                            }
                            else if (Revora.Database.GoldenTreeTable.Pools[proto.PickItemID].ItemID == 730007)
                            {
                                client.Inventory.AddBound(Revora.Database.GoldenTreeTable.Pools[proto.PickItemID].ItemID, 7, 1);
                            }
                            else if (Revora.Database.GoldenTreeTable.Pools[proto.PickItemID].ItemID == 730008)
                            {
                                client.Inventory.AddBound(Revora.Database.GoldenTreeTable.Pools[proto.PickItemID].ItemID, 8, 1);
                            }
                            else
                            {
                                client.Inventory.AddBound(Revora.Database.GoldenTreeTable.Pools[proto.PickItemID].ItemID, 0, 1);
                            }
                            #endregion
                            if (Revora.Database.GoldenTreeTable.Pools[proto.PickItemID].PerfectLevel == 1)
                            {
                                if (!Ranking.ContainsKey(client.Entity.Name)) Ranking.Add(client.Entity.Name, Revora.Database.GoldenTreeTable.Pools[proto.PickItemID].ID);
                                else
                                {
                                    Ranking[client.Entity.Name] = Revora.Database.GoldenTreeTable.Pools[proto.PickItemID].ID;
                                }
                            }
                            foreach (var player in Kernel.GamePool.Values)
                                player.Send(Kernel.FinalizeProtoBuf(proto, 3282));
                        }
                        break;
                    }
                case 3:
                    {
                        //Premios de Rangos no lo supe hacer.
                        break;

                    }
                case 5:
                    {
                        Info = new MsgGlobalLotteryProtoBlossom();
                        Info.Type = 5;
                        Info.GUI = 2;
                        Info.PrizesType = Kernel.GoldenTree.ID;
                        client.Send(Kernel.FinalizeProtoBuf(Info, 3282));
                        break;
                    }
                default: Console.WriteLine("MsgGlobalLottery UNKNOWN ACTION: " + Info.Type.ToString(), ConsoleColor.DarkRed); break;
            }
        }
        public void Ready(Client.GameState client)
        {
            Info = new MsgGlobalLotteryProtoBlossom();
            Info.PrizesType = Kernel.GoldenTree.ID;
            Info.GUI = 1;
            Info.Time = 5;
            client.Send(Kernel.FinalizeProtoBuf(Info, 3282));
        }
        public void Login(Client.GameState client)
        {
            if ((DateTime.Now > Kernel.GoldenTree.StartTime.AddMinutes(GoldenTreeMinutes) || DateTime.Now < Kernel.GoldenTree.StartTime))
            {
                if (Ranking == null)
                {
                    Info = new MsgGlobalLotteryProtoBlossom();
                    Info.PrizesType = Kernel.GoldenTree.ID;
                    Info.GUI = 0;
                }
                else
                {
                    Info = new MsgGlobalLotteryProtoBlossom();
                    Info.PrizesType = Kernel.GoldenTree.ID;
                    Info.GUI = 3;
                }
                client.Send(Kernel.FinalizeProtoBuf(Info, 3282));
                return;
            }
            Blossom(client);
        }
        public void Blossom(Client.GameState client)
        {
            Info = new MsgGlobalLotteryProtoBlossom();
            Info.PrizesType = Kernel.GoldenTree.ID;
            Info.GUI = 2;
            TimeSpan span = Kernel.GoldenTree.StartTime.AddMinutes(GoldenTreeMinutes) - DateTime.Now;
            Info.Time = (uint)(span.TotalSeconds);
            Info.Remain = (uint)(Max - Kernel.GoldenTreeTimes);
            Info.TotalCount = Max;
            client.Send(Kernel.FinalizeProtoBuf(Info, 3282));
        }
        public void End(Client.GameState client)
        {
            if (Ranking == null)
            {
                Info = new MsgGlobalLotteryProtoBlossom();
                Info.PrizesType = Kernel.GoldenTree.ID;
                Info.GUI = 0;
            }
            else
            {
                Info = new MsgGlobalLotteryProtoBlossom();
                Info.PrizesType = Kernel.GoldenTree.ID;
                Info.GUI = 3;
            }
            client.Send(Kernel.FinalizeProtoBuf(Info, 3282));
            return;

        }
    }
}
