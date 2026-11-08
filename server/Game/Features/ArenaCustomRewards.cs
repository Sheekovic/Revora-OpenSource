using System;
using System.Threading;
using System.Linq;
using Revora.Client;
using Npgsql;
using Revora.Database;
using Q=Revora.Game.Features.QuestInventory;
using Group=Revora.Game.Qualifier.QualifierList.QualifierGroup;
namespace Revora.Game.Features
{
    // Inactive until the owned coordinator replaces legacy custom reward writers.
    internal static class ArenaCustomRewards
    {
        internal static void Initialize() {
            foreach(uint id in new uint[]{3009002,3004243,3004244,3004245})
                if(!ConquerItemInformation.BaseInformations.ContainsKey(id))throw new InvalidOperationException("Arena custom prize catalog missing.");
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_arena_custom_rewards(
uid bigint NOT NULL,day varchar(8) NOT NULL,milestone varchar(12) NOT NULL CHECK(milestone IN('wins100','losses50')),
source_run uuid NOT NULL REFERENCES revora_arena_matches(id),eligible boolean NOT NULL,recipient_vip integer NOT NULL CHECK(recipient_vip BETWEEN 0 AND 255),
minimum_vip integer NOT NULL CHECK(minimum_vip BETWEEN 0 AND 255),policy_enabled boolean NOT NULL,
honor integer NOT NULL CHECK(honor=500),cp bigint NOT NULL CHECK(cp BETWEEN 0 AND 4294967295),
silver numeric(20,0) NOT NULL CHECK(silver BETWEEN 0 AND 18446744073709551615),rvp bigint NOT NULL CHECK(rvp BETWEEN 0 AND 4294967295),
item_types bigint[] NOT NULL,recorded_at timestamptz NOT NULL,paid_at timestamptz,item_uids bigint[],
CHECK((paid_at IS NULL AND item_uids IS NULL) OR (paid_at IS NOT NULL AND item_uids IS NOT NULL)),
PRIMARY KEY(uid,day,milestone),CHECK(eligible=(policy_enabled AND recipient_vip>=minimum_vip)));");}
        }
        internal static void Record(NpgsqlConnection c,NpgsqlTransaction t,Group group) {
            if(group==null || !Monitor.IsEntered(group.ParticipationSync) || t==null || t.Connection!=c || !group.Done || !group.OwnsImportedMap)
                throw new InvalidOperationException("Arena custom reward requires the owned result transaction.");
            // Legacy inactive callers without captured intents retain direct policy capture.
            uint cp=Config.ArenaHundredWinsCp,rvp=Config.ArenaHundredWinsRevoraPoints;ulong silver=Config.ArenaHundredWinsGold;
            byte minimum=Config.ArenaHundredWinsMinVip;bool lossesEnabled=Config.ArenaFiftyLossesPenaltyEnabled;
            foreach(var p in new[]{group.Player1,group.Player2}) {
                if(ArenaActors.IsBot(p))continue;
                if(!Q.Player(p,false) || !Monitor.IsEntered(p.ItemSyncRoot))throw new InvalidOperationException("Arena custom reward owner lock missing.");
                var captured=ArenaResultIntents.SnapshotFor(c,t,group.ParticipationKey,p.Entity.UID);
                uint awardCp=captured==null?cp:captured.Cp,awardRvp=captured==null?rvp:captured.Rvp;ulong awardSilver=captured==null?silver:captured.Silver;
                byte awardMinimum=captured==null?minimum:captured.Minimum;bool awardLosses=captured==null?lossesEnabled:captured.LossesEnabled;
                string day;
                using(var q=Q.Command(c,t,@"SELECT m.day FROM revora_arena_matches m JOIN revora_arena_result_statistics s ON s.run=m.id
WHERE m.id=@run AND m.state='finished' AND s.uid=@uid","run",group.ParticipationKey,"uid",(long)p.Entity.UID)) {
                    day=q.ExecuteScalar() as string;if(day==null)throw new InvalidOperationException("Arena custom result statistics missing.");
                }
                long wins,losses;using(var q=Q.Command(c,t,@"SELECT count(*) FILTER(WHERE s.won),count(*) FILTER(WHERE NOT s.won)
FROM revora_arena_result_statistics s JOIN revora_arena_matches m ON m.id=s.run
WHERE s.uid=@uid AND m.state='finished' AND m.day=@day","uid",(long)p.Entity.UID,"day",day))
                using(var r=q.ExecuteReader()){r.Read();wins=r.GetInt64(0);losses=r.GetInt64(1);}
                foreach(string milestone in new[]{"wins100","losses50"}) {
                    bool winning=milestone=="wins100";if(winning?wins!=100:losses!=50)continue;
                    byte currentVip=p.Entity.VIPLevel,vip=captured==null?currentVip:captured.Vip;
                    using(var q=Q.Command(c,t,"SELECT COALESCE(viplevel,0) FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID)) {
                        object saved=q.ExecuteScalar();if(saved==null || Convert.ToByte(saved)!=currentVip)throw new InvalidOperationException("Arena reward VIP requires reload.");
                    }
                    bool policy=winning || awardLosses,eligible=policy && vip>=awardMinimum;
                    long[] items=winning?new long[]{3009002,3004243,3004243,3004244,3004244,3004245}:new long[0];
                    Q.Execute(c,t,@"INSERT INTO revora_arena_custom_rewards(uid,day,milestone,source_run,eligible,recipient_vip,minimum_vip,policy_enabled,honor,cp,silver,rvp,item_types,recorded_at)
VALUES(@uid,@day,@milestone,@run,@eligible,@vip,@minimum,@policy,500,@cp,@silver,@rvp,@items,@now)
ON CONFLICT(uid,day,milestone) DO NOTHING","uid",(long)p.Entity.UID,"day",day,"milestone",milestone,"run",group.ParticipationKey,
                        "eligible",eligible,"vip",(int)vip,"minimum",(int)awardMinimum,"policy",policy,"cp",winning?(long)awardCp:0L,
                        "silver",winning?(decimal)awardSilver:0m,"rvp",winning?(long)awardRvp:0L,"items",items,"now",ArenaMatchLedger.Clock());
                    // Existing frozen decisions are immutable, including a prior ineligible decision.
                    using(var q=Q.Command(c,t,"SELECT count(*) FROM revora_arena_custom_rewards WHERE uid=@uid AND day=@day AND milestone=@milestone",
                        "uid",(long)p.Entity.UID,"day",day,"milestone",milestone))if(Convert.ToInt64(q.ExecuteScalar())!=1)throw new InvalidOperationException("Arena custom entitlement was not saved.");
                    if(!Q.Player(p,false) || p.Entity.VIPLevel!=currentVip)throw new InvalidOperationException("Arena custom recipient changed.");
                }
            }
        }
        private static bool Ready(GameState p){return Q.Player(p) && p.ArenaStatistic!=null && p.TeamArenaStatistic!=null
            && p.ArenaStatistic.EntityID==p.Entity.UID && p.TeamArenaStatistic.EntityID==p.Entity.UID
            && p.ArenaStatistic.CurrentHonor==p.TeamArenaStatistic.CurrentHonor && p.ArenaStatistic.HistoryHonor==p.TeamArenaStatistic.HistoryHonor
            && p.Trade!=null && !p.Trade.InTrade && p.Booth==null;}
        internal static void Pay(GameState p) {
            if(!Ready(p))return;
            lock(p.ItemSyncRoot)try {
                if(!Ready(p))return;
                uint honor=p.CurrentHonor,history=p.HistoryHonor,cp=p.Entity.ConquerPoints,rvp=p.Entity.RevoraPoint;ulong silver=p.Entity.Money;
                uint nextCp,nextRvp;ulong nextSilver;Revora.Network.GamePackets.ConquerItem[] items;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    SkillTable.LockOwner(c,t,p);string day,milestone;long giftCp,giftRvp;decimal giftSilver;long[] types;
                    using(var q=Q.Command(c,t,@"SELECT day,milestone,cp,silver,rvp,item_types FROM revora_arena_custom_rewards
WHERE uid=@uid AND eligible AND paid_at IS NULL ORDER BY day,milestone LIMIT 1 FOR UPDATE","uid",(long)p.Entity.UID))
                    using(var r=q.ExecuteReader()){if(!r.Read())return;day=r.GetString(0);milestone=r.GetString(1);giftCp=r.GetInt64(2);giftSilver=r.GetDecimal(3);giftRvp=r.GetInt64(4);types=(long[])r[5];}
                    long[] expected=milestone=="wins100"?new long[]{3009002,3004243,3004243,3004244,3004244,3004245}:new long[0];
                    if(!types.SequenceEqual(expected))throw new InvalidOperationException("Arena custom reward contents changed.");
                    if(p.Inventory.Count+types.Length>40 || honor>uint.MaxValue-500 || history>uint.MaxValue-500
                        || (decimal)cp+giftCp>uint.MaxValue || (decimal)rvp+giftRvp>uint.MaxValue || (decimal)silver+giftSilver>long.MaxValue)return;
                    nextCp=checked(cp+(uint)giftCp);nextRvp=checked(rvp+(uint)giftRvp);nextSilver=checked(silver+(ulong)giftSilver);
                    using(var q=Q.Command(c,t,"SELECT count(*) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0","uid",(long)p.Entity.UID))
                        if(Convert.ToInt64(q.ExecuteScalar())!=p.Inventory.Count)throw new InvalidOperationException("Arena custom bag requires reload.");
                    foreach(string table in new[]{"arena","teamarena"}) {
                        using(var q=Q.Command(c,t,"SELECT COALESCE(currenthonor,0),COALESCE(historyhonor,0) FROM "+table+" WHERE entityid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                        using(var r=q.ExecuteReader())if(!r.Read() || Convert.ToUInt32(r[0])!=honor || Convert.ToUInt32(r[1])!=history || r.Read())throw new InvalidOperationException("Arena custom Honor mirror changed.");
                        if(Q.Execute(c,t,"UPDATE "+table+" SET currenthonor=@honor,historyhonor=@history WHERE entityid=@uid",
                            "honor",(long)honor+500,"history",(long)history+500,"uid",(long)p.Entity.UID)!=1)throw new InvalidOperationException("Arena custom Honor not saved.");
                    }
                    if(Q.Execute(c,t,@"UPDATE entities SET conquerpoints=@cp,revorapoint=@rvp WHERE uid=@uid AND COALESCE(conquerpoints,0)=@oldcp AND COALESCE(revorapoint,0)=@oldrvp",
                        "cp",(long)nextCp,"rvp",(long)nextRvp,"uid",(long)p.Entity.UID,"oldcp",(long)cp,"oldrvp",(long)rvp)!=1)throw new InvalidOperationException("Arena custom purse changed.");
                    SilverBalance.Persist(c,t,p,nextSilver);items=types.Select(id=>Q.New((uint)id,false)).ToArray();foreach(var item in items)Q.Insert(c,t,p.Entity.UID,item);
                    if(Q.Execute(c,t,@"UPDATE revora_arena_custom_rewards SET paid_at=@now,item_uids=@items
WHERE uid=@uid AND day=@day AND milestone=@milestone AND eligible AND paid_at IS NULL","now",ArenaMatchLedger.Clock(),"items",items.Select(i=>(long)i.UID).ToArray(),
                        "uid",(long)p.Entity.UID,"day",day,"milestone",milestone)!=1)throw new InvalidOperationException("Arena custom payment receipt changed.");
                    ProgressionEvidence.Complete(c,t,p.Entity.UID,10320,"honor-custom-arena",day+":"+milestone,1);
                    if(!Ready(p) || p.CurrentHonor!=honor || p.HistoryHonor!=history || p.Entity.ConquerPoints!=cp || p.Entity.RevoraPoint!=rvp || p.Entity.Money!=silver)
                        throw new InvalidOperationException("Arena custom payout context changed.");t.Commit();
                }}
                p.CurrentHonor=honor+500;p.HistoryHonor=history+500;p.Entity.ApplyCommittedConquerPoints(nextCp);p.Entity.ApplyCommittedRevoraPoints(nextRvp);
                SilverBalance.ApplyCommitted(p,nextSilver);Q.Install(p,new uint[0],items);p.ArenaStatistic.Send(p);p.TeamArenaStatistic.Send(p);Q.Refresh(p);
                p.Send("Your Arena prize has arrived: 500 Honor Points and your earned rewards.");
            }catch(Exception ex){ArenaCacheRecovery.Fail(p,"arena-custom-reward",ex);}
        }

    }
}
