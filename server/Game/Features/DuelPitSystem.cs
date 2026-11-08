using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using Revora.Client;
using Revora.Database;
using Revora.Interfaces;
using Revora.Network.GamePackets;

namespace Revora.Game.Features
{
    public static class DuelPitSystem
    {
        public const uint NpcId = 900000011;
        public const string NpcName = "DuelPit";
        public const int Capacity = 24;
        public const int MinPlayers = 2;
        public const int MaxRounds = 16;
        public const string ClaimKey = "duel-claim";
        public const ushort TwinCityMap = 1002;
        public const ushort TwinCityX = 408;
        public const ushort TwinCityY = 330;
        public const ushort ClaimX = 370;
        public const ushort ClaimY = 307;
        // Twin City Team PK Manager look. Distinct from LMS (9430) and RelicRush (6207).
        public const ushort Mesh = 50440;

        private static readonly int[] StartHours = new int[] { 11, 19 };
        private static readonly TimeSpan RegisterWindow = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan DuelLimit = TimeSpan.FromMinutes(3);
        private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(45);
        private static readonly TimeSpan EventLimit = TimeSpan.FromMinutes(25);
        private static readonly object Gate = new object();

        private static bool ResultsReady;
        private static int Finishing;
        private static List<KeyValuePair<uint, uint>> PendingRank;
        internal static void InitializeResults()
        {
            using (var c = DataHolder.MySqlConnection) {
                c.Open(); QuestInventory.Execute(c, null, @"CREATE TABLE IF NOT EXISTS revora_duel_results(
cycle text NOT NULL,place integer NOT NULL CHECK(place BETWEEN 1 AND 4),uid bigint NOT NULL,
score bigint NOT NULL,imported boolean NOT NULL DEFAULT false,
PRIMARY KEY(cycle,place),UNIQUE(cycle,uid));");
            }
            DuelAdmissions.Initialize();
            ResultsReady = true;
        }

        private static readonly HashSet<uint> PendingReturns = new HashSet<uint>();
        public static void OnLogin(GameState client)
        {
            if (!ResultsReady || !QuestInventory.Player(client,false)) return;
            lock (Gate) {
                    if (DuelAdmissions.HasPendingReturn(client.Entity.UID)) PendingReturns.Add(client.Entity.UID);
                    else PendingReturns.Remove(client.Entity.UID);
                }
            ProcessReturns();
            lock (Gate) if (PendingReturns.Contains(client.Entity.UID))
                QuestInventory.Fail(client,"duel-login-return",new InvalidOperationException("Duel Pit return pending."));
        }
        private static void ProcessReturns()
        {
            uint[] pending; lock (Gate) pending = PendingReturns.Take(Capacity).ToArray();
            foreach (uint uid in pending) {
                if (!SessionInstall.TryEnter(uid)) continue;
                try {
                    DuelAdmissions.Forfeit(uid);
                    GameState p;
                    if (!Kernel.GamePool.TryGetValue(uid,out p) || p.Entity.PersistenceRequiresReload || DuelAdmissions.Return(p))
                        lock (Gate) PendingReturns.Remove(uid);
                } catch (Exception ex) { Console.WriteLine("[duel-return] error="+ex.GetType().Name); }
                finally { SessionInstall.Exit(uid); }
            }
        }

        private static Map EventMap;
        private static DateTime CycleStart;
        private static DateTime FightUntil;
        private static DateTime DuelUntil;
        private static DateTime WaitUntil;
        private static Time32 PeaceUntil;
        private static bool Running;
        private static bool RegisterOpen;
        private static bool Invited;
        private static bool Settling;
        private static int Round;
        private static uint King;
        private static uint Challenger;
        private static readonly List<uint> Queue = new List<uint>();
        private static readonly HashSet<uint> Registered = new HashSet<uint>();
        private static readonly Dictionary<uint, uint> Wins = new Dictionary<uint, uint>();
        private static readonly Dictionary<uint, int> ElimRound = new Dictionary<uint, int>();

        public static void Create()
        {
            InitializeResults();
            DuelAdmissions.RecoverInterrupted();
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
            else if (Kernel.Maps.ContainsKey(1005))
                source = Kernel.Maps[1005];
            else if (Kernel.Maps.ContainsKey(1002))
                source = Kernel.Maps[1002];
            if (source != null)
            {
                EventMap = source.MakeDynamicMap();
                if (!Constants.PKFreeMaps.Contains(EventMap.ID))
                    Constants.PKFreeMaps.Add(EventMap.ID);
                Console.WriteLine("DuelPit arena map " + EventMap.ID + " (base " + EventMap.BaseID + ")");
            }
            else
                Console.WriteLine("DuelPit arena skipped: no source map");
            if (Kernel.Maps.ContainsKey(TwinCityMap) && Kernel.Maps[TwinCityMap].Npcs.ContainsKey(NpcId))
                Console.WriteLine(NpcName + " at Twin City " + ClaimX + "," + ClaimY);
            CycleStart = NextStart(DateTime.Now);
        }

        public static DateTime NextStart(DateTime now)
        {
            DateTime today = now.Date;
            for (int i = 0; i < StartHours.Length; i++)
            {
                DateTime slot = today.AddHours(StartHours[i]).AddMinutes(15);
                if (now < slot)
                    return slot;
            }
            return today.AddDays(1).AddHours(StartHours[0]).AddMinutes(15);
        }

        public static bool InEvent(Entity entity)
        {
            if (entity == null || EventMap == null) return false;
            return entity.MapID == EventMap.ID;
        }

        public static bool SuppressCrime(Entity killer, Entity victim)
        {
            return InEvent(killer) && InEvent(victim);
        }

        public static void Tick()
        {
            if (ResultsReady) ProcessReturns();
            if (EventMap == null || CycleStart == default(DateTime)) return;
            DateTime now = DateTime.Now;
            if (!Running && !Invited && now >= CycleStart.Subtract(RegisterWindow) && now < CycleStart)
            {
                Invited = true;
                RegisterOpen = true;
                Kernel.SendWorldMessage(new Message("Duel Pit registration is open for 5 minutes. 1v1. Winner stays 16 rounds. Talk to DuelPit in Twin City (" + ClaimX + ", " + ClaimY + "). Max 24.", Color.Yellow, Message.Center), Server.GamePool);
                InviteAll();
            }
            if (!Running && now >= CycleStart)
                StartFight();
            if (!Running) return;
            Pulse(now);
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
            if (!Running) return;
            uint uid = client.Entity.UID;
            if (uid != King && uid != Challenger) return;
            GameState winner = GetFighter(uid == King ? Challenger : King);
            if (winner == null)
            {
                KickOut(client, "You are out of Duel Pit.");
                Finish();
                return;
            }
            EndDuel(winner, client);
        }

        public static void OnDisconnect(GameState client)
        {
            if (client == null || client.Entity == null) return;
            if (ResultsReady) lock (Gate)
                if (Registered.Contains(client.Entity.UID) || Wins.ContainsKey(client.Entity.UID)) PendingReturns.Add(client.Entity.UID);
            if (!Running) return;
            uint uid = client.Entity.UID;
            if (uid != King && uid != Challenger) return;
            GameState winner = GetFighter(uid == King ? Challenger : King);
            if (winner == null)
            {
                Finish();
                return;
            }
            EndDuel(winner, client);
        }

        private static string StatusText()
        {
            if (Running)
                return "Duel Pit is live. Round " + Round + "/" + MaxRounds + ". Winner stays. Loser goes to Twin City. First through fourth claim here.";
            if (RegisterOpen)
                return "Registration is open until " + CycleStart.ToString("HH:mm") + ". Seats " + Registered.Count + "/" + Capacity + ". 1v1. Winner stays 16 rounds. First through fourth earn a purse and a bound P7 soul.";
            return "Next Duel Pit " + CycleStart.ToString("HH:mm") + ". Twice a day, 11:15 and 19:15. Register 5 minutes before. 1v1. Winner stays. 16 rounds. Top four claim.";
        }

        private static void InviteAll()
        {
            foreach (GameState client in Kernel.GamePool.Values)
            {
                if (!CanInvite(client)) continue;
                GameState captured = client;
                captured.MessageOK = p => { TryRegister(p); };
                captured.Send(new NpcReply(NpcReply.MessageBox, "Duel Pit is open. 1v1. Winner stays 16 rounds. First through fourth get prizes. Register at DuelPit (" + ClaimX + ", " + ClaimY + ")?"));
                captured.Send(new Data(true) { UID = captured.Entity.UID, dwParam = 60, ID = Data.CountDown });
            }
        }

        private static bool CanInvite(GameState client)
        {
            if (!QuestInventory.Player(client)) return false;
            ushort map = client.Entity.MapID;
            if (map == 6000 || map == 6001 || map == 6002 || map == 6003 || map == 6004 || map == 3846)
                return false;
            if (client.InQualifier() || client.Entity.PokerTable > 0 || client.PlayRouletteUID > 0)
                return false;
            return true;
        }

        private static string TryRegister(GameState client)
        {
            if (!CanInvite(client)) return "Unable to register.";
            if (client.Entity.Dead) return "You cannot register while dead.";
            lock (Gate)
            {
                if (!RegisterOpen || Running) return "Registration is closed.";
                if (Registered.Contains(client.Entity.UID)) return "You are already registered.";
                if (Registered.Count >= Capacity) return "Duel Pit is full (24).";
                Registered.Add(client.Entity.UID);
                return "You are registered. 1v1 starts at " + CycleStart.ToString("HH:mm") + ". Winner stays. Loser goes to Twin City.";
            }
        }

        private static void StartFight()
        {
            List<uint> enter = new List<uint>();
            lock (Gate)
            {
                RegisterOpen = false;
                if (Running) return;
                foreach (uint uid in Registered)
                {
                    GameState client;
                    if (!Kernel.GamePool.TryGetValue(uid, out client)) continue;
                    if (!CanInvite(client)) continue;
                    enter.Add(uid);
                    if (enter.Count >= Capacity) break;
                }
                if (enter.Count < MinPlayers)
                {
                    Kernel.SendWorldMessage(new Message("Duel Pit did not start. Need at least two registered fighters.", Color.Orange, Message.Center), Server.GamePool);
                    ResetCycle(false);
                    return;
                }
                var leased = new List<uint>(); var locked = new List<GameState>();
                try {
                    foreach (uint uid in enter.OrderBy(uid=>uid)) {
                        if (!SessionInstall.TryEnter(uid)) return;
                        leased.Add(uid);
                        GameState p;
                        if (!Kernel.GamePool.TryGetValue(uid,out p) || !Monitor.TryEnter(p.ItemSyncRoot)) return;
                        locked.Add(p);
                    }
                    if (EventMap == null) throw new InvalidOperationException("Duel arena unavailable.");
                    DuelAdmissions.Begin(CycleStart.ToString("yyyyMMddHHmm",System.Globalization.CultureInfo.InvariantCulture),EventMap.ID,locked.ToArray());
                } catch (Exception ex) {
                    Console.WriteLine("[duel-admission] error="+ex.GetType().Name);return;
                } finally {
                    foreach (var p in locked) Monitor.Exit(p.ItemSyncRoot);
                    foreach (uint uid in leased) SessionInstall.Exit(uid);
                }
                Running = true;
                Settling = false;
                Round = 0;
                King = 0;
                Challenger = 0;
                Queue.Clear();
                Wins.Clear();
                ElimRound.Clear();
                FightUntil = DateTime.Now.Add(EventLimit);
                for (int i = 0; i < enter.Count; i++)
                {
                    Queue.Add(enter[i]);
                    Wins[enter[i]] = 0;
                }
            }
            Kernel.SendWorldMessage(new Message("Duel Pit has begun. " + enter.Count + " fighters. 1v1. Winner stays 16 rounds. Top four claim at DuelPit.", Color.Red, Message.Center), Server.GamePool);
            BeginNextDuel();
        }

        private static void BeginNextDuel()
        {
            GameState king = GetFighter(King);
            uint next = 0;
            lock (Gate)
            {
                if (!Running) return;
                if (Queue.Count == 0)
                {
                    Challenger = 0;
                    WaitUntil = DateTime.Now.Add(WaitLimit);
                    return;
                }
                next = Queue[0];
                Queue.RemoveAt(0);
                if (King == 0)
                {
                    King = next;
                    if (Queue.Count == 0)
                    {
                        Challenger = 0;
                        WaitUntil = DateTime.Now.Add(WaitLimit);
                        return;
                    }
                    next = Queue[0];
                    Queue.RemoveAt(0);
                }
                Challenger = next;
                DuelUntil = DateTime.Now.Add(DuelLimit);
                PeaceUntil = Time32.Now.AddSeconds(8);
                Settling = false;
            }
            king = GetFighter(King);
            GameState foe = GetFighter(Challenger);
            if (king == null || foe == null)
            {
                if (king == null) King = 0;
                if (foe == null) Challenger = 0;
                BeginNextDuel();
                return;
            }
            Heal(king);
            Tuple<ushort, ushort> a;
            Tuple<ushort, ushort> b;
            TwoSpawns(out a, out b);
            SendFighter(king, a.Item1, a.Item2);
            SendFighter(foe, b.Item1, b.Item2);
            string line = "Round " + (Round + 1) + "/" + MaxRounds + ". Fight in 8 seconds. Winner stays.";
            king.Send(new Message(line, Color.Red, Message.TopLeft));
            foe.Send(new Message(line, Color.Red, Message.TopLeft));
        }

        private static void TwoSpawns(out Tuple<ushort, ushort> a, out Tuple<ushort, ushort> b)
        {
            a = EventMap.RandomCoordinates();
            b = EventMap.RandomCoordinates();
            for (int i = 0; i < 20; i++)
            {
                Tuple<ushort, ushort> x = EventMap.RandomCoordinates();
                Tuple<ushort, ushort> y = EventMap.RandomCoordinates();
                if (Kernel.GetDistance(x.Item1, x.Item2, y.Item1, y.Item2) >= 18)
                {
                    a = x;
                    b = y;
                    return;
                }
            }
        }

        private static void SendFighter(GameState client, ushort x, ushort y)
        {
            if (client == null || client.Entity == null || EventMap == null) return;
            client.Entity.Teleport(EventMap.BaseID, EventMap.ID, x, y);
            client.Entity.PKMode = Enums.PKMode.PK;
            client.Send(new Data(true)
            {
                UID = client.Entity.UID,
                ID = Data.ChangePKMode,
                dwParam = (uint)client.Entity.PKMode
            });
            client.CantAttack = PeaceUntil;
        }

        private static void Pulse(DateTime now)
        {
            if (PendingRank != null) { Finish(); return; }
            if (now >= FightUntil)
            {
                Finish();
                return;
            }
            GameState king = GetFighter(King);
            GameState foe = GetFighter(Challenger);
            if (King != 0 && king == null)
            {
                if (foe != null)
                    EndDuel(foe, null);
                else
                    Finish();
                return;
            }
            if (Challenger != 0 && foe == null)
            {
                if (king != null)
                    EndDuel(king, null);
                else
                    Finish();
                return;
            }
            if (Challenger == 0)
            {
                if (now >= WaitUntil)
                    Finish();
                else if (king != null)
                    king.Send(new Message("Duel Pit  Round " + Round + "/" + MaxRounds + "  Waiting for opponent", Color.Yellow, Message.FirstRightCorner));
                return;
            }
            if (Time32.Now <= PeaceUntil)
            {
                TellCorner(king, foe);
                return;
            }
            if (now >= DuelUntil)
            {
                ResolveTimeout(king, foe);
                return;
            }
            TellCorner(king, foe);
        }

        private static void ResolveTimeout(GameState king, GameState foe)
        {
            if (king == null && foe == null)
            {
                Finish();
                return;
            }
            if (king == null)
            {
                EndDuel(foe, null);
                return;
            }
            if (foe == null)
            {
                EndDuel(king, null);
                return;
            }
            int kp = HpPercent(king.Entity);
            int fp = HpPercent(foe.Entity);
            if (fp > kp)
                EndDuel(foe, king);
            else
                EndDuel(king, foe);
        }

        private static int HpPercent(Entity entity)
        {
            if (entity == null || entity.MaxHitpoints == 0) return 0;
            return (int)(entity.Hitpoints * 100 / entity.MaxHitpoints);
        }

        private static void EndDuel(GameState winner, GameState loser)
        {
            lock (Gate)
            {
                if (!Running || Settling || PendingRank != null) return;
                Settling = true;
            }
            if (loser != null && loser.Entity != null)
            {
                lock (Gate)
                    ElimRound[loser.Entity.UID] = Round + 1;
                KickOut(loser, "You lost the duel. Winner stays.");
            }
            if (winner == null || winner.Entity == null)
            {
                Finish();
                return;
            }
            uint winUid = winner.Entity.UID;
            lock (Gate)
            {
                if (!Running || PendingRank != null) return;
                uint score;
                Wins.TryGetValue(winUid, out score);
                Wins[winUid] = score + 1;
                King = winUid;
                Challenger = 0;
                Round++;
            }
            winner.Send(new Message("You stay in the pit. Round " + Round + "/" + MaxRounds + ".", Color.Lime, Message.TopLeft));
            if (Round >= MaxRounds)
            {
                Finish();
                return;
            }
            Heal(winner);
            BeginNextDuel();
        }

        private static void Finish()
        {
            if (System.Threading.Interlocked.CompareExchange(ref Finishing, 1, 0) != 0) return;
            try {
                List<KeyValuePair<uint, uint>> rank;
                string cycle;
                lock (Gate) {
                    if (!Running || !ResultsReady) return;
                    if (PendingRank == null) {
                        uint kingUid = King;
                        rank = new List<KeyValuePair<uint, uint>>(Wins);
                        rank.Sort(delegate(KeyValuePair<uint, uint> a, KeyValuePair<uint, uint> b) {
                            if (a.Key == kingUid && b.Key != kingUid) return -1;
                            if (b.Key == kingUid && a.Key != kingUid) return 1;
                            int score = b.Value.CompareTo(a.Value); if (score != 0) return score;
                            int ea, eb; ElimRound.TryGetValue(a.Key, out ea); ElimRound.TryGetValue(b.Key, out eb);
                            int eliminated = eb.CompareTo(ea);
                            return eliminated != 0 ? eliminated : a.Key.CompareTo(b.Key);
                        });
                        PendingRank = new List<KeyValuePair<uint, uint>>();
                        for (int i = 0; i < rank.Count && PendingRank.Count < 4; i++)
                            if (i == 0 || rank[i].Value != 0 || rank[i].Key == kingUid) PendingRank.Add(rank[i]);
                    }
                    rank = PendingRank;
                    cycle = CycleStart.ToString("yyyyMMddHHmm", System.Globalization.CultureInfo.InvariantCulture);
                }
                using (var c = DataHolder.MySqlConnection) {
                    c.Open(); using (var t = c.BeginTransaction()) {
                        for (int i = 0; i < rank.Count; i++) {
                            QuestInventory.Execute(c,t,@"INSERT INTO revora_duel_results(cycle,place,uid,score)
VALUES(@cycle,@place,@uid,@score) ON CONFLICT(cycle,place) DO NOTHING",
                                "cycle",cycle,"place",i+1,"uid",(long)rank[i].Key,"score",(long)rank[i].Value);
                            using (var q = QuestInventory.Command(c,t,@"SELECT EXISTS(SELECT 1 FROM revora_duel_results
WHERE cycle=@cycle AND place=@place AND uid=@uid AND score=@score)",
                                "cycle",cycle,"place",i+1,"uid",(long)rank[i].Key,"score",(long)rank[i].Value))
                                if (!Equals(q.ExecuteScalar(),true)) throw new InvalidOperationException("Duel Pit result changed.");
                        }
                        using (var q = QuestInventory.Command(c,t,"SELECT count(*) FROM revora_duel_results WHERE cycle=@cycle","cycle",cycle))
                            if ((long)q.ExecuteScalar()!=rank.Count) throw new InvalidOperationException("Duel Pit result roster changed.");
                        t.Commit();
                    }
                }
                // All recipients are durable before online notifications or arena cleanup.
                for (int i = 0; i < rank.Count; i++) {
                    GameState client;
                    if (Kernel.GamePool.TryGetValue(rank[i].Key,out client) && QuestInventory.Player(client,false))
                        client.Send(new Message("You placed " + (i+1) + " in Duel Pit. Your prize awaits at DuelPit.",Color.Yellow,Message.TopLeft));
                }
                ResetCycle(true);
            }
            catch (Exception ex) { Console.WriteLine("[duel-result] error="+ex.GetType().Name); }
            finally { System.Threading.Interlocked.Exchange(ref Finishing,0); }
        }

        private static void ResetCycle(bool kickMap)
        {
            List<GameState> kick = new List<GameState>();
            lock (Gate)
            {
                DuelAdmissions.End(CycleStart.ToString("yyyyMMddHHmm",System.Globalization.CultureInfo.InvariantCulture));
                foreach (uint uid in Registered) PendingReturns.Add(uid);
                foreach (uint uid in Wins.Keys) PendingReturns.Add(uid);
                Running = false;
                RegisterOpen = false;
                Invited = false;
                Settling = false;
                King = 0;
                Challenger = 0;
                Round = 0;
                Registered.Clear();
                Queue.Clear();
                Wins.Clear();
                ElimRound.Clear();
                PendingRank = null;
                CycleStart = NextStart(DateTime.Now.AddSeconds(2));
            }
            if (kickMap && EventMap != null)
            {
                foreach (GameState client in Kernel.GamePool.Values)
                {
                    if (client != null && client.Entity != null && client.Entity.MapID == EventMap.ID)
                        kick.Add(client);
                }
                for (int i = 0; i < kick.Count; i++)
                    lock (Gate) PendingReturns.Add(kick[i].Entity.UID);
            }
            ProcessReturns();
        }

        private static void KickOut(GameState client, string why)
        {
            if (client == null || client.Entity == null) return;
            if (client.Entity.Dead)
            {
                client.Entity.Hitpoints = client.Entity.MaxHitpoints / 2;
                client.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.Dead | (ulong)FlagsUpdate.Flags.Ghost);
            }
            lock (Gate) PendingReturns.Add(client.Entity.UID);
            ProcessReturns();
            client.Send(new Message(why, Color.Red, Message.TopLeft));
        }

        private static void Heal(GameState client)
        {
            if (client == null || client.Entity == null) return;
            client.Entity.Hitpoints = client.Entity.MaxHitpoints;
            client.Entity.Mana = client.Entity.MaxMana;
            client.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.Dead | (ulong)FlagsUpdate.Flags.Ghost);
            client.Entity.Update((byte)FlagsUpdate.DataType.Hitpoints, client.Entity.Hitpoints, false);
        }

        private static GameState GetFighter(uint uid)
        {
            if (uid == 0) return null;
            GameState client;
            if (!Kernel.GamePool.TryGetValue(uid, out client) || client == null || client.Entity == null)
                return null;
            if (client.Entity.Dead) return null;
            if (EventMap != null && client.Entity.MapID != EventMap.ID && (uid == King || uid == Challenger))
            {
                if (Running && Time32.Now > PeaceUntil.AddSeconds(2))
                    return null;
            }
            return client;
        }

        private static void TellCorner(GameState king, GameState foe)
        {
            string hud = "Duel Pit  Round " + (Round + 1) + "/" + MaxRounds;
            if (king != null && king.Entity != null)
                hud += "  King:" + king.Entity.Name;
            if (king != null)
                king.Send(new Message(hud, Color.Yellow, Message.FirstRightCorner));
            if (foe != null)
                foe.Send(new Message(hud, Color.Yellow, Message.FirstRightCorner));
        }

        private static bool HasClaim(GameState client)
        {
            if (!QuestInventory.Player(client, false) || client.Variables == null || !SessionInstall.TryEnter(client.Entity.UID)) return false;
            try {
                lock (client.ItemSyncRoot) lock (client.Variables.SyncRoot) {
                    if (!QuestInventory.Player(client,false)) return false;
                    uint place = (uint)client[ClaimKey];
                    if (place != 0) return place <= 4;
                    if (!ResultsReady) return false;
                    using (var c = DataHolder.MySqlConnection) {
                        c.Open(); using (var t = c.BeginTransaction()) {
                            byte[] before, merged;
                            using (var q = QuestInventory.Command(c,t,"SELECT data FROM variablevault WHERE entityid=@uid FOR UPDATE","uid",(long)client.Entity.UID))
                                before = q.ExecuteScalar() as byte[];
                            if (before == null) throw new InvalidOperationException("Duel Pit variables missing.");
                            var saved = new VariableVault(); saved.Deserialize(before);
                            if ((uint)saved[ClaimKey] != 0) throw new InvalidOperationException("Duel Pit pending placement changed.");
                            string cycle;
                            using (var q = QuestInventory.Command(c,t,@"SELECT cycle,place FROM revora_duel_results
WHERE uid=@uid AND NOT imported ORDER BY cycle LIMIT 1 FOR UPDATE","uid",(long)client.Entity.UID))
                            using (var r = q.ExecuteReader()) {
                                if (!r.Read()) return false;
                                cycle=r.GetString(0);place=(uint)r.GetInt32(1);
                            }
                            if (!client.Variables.TryMerge(before,new[]{ClaimKey},new DynamicVariable[]{place},out merged))
                                throw new InvalidOperationException("Duel Pit variables changed.");
                            if (QuestInventory.Execute(c,t,"UPDATE variablevault SET data=@data WHERE entityid=@uid AND data=@before",
                                "data",merged,"uid",(long)client.Entity.UID,"before",before)!=1
                                || QuestInventory.Execute(c,t,"UPDATE revora_duel_results SET imported=true WHERE cycle=@cycle AND uid=@uid AND NOT imported",
                                "cycle",cycle,"uid",(long)client.Entity.UID)!=1)
                                throw new InvalidOperationException("Duel Pit prize import failed.");
                            if (!QuestInventory.Player(client,false)) throw new InvalidOperationException("Duel Pit owner changed.");
                            t.Commit();client.Variables.ReplaceContents(merged);return true;
                        }
                    }
                }
            }
            catch (Exception ex) { QuestInventory.Fail(client,"duel-import",ex);return false; }
            finally { SessionInstall.Exit(client.Entity.UID); }
        }

        private static string TryClaim(GameState client)
        {
            if (!QuestInventory.Player(client) || client.Variables == null || !SessionInstall.TryEnter(client.Entity.UID))
                return "Return when you are ready to claim your prize.";
            try
            {
                lock (client.ItemSyncRoot) lock (client.Variables.SyncRoot)
                {
                    if (!QuestInventory.Player(client) || !HasClaim(client))
                        return "You have no Duel Pit prize to claim.";
                    uint kind = (uint)client[ClaimKey];
            uint cp = Revora.Config.DuelPitFirstCp;
            ulong gold = Revora.Config.DuelPitFirstGold;
            uint rp = Revora.Config.DuelPitFirstRevoraPoints;
            string label = "first";
            if (kind == 2)
            {
                cp = Revora.Config.DuelPitSecondCp;
                gold = Revora.Config.DuelPitSecondGold;
                rp = Revora.Config.DuelPitSecondRevoraPoints;
                label = "second";
            }
            else if (kind == 3)
            {
                cp = Revora.Config.DuelPitThirdCp;
                gold = Revora.Config.DuelPitThirdGold;
                rp = Revora.Config.DuelPitThirdRevoraPoints;
                label = "third";
            }
            else if (kind == 4)
            {
                cp = Revora.Config.DuelPitFourthCp;
                gold = Revora.Config.DuelPitFourthGold;
                rp = Revora.Config.DuelPitFourthRevoraPoints;
                label = "fourth";
            }
                    long nextCp = (long)client.Entity.ConquerPoints + cp;
                    long nextRp = (long)client.Entity.RevoraPoint + rp;
                    decimal nextGold = (decimal)client.Entity.Money + gold;
                    if (nextCp > uint.MaxValue || nextRp > uint.MaxValue || nextGold > Entity.MaximumSilver)
                        return "Make room in your purse. Your prize will wait.";
                    if (client.Inventory.Count >= 40)
                        return "Leave one space for your soul. Your prize will wait.";
                    byte[] merged;
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
                            if (before == null) throw new InvalidOperationException("Duel Pit entitlement is not saved.");
                            var saved = new VariableVault(); saved.Deserialize(before);
                            if ((uint)saved[ClaimKey] != kind)
                                throw new InvalidOperationException("Duel Pit placement changed.");
                            if (!client.Variables.TryMerge(before, new[] { ClaimKey }, new DynamicVariable[] { (DynamicVariable)0ul }, out merged))
                                throw new InvalidOperationException("Duel Pit variables changed.");
                            using (var q = QuestInventory.Command(c, t,
                                "SELECT count(*) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0", "uid", (long)client.Entity.UID))
                                if ((long)q.ExecuteScalar() >= 40)
                                    return "Leave one space for your soul. Your prize will wait.";
                            long? oldCp, oldRp; PurseBalance.CopySaved(client, out oldCp, out oldRp);
                            if (QuestInventory.Execute(c, t, @"UPDATE entities SET conquerpoints=@cp,revorapoint=@rp,money=@gold
WHERE uid=@uid AND (CASE WHEN @cpnull THEN conquerpoints IS NULL ELSE conquerpoints=@oldcp END)
AND (CASE WHEN @rpnull THEN revorapoint IS NULL ELSE revorapoint=@oldrp END)",
                                "cp", nextCp, "rp", nextRp, "gold", nextGold, "uid", (long)client.Entity.UID,
                                "cpnull", !oldCp.HasValue, "oldcp", oldCp.GetValueOrDefault(),
                                "rpnull", !oldRp.HasValue, "oldrp", oldRp.GetValueOrDefault()) != 1)
                                throw new InvalidOperationException("Duel Pit purse changed.");
                            if (QuestInventory.Execute(c, t, "UPDATE variablevault SET data=@data WHERE entityid=@uid AND data=@before",
                                "data", merged, "uid", (long)client.Entity.UID, "before", before) != 1)
                                throw new InvalidOperationException("Duel Pit entitlement changed.");
                            QuestInventory.Insert(c, t, client.Entity.UID, soul);
                            if (!QuestInventory.Player(client)) throw new InvalidOperationException("Duel Pit owner changed.");
                            t.Commit();
                        }
                    }
                    client.Variables.ReplaceContents(merged);
                    client.Entity.ApplyCommittedConquerPoints((uint)nextCp);
                    client.Entity.ApplyCommittedRevoraPoints((uint)nextRp);
                    SilverBalance.ApplyCommitted(client, (ulong)nextGold);
                    QuestInventory.Install(client, new uint[0], new[] { soul });
                    return "Your " + label + "-place purse and bound P7 soul are yours.";
                }
            }
            catch (Exception ex)
            {
                QuestInventory.Fail(client, "duel-prize", ex);
                return "Your prize could not be collected. Reconnect and try again.";
            }
            finally { SessionInstall.Exit(client.Entity.UID); }
        }
    }
    internal static class DuelAdmissions
    {
        internal static bool HasPendingReturn(uint uid)
        {
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); using (var q = QuestInventory.Command(c, null,
                    "SELECT EXISTS(SELECT 1 FROM revora_duel_admissions WHERE uid=@uid AND NOT returned)", "uid", (long)uid))
                    return Convert.ToBoolean(q.ExecuteScalar());
            }
        }
        internal static void Initialize()
        {
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); QuestInventory.Execute(c, null, @"CREATE TABLE IF NOT EXISTS revora_duel_admissions(
cycle varchar(20) NOT NULL,uid bigint NOT NULL,arena integer NOT NULL,pkmode integer NOT NULL,
ended boolean NOT NULL DEFAULT false,returned boolean NOT NULL DEFAULT false,
PRIMARY KEY(cycle,uid));
CREATE INDEX IF NOT EXISTS revora_duel_return_owner ON revora_duel_admissions(uid) WHERE NOT returned;");
            }
        }
        internal static void RecoverInterrupted()
        {
            // A process restart forfeits unfinished matches. Committed rankings
            // retain their prizes. Never infer winners from saved map position.
            using (var c = DataHolder.MySqlConnection)
            { c.Open(); QuestInventory.Execute(c, null, "UPDATE revora_duel_admissions SET ended=true WHERE NOT ended"); }
        }
        internal static void Begin(string cycle, ushort arena, GameState[] players)
        {
            if (players.Length < 2 || players.Length > DuelPitSystem.Capacity
                || players.Any(p => !QuestInventory.Player(p))
                || players.Select(p => p.Entity.UID).Distinct().Count() != players.Length)
                throw new InvalidOperationException("Duel Pit admission roster is invalid.");
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); using (var t = c.BeginTransaction())
                {
                    QuestInventory.Execute(c, t, "SET LOCAL lock_timeout='1s'");
                    foreach (var p in players.OrderBy(p => p.Entity.UID))
                    {
                        using (var q = QuestInventory.Command(c, t, "SELECT count(*) FROM revora_duel_admissions WHERE uid=@uid AND cycle<>@cycle AND NOT returned",
                            "uid", (long)p.Entity.UID, "cycle", cycle))
                            if (Convert.ToInt32(q.ExecuteScalar()) != 0) throw new InvalidOperationException("Duel Pit previous return is pending.");
                        QuestInventory.Execute(c, t, @"INSERT INTO revora_duel_admissions(cycle,uid,arena,pkmode)
VALUES(@cycle,@uid,@arena,@mode) ON CONFLICT(cycle,uid) DO NOTHING",
                            "cycle", cycle, "uid", (long)p.Entity.UID, "arena", (int)arena, "mode", (int)p.Entity.PKMode);
                        using (var q = QuestInventory.Command(c, t, @"SELECT count(*) FROM revora_duel_admissions
WHERE cycle=@cycle AND uid=@uid AND arena=@arena AND pkmode=@mode AND NOT ended AND NOT returned",
                            "cycle", cycle, "uid", (long)p.Entity.UID, "arena", (int)arena, "mode", (int)p.Entity.PKMode))
                            if (Convert.ToInt32(q.ExecuteScalar()) != 1) throw new InvalidOperationException("Duel Pit admission changed.");
                    }
                    using (var q = QuestInventory.Command(c, t, "SELECT count(*) FROM revora_duel_admissions WHERE cycle=@cycle", "cycle", cycle))
                        if (Convert.ToInt32(q.ExecuteScalar()) != players.Length) throw new InvalidOperationException("Duel Pit roster changed.");
                    t.Commit();
                }
            }
        }
        internal static void End(string cycle)
        {
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); QuestInventory.Execute(c, null, "UPDATE revora_duel_admissions SET ended=true WHERE cycle=@cycle AND NOT ended", "cycle", cycle);
                using (var q = QuestInventory.Command(c, null, "SELECT count(*) FROM revora_duel_admissions WHERE cycle=@cycle AND NOT ended", "cycle", cycle))
                    if (Convert.ToInt32(q.ExecuteScalar()) != 0) throw new InvalidOperationException("Duel Pit end was not saved.");
            }
        }
        internal static void Forfeit(uint uid)
        {
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); QuestInventory.Execute(c, null, "UPDATE revora_duel_admissions SET ended=true WHERE uid=@uid AND NOT returned AND NOT ended", "uid", (long)uid);
                using (var q = QuestInventory.Command(c, null, "SELECT count(*) FROM revora_duel_admissions WHERE uid=@uid AND NOT returned AND NOT ended", "uid", (long)uid))
                    if (Convert.ToInt32(q.ExecuteScalar()) != 0) throw new InvalidOperationException("Duel Pit forfeit was not saved.");
            }
        }
        internal static bool Return(GameState p)
        {
            if (!QuestInventory.Player(p, false) || !Monitor.TryEnter(p.ItemSyncRoot)) return false;
            bool virtue = false, pk = false;
            try
            {
                virtue = Monitor.TryEnter(p.VirtueSyncRoot);
                if (!virtue) return false;
                pk = Monitor.TryEnter(p.PkSyncRoot);
                if (!pk) return false;
                int? mode = null;
                using (var c = DataHolder.MySqlConnection)
                {
                    c.Open(); using (var q = QuestInventory.Command(c, null, @"SELECT pkmode FROM revora_duel_admissions
WHERE uid=@uid AND ended AND NOT returned ORDER BY cycle DESC LIMIT 1", "uid", (long)p.Entity.UID))
                    { object value = q.ExecuteScalar(); if (value != null) mode = Convert.ToInt32(value); }
                }
                if (!mode.HasValue) return true;
                if (mode.Value < 0 || mode.Value > byte.MaxValue || !Enum.IsDefined(typeof(Enums.PKMode), (byte)mode.Value))
                    throw new InvalidOperationException("Duel Pit return mode is invalid.");
                if (!QuestInventory.Player(p, false)) return false;
                p.Entity.Teleport(1002, DuelPitSystem.TwinCityX, DuelPitSystem.TwinCityY);
                if (p.Entity.MapID != 1002 || p.Entity.X != DuelPitSystem.TwinCityX || p.Entity.Y != DuelPitSystem.TwinCityY)
                    throw new InvalidOperationException("Duel Pit return did not reach town.");
                p.Entity.PKMode = (Enums.PKMode)mode.Value;
                p.Send(new Data(true) { UID = p.Entity.UID, ID = Data.ChangePKMode, dwParam = (uint)p.Entity.PKMode });
                if (!EntityTable.SaveEntity(p)) throw new InvalidOperationException("Duel Pit returned character was not saved.");
                // Save destination first. A failed acknowledgement retries a safe
                // return on reconnect; it cannot consume a prize or strand a player.
                using (var c = DataHolder.MySqlConnection)
                {
                    c.Open();
                    QuestInventory.Execute(c, null, "UPDATE revora_duel_admissions SET returned=true WHERE uid=@uid AND ended AND NOT returned", "uid", (long)p.Entity.UID);
                    using (var q = QuestInventory.Command(c, null, "SELECT count(*) FROM revora_duel_admissions WHERE uid=@uid AND ended AND NOT returned", "uid", (long)p.Entity.UID))
                        if (Convert.ToInt32(q.ExecuteScalar()) != 0) throw new InvalidOperationException("Duel Pit return acknowledgement failed.");
                }
                return true;
            }
            catch (Exception ex) { QuestInventory.Fail(p, "duel-return", ex); return false; }
            finally
            {
                if (pk) Monitor.Exit(p.PkSyncRoot);
                if (virtue) Monitor.Exit(p.VirtueSyncRoot);
                Monitor.Exit(p.ItemSyncRoot);
            }
        }
    }

}
