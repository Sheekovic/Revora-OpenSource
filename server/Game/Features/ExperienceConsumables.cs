using System;
using System.Globalization;
using System.Linq;
using System.Collections.Generic;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class DragonBallExperience
    {
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        private sealed class Offer {internal byte Level;internal DateTime At;}
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<GameState,Offer> offers=new System.Runtime.CompilerServices.ConditionalWeakTable<GameState,Offer>();
        private static bool enabled;
        internal static void Initialize(){using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_dragon_exp_uses(
uid bigint NOT NULL REFERENCES entities(uid),day integer NOT NULL,level_before smallint NOT NULL CHECK(level_before BETWEEN 1 AND 134),
paid smallint NOT NULL CHECK(paid IN(1,2,10)),experience bigint NOT NULL CHECK(experience>0),points integer NOT NULL CHECK(points=paid*50),
item_uids bigint[] NOT NULL,item_counts integer[] NOT NULL,used_at timestamptz NOT NULL,PRIMARY KEY(uid,day),
CHECK(cardinality(item_uids)=cardinality(item_counts) AND cardinality(item_uids) BETWEEN 1 AND 10));");}enabled=true;}
        private static int Day(DateTime now){var local=now.ToLocalTime();return local.Year*10000+local.Month*100+local.Day;}
        internal static byte Price(byte level){return level>=130?(byte)10:level>=110?(byte)2:(byte)1;}
        internal static ulong Award(byte level){
            if(level>=101 && level<=129)return High[level-101];
            if(level>=98 && level<=100)return 214351925;
            if(level>=96 && level<98)return 100801220;
            if(level>=93 && level<96)return 134266326;
            if(level>=90 && level<93)return 102959118;
            if(level>=87 && level<90)return 84097242;
            if(level>=79 && level<87)return 70404048;
            if(level>=74 && level<79)return 57533091;
            if(level>=69 && level<74)return 36822370;
            if(level>=63 && level<69)return 28860143;
            if(level>50 && level<63)return 27152909;
            return 13901336;
        }
        private static readonly ulong[] High={242910783,286050512,259627544,232767237,241888762,249478280,265126887,187446887,193715970,204416075,227337342,230562942,234594942,238626942,240239742,242658942,246690942,290290023,358712493,282274058,338728870,243884786,292661743,322122547,292661744,351194092,337146328,303431696,322122547};
        private static bool Ready(GameState p){return enabled && Q.Near(p,3825,1036) && CharacterProgression.Managed(p) && p.Spells!=null
            && !p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Cursed) && p.Entity.Level>0 && p.Entity.Level<135 && p.Entity.Level<Config.LevelCap;}
        private static bool Material(ConquerItem item){return item.ID==1088000 && item.Position==0 && item.Warehouse==0 && item.Lock==0
            && !item.Suspicious && !item.InWardrobe && item.Days==0 && item.TimeLeftInMinutes==0;}
        internal static void Handle(GameState p,byte option){
            if(!Q.Near(p,3825,1036) || option!=0 && option!=1)return;
            if(!Ready(p)){p.Send("Your training is beyond what I can teach right now.");return;}
            if(!SessionInstall.TryEnter(p.Entity.UID))return;
            try{lock(p.ItemSyncRoot)lock(MentorExperience.Sync){
                if(!Ready(p))return;DateTime now=Clock();byte level=p.Entity.Level,price=Price(level);ulong xp=Award(level);
                if(option==0){offers.Remove(p);offers.Add(p,new Offer{Level=level,At=now});var dialog=new Npcs(p);
                    dialog.Text("Bring me "+price+" DragonBalls, and I will grant you "+xp.ToString("N0",CultureInfo.InvariantCulture)+" experience. I can help you once each day.");
                    dialog.Option("Make the exchange.",1);dialog.Option("Perhaps later.",255);dialog.Avatar(67);dialog.Send();return;}
                Offer offer;if(!offers.TryGetValue(p,out offer) || offer.Level!=level || Day(offer.At)!=Day(now) || now<offer.At || now>offer.At.AddMinutes(2)){
                    p.Send("Speak with me again before we make the exchange.");return;}
                var debit=new List<Tuple<ConquerItem,int>>();int needed=price;
                foreach(var item in p.Inventory.Objects.Where(Material).OrderByDescending(i=>i.Bound).ThenBy(i=>i.UID)){
                    int take=Math.Min(needed,Math.Max(1,(int)item.StackSize));debit.Add(Tuple.Create(item,take));needed-=take;if(needed==0)break;
                }
                if(needed!=0){p.Send("Bring me "+price+" DragonBalls for this exchange.");return;}
                ushort points=EnlightenmentProgress.PrepareCredit(p,(ushort)(price*50));
                var plan=new ExperienceRewardPlan(CharacterProgression.Capture(p),xp,Config.LevelCap,Config.AttributePointsPerLevel,ReincarnationRecovery.Snapshot(p));
                ulong autoHunt=p.Entity.InAutoHunt?checked(p.Entity.AutoHuntEXP+xp/Math.Max(1u,Config.AutoHuntExperienceDivisor)):p.Entity.AutoHuntEXP;
                ExperienceRewards.Effects effects;int day=Day(now);
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    Q.Execute(c,t,"SET LOCAL lock_timeout='2s';SET LOCAL statement_timeout='5s'");
                    using(var q=Q.Command(c,t,"SELECT lastdragonballuse FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID)){
                        object legacy=q.ExecuteScalar();if(legacy==null)throw new InvalidOperationException("DragonBall exchange owner missing.");
                        if(legacy!=DBNull.Value && DateTime.FromBinary(Convert.ToInt64(legacy)).Date>=now.ToLocalTime().Date){p.Send("I have already helped you today. Return tomorrow.");return;}}
                    using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_dragon_exp_uses WHERE uid=@uid AND day>=@day)","uid",(long)p.Entity.UID,"day",day))
                        if((bool)q.ExecuteScalar()){p.Send("I have already helped you today. Return tomorrow.");return;}
                    foreach(var entry in debit){var item=entry.Item1;bool remove=entry.Item2==Math.Max(1,(int)item.StackSize);
                        string sql=remove?"DELETE FROM items":"UPDATE items SET stacksize=stacksize-@take";
                        if(Q.Execute(c,t,sql+@" WHERE uid=@item AND entityid=@uid AND id=1088000 AND position=0 AND warehouse=0
AND stacksize=@stack AND bound=@bound AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')='0'",
                            "take",entry.Item2,"item",(long)item.UID,"uid",(long)p.Entity.UID,"stack",(int)item.StackSize,"bound",item.Bound?1:0)!=1)throw new InvalidOperationException("DragonBall payment changed.");}
                    CharacterProgression.Persist(c,t,p,plan.After);effects=ExperienceRewards.Persist(c,t,p,plan);ReincarnationRecovery.Consume(c,t,p,plan.Restored);
                    Q.Execute(c,t,@"INSERT INTO revora_dragon_exp_uses(uid,day,level_before,paid,experience,points,item_uids,item_counts,used_at)
VALUES(@uid,@day,@level,@paid,@xp,@points,@items,@counts,@now)","uid",(long)p.Entity.UID,"day",day,"level",(int)level,"paid",(int)price,"xp",checked((long)xp),"points",price*50,
                        "items",debit.Select(i=>(long)i.Item1.UID).ToArray(),"counts",debit.Select(i=>i.Item2).ToArray(),"now",now);
                    Q.Execute(c,t,"UPDATE entities SET lastdragonballuse=@stamp WHERE uid=@uid","stamp",now.ToLocalTime().Ticks,"uid",(long)p.Entity.UID);
                    EnlightenmentProgress.PersistCredit(c,t,p,points);if(!Ready(p))throw new InvalidOperationException("DragonBall exchange session changed.");t.Commit();
                }}
                EnlightenmentProgress.InstallCredit(p,points);p.LastDragonBallUse=now.ToLocalTime();offers.Remove(p);
                Q.Install(p,debit.Where(i=>i.Item2==Math.Max(1,(int)i.Item1.StackSize)).Select(i=>i.Item1.UID).ToArray(),new ConquerItem[0]);
                foreach(var entry in debit)if(p.Inventory.ContainsUID(entry.Item1.UID)){var item=entry.Item1;item.StackSize=(ushort)(item.StackSize-entry.Item2);item.Mode=Enums.ItemMode.Update;item.Send(p);item.Mode=Enums.ItemMode.Default;}
                ReincarnationRecovery.Committed(p,plan.Restored);MentorExperience.Apply(p,effects.MentorCredit);p.ApplyExperienceReward(plan,effects,autoHunt,true);
                p.Send("Your DragonBalls have become experience. Return tomorrow.");
            }}catch(Exception ex){Q.Fail(p,"dragonball-experience",ex);}finally{SessionInstall.Exit(p.Entity.UID);}
        }
    }
    internal static class ExperienceConsumables
    {
        internal static Func<DateTime> Clock = () => DateTime.Now;
        internal static void Initialize() {
            CharacterProgression.Initialize();
            DragonBallExperience.Initialize();
            using (var c = DataHolder.MySqlConnection) { c.Open(); Q.Execute(c, null, @"CREATE TABLE IF NOT EXISTS revora_exp_ball_days(
uid bigint PRIMARY KEY CHECK(uid>0), day integer NOT NULL CHECK(day>0), used integer NOT NULL CHECK(used BETWEEN 0 AND 10));"); }
        }
        private static int Day(DateTime date) { return date.Year * 10000 + date.Month * 100 + date.Day; }
        // CharacterProgression.Persist already locks the owner row. This one-row
        // ledger is authoritative after importing the legacy allowance once.
        private static bool Allowance(NpgsqlConnection c, NpgsqlTransaction t, GameState p, DateTime now, out byte used) {
            int today = Day(now), day = 0, count = 0; used = 0;
            using (var q = Q.Command(c, t, "SELECT day,used FROM revora_exp_ball_days WHERE uid=@uid FOR UPDATE", "uid", (long)p.Entity.UID))
            using (var r = q.ExecuteReader()) if (r.Read()) { day = Convert.ToInt32(r[0]); count = Convert.ToInt32(r[1]); }
            if (day == 0) {
                using (var q = Q.Command(c, t, "SELECT expballs,lastresettime FROM entities WHERE uid=@uid", "uid", (long)p.Entity.UID))
                using (var r = q.ExecuteReader()) {
                    if (!r.Read()) throw new InvalidOperationException("EXP Ball owner is missing.");
                    var reset = r.IsDBNull(1) ? DateTime.MinValue : DateTime.FromBinary(Convert.ToInt64(r[1]));
                    if (reset.Date > now.Date) return false;
                    count = reset.Date == now.Date && !r.IsDBNull(0) ? Convert.ToInt32(r[0]) : 0;
                }
                if (count < 0 || count > 10) throw new InvalidOperationException("Legacy EXP Ball allowance is invalid.");
                if (count == 10) return false;
                if (Q.Execute(c, t, "INSERT INTO revora_exp_ball_days(uid,day,used) VALUES(@uid,@day,@count)",
                    "uid", (long)p.Entity.UID, "day", today, "count", count + 1) != 1)
                    throw new InvalidOperationException("EXP Ball allowance was not created.");
            } else {
                if (day > today || day == today && count >= 10) return false;
                int next = day < today ? 1 : count + 1;
                if (Q.Execute(c, t, "UPDATE revora_exp_ball_days SET day=@today,used=@next WHERE uid=@uid AND day=@day AND used=@count",
                    "uid", (long)p.Entity.UID, "today", today, "next", next, "day", day, "count", count) != 1)
                    throw new InvalidOperationException("EXP Ball allowance changed.");
                count = next - 1;
            }
            used = checked((byte)(count + 1));
            if (Q.Execute(c, t, "UPDATE entities SET expballs=@used WHERE uid=@uid", "used", (int)used, "uid", (long)p.Entity.UID) != 1)
                throw new InvalidOperationException("EXP Ball counter was not saved.");
            return true;
        }
        private static bool Ready(GameState p) {
            return Q.Player(p) && CharacterProgression.Managed(p) && p.Spells != null && p.Booth == null && p.Trade != null && !p.Trade.InTrade;
        }
        internal static bool TryUse(ConquerItem item, GameState p) {
            if (item == null) return false;
            bool standard = item.ID == 723700 || item.ID == 723911 || item.ID == 722136;
            if (!standard && item.ID != 723744 && item.ID != 722057) return false;
            if (!Ready(p)) return true;
            lock (p.ItemSyncRoot) lock (MentorExperience.Sync) try {
                ConquerItem owned; DateTime now = Clock();
                if (!Ready(p) || !p.Inventory.TryGetItem(item.UID, out owned) || !ReferenceEquals(owned, item)
                    || item.Position != 0 || item.Warehouse != 0 || item.Lock != 0 || item.Suspicious || item.InWardrobe
                    || (item.Days != 0 && now >= item.DayStamp.AddDays(item.Days))) return true;
                if (p.Entity.Level >= Config.LevelCap || standard && p.Entity.Level >= 137
                    || p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Cursed)
                    || Config.TrainingGroundBlocksExperience && p.Map.BaseID == 1039) {
                    p.Send("Keep this EXP Ball until you can receive its experience."); return true;
                }
                ulong award = standard ? p.ExpBall : (DataHolder.LevelExperience(p.Entity.Level) / 100) * 10;
                if (award == 0) return true;
                var plan = new ExperienceRewardPlan(CharacterProgression.Capture(p), award, Config.LevelCap,
                    Config.AttributePointsPerLevel, ReincarnationRecovery.Snapshot(p), !standard || !item.Bound);
                ulong autoHunt = p.Entity.InAutoHunt ? checked(p.Entity.AutoHuntEXP + award / Math.Max(1u, Config.AutoHuntExperienceDivisor)) : p.Entity.AutoHuntEXP;
                ushort enlightenment = EnlightenmentProgress.PrepareCredit(p,10);
                byte used = p.ExpBalls; ExperienceRewards.Effects effects;
                using (var c = DataHolder.MySqlConnection) { c.Open(); using (var t = c.BeginTransaction()) {
                    CharacterProgression.Persist(c, t, p, plan.After);
                    if (standard && !Allowance(c, t, p, now, out used)) { p.Send(Constants.ExpBallsUsed); return true; }
                    string debit = item.StackSize <= 1 ? "DELETE FROM items" : "UPDATE items SET stacksize=stacksize-1";
                    if (Q.Execute(c, t, debit + @" WHERE uid=@item AND entityid=@uid AND id=@type AND warehouse=0 AND position=0
AND stacksize=@stack AND bound=@bound AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0
AND COALESCE(NULLIF(days,''),'0')=@days AND (@permanent OR daystamp=@stamp)",
                        "item", (long)item.UID, "uid", (long)p.Entity.UID, "type", (int)item.ID, "stack", (int)item.StackSize,
                        "bound", item.Bound ? 1 : 0, "days", item.Days.ToString(CultureInfo.InvariantCulture),
                        "permanent", item.Days == 0, "stamp", item.DayStamp.ToBinary()) != 1)
                        throw new InvalidOperationException("EXP Ball ownership changed.");
                    effects = ExperienceRewards.Persist(c, t, p, plan);
                    ReincarnationRecovery.Consume(c, t, p, plan.Restored);
                    EnlightenmentProgress.PersistCredit(c,t,p,enlightenment); t.Commit();
                }}
                EnlightenmentProgress.InstallCredit(p,enlightenment);
                if (standard) p.ExpBalls = used;
                Q.Install(p, item.StackSize <= 1 ? new[] { item.UID } : new uint[0], new ConquerItem[0]);
                if (item.StackSize > 1) { item.StackSize--; item.Mode = Enums.ItemMode.Update; item.Send(p); item.Mode = Enums.ItemMode.Default; }
                ReincarnationRecovery.Committed(p, plan.Restored); MentorExperience.Apply(p, effects.MentorCredit);
                p.ApplyExperienceReward(plan, effects, autoHunt, true);
            } catch (Exception ex) { Q.Fail(p, "experience-consumable", ex); }
            return true;
        }
    }
}
