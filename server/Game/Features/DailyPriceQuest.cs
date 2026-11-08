using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class DailyPriceQuest
    {
        internal const uint ListItem = 729087;
        internal static Func<DateTime> Clock = () => DateTime.UtcNow;
        internal static Func<int, int> Roll = max => Kernel.Random.Next(max);
        private static bool enabled;
        private sealed class Run {
            internal string Day; internal bool Done, Import, RuneReward;
            internal byte Quest, Changes, Amount, Maximum, Pool;
            internal uint Item, Cp, Rvp, Chi, ChangeFee, SkipBase, SkipStep;
            internal ushort Study;
        }
        private sealed class Offer { internal string Day; internal byte Quest, Changes; internal DateTime End; internal ConquerItem List; }
        private sealed class Take { internal ConquerItem Item; internal int Count; internal bool Removed { get { return Count >= Math.Max(1, (int)Item.StackSize); } } }
        private static readonly ConditionalWeakTable<GameState, Offer> offers = new ConditionalWeakTable<GameState, Offer>();
        private static readonly ConditionalWeakTable<GameState, Offer> trips = new ConditionalWeakTable<GameState, Offer>();
        private static readonly ushort[] maps = {0,1015,1020,1000,1011}, xs = {0,396,608,611,212}, ys = {0,528,556,636,372};
        private static readonly string[] monsters = {"", "BanditL97 / BanditL98", "Macaque / MacaqueL48", "SandMonster / SandMonsterL68", "Bandit / BanditL33"};
        private static readonly string[] locations = {"", "Bird Island", "Ape City", "Desert City", "Phoenix City"};
        internal static string Day(DateTime now) { return MagnoliaQuest.Day(now); }
        internal static void Initialize() {
            ConquerItemBaseInformation rune;
            if(!ConquerItemInformation.BaseInformations.TryGetValue(4030101,out rune) || rune.Class!=0)
                throw new InvalidOperationException("Daily quest yellow rune catalog is missing or class restricted.");
            if (Config.DailyPriceListItem != ListItem || !ConquerItemInformation.BaseInformations.ContainsKey(ListItem))
                throw new InvalidOperationException("Daily hunt list differs from the supported catalog identity.");
            using (var c = DataHolder.MySqlConnection) { c.Open(); Q.Execute(c, null, @"CREATE TABLE IF NOT EXISTS revora_price_runs(
uid bigint NOT NULL CHECK(uid>0),day varchar(8) NOT NULL,completed boolean NOT NULL DEFAULT false,legacy boolean NOT NULL DEFAULT false,
quest integer NOT NULL CHECK(quest BETWEEN 1 AND 4),changes integer NOT NULL CHECK(changes BETWEEN 0 AND 255),
item bigint NOT NULL,amount integer NOT NULL CHECK(amount BETWEEN 1 AND 255),cp bigint NOT NULL,rvp bigint NOT NULL,chi bigint NOT NULL,study integer NOT NULL,
change_fee bigint NOT NULL,skip_base bigint NOT NULL,skip_step bigint NOT NULL,maximum integer NOT NULL,pool integer NOT NULL CHECK(pool BETWEEN 1 AND 4),
reroll_fees bigint NOT NULL DEFAULT 0,skip_fee bigint NOT NULL DEFAULT 0,lists bigint[] NOT NULL DEFAULT '{}',
debited_ids bigint[] NOT NULL DEFAULT '{}',debited_counts integer[] NOT NULL DEFAULT '{}',reward_uid bigint,experience bigint NOT NULL DEFAULT 0,
created_at timestamptz NOT NULL DEFAULT now(),completed_at timestamptz,PRIMARY KEY(uid,day));
ALTER TABLE revora_price_runs ADD COLUMN IF NOT EXISTS valuable_reward boolean NOT NULL DEFAULT false;
ALTER TABLE revora_price_runs ADD COLUMN IF NOT EXISTS soul_uid bigint UNIQUE;
ALTER TABLE revora_price_runs ADD COLUMN IF NOT EXISTS rune_reward boolean NOT NULL DEFAULT false;
ALTER TABLE revora_price_runs ADD COLUMN IF NOT EXISTS rune_uid bigint UNIQUE CHECK(rune_uid>0);"); } enabled = true;
        }
        private static uint Type(byte quest) { return new[] {0u,Config.DailyPriceBanditBlade,Config.DailyPriceBloodyTeeth,Config.DailyPriceBatBlood,Config.DailyPriceBladeScrap}[quest]; }
        private static byte Amount(byte quest) { return new byte[] {0,Config.DailyPriceBanditAmount,Config.DailyPriceBloodyTeethAmount,Config.DailyPriceBatBloodAmount,Config.DailyPriceBladeScrapAmount}[quest]; }
        private static void Assign(Run r, byte quest) {
            r.Quest = quest; r.Item = Type(quest); r.Amount = Amount(quest);
            if (r.Amount == 0 || !ConquerItemInformation.BaseInformations.ContainsKey(r.Item)) throw new InvalidOperationException("Daily hunt assignment is invalid.");
        }
        private static Run NewRun(string day, byte quest) {
            var r = new Run { Day=day,Cp=Config.DailyQuestCp,Rvp=Config.DailyQuestRevoraPoints,Chi=Config.DailyQuestChiToken,
                Study=Config.DailyPriceQuestStudy,ChangeFee=Config.DailyPriceChangeCp,SkipBase=Config.DailyPriceSkipCpFlat,
                SkipStep=Config.DailyPriceSkipCpPerChange,Maximum=Config.DailyPriceChangeMax,Pool=(byte)(Math.Max(2,Math.Min(5,Config.DailyPriceRollExclusive))-1) };
            Assign(r,quest); return r;
        }
        private static byte Choose(int pool, byte avoid) {
            var choices = Enumerable.Range(1,pool).Where(i=>i!=avoid).ToArray();
            return choices.Length==0 ? (byte)0 : (byte)choices[Roll(choices.Length)];
        }
        private static Run Read(NpgsqlConnection c, NpgsqlTransaction t, GameState p, DateTime now) {
            using (var q=Q.Command(c,t,@"SELECT day,completed,quest,changes,item,amount,cp,rvp,chi,study,change_fee,skip_base,skip_step,maximum,pool,valuable_reward,rune_reward
FROM revora_price_runs WHERE uid=@uid ORDER BY day DESC LIMIT 1", "uid",(long)p.Entity.UID))
            using (var d=q.ExecuteReader()) if(d.Read()) return new Run {Day=d.GetString(0),Done=d.GetBoolean(1),Quest=Convert.ToByte(d[2]),Changes=Convert.ToByte(d[3]),
                Item=Convert.ToUInt32(d[4]),Amount=Convert.ToByte(d[5]),Cp=Convert.ToUInt32(d[6]),Rvp=Convert.ToUInt32(d[7]),Chi=Convert.ToUInt32(d[8]),Study=Convert.ToUInt16(d[9]),
                ChangeFee=Convert.ToUInt32(d[10]),SkipBase=Convert.ToUInt32(d[11]),SkipStep=Convert.ToUInt32(d[12]),Maximum=Convert.ToByte(d[13]),Pool=Convert.ToByte(d[14]),RuneReward=d.GetBoolean(16)};
            // Only saved legacy state with today's reset date can seed the new ledger.
            using(var q=Q.Command(c,t,@"SELECT e.lastresettime,d.evreyThink,d.pricequestid,d.pricetimes,d.priceitemid,d.priceamount
FROM entities e LEFT JOIN dialyquests d ON d.entityid=e.uid WHERE e.uid=@uid", "uid",(long)p.Entity.UID))
            using(var d=q.ExecuteReader()) if(d.Read() && !d.IsDBNull(0) && !d.IsDBNull(1)
                && DateTime.FromBinary(Convert.ToInt64(d[0])).Date==now.ToLocalTime().Date) {
                bool done=Convert.ToBoolean(d[1]);byte quest=d.IsDBNull(2) ? (byte)0 : Convert.ToByte(d[2]);
                if(done || quest>=1 && quest<=4 && !d.IsDBNull(4) && !d.IsDBNull(5) && Convert.ToUInt32(d[4])==Type(quest) && Convert.ToByte(d[5])>0) {
                    var r=NewRun(Day(now),quest>=1 && quest<=4 ? quest : (byte)1);r.Done=done;r.Import=true;r.Changes=d.IsDBNull(3) ? (byte)0 : Convert.ToByte(d[3]);
                    if(!done){r.Item=Convert.ToUInt32(d[4]);r.Amount=Convert.ToByte(d[5]);}return r;
                }
            }
            return null;
        }
        private static void Insert(NpgsqlConnection c,NpgsqlTransaction t,GameState p,Run r) {
            r.RuneReward=!r.Import;
            if(Q.Execute(c,t,@"INSERT INTO revora_price_runs(uid,day,completed,legacy,quest,changes,item,amount,cp,rvp,chi,study,change_fee,skip_base,skip_step,maximum,pool,valuable_reward,rune_reward)
VALUES(@uid,@day,@done,@legacy,@quest,@changes,@item,@amount,@cp,@rvp,@chi,@study,@change,@base,@step,@max,@pool,@valuable,@rune)",
                "uid",(long)p.Entity.UID,"day",r.Day,"done",r.Done,"legacy",r.Import,"quest",(int)r.Quest,"changes",(int)r.Changes,"item",(long)r.Item,"amount",(int)r.Amount,
                "cp",(long)r.Cp,"rvp",(long)r.Rvp,"chi",(long)r.Chi,"study",(int)r.Study,"change",(long)r.ChangeFee,"base",(long)r.SkipBase,
                "step",(long)r.SkipStep,"max",(int)r.Maximum,"pool",(int)r.Pool,"valuable",false,"rune",r.RuneReward)!=1)throw new InvalidOperationException("Daily hunt assignment was not saved.");
            r.Import=false;
        }
        private static bool Current(Run r,DateTime now) { return r!=null && r.Day==Day(now) && !r.Done; }
        private static bool Ready(GameState p) { return enabled && Q.Player(p) && CharacterProgression.Managed(p) && p.Spells!=null && p.Entity.SubClasses!=null && p.Booth==null && p.Trade!=null && !p.Trade.InTrade; }
        private static bool Near(GameState p) { return Ready(p) && Q.Near(p,DailyQuestEnvoySystem.NpcId,1002) && p.Map.BaseID==1002; }
        private static void Owner(NpgsqlConnection c,NpgsqlTransaction t,GameState p) {
            using(var q=Q.Command(c,t,"SELECT level FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID)) {
                object level=q.ExecuteScalar();if(level==null || Convert.ToInt32(level)<Config.DailyPriceMinLevel)throw new InvalidOperationException("Daily hunt eligibility changed.");
            }
        }
        private static void Bag(NpgsqlConnection c,NpgsqlTransaction t,GameState p) {
            var ids=new List<uint>();using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid","uid",(long)p.Entity.UID))
            using(var r=q.ExecuteReader())while(r.Read()){if(ids.Count>=40)throw new InvalidOperationException("Daily hunt bag exceeds capacity.");ids.Add(Convert.ToUInt32(r[0]));}
            if(!ids.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("Daily hunt bag requires reload.");
        }
        private static bool Usable(ConquerItem item) { return item!=null && item.Position==0 && item.Warehouse==0 && item.Lock==0 && !item.Suspicious && !item.InWardrobe && item.Days==0 && item.TimeLeftInMinutes==0; }
        private static void InsertItem(NpgsqlConnection c,NpgsqlTransaction t,GameState p,ConquerItem item) {
            if(Q.Execute(c,t,@"INSERT INTO items(uid,id,entityid,position,warehouse,stacksize,bound,durability,maximdurability)
VALUES(@item,@type,@uid,0,0,1,@bound,@dur,@max)","item",(long)item.UID,"type",(int)item.ID,"uid",(long)p.Entity.UID,
                "bound",item.Bound?1:0,"dur",(int)item.Durability,"max",(int)item.MaximDurability)!=1)throw new InvalidOperationException("Daily hunt item was not saved.");
        }
        private static void Projection(GameState p,Run r,DateTime now) {
            bool current=r!=null && string.CompareOrdinal(r.Day,Day(now))>=0;
            p.Entity.EvreyThink=current && r.Done;p.Entity.PriceQuestID=current ? r.Quest : (byte)0;p.Entity.PriceTimes=current ? r.Changes : (byte)0;
            p.Entity.PriceItemId=current && !r.Done ? r.Item : 0;p.Entity.Priceamount=current && !r.Done ? r.Amount : (byte)0;
            p.Entity.PriceName=current && !r.Done ? ConquerItemInformation.BaseInformations[r.Item].Name : "";
            p.Entity.PriceMonsterName=current && !r.Done ? monsters[r.Quest] : "";p.Entity.PriceLoction=current && !r.Done ? locations[r.Quest] : "";
            if(p.Entity.EvreyThink)QuestListSystem.MarkCompleted(p,QuestListSystem.DailyEnvoy);
            else if(current)QuestListSystem.MarkAccepted(p,QuestListSystem.DailyEnvoy);else QuestListSystem.MarkAvailable(p,QuestListSystem.DailyEnvoy);
        }
        internal static void Refresh(GameState p) {
            if(!enabled || !Q.Player(p,false))return;
            lock(p.ItemSyncRoot)try {DateTime now=Clock();using(var c=DataHolder.MySqlConnection){c.Open();Projection(p,Read(c,null,p,now),now);}}
            catch(Exception ex){Q.Fail(p,"daily-price-refresh",ex);}
        }
        private static Run Accept(GameState p,DateTime now,bool begin) {
            ConquerItem list=null;Run r;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                Owner(c,t,p);r=Read(c,t,p,now);
                if(r!=null && r.Import)Insert(c,t,p,r);
                if(r==null || string.CompareOrdinal(r.Day,Day(now))<0) {
                    if(!begin)return null;
                    r=NewRun(Day(now),1);Assign(r,Choose(r.Pool,0));
                    if(!p.Inventory.Objects.Any(i=>i.ID==ListItem && Usable(i)) && p.Inventory.Count>=40){p.Send("Leave one space for your hunting list.");return null;}
                    Insert(c,t,p,r);
                }
                if(Current(r,now) && !p.Inventory.Objects.Any(i=>i.ID==ListItem && Usable(i))) {
                    if(p.Inventory.Count>=40){p.Send("Leave one space for your hunting list.");t.Commit();Projection(p,r,now);return r;}
                    Bag(c,t,p);list=Q.New(ListItem,false);InsertItem(c,t,p,list);
                    if(Q.Execute(c,t,"UPDATE revora_price_runs SET lists=array_append(lists,@list) WHERE uid=@uid AND day=@day AND completed=false", "list",(long)list.UID,"uid",(long)p.Entity.UID,"day",r.Day)!=1)
                        throw new InvalidOperationException("Daily hunt list was not recorded.");
                }
                t.Commit();
            }}
            if(list!=null)Q.Install(p,new uint[0],new[]{list});Projection(p,r,now);return r;
        }
        private static ulong ChangeCost(Run r) { return (ulong)(r.Changes+1)*r.ChangeFee; }
        private static ulong SkipCost(Run r) { return (ulong)r.Changes*r.SkipStep+r.SkipBase; }
        private static bool Offered(GameState p,Run r,DateTime now) {
            Offer offer;bool valid=offers.TryGetValue(p,out offer) && offer.End>now && offer.Day==Day(now) && r!=null && offer.Day==r.Day && offer.Quest==r.Quest && offer.Changes==r.Changes;
            offers.Remove(p);return valid;
        }
        private static void Show(GameState p,Npcs d,Run r,DateTime now) {
            offers.Remove(p);
            if(!Current(r,now)){d.Text("That hunt is closed for today. Return tomorrow.");return;}
            offers.Add(p,new Offer{Day=r.Day,Quest=r.Quest,Changes=r.Changes,End=now.AddMinutes(2)});
            d.Text("Bring "+r.Amount+" "+ConquerItemInformation.BaseInformations[r.Item].Name+" from "+monsters[r.Quest]+" in "+locations[r.Quest]+".");
            d.Option("Hand over the materials.",11);
            if(r.Changes<r.Maximum && r.Pool>1)d.Option("Change the hunt: "+ChangeCost(r)+" CPs.",12);
            d.Option("Finish for "+SkipCost(r)+" CPs.",13);d.Option("Replace my lost list.",14);d.Option("What is my reward?",15);
        }
        private static string Change(GameState p,DateTime now) {
            Run r;uint cp;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                Owner(c,t,p);r=Read(c,t,p,now);
                if(!Current(r,now) || !Offered(p,r,now))return "Let us review your current hunt first.";
                if(r.Changes>=r.Maximum || r.Pool<=1)return "I have no more changes for this hunt.";
                ulong fee=ChangeCost(r);if(fee>p.Entity.ConquerPoints)return "You do not have enough CPs to change this hunt.";
                if(r.Import)Insert(c,t,p,r);byte old=r.Quest;Assign(r,Choose(r.Pool,old));
                cp=p.Entity.ConquerPoints-(uint)fee;
                if(Q.Execute(c,t,"UPDATE entities SET conquerpoints=@cp WHERE uid=@uid AND COALESCE(conquerpoints,0)=@old", "cp",(long)cp,"uid",(long)p.Entity.UID,"old",(long)p.Entity.ConquerPoints)!=1)
                    throw new InvalidOperationException("Daily hunt CP preimage changed.");
                if(Q.Execute(c,t,@"UPDATE revora_price_runs SET quest=@quest,item=@item,amount=@amount,changes=changes+1,reroll_fees=reroll_fees+@fee
WHERE uid=@uid AND day=@day AND quest=@old AND changes=@changes AND completed=false", "quest",(int)r.Quest,"item",(long)r.Item,"amount",(int)r.Amount,
                    "fee",checked((long)fee),"uid",(long)p.Entity.UID,"day",r.Day,"old",(int)old,"changes",(int)r.Changes)!=1)throw new InvalidOperationException("Daily hunt change was not saved.");
                if(!Near(p))throw new InvalidOperationException("Daily hunt session changed.");t.Commit();r.Changes++;
            }}
            trips.Remove(p);p.Entity.ApplyCommittedConquerPoints(cp);Projection(p,r,now);return "I have written a new hunt. Check your list before setting out.";
        }
        private static List<Take> Select(GameState p,uint type,int amount) {
            var takes=new List<Take>();
            foreach(var item in p.Inventory.Objects.Where(i=>i.ID==type && Usable(i)).OrderByDescending(i=>i.Bound).ThenBy(i=>i.UID)) {
                int count=Math.Min(amount,Math.Max(1,(int)item.StackSize));takes.Add(new Take{Item=item,Count=count});amount-=count;if(amount==0)return takes;
            }
            return null;
        }
        private static void Debit(NpgsqlConnection c,NpgsqlTransaction t,GameState p,Take take) {
            var i=take.Item;string sql=take.Removed ? "DELETE FROM items" : "UPDATE items SET stacksize=stacksize-@count";
            if(Q.Execute(c,t,sql+@" WHERE uid=@item AND id=@type AND entityid=@uid AND position=0 AND warehouse=0 AND stacksize=@stack AND bound=@bound
AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')='0'", "item",(long)i.UID,"type",(int)i.ID,"uid",(long)p.Entity.UID,
                "stack",(int)i.StackSize,"bound",i.Bound?1:0,"count",take.Count)!=1)throw new InvalidOperationException("Daily hunt material ownership changed.");
        }
        private static string Complete(GameState p,DateTime now,bool paid) {
            Run r;List<Take> takes;ConquerItem reward,rune=null;uint cp,rvp;ushort study;ulong autoHunt;
            ExperienceRewardPlan plan;ExperienceRewards.Effects effects;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                Owner(c,t,p);r=Read(c,t,p,now);
                if(!Current(r,now) || !Offered(p,r,now))return "Let us review your current hunt first.";
                if(p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Cursed))return "Return when you can receive your experience reward.";
                ulong fee=paid ? SkipCost(r) : 0;if(fee>p.Entity.ConquerPoints)return "You do not have enough CPs to finish this hunt.";
                takes=paid ? new List<Take>() : Select(p,r.Item,r.Amount);
                if(takes==null)return "Bring all the materials on your list, unlocked in your bag.";
                var lists=Select(p,ListItem,1);if(lists!=null)takes.AddRange(lists);
                if(p.Inventory.Count-takes.Count(i=>i.Removed)+(r.RuneReward?2:1)>40)return r.RuneReward?"Leave two spaces for your Chi Token and bound rune.":"Leave one space for your Chi Token.";
                ulong next=(ulong)p.Entity.ConquerPoints-fee+r.Cp;
                if(next>uint.MaxValue || (ulong)p.Entity.RevoraPoint+r.Rvp>uint.MaxValue || (int)p.Entity.SubClasses.StudyPoints+r.Study>ushort.MaxValue)
                    return "Spend some points before collecting this reward.";
                if(r.Import)Insert(c,t,p,r);Bag(c,t,p);
                cp=(uint)next;rvp=p.Entity.RevoraPoint+r.Rvp;study=(ushort)(p.Entity.SubClasses.StudyPoints+r.Study);
                ulong award=p.Entity.Level>=Config.LevelCap ? 0 : p.ExpBall;
                plan=new ExperienceRewardPlan(CharacterProgression.Capture(p),award,Config.LevelCap,Config.AttributePointsPerLevel,ReincarnationRecovery.Snapshot(p));
                autoHunt=p.Entity.InAutoHunt ? checked(p.Entity.AutoHuntEXP+award/Math.Max(1u,Config.AutoHuntExperienceDivisor)) : p.Entity.AutoHuntEXP;
                CharacterProgression.Persist(c,t,p,plan.After);
                if(Q.Execute(c,t,@"UPDATE entities SET conquerpoints=@cp,revorapoint=@rvp,studypoints=@study WHERE uid=@uid
AND COALESCE(conquerpoints,0)=@oldcp AND COALESCE(revorapoint,0)=@oldrvp AND COALESCE(studypoints,0)=@oldstudy", "cp",(long)cp,"rvp",(long)rvp,"study",(int)study,
                    "uid",(long)p.Entity.UID,"oldcp",(long)p.Entity.ConquerPoints,"oldrvp",(long)p.Entity.RevoraPoint,"oldstudy",(int)p.Entity.SubClasses.StudyPoints)!=1)
                    throw new InvalidOperationException("Daily hunt reward balance changed.");
                foreach(var take in takes.OrderBy(i=>i.Item.UID))Debit(c,t,p,take);
                reward=Q.New(r.Chi,takes.Any(i=>i.Item.Bound));InsertItem(c,t,p,reward);
                if(r.RuneReward){rune=Q.New(4030101,true);Q.Insert(c,t,p.Entity.UID,rune);}
                effects=ExperienceRewards.Persist(c,t,p,plan);ReincarnationRecovery.Consume(c,t,p,plan.Restored);
                if(Q.Execute(c,t,@"UPDATE revora_price_runs SET completed=true,skip_fee=@fee,debited_ids=@ids,debited_counts=@counts,reward_uid=@reward,soul_uid=@soul,rune_uid=@rune,experience=@exp,completed_at=@now
WHERE uid=@uid AND day=@day AND quest=@quest AND changes=@changes AND completed=false", "fee",checked((long)fee),"ids",takes.Select(i=>(long)i.Item.UID).ToArray(),
                    "counts",takes.Select(i=>i.Count).ToArray(),"reward",(long)reward.UID,"soul",DBNull.Value,"rune",rune==null?(object)DBNull.Value:(long)rune.UID,"exp",checked((long)award),"now",now,"uid",(long)p.Entity.UID,"day",r.Day,
                    "quest",(int)r.Quest,"changes",(int)r.Changes)!=1)throw new InvalidOperationException("Daily hunt completion was not saved.");
                StudyRewardEvidence.Record(c,t,p,r.Study,"price:"+r.Day);
                if(!Near(p))throw new InvalidOperationException("Daily hunt session changed.");t.Commit();
            }}
            Q.Install(p,takes.Where(i=>i.Removed).Select(i=>i.Item.UID),rune==null?new[]{reward}:new[]{reward,rune});
            foreach(var take in takes.Where(i=>!i.Removed)){take.Item.StackSize-=(ushort)take.Count;take.Item.Mode=Enums.ItemMode.Update;take.Item.Send(p);take.Item.Mode=Enums.ItemMode.Default;}
            p.Entity.ApplyCommittedConquerPoints(cp);p.Entity.ApplyCommittedRevoraPoints(rvp);p.Entity.SubClasses.StudyPoints=study;
            ReincarnationRecovery.Committed(p,plan.Restored);MentorExperience.Apply(p,effects.MentorCredit);p.ApplyExperienceReward(plan,effects,autoHunt,true);
            r.Done=true;Projection(p,r,now);trips.Remove(p);Q.Refresh(p);p.Send(new SubClassShowFull(0){ID=8,Study=study,StudyReceive=r.Study}.ToArray());
            return "The hunt is settled. Your reward is yours. Return tomorrow.";
        }
        internal static bool HandleNpc(GameState p,NpcRequest request) {
            if(p==null || request==null || p.ActiveNpc!=DailyQuestEnvoySystem.NpcId)return false;
            byte choice=request.OptionID;
            if(choice!=2 && (choice<11 || choice>15)){offers.Remove(p);return false;}
            if(!Ready(p))return true;
            lock(p.ItemSyncRoot)lock(MentorExperience.Sync)try {
                if(!Near(p) || request.NpcID!=0 && request.NpcID!=p.ActiveNpc)return true;
                var d=new Npcs(p);d.Avatar(Config.DailyEnvoyAvatar);DateTime now=Clock();
                if(p.Entity.Level<Config.DailyPriceMinLevel)d.Text("Return at level "+Config.DailyPriceMinLevel+" for a hunt.");
                else if(choice==2 || choice==14){var r=Accept(p,now,choice==2);if(r!=null)Show(p,d,r,now);else d.Text("Speak to me about a hunt first.");}
                else if(choice==12)d.Text(Change(p,now));
                else if(choice==11 || choice==13)d.Text(Complete(p,now,choice==13));
                else {using(var c=DataHolder.MySqlConnection){c.Open();var r=Read(c,null,p,now);d.Text(Current(r,now) ? "This hunt earns "+r.Cp+" CPs, "+r.Rvp+" RVPs, "+r.Study+" study points, one EXP Ball of experience and one Chi Token"+(r.RuneReward?", and a bound Tortoise Breaker rune.":".") : "Speak to me about today's hunt first.");}}
                d.Option("Review my hunt.",2);d.Option("Farewell.",255);d.Send();
            }catch(Exception ex){offers.Remove(p);Q.Fail(p,"daily-price-quest",ex);}return true;
        }
        internal static bool TryUse(ConquerItem list,GameState p) {
            if(list==null || list.ID!=ListItem)return false;if(!Ready(p))return true;
            lock(p.ItemSyncRoot)try {
                trips.Remove(p);ConquerItem owned;if(!Ready(p) || p.Entity.Level<Config.DailyPriceMinLevel || !Usable(list) || !p.Inventory.TryGetItem(list.UID,out owned) || !ReferenceEquals(owned,list))return true;
                DateTime now=Clock();Run r;using(var c=DataHolder.MySqlConnection){c.Open();r=Read(c,null,p,now);}
                if(!Current(r,now)){p.Send("Ask the Daily Quest Envoy for a current hunt.");return true;}
                trips.Add(p,new Offer{Day=r.Day,Quest=r.Quest,Changes=r.Changes,End=now.AddMinutes(2),List=list});
                var d=new Npcs(p);d.Text("Hunt "+monsters[r.Quest]+" in "+locations[r.Quest]+". Bring back "+r.Amount+" "+ConquerItemInformation.BaseInformations[r.Item].Name+".");
                d.Option("Take me there.",1);d.Option("Close the list.",255);p.ActiveNpc=ListItem;d.Send();
            }catch(Exception ex){Q.Fail(p,"daily-price-list",ex);}return true;
        }
        internal static void Travel(GameState p,NpcRequest request) {
            if(!Ready(p) || request==null)return;
            lock(p.ItemSyncRoot)try {
                Offer trip;ConquerItem owned;
                if(!Ready(p) || p.Entity.Level<Config.DailyPriceMinLevel || p.ActiveNpc!=ListItem || !trips.TryGetValue(p,out trip))return;trips.Remove(p);
                DateTime now=Clock();if(request.OptionID!=1 || request.NpcID!=0 && request.NpcID!=ListItem || trip.End<=now || trip.Day!=Day(now)
                    || !p.Inventory.TryGetItem(trip.List.UID,out owned) || !ReferenceEquals(owned,trip.List) || !Usable(owned) || owned.ID!=ListItem)return;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    Owner(c,t,p);Run r=Read(c,t,p,now);if(!Current(r,now) || r.Quest!=trip.Quest || r.Changes!=trip.Changes)return;
                    using(var q=Q.Command(c,t,@"SELECT EXISTS(SELECT 1 FROM items WHERE uid=@item AND entityid=@uid AND id=@type AND position=0 AND warehouse=0
AND stacksize=@stack AND bound=@bound AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')='0')", "item",(long)owned.UID,
                        "uid",(long)p.Entity.UID,"type",(int)ListItem,"stack",(int)owned.StackSize,"bound",owned.Bound?1:0))
                        if(!Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("Daily hunt list ownership changed.");t.Commit();
                }}
                Map map;MapsTable.MapInformation info;ushort id=maps[trip.Quest],x=xs[trip.Quest],y=ys[trip.Quest];
                if(!Kernel.Maps.TryGetValue(id,out map) || map.ID!=id || map.BaseID!=id || !MapsTable.MapInformations.TryGetValue(id,out info)
                    || info.ID!=id || info.BaseID!=id || info.Owner!=0 || !PotencyGuide.FindLanding(map,ref x,ref y)){p.Send("That hunting trail is closed for now.");return;}
                if(!Ready(p))return;p.ActiveNpc=0;p.Entity.Teleport(id,x,y);
            }catch(Exception ex){Q.Fail(p,"daily-price-travel",ex);}
        }
    }
}
