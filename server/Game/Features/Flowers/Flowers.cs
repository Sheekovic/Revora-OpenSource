using System;
using System.Linq;
using System.Text;
using Revora.Client;
using System.Collections.Generic;
using Revora.Network.GamePackets;

namespace Revora.Game.Features
{
    public unsafe class Flowers
    {
        public enum Effect : byte
        {
            None = 0,

            Rouse = 1,
            Lilies = 2,
            Orchids = 3,
            Tulips = 4,

            Kiss = 1,
            love = 2,
            Tins = 3,
            Jade = 4,
        }
        public enum FlowersT : byte
        {
            Rouse = 0,
            Lilies = 1,
            Orchids = 2,
            Tulips = 3,

            Kiss = 4,
            love = 5,
            Tins = 6,
            Jade = 7,
        }

        public Flowers()
        {
        }
        public Flowers(uint _UID, string name)
        {
            UID = _UID;
            Name = name;
        }
        public uint UID;
        public uint aFlower = 1;
        public uint SendDay = 0;
        internal string PersistentDay;
        public string Name = "";

        public int SendScreenValue(FlowersT typ, int rak)
        {
            if (rak == 0 || rak > 100)
                return 0;
            return (int)(30000002 + (uint)(100 * (byte)typ) + GetRank(rak));
        }
        public ushort GetRank(int rank)
        {
            if (rank == 1)
                return 0;
            if (rank == 2)
                return 10000;
            if (rank == 3)
                return 20000;
            if (rank > 3)
                return 30000;

            return 0;
        }
        public int BoySendScreenValue(FlowersT typ, int rak)
        {
            int ret = 0;
            if (rak == -1) return 0;
            if (rak > 100) return 0;

            ret = (int)(30000402 + (uint)(100 * (byte)typ));

            return ret;
        }

        public int RankRoses = 0;
        public int RankLilies = 0;//max 10 start with -1.
        public int RankOrchids = 0;//max 10 start with -1.
        public int RankTuilps = 0;//max 10 start with -1.

        public uint RedRoses;//kiss
        public uint RedRoses2day;
        public uint Lilies;//love
        public uint Lilies2day;
        public uint Orchads;//wine
        public uint Orchads2day;
        public uint Tulips;//jade
        public uint Tulips2day;

        public override string ToString()
        {
            return UID + "#"
                + Name + "#"
                + RedRoses + "#"
                + RedRoses2day + "#"
                + Lilies + "#"
                + Lilies2day + "#"
                + Orchads + "#"
                + Orchads2day + "#"
                + Tulips + "#"
                + Tulips2day + "#"
                + SendDay + "#"
                + aFlower + "#"
                + 0 + "#"
                + 0 + "#"
                + 0 + "#"
                + 0 + "#"
                + 0 + "#";
        }
        public void Read(string aLine)
        {
            if (aLine == "" || aLine == null) return;
            string[] line = aLine.Split('#');
            UID = uint.Parse(line[0]);
            Name = line[1];
            RedRoses = uint.Parse(line[2]);
            RedRoses2day = uint.Parse(line[3]);
            Lilies = uint.Parse(line[4]);
            Lilies2day = uint.Parse(line[5]);
            Orchads = uint.Parse(line[6]);
            Orchads2day = uint.Parse(line[7]);
            Tulips = uint.Parse(line[8]);
            Tulips2day = uint.Parse(line[9]);
            SendDay = uint.Parse(line[10]);
            aFlower = uint.Parse(line[11]);
            Reset();
        }
        public void Reset()
        {
            if(PersistentDay!=null)return;
            if (SendDay != DateTime.Now.Day)
            {
                RedRoses2day = Lilies2day = Orchads2day = Tulips2day = 0;
                aFlower = 1;
                SendDay = (uint)DateTime.Now.Day;
            }
        }

        public static System.Collections.Concurrent.ConcurrentDictionary<uint, Flowers> Flowers_Poll = new System.Collections.Concurrent.ConcurrentDictionary<uint, Flowers>();
        public static System.Collections.Concurrent.ConcurrentDictionary<uint, Flowers> BoyFlowers = new System.Collections.Concurrent.ConcurrentDictionary<uint, Flowers>();

        public static Flowers[] KissTop100 = new Flowers[0];
        public static Flowers[] LoveTop100 = new Flowers[0];
        public static Flowers[] TineTop100 = new Flowers[0];
        public static Flowers[] JadeTop100 = new Flowers[0];

        public static Flowers[] RedRousesTop100 = new Flowers[0];
        public static Flowers[] LiliesTop100 = new Flowers[0];
        public static Flowers[] OrchidsTop100 = new Flowers[0];
        public static Flowers[] TulipsTop100 = new Flowers[0];

        public static object RouseLock = new object();
        public static object LilisLock = new object();
        public static object OrchidsLock = new object();
        public static object TulipsLock = new object();

        public static object KissLock = new object();
        public static object LoveLock = new object();
        public static object TineLock = new object();
        public static object JadeLock = new object();

        public static List<Flowers> RankKiss = new List<Flowers>();
        public static List<Flowers> RankLove = new List<Flowers>();
        public static List<Flowers> RankTine = new List<Flowers>();
        public static List<Flowers> RankJade = new List<Flowers>();

        public static List<Flowers> Rankrose = new List<Flowers>();
        public static List<Flowers> Ranklili = new List<Flowers>();
        public static List<Flowers> Rankorchid = new List<Flowers>();
        public static List<Flowers> RankTulips = new List<Flowers>();

        public static void CulculateRankJade(Flowers f) { RefreshReceived(f,false); }
        public static void CulculateRankTine(Flowers f) { RefreshReceived(f,false); }
        public static void CulculateRankLove(Flowers f) { RefreshReceived(f,false); }
        public static void CulculateRankKiss(Flowers f) { RefreshReceived(f,false); }
        public static void CulculateRankRouse(Flowers f) { RefreshReceived(f,true); }
        public static void CulculateRankLilies(Flowers f) { RefreshReceived(f,true); }
        public static void CulculateRankOrchids(Flowers f) { RefreshReceived(f,true); }
        public static void CulculateRankTulips(Flowers f) { RefreshReceived(f,true); }
        public static void FlowersPacket(byte[] packet, GameState client)
        {
            if (packet == null || packet.Length < 24 || packet.Length > 1024
                || BitConverter.ToUInt16(packet, 0) != packet.Length - 8) return;
            uint category = BitConverter.ToUInt32(packet, 8);
            if (PrestigeRanking.IsPrestige(category))
            {
                if (!PrestigeRanking.AllowQuery(client)) return;
                uint mode = BitConverter.ToUInt32(packet, 4);
                if (mode == GenericRanking.Ranking)
                    PrestigeRanking.SendPage(client, category, BitConverter.ToUInt16(packet, 14));
                else if (mode == GenericRanking.QueryCount)
                    PrestigeRanking.SendSelf(client, category);
                return;
            }
            if(!QuestInventory.Player(client,false) || client.Entity.MyFlowers==null)return;
            try{FlowerDelivery.EnsureDay(client);}catch(Exception ex){QuestInventory.Fail(client,"flower-day",ex);return;}
            GenericRanking ranking = new GenericRanking(false, 0);
            ranking.Deserialize(packet);
            switch (ranking.Mode)
            {
                case GenericRanking.Ranking:
                    {
                        if (ranking.RankingType == GenericRanking.InnerPower)
                        {
                            ushort page = ranking.Page;
                            var array = Game.InnerPower.InnerPowerRank.GetRankingList();
                            if (array.Length < page * 10) return;
                            uint count = (uint)Math.Min(array.Length - page * 10, 10);
                            var nRanking = new GenericRanking(true, count);
                            nRanking.Mode = GenericRanking.Ranking;
                            nRanking.RankingType = ranking.RankingType;
                            nRanking.Page = page;
                            nRanking.RegisteredCount = (ushort)array.Length;
                            int rank = (int)page * 10;
                            for (int i = rank; i < rank + count; i++)
                            {
                                var current = array[i];
                                nRanking.Append((uint)(i + 1), (uint)current.TotalScore, current.UID, current.Name);
                            }
                            client.Send(nRanking);


                            for (int i = 0; i < array.Length; i++)
                            {
                                var element = array[i];
                                if (element.UID == client.Entity.UID)
                                {
                                    Network.GamePackets.GenericRanking PacketRank = new Network.GamePackets.GenericRanking(true, 1);
                                    PacketRank.Mode = Network.GamePackets.GenericRanking.QueryCount;
                                    PacketRank.RankingType = GenericRanking.InnerPower;
                                    PacketRank.Append((uint)(i + 1), element.TotalScore, element.UID, element.Name);
                                    client.Send(PacketRank.ToArray());

                                    break;
                                }

                            }
                            break;
                        }
                        if (ranking.RankingType == GenericRanking.Prestige)
                        {
                            PerfectionManager.ShowRank(ranking, client);
                            //GeneralData.ShowGenericRanking(ranking, client);
                        //    GeneralData.ShowGenericRanking2(ranking, client);
                        }
                        if (ranking.RankingType >= GenericRanking.DragonChi && ranking.RankingType <= GenericRanking.TurtleChi)
                            GeneralData.ShowGenericRanking(ranking, client);
                        else if (ranking.RankingType >= GenericRanking.RoseFairy && ranking.RankingType <= GenericRanking.TulipFairy)
                            GeneralData.ShowGenericRanking2(ranking, client);
                        else if (ranking.RankingType >= GenericRanking.Kiss && ranking.RankingType <= GenericRanking.Jade)
                            GeneralData.ShowGenericRanking2(ranking, client);
                        break;
                    }
                case GenericRanking.QueryCount:
                    {
                        if (Flowers.IsGirl((uint)client.Entity.Body))
                        {
                            if (client.Entity.MyFlowers != null)
                            {
                                int num61 = -1;
                                uint num62 = Flowers.CreateMyRank(client, out num61);
                                packet[4] = 5;
                                client.Send(packet);
                                client.Entity.FlowerRank = (uint)client.Entity.MyFlowers.SendScreenValue((Game.Features.Flowers.FlowersT)num62, num61);
                                GenericRanking genericRanking2 = new GenericRanking(true, 1u);
                                genericRanking2.Mode = 2u;
                                genericRanking2.RankingType = client.Entity.FlowerRank;
                                genericRanking2.Count = 1u;
                                int rank = num61;
                                if (client.Entity.MyFlowers.RankRoses <= 100 && client.Entity.MyFlowers.RankRoses > 0)
                                {
                                    genericRanking2.RankingType = GenericRanking.RoseFairy;
                                    genericRanking2.Append((uint)client.Entity.MyFlowers.RankRoses, client.Entity.MyFlowers.RedRoses, client.Entity.UID, client.Entity.Name);
                                    client.Send(genericRanking2.ToArray());
                                }
                                if (client.Entity.MyFlowers.RankLilies <= 100 && client.Entity.MyFlowers.RankLilies > 0)
                                {
                                    genericRanking2.Reset();
                                    genericRanking2.RankingType = GenericRanking.LilyFairy;
                                    genericRanking2.Append((uint)client.Entity.MyFlowers.RankLilies, client.Entity.MyFlowers.Lilies, client.Entity.UID, client.Entity.Name);
                                    client.Send(genericRanking2.ToArray());
                                }
                                if (client.Entity.MyFlowers.RankOrchids <= 100 && client.Entity.MyFlowers.RankOrchids > 0)
                                {
                                    genericRanking2.Reset();
                                    genericRanking2.RankingType = GenericRanking.OrchidFairy;
                                    genericRanking2.Append((uint)client.Entity.MyFlowers.RankOrchids, client.Entity.MyFlowers.Orchads, client.Entity.UID, client.Entity.Name);
                                    client.Send(genericRanking2.ToArray());
                                }
                                if (client.Entity.MyFlowers.RankTuilps <= 100 && client.Entity.MyFlowers.RankTuilps > 0)
                                {
                                    genericRanking2.Reset();
                                    genericRanking2.RankingType = GenericRanking.TulipFairy;
                                    genericRanking2.Append((uint)client.Entity.MyFlowers.RankTuilps, client.Entity.MyFlowers.Tulips, client.Entity.UID, client.Entity.Name);
                                    client.Send(genericRanking2.ToArray());
                                }
                                packet[4] = 5;
                                client.Send(packet);
                                break;
                            }
                            break;
                        }
                        else
                        {
                            if (client.Entity.MyFlowers != null)
                            {
                                GenericRanking genericRanking3 = new GenericRanking(true, 1u);
                                genericRanking3.Mode = 2u;
                                genericRanking3.RankingType = 30000402u;
                                genericRanking3.Count = 1u;
                                if (client.Entity.MyFlowers.RankRoses <= 100 && client.Entity.MyFlowers.RankRoses > 0)
                                {
                                    genericRanking3.Append((uint)client.Entity.MyFlowers.RankRoses, client.Entity.MyFlowers.RedRoses, client.Entity.UID, client.Entity.Name);
                                    client.Send(genericRanking3.ToArray());
                                }
                                if (client.Entity.MyFlowers.RankLilies <= 100 && client.Entity.MyFlowers.RankLilies > 0)
                                {
                                    genericRanking3.Reset();
                                    genericRanking3.Append((uint)client.Entity.MyFlowers.RankLilies, client.Entity.MyFlowers.Lilies, client.Entity.UID, client.Entity.Name);
                                    genericRanking3.RankingType = 30000502u;
                                    client.Send(genericRanking3.ToArray());
                                }
                                if (client.Entity.MyFlowers.RankOrchids <= 100 && client.Entity.MyFlowers.RankOrchids > 0)
                                {
                                    genericRanking3.Reset();
                                    genericRanking3.Append((uint)client.Entity.MyFlowers.RankOrchids, client.Entity.MyFlowers.Orchads, client.Entity.UID, client.Entity.Name);
                                    genericRanking3.RankingType = 30000602u;
                                    client.Send(genericRanking3.ToArray());
                                }
                                if (client.Entity.MyFlowers.RankTuilps <= 100 && client.Entity.MyFlowers.RankTuilps > 0)
                                {
                                    genericRanking3.Reset();
                                    genericRanking3.Append((uint)client.Entity.MyFlowers.RankTuilps, client.Entity.MyFlowers.Tulips, client.Entity.UID, client.Entity.Name);
                                    genericRanking3.RankingType = 30000702u;
                                    client.Send(genericRanking3.ToArray());
                                }
                                packet[4] = 5;
                                client.Send(packet);
                                break;
                            }
                        }
                        break;
                    }
            }
        }
        public static void SendFlower(Client.GameState client, byte[] packet)
        {
            if(packet==null || packet.Length<24 || packet.Length>128 || BitConverter.ToUInt16(packet,0)+8!=packet.Length
                || BitConverter.ToUInt16(packet,2)!=1150 || BitConverter.ToUInt64(packet,packet.Length-8)!=Revora.Network.PacketHandler.ClientSeal
                || !QuestInventory.Player(client) || client.Entity.MyFlowers==null)return;
            uint action=BitConverter.ToUInt32(packet,4);if(action>1)return;

            uint Target = BitConverter.ToUInt32(packet, 8);
            uint ITEM_UID = BitConverter.ToUInt32(packet, 12);
            if(ITEM_UID!=0) {
                ConquerItem gift;
                if(!client.Inventory.TryGetItem(ITEM_UID,out gift) || gift.ID/1000<751 || gift.ID/1000>758
                    || gift.ID%1000==0 || !Revora.Database.ConquerItemInformation.BaseInformations.ContainsKey(gift.ID))return;
                // Flowers (751..754) and male gifts (755..758) are distinct
                // catalog families, not interchangeable counter aliases.
                if(action==0 ? gift.ID/1000>754 : gift.ID/1000<755)return;
            }
            FlowerDelivery.Send(client,Target,ITEM_UID,action);
        }
        public static uint CreateMyRank(Client.GameState client, out int rank)
        {
            List<ClientRank> FRanks = new List<ClientRank>()
            {
           new ClientRank(){Rank = (uint)client.Entity.MyFlowers.RankLilies, Amount = client.Entity.MyFlowers.Lilies}
           ,   new ClientRank(){Rank = (uint)client.Entity.MyFlowers.RankOrchids, Amount = client.Entity.MyFlowers.Orchads}
           ,   new ClientRank(){Rank = (uint)client.Entity.MyFlowers.RankRoses, Amount = client.Entity.MyFlowers.RedRoses}
           ,   new ClientRank(){Rank = (uint)client.Entity.MyFlowers.RankTuilps, Amount = client.Entity.MyFlowers.Tulips}
            };
            var array = FRanks.Where((f1) => f1.Rank != 0).ToArray();
            Array.Sort(array, (f1, f2) =>
            {
                int n_rank = f1.Rank.CompareTo(f2.Rank);

                if (f2.Rank == f1.Rank)
                    return f2.Amount.CompareTo(f1.Amount);
                return n_rank;
            });
            if (array != null && array.Length > 0)
            {
                ClientRank BestRank = array[0];
                if (BestRank.Rank != 0)
                {
                    rank = (int)BestRank.Rank;
                    if (client.Entity.MyFlowers.RankLilies == BestRank.Rank && client.Entity.MyFlowers.Lilies == BestRank.Amount)
                        return (byte)Game.Features.Flowers.FlowersT.Lilies;
                    if (client.Entity.MyFlowers.RankOrchids == BestRank.Rank && client.Entity.MyFlowers.Orchads == BestRank.Amount)
                        return (byte)Game.Features.Flowers.FlowersT.Orchids;
                    if (client.Entity.MyFlowers.RankRoses == BestRank.Rank && client.Entity.MyFlowers.RedRoses == BestRank.Amount)
                        return (byte)Game.Features.Flowers.FlowersT.Rouse;
                    if (client.Entity.MyFlowers.RankTuilps == BestRank.Rank && client.Entity.MyFlowers.Tulips == BestRank.Amount)
                        return (byte)Game.Features.Flowers.FlowersT.Tulips;
                }
            }
            rank = 0;
            return 0;
        }
        public static uint GetFlowerTyp(uint ID)
        {
            if (ID >= 751001 && ID <= 751999 || ID >= 755001 && ID <= 755999)
                return (uint)Game.Features.Flowers.FlowersT.Rouse;
            if (ID >= 752001 && ID <= 752999 || ID >= 756001 && ID <= 756999)
                return (uint)Game.Features.Flowers.FlowersT.Lilies;
            if (ID >= 753001 && ID <= 753999 || ID >= 757001 && ID <= 757999)
                return (uint)Game.Features.Flowers.FlowersT.Orchids;
            if (ID >= 754001 && ID <= 754999 || ID >= 758001 && ID <= 758999)
                return (uint)Game.Features.Flowers.FlowersT.Tulips;
            return 0;
        }
        public static bool IsBoy(uint mesh)
        {
            return mesh == 1003 || mesh == 1004 || mesh == 1005 || mesh == 1006;
        }
        public static bool IsGirl(uint mesh)
        {
            return mesh == 2001 || mesh == 2002 || mesh == 2005 || mesh == 2006;
        }
        private static readonly object MembershipLock=new object();
        internal static void RefreshReceived(Flowers flowers,bool girl) {
            lock(MembershipLock) {
                var destination=girl?Flowers_Poll:BoyFlowers;var other=girl?BoyFlowers:Flowers_Poll;Flowers existing;
                if(destination.TryGetValue(flowers.UID,out existing) && !ReferenceEquals(existing,flowers)
                    || other.TryGetValue(flowers.UID,out existing) && !ReferenceEquals(existing,flowers))
                    throw new InvalidOperationException("Conflicting flower ranking identity.");
                destination[flowers.UID]=flowers;other.TryRemove(flowers.UID,out existing);
                RebuildMembershipRanks();
            }
        }
        internal static void RestoreRanks(IEnumerable<Tuple<Flowers,bool>> records) {
            lock(MembershipLock) {
                if(Flowers_Poll.Count!=0 || BoyFlowers.Count!=0)
                    throw new InvalidOperationException("Flower ranking restoration requires empty membership caches.");
                foreach(var record in records) {
                    var destination=record.Item2?Flowers_Poll:BoyFlowers;
                    if(!destination.TryAdd(record.Item1.UID,record.Item1))throw new InvalidOperationException("Duplicate flower ranking owner.");
                }
                RebuildMembershipRanks();
            }
        }
        internal static void Attach(GameState player) {
            if(player==null || player.Entity==null)return;
            lock(player.ItemSyncRoot ?? (object)player)lock(MembershipLock) {
                uint uid=player.Entity.UID;Flowers female,male;
                Flowers_Poll.TryGetValue(uid,out female);BoyFlowers.TryGetValue(uid,out male);
                if(female!=null && male!=null && !ReferenceEquals(female,male))
                    throw new InvalidOperationException("Conflicting flower histories require reconciliation.");
                var flowers=female ?? male ?? new Flowers(uid,player.Entity.Name);
                bool girl=IsGirl(player.Entity.Body),moved=girl?male!=null:female!=null;
                if(girl){Flowers_Poll[uid]=flowers;BoyFlowers.TryRemove(uid,out male);}
                else {BoyFlowers[uid]=flowers;Flowers_Poll.TryRemove(uid,out female);}
                flowers.Name=player.Entity.Name;player.Entity.MyFlowers=flowers;
                FlowerDelivery.Load(player);
                if(moved)RebuildMembershipRanks();
            }
        }
        private static Flowers[] MembershipRank(IEnumerable<Flowers> values,Func<Flowers,uint> count,Action<Flowers,int> assign) {
            // Stable UID ordering makes live and restored Top 100 ties agree.
            var result=values.Where(f=>count(f)>0).OrderByDescending(count).ThenBy(f=>f.UID).Take(100).ToArray();
            for(int i=0;i<result.Length;i++)assign(result[i],i+1);return result;
        }
        private static void RebuildMembershipRanks() {
            lock(RouseLock)lock(LilisLock)lock(OrchidsLock)lock(TulipsLock)
            lock(KissLock)lock(LoveLock)lock(TineLock)lock(JadeLock) {
                var female=Flowers_Poll.Values.ToArray();var male=BoyFlowers.Values.ToArray();
                foreach(var f in female.Concat(male))f.RankRoses=f.RankLilies=f.RankOrchids=f.RankTuilps=0;
                RedRousesTop100=MembershipRank(female,f=>f.RedRoses,(f,r)=>f.RankRoses=r);Rankrose=RedRousesTop100.ToList();
                LiliesTop100=MembershipRank(female,f=>f.Lilies,(f,r)=>f.RankLilies=r);Ranklili=LiliesTop100.ToList();
                OrchidsTop100=MembershipRank(female,f=>f.Orchads,(f,r)=>f.RankOrchids=r);Rankorchid=OrchidsTop100.ToList();
                TulipsTop100=MembershipRank(female,f=>f.Tulips,(f,r)=>f.RankTuilps=r);RankTulips=TulipsTop100.ToList();
                KissTop100=MembershipRank(male,f=>f.RedRoses,(f,r)=>f.RankRoses=r);RankKiss=KissTop100.ToList();
                LoveTop100=MembershipRank(male,f=>f.Lilies,(f,r)=>f.RankLilies=r);RankLove=LoveTop100.ToList();
                TineTop100=MembershipRank(male,f=>f.Orchads,(f,r)=>f.RankOrchids=r);RankTine=TineTop100.ToList();
                JadeTop100=MembershipRank(male,f=>f.Tulips,(f,r)=>f.RankTuilps=r);RankJade=JadeTop100.ToList();
            }
        }
        public class ClientRank
        {
            public uint Rank;
            public uint Amount;
        }
    }
}
