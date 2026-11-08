using System;
using System.Collections.Generic;
using System.Globalization;
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
    internal static class BattlePowerTournament
    {
        internal const uint Manager = 91050;
        internal static readonly string[] Divisions = { "250-300", "301-350", "351+" };
        private static readonly uint[] Cps = { 300, 500, 1000 }, Chi = { 5000, 10000, 15000 };
        private static readonly uint[,] ExchangeChi = { { 5000, 10000, 20000 }, { 10000, 20000, 30000 }, { 20000, 30000, 50000 } };
        private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
        private sealed class Recent { internal DateTime Until; internal ushort LoginMap; }
        private static readonly ConditionalWeakTable<GameState, Recent> recent = new ConditionalWeakTable<GameState, Recent>();
        internal static void LoadedLocation(GameState p, ushort map) { recent.GetValue(p, _ => new Recent()).LoginMap = map; }
        internal static int Kind(int power) { return power < 250 ? -1 : power <= 300 ? 20 : power <= 350 ? 21 : 22; }
        internal static bool Eligible(int kind, int power, bool admitted)
        { return LocalPkRules.BpWar(kind) && (admitted ? kind == 22 || power <= (kind == 20 ? 300 : 350) : Kind(power) == kind); }
        internal static DateTime SeasonEnd(DateTime start)
        {
            var local = TimeZoneInfo.ConvertTimeFromUtc(start, Zone);
            var next = new DateTime(local.Year, local.Month, 1).AddMonths(1);
            next = next.AddDays(((int)DayOfWeek.Sunday - (int)next.DayOfWeek + 7) % 7).AddHours(20);
            return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(next, DateTimeKind.Unspecified), Zone);
        }
        internal static void PlaceManager()
        {
            var town = Kernel.Maps[1002]; Revora.Interfaces.INpc existing;
            if (town.Npcs.TryGetValue(Manager, out existing)) {
                if (existing.Mesh != 37270) throw new InvalidOperationException("BP manager identity is occupied.");
                return;
            }
            ushort x = 295, y = 141;
            if (!PotencyGuide.FindLanding(town, ref x, ref y)) throw new InvalidOperationException("BP manager landing unavailable.");
            town.AddNpc(new NpcSpawn { UID = Manager, MapID = 1002, X = x, Y = y, Mesh = 37270, Type = Enums.NpcType.Talker, Name = "BPTournamentManager" });
        }
        internal static void Initialize()
        {
            for (uint id = 3006840; id <= 3006842; id++) if (!ConquerItemInformation.BaseInformations.ContainsKey(id))
                throw new InvalidOperationException("BP tournament token catalog missing.");
            using (var c = DataHolder.MySqlConnection) { c.Open(); Q.Execute(c, null, @"
CREATE TABLE IF NOT EXISTS revora_bp_admissions (
round_start timestamptz NOT NULL,uid bigint NOT NULL,run varchar(40) NOT NULL,power integer NOT NULL CHECK(power>=250),
PRIMARY KEY(round_start,uid),FOREIGN KEY(run,uid) REFERENCES revora_pk_entries(run,uid));
ALTER TABLE revora_bp_admissions ADD COLUMN IF NOT EXISTS arena_id integer NOT NULL DEFAULT 0;
ALTER TABLE revora_bp_admissions ADD COLUMN IF NOT EXISTS prior_mode integer NOT NULL DEFAULT 1;
CREATE INDEX IF NOT EXISTS revora_bp_admission_recovery ON revora_bp_admissions(uid,arena_id);
CREATE TABLE IF NOT EXISTS revora_bp_claims (
run varchar(40) PRIMARY KEY REFERENCES revora_pk_wars(run),uid bigint NOT NULL,cp bigint NOT NULL,chi bigint NOT NULL,
token_uid bigint NOT NULL CHECK(token_uid>=0),claimed_at timestamptz NOT NULL);
CREATE TABLE IF NOT EXISTS revora_bp_tokens (
item_uid bigint PRIMARY KEY,uid bigint NOT NULL,kind integer NOT NULL CHECK(kind BETWEEN 20 AND 22),season varchar(6) NOT NULL,
expires_at timestamptz NOT NULL,spent_at timestamptz,issued_run varchar(40) NOT NULL UNIQUE REFERENCES revora_bp_claims(run));
CREATE TABLE IF NOT EXISTS revora_bp_exchanges (
id uuid PRIMARY KEY,uid bigint NOT NULL,kind integer NOT NULL CHECK(kind BETWEEN 20 AND 22),season varchar(6) NOT NULL,
tokens bigint[] NOT NULL,chi bigint NOT NULL,exchanged_at timestamptz NOT NULL);
ALTER TABLE revora_pk_wars ADD COLUMN IF NOT EXISTS bp_valuable_reward boolean NOT NULL DEFAULT false;
ALTER TABLE revora_bp_claims ADD COLUMN IF NOT EXISTS soul_uid bigint UNIQUE;"); }
        }
        internal static bool Register(string run, GameState p, DateTime now)
        {
            if (!Q.Player(p) || !Monitor.IsEntered(p.ItemSyncRoot)) return false;
            int power = p.Entity.BattlePower;
            using (var c = DataHolder.MySqlConnection) { c.Open(); using (var t = c.BeginTransaction()) {
                using (var q = Q.Command(c, t, "SELECT uid FROM entities WHERE uid=@uid FOR UPDATE", "uid", (long)p.Entity.UID)) if (q.ExecuteScalar() == null) return false;
                DateTime start; int kind;
                using (var q = Q.Command(c, t, @"SELECT starts_at,kind FROM revora_pk_wars WHERE run=@run AND state='open'
AND kind BETWEEN 20 AND 22 AND bracket=0 AND starts_at<=@now AND join_until>@now FOR UPDATE", "run", run, "now", now))
                using (var r = q.ExecuteReader()) { if (!r.Read()) return false; start = r.GetFieldValue<DateTimeOffset>(0).UtcDateTime; kind = r.GetInt32(1); }
                if (!Eligible(kind, power, false)) return false;
                using (var q = Q.Command(c, t, "SELECT run FROM revora_bp_admissions WHERE round_start=@start AND uid=@uid", "start", start, "uid", (long)p.Entity.UID)) {
                    var prior = q.ExecuteScalar() as string;
                    if (prior != null) {
                        if (prior != run) return false;
                        using (var check = Q.Command(c, t, "SELECT admitted_at IS NULL FROM revora_pk_entries WHERE run=@run AND uid=@uid", "run", run, "uid", (long)p.Entity.UID))
                        { bool retry = Equals(check.ExecuteScalar(), true); t.Commit(); return retry; }
                    }
                }
                using (var q = Q.Command(c, t, "SELECT count(*)<64 FROM revora_pk_entries WHERE run=@run", "run", run)) if (!Equals(q.ExecuteScalar(), true)) return false;
                if (Q.Execute(c, t, "INSERT INTO revora_pk_entries(run,uid,level,class_id) VALUES(@run,@uid,@level,@class)",
                    "run", run, "uid", (long)p.Entity.UID, "level", (int)p.Entity.Level, "class", (int)p.Entity.Class) != 1
                    || Q.Execute(c, t, "INSERT INTO revora_bp_admissions(round_start,uid,run,power,prior_mode) VALUES(@start,@uid,@run,@power,@mode)",
                    "start", start, "uid", (long)p.Entity.UID, "run", run, "power", power, "mode", (int)p.Entity.PKMode) != 1) throw new InvalidOperationException("BP reservation was not saved.");
                t.Commit(); return true;
            } }
        }
        internal static void PrepareArena(string run, uint uid, ushort arena)
        {
            using (var c = DataHolder.MySqlConnection) { c.Open();
                if (arena == 0 || Q.Execute(c, null, @"UPDATE revora_bp_admissions a SET arena_id=@arena WHERE run=@run AND uid=@uid
AND (arena_id=0 OR arena_id=@arena) AND EXISTS(SELECT 1 FROM revora_pk_wars w WHERE w.run=a.run AND w.state='open')",
                    "arena", (int)arena, "run", run, "uid", (long)uid) != 1) throw new InvalidOperationException("BP travel recovery was not saved.");
            }
        }
        internal static void Admit(string run, uint uid, ushort arena, DateTime now)
        {
            using (var c = DataHolder.MySqlConnection) { c.Open(); using (var t = c.BeginTransaction()) {
                using (var q = Q.Command(c, t, "SELECT run FROM revora_pk_wars WHERE run=@run AND kind BETWEEN 20 AND 22 AND state='open' AND starts_at<=@now AND join_until>@now FOR UPDATE", "run", run, "now", now))
                    if (q.ExecuteScalar() == null) throw new InvalidOperationException("BP admission window closed.");
                if (arena == 0 || Q.Execute(c, t, "UPDATE revora_bp_admissions SET arena_id=@arena WHERE run=@run AND uid=@uid", "arena", (int)arena, "run", run, "uid", (long)uid) != 1
                    || Q.Execute(c, t, "UPDATE revora_pk_entries SET admitted_at=COALESCE(admitted_at,@now),alive=true WHERE run=@run AND uid=@uid", "now", now, "run", run, "uid", (long)uid) != 1)
                    throw new InvalidOperationException("BP admission was not saved.");
                t.Commit();
            } }
        }
        internal static void Recover(GameState p)
        {
            int mode; Recent state; ushort saved = recent.TryGetValue(p, out state) ? state.LoginMap : (ushort)0;
            using (var c = DataHolder.MySqlConnection) { c.Open(); using (var q = Q.Command(c, null, @"SELECT prior_mode FROM revora_bp_admissions
WHERE uid=@uid AND (arena_id=@map OR arena_id=@saved) AND arena_id<>0 ORDER BY round_start DESC LIMIT 1",
                "uid", (long)p.Entity.UID, "map", (int)p.Entity.MapID, "saved", (int)saved)) {
                var value = q.ExecuteScalar(); if (value == null) { if (state != null) state.LoginMap = 0; return; } mode = Convert.ToInt32(value);
            } }
            p.Entity.OnDeath = null;
            p.Entity.PKMode = Enum.IsDefined(typeof(Enums.PKMode), (byte)mode) ? (Enums.PKMode)mode : Enums.PKMode.Peace;
            p.Send(new Data(true) { UID = p.Entity.UID, ID = Data.ChangePKMode, dwParam = (uint)p.Entity.PKMode });
            p.Entity.Teleport(1002, 408, 330); p.Entity.BringToLife();
            if (p.Entity.MapID != 1002) throw new InvalidOperationException("BP login recovery did not reach home.");
            if (state != null) state.LoginMap = 0;
            p.Send("Your former BP trial is over. The manager keeps any earned purse.");
        }
        private static void Owner(NpgsqlConnection c, NpgsqlTransaction t, GameState p)
        {
            using (var q = Q.Command(c, t, "SELECT conquerpoints FROM entities WHERE uid=@uid FOR UPDATE", "uid", (long)p.Entity.UID)) {
                var cp = q.ExecuteScalar(); if (cp == null || Convert.ToUInt32(cp) != p.Entity.ConquerPoints)
                    throw new InvalidOperationException("BP purse owner preimage changed.");
            }
        }
        private static void AddChi(NpgsqlConnection c, NpgsqlTransaction t, GameState p, uint before, uint after)
        {
            if (Q.Execute(c, t, "UPDATE chi SET points=@after WHERE uid=@uid AND points=@before", "after", (long)after,
                "uid", (long)p.Entity.UID, "before", (long)before) != 1) throw new InvalidOperationException("BP Chi preimage changed.");
        }
        internal static string Claim(GameState p, DateTime now)
        {
            if (!Q.Player(p) || !Monitor.IsEntered(p.ItemSyncRoot)) return "Your trial purse is unavailable.";
            uint before = p.ChiPoints;
            if (!ChiWallet.Ready(p) || !ChiWallet.Matches(p, before)) return "Let your Chi settle, then speak to me again.";
            using (var c = DataHolder.MySqlConnection) { c.Open(); using (var t = c.BeginTransaction()) {
                Owner(c, t, p); string run; int kind; DateTime start; bool valuable;
                using (var q = Q.Command(c, t, @"SELECT w.run,w.kind,w.starts_at,w.bp_valuable_reward FROM revora_pk_wars w WHERE w.winner=@uid
AND w.kind BETWEEN 20 AND 22 AND w.state='complete' AND w.ended_at<=@now
AND NOT EXISTS(SELECT 1 FROM revora_bp_claims x WHERE x.run=w.run) ORDER BY w.ended_at,w.run LIMIT 1 FOR UPDATE",
                    "uid", (long)p.Entity.UID, "now", now))
                using (var r = q.ExecuteReader()) { if (!r.Read()) return "I hold no unclaimed BP champion's purse for you."; run = r.GetString(0); kind = r.GetInt32(1); start = r.GetFieldValue<DateTimeOffset>(2).UtcDateTime; valuable=r.GetBoolean(3); }
                uint cp = Cps[kind - 20], chi = Chi[kind - 20]; DateTime expiry = SeasonEnd(start); bool token = now < expiry;
                if ((ulong)p.Entity.ConquerPoints + cp > uint.MaxValue || (ulong)before + chi > uint.MaxValue) return "Spend some CPs or Chi first. Your purse will wait.";
                int slots=(token?1:0)+(valuable?1:0);
                if (p.Inventory.Count+slots>40) return "Make "+slots+" places for your champion rewards. Your purse will wait.";
                if (slots>0) using (var q = Q.Command(c, t, "SELECT count(*) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0", "uid", (long)p.Entity.UID))
                    if (Convert.ToInt64(q.ExecuteScalar()) != p.Inventory.Count) throw new InvalidOperationException("BP prize inventory preimage changed.");
                var rewards=new List<ConquerItem>();if(token)rewards.Add(Q.New((uint)(3006840+kind-20)));
                var soul=valuable?QuestSoulReward.Create():null;if(soul!=null)rewards.Add(soul);var items=rewards.ToArray();
                foreach (var item in items) {
                    Q.Insert(c, t, p.Entity.UID, item);
                    using (var q = Q.Command(c, t, "SELECT count(*) FROM items WHERE uid=@item AND entityid=@uid AND id=@type AND bound=1 AND stacksize=1 AND position=0 AND warehouse=0",
                        "item", (long)item.UID, "uid", (long)p.Entity.UID, "type", (int)item.ID))
                        if (Convert.ToInt64(q.ExecuteScalar()) != 1) throw new InvalidOperationException("BP token item was not saved.");
                }
                uint after = before + chi, nextCp = p.Entity.ConquerPoints + cp;
                AddChi(c, t, p, before, after);
                if (Q.Execute(c, t, "UPDATE entities SET conquerpoints=@after WHERE uid=@uid AND conquerpoints=@before", "after", (long)nextCp,
                    "uid", (long)p.Entity.UID, "before", (long)p.Entity.ConquerPoints) != 1) throw new InvalidOperationException("BP CP preimage changed.");
                if (Q.Execute(c, t, "INSERT INTO revora_bp_claims(run,uid,cp,chi,token_uid,claimed_at,soul_uid) VALUES(@run,@uid,@cp,@chi,@token,@now,@soul)",
                    "run", run, "uid", (long)p.Entity.UID, "cp", (long)cp, "chi", (long)chi, "token", token ? (long)items[0].UID : 0L, "now", now,"soul",soul==null?(object)DBNull.Value:(long)soul.UID) != 1)
                    throw new InvalidOperationException("BP claim receipt was not saved.");
                if (token && Q.Execute(c, t, "INSERT INTO revora_bp_tokens(item_uid,uid,kind,season,expires_at,issued_run) VALUES(@item,@uid,@kind,@season,@expiry,@run)",
                    "item", (long)items[0].UID, "uid", (long)p.Entity.UID, "kind", kind, "season", TimeZoneInfo.ConvertTimeFromUtc(start, Zone).ToString("yyyyMM", CultureInfo.InvariantCulture),
                    "expiry", expiry, "run", run) != 1) throw new InvalidOperationException("BP token origin was not saved.");
                t.Commit(); p.Entity.ApplyCommittedConquerPoints(nextCp); ChiWallet.Committed(p, before, after); Q.Install(p, new uint[0], items);
                p.Send(new ChiPowers(true).Query(p));
                return (token ? "Your champion's purse and season token are yours." : "Your CPs and Chi are yours. That season's token has expired.")+(valuable?" A bound P7 soul is yours as well.":"");
            } }
        }
        private static bool Usable(ConquerItem item, uint type)
        { return item != null && item.ID == type && item.Bound && item.StackSize == 1 && item.Position == 0 && item.Warehouse == 0 && item.Lock == 0 && !item.Suspicious && !item.InWardrobe && item.Days == 0; }
        internal static string Exchange(GameState p, int kind, int count, DateTime now)
        {
            if (!Q.Player(p) || !Monitor.IsEntered(p.ItemSyncRoot)) return "Your trial tokens are unavailable.";
            if (!LocalPkRules.BpWar(kind) || count < 2 || count > 4) return "Choose two, three or four matching tokens.";
            uint before = p.ChiPoints;
            if (!ChiWallet.Ready(p) || !ChiWallet.Matches(p, before)) return "Let your Chi settle, then speak to me again.";
            var stamp = recent.GetValue(p, _ => new Recent()); if (stamp.Until > now) return "Your last exchange is complete.";
            uint gain = ExchangeChi[kind - 20, count - 2];
            if ((ulong)before + gain > uint.MaxValue) return "Spend some Chi first. Keep your tokens until then.";
            using (var c = DataHolder.MySqlConnection) { c.Open(); using (var t = c.BeginTransaction()) {
                Owner(c, t, p); string season = null; var items = new List<ConquerItem>(); uint type = (uint)(3006840 + kind - 20);
                using (var q = Q.Command(c, t, @"SELECT item_uid,season FROM revora_bp_tokens WHERE uid=@uid AND kind=@kind
AND spent_at IS NULL AND expires_at>@now ORDER BY season,item_uid LIMIT 64 FOR UPDATE", "uid", (long)p.Entity.UID, "kind", kind, "now", now))
                using (var r = q.ExecuteReader()) while (r.Read()) {
                    string candidate = r.GetString(1); ConquerItem item;
                    if (season != candidate) { if (items.Count >= count) break; items.Clear(); season = candidate; }
                    if (p.Inventory.TryGetItem(checked((uint)r.GetInt64(0)), out item) && Usable(item, type)) items.Add(item);
                }
                if (items.Count < count) return "Bring matching tokens you earned this season, and keep them unlocked in your bag.";
                var chosen = items.Take(count).ToArray(); uint after = before + gain;
                foreach (var item in chosen) {
                    if (Q.Execute(c, t, @"DELETE FROM items WHERE uid=@item AND entityid=@uid AND id=@type AND bound=1 AND stacksize=1
AND position=0 AND warehouse=0 AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0
AND COALESCE(NULLIF(days,''),'0')='0'", "item", (long)item.UID, "uid", (long)p.Entity.UID, "type", (int)type) != 1)
                        throw new InvalidOperationException("BP token ownership changed.");
                    if (Q.Execute(c, t, "UPDATE revora_bp_tokens SET spent_at=@now WHERE item_uid=@item AND uid=@uid AND kind=@kind AND season=@season AND spent_at IS NULL AND expires_at>@now",
                        "now", now, "item", (long)item.UID, "uid", (long)p.Entity.UID, "kind", kind, "season", season) != 1) throw new InvalidOperationException("BP token origin changed.");
                }
                AddChi(c, t, p, before, after);
                if (Q.Execute(c, t, "INSERT INTO revora_bp_exchanges(id,uid,kind,season,tokens,chi,exchanged_at) VALUES(@id,@uid,@kind,@season,@tokens,@chi,@now)",
                    "id", Guid.NewGuid(), "uid", (long)p.Entity.UID, "kind", kind, "season", season, "tokens", chosen.Select(i => (long)i.UID).ToArray(), "chi", (long)gain, "now", now) != 1)
                    throw new InvalidOperationException("BP exchange receipt was not saved.");
                t.Commit(); ChiWallet.Committed(p, before, after); Q.Install(p, chosen.Select(i => i.UID), new ConquerItem[0]); stamp.Until = now.AddSeconds(1);
                p.Send(new ChiPowers(true).Query(p)); return "Your victories grant you " + gain + " Chi points.";
            } }
        }
        internal static bool HandleNpc(GameState p, NpcRequest request, DateTime now)
        {
            if (p == null || p.ItemSyncRoot == null || request == null) return true;
            lock (p.ItemSyncRoot) {
                if (!Q.Near(p, Manager, 1002)) return true;
                try {
                    if (request.OptionID >= 1 && request.OptionID <= 3) {
                        if (LocalPkWar.Current == null || !LocalPkWar.Current.Enroll(p, request.OptionID + 19)) p.Send("That trial is closed, full, or outside your Battle Power division. A used place cannot be reclaimed.");
                        return true;
                    }
                    if (request.OptionID == 10) { p.Send(Claim(p, now)); Q.Refresh(p); return true; }
                    if (request.OptionID >= 20 && request.OptionID <= 28) { int choice = request.OptionID - 20; p.Send(Exchange(p, 20 + choice / 3, 2 + choice % 3, now)); return true; }
                    var d = new Npcs(p);
                    if (request.OptionID == 11) {
                        d.Text("Show me matching tokens from one season. More victories earn greater Chi.");
                        for (int division = 0; division < 3; division++) for (int number = 2; number <= 4; number++)
                            d.Option(Divisions[division] + ": " + number + " tokens for " + ExchangeChi[division, number - 2] + " Chi.", (byte)(20 + division * 3 + number - 2));
                    } else if (request.OptionID == 12) {
                        d.Text("Four Sunday trials mark each month. Enter from 20:00 to 20:10 Pacific; the last worthy survivor wins before 21:00. Your division is fixed for the round. Exceed its power limit and your place is lost.");
                        d.Text("Tokens expire when the next month's first trial opens. CP and Chi purses wait for you. These are Revora's season rules.");
                    } else {
                        d.Text("The Sunday trials await. New champions earn a bound P7 soul with their purse. Choose your power division.");
                        for (byte i = 0; i < 3; i++) d.Option("Enter " + Divisions[i] + " BP.", (byte)(i + 1));
                        d.Option("Claim my champion's purse.", 10); d.Option("Exchange my tokens.", 11); d.Option("Tell me the trial rules.", 12);
                    }
                    d.Option("Farewell.", 255); d.Send();
                } catch (Exception ex) { Q.Fail(p, "bp-pk", ex); }
                return true;
            }
        }
    }
}
