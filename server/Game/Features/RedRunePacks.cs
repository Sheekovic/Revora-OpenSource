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
    // NPC 900181 Online Points Seller issues leftover RedRuneFragment 3306369.
    // Combine 20 units into bound RandomRedRune(B)Pack 3306509. Pack Random.Next(1,11)
    // yields 1-10 bound red runes; leftover case 11 is unreachable. Yellow/blue
    // consumers are owned separately; SuperBoss Next(1,17) case 17 and NPC blue Add(3306368) are not 3306370/3306371.
    internal static class RedRunePacks
    {
        internal const uint Fragment=3306369,Pack=3306509;
        internal const int Cost=20;
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        internal static Func<int,int,int> Roll=(min,max)=>new Random().Next(min,max);
        internal static Action<string> Spoken;
        private static bool enabled;
        internal static bool Enabled{get{return enabled;}}
        internal static readonly uint[] Rewards=new uint[]{4010001,4010101,4010201,4010301,4010401,4010501,4010601,4010701,4010801,4010901};
        private sealed class Take{internal ConquerItem Item;internal uint Uid;internal ushort Before,Amount;internal bool Bound;internal byte Days;internal DateTime Stamp;}
        private static bool Handles(uint id){return id==Fragment || id==Pack;}
        internal static uint Result(int pick){return pick>=1 && pick<=Rewards.Length?Rewards[pick-1]:0;}
        internal static void Initialize(){
            using(var c=DataHolder.MySqlConnection){c.Open();
                Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_red_rune_combines(
id uuid PRIMARY KEY,uid bigint NOT NULL,item_uid bigint NOT NULL,consumed integer NOT NULL CHECK(consumed=20),
bound boolean NOT NULL,sources bigint[] NOT NULL,amounts integer[] NOT NULL,
pack_uid bigint NOT NULL,used_at timestamptz NOT NULL);
CREATE TABLE IF NOT EXISTS revora_red_rune_pack_uses(
id uuid PRIMARY KEY,uid bigint NOT NULL,pack_uid bigint NOT NULL,pack_type integer NOT NULL,
result_uid bigint NOT NULL,result_type integer NOT NULL,pick smallint NOT NULL,used_at timestamptz NOT NULL);");}
            if(!ConquerItemInformation.BaseInformations.ContainsKey(Fragment) || !ConquerItemInformation.BaseInformations.ContainsKey(Pack))
                throw new InvalidOperationException("Red rune catalog incomplete.");
            foreach(uint rune in Rewards)
                if(!ConquerItemInformation.BaseInformations.ContainsKey(rune))
                    throw new InvalidOperationException("Red rune catalog incomplete for "+rune+".");
            enabled=true;
        }
        private static bool Ready(GameState p){return enabled && Q.Player(p) && p.Booth==null && p.Trade!=null && !p.Trade.InTrade;}
        private static void Hear(GameState p,string text){p.Send(text);var s=Spoken;if(s!=null)s(text);}
        private static int Units(ConquerItem i){return i.StackSize<=1?1:i.StackSize;}
        private static bool Whole(Take t){return t.Before<=1 || t.Amount>=t.Before;}
        private static Take From(ConquerItem i,ushort amount){
            return new Take{Item=i,Uid=i.UID,Before=i.StackSize,Amount=amount,Bound=i.Bound,Days=i.Days,Stamp=i.DayStamp};
        }
        private static List<Take> Select(GameState p){
            var stones=p.Inventory.Objects.Where(i=>i.ID==Fragment).ToArray();
            int total=0;foreach(var i in stones)total+=Units(i);if(total<Cost)return null;
            var big=stones.FirstOrDefault(i=>i.StackSize>1 && i.StackSize>=Cost);
            if(big!=null)return new List<Take>{From(big,(ushort)Cost)};
            var takes=new List<Take>();int need=Cost;
            foreach(var i in stones){
                ushort amt=(ushort)Math.Min(need,Units(i));
                takes.Add(From(i,amt));need-=amt;if(need==0)break;
            }
            return need==0?takes:null;
        }
        private static List<uint> Bag(NpgsqlConnection c,NpgsqlTransaction t,GameState p){
            var ids=new List<uint>();
            using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid","uid",(long)p.Entity.UID))
            using(var r=q.ExecuteReader())while(r.Read()){if(ids.Count>=40)throw new InvalidOperationException("Red rune inventory exceeds capacity.");ids.Add(checked((uint)Convert.ToInt64(r[0])));}
            if(!ids.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("Red rune inventory changed.");
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
            if(item.ID==Fragment)Combine(item,p);else Open(item,p);return true;
        }
        private static void Combine(ConquerItem item,GameState p){
            lock(p.ItemSyncRoot)try{
                ConquerItem owned;DateTime now=Clock();
                if(!Ready(p) || !p.Inventory.TryGetItem(item.UID,out owned) || !ReferenceEquals(owned,item) || item.ID!=Fragment || item.Position!=0 || item.Warehouse!=0
                    || item.Lock!=0 || item.Suspicious || item.InWardrobe || (item.Days==0?item.TimeLeftInMinutes!=0:now.ToLocalTime()>=item.DayStamp.AddDays(item.Days)))return;
                var takes=Select(p);
                int released=takes==null?0:takes.Count(Whole);
                if(takes==null || p.Inventory.Count-released>=40){Hear(p,"You do not have 20 pieces, or you lack 5 inventory spaces.");return;}
                var reward=Q.New(Pack,true);
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                        if(q.ExecuteScalar()==null)throw new InvalidOperationException("Red rune combine owner missing.");
                    Bag(c,t,p);
                    foreach(var take in takes){
                        string sql=Whole(take)?"DELETE FROM items":"UPDATE items SET stacksize=stacksize-@amount";
                        if(Q.Execute(c,t,sql+@" WHERE uid=@item AND entityid=@uid AND id=@type AND warehouse=0 AND position=0 AND stacksize=@stack AND bound=@bound
AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')=@days AND (@permanent OR daystamp=@stamp)",
                            "item",(long)take.Uid,"uid",(long)p.Entity.UID,"type",(int)Fragment,"stack",(int)take.Before,"amount",(int)take.Amount,"bound",take.Bound?1:0,
                            "days",take.Days.ToString(CultureInfo.InvariantCulture),"permanent",take.Days==0,"stamp",take.Stamp.ToBinary())!=1)
                            throw new InvalidOperationException("Red rune fragment ownership changed.");
                    }
                    Q.Insert(c,t,p.Entity.UID,reward);
                    if(Q.Execute(c,t,@"INSERT INTO revora_red_rune_combines(id,uid,item_uid,consumed,bound,sources,amounts,pack_uid,used_at)
VALUES(@id,@uid,@item,@consumed,@bound,@sources,@amounts,@pack,@now)","id",Guid.NewGuid(),"uid",(long)p.Entity.UID,"item",(long)item.UID,
                        "consumed",(int)Cost,"bound",item.Bound,"sources",takes.Select(x=>(long)x.Uid).ToArray(),
                        "amounts",takes.Select(x=>(int)x.Amount).ToArray(),"pack",(long)reward.UID,"now",now)!=1)
                        throw new InvalidOperationException("Red rune combine receipt was not saved.");
                    if(!Ready(p))throw new InvalidOperationException("Red rune combine session changed.");t.Commit();
                }}
                Q.Install(p,takes.Where(Whole).Select(x=>x.Uid),new[]{reward});
                foreach(var take in takes.Where(x=>!Whole(x))){take.Item.StackSize=(ushort)(take.Before-take.Amount);take.Item.Mode=Enums.ItemMode.Update;take.Item.Send(p);take.Item.Mode=Enums.ItemMode.Default;}
            }catch(Exception ex){Q.Fail(p,"red-rune-combine",ex);}
        }
        private static void Open(ConquerItem item,GameState p){
            lock(p.ItemSyncRoot)try{
                ConquerItem owned;DateTime now=Clock();
                if(!Ready(p) || !p.Inventory.TryGetItem(item.UID,out owned) || !ReferenceEquals(owned,item) || item.ID!=Pack || item.Position!=0 || item.Warehouse!=0
                    || item.Lock!=0 || item.Suspicious || item.InWardrobe || (item.Days==0?item.TimeLeftInMinutes!=0:now.ToLocalTime()>=item.DayStamp.AddDays(item.Days)))return;
                bool deplete=item.StackSize<=1;
                if(!deplete && p.Inventory.Count>=40)return;
                int pick=Roll(1,11);
                uint resultId=Result(pick);
                if(resultId==0 || !ConquerItemInformation.BaseInformations.ContainsKey(resultId))return;
                var reward=Q.New(resultId,true);
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                        if(q.ExecuteScalar()==null)throw new InvalidOperationException("Red rune pack owner missing.");
                    Bag(c,t,p);
                    string sql=deplete?"DELETE FROM items":"UPDATE items SET stacksize=stacksize-1";
                    if(Q.Execute(c,t,sql+@" WHERE uid=@item AND entityid=@uid AND id=@type AND warehouse=0 AND position=0 AND stacksize=@stack AND bound=@bound
AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')=@days AND (@permanent OR daystamp=@stamp)",
                        "item",(long)item.UID,"uid",(long)p.Entity.UID,"type",(int)item.ID,"stack",(int)item.StackSize,"bound",item.Bound?1:0,
                        "days",item.Days.ToString(CultureInfo.InvariantCulture),"permanent",item.Days==0,"stamp",item.DayStamp.ToBinary())!=1)
                        throw new InvalidOperationException("Red rune pack ownership changed.");
                    Q.Insert(c,t,p.Entity.UID,reward);
                    if(Q.Execute(c,t,@"INSERT INTO revora_red_rune_pack_uses(id,uid,pack_uid,pack_type,result_uid,result_type,pick,used_at)
VALUES(@id,@uid,@pack,@type,@result,@rid,@pick,@now)","id",Guid.NewGuid(),"uid",(long)p.Entity.UID,"pack",(long)item.UID,"type",(int)item.ID,
                        "result",(long)reward.UID,"rid",(int)reward.ID,"pick",pick,"now",now)!=1)
                        throw new InvalidOperationException("Red rune pack receipt was not saved.");
                    if(!Ready(p))throw new InvalidOperationException("Red rune pack session changed.");t.Commit();
                }}
                Q.Install(p,deplete?new[]{item.UID}:new uint[0],new[]{reward});
                if(!deplete){item.StackSize--;item.Mode=Enums.ItemMode.Update;item.Send(p);item.Mode=Enums.ItemMode.Default;}
            }catch(Exception ex){Q.Fail(p,"red-rune-pack",ex);}
        }
    }
}
