using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    // HonorShop UID 6000 stocks leftover FancyHossuHairPack 3006319.
    // Ordinary HonorPurchases.Buy is the producer. Random.Next(1,6) yields
    // 1-5 / 3006165-3006169; leftover case 6 / 3006170 is unreachable.
    // Luxury/Legendary 3006320/3006321 and older 3006316-3006318 stay
    // unowned (no UseItem case). HonorPurchases refuses new Honor spend.
    internal static class HossuHairPacks
    {
        internal const uint Pack=3006319;
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        internal static Func<int,int,int> Roll=(min,max)=>new Random().Next(min,max);
        internal static Action<string> Spoken;
        private static bool enabled;
        internal static bool Enabled{get{return enabled;}}
        private static readonly Dictionary<uint,byte> Catalog=new Dictionary<uint,byte>{{3006319,0}};
        internal static readonly uint[] Rewards=new uint[]{3006165,3006166,3006167,3006168,3006169};
        private static readonly string[] Names=new string[]{"NormalHossuHair","RefinedHossuHair","UniqueHossuHair","EliteHossuHair","SuperHossuHair"};
        private static bool Handles(uint id){return Catalog.ContainsKey(id);}
        internal static uint Result(int pick){return pick>=1 && pick<=Rewards.Length?Rewards[pick-1]:0;}
        internal static void Initialize(){
            using(var c=DataHolder.MySqlConnection){c.Open();
                Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_hossu_hair_pack_uses(
id uuid PRIMARY KEY,uid bigint NOT NULL,pack_uid bigint NOT NULL,pack_type integer NOT NULL,
result_uid bigint NOT NULL,result_type integer NOT NULL,pick smallint NOT NULL,used_at timestamptz NOT NULL);");
            }
            if(!ConquerItemInformation.BaseInformations.ContainsKey(Pack))
                throw new InvalidOperationException("Hossu hair pack catalog incomplete.");
            foreach(uint hair in Rewards)
                if(!ConquerItemInformation.BaseInformations.ContainsKey(hair))
                    throw new InvalidOperationException("Hossu hair catalog incomplete for "+hair+".");
            enabled=true;
        }
        private static bool Ready(GameState p){return enabled && Q.Player(p) && p.Booth==null && p.Trade!=null && !p.Trade.InTrade;}
        private static List<uint> Bag(NpgsqlConnection c,NpgsqlTransaction t,GameState p){
            var ids=new List<uint>();
            using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid","uid",(long)p.Entity.UID))
            using(var r=q.ExecuteReader())while(r.Read()){if(ids.Count>=40)throw new InvalidOperationException("Hossu hair inventory exceeds capacity.");ids.Add(checked((uint)Convert.ToInt64(r[0])));}
            if(!ids.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("Hossu hair inventory changed.");
            return ids;
        }
        internal static bool TryHandle(GameState p,byte[] packet){
            ConquerItem item;
            if(p==null || p.Inventory==null || packet==null || packet.Length<12 || !p.Inventory.TryGetItem(BitConverter.ToUInt32(packet,8),out item) || !Handles(item.ID))return false;
            if(packet.Length<32 || packet.Length>128 || BitConverter.ToUInt16(packet,0)+8!=packet.Length || BitConverter.ToUInt16(packet,2)!=1009
                || BitConverter.ToUInt16(packet,20)!=4 || BitConverter.ToUInt64(packet,packet.Length-8)!=Revora.Network.PacketHandler.ClientSeal)return true;
            TryUse(item,p);return true;
        }
        internal static bool TryUse(ConquerItem item,GameState p){
            if(item==null || !Handles(item.ID))return false;if(!Ready(p))return true;
            lock(p.ItemSyncRoot)try{
                ConquerItem owned;DateTime now=Clock();
                if(!Ready(p) || !p.Inventory.TryGetItem(item.UID,out owned) || !ReferenceEquals(owned,item) || item.Position!=0 || item.Warehouse!=0
                    || item.Lock!=0 || item.Suspicious || item.InWardrobe || (item.Days==0?item.TimeLeftInMinutes!=0:now.ToLocalTime()>=item.DayStamp.AddDays(item.Days)))return true;
                if(p.Inventory.Count>38)return true;
                int pick=Roll(1,6);
                uint resultId=Result(pick);
                if(resultId==0 || !ConquerItemInformation.BaseInformations.ContainsKey(resultId))return true;
                bool deplete=item.StackSize<=1;
                var reward=Q.New(resultId,item.Bound);
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                        if(q.ExecuteScalar()==null)throw new InvalidOperationException("Hossu hair pack owner missing.");
                    Bag(c,t,p);
                    string sql=deplete?"DELETE FROM items":"UPDATE items SET stacksize=stacksize-1";
                    if(Q.Execute(c,t,sql+@" WHERE uid=@item AND entityid=@uid AND id=@type AND warehouse=0 AND position=0 AND stacksize=@stack AND bound=@bound
AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')=@days AND (@permanent OR daystamp=@stamp)",
                        "item",(long)item.UID,"uid",(long)p.Entity.UID,"type",(int)item.ID,"stack",(int)item.StackSize,"bound",item.Bound?1:0,
                        "days",item.Days.ToString(CultureInfo.InvariantCulture),"permanent",item.Days==0,"stamp",item.DayStamp.ToBinary())!=1)
                        throw new InvalidOperationException("Hossu hair pack ownership changed.");
                    Q.Insert(c,t,p.Entity.UID,reward);
                    if(Q.Execute(c,t,@"INSERT INTO revora_hossu_hair_pack_uses(id,uid,pack_uid,pack_type,result_uid,result_type,pick,used_at)
VALUES(@id,@uid,@pack,@type,@result,@rid,@pick,@now)","id",Guid.NewGuid(),"uid",(long)p.Entity.UID,"pack",(long)item.UID,"type",(int)item.ID,
                        "result",(long)reward.UID,"rid",(int)reward.ID,"pick",pick,"now",now)!=1)
                        throw new InvalidOperationException("Hossu hair pack receipt was not saved.");
                    if(!Ready(p))throw new InvalidOperationException("Hossu hair pack session changed.");
                    t.Commit();
                }}
                Q.Install(p,deplete?new[]{item.UID}:new uint[0],new[]{reward});
                if(!deplete){item.StackSize--;item.Mode=Enums.ItemMode.Update;item.Send(p);item.Mode=Enums.ItemMode.Default;}
                string text="Congratulations! "+p.Entity.Name+" has obtained "+Names[pick-1];
                p.Send(new Message(text,System.Drawing.Color.Yellow,Message.Center));
                var spoken=Spoken;if(spoken!=null)spoken(text);
            }catch(Exception ex){Q.Fail(p,"hossu-hair-pack",ex);}
            return true;
        }
    }
}
