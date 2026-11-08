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
    internal static class ExorcismQuest
    {
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        private static bool enabled;
        internal sealed class Daily {
            internal Guid Id;internal string Day,Completed,State;internal int Grade;internal bool Legacy,RuneReward;
            internal uint Cp,Rvp,Chi,Token,Fee;internal ushort BaseStudy;
            internal ushort Study {get{return checked((ushort)(BaseStudy*(1<<(Grade-1))));}}
        }
        private sealed class Offer {internal Daily Run;internal DateTime End;}
        private static readonly ConditionalWeakTable<GameState,Offer> offers=new ConditionalWeakTable<GameState,Offer>();
        internal static string Day(DateTime now){return MagnoliaQuest.Day(now);}
        internal static bool Ready(GameState p){return enabled && Q.Player(p) && CharacterProgression.Managed(p) && p.Spells!=null && p.Entity.SubClasses!=null && p.Trade!=null && !p.Trade.InTrade && p.Booth==null;}
        internal static bool Near(GameState p){return Ready(p) && Q.Near(p,12333,1036) && p.Map.BaseID==1036;}
        internal static void Initialize() {
            ConquerItemBaseInformation rune;
            if(!ConquerItemInformation.BaseInformations.TryGetValue(4030101,out rune) || rune.Class!=0)
                throw new InvalidOperationException("Exorcism yellow rune catalog missing or class restricted.");
            foreach(uint id in new[]{Config.DailyExorcismToken,Config.DailyQuestChiToken})if(!ConquerItemInformation.BaseInformations.ContainsKey(id))throw new InvalidOperationException("Exorcism daily catalog missing.");
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_exorcism_daily(
id uuid PRIMARY KEY,uid bigint NOT NULL,day varchar(8) NOT NULL,completed_day varchar(8) NOT NULL DEFAULT '',
state varchar(12) NOT NULL CHECK(state IN('accepted','victory','claimed')),grade integer NOT NULL CHECK(grade BETWEEN 1 AND 3),legacy boolean NOT NULL DEFAULT false,
cp bigint NOT NULL,rvp bigint NOT NULL,chi bigint NOT NULL,token bigint NOT NULL,fee bigint NOT NULL,base_study integer NOT NULL,
source_run uuid,paid boolean NOT NULL DEFAULT false,paid_cp bigint NOT NULL DEFAULT 0,reward_uids bigint[] NOT NULL DEFAULT '{}',experience bigint NOT NULL DEFAULT 0,
created_at timestamptz NOT NULL DEFAULT now(),victory_at timestamptz,claimed_at timestamptz,UNIQUE(uid,day));
ALTER TABLE revora_exorcism_daily ADD COLUMN IF NOT EXISTS valuable_reward boolean NOT NULL DEFAULT false;
ALTER TABLE revora_exorcism_daily ADD COLUMN IF NOT EXISTS rune_reward boolean NOT NULL DEFAULT false;
CREATE UNIQUE INDEX IF NOT EXISTS revora_exorcism_daily_active ON revora_exorcism_daily(uid) WHERE completed_day='';");}
            ExorcismWorld.Initialize();enabled=true;
        }
        internal static void Owner(NpgsqlConnection c,NpgsqlTransaction t,GameState p) {
            using(var q=Q.Command(c,t,"SELECT level FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID)) {
                object level=q.ExecuteScalar();if(level==null || Convert.ToInt32(level)<Config.DailyExorcismMinLevel)throw new InvalidOperationException("Exorcism eligibility changed.");
            }
        }
        internal static Daily Read(NpgsqlConnection c,NpgsqlTransaction t,GameState p) {
            using(var q=Q.Command(c,t,@"SELECT id,day,completed_day,state,grade,legacy,cp,rvp,chi,token,fee,base_study,valuable_reward,rune_reward
FROM revora_exorcism_daily WHERE uid=@uid ORDER BY day DESC LIMIT 1","uid",(long)p.Entity.UID))
            using(var r=q.ExecuteReader())if(r.Read())return new Daily{Id=r.GetGuid(0),Day=r.GetString(1),Completed=r.GetString(2),State=r.GetString(3),Grade=r.GetInt32(4),Legacy=r.GetBoolean(5),
                Cp=Convert.ToUInt32(r[6]),Rvp=Convert.ToUInt32(r[7]),Chi=Convert.ToUInt32(r[8]),Token=Convert.ToUInt32(r[9]),Fee=Convert.ToUInt32(r[10]),BaseStudy=Convert.ToUInt16(r[11]),RuneReward=r.GetBoolean(13)};return null;
        }
        private static Daily New(DateTime now,int grade){return new Daily{RuneReward=true,Id=Guid.NewGuid(),Day=Day(now),Completed="",State="accepted",Grade=grade,Cp=Config.DailyQuestCp,Rvp=Config.DailyQuestRevoraPoints,
            Chi=Config.DailyQuestChiToken,Token=Config.DailyExorcismToken,Fee=Config.DailyExorcismSkipCp,BaseStudy=Config.DailyExorcismStudy};}
        private static void Insert(NpgsqlConnection c,NpgsqlTransaction t,GameState p,Daily r) {
            if(r.Legacy)r.RuneReward=false;
            if(Q.Execute(c,t,@"INSERT INTO revora_exorcism_daily(id,uid,day,completed_day,state,grade,legacy,cp,rvp,chi,token,fee,base_study,valuable_reward,rune_reward)
VALUES(@id,@uid,@day,@done,@state,@grade,@legacy,@cp,@rvp,@chi,@token,@fee,@study,@valuable,@rune)","id",r.Id,"uid",(long)p.Entity.UID,"day",r.Day,"done",r.Completed,"state",r.State,
                "grade",r.Grade,"legacy",r.Legacy,"cp",(long)r.Cp,"rvp",(long)r.Rvp,"chi",(long)r.Chi,"token",(long)r.Token,"fee",(long)r.Fee,"study",(int)r.BaseStudy,"valuable",false,"rune",r.RuneReward)!=1)
                throw new InvalidOperationException("Exorcism daily record was not saved.");
        }
        private static Daily Import(NpgsqlConnection c,NpgsqlTransaction t,GameState p,DateTime now) {
            var r=Read(c,t,p);if(r!=null)return r;bool done=false;int grade=0;
            using(var q=Q.Command(c,t,@"SELECT e.lastresettime,d.thirllingspook,d.thirlspooklevel FROM entities e LEFT JOIN dialyquests d ON d.entityid=e.uid WHERE e.uid=@uid","uid",(long)p.Entity.UID))
            using(var d=q.ExecuteReader())if(d.Read() && !d.IsDBNull(0) && DateTime.FromBinary(Convert.ToInt64(d[0])).Date==now.ToLocalTime().Date){done=!d.IsDBNull(1) && Convert.ToBoolean(d[1]);grade=d.IsDBNull(2)?0:Convert.ToInt32(d[2]);}
            if(!done && (grade<1 || grade>3))return null;
            r=New(now,grade>=1 && grade<=3?grade:1);r.Legacy=true;
            // The old completion/victory flags cannot prove an unpaid reward. Do not pay them again.
            if(done){r.State="claimed";r.Completed=Day(now);}Insert(c,t,p,r);return r;
        }
        internal static bool Available(Daily r,DateTime now){return r==null || r.Completed!="" && string.CompareOrdinal(r.Completed,Day(now))<0;}
        internal static bool Pending(Daily r,DateTime now){return r!=null && r.Completed=="" && r.State=="accepted" && string.CompareOrdinal(r.Day,Day(now))<=0;}
        internal static void Projection(GameState p,Daily r,DateTime now) {
            p.Entity.ThirllingSpook=r!=null && r.Completed!="" && string.CompareOrdinal(r.Completed,Day(now))>=0;
            p.Entity.ThirllingSpookPrice=r!=null && r.State=="victory" && r.Completed=="";
            p.Entity.ThirlSpookLevel=r!=null && r.Completed==""?(byte)r.Grade:(byte)0;
        }
        internal static void Refresh(GameState p) {
            if(!enabled || !Q.Player(p,false) || p.Entity.Level<Config.DailyExorcismMinLevel)return;
            lock(p.ItemSyncRoot)try{using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){Owner(c,t,p);var r=Import(c,t,p,Clock());t.Commit();Projection(p,r,Clock());}}}
            catch(Exception ex){Q.Fail(p,"exorcism-refresh",ex);}
        }
        private static string Accept(GameState p,int grade,DateTime now) {
            Daily r;using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);r=Import(c,t,p,now);
                if(Available(r,now)){if(ExorcismWorld.HasAdmission(c,t,p.Entity.UID,now))return "Leave your current hunt before accepting another commission.";r=New(now,grade);Insert(c,t,p,r);}
                else if(Pending(r,now) && r.Grade!=grade) {
                    if(ExorcismWorld.HasAdmission(c,t,p.Entity.UID,now))return "Finish or leave your current hunt before changing its difficulty.";
                    if(Q.Execute(c,t,"UPDATE revora_exorcism_daily SET grade=@grade WHERE id=@id AND state='accepted' AND completed_day=''","grade",grade,"id",r.Id)!=1)throw new InvalidOperationException("Exorcism difficulty changed.");r.Grade=grade;
                }
                if(!Near(p))throw new InvalidOperationException("Exorcism acceptance session changed.");t.Commit();
            }}Projection(p,r,now);return r.State=="victory"?"Your victory is recorded. Claim your reward.":r.Completed!=""?"You have earned today's reward. You may still help your companions.":"The seal is ready. Gather your companions, then enter the burrow."+(r.RuneReward?" A bound Tortoise Breaker rune awaits beside your hunt rewards.":"");
        }
        private static void Show(GameState p,Npcs d,DateTime now) {
            Daily r;using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){Owner(c,t,p);r=Import(c,t,p,now);t.Commit();}}
            Projection(p,r,now);d.Text(r!=null && r.State=="victory"?"The Spook is vanquished. Your reward awaits.":"Thrilling Spooks stir beneath the earth. Choose your foe, gather your companions, and speak to Earth Master inside. You have one hour.");
            if(r!=null && r.State=="victory")d.Option("Claim my reward.",11);
            else if(Available(r,now) || Pending(r,now)){d.Option("Thrilling Spook.",1);d.Option("Thrilling Spook II.",2);d.Option("Thrilling Spook III.",3);d.Option("Finish with CPs.",7);}
            d.Option("Enter or resume the hunt.",12);d.Option("Leave my current hunt.",13);d.Option("Exchange Exorcism Tokens.",6);d.Option("Farewell.",255);
        }
        private static void ReviewPaid(GameState p,Npcs d,DateTime now) {
            offers.Remove(p);Daily r;using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){Owner(c,t,p);r=Import(c,t,p,now);t.Commit();}}
            if(!Available(r,now) && !Pending(r,now)){d.Text("Claim your earned reward, or return tomorrow.");return;}
            if(Available(r,now))r=New(now,3);else r.Grade=3;
            offers.Add(p,new Offer{Run=r,End=now.AddMinutes(2)});d.Text("For "+r.Fee+" CPs, I will seal the strongest Spook for you. You receive three tokens and the daily reward."+(r.RuneReward?" A bound Tortoise Breaker rune is included.":""));d.Option("Pay "+r.Fee+" CPs.",10);d.Option("Keep my CPs.",255);
        }
        private static void Bag(NpgsqlConnection c,NpgsqlTransaction t,GameState p) {
            var ids=new List<uint>();using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid","uid",(long)p.Entity.UID))
            using(var r=q.ExecuteReader())while(r.Read()){if(ids.Count>=40)throw new InvalidOperationException("Exorcism bag exceeds capacity.");ids.Add(Convert.ToUInt32(r[0]));}
            if(!ids.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("Exorcism reward bag requires reload.");
        }
        private static void Gift(NpgsqlConnection c,NpgsqlTransaction t,GameState p,ConquerItem item) {
            if(Q.Execute(c,t,@"INSERT INTO items(uid,id,entityid,position,warehouse,stacksize,bound,durability,maximdurability)
VALUES(@item,@type,@uid,0,0,1,@bound,@dur,@max)","item",(long)item.UID,"type",(int)item.ID,"uid",(long)p.Entity.UID,"bound",item.Bound?1:0,"dur",(int)item.Durability,"max",(int)item.MaximDurability)!=1)
                throw new InvalidOperationException("Exorcism daily gift was not saved.");
        }
        private static string Complete(GameState p,bool paid,DateTime now) {
            Offer offer=null;if(paid){if(!offers.TryGetValue(p,out offer))return "Let us review the price first.";offers.Remove(p);if(now>=offer.End)return "Let us review the price again.";}
            Daily r;ConquerItem[] rewards;uint cp,rvp;ushort study;ulong autoHunt;ExperienceRewardPlan plan;ExperienceRewards.Effects effects;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var saved=Import(c,t,p,now);r=paid?offer.Run:saved;
                if(r==null || string.CompareOrdinal(r.Day,Day(now))>0 || r.Completed!="")return "Return tomorrow for another reward.";
                if(paid) {
                    if(!Pending(saved,now) && !Available(saved,now) || Pending(saved,now) && saved.Id!=r.Id || Available(saved,now) && r.Day!=Day(now))return "Let us review your commission again.";
                    if(ExorcismWorld.HasHunt(c,t,p.Entity.UID,now))return "Finish or leave the hunt before asking me to seal it.";
                }else if(r.State!="victory")return "Defeat your summoned Spook before claiming a reward.";
                if(p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Cursed))return "Return when you can receive experience.";
                uint fee=paid?r.Fee:0;if(p.Entity.ConquerPoints<fee)return "You do not have enough CPs.";
                if(p.Inventory.Count+1+r.Grade+(r.RuneReward?1:0)>40)return "Make room for all your hunt rewards.";
                ulong next=(ulong)p.Entity.ConquerPoints-fee+r.Cp;
                if(next>uint.MaxValue || (ulong)p.Entity.RevoraPoint+r.Rvp>uint.MaxValue || p.Entity.SubClasses.StudyPoints+r.Study>ushort.MaxValue)return "Spend some points before claiming your reward.";
                Bag(c,t,p);if(paid && Available(saved,now))Insert(c,t,p,r);
                cp=(uint)next;rvp=p.Entity.RevoraPoint+r.Rvp;study=(ushort)(p.Entity.SubClasses.StudyPoints+r.Study);
                ulong award=p.Entity.Level>=Config.LevelCap?0:p.ExpBall;
                plan=new ExperienceRewardPlan(CharacterProgression.Capture(p),award,Config.LevelCap,Config.AttributePointsPerLevel,ReincarnationRecovery.Snapshot(p));
                autoHunt=p.Entity.InAutoHunt?checked(p.Entity.AutoHuntEXP+award/Math.Max(1u,Config.AutoHuntExperienceDivisor)):p.Entity.AutoHuntEXP;
                CharacterProgression.Persist(c,t,p,plan.After);
                if(Q.Execute(c,t,@"UPDATE entities SET conquerpoints=@cp,revorapoint=@rvp,studypoints=@study WHERE uid=@uid
AND COALESCE(conquerpoints,0)=@oldcp AND COALESCE(revorapoint,0)=@oldrvp AND COALESCE(studypoints,0)=@oldstudy","cp",(long)cp,"rvp",(long)rvp,"study",(int)study,"uid",(long)p.Entity.UID,
                    "oldcp",(long)p.Entity.ConquerPoints,"oldrvp",(long)p.Entity.RevoraPoint,"oldstudy",(int)p.Entity.SubClasses.StudyPoints)!=1)throw new InvalidOperationException("Exorcism daily balances changed.");
                rewards=new[]{Q.New(r.Chi,false)}.Concat(Enumerable.Range(0,r.Grade).Select(_=>Q.New(r.Token,false))).Concat(r.RuneReward?new[]{Q.New(4030101,true)}:new ConquerItem[0]).ToArray();foreach(var gift in rewards)Gift(c,t,p,gift);
                effects=ExperienceRewards.Persist(c,t,p,plan);ReincarnationRecovery.Consume(c,t,p,plan.Restored);
                if(Q.Execute(c,t,@"UPDATE revora_exorcism_daily SET state='claimed',completed_day=@day,claimed_at=@now,grade=@grade,paid=@paid,paid_cp=@fee,reward_uids=@rewards,experience=@exp
WHERE id=@id AND uid=@uid AND completed_day='' AND state=@state", "day",Day(now),"now",now,"grade",r.Grade,"paid",paid,"fee",(long)fee,"rewards",rewards.Select(i=>(long)i.UID).ToArray(),
                    "exp",checked((long)award),"id",r.Id,"uid",(long)p.Entity.UID,"state",paid?"accepted":"victory")!=1)throw new InvalidOperationException("Exorcism reward claim was not saved.");
                StudyRewardEvidence.Record(c,t,p,r.Study,"exorcism:"+r.Id.ToString("N"));
                if(!Near(p))throw new InvalidOperationException("Exorcism reward session changed.");t.Commit();
            }}Q.Install(p,new uint[0],rewards);p.Entity.ApplyCommittedConquerPoints(cp);p.Entity.ApplyCommittedRevoraPoints(rvp);p.Entity.SubClasses.StudyPoints=study;
            ReincarnationRecovery.Committed(p,plan.Restored);MentorExperience.Apply(p,effects.MentorCredit);p.ApplyExperienceReward(plan,effects,autoHunt,true);
            r.State="claimed";r.Completed=Day(now);Projection(p,r,now);Q.Refresh(p);p.Send(new SubClassShowFull(0){ID=8,Study=study,StudyReceive=r.Study}.ToArray());return "The seal holds. Take your reward, and return tomorrow.";
        }
        internal static bool HandleNpc(GameState p,NpcRequest req) {
            if(p==null || req==null || p.ActiveNpc!=12333)return false;
            if(req.OptionID!=10)offers.Remove(p);
            if(ExorcismRewards.HandleNpc(p,req))return true;
            if(!Ready(p))return true;
            lock(p.ItemSyncRoot)lock(MentorExperience.Sync)try {
                if(!Near(p) || req.NpcID!=0 && req.NpcID!=12333)return true;
                var d=new Npcs(p);d.Avatar(67);DateTime now=Clock();
                if(p.Entity.Level<Config.DailyExorcismMinLevel)d.Text("Return at level "+Config.DailyExorcismMinLevel+" for the hunt.");
                else if(req.OptionID==0 || req.OptionID==4){Show(p,d,now);d.Send();return true;}
                else if(req.OptionID>=1 && req.OptionID<=3)d.Text(Accept(p,req.OptionID,now));
                else if(req.OptionID==7){ReviewPaid(p,d,now);d.Send();return true;}
                else if(req.OptionID==10 || req.OptionID==11)d.Text(Complete(p,req.OptionID==10,now));
                else if(req.OptionID==12)d.Text(ExorcismWorld.Enter(p));
                else if(req.OptionID==13)d.Text(ExorcismWorld.Leave(p));
                else return true;
                d.Option("Enter or resume the hunt.",12);d.Option("Speak of the hunt.",0);d.Option("Farewell.",255);d.Send();
            }catch(Exception ex){var db=ex as PostgresException;if(db!=null)Console.WriteLine("[quest] source=exorcism-daily sqlstate="+db.SqlState);offers.Remove(p);Q.Fail(p,"exorcism-daily",ex);}return true;
        }
    }
}
