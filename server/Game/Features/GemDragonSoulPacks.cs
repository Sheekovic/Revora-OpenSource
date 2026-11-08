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
    // Way of Heroes StageGoal gem/soul prizes and FashionSuitPack 3001264.
    // Leftover UseItem removed the pack before choosing a bound catalog reward.
    // An empty pool or a failed insert could lose the pack. The reward is always
    // bound, matching AddBoundItem. Refined and super gems stay the leftover
    // ten-id lists. Dragon Soul bags stay every installed row at purification
    // level 4 or 6, chosen uniformly. Fashion suits use compatible five-star
    // garments for seven days, retaining the installed bound rental duration.
    internal static class GemDragonSoulPacks
    {
        internal const uint RefinedPack=3005130,SuperPack=3005132,Phase4Pack=3005133,Phase6Pack=3005135;
        internal static readonly uint[] RefinedIds=new uint[]{700002,700012,700022,700032,700042,700052,700062,700072,700102,700122};
        internal static readonly uint[] SuperIds=new uint[]{700003,700013,700023,700033,700043,700053,700063,700073,700103,700123};
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        internal static Func<int,int,int> Roll=(min,max)=>Kernel.Random.Next(min,max);
        internal static Action<string> Spoken;
        internal static Action<GameState> BeforeInstall;
        internal static uint[] RefinedGems;
        internal static uint[] SuperGems;
        internal static uint[] PhaseFour;
        internal static uint[] PhaseSix;
        private static bool enabled;
        internal static bool Enabled{get{return enabled;}}
        private static readonly Dictionary<uint,byte> Catalog=new Dictionary<uint,byte>{{3005130,0},{3005132,0},{3005133,0},{3005135,0}};
        private static bool Handles(uint id){return id==3005131 || id==3001264 || Catalog.ContainsKey(id);}
        internal static uint[] Pool(uint pack){
            // Revora pool: the twelve installed decorative furniture types, equally likely.
            if(pack==3005131)return Enumerable.Range(721177,12).Select(id=>(uint)id).ToArray();
            if(pack==3001264)return CollectionAchievementCatalog.Garments.Where(id=>id>=181305 && id<=194300
                && ConquerItemInformation.BaseInformations.ContainsKey(id) && (int)ItemHandler.GetPositionFromID(id)==ConquerItem.Garment
                && StorageItem.Garments.ContainsKey(id) && StorageItem.Garments[id].ID==id && StorageItem.Garments[id].Type==1 && StorageItem.Garments[id].Stars==5)
                .Distinct().OrderBy(id=>id).ToArray();
            if(pack==RefinedPack)return RefinedGems??new uint[0];
            if(pack==SuperPack)return SuperGems??new uint[0];
            if(pack==Phase4Pack)return PhaseFour??new uint[0];
            if(pack==Phase6Pack)return PhaseSix??new uint[0];
            return new uint[0];
        }
        internal static void Initialize(){
            using(var c=DataHolder.MySqlConnection){c.Open();
                Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_gem_dragon_soul_pack_uses(
id uuid PRIMARY KEY,uid bigint NOT NULL,pack_uid bigint NOT NULL,
pack_type integer NOT NULL CHECK(pack_type IN (3005130,3005132,3005133,3005135)),
result_uid bigint NOT NULL,result_type integer NOT NULL,
pick smallint NOT NULL,pool_size smallint NOT NULL,used_at timestamptz NOT NULL);
CREATE TABLE IF NOT EXISTS revora_fine_furniture_pack_uses(
id uuid PRIMARY KEY,uid bigint NOT NULL,pack_uid bigint NOT NULL,pack_type integer NOT NULL CHECK(pack_type=3005131),
result_uid bigint NOT NULL UNIQUE,result_type integer NOT NULL CHECK(result_type BETWEEN 721177 AND 721188),
pick smallint NOT NULL CHECK(pick BETWEEN 0 AND 11),pool_size smallint NOT NULL CHECK(pool_size=12),used_at timestamptz NOT NULL);
CREATE TABLE IF NOT EXISTS revora_fashion_suit_pack_uses(
id uuid PRIMARY KEY,uid bigint NOT NULL,pack_uid bigint NOT NULL,pack_type integer NOT NULL CHECK(pack_type=3001264),
result_uid bigint NOT NULL UNIQUE,result_type integer NOT NULL,pick smallint NOT NULL,pool_size smallint NOT NULL,
days smallint NOT NULL CHECK(days=7),used_at timestamptz NOT NULL);");}
            foreach(uint id in Pool(3005131).Concat(new uint[]{3005131}).Concat(RefinedIds).Concat(SuperIds).Concat(new uint[]{RefinedPack,SuperPack,Phase4Pack,Phase6Pack}))
                if(!ConquerItemInformation.BaseInformations.ContainsKey(id))
                    throw new InvalidOperationException("Gem or Dragon Soul pack catalog incomplete for "+id+".");
            RefinedGems=RefinedIds.ToArray();
            SuperGems=SuperIds.ToArray();
            PhaseFour=ConquerItemInformation.BaseInformations.Where(pair=>pair.Value.PurificationLevel==4).Select(pair=>pair.Key).OrderBy(id=>id).ToArray();
            PhaseSix=ConquerItemInformation.BaseInformations.Where(pair=>pair.Value.PurificationLevel==6).Select(pair=>pair.Key).OrderBy(id=>id).ToArray();
            if(PhaseFour.Length==0 || PhaseSix.Length==0)
                throw new InvalidOperationException("Dragon Soul pack catalog has an empty purification pool.");
            enabled=true;
        }
        private static bool Ready(GameState p){return enabled && Q.Player(p) && p.Booth==null && p.Trade!=null && !p.Trade.InTrade;}
        private static void Say(GameState p,string text){p.Send(text);var spoken=Spoken;if(spoken!=null)spoken(text);}
        private static ConquerItem RewardItem(uint id){
            ushort dur=ConquerItemInformation.BaseInformations[id].Durability;
            return new ConquerItem(true){ID=id,UID=ConquerItem.ItemUID.Next,Plus=0,Bound=true,StackSize=1,Durability=dur,MaximDurability=dur};
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
                uint[] pool=Pool(item.ID);
                if(pool.Length==0){Say(p,"This pack has no installed reward.");return true;}
                bool deplete=item.StackSize<=1;
                if(p.Inventory.Count+(deplete?0:1)>40){Say(p,"Your inventory is Full.");return true;}
                int pick=Roll(0,pool.Length);
                if(pick<0 || pick>=pool.Length)return true;
                uint resultId=pool[pick];
                if(!ConquerItemInformation.BaseInformations.ContainsKey(resultId))return true;
                var reward=RewardItem(resultId);
                if(item.ID==3001264){reward.Days=7;reward.DayStamp=now.ToLocalTime();}
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                        if(q.ExecuteScalar()==null)throw new InvalidOperationException("Gem or Dragon Soul pack owner missing.");
                    var ids=new List<uint>();
                    using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid","uid",(long)p.Entity.UID))
                    using(var r=q.ExecuteReader())while(r.Read()){if(ids.Count>=40)throw new InvalidOperationException("Gem or Dragon Soul pack inventory exceeds capacity.");ids.Add(checked((uint)Convert.ToInt64(r[0])));}
                    if(!ids.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("Gem or Dragon Soul pack inventory changed.");
                    string sql=deplete?"DELETE FROM items":"UPDATE items SET stacksize=stacksize-1";
                    if(Q.Execute(c,t,sql+@" WHERE uid=@item AND entityid=@uid AND id=@type AND warehouse=0 AND position=0 AND stacksize=@stack AND bound=@bound
AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')=@days AND (@permanent OR daystamp=@stamp)",
                        "item",(long)item.UID,"uid",(long)p.Entity.UID,"type",(int)item.ID,"stack",(int)item.StackSize,"bound",item.Bound?1:0,
                        "days",item.Days.ToString(CultureInfo.InvariantCulture),"permanent",item.Days==0,"stamp",item.DayStamp.ToBinary())!=1)
                        throw new InvalidOperationException("Gem or Dragon Soul pack ownership changed.");
                    if(Q.Execute(c,t,@"INSERT INTO items(uid,id,entityid,position,warehouse,stacksize,bound,plus,durability,maximdurability,days,daystamp)
VALUES(@item,@type,@uid,0,0,1,1,0,@dur,@max,@days,@stamp)","item",(long)reward.UID,"type",(int)reward.ID,"uid",(long)p.Entity.UID,
                        "dur",(int)reward.Durability,"max",(int)reward.MaximDurability,"days",reward.Days.ToString(CultureInfo.InvariantCulture),"stamp",reward.Days==0?0L:reward.DayStamp.ToBinary())!=1)
                        throw new InvalidOperationException("Gem or Dragon Soul reward was not saved.");
                    if(item.ID==3005131){
                        if(Q.Execute(c,t,@"INSERT INTO revora_fine_furniture_pack_uses(id,uid,pack_uid,pack_type,result_uid,result_type,pick,pool_size,used_at)
VALUES(@id,@uid,@pack,3005131,@result,@rid,@pick,12,@now)","id",Guid.NewGuid(),"uid",(long)p.Entity.UID,"pack",(long)item.UID,
                            "result",(long)reward.UID,"rid",(int)reward.ID,"pick",(short)pick,"now",now)!=1)
                            throw new InvalidOperationException("Fine Furniture pack receipt was not saved.");
                    }else if(item.ID==3001264){
                        if(Q.Execute(c,t,@"INSERT INTO revora_fashion_suit_pack_uses(id,uid,pack_uid,pack_type,result_uid,result_type,pick,pool_size,days,used_at)
VALUES(@id,@uid,@pack,3001264,@result,@rid,@pick,@pool,7,@now)","id",Guid.NewGuid(),"uid",(long)p.Entity.UID,"pack",(long)item.UID,
                            "result",(long)reward.UID,"rid",(int)reward.ID,"pick",(short)pick,"pool",(short)pool.Length,"now",now)!=1)
                            throw new InvalidOperationException("Fashion Suit pack receipt was not saved.");
                    }else if(Q.Execute(c,t,@"INSERT INTO revora_gem_dragon_soul_pack_uses(id,uid,pack_uid,pack_type,result_uid,result_type,pick,pool_size,used_at)
VALUES(@id,@uid,@pack,@type,@result,@rid,@pick,@pool,@now)","id",Guid.NewGuid(),"uid",(long)p.Entity.UID,"pack",(long)item.UID,
                        "type",(int)item.ID,"result",(long)reward.UID,"rid",(int)reward.ID,"pick",(short)pick,"pool",(short)pool.Length,"now",now)!=1)
                        throw new InvalidOperationException("Gem or Dragon Soul pack receipt was not saved.");
                    if(!Ready(p))throw new InvalidOperationException("Gem or Dragon Soul pack session changed.");
                    t.Commit();
                }}
                var before=BeforeInstall;if(before!=null)before(p);
                Q.Install(p,deplete?new[]{item.UID}:new uint[0],new[]{reward});
                if(!deplete){item.StackSize--;item.Mode=Enums.ItemMode.Update;item.Send(p);item.Mode=Enums.ItemMode.Default;}
                Say(p,"You received a bound "+ConquerItemInformation.BaseInformations[resultId].Name+".");
            }catch(Exception ex){Q.Fail(p,"gem-dragon-soul-pack",ex);}
            return true;
        }
    }
}
