using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class GuildPrizeRollover
    {
        internal const uint Trophy=2100085;
        private sealed class Change {internal Guid Prize;internal uint Uid;internal ushort Position;internal uint Warehouse;internal ConquerItem Item;}
        private static bool initialized;
        private static readonly object pollGate=new object();
        private static DateTime nextPoll;
        private static long cursor;
        internal static void Initialize() {
            ConquerItemBaseInformation info;
            if(!ConquerItemInformation.BaseInformations.TryGetValue(Trophy,out info) || info.Name!="GoldTrophy"
                || ItemHandler.GetPositionFromID(Trophy)!=ItemHandler.GetPositionFromID(2100075))throw new InvalidOperationException("Guild trophy catalog changed.");
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE INDEX IF NOT EXISTS revora_guild_prize_rollover
ON revora_guild_prizes(uid,ends_at) WHERE tier=3 AND rolled_at IS NULL;");}initialized=true;
        }
        private static bool Owner(GameState p) {
            GameState current;return p!=null && !p.Fake && p.Entity!=null && ReferenceEquals(p.Entity.Owner,p) && p.ItemSyncRoot!=null
                && !p.Entity.PersistenceRequiresReload && Kernel.GamePool.TryGetValue(p.Entity.UID,out current) && ReferenceEquals(current,p);
        }
        private static ConquerItem Find(GameState p,Change change) {
            ConquerItem item=null;
            if(change.Warehouse!=0) {
                Game.ConquerStructures.Warehouse wh;
                if(p.Warehouses.TryGetValue((Game.ConquerStructures.Warehouse.WarehouseID)change.Warehouse,out wh))item=wh.GetItem(change.Uid);
            }else if(change.Position==0)p.Inventory.TryGetItem(change.Uid,out item);
            else p.Equipment.TryGetItem(change.Uid,out item);
            if(item==null || item.UID!=change.Uid || item.ID!=2100075 || item.Position!=change.Position || item.Warehouse!=change.Warehouse
                || !item.Bound || item.StackSize!=1 || item.Days!=0 || item.InWardrobe)throw new InvalidOperationException("Guild rollover item requires reload.");
            return item;
        }
        private static int ConvertOwned(GameState p,bool loading) {
            if(!Owner(p))throw new InvalidOperationException("Guild rollover session changed.");
            var now=GuildSeasonWar.Clock();var changes=new List<Change>();
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                    if(q.ExecuteScalar()==null)throw new InvalidOperationException("Guild rollover character missing.");
                using(var q=Q.Command(c,t,@"SELECT p.id,i.uid,i.position,i.warehouse FROM revora_guild_prizes p
JOIN items i ON i.uid=p.item_uid AND i.entityid=p.uid
WHERE p.uid=@uid AND p.tier=3 AND p.rolled_at IS NULL AND p.ends_at<=@now AND i.id=2100075 AND i.bound=1
AND i.stacksize=1 AND COALESCE(NULLIF(i.days,''),'0')='0' AND COALESCE(i.inwardrobe,0)=0
ORDER BY i.uid LIMIT 1001 FOR UPDATE OF p,i","uid",(long)p.Entity.UID,"now",now))using(var r=q.ExecuteReader())while(r.Read())
                    changes.Add(new Change{Prize=r.GetGuid(0),Uid=Convert.ToUInt32(r[1]),Position=Convert.ToUInt16(r[2]),Warehouse=Convert.ToUInt32(r[3])});
                if(changes.Count>1000)throw new InvalidOperationException("Guild rollover batch exceeds owned-item limit.");
                foreach(var change in changes) {
                    if(change.Position!=0 && (change.Warehouse!=0 || change.Position!=ConquerItem.Bottle && change.Position!=ConquerItem.AlternateBottle))
                        throw new InvalidOperationException("Guild prize has an invalid saved slot.");
                    if(loading && (p.Inventory.ContainsUID(change.Uid) || p.Equipment.Objects.Any(i=>i!=null && i.UID==change.Uid)
                        || p.Warehouses.Values.Any(w=>w.ContainsUID(change.Uid))))throw new InvalidOperationException("Guild rollover must precede item loading.");
                    if(!loading)change.Item=Find(p,change);
                    if(Q.Execute(c,t,@"UPDATE items SET id=2100085,days='0' WHERE uid=@item AND entityid=@uid AND id=2100075
AND position=@position AND warehouse=@warehouse AND bound=1 AND stacksize=1 AND COALESCE(NULLIF(days,''),'0')='0' AND COALESCE(inwardrobe,0)=0",
                        "item",(long)change.Uid,"uid",(long)p.Entity.UID,"position",(int)change.Position,"warehouse",(int)change.Warehouse)!=1)
                        throw new InvalidOperationException("Guild prize conversion was not saved.");
                    if(Q.Execute(c,t,"UPDATE revora_guild_prizes SET rolled_at=@now,trophy_uid=@item WHERE id=@id AND uid=@uid AND tier=3 AND item_uid=@item AND rolled_at IS NULL AND ends_at<=@now",
                        "now",now,"item",(long)change.Uid,"id",change.Prize,"uid",(long)p.Entity.UID)!=1)throw new InvalidOperationException("Guild trophy provenance was not saved.");
                }
                if(!Owner(p) || !loading && !Q.Player(p,false))throw new InvalidOperationException("Guild rollover owner changed before commit.");
                t.Commit();
            }}
            if(!loading && changes.Count!=0) {
                foreach(var change in changes) {
                    var item=change.Item;item.ID=Trophy;item.Days=0;item.TimeLeftInMinutes=0;
                    if(item.Warehouse==0){item.Mode=Enums.ItemMode.Update;item.Send(p);}
                    // Stored items retain their exact warehouse object and appear
                    // in the normal next warehouse listing; no inventory duplicate.
                }
                if(changes.Any(i=>i.Position!=0)){p.LoadItemStats();p.CalculateHPBonus();PrestigeRanking.SendSelf(p);p.Send(new WindowStats(p).ToArray());}
                p.Send("Your season's Gold Prize has become a lasting Gold Trophy.");
            }
            return changes.Count;
        }
        internal static void BeforeLoad(GameState p) {
            if(!initialized || p==null || p.Fake)return;
            if(p.ItemSyncRoot==null)throw new InvalidOperationException("Guild rollover requires its owner lock.");
            lock(p.ItemSyncRoot)try {
                if(!Owner(p) || p.Account==null || p.Account.EntityID!=p.Entity.UID)throw new InvalidOperationException("Guild rollover login ownership changed.");
                ConvertOwned(p,true);
            }catch(Exception ex){Q.Fail(p,"guild-rollover-load",ex);throw;}
        }
        internal static void Apply(GameState p) {
            if(!initialized || !Q.Player(p,false) || !Monitor.TryEnter(p.ItemSyncRoot))return;
            try {
                if(!Q.Player(p,false) || p.Booth!=null || p.Trade==null || p.Trade.InTrade)return;
                ConvertOwned(p,false);
            }catch(Exception ex){Q.Fail(p,"guild-rollover",ex);}finally{Monitor.Exit(p.ItemSyncRoot);}
        }
        internal static void Tick() {
            if(!initialized || !Monitor.TryEnter(pollGate))return;
            try {
                var now=GuildSeasonWar.Clock();if(now<nextPoll)return;nextPoll=now.AddMinutes(1);
                var owners=Kernel.GamePool.Values.Where(p=>Q.Player(p,false)).Select(p=>(long)p.Entity.UID).ToArray();if(owners.Length==0)return;
                var due=new List<uint>();
                using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,@"SELECT DISTINCT p.uid FROM revora_guild_prizes p
JOIN items i ON i.uid=p.item_uid AND i.entityid=p.uid WHERE p.uid=ANY(@owners) AND p.uid>@cursor
AND p.tier=3 AND p.rolled_at IS NULL AND p.ends_at<=@now AND i.id=2100075 ORDER BY p.uid LIMIT 32",
                    "owners",owners,"cursor",cursor,"now",now))using(var r=q.ExecuteReader())while(r.Read())due.Add(Convert.ToUInt32(r[0]));}
                if(due.Count==0){cursor=0;return;}
                cursor=due.Last();foreach(uint uid in due){GameState p;if(Kernel.GamePool.TryGetValue(uid,out p))Apply(p);}
            }catch(Exception ex){Console.WriteLine("[guild-rollover] poll_failure="+ex.GetType().Name);}finally{Monitor.Exit(pollGate);}
        }
    }
}
