using System;
using System.Collections.Generic;
using System.Drawing;
using Revora.Client;
using Revora.Database;
using Revora.Interfaces;
using Revora.Network.GamePackets;

namespace Revora.Game.Features
{
    public static class LastManStandingSystem
    {
        public const uint NpcId = 900000003;
        public const string NpcName = "LastManStanding";
        public const int Capacity = 32;
        public const int MinPlayers = 2;
        public static uint CpPrize { get { return Revora.Config.LastManStandingCp; } }
        public static ulong GoldPrize { get { return Revora.Config.LastManStandingGold; } }
        public static uint RevoraPointPrize { get { return Revora.Config.LastManStandingRevoraPoints; } }
        public const string ClaimKey = "lms-claim";
        public const ushort TwinCityMap = 1002;
        public const ushort TwinCityX = 408;
        public const ushort TwinCityY = 330;
        public const ushort ClaimX = 354;
        public const ushort ClaimY = 295;
        // Twin City Arena Manager look. Do not share lookface 12 (mesh 123) with the other clerks.
        public const ushort Mesh = 50340;

        private static readonly int[] StartHours = new int[] { 0, 2, 4, 6, 8, 10, 12, 14, 16, 18, 20, 22 };
        private static readonly TimeSpan RegisterWindow = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan FightLimit = TimeSpan.FromMinutes(15);
        private static readonly object Gate = new object();

        private static Map EventMap;
        private static DateTime CycleStart;
        private static DateTime FightUntil;
        private static bool Running;
        private static bool RegisterOpen;
        private static bool Invited;
        private static Time32 PeaceUntil;
        private static readonly HashSet<uint> Registered = new HashSet<uint>();
        private static readonly HashSet<uint> Alive = new HashSet<uint>();
        private static readonly HashSet<uint> Eliminated = new HashSet<uint>();
        private static uint PendingWinner;
        private static string PendingCycle;
        private static bool AbortRound;
        private static readonly HashSet<uint> PendingReturns = new HashSet<uint>();

        internal static void InitializePrizes()
        {
            LmsAdmissions.Initialize();
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); QuestInventory.Execute(c, null, @"CREATE TABLE IF NOT EXISTS revora_lms_victories(
cycle varchar(20) PRIMARY KEY,uid bigint NOT NULL,earned_at timestamptz NOT NULL DEFAULT now());
CREATE TABLE IF NOT EXISTS revora_lms_offline_prizes(
cycle varchar(20) PRIMARY KEY REFERENCES revora_lms_victories(cycle),uid bigint NOT NULL,applied_at timestamptz NULL);
CREATE TABLE IF NOT EXISTS revora_lms_selected_winners(
cycle varchar(20) PRIMARY KEY,uid bigint NOT NULL CHECK(uid>0),selected_at timestamptz NOT NULL DEFAULT now());
CREATE INDEX IF NOT EXISTS revora_lms_offline_pending ON revora_lms_offline_prizes(uid) WHERE applied_at IS NULL;");
            }
        }

        public static void Create()
        {
            InitializePrizes();
            RecoverSelectedVictories();
            LmsAdmissions.RecoverInterrupted();
            INpc existing = null;
            Map from = null;
            foreach (Map map in Kernel.Maps.Values)
            {
                if (map == null || map.Npcs == null) continue;
                if (map.Npcs.TryGetValue(NpcId, out existing))
                {
                    from = map;
                    break;
                }
            }
            if (existing != null && from != null)
            {
                from.RemoveNpc(existing);
                if (Map.SecoundNpc.ContainsKey(NpcId))
                    Map.SecoundNpc.Remove(NpcId);
            }
            CityGuardSystem.SpawnTalker(NpcId, TwinCityMap, ClaimX, ClaimY, Mesh, NpcName, (byte)Enums.ConquerAngle.South);
            try
            {
                using (var cmd = new MySqlCommand(MySqlCommandType.UPDATE))
                    cmd.Update("npcs").Set("mapid", TwinCityMap).Set("cellx", ClaimX).Set("celly", ClaimY).Set("lookface", Mesh).Where("id", NpcId).Execute();
            }
            catch
            {
            }
            Map source = null;
            if (Kernel.Maps.ContainsKey(700))
                source = Kernel.Maps[700];
            else if (Kernel.Maps.ContainsKey(1507))
                source = Kernel.Maps[1507];
            else if (Kernel.Maps.ContainsKey(1005))
                source = Kernel.Maps[1005];
            else if (Kernel.Maps.ContainsKey(1002))
                source = Kernel.Maps[1002];
            if (source != null)
            {
                EventMap = source.MakeDynamicMap();
                if (!Constants.PKFreeMaps.Contains(EventMap.ID))
                    Constants.PKFreeMaps.Add(EventMap.ID);
                Console.WriteLine("Last Man Standing arena map " + EventMap.ID + " (base " + EventMap.BaseID + ")");
            }
            else
                Console.WriteLine("Last Man Standing arena skipped: no source map");
            if (Kernel.Maps.ContainsKey(TwinCityMap) && Kernel.Maps[TwinCityMap].Npcs.ContainsKey(NpcId))
                Console.WriteLine(NpcName + " at Twin City " + ClaimX + "," + ClaimY);
            CycleStart = NextStart(DateTime.Now);
        }

        public static DateTime NextStart(DateTime now)
        {
            DateTime today = now.Date;
            for (int i = 0; i < StartHours.Length; i++)
            {
                DateTime slot = today.AddHours(StartHours[i]);
                if (now < slot)
                    return slot;
            }
            return today.AddDays(1).AddHours(StartHours[0]);
        }

        public static bool InEvent(Entity entity)
        {
            if (entity == null || EventMap == null) return false;
            return entity.MapID == EventMap.ID;
        }

        public static bool Isolates(GameState client)
        {
            return client != null && client.Entity != null && InEvent(client.Entity);
        }

        public static void Tick()
        {
            ProcessReturns();
            if (AbortRound) { ResetCycle(true); return; }
            if (PendingWinner != 0) { CompletePendingVictory(); return; }
            if (EventMap == null || CycleStart == default(DateTime)) return;
            DateTime now = DateTime.Now;
            if (!Running && !Invited && now >= CycleStart.Subtract(RegisterWindow) && now < CycleStart)
            {
                Invited = true;
                RegisterOpen = true;
                Kernel.SendWorldMessage(new Message("Last Man Standing registration is open for 5 minutes. Accept the popup or talk to LastManStanding in Twin City (" + ClaimX + ", " + ClaimY + "). Max 32.", Color.Yellow, Message.Center), Server.GamePool);
                InviteAll();
            }
            if (!Running && now >= CycleStart)
                StartFight();
            if (!Running) return;
            if (Time32.Now > PeaceUntil)
                SweepAndCheck(now >= FightUntil);
        }

        public static bool HandleNpc(GameState client, NpcRequest request)
        {
            if (client == null || request == null || client.Entity == null || client.ActiveNpc != NpcId)
                return false;
            Npcs dialog = new Npcs(client);
            switch (request.OptionID)
            {
                case 0:
                    dialog.Text(StatusText());
                    if (RegisterOpen && !Running)
                        dialog.Option("Register", 1);
                    if (HasClaim(client))
                        dialog.Option("Claim my prize", 2);
                    dialog.Option("Goodbye", 255);
                    dialog.Send();
                    return true;
                case 1:
                    dialog.Text(TryRegister(client));
                    dialog.Option("Alright.", 255);
                    dialog.Send();
                    return true;
                case 2:
                    dialog.Text(TryClaim(client));
                    dialog.Option("Alright.", 255);
                    dialog.Send();
                    return true;
            }
            return true;
        }

        public static void OnDeath(GameState client)
        {
            if (client == null || client.Entity == null) return;
            if (!Running || EventMap == null) return;
            if (client.Entity.MapID != EventMap.ID && !Alive.Contains(client.Entity.UID))
                return;
            Eliminate(client, true);
        }

        public static void OnDisconnect(GameState client)
        {
            if (client == null || client.Entity == null) return;
            if (!Running) return;
            lock (Gate)
            {
                if (!Alive.Contains(client.Entity.UID)) return;
                Alive.Remove(client.Entity.UID);
                Eliminated.Add(client.Entity.UID);
                PendingReturns.Add(client.Entity.UID);
            }
            SweepAndCheck(false);
        }

        public static void OnLogin(GameState client)
        {
            if (!QuestInventory.Player(client, false)) return;
            lock (Gate)
            {
                Registered.Remove(client.Entity.UID);
                if (Alive.Remove(client.Entity.UID)) Eliminated.Add(client.Entity.UID);
                {
                    if (LmsAdmissions.HasPendingReturn(client.Entity.UID)) PendingReturns.Add(client.Entity.UID);
                    else PendingReturns.Remove(client.Entity.UID);
                }
            }
            ProcessReturns();
            lock (Gate)
                if (PendingReturns.Contains(client.Entity.UID))
                    QuestInventory.Fail(client, "lms-login-return", new InvalidOperationException("LMS return is pending; reconnect."));
        }

        private static void ProcessReturns()
        {
            lock (Gate)
            {
                var pending = new List<uint>(PendingReturns);
                for (int i = 0; i < Math.Min(Capacity, pending.Count); i++)
                {
                    uint uid = pending[i];
                    try
                    {
                        LmsAdmissions.Forfeit(uid);
                        GameState p;
                        // Offline owners discover their durable return at login.
                        if (!Kernel.GamePool.TryGetValue(uid, out p) || p.Entity.PersistenceRequiresReload || LmsAdmissions.Return(p))
                            PendingReturns.Remove(uid);
                    }
                    catch (Exception ex) { Console.WriteLine("[lms-return] error=" + ex.GetType().Name); }
                }
            }
        }

        private static string StatusText()
        {
            if (Running)
                return "Last Man Standing is live. " + AliveCount() + " fighters remain. Winner claims here: " + CpPrize + " CPs, " + GoldPrize + " gold, " + RevoraPointPrize + " Revora Points and one bound P7 soul.";
            if (RegisterOpen)
                return "Registration is open until " + CycleStart.ToString("HH:mm") + ". Seats " + Registered.Count + "/" + Capacity + ". Last one standing claims CPs, gold, and Revora Points here.";
            return "Next Last Man Standing " + CycleStart.ToString("HH:mm") + ". Registration opens 5 minutes before. Max 32. Death sends you to Twin City with no return.";
        }

        private static void InviteAll()
        {
            foreach (GameState client in Kernel.GamePool.Values)
            {
                if (!CanInvite(client)) continue;
                GameState captured = client;
                captured.MessageOK = p => { TryRegister(p); };
                captured.Send(new NpcReply(NpcReply.MessageBox, "Last Man Standing registration is open. Register now? Max 32. Winner claims a bound P7 soul, CPs, gold, and Revora Points at LastManStanding (" + ClaimX + ", " + ClaimY + ")."));
                captured.Send(new Data(true) { UID = captured.Entity.UID, dwParam = 60, ID = Data.CountDown });
            }
        }

        private static bool CanInvite(GameState client)
        {
            if (!QuestInventory.Player(client)) return false;
            lock (Gate) if (PendingReturns.Contains(client.Entity.UID)) return false;
            ushort map = client.Entity.MapID;
            if (map == 6000 || map == 6001 || map == 6002 || map == 6003 || map == 6004 || map == 3846)
                return false;
            if (client.InQualifier() || client.Entity.PokerTable > 0 || client.PlayRouletteUID > 0)
                return false;
            return true;
        }

        private static string TryRegister(GameState client)
        {
            if (client == null || client.Entity == null) return "Unable to register.";
            if (client.Entity.Dead) return "You cannot register while dead.";
            lock (Gate)
            {
                if (!RegisterOpen || Running) return "Registration is closed.";
                if (!CanInvite(client)) return "You cannot enter the arena right now.";
                if (Registered.Contains(client.Entity.UID)) return "You are already registered.";
                if (Registered.Count >= Capacity) return "Last Man Standing is full (32).";
                Registered.Add(client.Entity.UID);
                return "You are registered. You will be sent to the arena at " + CycleStart.ToString("HH:mm") + ".";
            }
        }

        private static void StartFight()
        {
            List<GameState> enter = new List<GameState>();
            lock (Gate)
            {
                RegisterOpen = false;
                Invited = false;
                if (Running) return;
                foreach (uint uid in Registered)
                {
                    GameState client;
                    if (!Kernel.GamePool.TryGetValue(uid, out client)) continue;
                    if (!CanInvite(client)) continue;
                    enter.Add(client);
                    if (enter.Count >= Capacity) break;
                }
                if (enter.Count < MinPlayers)
                {
                    Kernel.SendWorldMessage(new Message("Last Man Standing did not start. Need at least two registered fighters.", Color.Orange, Message.Center), Server.GamePool);
                    ResetCycle(false);
                    return;
                }
                var locked = new List<GameState>();
                try
                {
                    // Never block on actor ownership while holding the event gate.
                    foreach (var p in enter)
                    {
                        if (!System.Threading.Monitor.TryEnter(p.ItemSyncRoot)) return;
                        locked.Add(p);
                    }
                    LmsAdmissions.Begin(CycleStart.ToString("yyyyMMddHHmm", System.Globalization.CultureInfo.InvariantCulture), EventMap.ID, enter.ToArray());
                    Running = true;
                    Alive.Clear();
                    Eliminated.Clear();
                    FightUntil = DateTime.Now.Add(FightLimit);
                    PeaceUntil = Time32.Now.AddSeconds(10);
                    foreach (GameState client in enter)
                        Alive.Add(client.Entity.UID);
                    foreach (GameState client in enter)
                    {
                        SendIntoArena(client);
                        if (!InEvent(client.Entity)) throw new InvalidOperationException("LMS transfer failed.");
                    }
                }
                catch (Exception ex)
                {
                    AbortRound = true;
                    Console.WriteLine("[lms-admission] error=" + ex.GetType().Name);
                    return;
                }
                finally { foreach (var p in locked) System.Threading.Monitor.Exit(p.ItemSyncRoot); }
            }
            Kernel.SendWorldMessage(new Message("Last Man Standing has begun. " + enter.Count + " fighters. Last one standing claims the prize at LastManStanding.", Color.Red, Message.Center), Server.GamePool);
        }

        private static void SendIntoArena(GameState client)
        {
            if (client == null || client.Entity == null || EventMap == null) return;
            Tuple<ushort, ushort> xy = EventMap.RandomCoordinates();
            client.Entity.Teleport(EventMap.BaseID, EventMap.ID, xy.Item1, xy.Item2);
            client.Entity.PKMode = Enums.PKMode.PK;
            client.Send(new Data(true)
            {
                UID = client.Entity.UID,
                ID = Data.ChangePKMode,
                dwParam = (uint)client.Entity.PKMode
            });
            client.CantAttack = PeaceUntil;
            client.Send(new Message("Fight begins in 10 seconds. Death sends you to Twin City. No return.", Color.Red, Message.Talk));
        }

        private static void Eliminate(GameState client, bool teleport)
        {
            bool check = false;
            lock (Gate)
            {
                if (!Running) return;
                if (!Alive.Remove(client.Entity.UID) && !InEvent(client.Entity))
                    return;
                Eliminated.Add(client.Entity.UID);
                PendingReturns.Add(client.Entity.UID);
                check = true;
            }
            if (teleport) ProcessReturns();
            client.Send(new Message("You are out of Last Man Standing. No return.", Color.Red, Message.TopLeft));
            if (check)
                SweepAndCheck(false);
        }

        private static int AliveCount()
        {
            lock (Gate)
                return Alive.Count;
        }

        private static void SweepAndCheck(bool timeout)
        {
            List<GameState> still = new List<GameState>();
            DateTime cycle;
            lock (Gate)
            {
                if (!Running || AbortRound || PendingWinner != 0) return;
                cycle = CycleStart;
                List<uint> drop = new List<uint>();
                foreach (uint uid in Alive)
                {
                    GameState client;
                    if (!Kernel.GamePool.TryGetValue(uid, out client) || client.Entity == null)
                    {
                        drop.Add(uid);
                        continue;
                    }
                    if (client.Entity.Dead || client.Entity.MapID != EventMap.ID)
                    {
                        drop.Add(uid);
                        continue;
                    }
                    still.Add(client);
                }
                for (int i = 0; i < drop.Count; i++)
                {
                    Alive.Remove(drop[i]);
                    Eliminated.Add(drop[i]);
                    PendingReturns.Add(drop[i]);
                }
            }
            if (still.Count > 1 && !timeout)
            {
                string line = "Last Man Standing: " + still.Count + " remain.";
                for (int i = 0; i < still.Count; i++)
                    still[i].Send(new Message(line, Color.Yellow, Message.FirstRightCorner));
                return;
            }
            if (still.Count == 1)
            {
                Finish(still[0]);
                return;
            }
            if (still.Count == 0 || timeout && still.Count > 1)
            {
                lock (Gate)
                {
                    if (!Running || PendingWinner != 0 || CycleStart != cycle) return;
                    // A death/departure may have changed the roster after our
                    // snapshot. Let the next sweep resolve that surviving actor.
                    if (Alive.Count != still.Count) return;
                    foreach (var p in still)
                        if (p.Entity.Dead || !InEvent(p.Entity)) return;
                    string message = still.Count == 0 ? "Last Man Standing ended with no survivor."
                        : "Time is up. Last Man Standing ends without a winner. The survivors return to Twin City.";
                    Kernel.SendWorldMessage(new Message(message, Color.Orange, Message.Center), Server.GamePool);
                    ResetCycle(true);
                }
            }
        }

        private static void Finish(GameState winner)
        {
            lock (Gate)
            {
                if (PendingWinner == 0)
                {
                    if (!Running || winner == null || winner.Entity == null || winner.Entity.Dead
                        || !InEvent(winner.Entity) || Alive.Count != 1 || !Alive.Contains(winner.Entity.UID)) return;
                    PendingWinner = winner.Entity.UID;
                    PendingCycle = CycleStart.ToString("yyyyMMddHHmm", System.Globalization.CultureInfo.InvariantCulture);
                }
            }
            CompletePendingVictory();
        }

        private static void CompletePendingVictory()
        {
            // Never wait for an actor lock while holding the event gate: death
            // callbacks may already own the actor before entering this gate.
            lock (Gate)
            {
                GameState winner;
                if (PendingWinner == 0) return;
                try { PersistSelectedWinner(PendingWinner, PendingCycle); }
                catch (Exception ex) { Console.WriteLine("[lms-selection] error=" + ex.GetType().Name); return; }
                Kernel.GamePool.TryGetValue(PendingWinner, out winner);
                if (!QuestInventory.Player(winner, false) || winner.Variables == null) winner = null;
                if (winner != null && !System.Threading.Monitor.TryEnter(winner.ItemSyncRoot)) return;
                try
                {
                    if (winner == null) RecordOfflineVictory(PendingWinner, PendingCycle);
                    else if (!RecordVictory(winner, PendingCycle)) return;
                    LmsAdmissions.End(PendingCycle);
                    PendingWinner = 0; PendingCycle = null;
                }
                catch (Exception ex)
                {
                    // No cached entitlement/currency changed. Keep this exact
                    // winner for retry; do not rescan and select another actor.
                    Console.WriteLine("[lms-victory] error=" + ex.GetType().Name);
                    return;
                }
                finally { if (winner != null) System.Threading.Monitor.Exit(winner.ItemSyncRoot); }
                if (winner != null)
                {
                    winner.Send(new Message("You are last standing. Your purse and bound P7 soul await at LastManStanding.", Color.Yellow, Message.TopLeft));
                    Kernel.SendWorldMessage(new Message(winner.Entity.Name + " won Last Man Standing. Claim at LastManStanding in Twin City (" + ClaimX + ", " + ClaimY + ").", Color.Red, Message.Center), Server.GamePool);
                }
                ResetCycle(true);
            }
        }

        private static void PersistSelectedWinner(uint uid, string cycle)
        {
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); using (var t = c.BeginTransaction())
                {
                    QuestInventory.Execute(c, t, "SET LOCAL lock_timeout='1s'");
                    QuestInventory.Execute(c, t, "INSERT INTO revora_lms_selected_winners(cycle,uid) VALUES(@cycle,@uid) ON CONFLICT(cycle) DO NOTHING",
                        "cycle", cycle, "uid", (long)uid);
                    using (var q = QuestInventory.Command(c, t, "SELECT uid FROM revora_lms_selected_winners WHERE cycle=@cycle", "cycle", cycle))
                        if (Convert.ToUInt32(q.ExecuteScalar()) != uid)
                            throw new InvalidOperationException("LMS selected winner was not recorded or conflicts.");
                    t.Commit();
                }
            }
        }

        private static void RecoverSelectedVictories()
        {
            var pending = new List<Tuple<uint, string>>();
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); using (var q = QuestInventory.Command(c, null, @"SELECT s.uid,s.cycle
FROM revora_lms_selected_winners s LEFT JOIN revora_lms_victories v ON v.cycle=s.cycle
WHERE v.cycle IS NULL OR v.uid<>s.uid ORDER BY s.cycle LIMIT 129"))
                using (var r = q.ExecuteReader())
                    while (r.Read()) pending.Add(Tuple.Create(checked((uint)r.GetInt64(0)), r.GetString(1)));
            }
            // Startup stays unavailable if work exceeds the bounded pass; each
            // committed result survives retry and is excluded next time.
            for (int i = 0; i < Math.Min(128, pending.Count); i++)
                RecordOfflineVictory(pending[i].Item1, pending[i].Item2);
            if (pending.Count > 128) throw new InvalidOperationException("LMS selected winner recovery has more pending work.");
        }

        private static void RecordOfflineVictory(uint uid, string cycle)
        {
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); using (var t = c.BeginTransaction())
                {
                    QuestInventory.Execute(c, t, "SET LOCAL lock_timeout='1s'");
                    using (var q = QuestInventory.Command(c, t, "SELECT uid FROM entities WHERE uid=@uid FOR UPDATE", "uid", (long)uid))
                        if (q.ExecuteScalar() == null) throw new InvalidOperationException("LMS winner is missing.");
                    int added = QuestInventory.Execute(c, t, "INSERT INTO revora_lms_victories(cycle,uid) VALUES(@cycle,@uid) ON CONFLICT(cycle) DO NOTHING", "cycle", cycle, "uid", (long)uid);
                    if (added == 0)
                    {
                        using (var q = QuestInventory.Command(c, t, "SELECT uid FROM revora_lms_victories WHERE cycle=@cycle", "cycle", cycle))
                            if (Convert.ToUInt32(q.ExecuteScalar()) != uid) throw new InvalidOperationException("LMS cycle belongs to another winner.");
                    }
                    else if (QuestInventory.Execute(c, t, "INSERT INTO revora_lms_offline_prizes(cycle,uid) VALUES(@cycle,@uid)", "cycle", cycle, "uid", (long)uid) != 1)
                        throw new InvalidOperationException("LMS offline prize was not saved.");
                    t.Commit();
                }
            }
        }

        private static void ImportOfflinePrizes(GameState client)
        {
            lock (client.ItemSyncRoot) lock (client.Variables.SyncRoot)
            {
                if (!QuestInventory.Player(client, false)) return;
                using (var c = DataHolder.MySqlConnection)
                {
                    c.Open(); using (var t = c.BeginTransaction())
                    {
                        SkillTable.LockOwner(c, t, client);
                        long count;
                        using (var q = QuestInventory.Command(c, t, "SELECT count(*) FROM revora_lms_offline_prizes WHERE uid=@uid AND applied_at IS NULL", "uid", (long)client.Entity.UID))
                            count = Convert.ToInt64(q.ExecuteScalar());
                        if (count == 0) return;
                        byte[] before;
                        using (var q = QuestInventory.Command(c, t, "SELECT data FROM variablevault WHERE entityid=@uid FOR UPDATE", "uid", (long)client.Entity.UID))
                            before = q.ExecuteScalar() as byte[];
                        if (before == null) throw new InvalidOperationException("LMS variables are missing.");
                        var saved = new VariableVault(); saved.Deserialize(before);
                        uint current = (uint)saved[ClaimKey];
                        if (current != (uint)client[ClaimKey] || current + count > uint.MaxValue)
                            throw new InvalidOperationException("LMS pending prize count changed.");
                        var next = new VariableVault(); next.Deserialize(client.Variables.Serialize()); next[ClaimKey] = (uint)(current + count);
                        if (QuestInventory.Execute(c, t, "UPDATE variablevault SET data=@data WHERE entityid=@uid AND data=@before", "data", next.Serialize(), "uid", (long)client.Entity.UID, "before", before) != 1
                            || QuestInventory.Execute(c, t, "UPDATE revora_lms_offline_prizes SET applied_at=now() WHERE uid=@uid AND applied_at IS NULL", "uid", (long)client.Entity.UID) != count)
                            throw new InvalidOperationException("LMS offline prize import changed.");
                        t.Commit(); next.Changed = false; client.Variables = next;
                    }
                }
            }
        }

        private static bool RecordVictory(GameState winner, string cycle)
        {
            lock (winner.ItemSyncRoot) lock (winner.Variables.SyncRoot)
            {
                if (!QuestInventory.Player(winner, false)) return false;
                using (var c = DataHolder.MySqlConnection)
                {
                    c.Open(); using (var t = c.BeginTransaction())
                    {
                        SkillTable.LockOwner(c, t, winner);
                        if (QuestInventory.Execute(c, t, "INSERT INTO revora_lms_victories(cycle,uid) VALUES(@cycle,@uid) ON CONFLICT(cycle) DO NOTHING",
                            "cycle", cycle, "uid", (long)winner.Entity.UID) == 0)
                        {
                            using (var q = QuestInventory.Command(c, t, "SELECT uid FROM revora_lms_victories WHERE cycle=@cycle", "cycle", cycle))
                                if (Convert.ToUInt32(q.ExecuteScalar()) != winner.Entity.UID)
                                    throw new InvalidOperationException("LMS cycle already belongs to another winner.");
                            return true;
                        }
                        byte[] before;
                        using (var q = QuestInventory.Command(c, t, "SELECT data FROM variablevault WHERE entityid=@uid FOR UPDATE", "uid", (long)winner.Entity.UID))
                            before = q.ExecuteScalar() as byte[];
                        if (before == null) throw new InvalidOperationException("LMS winner variables are not saved.");
                        var saved = new VariableVault(); saved.Deserialize(before);
                        uint count = (uint)saved[ClaimKey];
                        if (count != (uint)winner[ClaimKey] || count == uint.MaxValue)
                            throw new InvalidOperationException("LMS entitlement count changed.");
                        var next = new VariableVault(); next.Deserialize(winner.Variables.Serialize()); next[ClaimKey] = count + 1;
                        if (QuestInventory.Execute(c, t, "UPDATE variablevault SET data=@data WHERE entityid=@uid AND data=@before",
                            "data", next.Serialize(), "uid", (long)winner.Entity.UID, "before", before) != 1)
                            throw new InvalidOperationException("LMS victory entitlement was not saved.");
                        t.Commit(); next.Changed = false; winner.Variables = next;
                        return true;
                    }
                }
            }
        }

        private static void ResetCycle(bool kickMap)
        {
            lock (Gate)
            {
                try { LmsAdmissions.End(CycleStart.ToString("yyyyMMddHHmm", System.Globalization.CultureInfo.InvariantCulture)); }
                catch (Exception ex) { AbortRound = true; Console.WriteLine("[lms-end] error=" + ex.GetType().Name); return; }
                foreach (uint uid in Registered) PendingReturns.Add(uid);
                foreach (uint uid in Alive) PendingReturns.Add(uid);
                foreach (uint uid in Eliminated) PendingReturns.Add(uid);
                AbortRound = false;
                Running = false;
                RegisterOpen = false;
                Invited = false;
                Registered.Clear();
                Alive.Clear();
                Eliminated.Clear();
                CycleStart = NextStart(DateTime.Now.AddSeconds(2));
            }
            ProcessReturns();
        }

        private static bool HasClaim(GameState client)
        {
            if (!QuestInventory.Player(client, false) || client.Variables == null) return false;
            try { ImportOfflinePrizes(client); return (uint)client[ClaimKey] != 0; }
            catch (Exception ex) { QuestInventory.Fail(client, "lms-offline-prize", ex); return false; }
        }

        private static string TryClaim(GameState client)
        {
            if (!QuestInventory.Player(client) || client.Variables == null)
                return "Return when you are ready to claim your prize.";
            lock (client.ItemSyncRoot) lock (client.Variables.SyncRoot)
            try
            {
                if (!QuestInventory.Player(client) || !HasClaim(client))
                    return "You have no Last Man Standing prize to claim.";
                if (client.Inventory.Count >= 40)
                    return "Leave one space for your soul. Your champion's prize will wait.";
                uint cpPrize = CpPrize, rvpPrize = RevoraPointPrize;
                ulong goldPrize = GoldPrize;
                long cp = (long)client.Entity.ConquerPoints + cpPrize;
                long rvp = (long)client.Entity.RevoraPoint + rvpPrize;
                decimal gold = (decimal)client.Entity.Money + goldPrize;
                if (cp > uint.MaxValue || rvp > uint.MaxValue || gold > Entity.MaximumSilver)
                    return "Make room in your purse. Your prize will wait.";

                var next = new VariableVault(); next.Deserialize(client.Variables.Serialize());
                var soul = QuestSoulReward.Create();
                using (var c = DataHolder.MySqlConnection)
                {
                    c.Open(); using (var t = c.BeginTransaction())
                    {
                        SilverBalance.AssertSaved(c, t, client);
                        byte[] before;
                        using (var q = QuestInventory.Command(c, t,
                            "SELECT data FROM variablevault WHERE entityid=@uid FOR UPDATE", "uid", (long)client.Entity.UID))
                            before = q.ExecuteScalar() as byte[];
                        if (before == null) throw new InvalidOperationException("Last Man Standing entitlement is not saved.");
                        var saved = new VariableVault(); saved.Deserialize(before);
                        if ((uint)saved[ClaimKey] == 0)
                            throw new InvalidOperationException("Last Man Standing entitlement is already consumed.");
                        if ((uint)saved[ClaimKey] != (uint)client[ClaimKey])
                            throw new InvalidOperationException("Last Man Standing entitlement count changed.");
                        next[ClaimKey] = (uint)saved[ClaimKey] - 1;
                        if (QuestInventory.Execute(c, t, @"UPDATE entities SET conquerpoints=@cp,revorapoint=@rvp,money=@gold
WHERE uid=@uid AND conquerpoints=@oldcp AND revorapoint=@oldrvp",
                            "cp", cp, "rvp", rvp, "gold", gold, "uid", (long)client.Entity.UID,
                            "oldcp", (long)client.Entity.ConquerPoints, "oldrvp", (long)client.Entity.RevoraPoint) != 1)
                            throw new InvalidOperationException("Last Man Standing purse changed.");
                        if (QuestInventory.Execute(c, t, @"UPDATE variablevault SET data=@data
WHERE entityid=@uid AND data=@before", "data", next.Serialize(),
                            "uid", (long)client.Entity.UID, "before", before) != 1)
                            throw new InvalidOperationException("Last Man Standing entitlement changed.");
                        QuestInventory.Insert(c, t, client.Entity.UID, soul);
                        t.Commit();
                    }
                }
                next.Changed = false; client.Variables = next;
                client.Entity.ApplyCommittedConquerPoints((uint)cp);
                client.Entity.ApplyCommittedRevoraPoints((uint)rvp);
                SilverBalance.ApplyCommitted(client, (ulong)gold);
                QuestInventory.Install(client, new uint[0], new[] { soul });
                return "Your champion's purse and bound P7 soul are yours. May your next battle be worthy of them.";
            }
            catch (Exception ex)
            {
                QuestInventory.Fail(client, "lms-prize", ex);
                return "Your prize could not be collected. Reconnect and try again.";
            }
        }
    }
}
