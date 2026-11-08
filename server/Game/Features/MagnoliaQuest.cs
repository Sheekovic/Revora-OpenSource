using System;
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
    internal static partial class MagnoliaQuest
    {
        internal static Func<DateTime> Clock = () => DateTime.UtcNow;
        private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
        private static bool enabled;
        private sealed class Offer { internal DateTime End; internal string Day; internal uint Fee; }
        private sealed class Run { internal string Day; internal bool Done, Legacy, RuneReward; internal uint Cp, Rvp, Chi; internal ushort Study; internal int Version, BonusVersion, Minutes=60, Changes; internal uint Seed, Type; }
        private static readonly ConditionalWeakTable<GameState, Offer> offers = new ConditionalWeakTable<GameState, Offer>();
        internal static string Day(DateTime utc) {
            if (utc.Kind != DateTimeKind.Utc) throw new ArgumentException("Magnolia requires UTC.");
            return TimeZoneInfo.ConvertTimeFromUtc(utc, Zone).ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        }
        internal static void Initialize() {
            ConquerItemBaseInformation rune;
            if(!ConquerItemInformation.BaseInformations.TryGetValue(4030101,out rune) || rune.Class!=0)
                throw new InvalidOperationException("Magnolia yellow rune catalog missing or class restricted.");
            using (var c = DataHolder.MySqlConnection) { c.Open(); Q.Execute(c, null, @"CREATE TABLE IF NOT EXISTS revora_magnolia_runs(
uid bigint NOT NULL CHECK(uid>0),day varchar(8) NOT NULL,completed boolean NOT NULL DEFAULT false,legacy boolean NOT NULL DEFAULT false,
issued_seed bigint,planted_seed bigint,seed_type integer,fee bigint NOT NULL CHECK(fee BETWEEN 0 AND 4294967295),
cp bigint NOT NULL CHECK(cp BETWEEN 0 AND 4294967295),rvp bigint NOT NULL CHECK(rvp BETWEEN 0 AND 4294967295),
study integer NOT NULL CHECK(study BETWEEN 0 AND 65535),chi bigint NOT NULL,reward_uid bigint,
experience bigint NOT NULL DEFAULT 0 CHECK(experience>=0),created_at timestamptz NOT NULL DEFAULT now(),completed_at timestamptz,
PRIMARY KEY(uid,day));
ALTER TABLE revora_magnolia_runs ADD COLUMN IF NOT EXISTS valuable_reward boolean NOT NULL DEFAULT false;
ALTER TABLE revora_magnolia_runs ADD COLUMN IF NOT EXISTS soul_uid bigint UNIQUE;
ALTER TABLE revora_magnolia_runs ADD COLUMN IF NOT EXISTS rune_reward boolean NOT NULL DEFAULT false;
ALTER TABLE revora_magnolia_runs ADD COLUMN IF NOT EXISTS rune_uid bigint UNIQUE CHECK(rune_uid>0);"); InitializeGrades(c); InitializeBonuses(c); } enabled = true;
        }
        private static Run NewRun(string day,uint type) {
            return new Run { RuneReward=true, Day = day, Cp = Config.DailyQuestCp, Rvp = Config.DailyQuestRevoraPoints,
                Study = GradeStudy(type), Chi = Config.DailyQuestChiToken, Version=1, BonusVersion=4, Minutes=GradeMinutes(type), Type=type };
        }
        private static bool Ready(GameState p) {
            return enabled && Q.Player(p) && CharacterProgression.Managed(p) && p.Spells != null && p.Entity.SubClasses != null
                && p.Booth == null && p.Trade != null && !p.Trade.InTrade;
        }
        private static Run Read(NpgsqlConnection c, NpgsqlTransaction t, GameState p, string day, DateTime utc) {
            using (var q = Q.Command(c, t, "SELECT day,completed,legacy,cp,rvp,study,chi,rules_version,exp_minutes,COALESCE(current_seed,issued_seed,0),COALESCE(seed_type,0),changes,bonus_version,valuable_reward,rune_reward FROM revora_magnolia_runs WHERE uid=@uid ORDER BY day DESC LIMIT 1", "uid", (long)p.Entity.UID))
            using (var r = q.ExecuteReader()) if (r.Read()) return new Run { Day = r.GetString(0), Done = r.GetBoolean(1), Legacy = r.GetBoolean(2),
                Cp = checked((uint)Convert.ToInt64(r[3])), Rvp = checked((uint)Convert.ToInt64(r[4])), Study = checked((ushort)Convert.ToInt32(r[5])), Chi = checked((uint)Convert.ToInt64(r[6])), Version=r.GetInt32(7), Minutes=r.GetInt32(8), Seed=Convert.ToUInt32(r[9]), Type=Convert.ToUInt32(r[10]), Changes=r.GetInt32(11), BonusVersion=r.GetInt32(12),RuneReward=r.GetBoolean(14) };
            // Import only a persisted completion from the current legacy reset date.
            // The old claimed-seed flag was RAM-only and cannot prove an earlier grant.
            using (var q = Q.Command(c, t, @"SELECT d.magniloquest,e.lastresettime FROM entities e LEFT JOIN dialyquests d ON d.entityid=e.uid WHERE e.uid=@uid", "uid", (long)p.Entity.UID))
            using (var r = q.ExecuteReader()) if (r.Read() && !r.IsDBNull(0) && Convert.ToBoolean(r[0]) && !r.IsDBNull(1)
                && DateTime.FromBinary(Convert.ToInt64(r[1])).Date == utc.ToLocalTime().Date)
                return new Run { Day = day, Done = true, Legacy = true };
            return null;
        }
        private static void Owner(NpgsqlConnection c, NpgsqlTransaction t, GameState p) {
            using (var q = Q.Command(c, t, "SELECT level FROM entities WHERE uid=@uid FOR UPDATE", "uid", (long)p.Entity.UID)) {
                object value = q.ExecuteScalar(); if (value == null || Convert.ToInt32(value) < Config.DailyMagnoliaMinLevel)
                    throw new InvalidOperationException("Magnolia owner eligibility changed.");
            }
        }
        private static void Insert(NpgsqlConnection c, NpgsqlTransaction t, GameState p, Run r, ConquerItem seed, uint fee) {
            if (Q.Execute(c, t, @"INSERT INTO revora_magnolia_runs(uid,day,completed,legacy,issued_seed,seed_type,fee,cp,rvp,study,chi,rules_version,exp_minutes,bonus_version,valuable_reward,rune_reward)
VALUES(@uid,@day,@done,@legacy,NULLIF(@seed,0),NULLIF(@type,0),@fee,@cp,@rvp,@study,@chi,@version,@minutes,@bonus,@valuable,@rune)", "uid", (long)p.Entity.UID,
                "day", r.Day, "done", r.Done, "legacy", r.Legacy, "seed", seed == null ? 0L : (long)seed.UID,
                "type", seed == null ? 0 : (int)seed.ID, "fee", (long)fee, "cp", (long)r.Cp, "rvp", (long)r.Rvp,
                "study", (int)r.Study, "chi", (long)r.Chi,"version",r.Version,"minutes",r.Minutes,"bonus",r.BonusVersion,"valuable",false,"rune",r.RuneReward) != 1) throw new InvalidOperationException("Magnolia daily record was not saved.");
        }
        private static void Import(NpgsqlConnection c, NpgsqlTransaction t, GameState p, Run r) {
            if (r == null || !r.Legacy) return;
            using (var q = Q.Command(c, t, "SELECT EXISTS(SELECT 1 FROM revora_magnolia_runs WHERE uid=@uid AND day=@day)", "uid", (long)p.Entity.UID, "day", r.Day))
                if (!Equals(q.ExecuteScalar(), true)) Insert(c, t, p, r, null, 0);
        }
        private static void Projection(GameState p, Run r, string day) {
            p.Entity.CliamSeed = r != null && string.CompareOrdinal(r.Day, day) >= 0;
            p.Entity.MagniloQuest = p.Entity.CliamSeed && r.Done;
        }
        internal static void Refresh(GameState p) {
            if (!enabled || !Q.Player(p)) return;
            lock (p.ItemSyncRoot) try {
                var now = Clock(); string day = Day(now);
                using (var c = DataHolder.MySqlConnection) { c.Open(); Projection(p, Read(c, null, p, day, now), day); }
            } catch (Exception ex) { Q.Fail(p, "magnolia-refresh", ex); }
        }
        private static string Issue(GameState p, bool super, DateTime now) {
            Offer offer;
            if (!offers.TryGetValue(p, out offer) || offer.End <= now || offer.Day != Day(now)) return "Choose a seed before I wrap it for you.";
            offers.Remove(p); string day = offer.Day; uint fee = super ? offer.Fee : 0;
            if (p.Inventory.Count >= 40) return "Leave one space for your seed.";
            if (p.Inventory.Objects.Any(i => SeedType(i.ID))) return "You already carry a seed. Plant it in a flower bed.";
            if (p.Entity.ConquerPoints < fee) return "You do not have enough CPs for that seed.";
            var seed = Q.New(super ? 729310u : 729306u, false); Run run;
            using (var c = DataHolder.MySqlConnection) { c.Open(); using (var t = c.BeginTransaction()) {
                Owner(c, t, p); run = Read(c, t, p, day, now);
                if (run != null && string.CompareOrdinal(run.Day, day) >= 0) {
                    Import(c, t, p, run); t.Commit(); Projection(p, run, day); return "Your seed for today is already accounted for. Return tomorrow.";
                }
                SeedBag(c,t,p);
                if (Q.Execute(c, t, "UPDATE entities SET conquerpoints=@cp WHERE uid=@uid AND COALESCE(conquerpoints,0)=@old", "cp", (long)(p.Entity.ConquerPoints-fee),
                    "uid", (long)p.Entity.UID, "old", (long)p.Entity.ConquerPoints) != 1) throw new InvalidOperationException("Seed price preimage changed.");
                InsertItem(c, t, p, seed); run = NewRun(day,seed.ID); run.Seed=seed.UID; Insert(c, t, p, run, seed, fee); t.Commit();
            }}
            p.Entity.ApplyCommittedConquerPoints(p.Entity.ConquerPoints-fee); Q.Install(p, new uint[0], new[] {seed}); Projection(p, run, day);
            return "Plant your seed beside either flower bed. May it take root.";
        }
        private static void InsertItem(NpgsqlConnection c, NpgsqlTransaction t, GameState p, ConquerItem item) {
            if (Q.Execute(c, t, @"INSERT INTO items(uid,id,entityid,position,warehouse,stacksize,bound,durability,maximdurability)
VALUES(@item,@type,@uid,0,0,1,@bound,@dur,@max)", "item", (long)item.UID, "type", (int)item.ID, "uid", (long)p.Entity.UID,
                "bound", item.Bound ? 1 : 0, "dur", (int)item.Durability, "max", (int)item.MaximDurability) != 1)
                throw new InvalidOperationException("Magnolia item was not saved.");
        }
        internal static bool HandleNpc(GameState p, NpcRequest request) {
            if (p == null || request == null || p.ActiveNpc != ExplorationWorld.GardenKeeper) return false;
            if (request.OptionID == 2) { seedOffers.Remove(p); return false; }
            if (!Ready(p)) return true;
            if (request.OptionID>=5 && request.OptionID<=7) return HandleSeedChange(p,request);
            seedOffers.Remove(p);
            lock (p.ItemSyncRoot) try {
                if (!Ready(p) || !Q.Near(p, ExplorationWorld.GardenKeeper, 1806) || p.Map.BaseID != 1806
                    || request.NpcID != 0 && request.NpcID != p.ActiveNpc) return true;
                if (request.OptionID == 255) { offers.Remove(p); p.ActiveNpc = 0; return true; }
                var d = new Npcs(p); d.Avatar(113); DateTime now = Clock();
                if (p.Entity.Level < Config.DailyMagnoliaMinLevel) d.Text("Return when you reach level " + Config.DailyMagnoliaMinLevel + ".");
                else if (request.OptionID == 1) {
                    offers.Remove(p); offers.Add(p, new Offer { Day = Day(now), End = now.AddMinutes(2), Fee = Config.DailyMagnoliaSuperSeedCp });
                    d.Text("A normal seed is free. A super seed costs " + Config.DailyMagnoliaSuperSeedCp + " CPs. New harvests also earn a bound Tortoise Breaker rune. Better seeds earn more EXP and Study Points. Refined or better seeds have a 20% chance of a bonus gift.");
                    d.Option("A normal seed.", 3); d.Option("Buy a super seed.", 4);
                } else if (request.OptionID == 3 || request.OptionID == 4) d.Text(Issue(p, request.OptionID == 4, now));
                else {
                    offers.Remove(p); Refresh(p); if (p.Entity.PersistenceRequiresReload) return true;
                    d.Text(p.Entity.MagniloQuest ? "The garden is brighter for your care. Return tomorrow."
                        : p.Entity.CliamSeed ? "Plant your seed beside a flower bed." : "Help me fill this garden with magnolias.");
                    if (!p.Entity.CliamSeed && !p.Inventory.Objects.Any(i=>SeedType(i.ID))) d.Option("Choose a seed.", 1);
                    if (!p.Entity.MagniloQuest && p.Inventory.Objects.Any(i=>SeedType(i.ID))) d.Option("Change my seed.",5);
                }
                d.Option("Back to Twin City.", 2); d.Option("Farewell.", 255); d.Send();
            } catch (Exception ex) { Q.Fail(p, "magnolia-seed", ex); }
            return true;
        }
        internal static bool TryUse(ConquerItem seed, GameState p) {
            if (seed == null || !SeedType(seed.ID)) return false;
            if (!Ready(p)) return true;
            lock (p.ItemSyncRoot) lock (MentorExperience.Sync) try {
                ConquerItem owned;
                if (!Ready(p) || p.Entity.Level < Config.DailyMagnoliaMinLevel || p.Entity.MapID != 1806 || p.Map.BaseID != 1806
                    || !p.Inventory.TryGetItem(seed.UID, out owned) || !ReferenceEquals(owned, seed) || seed.Position != 0 || seed.Warehouse != 0
                    || seed.Lock != 0 || seed.Suspicious || seed.InWardrobe || seed.Days != 0 && DateTime.Now >= seed.DayStamp.AddDays(seed.Days)) return true;
                uint bed = Kernel.GetDistance(p.Entity.X, p.Entity.Y, 98, 111) < 7 ? 98743u
                    : Kernel.GetDistance(p.Entity.X, p.Entity.Y, 106, 98) < 7 ? 98742u : 0;
                if (bed == 0) { p.Send("Plant your seed beside a flower bed."); return true; }
                if (p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Cursed)) { p.Send("Keep your seed until you can receive its experience."); return true; }
                if (seed.StackSize > 1 && p.Inventory.Count >= 40) { p.Send("Leave one space for your Chi Token."); return true; }
                DateTime now = Clock(); string day = Day(now); Run run; ConquerItem reward,rune=null; ConquerItem[] bonus;
                ExperienceRewardPlan plan; ExperienceRewards.Effects effects; uint cp, rvp; ushort study; ulong autoHunt;
                using (var c = DataHolder.MySqlConnection) { c.Open(); using (var t = c.BeginTransaction()) {
                    Owner(c, t, p); run = Read(c, t, p, day, now);
                    if (run != null && (string.CompareOrdinal(run.Day, day) > 0 || run.Day == day && run.Done)) {
                        Import(c, t, p, run); t.Commit(); Projection(p, run, day); p.Send("You have tended the garden today. Return tomorrow."); return true;
                    }
                    if (run == null || run.Day != day) { run = NewRun(day,seed.ID); run.Seed=seed.UID; Insert(c, t, p, run, seed, 0); }
                    if (run.Version==1 && (run.Seed!=seed.UID || run.Type!=seed.ID)) { p.Send("Plant the seed entrusted to you today."); return true; }
                    if(p.Inventory.Count-(seed.StackSize<=1?1:0)+1+(run.RuneReward?1:0)+BonusCapacity(run)>40){p.Send("Leave room for your harvest gifts and a possible bonus.");return true;}
                    if ((ulong)p.Entity.ConquerPoints+run.Cp > uint.MaxValue || (ulong)p.Entity.RevoraPoint+run.Rvp > uint.MaxValue
                        || (int)p.Entity.SubClasses.StudyPoints+run.Study > ushort.MaxValue) { p.Send("Spend some points before collecting the garden's reward."); return true; }
                    cp = p.Entity.ConquerPoints+run.Cp; rvp = p.Entity.RevoraPoint+run.Rvp; study = (ushort)(p.Entity.SubClasses.StudyPoints+run.Study);
                    ulong award = p.Entity.Level >= Config.LevelCap ? 0 : ExperienceMath.Scale(p.ExpBall,(ulong)run.Minutes,60);
                    plan = new ExperienceRewardPlan(CharacterProgression.Capture(p), award, Config.LevelCap, Config.AttributePointsPerLevel, ReincarnationRecovery.Snapshot(p));
                    autoHunt = p.Entity.InAutoHunt ? checked(p.Entity.AutoHuntEXP+award/Math.Max(1u,Config.AutoHuntExperienceDivisor)) : p.Entity.AutoHuntEXP;
                    SeedBag(c,t,p);
                    CharacterProgression.Persist(c, t, p, plan.After);
                    if (Q.Execute(c, t, @"UPDATE entities SET conquerpoints=@cp,revorapoint=@rvp,studypoints=@study
WHERE uid=@uid AND COALESCE(conquerpoints,0)=@oldcp AND COALESCE(revorapoint,0)=@oldrvp AND COALESCE(studypoints,0)=@oldstudy", "uid", (long)p.Entity.UID,
                        "cp", (long)cp, "rvp", (long)rvp, "study", (int)study, "oldcp", (long)p.Entity.ConquerPoints,
                        "oldrvp", (long)p.Entity.RevoraPoint, "oldstudy", (int)p.Entity.SubClasses.StudyPoints) != 1)
                        throw new InvalidOperationException("Magnolia reward balance changed.");
                    string debit = seed.StackSize <= 1 ? "DELETE FROM items" : "UPDATE items SET stacksize=stacksize-1";
                    if (Q.Execute(c, t, debit + @" WHERE uid=@seed AND entityid=@uid AND id=@type AND position=0 AND warehouse=0 AND stacksize=@stack
AND bound=@bound AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0
AND COALESCE(NULLIF(days,''),'0')=@days AND (@permanent OR daystamp=@stamp)", "seed", (long)seed.UID, "uid", (long)p.Entity.UID,
                        "type", (int)seed.ID, "stack", (int)seed.StackSize, "bound", seed.Bound ? 1 : 0,
                        "days", seed.Days.ToString(CultureInfo.InvariantCulture), "permanent", seed.Days == 0, "stamp", seed.DayStamp.ToBinary()) != 1)
                        throw new InvalidOperationException("Magnolia seed ownership changed.");
                    using (var q = Q.Command(c, t, "SELECT count(*) FROM items WHERE entityid=@uid AND warehouse=0 AND position=0 AND COALESCE(inwardrobe,0)=0", "uid", (long)p.Entity.UID))
                        if (Convert.ToInt64(q.ExecuteScalar()) != p.Inventory.Count - (seed.StackSize <= 1 ? 1 : 0))
                            throw new InvalidOperationException("Magnolia inventory requires reload.");
                    reward = Q.New(run.Chi, seed.Bound); InsertItem(c, t, p, reward);
                    if(run.RuneReward){rune=Q.New(4030101,true);InsertItem(c,t,p,rune);}
                    bonus=PersistBonus(c,t,p,run,seed.Bound);
                    effects = ExperienceRewards.Persist(c, t, p, plan); ReincarnationRecovery.Consume(c, t, p, plan.Restored);
                    if (Q.Execute(c, t, @"UPDATE revora_magnolia_runs SET completed=true,planted_seed=@seed,seed_type=@type,reward_uid=@reward,soul_uid=@soul,rune_uid=@rune,experience=@exp,completed_at=@now
WHERE uid=@uid AND day=@day AND completed=false", "uid", (long)p.Entity.UID, "day", day, "seed", (long)seed.UID,
                        "type", (int)seed.ID, "reward", (long)reward.UID,"soul",DBNull.Value,"rune",rune==null?(object)DBNull.Value:(long)rune.UID, "exp", checked((long)award), "now", now) != 1)
                        throw new InvalidOperationException("Magnolia completion was not saved.");
                    StudyRewardEvidence.Record(c,t,p,run.Study,"magnolia:"+day);
                    t.Commit();
                }}
                Q.Install(p, seed.StackSize <= 1 ? new[] {seed.UID} : new uint[0], (rune==null?new[]{reward}:new[]{reward,rune}).Concat(bonus).ToArray());
                if (seed.StackSize > 1) { seed.StackSize--; seed.Mode = Enums.ItemMode.Update; seed.Send(p); seed.Mode = Enums.ItemMode.Default; }
                p.Entity.ApplyCommittedConquerPoints(cp); p.Entity.ApplyCommittedRevoraPoints(rvp); p.Entity.SubClasses.StudyPoints = study;
                ReincarnationRecovery.Committed(p, plan.Restored); MentorExperience.Apply(p, effects.MentorCredit); p.ApplyExperienceReward(plan, effects, autoHunt, true);
                run.Done = true; Projection(p, run, day); Q.Refresh(p);
                p.Send(new SubClassShowFull(0) {ID=8,Study=study,StudyReceive=run.Study}.ToArray());
                p.SendScreen(new _String(true) {UID=bed,Type=_String.Effect,TextsCount=1,Texts={"accession1"}}, true);
                p.Send("Your magnolia has taken root. The garden's reward is yours.");
            } catch (Exception ex) { Q.Fail(p, "magnolia-plant", ex); }
            return true;
        }
    }
}
