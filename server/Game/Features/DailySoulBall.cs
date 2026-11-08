using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class DailySoulBall
    {
        internal static Func<DateTime> Clock = () => DateTime.UtcNow;
        private static bool enabled;
        private sealed class Run {
            internal Guid Id; internal string Day, Completed; internal bool Legacy, Paid, RuneReward;
            internal uint Bead, Type, Spirits, Need, Cp, Rvp, Chi, BuyFee, SkipFee, Bless;
            internal ushort Study, SkipStudy;
        }
        private sealed class Offer { internal Run Preview; internal DateTime End; }
        private sealed class Life { internal bool Credited; }
        private sealed class Display { internal string Completed; }
        private static readonly ConditionalWeakTable<GameState,Display> displays = new ConditionalWeakTable<GameState,Display>();
        private static readonly ConditionalWeakTable<GameState,Offer> offers = new ConditionalWeakTable<GameState,Offer>();
        private static readonly ConditionalWeakTable<Entity,Life> lives = new ConditionalWeakTable<Entity,Life>();
        private static uint[] Types { get { return new[]{Config.DailySpiritBeadNormal,Config.DailySpiritBeadRefined,Config.DailySpiritBeadUnique,Config.DailySpiritBeadElite,Config.DailySpiritBeadSuper}; } }
        private static bool Ready(GameState p) { return enabled && Q.Player(p) && CharacterProgression.Managed(p) && p.Spells!=null && p.Entity.SubClasses!=null && p.Booth==null && p.Trade!=null && !p.Trade.InTrade; }
        private static bool Near(GameState p) { return Ready(p) && Q.Near(p,DailyQuestEnvoySystem.NpcId,1002) && p.Map.BaseID==1002; }
        private static string Day(DateTime now) { return MagnoliaQuest.Day(now); }
        private static bool Available(Run r,DateTime now) { return r==null || r.Completed!="" && string.CompareOrdinal(r.Completed,Day(now))<0; }
        private static bool Active(Run r,DateTime now) { return r!=null && r.Completed=="" && string.CompareOrdinal(r.Day,Day(now))<=0; }
        internal static void Initialize() {
            ConquerItemBaseInformation rune;
            if(!ConquerItemInformation.BaseInformations.TryGetValue(4030101,out rune) || rune.Class!=0)
                throw new InvalidOperationException("Soul Ball yellow rune catalog is missing or class restricted.");
            foreach(uint id in Types.Concat(new[]{Config.DailyQuestChiToken}))if(!ConquerItemInformation.BaseInformations.ContainsKey(id))throw new InvalidOperationException("Soul Ball catalog is missing.");
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_soul_ball_runs(
id uuid PRIMARY KEY,uid bigint NOT NULL,day varchar(8) NOT NULL,completed_day varchar(8) NOT NULL DEFAULT '',legacy boolean NOT NULL DEFAULT false,
bead bigint NOT NULL,type bigint NOT NULL,spirits bigint NOT NULL CHECK(spirits>=0),need bigint NOT NULL CHECK(need>0),
cp bigint NOT NULL,rvp bigint NOT NULL,chi bigint NOT NULL,buy_fee bigint NOT NULL,skip_fee bigint NOT NULL,blessing bigint NOT NULL,study integer NOT NULL,skip_study integer NOT NULL,
replaced_beads bigint[] NOT NULL DEFAULT '{}',paid_issue bigint NOT NULL DEFAULT 0,paid_complete bigint NOT NULL DEFAULT 0,reward_uid bigint,experience bigint NOT NULL DEFAULT 0,
created_at timestamptz NOT NULL DEFAULT now(),completed_at timestamptz,UNIQUE(uid,day));
ALTER TABLE revora_soul_ball_runs ADD COLUMN IF NOT EXISTS valuable_reward boolean NOT NULL DEFAULT false;
ALTER TABLE revora_soul_ball_runs ADD COLUMN IF NOT EXISTS soul_uid bigint UNIQUE;
ALTER TABLE revora_soul_ball_runs ADD COLUMN IF NOT EXISTS rune_reward boolean NOT NULL DEFAULT false;
ALTER TABLE revora_soul_ball_runs ADD COLUMN IF NOT EXISTS rune_uid bigint UNIQUE CHECK(rune_uid>0);
ALTER TABLE revora_soul_ball_runs ADD COLUMN IF NOT EXISTS paid_finish boolean NOT NULL DEFAULT false;
CREATE TABLE IF NOT EXISTS revora_soul_ball_bosses(source_run uuid PRIMARY KEY,daily_run uuid NOT NULL REFERENCES revora_soul_ball_runs(id),
uid bigint NOT NULL,source varchar(16) NOT NULL CHECK(source IN('terato','spook','sword-master','snow-banshee')),before_spirits bigint NOT NULL,after_spirits bigint NOT NULL,earned_at timestamptz NOT NULL);
ALTER TABLE revora_soul_ball_bosses DROP CONSTRAINT IF EXISTS revora_soul_ball_bosses_source_check;
ALTER TABLE revora_soul_ball_bosses ADD CONSTRAINT revora_soul_ball_bosses_source_check CHECK(source IN('terato','spook','sword-master','snow-banshee'));
CREATE UNIQUE INDEX IF NOT EXISTS revora_soul_ball_active ON revora_soul_ball_runs(uid) WHERE completed_day='';");}enabled=true;
        }
        private static Run NewRun(DateTime now,int grade) {
            var r=new Run{RuneReward=true,Id=Guid.NewGuid(),Day=Day(now),Completed="",Cp=Config.DailyQuestCp,Rvp=Config.DailyQuestRevoraPoints,Chi=Config.DailyQuestChiToken,
                BuyFee=Config.DailySpiritBeadSuperCp,SkipFee=Config.DailySpiritBeadSkipCp,SkipStudy=Config.DailySoulBallStudy};Grade(r,grade);return r;
        }
        private static void Grade(Run r,int grade) {
            r.Type=Types[grade];r.Need=new[]{Config.DailySpiritBeadNormalNeed,Config.DailySpiritBeadRefinedNeed,Config.DailySpiritBeadUniqueNeed,Config.DailySpiritBeadEliteNeed,Config.DailySpiritBeadSuperNeed}[grade];
            r.Study=new[]{Config.DailySpiritBeadNormalStudy,Config.DailySpiritBeadRefinedStudy,Config.DailySpiritBeadUniqueStudy,Config.DailySpiritBeadEliteStudy,Config.DailySpiritBeadSuperStudy}[grade];
            r.Bless=grade==4?172800u:86400u;r.Spirits=Math.Min((uint)Config.DailySpiritBeadFill,r.Need);
            if(r.Need==0)throw new InvalidOperationException("Soul Ball spirit requirement is invalid.");
        }
        private static void Owner(NpgsqlConnection c,NpgsqlTransaction t,GameState p) {
            using(var q=Q.Command(c,t,"SELECT level FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID)) {
                object level=q.ExecuteScalar();if(level==null || Convert.ToInt32(level)<Config.DailySoulBallMinLevel)throw new InvalidOperationException("Soul Ball eligibility changed.");
            }
        }
        private static Run Read(NpgsqlConnection c,NpgsqlTransaction t,GameState p) {
            using(var q=Q.Command(c,t,@"SELECT id,day,completed_day,bead,type,spirits,need,cp,rvp,chi,buy_fee,skip_fee,blessing,study,skip_study,legacy,paid_complete,paid_finish,valuable_reward,rune_reward
FROM revora_soul_ball_runs WHERE uid=@uid ORDER BY day DESC LIMIT 1","uid",(long)p.Entity.UID))
            using(var d=q.ExecuteReader())if(d.Read())return new Run{Id=d.GetGuid(0),Day=d.GetString(1),Completed=d.GetString(2),Bead=Convert.ToUInt32(d[3]),Type=Convert.ToUInt32(d[4]),
                Spirits=Convert.ToUInt32(d[5]),Need=Convert.ToUInt32(d[6]),Cp=Convert.ToUInt32(d[7]),Rvp=Convert.ToUInt32(d[8]),Chi=Convert.ToUInt32(d[9]),BuyFee=Convert.ToUInt32(d[10]),
                SkipFee=Convert.ToUInt32(d[11]),Bless=Convert.ToUInt32(d[12]),Study=Convert.ToUInt16(d[13]),SkipStudy=Convert.ToUInt16(d[14]),Legacy=d.GetBoolean(15),Paid=Convert.ToInt64(d[16])>0 || d.GetBoolean(17),RuneReward=d.GetBoolean(19)};return null;
        }
        private static void Insert(NpgsqlConnection c,NpgsqlTransaction t,GameState p,Run r,uint paid) {
            if(r.Legacy)r.RuneReward=false;
            if(Q.Execute(c,t,@"INSERT INTO revora_soul_ball_runs(id,uid,day,completed_day,legacy,bead,type,spirits,need,cp,rvp,chi,buy_fee,skip_fee,blessing,study,skip_study,paid_issue,valuable_reward,rune_reward)
VALUES(@id,@uid,@day,@done,@legacy,@bead,@type,@spirits,@need,@cp,@rvp,@chi,@buy,@skip,@bless,@study,@skipstudy,@paid,@valuable,@rune)",
                "id",r.Id,"uid",(long)p.Entity.UID,"day",r.Day,"done",r.Completed,"legacy",r.Legacy,"bead",(long)r.Bead,"type",(long)r.Type,"spirits",(long)r.Spirits,"need",(long)r.Need,
                "cp",(long)r.Cp,"rvp",(long)r.Rvp,"chi",(long)r.Chi,"buy",(long)r.BuyFee,"skip",(long)r.SkipFee,"bless",(long)r.Bless,"study",(int)r.Study,"skipstudy",(int)r.SkipStudy,"paid",(long)paid,"valuable",false,"rune",r.RuneReward)!=1)
                throw new InvalidOperationException("Soul Ball record was not saved.");
        }
        private static bool Usable(ConquerItem i) { return i!=null && i.Position==0 && i.Warehouse==0 && i.Lock==0 && !i.Suspicious && !i.InWardrobe && i.Days==0 && i.TimeLeftInMinutes==0 && i.StackSize<=1; }
        private static ConquerItem Bead(GameState p,Run r) { ConquerItem i;return r!=null && p.Inventory.TryGetItem(r.Bead,out i) && i.ID==r.Type && Usable(i)?i:null; }
        private static void Owned(NpgsqlConnection c,NpgsqlTransaction t,GameState p,ConquerItem i,bool remove) {
            string where=@" WHERE uid=@item AND id=@type AND entityid=@uid AND position=0 AND warehouse=0 AND stacksize=@stack AND bound=@bound
AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')='0'";
            using(var q=Q.Command(c,t,(remove?"DELETE FROM items":"SELECT count(*) FROM items")+where,"item",(long)i.UID,"type",(int)i.ID,"uid",(long)p.Entity.UID,"stack",(int)i.StackSize,"bound",i.Bound?1:0))
                if((remove?q.ExecuteNonQuery():Convert.ToInt32(q.ExecuteScalar()))!=1)throw new InvalidOperationException("Soul Ball ownership changed.");
        }
        private static void Bag(NpgsqlConnection c,NpgsqlTransaction t,GameState p) {
            var ids=new List<uint>();using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid","uid",(long)p.Entity.UID))
            using(var d=q.ExecuteReader())while(d.Read()){if(ids.Count>=40)throw new InvalidOperationException("Soul Ball bag exceeds capacity.");ids.Add(Convert.ToUInt32(d[0]));}
            if(!ids.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("Soul Ball inventory requires reload.");
        }
        private static void AddItem(NpgsqlConnection c,NpgsqlTransaction t,GameState p,ConquerItem i) {
            if(Q.Execute(c,t,@"INSERT INTO items(uid,id,entityid,position,warehouse,stacksize,bound,durability,maximdurability)
VALUES(@item,@type,@uid,0,0,1,@bound,@dur,@max)","item",(long)i.UID,"type",(int)i.ID,"uid",(long)p.Entity.UID,"bound",i.Bound?1:0,"dur",(int)i.Durability,"max",(int)i.MaximDurability)!=1)
                throw new InvalidOperationException("Soul Ball item was not saved.");
        }
        private static Run Import(NpgsqlConnection c,NpgsqlTransaction t,GameState p,DateTime now) {
            var r=Read(c,t,p);if(r!=null)return r;if(AnniversaryQuest.BlocksSoul(c,t,p,now))return null;
            bool done=false;uint spirits=0;
            using(var q=Q.Command(c,t,@"SELECT e.lastresettime,d.spiritbeads,d.spiritbeadscount FROM entities e LEFT JOIN dialyquests d ON d.entityid=e.uid WHERE e.uid=@uid","uid",(long)p.Entity.UID))
            using(var d=q.ExecuteReader())if(d.Read()) {
                done=!d.IsDBNull(0) && !d.IsDBNull(1) && DateTime.FromBinary(Convert.ToInt64(d[0])).Date==now.ToLocalTime().Date && Convert.ToBoolean(d[1]);
                spirits=d.IsDBNull(2)?0:Convert.ToUInt32(d[2]);
            }
            var beads=p.Inventory.Objects.Where(i=>Types.Contains(i.ID)).ToArray();
            if(done){r=NewRun(now,0);r.Legacy=true;r.Completed=Day(now);Insert(c,t,p,r,0);return r;}
            // Ambiguous mixed Anniversary/daily holdings are never converted into daily progress.
            if(beads.Length!=1 || !Usable(beads[0]) || p.Inventory.Contains(3001172,1))return null;
            Owned(c,t,p,beads[0],false);r=NewRun(now,Array.IndexOf(Types,beads[0].ID));r.Legacy=true;r.Bead=beads[0].UID;r.Spirits=Math.Min(spirits,r.Need);Insert(c,t,p,r,0);return r;
        }
        private static void Projection(GameState p,Run r,DateTime now) {
            displays.Remove(p);displays.Add(p,new Display{Completed=r==null?"":r.Completed});
            // The legacy flag also closes the Anniversary service. Never clear its completion.
            if(CompletedToday(p))p.Entity.SpiritBeads=true;
        }
        internal static bool CompletedToday(GameState p) {
            if(AnniversaryQuest.CompletedToday(p))return true;
            Display d;return displays.TryGetValue(p,out d)?d.Completed!="" && string.CompareOrdinal(d.Completed,Day(Clock()))>=0:p.Entity.SpiritBeads;
        }
        internal static void Refresh(GameState p) {
            if(!enabled || !Q.Player(p,false))return;
            lock(p.ItemSyncRoot)try{using(var c=DataHolder.MySqlConnection){c.Open();var r=Read(c,null,p);if(r!=null)Projection(p,r,Clock());}}
            catch(Exception ex){Q.Fail(p,"soul-ball-refresh",ex);}
        }
        private static Run Review(GameState p,DateTime now) {
            Run r;using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){Owner(c,t,p);Bag(c,t,p);r=Import(c,t,p,now);if(!Near(p))throw new InvalidOperationException("Soul Ball session changed.");t.Commit();}}
            Projection(p,r,now);return r;
        }
        private static Run Offered(GameState p,Run saved,DateTime now) {
            Offer o;bool found=offers.TryGetValue(p,out o);offers.Remove(p);
            if(!found || o.End<=now || o.Preview.Day!=Day(now) && !Active(saved,now))return null;
            if(Available(saved,now))return o.Preview.Bead==0 && o.Preview.Day==Day(now)?o.Preview:null;
            return Active(saved,now) && saved.Id==o.Preview.Id?saved:null;
        }
        private static int NextGrade(Run previous) {
            // Legacy completion-only rows do not prove a bead grade. Direct completion
            // takes the highest blessing reward and starts the next cycle at Normal.
            if(previous==null || previous.Paid || previous.Bead==0)return 0;
            int grade=Array.IndexOf(Types,previous.Type);
            if(grade<0)throw new InvalidOperationException("Soul Ball completed grade is unknown.");
            return (grade+1)%Types.Length;
        }
        private static void Show(GameState p,Npcs d,DateTime now) {
            offers.Remove(p);var r=Review(p,now);
            using(var c=DataHolder.MySqlConnection){c.Open();if(AnniversaryQuest.BlocksSoul(c,null,p,now)){d.Text("Finish your Anniversary bead, or return tomorrow after its reward.");return;}}
            if(!Available(r,now) && !Active(r,now)){d.Text("Your bead's work is done for today. Return tomorrow.");return;}
            if(Available(r,now))r=NewRun(now,NextGrade(r));
            offers.Add(p,new Offer{Preview=r,End=now.AddMinutes(2)});
            if(r.Bead==0){d.Text("You have earned a "+ConquerItemInformation.BaseInformations[r.Type].Name+". Fill it on your hunts to claim today's reward.");d.Option("Take my bead.",8);if(r.Type!=Config.DailySpiritBeadSuper)d.Option("Super bead: "+r.BuyFee+" CPs.",9);}
            else { d.Text("Your bead holds "+r.Spirits+" of "+r.Need+" spirits. Keep it in your bag while hunting.");if(Bead(p,r)==null)d.Option("I lost my bead.",35); }
            d.Option("Finish now: "+r.SkipFee+" CPs.",31);d.Option("About the reward.",21);d.Option("Which monsters?",22);
        }
        private static string Issue(GameState p,DateTime now,bool super) {
            Run r;ConquerItem bead;uint cp;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                Owner(c,t,p);var saved=Read(c,t,p);r=Offered(p,saved,now);if(r==null || !Available(saved,now))return "Let us review your bead first.";
                if(AnniversaryQuest.BlocksSoul(c,t,p,now) || p.Inventory.Objects.Any(i=>Types.Contains(i.ID) || i.ID==3001172))return "Finish the bead you already carry first.";
                if(p.Inventory.Count>=40)return "Make room for your bead.";uint fee=super && r.Type!=Config.DailySpiritBeadSuper?r.BuyFee:0;if(p.Entity.ConquerPoints<fee)return "You do not have enough CPs.";
                if(super)Grade(r,4);Bag(c,t,p);bead=Q.New(r.Type);r.Bead=bead.UID;cp=p.Entity.ConquerPoints-fee;
                if(Q.Execute(c,t,"UPDATE entities SET conquerpoints=@next WHERE uid=@uid AND COALESCE(conquerpoints,0)=@old","next",(long)cp,"uid",(long)p.Entity.UID,"old",(long)p.Entity.ConquerPoints)!=1)
                    throw new InvalidOperationException("Soul Ball payment changed.");
                AddItem(c,t,p,bead);Insert(c,t,p,r,fee);if(!Near(p))throw new InvalidOperationException("Soul Ball session changed.");t.Commit();
            }}Q.Install(p,new uint[0],new[]{bead});p.Entity.ApplyCommittedConquerPoints(cp);Projection(p,r,now);return "Keep this bead close. Return its spirits to rest when it is full.";
        }
        private static string Replace(GameState p,DateTime now) {
            Run r;ConquerItem replacement;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                Owner(c,t,p);r=Offered(p,Read(c,t,p),now);if(!Active(r,now) || r.Bead==0)return "Let us review your bead first.";
                if(AnniversaryQuest.BlocksSoul(c,t,p,now) || p.Inventory.Objects.Any(i=>Types.Contains(i.ID) || i.ID==3001172))return "Check the bead you already carry first.";
                using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM items WHERE uid=@bead)","bead",(long)r.Bead))
                    if(Equals(q.ExecuteScalar(),true))return "Your original bead still exists. Check your storage before asking for another.";
                if(p.Inventory.Count>=40)return "Make room for a replacement bead.";Bag(c,t,p);replacement=Q.New(r.Type);AddItem(c,t,p,replacement);
                if(Q.Execute(c,t,"UPDATE revora_soul_ball_runs SET bead=@next,replaced_beads=array_append(replaced_beads,@old) WHERE id=@id AND uid=@uid AND bead=@old AND completed_day=''",
                    "next",(long)replacement.UID,"old",(long)r.Bead,"id",r.Id,"uid",(long)p.Entity.UID)!=1)throw new InvalidOperationException("Soul Ball replacement was not saved.");
                if(!Near(p))throw new InvalidOperationException("Soul Ball session changed.");t.Commit();
            }}Q.Install(p,new uint[0],new[]{replacement});return "Guard this replacement well. Your gathered spirits are safe.";
        }
        private static string Complete(GameState p,DateTime now,bool paid,ConquerItem requested) {
            Run r;ConquerItem bead,reward,rune=null;uint cp,rvp,blessing;ushort study,earned;ulong autoHunt;ExperienceRewardPlan plan;ExperienceRewards.Effects effects;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                Owner(c,t,p);var saved=Import(c,t,p,now);r=paid?Offered(p,saved,now):saved;
                if(r==null || r.Completed!="" || string.CompareOrdinal(r.Day,Day(now))>0)return "Speak with the Daily Quest Envoy about your bead.";
                bead=Bead(p,r);
                if(!paid && (bead==null || !ReferenceEquals(bead,requested)))return "Use the bead entrusted to you, unlocked in your bag.";
                if(!paid && r.Spirits<r.Need)return "Your bead holds "+r.Spirits+" of "+r.Need+" spirits.";
                if(paid && r.Bead!=0 && bead==null)return "Bring your original bead, unlocked in your bag.";
                if(AnniversaryQuest.BlocksSoul(c,t,p,now) || p.Inventory.Contains(3001172,1))return "Finish your Anniversary bead, or return tomorrow after its reward.";
                if(p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Cursed))return "Return when you can receive experience.";
                uint fee=paid?r.SkipFee:0;if(p.Entity.ConquerPoints<fee)return "You do not have enough CPs.";
                if(p.Inventory.Count-(bead==null?0:1)+(r.RuneReward?2:1)>40)return r.RuneReward?"Leave room for your Chi Token and bound rune.":"Leave room for your Chi Token.";
                earned=paid?r.SkipStudy:r.Study;ulong next=(ulong)p.Entity.ConquerPoints-fee+r.Cp;uint bless=paid?172800u:r.Bless;
                if(next>uint.MaxValue || (ulong)p.Entity.RevoraPoint+r.Rvp>uint.MaxValue || p.Entity.SubClasses.StudyPoints+earned>ushort.MaxValue || (ulong)p.Entity.HeavenBlessing+bless>uint.MaxValue)
                    return "Spend some points or blessing time before collecting your reward.";
                Bag(c,t,p);if(paid && Available(saved,now))Insert(c,t,p,r,0);
                cp=(uint)next;rvp=p.Entity.RevoraPoint+r.Rvp;study=(ushort)(p.Entity.SubClasses.StudyPoints+earned);blessing=p.Entity.HeavenBlessing+bless;
                ulong award=p.Entity.Level>=Config.LevelCap?0:p.ExpBall;
                plan=new ExperienceRewardPlan(CharacterProgression.Capture(p),award,Config.LevelCap,Config.AttributePointsPerLevel,ReincarnationRecovery.Snapshot(p));
                autoHunt=p.Entity.InAutoHunt?checked(p.Entity.AutoHuntEXP+award/Math.Max(1u,Config.AutoHuntExperienceDivisor)):p.Entity.AutoHuntEXP;
                CharacterProgression.Persist(c,t,p,plan.After);
                if(Q.Execute(c,t,@"UPDATE entities SET conquerpoints=@cp,revorapoint=@rvp,studypoints=@study WHERE uid=@uid
AND COALESCE(conquerpoints,0)=@oldcp AND COALESCE(revorapoint,0)=@oldrvp AND COALESCE(studypoints,0)=@oldstudy","cp",(long)cp,"rvp",(long)rvp,"study",(int)study,
                    "uid",(long)p.Entity.UID,"oldcp",(long)p.Entity.ConquerPoints,"oldrvp",(long)p.Entity.RevoraPoint,"oldstudy",(int)p.Entity.SubClasses.StudyPoints)!=1)throw new InvalidOperationException("Soul Ball balances changed.");
                BlessingBalance.Persist(c,t,p,blessing);if(bead!=null)Owned(c,t,p,bead,true);reward=Q.New(r.Chi,bead!=null && bead.Bound);AddItem(c,t,p,reward);
                if(r.RuneReward){rune=Q.New(4030101,true);Q.Insert(c,t,p.Entity.UID,rune);}
                effects=ExperienceRewards.Persist(c,t,p,plan);ReincarnationRecovery.Consume(c,t,p,plan.Restored);
                if(Q.Execute(c,t,@"UPDATE revora_soul_ball_runs SET completed_day=@day,completed_at=@now,paid_complete=@fee,paid_finish=@paid,reward_uid=@reward,soul_uid=@soul,rune_uid=@rune,experience=@exp
WHERE id=@id AND uid=@uid AND completed_day=''","day",Day(now),"now",now,"fee",(long)fee,"paid",paid,"reward",(long)reward.UID,"soul",DBNull.Value,"rune",rune==null?(object)DBNull.Value:(long)rune.UID,"exp",checked((long)award),"id",r.Id,"uid",(long)p.Entity.UID)!=1)
                    throw new InvalidOperationException("Soul Ball completion was not saved.");
                StudyRewardEvidence.Record(c,t,p,earned,"soul-ball:"+r.Id.ToString("N"));
                if(!Ready(p) || paid && !Near(p))throw new InvalidOperationException("Soul Ball session changed.");t.Commit();
            }}Q.Install(p,bead==null?new uint[0]:new[]{bead.UID},rune==null?new[]{reward}:new[]{reward,rune});p.Entity.ApplyCommittedConquerPoints(cp);p.Entity.ApplyCommittedRevoraPoints(rvp);p.Entity.SubClasses.StudyPoints=study;
            BlessingBalance.ApplyCommitted(p,blessing);ReincarnationRecovery.Committed(p,plan.Restored);MentorExperience.Apply(p,effects.MentorCredit);p.ApplyExperienceReward(plan,effects,autoHunt,true);
            r.Completed=Day(now);Projection(p,r,now);Q.Refresh(p);p.Send(new SubClassShowFull(0){ID=8,Study=study,StudyReceive=earned}.ToArray());return "The spirits are at peace. Take your reward, and return tomorrow.";
        }
        internal static bool TryUse(ConquerItem item,GameState p) {
            if(item==null || !Types.Contains(item.ID))return false;if(!Ready(p))return true;
            lock(p.ItemSyncRoot)lock(MentorExperience.Sync)try{if(Ready(p) && p.Entity.Level>=Config.DailySoulBallMinLevel)p.Send(Complete(p,Clock(),false,item));}
            catch(Exception ex){Q.Fail(p,"soul-ball-reward",ex);}return true;
        }
        internal static bool HandleNpc(GameState p,NpcRequest request) {
            if(p==null || request==null || p.ActiveNpc!=DailyQuestEnvoySystem.NpcId)return false;
            byte choice=request.OptionID;if(choice!=7 && choice!=8 && choice!=9 && choice!=10 && choice!=20 && choice!=21 && choice!=22 && choice!=31 && choice!=35){offers.Remove(p);return false;}
            if(!Ready(p))return true;
            lock(p.ItemSyncRoot)lock(MentorExperience.Sync)try {
                if(!Near(p) || request.NpcID!=0 && request.NpcID!=p.ActiveNpc)return true;var d=new Npcs(p);d.Avatar(Config.DailyEnvoyAvatar);DateTime now=Clock();
                if(p.Entity.Level<Config.DailySoulBallMinLevel)d.Text("Return at level "+Config.DailySoulBallMinLevel+" for a bead.");
                else if(choice==7)Show(p,d,now);
                else if(choice==8 || choice==9)d.Text(Issue(p,now,choice==9));
                else if(choice==31)d.Text(Complete(p,now,true,null));
                else if(choice==35)d.Text(Replace(p,now));
                else if(choice==22)d.Text("Wild monsters give 1 spirit below level 70, 2 at 70-99, 3 at 100-119, and 4 from level 120. Terato gives 1,000 spirits. Your daily Spook victory gives 600, 800 or 1,000 by difficulty.");
                else if(choice==10 || choice==20)d.Text("Your first bead is Normal. Complete it to earn a finer bead next time. After Super, begin again with Normal.");
                else {using(var c=DataHolder.MySqlConnection){c.Open();var r=Read(c,null,p);if(Active(r,now))d.Text("Your bead earns "+r.Cp+" CPs, "+r.Rvp+" RVPs, "+r.Study+" study points, one EXP Ball of experience, one Chi Token and "+(r.Bless/3600)+" hours of blessing"+(r.RuneReward?", and a bound Tortoise Breaker rune.":"."));
                    else d.Text("Each bead earns CPs, RVPs, study points, experience, a Chi Token, a bound Tortoise Breaker rune and blessing. Finish now grants "+Config.DailySoulBallStudy+" study points and 48 hours of blessing.");}}
                d.Option("Review my bead.",7);d.Option("Farewell.",255);d.Send();
            }catch(Exception ex){offers.Remove(p);Q.Fail(p,"soul-ball-npc",ex);}return true;
        }
        internal static string RecordTerato(NpgsqlConnection c,NpgsqlTransaction t,GameState p,TeratoEncounter.Actor actor) {
            if(!enabled || !Ready(p) || p.Entity.Level<Config.DailySoulBallMinLevel || !p.Inventory.Objects.Any(i=>Types.Contains(i.ID)))return null;
            TeratoEncounter.Actor current;Entity body;
            if(t==null || !Monitor.IsEntered(p.ItemSyncRoot) || !Monitor.IsEntered(TeratoEncounter.Gate) || actor==null || actor.Released || actor.Body.Dead
                || !TeratoEncounter.Actors.TryGetValue(actor.Body.UID,out current) || !ReferenceEquals(current,actor)
                || !actor.Map.Entities.TryGetValue(actor.Body.UID,out body) || !ReferenceEquals(body,actor.Body) || !TeratoEncounter.Eligible(actor,p,true))
                throw new InvalidOperationException("Soul Ball requires the owned live Terato victory transaction.");
            using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_terato_runs WHERE id=@source AND state='victory' AND killer=@uid)","source",actor.Run,"uid",(long)p.Entity.UID))
                if(!Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("Soul Ball Terato victory was not recorded.");
            return RecordBoss(c,t,p,actor.Run,"terato",1000,"Terato's");
        }
        internal static string RecordSpook(NpgsqlConnection c,NpgsqlTransaction t,GameState p,ExorcismWorld.Hunt hunt) {
            if(!enabled || !Ready(p) || p.Entity.Level<Config.DailySoulBallMinLevel || !p.Inventory.Objects.Any(i=>Types.Contains(i.ID)))return null;
            ExorcismWorld.Hunt current;Entity body;
            if(t==null || !Monitor.IsEntered(p.ItemSyncRoot) || !Monitor.IsEntered(ExorcismWorld.Gate) || hunt==null || hunt.Released || hunt.Body==null || hunt.Body.Dead
                || !ExorcismWorld.Maps.TryGetValue(hunt.Map.ID,out current) || !ReferenceEquals(current,hunt) || !hunt.Map.Entities.TryGetValue(hunt.Body.UID,out body)
                || !ReferenceEquals(body,hunt.Body) || !ExorcismWorld.Eligible(hunt,p,true))throw new InvalidOperationException("Soul Ball requires the owned Spook victory transaction.");
            using(var q=Q.Command(c,t,@"SELECT EXISTS(SELECT 1 FROM revora_exorcism_hunts h JOIN revora_exorcism_daily d ON d.source_run=h.id
WHERE h.id=@run AND h.state='victory' AND h.killer=@uid AND d.uid=@uid AND d.state='victory' AND d.completed_day='')","run",hunt.Id,"uid",(long)p.Entity.UID))
                if(!Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("Soul Ball Spook victory was not recorded.");
            return RecordBoss(c,t,p,hunt.Id,"spook",new uint[]{600,800,1000}[hunt.Grade-1],"The Spook's");
        }
        internal static string RecordSwordMaster(NpgsqlConnection c,NpgsqlTransaction t,GameState p,SwordMasterEncounter.Actor actor) {
            if(!enabled || !Ready(p) || p.Entity.Level<Config.DailySoulBallMinLevel || !p.Inventory.Objects.Any(i=>Types.Contains(i.ID)))return null;
            if(t==null || t.Connection!=c || !Monitor.IsEntered(p.ItemSyncRoot) || !Monitor.IsEntered(SwordMasterEncounter.Gate)
                || !SwordMasterEncounter.Current(actor) || !SwordMasterEncounter.Eligible(actor,p,true))
                throw new InvalidOperationException("Soul Ball requires the owned Sword Master victory transaction.");
            using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_sword_master_runs WHERE id=@source AND state='victory' AND killer=@uid)",
                "source",actor.Run.Id,"uid",(long)p.Entity.UID))if(!Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("Sword Master victory was not recorded.");
            return RecordBoss(c,t,p,actor.Run.Id,"sword-master",1000,"The Sword Master's");
        }
        internal static string RecordSnowBanshee(NpgsqlConnection c,NpgsqlTransaction t,GameState p,SnowBansheeEncounter.Actor actor) {
            if(!enabled || !Ready(p) || p.Entity.Level<Config.DailySoulBallMinLevel || !p.Inventory.Objects.Any(i=>Types.Contains(i.ID)))return null;
            if(t==null || t.Connection!=c || !Monitor.IsEntered(p.ItemSyncRoot) || !Monitor.IsEntered(SnowBansheeEncounter.Gate)
                || !SnowBansheeEncounter.Current(actor) || !SnowBansheeEncounter.Eligible(actor,p,true))
                throw new InvalidOperationException("Soul Ball requires the owned Snow Banshee victory transaction.");
            using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_snow_banshee_runs WHERE id=@source AND lease=@lease AND state='victory' AND killer=@uid)",
                "source",actor.Run.Id,"lease",actor.Run.Lease,"uid",(long)p.Entity.UID))if(!Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("Snow Banshee victory was not recorded.");
            return RecordBoss(c,t,p,actor.Run.Id,"snow-banshee",1000,"The Banshee's");
        }
        private static string RecordBoss(NpgsqlConnection c,NpgsqlTransaction t,GameState p,Guid run,string source,uint gain,string label) {
            Owner(c,t,p);DateTime now=Clock();var r=Import(c,t,p,now);var bead=Bead(p,r);
            if(!Active(r,now) || bead==null || r.Spirits>=r.Need || p.Inventory.Contains(3001172,1))return null;
            Owned(c,t,p,bead,false);uint next=(uint)Math.Min((ulong)r.Need,(ulong)r.Spirits+gain);
            if(Q.Execute(c,t,"UPDATE revora_soul_ball_runs SET spirits=@next WHERE id=@id AND uid=@uid AND spirits=@old AND completed_day=''",
                "next",(long)next,"id",r.Id,"uid",(long)p.Entity.UID,"old",(long)r.Spirits)!=1)throw new InvalidOperationException("Boss spirits were not saved.");
            if(Q.Execute(c,t,@"INSERT INTO revora_soul_ball_bosses(source_run,daily_run,uid,source,before_spirits,after_spirits,earned_at)
VALUES(@source,@daily,@uid,@kind,@before,@after,@now)","kind",source,"source",run,"daily",r.Id,"uid",(long)p.Entity.UID,"before",(long)r.Spirits,"after",(long)next,"now",now)!=1)
                throw new InvalidOperationException("Boss spirit source was not saved.");
            if(!Ready(p))throw new InvalidOperationException("Boss spirit owner changed.");
            return next>=r.Need?label+" spirits fill your bead. Use it to claim your reward.":label+" spirits bring your bead to "+next+" of "+r.Need+".";
        }
        // A revived creature gets a new life; changing DeathStamp cannot replay the same corpse.
        internal static void Revived(Entity monster) { if(monster!=null && !monster.Dead)lock(monster)lives.Remove(monster); }
        private static uint SpiritValue(byte level) { return level<70?1u:level<100?2u:level<120?3u:4u; }
        // Public hunting worlds only. Adventure Zone 1217 has a reviewed fixed
        // terrain alias; arbitrary copies and the separate Potency map do not.
        private static readonly HashSet<ushort> huntingWorlds=new HashSet<ushort>{
            1002,1011,1020,1000,1015,1001,1013,1014,1021,1022,1210,1211,
            1207,1208,1212,1214,1215,1217,1762,1927,1999,2054,2055,2056
        };
        private static bool WildernessWorld(Map map) {
            Map current;MapsTable.MapInformation info;
            if(map==null || !huntingWorlds.Contains(map.ID) || !Kernel.Maps.TryGetValue(map.ID,out current) || !ReferenceEquals(current,map))return false;
            ushort terrain=map.ID==1217?(ushort)1075:map.ID;
            return map.BaseID==terrain && MapsTable.MapInformations.TryGetValue(map.ID,out info)
                && info.ID==map.ID && info.BaseID==terrain && info.Owner==0;
        }
        internal static bool WildernessDeath(Entity monster,Entity killer) {
            var p=killer==null?null:killer.Owner;MonsterInformation template;Entity current;
            if(!Q.Player(p) || !ReferenceEquals(p.Entity,killer) || monster==null || !monster.Dead || monster.EntityFlag!=EntityFlag.Monster || monster.Companion
                || monster.MonsterInfo==null || !ReferenceEquals(monster.MonsterInfo.Owner,monster) || monster.MonsterInfo.Guard || monster.MonsterInfo.Reviver || monster.MonsterInfo.Boss
                || CityGuardSystem.IsCityGuard(monster) || LivingWorldSystem.IsAdventurer(monster) || monster.MapID!=killer.MapID || !WildernessWorld(p.Map)
                || !p.Map.Entities.TryGetValue(monster.UID,out current) || !ReferenceEquals(current,monster)
                || !MonsterInformation.MonsterInformations.TryGetValue(monster.MonsterInfo.ID,out template) || template.Name!=monster.MonsterInfo.Name || template.Level!=monster.MonsterInfo.Level
                || template.Guard || template.Reviver || template.Boss)return false;
            return true;
        }
        internal static void OnDeath(Entity monster,Entity killer) {
            var p=killer==null?null:killer.Owner;
            if(!Ready(p) || p.Entity.Level<Config.DailySoulBallMinLevel || !WildernessDeath(monster,killer))return;
            lock(p.ItemSyncRoot)lock(monster)try {
                if(!Ready(p) || !WildernessDeath(monster,killer))return;
                var life=lives.GetValue(monster,_=>new Life());if(life.Credited)return;
                if(!p.Inventory.Objects.Any(i=>Types.Contains(i.ID))){life.Credited=true;return;}
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    Owner(c,t,p);var r=Import(c,t,p,Clock());var bead=Bead(p,r);if(!Active(r,Clock()) || bead==null || r.Spirits>=r.Need || p.Inventory.Contains(3001172,1)){life.Credited=true;return;}
                    Owned(c,t,p,bead,false);uint gain=SpiritValue(monster.MonsterInfo.Level);uint next=(uint)Math.Min((ulong)r.Need,(ulong)r.Spirits+gain);
                    if(Q.Execute(c,t,"UPDATE revora_soul_ball_runs SET spirits=@next WHERE id=@id AND uid=@uid AND spirits=@old AND completed_day=''","next",(long)next,"id",r.Id,"uid",(long)p.Entity.UID,"old",(long)r.Spirits)!=1)
                        throw new InvalidOperationException("Soul Ball spirits were not saved.");
                    if(!Ready(p) || !WildernessDeath(monster,killer))throw new InvalidOperationException("Soul Ball hunter changed.");t.Commit();life.Credited=true;
                    p.Send(next>=r.Need?"Your Spirit Ball is full. Use it to release the spirits.":"Your Spirit Ball holds "+next+" of "+r.Need+" spirits.");
                }}
            }catch(Exception ex){Q.Fail(p,"soul-ball-hunt",ex);}
        }
    }
}
