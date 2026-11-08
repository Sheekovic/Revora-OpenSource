using System;
using Revora.Client;
using Revora.Database;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features
{
    internal static class ArenaPointPurchases
    {
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        internal static void Initialize() {
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_arena_point_purchases(
id uuid PRIMARY KEY,uid bigint NOT NULL,cost bigint NOT NULL CHECK(cost=9000000),
points_before bigint NOT NULL CHECK(points_before BETWEEN 0 AND 1500),points_after bigint NOT NULL,
purchased_at timestamptz NOT NULL,CHECK(points_after=points_before+1500));
CREATE INDEX IF NOT EXISTS revora_arena_point_purchase_owner ON revora_arena_point_purchases(uid,purchased_at);");}
        }
        private static bool Ready(GameState p) {
            return Q.Player(p) && p.QualifierGroup==null && p.TeamQualifierGroup==null
                && p.Trade!=null && !p.Trade.InTrade && p.Booth==null
                && p.ArenaStatistic!=null && p.TeamArenaStatistic!=null
                && p.ArenaStatistic.EntityID==p.Entity.UID && p.TeamArenaStatistic.EntityID==p.Entity.UID
                && p.ArenaStatistic.PlayWith==null && p.TeamArenaStatistic.PlayWith==null
                && p.ArenaStatistic.ArenaPoints==p.TeamArenaStatistic.ArenaPoints;
        }
        internal static void Buy(GameState p) {
            if(!Ready(p))return;
            lock(p.ItemSyncRoot)try {
                if(!Ready(p) || p.ArenaPoints>1500)return;
                if(p.Entity.Money<9000000){p.Send("You need 9,000,000 silver to buy Arena points.");return;}
                var now=Clock();if(now.Kind!=DateTimeKind.Utc)throw new InvalidOperationException("Arena purchase requires UTC.");
                // Preserve unflushed legacy earnings before charging. Managed saves cannot overwrite durable values.
                ArenaTable.SaveArenaStatistics(p.ArenaStatistic);TeamArenaTable.SaveArenaStatistics(p.TeamArenaStatistic);
                uint before=p.ArenaPoints,after=before+1500;ulong silver=p.Entity.Money,next=silver-9000000;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    SkillTable.LockOwner(c,t,p);
                    if(TeamArenaResultStatistics.Pending(c,t,p.Entity.UID))return;
                    if(ArenaStartupRecovery.Initialized)
                        using(var q=Q.Command(c,t,@"SELECT EXISTS(SELECT 1 FROM revora_arena_matches m WHERE m.state='active'
AND (m.player1=@uid OR m.player2=@uid) AND NOT EXISTS(SELECT 1 FROM revora_arena_abandoned a WHERE a.run=m.id))","uid",(long)p.Entity.UID))
                            if(Equals(q.ExecuteScalar(),true))return;
                    using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_arena_point_purchases WHERE uid=@uid AND purchased_at>@cutoff)",
                        "uid",(long)p.Entity.UID,"cutoff",now.AddSeconds(-1)))if(Equals(q.ExecuteScalar(),true))return;
                    foreach(string table in new[]{"arena","teamarena"}) {
                        using(var q=Q.Command(c,t,"SELECT arenapoints FROM "+table+" WHERE entityid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                        using(var r=q.ExecuteReader())if(!r.Read() || Convert.ToUInt32(r[0])!=before || r.Read())throw new InvalidOperationException("Arena purchase points require reload.");
                        if(Q.Execute(c,t,"UPDATE "+table+" SET arenapoints=@after WHERE entityid=@uid","after",(long)after,"uid",(long)p.Entity.UID)!=1)
                            throw new InvalidOperationException("Arena purchase points were not saved.");
                    }
                    SilverBalance.Persist(c,t,p,next);
                    if(Q.Execute(c,t,@"INSERT INTO revora_arena_point_purchases(id,uid,cost,points_before,points_after,purchased_at)
VALUES(@id,@uid,9000000,@before,@after,@now)","id",Guid.NewGuid(),"uid",(long)p.Entity.UID,"before",(long)before,"after",(long)after,"now",now)!=1)
                        throw new InvalidOperationException("Arena purchase receipt was not saved.");
                    if(!Ready(p) || p.ArenaPoints!=before || p.Entity.Money!=silver)throw new InvalidOperationException("Arena purchase owner changed.");
                    t.Commit();
                }}
                p.ArenaPoints=after;SilverBalance.ApplyCommitted(p,next);
                p.ArenaStatistic.Send(p);p.TeamArenaStatistic.Send(p);
            }catch(Exception ex){ArenaCacheRecovery.Fail(p,"arena-point-purchase",ex);}
        }
    }
}
