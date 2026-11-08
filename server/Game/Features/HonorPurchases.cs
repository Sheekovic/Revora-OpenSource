using System;
using System.Collections.Generic;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class HonorPurchases
    {
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        // HonorShop stocks these Hossu packs, but ItemHandler has no UseItem case
        // and no leftover reward mapping. Refuse new Honor spends; keep held items.
        private static readonly HashSet<uint> UnusableHossuPacks=new HashSet<uint>{3006316u,3006317u,3006318u,3006320u,3006321u};
        internal static void Initialize() {
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_honor_purchases(
id uuid PRIMARY KEY,uid bigint NOT NULL,item_type integer NOT NULL,item_uid bigint NOT NULL UNIQUE,
cost bigint NOT NULL CHECK(cost>0),honor_before bigint NOT NULL,honor_after bigint NOT NULL CHECK(honor_after>=0),
purchased_at timestamptz NOT NULL,CHECK(honor_before-honor_after=cost));
CREATE INDEX IF NOT EXISTS revora_honor_recent ON revora_honor_purchases(uid,item_type,purchased_at);");}
        }
        private static bool Ready(GameState p) {
            return Q.Player(p) && p.Trade!=null && !p.Trade.InTrade && p.Booth==null
                && p.ArenaStatistic!=null && p.TeamArenaStatistic!=null
                && p.ArenaStatistic.EntityID==p.Entity.UID && p.TeamArenaStatistic.EntityID==p.Entity.UID
                && p.ArenaStatistic.CurrentHonor==p.TeamArenaStatistic.CurrentHonor;
        }
        internal static void Buy(GameState p,uint type) {
            if(!Ready(p))return;
            lock(p.ItemSyncRoot)try {
                uint cost;
                if(!Ready(p) || HonorShop.Shop==null || HonorShop.Shop.Items==null
                    || !HonorShop.Shop.Items.TryGetValue(type,out cost) || cost==0
                    || !ConquerItemInformation.BaseInformations.ContainsKey(type))return;
                if(UnusableHossuPacks.Contains(type)){p.Send("This item cannot be used.");return;}
                if(p.Inventory.Count>=40){p.Send("Make one place for your purchase.");return;}
                if(p.CurrentHonor<cost){p.Send("You do not have enough Honor Points.");return;}
                // Legacy match rewards are accumulated in the canonical statistic.
                // Flush that earned balance under the owner lock before charging;
                // a failed purchase must not erase already earned Honor on reload.
                ArenaTable.SaveArenaStatistics(p.ArenaStatistic);
                TeamArenaTable.SaveArenaStatistics(p.TeamArenaStatistic);
                uint before=p.CurrentHonor,after=before-cost;
                var item=Q.New(type,false);
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    SkillTable.LockOwner(c,t,p);
                    DateTime now=Clock();
                    using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_honor_purchases WHERE uid=@uid AND item_type=@type AND purchased_at>@now-interval '1 second')",
                        "uid",(long)p.Entity.UID,"type",(int)type,"now",now))if(Equals(q.ExecuteScalar(),true))return;
                    using(var q=Q.Command(c,t,"SELECT count(*) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0","uid",(long)p.Entity.UID))
                        if(Convert.ToInt64(q.ExecuteScalar())!=p.Inventory.Count)throw new InvalidOperationException("Honor purchase inventory requires reload.");
                    using(var q=Q.Command(c,t,"SELECT currenthonor FROM arena WHERE entityid=@uid FOR UPDATE","uid",(long)p.Entity.UID)) {
                        object saved=q.ExecuteScalar();
                        if(saved==null || Convert.ToUInt32(saved)!=before)
                            throw new InvalidOperationException("Honor balance differs from saved Arena identity.");
                    }
                    // Solo Arena is the login authority; team Arena mirrors the spend.
                    if(Q.Execute(c,t,"UPDATE arena SET currenthonor=@after WHERE entityid=@uid AND currenthonor=@before",
                        "uid",(long)p.Entity.UID,"before",(long)before,"after",(long)after)!=1
                        || Q.Execute(c,t,"UPDATE teamarena SET currenthonor=@after WHERE entityid=@uid",
                        "uid",(long)p.Entity.UID,"after",(long)after)!=1)
                        throw new InvalidOperationException("Honor purchase balances were not saved.");
                    Q.Insert(c,t,p.Entity.UID,item);
                    if(Q.Execute(c,t,@"INSERT INTO revora_honor_purchases(id,uid,item_type,item_uid,cost,honor_before,honor_after,purchased_at)
VALUES(@id,@uid,@type,@item,@cost,@before,@after,@now)","id",Guid.NewGuid(),"uid",(long)p.Entity.UID,"type",(int)type,
                        "item",(long)item.UID,"cost",(long)cost,"before",(long)before,"after",(long)after,"now",now)!=1)
                        throw new InvalidOperationException("Honor purchase receipt was not saved.");
                    if(!Ready(p) || p.CurrentHonor!=before)throw new InvalidOperationException("Honor purchase owner changed.");
                    t.Commit();
                }}
                p.CurrentHonor=after;Q.Install(p,new uint[0],new[]{item});
                p.ArenaStatistic.Send(p);p.TeamArenaStatistic.Send(p);
            }catch(Exception ex){ArenaCacheRecovery.Fail(p,"honor-purchase",ex);}
        }
    }
}
