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
    // HonorShop UID 6000 stocks leftover GetNextRefineryItem packs that are
    // outside the 48 owned Power Arena families. Ordinary HonorPurchases.Buy
    // is the producer. Unstocked primary tiers stay leftover live-split-write.
    internal static class HonorRefineryPacks
    {
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        internal static Func<byte> Extra=()=>ItemHandler.GetNextRefineryItem();
        private static bool enabled;
        internal static bool Enabled{get{return enabled;}}
        private sealed class Spec{internal uint Offset;internal byte Remap;}
        private static readonly Dictionary<uint,Spec> Catalog=new Dictionary<uint,Spec>{
            {723679,new Spec{Offset=815}},{723680,new Spec{Offset=815}},
            {723682,new Spec{Offset=667,Remap=1}},{723683,new Spec{Offset=667,Remap=1}},
            {723691,new Spec{Offset=683}},{723692,new Spec{Offset=683}},
            {724149,new Spec{Offset=237}},{724150,new Spec{Offset=237}},
            {724193,new Spec{Offset=223}},{724194,new Spec{Offset=223}},
            {724196,new Spec{Offset=230}},{724197,new Spec{Offset=230}},
            {724199,new Spec{Offset=266,Remap=2}},{724200,new Spec{Offset=266,Remap=2}},
            {724202,new Spec{Offset=162}},{724203,new Spec{Offset=162}},
            {724205,new Spec{Offset=274}},{724206,new Spec{Offset=274}},
            {724208,new Spec{Offset=161}},{724209,new Spec{Offset=161}},
            {724211,new Spec{Offset=225}},{724212,new Spec{Offset=225}},
            {725161,new Spec{Offset=32}},{725162,new Spec{Offset=32}},
            {725164,new Spec{Offset=34}},{725165,new Spec{Offset=34}},
            {725167,new Spec{Offset=36}},{725168,new Spec{Offset=36}},
            {725170,new Spec{Offset=38}},{725171,new Spec{Offset=38}}
        };
        private static bool Handles(uint id){return Catalog.ContainsKey(id);}
        internal static uint Result(uint pack,byte extra){
            Spec spec=Catalog[pack];uint id=pack+extra+spec.Offset;
            if(spec.Remap==1 && (id==724348 || id==724349))id+=150;
            if(spec.Remap==2 && id>=724466)id+=4;
            return id;
        }
        internal static void Initialize(){
            using(var c=DataHolder.MySqlConnection){c.Open();
                Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_honor_refinery_pack_uses(
id uuid PRIMARY KEY,uid bigint NOT NULL,pack_uid bigint NOT NULL,pack_type integer NOT NULL,
result_uid bigint NOT NULL,result_type integer NOT NULL,extra smallint NOT NULL,used_at timestamptz NOT NULL);");
            }
            foreach(uint pack in Catalog.Keys)for(byte extra=0;extra<3;extra++){
                uint result=Result(pack,extra);
                if(!ConquerItemInformation.BaseInformations.ContainsKey(pack) || !ConquerItemInformation.BaseInformations.ContainsKey(result))
                    throw new InvalidOperationException("Honor refinery catalog incomplete for "+pack+" extra "+extra+".");
            }
            enabled=true;
        }
        private static bool Ready(GameState p){return enabled && Q.Player(p) && p.Booth==null && p.Trade!=null && !p.Trade.InTrade;}
        private static List<uint> Bag(NpgsqlConnection c,NpgsqlTransaction t,GameState p){
            var ids=new List<uint>();
            using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid","uid",(long)p.Entity.UID))
            using(var r=q.ExecuteReader())while(r.Read()){if(ids.Count>=40)throw new InvalidOperationException("Honor refinery inventory exceeds capacity.");ids.Add(checked((uint)Convert.ToInt64(r[0])));}
            if(!ids.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("Honor refinery inventory changed.");
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
                bool deplete=item.StackSize<=1;
                if(p.Inventory.Count+(deplete?0:1)>40){p.Send(Constants.FullInventory);return true;}
                byte extra=Extra();if(extra>2)return true;
                var reward=Q.New(Result(item.ID,extra),item.Bound);
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                        if(q.ExecuteScalar()==null)throw new InvalidOperationException("Honor refinery pack owner missing.");
                    Bag(c,t,p);
                    string sql=deplete?"DELETE FROM items":"UPDATE items SET stacksize=stacksize-1";
                    if(Q.Execute(c,t,sql+@" WHERE uid=@item AND entityid=@uid AND id=@type AND warehouse=0 AND position=0 AND stacksize=@stack AND bound=@bound
AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')=@days AND (@permanent OR daystamp=@stamp)",
                        "item",(long)item.UID,"uid",(long)p.Entity.UID,"type",(int)item.ID,"stack",(int)item.StackSize,"bound",item.Bound?1:0,
                        "days",item.Days.ToString(CultureInfo.InvariantCulture),"permanent",item.Days==0,"stamp",item.DayStamp.ToBinary())!=1)
                        throw new InvalidOperationException("Honor refinery pack ownership changed.");
                    Q.Insert(c,t,p.Entity.UID,reward);
                    if(Q.Execute(c,t,@"INSERT INTO revora_honor_refinery_pack_uses(id,uid,pack_uid,pack_type,result_uid,result_type,extra,used_at)
VALUES(@id,@uid,@pack,@type,@result,@rid,@extra,@now)","id",Guid.NewGuid(),"uid",(long)p.Entity.UID,"pack",(long)item.UID,"type",(int)item.ID,
                        "result",(long)reward.UID,"rid",(int)reward.ID,"extra",(int)extra,"now",now)!=1)
                        throw new InvalidOperationException("Honor refinery pack receipt was not saved.");
                    if(!Ready(p))throw new InvalidOperationException("Honor refinery pack session changed.");
                    t.Commit();
                }}
                Q.Install(p,deplete?new[]{item.UID}:new uint[0],new[]{reward});
                if(!deplete){item.StackSize--;item.Mode=Enums.ItemMode.Update;item.Send(p);item.Mode=Enums.ItemMode.Default;}
            }catch(Exception ex){Q.Fail(p,"honor-refinery-pack",ex);}
            return true;
        }
    }
}
