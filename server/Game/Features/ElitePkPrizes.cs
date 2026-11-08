using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    // Earned placements pay contents directly; owned legacy outer packs remain independent.
    internal static class ElitePkPrizes
    {
        private sealed class Prize {
            internal string Run;internal int Signup,Place,Bracket;internal uint Type;
            internal DateTime Earned,Expires,Offered;internal byte Level;internal bool Valuable;
        }
        private static readonly ConditionalWeakTable<GameState,Prize> offers=new ConditionalWeakTable<GameState,Prize>();
        internal static void Initialize(NpgsqlConnection c,NpgsqlTransaction t) {
            Q.Execute(c,t,@"CREATE TABLE IF NOT EXISTS revora_epk_prize_claims(
run varchar(32) NOT NULL,uid bigint NOT NULL,pack_type integer NOT NULL,signup_level integer NOT NULL,
claim_level integer NOT NULL,place integer NOT NULL,experience bigint NOT NULL CHECK(experience>=0),
cp bigint NOT NULL CHECK(cp>=0),rewards bigint[] NOT NULL,claimed_at timestamptz NOT NULL,
PRIMARY KEY(run,uid),FOREIGN KEY(run,uid) REFERENCES revora_epk_placements(run,uid));");
        }
        private static bool Near(GameState p) {
            return Q.Near(p,ElitePkEvent.Envoy,1002) && CharacterProgression.Managed(p) && p.Spells!=null;
        }
        private static Prize Read(NpgsqlConnection c,NpgsqlTransaction t,GameState p,DateTime now,bool locked) {
            using(var q=Q.Command(c,t,@"SELECT p.run,p.signup_level,p.place,p.bracket,p.pack_type,p.earned_at,p.expires_at,r.valuable_reward
FROM revora_epk_placements p JOIN revora_epk_runs r ON r.run=p.run
WHERE p.uid=@uid AND r.state='complete' AND p.earned_at<=@now AND p.expires_at>@now
AND NOT EXISTS(SELECT 1 FROM revora_epk_prize_claims c WHERE c.run=p.run AND c.uid=p.uid)
ORDER BY p.expires_at,p.run LIMIT 1"+(locked?" FOR UPDATE OF p":""),"uid",(long)p.Entity.UID,"now",now))
            using(var r=q.ExecuteReader()) {
                if(!r.Read())return null;
                var prize=new Prize{Run=r.GetString(0),Signup=r.GetInt32(1),Place=r.GetInt32(2),Bracket=r.GetInt32(3),Type=(uint)r.GetInt32(4),
                    Earned=r.GetFieldValue<DateTimeOffset>(5).UtcDateTime,Expires=r.GetFieldValue<DateTimeOffset>(6).UtcDateTime,Valuable=r.GetBoolean(7)};
                if(prize.Signup<1 || prize.Signup>byte.MaxValue || ElitePkEvent.Bracket(prize.Signup)!=prize.Bracket
                    || prize.Type!=720714+prize.Bracket*4+(prize.Place==1?3:prize.Place==2?2:prize.Place==3?1:0))
                    throw new InvalidOperationException("Elite prize placement is inconsistent.");
                return prize;
            }
        }
        private static ulong Experience(GameState p,Prize prize) {
            if(p.Entity.Level!=prize.Signup || p.Entity.Level>=Config.LevelCap)return 0;
            ulong curve=DataHolder.LevelExperience(p.Entity.Level);
            return prize.Place==1?curve:prize.Place==2?curve/2:prize.Place==3?curve/10*3:curve/100*15;
        }
        private static uint Cps(Prize prize) { return prize.Bracket==3?(prize.Place>=4?3000000u:5000000u):0; }
        private static string Claim(GameState p,Func<DateTime> clock) {
            DateTime now=clock();
            Prize offered;offers.TryGetValue(p,out offered);offers.Remove(p);
            if(offered==null || now<offered.Offered || now>=offered.Offered.AddMinutes(2) || p.Entity.Level!=offered.Level)
                return "Let me check your prize again.";
            ExperienceRewardPlan plan=null;ExperienceRewards.Effects effects=null;ConquerItem[] rewards;uint nextCp;ulong autoHunt=p.Entity.AutoHuntEXP;
            var profile=CharacterProgression.Capture(p);
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                    if(q.ExecuteScalar()==null)throw new InvalidOperationException("Elite prize owner is missing.");
                var prize=Read(c,t,p,now,true);
                if(prize==null || prize.Run!=offered.Run || prize.Type!=offered.Type || prize.Signup!=offered.Signup
                    || prize.Valuable!=offered.Valuable || prize.Place!=offered.Place || prize.Earned!=offered.Earned || prize.Expires!=offered.Expires)
                    return "No unclaimed prize awaits you. Prizes close when next week's bracket begins.";
                var contents=ElitePkPacks.Rewards(prize.Type,false);
                int slots=contents.Length+(prize.Valuable?1:0);
                if(p.Inventory.Count+slots>40)return "Make "+slots+" spaces in your bag, then return for your prize.";
                ulong sum=(ulong)p.Entity.ConquerPoints+Cps(prize);if(sum>uint.MaxValue)return "Spend some CPs before collecting your prize.";
                nextCp=(uint)sum;ulong award=Experience(p,prize);
                if(award>0 && (p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Cursed) || Config.TrainingGroundBlocksExperience && p.Map.BaseID==1039))
                    return "Return when you can receive your experience.";
                if(award>0){plan=new ExperienceRewardPlan(profile,award,Config.LevelCap,Config.AttributePointsPerLevel,ReincarnationRecovery.Snapshot(p));
                    autoHunt=p.Entity.InAutoHunt?checked(autoHunt+award/Math.Max(1u,Config.AutoHuntExperienceDivisor)):autoHunt;}
                CharacterProgression.Persist(c,t,p,plan==null?profile:plan.After);
                if(Q.Execute(c,t,"UPDATE entities SET conquerpoints=@next WHERE uid=@uid AND conquerpoints=@old",
                    "next",(long)nextCp,"uid",(long)p.Entity.UID,"old",(long)p.Entity.ConquerPoints)!=1)throw new InvalidOperationException("Elite prize balance changed.");
                var ids=new List<uint>();
                using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid","uid",(long)p.Entity.UID))
                using(var r=q.ExecuteReader())while(r.Read()){if(ids.Count>=40)throw new InvalidOperationException("Elite prize bag is over capacity.");ids.Add(checked((uint)Convert.ToInt64(r[0])));}
                if(!ids.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("Elite prize bag changed.");
                rewards=contents.Select(r=>Q.New(r.Item1,r.Item2)).ToArray();
                if(prize.Valuable)rewards=rewards.Concat(new[]{QuestSoulReward.Create()}).ToArray();
                foreach(var reward in rewards)
                    if(Q.Execute(c,t,@"INSERT INTO items(uid,id,entityid,position,warehouse,stacksize,bound,durability,maximdurability)
VALUES(@item,@type,@uid,0,0,1,@bound,@dur,@max)","item",(long)reward.UID,"type",(int)reward.ID,"uid",(long)p.Entity.UID,
                        "bound",reward.Bound?1:0,"dur",(int)reward.Durability,"max",(int)reward.MaximDurability)!=1)throw new InvalidOperationException("Elite prize item was not saved.");
                if(plan!=null){effects=ExperienceRewards.Persist(c,t,p,plan);ReincarnationRecovery.Consume(c,t,p,plan.Restored);}
                if(Q.Execute(c,t,@"INSERT INTO revora_epk_prize_claims(run,uid,pack_type,signup_level,claim_level,place,experience,cp,rewards,claimed_at)
VALUES(@run,@uid,@type,@signup,@level,@place,@exp,@cp,@items,@now)","run",prize.Run,"uid",(long)p.Entity.UID,"type",(int)prize.Type,
                    "signup",prize.Signup,"level",(int)p.Entity.Level,"place",prize.Place,"exp",checked((long)award),"cp",(long)Cps(prize),
                    "items",rewards.Select(i=>(long)i.UID).ToArray(),"now",now)!=1)throw new InvalidOperationException("Elite prize receipt was not saved.");
                if(!Near(p) || clock()>=prize.Expires || clock()>=offered.Offered.AddMinutes(2))throw new InvalidOperationException("Elite prize session or deadline changed.");t.Commit();
            }}
            Q.Install(p,new uint[0],rewards);p.Entity.ApplyCommittedConquerPoints(nextCp);
            if(plan!=null){ReincarnationRecovery.Committed(p,plan.Restored);MentorExperience.Apply(p,effects.MentorCredit);p.ApplyExperienceReward(plan,effects,autoHunt,true);}
            else CharacterProgression.Committed(p,profile);
            return "A fine showing in the arena. Your prize is yours!";
        }
        internal static bool HandleNpc(GameState p,NpcRequest request,Func<DateTime> clock) {
            if(p==null || request==null || p.ActiveNpc!=ElitePkEvent.Envoy)return false;
            if(request.OptionID!=6 && request.OptionID!=7){offers.Remove(p);return false;}
            if(!Q.Player(p))return true;
            lock(p.ItemSyncRoot)lock(MentorExperience.Sync)try {
                if(!Near(p) || request.NpcID!=0 && request.NpcID!=ElitePkEvent.Envoy)return true;
                var d=new Npcs(p);DateTime now=clock();
                if(request.OptionID==7)d.Text(Claim(p,clock));
                else {
                    offers.Remove(p);Prize prize;
                    using(var c=DataHolder.MySqlConnection){c.Open();prize=Read(c,null,p,now,false);}
                    if(prize==null)d.Text("No unclaimed prize awaits you. Collect earned prizes before next week's bracket begins.");
                    else {
                        prize.Level=p.Entity.Level;prize.Offered=now;offers.Add(p,prize);
                        string rank=prize.Place==1?"champion":prize.Place==2?"runner-up":prize.Place==3?"third-place":"top-eight";
                        d.Text("Your "+rank+" prize awaits. Leave "+(ElitePkPacks.Rewards(prize.Type,false).Length+(prize.Valuable?1:0))+" bag spaces. "+(prize.Valuable?"A bound P7 soul is included. ":"")
                            +(Cps(prize)>0?Cps(prize).ToString("N0")+" CPs are included. ":"")
                            +(prize.Signup!=p.Entity.Level?"Your level changed; the EXP bonus is lost.":Experience(p,prize)==0?"You have reached the EXP limit.":"The prize includes your EXP bonus."));
                        d.Option("Collect my prize.",7);
                    }
                }
                d.Option("Farewell.",255);d.Send();
            }catch(Exception ex){Q.Fail(p,"elite-prize-claim",ex);}return true;
        }
    }
}
