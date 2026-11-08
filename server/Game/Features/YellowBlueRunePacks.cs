using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    // Consumer-only yellow/blue rune fragments and packs. SuperBoss Next(1,17)
    // case 17 is unreachable, and the blue seller adds 3306368 rather than 3306371.
    // Combine 20 units into one bound plus-0 pack. Yellow Next(1,15) is picks 1-14,
    // with 13 and 14 both 4031301. Blue Next(1,10) is picks 1-9. Rewards stay bound.
    internal static class YellowBlueRunePacks
    {
        internal const uint YellowFragment=3306370,YellowPack=3306510,BlueFragment=3306371,BluePack=3306511;
        internal const int Cost=20;
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        internal static Func<int,int,int> Roll=(min,max)=>new Random().Next(min,max);
        internal static Action<string> Spoken;
        internal static Action<GameState> BeforeInstall;
        private static bool enabled;
        internal static bool Enabled{get{return enabled;}}
        internal static readonly uint[] YellowRewards=new uint[]{4030101,4030201,4030301,4030401,4030501,4030601,4030701,4030801,4030901,4031001,4031101,4031201,4031301,4031301};
        internal static readonly uint[] BlueRewards=new uint[]{4020101,4020201,4020301,4020401,4020501,4020601,4020701,4020801,4020901};
        private sealed class Take{internal ConquerItem Item;internal uint Uid;internal ushort Before,Amount;internal bool Bound;internal byte Days;internal DateTime Stamp;}
        private static bool Fragment(uint id){return id==YellowFragment || id==BlueFragment;}
        private static bool Pack(uint id){return id==YellowPack || id==BluePack;}
        private static bool Handles(uint id){return id==YellowFragment || id==BlueFragment || id==YellowPack || id==BluePack;}
        private static uint PackOf(uint fragment){return fragment==BlueFragment?BluePack:YellowPack;}
        internal static uint Result(uint pack,int pick){
            uint[] rows=pack==BluePack?BlueRewards:YellowRewards;
            return pick>=1 && pick<=rows.Length?rows[pick-1]:0;
        }
        internal static void Initialize(){
            using(var c=DataHolder.MySqlConnection){c.Open();
                Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_yb_rune_combines(
id uuid PRIMARY KEY,uid bigint NOT NULL,fragment_type integer NOT NULL,consumed integer NOT NULL CHECK(consumed=20),
bound boolean NOT NULL,sources bigint[] NOT NULL,amounts integer[] NOT NULL,
pack_uid bigint NOT NULL,pack_type integer NOT NULL,used_at timestamptz NOT NULL);
CREATE TABLE IF NOT EXISTS revora_yb_rune_pack_uses(
id uuid PRIMARY KEY,uid bigint NOT NULL,pack_uid bigint NOT NULL,pack_type integer NOT NULL,
result_uid bigint NOT NULL,result_type integer NOT NULL,pick smallint NOT NULL,used_at timestamptz NOT NULL);");}
            foreach(uint id in new uint[]{YellowFragment,YellowPack,BlueFragment,BluePack})
                if(!ConquerItemInformation.BaseInformations.ContainsKey(id))
                    throw new InvalidOperationException("Yellow/blue rune catalog incomplete.");
            foreach(uint rune in YellowRewards.Concat(BlueRewards).Distinct())
                if(!ConquerItemInformation.BaseInformations.ContainsKey(rune))
                    throw new InvalidOperationException("Yellow/blue rune catalog incomplete for "+rune+".");
            enabled=true;
        }
        private static bool Ready(GameState p){return enabled && Q.Player(p) && p.Booth==null && p.Trade!=null && !p.Trade.InTrade;}
        private static void Hear(GameState p,string text){p.Send(text);var s=Spoken;if(s!=null)s(text);}
        private static int Units(ConquerItem i){return i.StackSize<=1?1:i.StackSize;}
        private static bool Whole(Take t){return t.Before<=1 || t.Amount>=t.Before;}
        private static bool Eligible(ConquerItem i,uint id,DateTime now){
            if(i==null || i.ID!=id || i.Position!=0 || i.Warehouse!=0 || i.Lock!=0 || i.Suspicious || i.InWardrobe)return false;
            return i.Days==0?i.TimeLeftInMinutes==0:now.ToLocalTime()<i.DayStamp.AddDays(i.Days);
        }
        private static Take From(ConquerItem i,ushort amount){
            return new Take{Item=i,Uid=i.UID,Before=i.StackSize,Amount=amount,Bound=i.Bound,Days=i.Days,Stamp=i.DayStamp};
        }
        private static List<Take> Select(GameState p,uint fragment,DateTime now){
            var stones=p.Inventory.Objects.Where(i=>Eligible(i,fragment,now)).OrderBy(i=>i.UID).ToArray();
            int total=0;foreach(var i in stones)total+=Units(i);if(total<Cost)return null;
            var takes=new List<Take>();int need=Cost;
            foreach(var i in stones){
                ushort amt=(ushort)Math.Min(need,Units(i));
                takes.Add(From(i,amt));need-=amt;if(need==0)break;
            }
            return need==0?takes:null;
        }
        private static ConquerItem Rune(uint id){
            var info=ConquerItemInformation.BaseInformations[id];
            return new ConquerItem(true){ID=id,UID=ConquerItem.ItemUID.Next,Bound=true,Plus=0,StackSize=1,Position=0,
                Color=(Enums.Color)3,RuneSkill=info.subtype,Durability=info.Durability,MaximDurability=info.Durability};
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
            if(Fragment(item.ID))Combine(item,p);else Open(item,p);return true;
        }
        private static void Combine(ConquerItem item,GameState p){
            lock(p.ItemSyncRoot)try{
                ConquerItem owned;DateTime now=Clock();
                if(!Ready(p) || !p.Inventory.TryGetItem(item.UID,out owned) || !ReferenceEquals(owned,item) || !Fragment(item.ID) || !Eligible(item,item.ID,now))return;
                var takes=Select(p,item.ID,now);
                int released=takes==null?0:takes.Count(Whole);
                if(takes==null || p.Inventory.Count-released>=40){Hear(p,"You need 20 eligible fragments and room for the resulting pack.");return;}
                var reward=Q.New(PackOf(item.ID),true);
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                        if(q.ExecuteScalar()==null)throw new InvalidOperationException("Yellow/blue rune combine owner missing.");
                    var ids=new List<uint>();using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid","uid",(long)p.Entity.UID))
                    using(var r=q.ExecuteReader())while(r.Read()){if(ids.Count>=40)throw new InvalidOperationException("Yellow/blue rune inventory exceeds capacity.");ids.Add(checked((uint)Convert.ToInt64(r[0])));}
                    if(!ids.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("Yellow/blue rune inventory changed.");
                    foreach(var take in takes){
                        string sql=Whole(take)?"DELETE FROM items":"UPDATE items SET stacksize=stacksize-@amount";
                        if(Q.Execute(c,t,sql+@" WHERE uid=@item AND entityid=@uid AND id=@type AND warehouse=0 AND position=0 AND stacksize=@stack AND bound=@bound
AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')=@days AND (@permanent OR daystamp=@stamp)",
                            "item",(long)take.Uid,"uid",(long)p.Entity.UID,"type",(int)item.ID,"stack",(int)take.Before,"amount",(int)take.Amount,"bound",take.Bound?1:0,
                            "days",take.Days.ToString(CultureInfo.InvariantCulture),"permanent",take.Days==0,"stamp",take.Stamp.ToBinary())!=1)
                            throw new InvalidOperationException("Yellow/blue rune fragment ownership changed.");
                    }
                    Q.Insert(c,t,p.Entity.UID,reward);
                    if(Q.Execute(c,t,@"INSERT INTO revora_yb_rune_combines(id,uid,fragment_type,consumed,bound,sources,amounts,pack_uid,pack_type,used_at)
VALUES(@id,@uid,@fragment,@consumed,@bound,@sources,@amounts,@pack,@ptype,@now)","id",Guid.NewGuid(),"uid",(long)p.Entity.UID,"fragment",(int)item.ID,
                        "consumed",(int)Cost,"bound",true,"sources",takes.Select(x=>(long)x.Uid).ToArray(),"amounts",takes.Select(x=>(int)x.Amount).ToArray(),
                        "pack",(long)reward.UID,"ptype",(int)reward.ID,"now",now)!=1)
                        throw new InvalidOperationException("Yellow/blue rune combine receipt was not saved.");
                    if(!Ready(p))throw new InvalidOperationException("Yellow/blue rune combine session changed.");t.Commit();
                }}
                var before=BeforeInstall;if(before!=null)before(p);
                Q.Install(p,takes.Where(Whole).Select(x=>x.Uid),new[]{reward});
                foreach(var take in takes.Where(x=>!Whole(x))){take.Item.StackSize=(ushort)(take.Before-take.Amount);take.Item.Mode=Enums.ItemMode.Update;take.Item.Send(p);take.Item.Mode=Enums.ItemMode.Default;}
            }catch(Exception ex){Q.Fail(p,"yb-rune-combine",ex);}
        }
        private static void Open(ConquerItem item,GameState p){
            lock(p.ItemSyncRoot)try{
                ConquerItem owned;DateTime now=Clock();
                if(!Ready(p) || !p.Inventory.TryGetItem(item.UID,out owned) || !ReferenceEquals(owned,item) || !Pack(item.ID) || !Eligible(item,item.ID,now))return;
                bool deplete=item.StackSize<=1;
                if(!deplete && p.Inventory.Count>=40)return;
                int pick=Roll(1,item.ID==BluePack?10:15);
                uint resultId=Result(item.ID,pick);
                if(resultId==0 || !ConquerItemInformation.BaseInformations.ContainsKey(resultId))return;
                var reward=Rune(resultId);
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                        if(q.ExecuteScalar()==null)throw new InvalidOperationException("Yellow/blue rune pack owner missing.");
                    var ids=new List<uint>();using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid","uid",(long)p.Entity.UID))
                    using(var r=q.ExecuteReader())while(r.Read()){if(ids.Count>=40)throw new InvalidOperationException("Yellow/blue rune inventory exceeds capacity.");ids.Add(checked((uint)Convert.ToInt64(r[0])));}
                    if(!ids.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("Yellow/blue rune inventory changed.");
                    string sql=deplete?"DELETE FROM items":"UPDATE items SET stacksize=stacksize-1";
                    if(Q.Execute(c,t,sql+@" WHERE uid=@item AND entityid=@uid AND id=@type AND warehouse=0 AND position=0 AND stacksize=@stack AND bound=@bound
AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')=@days AND (@permanent OR daystamp=@stamp)",
                        "item",(long)item.UID,"uid",(long)p.Entity.UID,"type",(int)item.ID,"stack",(int)item.StackSize,"bound",item.Bound?1:0,
                        "days",item.Days.ToString(CultureInfo.InvariantCulture),"permanent",item.Days==0,"stamp",item.DayStamp.ToBinary())!=1)
                        throw new InvalidOperationException("Yellow/blue rune pack ownership changed.");
                    if(Q.Execute(c,t,@"INSERT INTO items(uid,id,entityid,position,warehouse,stacksize,bound,plus,durability,maximdurability,color)
VALUES(@item,@type,@uid,0,0,1,1,0,@dur,@max,3)","item",(long)reward.UID,"type",(int)reward.ID,"uid",(long)p.Entity.UID,
                        "dur",(int)reward.Durability,"max",(int)reward.MaximDurability)!=1)
                        throw new InvalidOperationException("Yellow/blue rune was not saved.");
                    if(Q.Execute(c,t,@"INSERT INTO revora_yb_rune_pack_uses(id,uid,pack_uid,pack_type,result_uid,result_type,pick,used_at)
VALUES(@id,@uid,@pack,@type,@result,@rid,@pick,@now)","id",Guid.NewGuid(),"uid",(long)p.Entity.UID,"pack",(long)item.UID,"type",(int)item.ID,
                        "result",(long)reward.UID,"rid",(int)reward.ID,"pick",pick,"now",now)!=1)
                        throw new InvalidOperationException("Yellow/blue rune pack receipt was not saved.");
                    if(!Ready(p))throw new InvalidOperationException("Yellow/blue rune pack session changed.");t.Commit();
                }}
                var before=BeforeInstall;if(before!=null)before(p);
                Q.Install(p,deplete?new[]{item.UID}:new uint[0],new[]{reward});
                if(!deplete){item.StackSize--;item.Mode=Enums.ItemMode.Update;item.Send(p);item.Mode=Enums.ItemMode.Default;}
            }catch(Exception ex){Q.Fail(p,"yb-rune-pack",ex);}
        }
    }
}
