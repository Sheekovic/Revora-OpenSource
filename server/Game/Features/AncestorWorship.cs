using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class AncestorWorship
    {
        internal const uint RuneRewardId=4030101;
        internal static Func<DateTime> Clock = () => DateTime.UtcNow;
        private static bool enabled;
        internal static void Initialize()
        {
            if(!ConquerItemInformation.BaseInformations.ContainsKey(Config.DailyQuestChiToken))
                throw new InvalidOperationException("Worship Chi Token is missing.");
            ConquerItemBaseInformation rune;
            if(!ConquerItemInformation.BaseInformations.TryGetValue(RuneRewardId,out rune) || rune.Class!=0)
                throw new InvalidOperationException("Worship yellow rune is missing or class restricted.");
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_worship_claims(
uid bigint NOT NULL,day varchar(8) NOT NULL CHECK(day ~ '^[0-9]{8}$'),legacy boolean NOT NULL,
statue bigint NOT NULL,cp bigint NOT NULL CHECK(cp>=0),rvp bigint NOT NULL CHECK(rvp>=0),experience bigint NOT NULL CHECK(experience>=0),
soul_uid bigint UNIQUE,chi_uid bigint UNIQUE,claimed_at timestamptz NOT NULL,PRIMARY KEY(uid,day),
CONSTRAINT revora_worship_reward_v2 CHECK((legacy AND soul_uid IS NULL AND chi_uid IS NULL) OR (NOT legacy AND chi_uid IS NOT NULL)));
ALTER TABLE revora_worship_claims DROP CONSTRAINT IF EXISTS revora_worship_claims_check;
ALTER TABLE revora_worship_claims DROP CONSTRAINT IF EXISTS revora_worship_reward_v2;
ALTER TABLE revora_worship_claims ADD CONSTRAINT revora_worship_reward_v2 CHECK((legacy AND soul_uid IS NULL AND chi_uid IS NULL) OR (NOT legacy AND chi_uid IS NOT NULL));
ALTER TABLE revora_worship_claims ADD COLUMN IF NOT EXISTS rune_uid bigint UNIQUE CHECK(rune_uid>0)");}
            enabled=true;
        }
        private static bool Ready(GameState p) {return enabled && Q.Player(p) && CharacterProgression.Managed(p) && p.Spells!=null && p.Booth==null && p.Trade!=null && !p.Trade.InTrade;}
        private static bool Near(GameState p) {return Ready(p) && p.ActiveNpc>=98130 && p.ActiveNpc<=98135
            && Q.Near(p,p.ActiveNpc,Config.DailyWorshipMap) && p.Map.BaseID==Config.DailyWorshipMap;}
        private static void LockOwner(NpgsqlConnection c,NpgsqlTransaction t,GameState p)
        {
            using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                if(q.ExecuteScalar()==null)throw new InvalidOperationException("Worship owner is missing.");
        }
        private static string Latest(NpgsqlConnection c,NpgsqlTransaction t,GameState p,DateTime now)
        {
            using(var q=Q.Command(c,t,"SELECT day FROM revora_worship_claims WHERE uid=@uid ORDER BY day DESC LIMIT 1","uid",(long)p.Entity.UID)) {
                var value=q.ExecuteScalar();if(value!=null)return (string)value;
            }
            string day=MagnoliaQuest.Day(now);bool paid=false;
            using(var q=Q.Command(c,t,@"SELECT e.lastresettime,d.horingthe FROM entities e LEFT JOIN dialyquests d ON d.entityid=e.uid WHERE e.uid=@uid","uid",(long)p.Entity.UID))
            using(var r=q.ExecuteReader())if(r.Read() && !r.IsDBNull(0) && !r.IsDBNull(1))
                paid=Convert.ToBoolean(r[1]) && DateTime.FromBinary(Convert.ToInt64(r[0])).ToString("yyyyMMdd",CultureInfo.InvariantCulture)==day;
            if(!paid)return null;
            if(Q.Execute(c,t,@"INSERT INTO revora_worship_claims(uid,day,legacy,statue,cp,rvp,experience,claimed_at)
VALUES(@uid,@day,true,0,0,0,0,@now)","uid",(long)p.Entity.UID,"day",day,"now",now)!=1)
                throw new InvalidOperationException("Legacy worship completion was not imported.");
            return day;
        }
        private static bool Paid(string latest,string today) {return latest!=null && string.CompareOrdinal(latest,today)>=0;}
        internal static void Refresh(GameState p)
        {
            if(!enabled || !Q.Player(p,false))return;
            lock(p.ItemSyncRoot)try {
                DateTime now=Clock();string latest;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    LockOwner(c,t,p);latest=Latest(c,t,p,now);t.Commit();
                }}
                p.Entity.HoringThe=Paid(latest,MagnoliaQuest.Day(now));
            }catch(Exception ex){Q.Fail(p,"worship-refresh",ex);}
        }
        private static string RewardText() {return Config.DailyWorshipCp+" CPs, "+Config.DailyWorshipRevoraPoints+" RVPs, one Chi Token, a bound Tortoise Breaker rune and one EXP Ball's experience";}
        internal static bool HandleEnvoy(GameState p,NpcRequest request)
        {
            if(p==null || request==null || p.ActiveNpc!=DailyQuestEnvoySystem.NpcId || request.OptionID!=1 && request.OptionID!=23)return false;
            if(!Ready(p))return true;
            lock(p.ItemSyncRoot)try {
                if(!Q.Near(p,DailyQuestEnvoySystem.NpcId,1002) || p.Map.BaseID!=1002 || request.NpcID!=0 && request.NpcID!=p.ActiveNpc)return true;
                Refresh(p);if(!Ready(p))return true;
                var d=new Npcs(p);d.Avatar(Config.DailyEnvoyAvatar);
                if(p.Entity.Level<Config.DailyWorshipMinLevel)d.Text("Return at level "+Config.DailyWorshipMinLevel+" to honor the ancestors.");
                else if(p.Entity.HoringThe)d.Text("The ancestors have heard you today. Return tomorrow.");
                else if(request.OptionID==1){p.Entity.Teleport(Config.DailyWorshipMap,Config.DailyWorshipX,Config.DailyWorshipY);return true;}
                else {d.Text("Honor one statue in the Ancestral Hall. Your daily offering earns "+RewardText()+".");d.Option("Take me there.",1);}
                d.Option("Farewell.",255);d.Send();
            }catch(Exception ex){Q.Fail(p,"worship-travel",ex);}return true;
        }
        private static void Bag(NpgsqlConnection c,NpgsqlTransaction t,GameState p)
        {
            var ids=new List<uint>();using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid","uid",(long)p.Entity.UID))
            using(var r=q.ExecuteReader())while(r.Read()){if(ids.Count>=40)throw new InvalidOperationException("Worship bag exceeds capacity.");ids.Add(Convert.ToUInt32(r[0]));}
            if(!ids.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("Worship bag requires reload.");
        }
        private static string Claim(GameState p,DateTime now)
        {
            string day=MagnoliaQuest.Day(now);ConquerItem chi,rune;ExperienceRewardPlan plan;ExperienceRewards.Effects effects;uint cp,rvp;ulong autoHunt;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                LockOwner(c,t,p);string latest=Latest(c,t,p,now);
                if(Paid(latest,day)){t.Commit();p.Entity.HoringThe=true;return "The ancestors have heard you today. Return tomorrow.";}
                if(p.Inventory.Count+2>40)return "Leave two places for your gifts, then return.";
                if((ulong)p.Entity.ConquerPoints+Config.DailyWorshipCp>uint.MaxValue || (ulong)p.Entity.RevoraPoint+Config.DailyWorshipRevoraPoints>uint.MaxValue)
                    return "Spend some of your saved points before collecting your gifts.";
                Bag(c,t,p);cp=p.Entity.ConquerPoints+Config.DailyWorshipCp;rvp=p.Entity.RevoraPoint+Config.DailyWorshipRevoraPoints;
                ulong award=p.Entity.Level>=Config.LevelCap?0:p.ExpBall;
                plan=new ExperienceRewardPlan(CharacterProgression.Capture(p),award,Config.LevelCap,Config.AttributePointsPerLevel,ReincarnationRecovery.Snapshot(p));
                autoHunt=p.Entity.InAutoHunt?checked(p.Entity.AutoHuntEXP+award/Math.Max(1u,Config.AutoHuntExperienceDivisor)):p.Entity.AutoHuntEXP;
                CharacterProgression.Persist(c,t,p,plan.After);
                if(Q.Execute(c,t,@"UPDATE entities SET conquerpoints=@cp,revorapoint=@rvp WHERE uid=@uid
AND COALESCE(conquerpoints,0)=@oldcp AND COALESCE(revorapoint,0)=@oldrvp",
                    "cp",(long)cp,"rvp",(long)rvp,"uid",(long)p.Entity.UID,"oldcp",(long)p.Entity.ConquerPoints,"oldrvp",(long)p.Entity.RevoraPoint)!=1)
                    throw new InvalidOperationException("Worship purse changed.");
                chi=Q.New(Config.DailyQuestChiToken,false);Q.Insert(c,t,p.Entity.UID,chi);
                rune=Q.New(RuneRewardId,true);Q.Insert(c,t,p.Entity.UID,rune);
                effects=ExperienceRewards.Persist(c,t,p,plan);ReincarnationRecovery.Consume(c,t,p,plan.Restored);
                Q.Execute(c,t,"INSERT INTO dialyquests(entityid,horingthe) SELECT @uid,0 WHERE NOT EXISTS(SELECT 1 FROM dialyquests WHERE entityid=@uid)","uid",(long)p.Entity.UID);
                if(Q.Execute(c,t,"UPDATE dialyquests SET horingthe=1 WHERE entityid=@uid","uid",(long)p.Entity.UID)!=1)
                    throw new InvalidOperationException("Worship daily projection was not saved.");
                if(Q.Execute(c,t,@"INSERT INTO revora_worship_claims(uid,day,legacy,statue,cp,rvp,experience,soul_uid,chi_uid,rune_uid,claimed_at)
VALUES(@uid,@day,false,@statue,@cp,@rvp,@exp,@soul,@chi,@rune,@now)","uid",(long)p.Entity.UID,"day",day,"statue",(long)p.ActiveNpc,
                    "cp",(long)Config.DailyWorshipCp,"rvp",(long)Config.DailyWorshipRevoraPoints,"exp",checked((long)award),"soul",DBNull.Value,"chi",(long)chi.UID,"rune",(long)rune.UID,"now",now)!=1)
                    throw new InvalidOperationException("Worship receipt was not saved.");
                if(!Near(p) || p.Entity.Level<Config.DailyWorshipMinLevel)throw new InvalidOperationException("Worship session changed.");
                t.Commit();
            }}
            Q.Install(p,new uint[0],new[]{chi,rune});p.Entity.ApplyCommittedConquerPoints(cp);p.Entity.ApplyCommittedRevoraPoints(rvp);
            ReincarnationRecovery.Committed(p,plan.Restored);MentorExperience.Apply(p,effects.MentorCredit);p.ApplyExperienceReward(plan,effects,autoHunt,true);
            p.Entity.HoringThe=true;Q.Refresh(p);DialyQuestsEffect.Check(p,"Sure");p.Entity.Update(10,"accession",true);
            return "Your offering is received. Take "+RewardText()+", and return tomorrow.";
        }
        internal static bool HandleNpc(GameState p,NpcRequest request)
        {
            if(p==null || request==null || p.ActiveNpc<98130 || p.ActiveNpc>98135)return false;
            if(!Ready(p))return true;
            lock(p.ItemSyncRoot)lock(MentorExperience.Sync)try {
                if(!Near(p) || request.NpcID!=0 && request.NpcID!=p.ActiveNpc)return true;
                if(request.OptionID!=0 && request.OptionID!=1)return true;
                var d=new Npcs(p);d.Avatar(47);
                if(p.Entity.Level<Config.DailyWorshipMinLevel)d.Text("Return at level "+Config.DailyWorshipMinLevel+" to honor the ancestors.");
                else if(request.OptionID==1)d.Text(Claim(p,Clock()));
                else {Refresh(p);if(!Ready(p))return true;d.Text(p.Entity.HoringThe?"The ancestors have heard you today. Return tomorrow.":"Honor the ancestors once each day for "+RewardText()+".");if(!p.Entity.HoringThe)d.Option("Honor the ancestors.",1);}
                d.Option("Farewell.",255);d.Send();
            }catch(Exception ex){Q.Fail(p,"ancestor-worship",ex);}return true;
        }
    }
}
