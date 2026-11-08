using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class DailyRareQuest
    {
        internal static Func<DateTime> Clock = () => DateTime.UtcNow;
        internal static Func<int,int> Roll = max => Kernel.Random.Next(max);
        private static bool enabled;
        private sealed class Run {
            internal string Day; internal bool Done, Import, RuneReward;
            internal byte Quest, Changes, Stars=1, Amount, Maximum, MaxStars, Pool;
            internal uint Item, Cp, Rvp, Chi, ChangeFee, StarFee, SkipFee;
            internal ushort Study, Bonus;
        }
        private sealed class Offer { internal string Day; internal byte Quest, Changes, Stars; internal bool Star; internal DateTime End; }
        private sealed class Take { internal ConquerItem Item; internal int Count; internal bool Removed { get { return Count>=Math.Max(1,(int)Item.StackSize); } } }
        private static readonly ConditionalWeakTable<GameState,Offer> offers = new ConditionalWeakTable<GameState,Offer>();
        internal static string Day(DateTime now) { return MagnoliaQuest.Day(now); }
        internal static void Initialize() {
            ConquerItemBaseInformation rune;
            if(!ConquerItemInformation.BaseInformations.TryGetValue(4030101,out rune) || rune.Class!=0)
                throw new InvalidOperationException("Daily quest yellow rune catalog is missing or class restricted.");
            foreach(uint id in new[]{Config.DailyRarePhoenixGem,Config.DailyRareDragonGem,Config.DailyRareFuryGem,Config.DailyRareRainbowGem,Config.DailyQuestChiToken})
                if(!ConquerItemInformation.BaseInformations.ContainsKey(id))throw new InvalidOperationException("Rare Materials catalog is missing.");
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_rare_runs(
uid bigint NOT NULL CHECK(uid>0),day varchar(8) NOT NULL,completed boolean NOT NULL DEFAULT false,legacy boolean NOT NULL DEFAULT false,
quest integer NOT NULL CHECK(quest BETWEEN 1 AND 4),changes integer NOT NULL CHECK(changes BETWEEN 0 AND 255),stars integer NOT NULL CHECK(stars BETWEEN 1 AND 255),
item bigint NOT NULL,amount integer NOT NULL CHECK(amount BETWEEN 1 AND 255),cp bigint NOT NULL,rvp bigint NOT NULL,chi bigint NOT NULL,study integer NOT NULL,bonus integer NOT NULL,
change_fee bigint NOT NULL,star_fee bigint NOT NULL,skip_fee bigint NOT NULL,maximum integer NOT NULL,max_stars integer NOT NULL,pool integer NOT NULL CHECK(pool BETWEEN 1 AND 4),
reroll_rvp bigint NOT NULL DEFAULT 0,star_rvp bigint NOT NULL DEFAULT 0,paid_cp bigint NOT NULL DEFAULT 0,
debited_ids bigint[] NOT NULL DEFAULT '{}',debited_counts integer[] NOT NULL DEFAULT '{}',reward_uid bigint,experience bigint NOT NULL DEFAULT 0,
created_at timestamptz NOT NULL DEFAULT now(),completed_at timestamptz,PRIMARY KEY(uid,day));
ALTER TABLE revora_rare_runs ADD COLUMN IF NOT EXISTS valuable_reward boolean NOT NULL DEFAULT false;
ALTER TABLE revora_rare_runs ADD COLUMN IF NOT EXISTS soul_uid bigint UNIQUE;
ALTER TABLE revora_rare_runs ADD COLUMN IF NOT EXISTS rune_reward boolean NOT NULL DEFAULT false;
ALTER TABLE revora_rare_runs ADD COLUMN IF NOT EXISTS rune_uid bigint UNIQUE CHECK(rune_uid>0);");}enabled=true;
        }
        private static uint Type(byte quest) { return new[]{0u,Config.DailyRarePhoenixGem,Config.DailyRareDragonGem,Config.DailyRareFuryGem,Config.DailyRareRainbowGem}[quest]; }
        private static void Assign(Run r,byte quest) {
            r.Quest=quest;r.Item=Type(quest);
            if(r.Amount==0 || !ConquerItemInformation.BaseInformations.ContainsKey(r.Item))throw new InvalidOperationException("Rare Materials assignment is invalid.");
        }
        private static Run NewRun(string day,byte quest) {
            var r=new Run{Day=day,Amount=Config.DailyRareGemAmount,Cp=Config.DailyQuestCp,Rvp=Config.DailyQuestRevoraPoints,Chi=Config.DailyQuestChiToken,
                Study=Config.DailyRareMaterialsStudy,Bonus=Config.DailyRareStarStudyBonus,ChangeFee=Config.DailyRareChangeRevoraPoints,StarFee=Config.DailyRareStarRevoraPoints,
                SkipFee=Config.DailyRareSkipCp,Maximum=Config.DailyRareChangeMax,MaxStars=(byte)Math.Max(1,(int)Config.DailyRareStarMax),Pool=(byte)(Math.Max(2,Math.Min(5,Config.DailyRareRollExclusive))-1)};
            Assign(r,quest);return r;
        }
        private static byte Choose(int pool,byte avoid) {
            var choices=Enumerable.Range(1,pool).Where(i=>i!=avoid).ToArray();return choices.Length==0 ? (byte)0 : (byte)choices[Roll(choices.Length)];
        }
        private static Run Read(NpgsqlConnection c,NpgsqlTransaction t,GameState p,DateTime now) {
            using(var q=Q.Command(c,t,@"SELECT day,completed,quest,changes,stars,item,amount,cp,rvp,chi,study,bonus,change_fee,star_fee,skip_fee,maximum,max_stars,pool,valuable_reward,rune_reward
FROM revora_rare_runs WHERE uid=@uid ORDER BY day DESC LIMIT 1","uid",(long)p.Entity.UID))
            using(var d=q.ExecuteReader())if(d.Read())return new Run{Day=d.GetString(0),Done=d.GetBoolean(1),Quest=Convert.ToByte(d[2]),Changes=Convert.ToByte(d[3]),Stars=Convert.ToByte(d[4]),
                Item=Convert.ToUInt32(d[5]),Amount=Convert.ToByte(d[6]),Cp=Convert.ToUInt32(d[7]),Rvp=Convert.ToUInt32(d[8]),Chi=Convert.ToUInt32(d[9]),Study=Convert.ToUInt16(d[10]),
                Bonus=Convert.ToUInt16(d[11]),ChangeFee=Convert.ToUInt32(d[12]),StarFee=Convert.ToUInt32(d[13]),SkipFee=Convert.ToUInt32(d[14]),Maximum=Convert.ToByte(d[15]),MaxStars=Convert.ToByte(d[16]),Pool=Convert.ToByte(d[17]),RuneReward=d.GetBoolean(19)};
            using(var q=Q.Command(c,t,@"SELECT e.lastresettime,d.rarematerials,d.pricematerialsid,d.materialstimes,d.materialstemid
FROM entities e LEFT JOIN dialyquests d ON d.entityid=e.uid WHERE e.uid=@uid","uid",(long)p.Entity.UID))
            using(var d=q.ExecuteReader())if(d.Read() && !d.IsDBNull(0) && !d.IsDBNull(1) && DateTime.FromBinary(Convert.ToInt64(d[0])).Date==now.ToLocalTime().Date) {
                bool done=Convert.ToBoolean(d[1]);byte quest=d.IsDBNull(2)?(byte)0:Convert.ToByte(d[2]);
                if(done || quest>=1 && quest<=4 && !d.IsDBNull(4) && Convert.ToUInt32(d[4])==Type(quest)) {
                    // The old star value was never persisted; only saved assignment/completion is evidence.
                    var r=NewRun(Day(now),quest>=1 && quest<=4?quest:(byte)1);r.Done=done;r.Import=true;r.Changes=d.IsDBNull(3)?(byte)0:Convert.ToByte(d[3]);return r;
                }
            }return null;
        }
        private static void Insert(NpgsqlConnection c,NpgsqlTransaction t,GameState p,Run r) {
            r.RuneReward=!r.Import;
            if(Q.Execute(c,t,@"INSERT INTO revora_rare_runs(uid,day,completed,legacy,quest,changes,stars,item,amount,cp,rvp,chi,study,bonus,change_fee,star_fee,skip_fee,maximum,max_stars,pool,valuable_reward,rune_reward)
VALUES(@uid,@day,@done,@legacy,@quest,@changes,@stars,@item,@amount,@cp,@rvp,@chi,@study,@bonus,@change,@star,@skip,@max,@maxstars,@pool,@valuable,@rune)",
                "uid",(long)p.Entity.UID,"day",r.Day,"done",r.Done,"legacy",r.Import,"quest",(int)r.Quest,"changes",(int)r.Changes,"stars",(int)r.Stars,
                "item",(long)r.Item,"amount",(int)r.Amount,"cp",(long)r.Cp,"rvp",(long)r.Rvp,"chi",(long)r.Chi,"study",(int)r.Study,"bonus",(int)r.Bonus,
                "change",(long)r.ChangeFee,"star",(long)r.StarFee,"skip",(long)r.SkipFee,"max",(int)r.Maximum,"maxstars",(int)r.MaxStars,"pool",(int)r.Pool,"valuable",false,"rune",r.RuneReward)!=1)
                throw new InvalidOperationException("Rare Materials assignment was not saved.");r.Import=false;
        }
        private static bool Current(Run r,DateTime now) {return r!=null && r.Day==Day(now) && !r.Done;}
        private static int Study(Run r) {return r.Study+(r.Stars-1)*r.Bonus;}
        private static bool Ready(GameState p) {return enabled && Q.Player(p) && CharacterProgression.Managed(p) && p.Spells!=null && p.Entity.SubClasses!=null && p.Booth==null && p.Trade!=null && !p.Trade.InTrade;}
        private static bool Near(GameState p) {return Ready(p) && Q.Near(p,DailyQuestEnvoySystem.NpcId,1002) && p.Map.BaseID==1002;}
        private static void Owner(NpgsqlConnection c,NpgsqlTransaction t,GameState p) {
            using(var q=Q.Command(c,t,"SELECT level FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID)) {
                object level=q.ExecuteScalar();if(level==null || Convert.ToInt32(level)<Config.DailyRareMinLevel)throw new InvalidOperationException("Rare Materials eligibility changed.");
            }
        }
        private static void Projection(GameState p,Run r,DateTime now) {
            bool current=r!=null && string.CompareOrdinal(r.Day,Day(now))>=0;
            p.Entity.RareMaterials=current && r.Done;p.Entity.PriceMaterialsID=current?r.Quest:(byte)0;p.Entity.MaterialsTimes=current?r.Changes:(byte)0;p.Entity.MaterialsStar=current?r.Stars:(byte)0;
            p.Entity.MaterialstemId=current && !r.Done?r.Item:0;p.Entity.MaterialsName=current && !r.Done?ConquerItemInformation.BaseInformations[r.Item].Name:"";
        }
        internal static void Refresh(GameState p) {
            if(!enabled || !Q.Player(p,false))return;
            lock(p.ItemSyncRoot)try{DateTime now=Clock();using(var c=DataHolder.MySqlConnection){c.Open();Projection(p,Read(c,null,p,now),now);}}
            catch(Exception ex){Q.Fail(p,"daily-rare-refresh",ex);}
        }
        private static Run Accept(GameState p,DateTime now) {
            Run r;using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                Owner(c,t,p);r=Read(c,t,p,now);if(r!=null && r.Import)Insert(c,t,p,r);
                if(r==null || string.CompareOrdinal(r.Day,Day(now))<0){r=NewRun(Day(now),1);Assign(r,Choose(r.Pool,0));Insert(c,t,p,r);}
                if(!Near(p))throw new InvalidOperationException("Rare Materials session changed.");t.Commit();
            }}Projection(p,r,now);return r;
        }
        private static void OfferFor(GameState p,Run r,DateTime now,bool star) {
            offers.Remove(p);offers.Add(p,new Offer{Day=r.Day,Quest=r.Quest,Changes=r.Changes,Stars=r.Stars,Star=star,End=now.AddMinutes(2)});
        }
        private static bool Offered(GameState p,Run r,DateTime now,bool star) {
            Offer offer;bool valid=offers.TryGetValue(p,out offer) && offer.End>now && offer.Day==Day(now) && r!=null && offer.Day==r.Day
                && offer.Quest==r.Quest && offer.Changes==r.Changes && offer.Stars==r.Stars && offer.Star==star;offers.Remove(p);return valid;
        }
        private static void Show(GameState p,Npcs d,Run r,DateTime now,bool star) {
            offers.Remove(p);if(!Current(r,now)){d.Text("Your commission is settled for today. Return tomorrow.");return;}
            OfferFor(p,r,now,star);
            if(star){d.Text("A higher star earns "+r.Bonus+" more study points. The upgrade costs "+r.StarFee+" RVPs.");if(r.Stars<r.MaxStars)d.Option("Raise the star.",34);return;}
            d.Text("Bring "+r.Amount+" "+ConquerItemInformation.BaseInformations[r.Item].Name+". Your commission has "+r.Stars+" star"+(r.Stars==1?".":"s."));
            d.Text(r.Quest==4?"Miners find these in Forest Mine. Take a pickaxe.":"Miners find these in Plain Mine. Take a pickaxe.");
            d.Option("Hand over the gems.",28);if(r.Changes<r.Maximum && r.Pool>1)d.Option("Change the gems: "+r.ChangeFee+" RVPs.",29);
            if(r.Stars<r.MaxStars)d.Option("Raise the star.",32);d.Option("Finish for "+r.SkipFee+" CPs.",30);d.Option("What is my reward?",33);
        }
        private static string Change(GameState p,DateTime now,bool star) {
            Run r;uint balance;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                Owner(c,t,p);r=Read(c,t,p,now);if(!Current(r,now) || !Offered(p,r,now,star))return "Let us review your commission first.";
                if(star?r.Stars>=r.MaxStars:r.Changes>=r.Maximum || r.Pool<=1)return star?"Your commission is already at its highest star.":"I have no more changes for this commission.";
                uint fee=star?r.StarFee:r.ChangeFee;if(p.Entity.RevoraPoint<fee)return "You do not have enough RVPs.";
                if(r.Import)Insert(c,t,p,r);byte old=r.Quest;if(!star)Assign(r,Choose(r.Pool,old));balance=p.Entity.RevoraPoint-fee;
                if(Q.Execute(c,t,"UPDATE entities SET revorapoint=@next WHERE uid=@uid AND COALESCE(revorapoint,0)=@old","next",(long)balance,"uid",(long)p.Entity.UID,"old",(long)p.Entity.RevoraPoint)!=1)
                    throw new InvalidOperationException("Rare Materials RVP preimage changed.");
                if(Q.Execute(c,t,@"UPDATE revora_rare_runs SET quest=@quest,item=@item,changes=changes+@change,stars=stars+@star,reroll_rvp=reroll_rvp+@rerollfee,star_rvp=star_rvp+@starfee
WHERE uid=@uid AND day=@day AND quest=@old AND changes=@changes AND stars=@stars AND completed=false", "quest",(int)r.Quest,"item",(long)r.Item,
                    "change",star?0:1,"star",star?1:0,"rerollfee",star?0L:(long)fee,"starfee",star?(long)fee:0L,"uid",(long)p.Entity.UID,"day",r.Day,
                    "old",(int)old,"changes",(int)r.Changes,"stars",(int)r.Stars)!=1)throw new InvalidOperationException("Rare Materials paid change was not saved.");
                if(!Near(p))throw new InvalidOperationException("Rare Materials session changed.");t.Commit();if(star)r.Stars++;else r.Changes++;
            }}p.Entity.ApplyCommittedRevoraPoints(balance);Projection(p,r,now);return star?"Your commission now bears a brighter star.":"I have chosen a different gem for your commission.";
        }
        private static bool Usable(ConquerItem i) {return i!=null && i.Position==0 && i.Warehouse==0 && i.Lock==0 && !i.Suspicious && !i.InWardrobe && i.Days==0 && i.TimeLeftInMinutes==0;}
        private static List<Take> Select(GameState p,Run r) {
            var takes=new List<Take>();int remaining=r.Amount;
            foreach(var item in p.Inventory.Objects.Where(i=>i.ID==r.Item && Usable(i)).OrderByDescending(i=>i.Bound).ThenBy(i=>i.UID)) {
                int count=Math.Min(remaining,Math.Max(1,(int)item.StackSize));takes.Add(new Take{Item=item,Count=count});remaining-=count;if(remaining==0)return takes;
            }return null;
        }
        private static void Bag(NpgsqlConnection c,NpgsqlTransaction t,GameState p) {
            var ids=new List<uint>();using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid","uid",(long)p.Entity.UID))
            using(var r=q.ExecuteReader())while(r.Read()){if(ids.Count>=40)throw new InvalidOperationException("Rare Materials bag exceeds capacity.");ids.Add(Convert.ToUInt32(r[0]));}
            if(!ids.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("Rare Materials bag requires reload.");
        }
        private static void Debit(NpgsqlConnection c,NpgsqlTransaction t,GameState p,Take take) {
            var i=take.Item;string sql=take.Removed?"DELETE FROM items":"UPDATE items SET stacksize=stacksize-@count";
            if(Q.Execute(c,t,sql+@" WHERE uid=@item AND id=@type AND entityid=@uid AND position=0 AND warehouse=0 AND stacksize=@stack AND bound=@bound
AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')='0'", "item",(long)i.UID,"type",(int)i.ID,"uid",(long)p.Entity.UID,
                "stack",(int)i.StackSize,"bound",i.Bound?1:0,"count",take.Count)!=1)throw new InvalidOperationException("Rare Materials gem ownership changed.");
        }
        private static string Complete(GameState p,DateTime now,bool paid) {
            Run r;List<Take> takes;ConquerItem reward,rune=null;uint cp,rvp;ushort study;ulong autoHunt;ExperienceRewardPlan plan;ExperienceRewards.Effects effects;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                Owner(c,t,p);r=Read(c,t,p,now);if(!Current(r,now) || !Offered(p,r,now,false))return "Let us review your commission first.";
                if(p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Cursed))return "Return when you can receive your experience reward.";
                uint fee=paid?r.SkipFee:0;if(p.Entity.ConquerPoints<fee)return "You do not have enough CPs to finish this commission.";
                takes=paid?new List<Take>():Select(p,r);if(takes==null)return "Bring all the required normal gems, unlocked in your bag.";
                if(p.Inventory.Count-takes.Count(i=>i.Removed)+(r.RuneReward?2:1)>40)return r.RuneReward?"Leave two spaces for your Chi Token and bound rune.":"Leave one space for your Chi Token.";
                ulong next=(ulong)p.Entity.ConquerPoints-fee+r.Cp;int totalStudy=p.Entity.SubClasses.StudyPoints+Study(r);
                if(next>uint.MaxValue || (ulong)p.Entity.RevoraPoint+r.Rvp>uint.MaxValue || totalStudy>ushort.MaxValue)return "Spend some points before collecting your reward.";
                if(r.Import)Insert(c,t,p,r);Bag(c,t,p);cp=(uint)next;rvp=p.Entity.RevoraPoint+r.Rvp;study=(ushort)totalStudy;
                ulong award=p.Entity.Level>=Config.LevelCap?0:p.ExpBall;
                plan=new ExperienceRewardPlan(CharacterProgression.Capture(p),award,Config.LevelCap,Config.AttributePointsPerLevel,ReincarnationRecovery.Snapshot(p));
                autoHunt=p.Entity.InAutoHunt?checked(p.Entity.AutoHuntEXP+award/Math.Max(1u,Config.AutoHuntExperienceDivisor)):p.Entity.AutoHuntEXP;
                CharacterProgression.Persist(c,t,p,plan.After);
                if(Q.Execute(c,t,@"UPDATE entities SET conquerpoints=@cp,revorapoint=@rvp,studypoints=@study WHERE uid=@uid
AND COALESCE(conquerpoints,0)=@oldcp AND COALESCE(revorapoint,0)=@oldrvp AND COALESCE(studypoints,0)=@oldstudy", "cp",(long)cp,"rvp",(long)rvp,"study",(int)study,
                    "uid",(long)p.Entity.UID,"oldcp",(long)p.Entity.ConquerPoints,"oldrvp",(long)p.Entity.RevoraPoint,"oldstudy",(int)p.Entity.SubClasses.StudyPoints)!=1)
                    throw new InvalidOperationException("Rare Materials reward balance changed.");
                foreach(var take in takes.OrderBy(i=>i.Item.UID))Debit(c,t,p,take);reward=Q.New(r.Chi,takes.Any(i=>i.Item.Bound));
                if(Q.Execute(c,t,@"INSERT INTO items(uid,id,entityid,position,warehouse,stacksize,bound,durability,maximdurability)
VALUES(@item,@type,@uid,0,0,1,@bound,@dur,@max)", "item",(long)reward.UID,"type",(int)reward.ID,"uid",(long)p.Entity.UID,"bound",reward.Bound?1:0,
                    "dur",(int)reward.Durability,"max",(int)reward.MaximDurability)!=1)throw new InvalidOperationException("Rare Materials reward item was not saved.");
                if(r.RuneReward){rune=Q.New(4030101,true);Q.Insert(c,t,p.Entity.UID,rune);}
                effects=ExperienceRewards.Persist(c,t,p,plan);ReincarnationRecovery.Consume(c,t,p,plan.Restored);
                if(Q.Execute(c,t,@"UPDATE revora_rare_runs SET completed=true,paid_cp=@fee,debited_ids=@ids,debited_counts=@counts,reward_uid=@reward,soul_uid=@soul,rune_uid=@rune,experience=@exp,completed_at=@now
WHERE uid=@uid AND day=@day AND quest=@quest AND changes=@changes AND stars=@stars AND completed=false", "fee",(long)fee,"ids",takes.Select(i=>(long)i.Item.UID).ToArray(),
                    "counts",takes.Select(i=>i.Count).ToArray(),"reward",(long)reward.UID,"soul",DBNull.Value,"rune",rune==null?(object)DBNull.Value:(long)rune.UID,"exp",checked((long)award),"now",now,"uid",(long)p.Entity.UID,"day",r.Day,
                    "quest",(int)r.Quest,"changes",(int)r.Changes,"stars",(int)r.Stars)!=1)throw new InvalidOperationException("Rare Materials completion was not saved.");
                StudyRewardEvidence.Record(c,t,p,Study(r),"rare:"+r.Day);
                if(!Near(p))throw new InvalidOperationException("Rare Materials session changed.");t.Commit();
            }}
            Q.Install(p,takes.Where(i=>i.Removed).Select(i=>i.Item.UID),rune==null?new[]{reward}:new[]{reward,rune});
            foreach(var take in takes.Where(i=>!i.Removed)){take.Item.StackSize-=(ushort)take.Count;take.Item.Mode=Enums.ItemMode.Update;take.Item.Send(p);take.Item.Mode=Enums.ItemMode.Default;}
            p.Entity.ApplyCommittedConquerPoints(cp);p.Entity.ApplyCommittedRevoraPoints(rvp);p.Entity.SubClasses.StudyPoints=study;
            ReincarnationRecovery.Committed(p,plan.Restored);MentorExperience.Apply(p,effects.MentorCredit);p.ApplyExperienceReward(plan,effects,autoHunt,true);
            r.Done=true;Projection(p,r,now);Q.Refresh(p);p.Send(new SubClassShowFull(0){ID=8,Study=study,StudyReceive=(ushort)Study(r)}.ToArray());
            return "Your commission is settled. Take your reward, and return tomorrow.";
        }
        internal static bool HandleNpc(GameState p,NpcRequest request) {
            if(p==null || request==null || p.ActiveNpc!=DailyQuestEnvoySystem.NpcId)return false;
            byte choice=request.OptionID;if(choice!=19 && choice!=28 && choice!=29 && choice!=30 && choice!=32 && choice!=33 && choice!=34){offers.Remove(p);return false;}
            if(!Ready(p))return true;
            lock(p.ItemSyncRoot)lock(MentorExperience.Sync)try {
                if(!Near(p) || request.NpcID!=0 && request.NpcID!=p.ActiveNpc)return true;
                var d=new Npcs(p);d.Avatar(Config.DailyEnvoyAvatar);DateTime now=Clock();
                if(p.Entity.Level<Config.DailyRareMinLevel)d.Text("Return at level "+Config.DailyRareMinLevel+" for a commission.");
                else if(choice==19)Show(p,d,Accept(p,now),now,false);
                else if(choice==29 || choice==34)d.Text(Change(p,now,choice==34));
                else if(choice==28 || choice==30)d.Text(Complete(p,now,choice==30));
                else using(var c=DataHolder.MySqlConnection){c.Open();var r=Read(c,null,p,now);
                    if(choice==32)Show(p,d,r,now,true);
                    else d.Text(Current(r,now)?"This commission earns "+r.Cp+" CPs, "+r.Rvp+" RVPs, "+Study(r)+" study points, one EXP Ball of experience and one Chi Token"+(r.RuneReward?", and a bound Tortoise Breaker rune.":"."):"Ask me for today's commission first.");
                }
                d.Option("Review my commission.",19);d.Option("Farewell.",255);d.Send();
            }catch(Exception ex){offers.Remove(p);Q.Fail(p,"daily-rare-quest",ex);}return true;
        }
    }
}
