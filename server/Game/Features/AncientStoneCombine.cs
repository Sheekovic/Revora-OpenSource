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
    // Ordinary leftover AncientStone/HolyStone 3004246 combine into one 720121.
    // Leftover required twenty total units and Count<=31. Catalog collect-two-hundred
    // / P7 copy is not adopted. Direct daily P7 is unchanged.
    internal static class AncientStoneCombine
    {
        internal const uint Stone=3004246,Pack=720121;
        internal const int Cost=20;
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        internal static Action<string> Spoken;
        private static bool enabled;
        private sealed class Take{internal ConquerItem Item;internal uint Uid;internal ushort Before,Amount;internal bool Bound;internal byte Days;internal DateTime Stamp;}
        internal static void Initialize(){
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_ancient_stone_combines(
id uuid PRIMARY KEY,uid bigint NOT NULL CHECK(uid>0),item_uid bigint NOT NULL,
item_type integer NOT NULL CHECK(item_type=3004246),consumed integer NOT NULL CHECK(consumed=20),
bound boolean NOT NULL,sources bigint[] NOT NULL,amounts integer[] NOT NULL,
rewards bigint[] NOT NULL,used_at timestamptz NOT NULL);");}enabled=true;
        }
        private static bool Ready(GameState p){return enabled && Q.Player(p) && p.Booth==null && p.Trade!=null && !p.Trade.InTrade;}
        private static void Hear(GameState p,string text){p.Send(text);var s=Spoken;if(s!=null)s(text);}
        private static int Units(ConquerItem i){return i.StackSize<=1?1:i.StackSize;}
        private static bool Whole(Take t){return t.Before<=1 || t.Amount>=t.Before;}
        private static ConquerItem Gift(){
            ushort dur=1;
            if(ConquerItemInformation.BaseInformations.ContainsKey(Pack))dur=ConquerItemInformation.BaseInformations[Pack].Durability;
            return new ConquerItem(true){ID=Pack,UID=ConquerItem.ItemUID.Next,Plus=0,Bound=false,StackSize=1,Durability=dur,MaximDurability=dur};
        }
        private static Take From(ConquerItem i,ushort amount){
            return new Take{Item=i,Uid=i.UID,Before=i.StackSize,Amount=amount,Bound=i.Bound,Days=i.Days,Stamp=i.DayStamp};
        }
        private static List<Take> Select(GameState p){
            var stones=p.Inventory.Objects.Where(i=>i.ID==Stone).ToArray();
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
        internal static bool TryHandle(GameState p,byte[] packet){
            ConquerItem item;
            if(p==null || p.Inventory==null || packet==null || packet.Length<12 || !p.Inventory.TryGetItem(BitConverter.ToUInt32(packet,8),out item) || item.ID!=Stone)return false;
            if(packet.Length<32 || packet.Length>128 || BitConverter.ToUInt16(packet,0)+8!=packet.Length || BitConverter.ToUInt16(packet,2)!=1009
                || BitConverter.ToUInt16(packet,20)!=4 || BitConverter.ToUInt64(packet,packet.Length-8)!=Revora.Network.PacketHandler.ClientSeal)return true;
            TryUse(item,p);return true;
        }
        internal static bool TryUse(ConquerItem item,GameState p){
            if(item==null || item.ID!=Stone)return false;if(!Ready(p))return true;
            lock(p.ItemSyncRoot)try{
                ConquerItem owned;DateTime now=Clock();
                if(!Ready(p) || !p.Inventory.TryGetItem(item.UID,out owned) || !ReferenceEquals(item,owned) || item.Position!=0 || item.Warehouse!=0
                    || item.Lock!=0 || item.Suspicious || item.InWardrobe || (item.Days==0?item.TimeLeftInMinutes!=0:now.ToLocalTime()>=item.DayStamp.AddDays(item.Days)))return true;
                var takes=Select(p);
                if(takes==null || p.Inventory.Count>31){Hear(p,"You do not have 20 pieces, or you lack 5 inventory spaces.");return true;}
                var reward=Gift();
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                        if(q.ExecuteScalar()==null)throw new InvalidOperationException("Ancient Stone combine owner missing.");
                    var ids=new List<uint>();using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid","uid",(long)p.Entity.UID))
                    using(var r=q.ExecuteReader())while(r.Read()){if(ids.Count>=40)throw new InvalidOperationException("Ancient Stone combine inventory exceeds capacity.");ids.Add(checked((uint)Convert.ToInt64(r[0])));}
                    if(!ids.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("Ancient Stone combine inventory changed.");
                    foreach(var take in takes){
                        string sql=Whole(take)?"DELETE FROM items":"UPDATE items SET stacksize=stacksize-@amount";
                        if(Q.Execute(c,t,sql+@" WHERE uid=@item AND entityid=@uid AND id=@type AND warehouse=0 AND position=0 AND stacksize=@stack AND bound=@bound
AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')=@days AND (@permanent OR daystamp=@stamp)",
                            "item",(long)take.Uid,"uid",(long)p.Entity.UID,"type",(int)Stone,"stack",(int)take.Before,"amount",(int)take.Amount,"bound",take.Bound?1:0,
                            "days",take.Days.ToString(CultureInfo.InvariantCulture),"permanent",take.Days==0,"stamp",take.Stamp.ToBinary())!=1)
                            throw new InvalidOperationException("Ancient Stone ownership changed.");
                    }
                    if(Q.Execute(c,t,@"INSERT INTO items(uid,id,entityid,position,warehouse,stacksize,bound,plus,durability,maximdurability)
VALUES(@item,@type,@uid,0,0,1,@bound,@plus,@dur,@max)","item",(long)reward.UID,"type",(int)reward.ID,"uid",(long)p.Entity.UID,
                        "bound",reward.Bound?1:0,"plus",(int)reward.Plus,"dur",(int)reward.Durability,"max",(int)reward.MaximDurability)!=1)
                        throw new InvalidOperationException("Ancient Stone pack was not saved.");
                    if(Q.Execute(c,t,@"INSERT INTO revora_ancient_stone_combines(id,uid,item_uid,item_type,consumed,bound,sources,amounts,rewards,used_at)
VALUES(@id,@uid,@item,@type,@consumed,@bound,@sources,@amounts,@rewards,@now)","id",Guid.NewGuid(),"uid",(long)p.Entity.UID,"item",(long)item.UID,
                        "type",(int)Stone,"consumed",(int)Cost,"bound",item.Bound,"sources",takes.Select(x=>(long)x.Uid).ToArray(),
                        "amounts",takes.Select(x=>(int)x.Amount).ToArray(),"rewards",new[]{(long)reward.UID},"now",now)!=1)
                        throw new InvalidOperationException("Ancient Stone combine receipt was not saved.");
                    if(!Ready(p))throw new InvalidOperationException("Ancient Stone combine session changed.");t.Commit();
                }}
                Q.Install(p,takes.Where(Whole).Select(x=>x.Uid),new[]{reward});
                foreach(var take in takes.Where(x=>!Whole(x))){take.Item.StackSize=(ushort)(take.Before-take.Amount);take.Item.Mode=Enums.ItemMode.Update;take.Item.Send(p);take.Item.Mode=Enums.ItemMode.Default;}
            }catch(Exception ex){Q.Fail(p,"ancient-stone-combine",ex);}return true;
        }
    }
}
