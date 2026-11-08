using System;
using System.Threading;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Q=Revora.Game.Features.QuestInventory;
using Group=Revora.Game.Qualifier.QualifierList.QualifierGroup;
namespace Revora.Game.Features
{
    // Owned match results create durable daily entitlements; delivery is atomic.
    internal static class ArenaDailyRewards
    {
        internal const uint ExpBall=723700, RuneType=4030101;
        internal static void Initialize() {
            if(!ConquerItemInformation.BaseInformations.ContainsKey(ExpBall) || !ConquerItemInformation.BaseInformations.ContainsKey(RuneType)
                || ConquerItemInformation.BaseInformations[RuneType].Class!=0)throw new InvalidOperationException("Arena daily reward catalog missing.");
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_arena_daily_rewards(
uid bigint NOT NULL,day varchar(8) NOT NULL,milestone varchar(12) NOT NULL CHECK(milestone IN('wins9','matches20')),
source_run uuid NOT NULL REFERENCES revora_arena_matches(id),honor integer NOT NULL CHECK(honor=500),
item_type integer NOT NULL CHECK(item_type=723700),paid_at timestamptz,item_uid bigint UNIQUE,
PRIMARY KEY(uid,day,milestone),CHECK((paid_at IS NULL AND item_uid IS NULL) OR (paid_at IS NOT NULL AND item_uid IS NOT NULL)));
ALTER TABLE revora_arena_daily_rewards ADD COLUMN IF NOT EXISTS rune_type integer NOT NULL DEFAULT 0 CHECK(rune_type IN(0,4030101));
ALTER TABLE revora_arena_daily_rewards ADD COLUMN IF NOT EXISTS rune_uid bigint UNIQUE CHECK(
(rune_type=0 AND rune_uid IS NULL) OR (rune_type=4030101 AND
((paid_at IS NULL AND rune_uid IS NULL) OR (paid_at IS NOT NULL AND rune_uid IS NOT NULL))));");}
        }
        internal static void Record(NpgsqlConnection c,NpgsqlTransaction t,Group group) {
            if(group==null || !Monitor.IsEntered(group.ParticipationSync) || t==null || t.Connection!=c || !group.Done || !group.OwnsImportedMap)
                throw new InvalidOperationException("Arena daily rewards require the owned result transaction.");
            foreach(var p in new[]{group.Player1,group.Player2}) {
                if(ArenaActors.IsBot(p))continue;
                if(!Q.Player(p,false) || !Monitor.IsEntered(p.ItemSyncRoot))throw new InvalidOperationException("Arena daily owner lock missing.");
                string day;
                using(var q=Q.Command(c,t,@"SELECT m.day FROM revora_arena_matches m JOIN revora_arena_result_statistics s ON s.run=m.id
WHERE m.id=@run AND m.state='finished' AND s.uid=@uid","run",group.ParticipationKey,"uid",(long)p.Entity.UID)) {
                    day=q.ExecuteScalar() as string;if(day==null)throw new InvalidOperationException("Arena daily result statistics missing.");
                }
                long matches,wins;using(var q=Q.Command(c,t,@"SELECT count(*),count(*) FILTER(WHERE s.won) FROM revora_arena_result_statistics s
JOIN revora_arena_matches m ON m.id=s.run WHERE s.uid=@uid AND m.state='finished' AND m.day=@day","uid",(long)p.Entity.UID,"day",day))
                using(var r=q.ExecuteReader()){r.Read();matches=r.GetInt64(0);wins=r.GetInt64(1);}
                foreach(string milestone in new[]{"wins9","matches20"}) {
                    if(milestone=="wins9"?wins<9:matches<20)continue;
                    Q.Execute(c,t,@"INSERT INTO revora_arena_daily_rewards(uid,day,milestone,source_run,honor,item_type,rune_type)
VALUES(@uid,@day,@milestone,@run,500,723700,4030101) ON CONFLICT(uid,day,milestone) DO NOTHING",
                        "uid",(long)p.Entity.UID,"day",day,"milestone",milestone,"run",group.ParticipationKey);
                    using(var q=Q.Command(c,t,"SELECT count(*) FROM revora_arena_daily_rewards WHERE uid=@uid AND day=@day AND milestone=@milestone AND honor=500 AND item_type=723700",
                        "uid",(long)p.Entity.UID,"day",day,"milestone",milestone))if(Convert.ToInt64(q.ExecuteScalar())!=1)throw new InvalidOperationException("Arena daily entitlement was not saved.");
                }
            }
        }
        private static bool Ready(GameState p) {return Q.Player(p) && p.ArenaStatistic!=null && p.TeamArenaStatistic!=null
            && p.ArenaStatistic.EntityID==p.Entity.UID && p.TeamArenaStatistic.EntityID==p.Entity.UID
            && p.ArenaStatistic.CurrentHonor==p.TeamArenaStatistic.CurrentHonor && p.ArenaStatistic.HistoryHonor==p.TeamArenaStatistic.HistoryHonor
            && p.Trade!=null && !p.Trade.InTrade && p.Booth==null;}
        internal static void Pay(GameState p) {
            PayCore(p,false);
        }
        internal static void PayTeam(GameState p) {
            PayCore(p,true);
        }
        private static void PayCore(GameState p,bool team) {
            if(!Ready(p))return;
            lock(p.ItemSyncRoot)try {
                if(!Ready(p) || p.Inventory.Count>=40 || p.CurrentHonor>uint.MaxValue-500 || p.HistoryHonor>uint.MaxValue-500)return;
                uint before=p.CurrentHonor,history=p.HistoryHonor;var item=Q.New(ExpBall,false);Revora.Network.GamePackets.ConquerItem rune=null;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    SkillTable.LockOwner(c,t,p);string day,milestone;uint runeType;
                    using(var q=Q.Command(c,t,team?
                        "SELECT day,milestone,rune_type FROM revora_team_arena_daily_rewards WHERE uid=@uid AND paid_at IS NULL ORDER BY day,milestone LIMIT 1 FOR UPDATE":
                        "SELECT day,milestone,rune_type FROM revora_arena_daily_rewards WHERE uid=@uid AND paid_at IS NULL ORDER BY day,milestone LIMIT 1 FOR UPDATE","uid",(long)p.Entity.UID))
                    using(var r=q.ExecuteReader()){if(!r.Read())return;day=r.GetString(0);milestone=r.GetString(1);runeType=checked((uint)r.GetInt32(2));}
                    if(runeType!=0 && runeType!=RuneType)throw new InvalidOperationException("Arena rune entitlement invalid.");
                    if(p.Inventory.Count+(runeType==0?1:2)>40)return;
                    if(runeType!=0)rune=Q.New(runeType,true);
                    using(var q=Q.Command(c,t,"SELECT count(*) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0","uid",(long)p.Entity.UID))
                        if(Convert.ToInt64(q.ExecuteScalar())!=p.Inventory.Count)throw new InvalidOperationException("Arena reward inventory requires reload.");
                    foreach(string table in new[]{"arena","teamarena"}) {
                        using(var q=Q.Command(c,t,"SELECT COALESCE(currenthonor,0),COALESCE(historyhonor,0) FROM "+table+" WHERE entityid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                        using(var r=q.ExecuteReader())if(!r.Read() || Convert.ToUInt32(r[0])!=before || Convert.ToUInt32(r[1])!=history || r.Read())throw new InvalidOperationException("Arena Honor mirror requires reload.");
                        if(Q.Execute(c,t,"UPDATE "+table+" SET currenthonor=@current,historyhonor=@history WHERE entityid=@uid",
                            "current",(long)before+500,"history",(long)history+500,"uid",(long)p.Entity.UID)!=1)throw new InvalidOperationException("Arena Honor was not saved.");
                    }
                    Q.Insert(c,t,p.Entity.UID,item);if(rune!=null)Q.Insert(c,t,p.Entity.UID,rune);
                    if(Q.Execute(c,t,team?
                        "UPDATE revora_team_arena_daily_rewards SET paid_at=@now,item_uid=@item,rune_uid=@rune WHERE uid=@uid AND day=@day AND milestone=@milestone AND paid_at IS NULL":
                        "UPDATE revora_arena_daily_rewards SET paid_at=@now,item_uid=@item,rune_uid=@rune WHERE uid=@uid AND day=@day AND milestone=@milestone AND paid_at IS NULL",
                        "now",ArenaMatchLedger.Clock(),"item",(long)item.UID,"rune",rune==null?(object)DBNull.Value:(long)rune.UID,"uid",(long)p.Entity.UID,"day",day,"milestone",milestone)!=1)throw new InvalidOperationException("Arena reward receipt changed.");
                    ProgressionEvidence.Complete(c,t,p.Entity.UID,10320,team?"honor-team-daily":"honor-solo-daily",day+":"+milestone,1);
                    if(!Ready(p) || p.CurrentHonor!=before || p.HistoryHonor!=history)throw new InvalidOperationException("Arena reward owner changed.");t.Commit();
                }}
                p.CurrentHonor=before+500;p.HistoryHonor=history+500;Q.Install(p,new uint[0],rune==null?new[]{item}:new[]{item,rune});p.ArenaStatistic.Send(p);p.TeamArenaStatistic.Send(p);Q.Refresh(p);
                p.Send((team?"Your Team Arena reward: ":"Your Arena reward: ")+"500 Honor Points, an EXP Ball"+(rune==null?".":" and a bound Tortoise Breaker rune."));
            }catch(Exception ex){ArenaCacheRecovery.Fail(p,"arena-daily-reward",ex);}
        }
    }
}
