using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Revora.Client;
using Revora.Database;
using Revora.Interfaces;
using Revora.Network.GamePackets;

namespace Revora.Game.Features
{
    public static class RelicRushSystem
    {
        internal static Func<DateTime> Clock = () => DateTime.Now;
        private static bool FinalMinuteAnnounced;
        internal static uint HoldPoints(DateTime now, DateTime until)
        { return now>=until?0u:now>=until.AddMinutes(-1)?2u:1u; }
        public const uint NpcId = 900000005;
        public const uint RelicNpcId = 900000006;
        public const string NpcName = "RelicRush";
        public const int Capacity = 24;
        public const int MinPlayers = 2;
        public const string ClaimKey = "relic-claim";
        public const ushort TwinCityMap = 1002;
        public const ushort TwinCityX = 408;
        public const ushort TwinCityY = 330;
        public const ushort ClaimX = 366;
        public const ushort ClaimY = 295;
        public const ushort Mesh = 50020;
        public const ushort RelicMesh = 30890;

        private static readonly int[] StartHours = new int[] { 1, 3, 5, 7, 9, 11, 13, 17, 19, 21, 23 };
        private static readonly TimeSpan RegisterWindow = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan FightLimit = TimeSpan.FromMinutes(8);
        private static readonly object Gate = new object();

        private static Map EventMap;
        private static DateTime CycleStart;
        private static DateTime FightUntil;
        private static bool Running;
        private static bool RegisterOpen;
        private static bool Invited;
        private static Time32 PeaceUntil;
        private static Time32 NextPulse;
        private static uint Holder;
        private static ushort RelicX;
        private static ushort RelicY;
        private static readonly HashSet<uint> Registered = new HashSet<uint>();
        private static readonly HashSet<uint> Alive = new HashSet<uint>();
        private static readonly Dictionary<uint, uint> Scores = new Dictionary<uint, uint>();
        private static List<KeyValuePair<uint, uint>> PendingRank;
        private static int Finishing;
        private static bool AbortRound;
        private static readonly HashSet<uint> PendingReturns = new HashSet<uint>();

        internal static void InitializePrizes()
        {
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open();
                QuestInventory.Execute(c, null, @"CREATE TABLE IF NOT EXISTS revora_relic_placements(
cycle text NOT NULL,place integer NOT NULL CHECK(place BETWEEN 1 AND 3),
uid bigint NOT NULL,score bigint NOT NULL CHECK(score>0),imported boolean NOT NULL DEFAULT false,
PRIMARY KEY(cycle,place),UNIQUE(cycle,uid));
CREATE INDEX IF NOT EXISTS revora_relic_pending ON revora_relic_placements(uid,cycle) WHERE NOT imported;");
            }
        }

        public static uint FirstCp { get { return Revora.Config.RelicRushFirstCp; } }
        public static ulong FirstGold { get { return Revora.Config.RelicRushFirstGold; } }
        public static uint FirstRevoraPoints { get { return Revora.Config.RelicRushFirstRevoraPoints; } }
        public static uint SecondCp { get { return Revora.Config.RelicRushSecondCp; } }
        public static ulong SecondGold { get { return Revora.Config.RelicRushSecondGold; } }
        public static uint SecondRevoraPoints { get { return Revora.Config.RelicRushSecondRevoraPoints; } }
        public static uint ThirdCp { get { return Revora.Config.RelicRushThirdCp; } }
        public static ulong ThirdGold { get { return Revora.Config.RelicRushThirdGold; } }
        public static uint ThirdRevoraPoints { get { return Revora.Config.RelicRushThirdRevoraPoints; } }

        public static void Create()
        {
            InitializePrizes();
            RelicAdmissions.Initialize();
            RelicAdmissions.RecoverInterrupted();
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
                Console.WriteLine("RelicRush arena map " + EventMap.ID + " (base " + EventMap.BaseID + ")");
            }
            else
                Console.WriteLine("RelicRush arena skipped: no source map");
            if (Kernel.Maps.ContainsKey(TwinCityMap) && Kernel.Maps[TwinCityMap].Npcs.ContainsKey(NpcId))
                Console.WriteLine(NpcName + " at Twin City " + ClaimX + "," + ClaimY);
            CycleStart = NextStart(Clock());
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

        public static bool SuppressCrime(Entity killer, Entity victim)
        {
            return InEvent(killer) && InEvent(victim);
        }

        public static void Tick()
        {
            ProcessReturns();
            if (AbortRound) { ResetCycle(true); return; }
            if (EventMap == null || CycleStart == default(DateTime)) return;
            DateTime now = Clock();
            if (!Running && !Invited && now >= CycleStart.Subtract(RegisterWindow) && now < CycleStart)
            {
                Invited = true;
                RegisterOpen = true;
                Kernel.SendWorldMessage(new Message("RelicRush registration is open for 5 minutes. Hold the relic to score. Talk to RelicRush in Twin City (" + ClaimX + ", " + ClaimY + "). Max 24.", Color.Yellow, Message.Center), Server.GamePool);
                InviteAll();
            }
            if (!Running && now >= CycleStart)
                StartFight();
            if (!Running) return;
            if (Time32.Now <= PeaceUntil) return;
            if (Time32.Now > NextPulse)
            {
                NextPulse = Time32.Now.AddSeconds(1);
                Pulse();
            }
            if (now >= FightUntil)
                FinishByScore();
        }

        public static bool HandleNpc(GameState client, NpcRequest request)
        {
            if (client == null || request == null || client.Entity == null)
                return false;
            if (client.ActiveNpc == RelicNpcId)
            {
                Npcs relic = new Npcs(client);
                relic.Text("Walk next to the relic to take it. Hold it to score; the final minute gives double points. Death drops it.");
                relic.Option("Alright.", 255);
                relic.Send();
                return true;
            }
            if (client.ActiveNpc != NpcId)
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
            DropRelic(client.Entity.X, client.Entity.Y, client.Entity.UID);
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
                PendingReturns.Add(client.Entity.UID);
                if (Holder == client.Entity.UID)
                {
                    Holder = 0;
                    RelicX = client.Entity.X;
                    RelicY = client.Entity.Y;
                }
            }
            PlaceRelic(RelicX, RelicY);
        }

        public static void OnLogin(GameState client)
        {
            if (!QuestInventory.Player(client, false)) return;
            lock (Gate)
            {
                Registered.Remove(client.Entity.UID);
                Alive.Remove(client.Entity.UID);
                {
                    if (RelicAdmissions.HasPendingReturn(client.Entity.UID)) PendingReturns.Add(client.Entity.UID);
                    else PendingReturns.Remove(client.Entity.UID);
                }
            }
            ProcessReturns();
            lock (Gate)
                if (PendingReturns.Contains(client.Entity.UID))
                    QuestInventory.Fail(client, "relic-login-return", new InvalidOperationException("Relic Rush return is pending; reconnect."));
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
                        RelicAdmissions.Forfeit(uid);
                        GameState p;
                        // Offline owners discover their durable return at login.
                        if (!Kernel.GamePool.TryGetValue(uid, out p) || p.Entity.PersistenceRequiresReload || RelicAdmissions.Return(p))
                            PendingReturns.Remove(uid);
                    }
                    catch (Exception ex) { Console.WriteLine("[relic-return] error=" + ex.GetType().Name); }
                }
            }
        }

        private static string StatusText()
        {
            if (Running)
                return "RelicRush is live. Hold the relic to score. Death drops it and sends you out. Top three claim CPs, gold, and Revora Points here.";
            if (RegisterOpen)
                return "Registration is open until " + CycleStart.ToString("HH:mm") + ". Seats " + Registered.Count + "/" + Capacity + ". Hold the relic to score. Top three win.";
            return "Next RelicRush " + CycleStart.ToString("HH:mm") + ". Registration opens 5 minutes before. Odd hours. Max 24. Walk onto the relic, keep it, stay alive.";
        }

        private static void InviteAll()
        {
            foreach (GameState client in Kernel.GamePool.Values)
            {
                if (!CanInvite(client)) continue;
                GameState captured = client;
                captured.MessageOK = p => { TryRegister(p); };
                captured.Send(new NpcReply(NpcReply.MessageBox, "RelicRush is open. Hold the relic to score. Register now? Max 24. Prizes at RelicRush (" + ClaimX + ", " + ClaimY + ")."));
                captured.Send(new Data(true) { UID = captured.Entity.UID, dwParam = 60, ID = Data.CountDown });
            }
        }

        private static bool CanInvite(GameState client)
        {
            if (client == null || client.Entity == null || client.Entity.Dead) return false;
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
                if (Registered.Contains(client.Entity.UID)) return "You are already registered.";
                if (Registered.Count >= Capacity) return "RelicRush is full (24).";
                Registered.Add(client.Entity.UID);
                return "You are registered. You enter at " + CycleStart.ToString("HH:mm") + ".";
            }
        }

        private static void StartFight()
        {
            var enter = new List<GameState>();
            lock (Gate)
            {
                RegisterOpen = false; Invited = false;
                if (Running || AbortRound) return;
                foreach (uint uid in Registered)
                {
                    GameState p;
                    if (Kernel.GamePool.TryGetValue(uid, out p) && CanInvite(p) && QuestInventory.Player(p)) enter.Add(p);
                    if (enter.Count >= Capacity) break;
                }
                if (enter.Count < MinPlayers)
                {
                    Kernel.SendWorldMessage(new Message("RelicRush did not start. Need at least two registered fighters.", Color.Orange, Message.Center), Server.GamePool);
                    ResetCycle(false); return;
                }
                var locked = new List<GameState>();
                try
                {
                    foreach (var p in enter.OrderBy(p => p.Entity.UID))
                    {
                        if (!System.Threading.Monitor.TryEnter(p.ItemSyncRoot)) return;
                        locked.Add(p);
                    }
                    if (EventMap == null) throw new InvalidOperationException("Relic arena unavailable.");
                    RelicAdmissions.Begin(CycleStart.ToString("yyyyMMddHHmm", System.Globalization.CultureInfo.InvariantCulture), EventMap.ID, enter.ToArray());
                    Running = true; Alive.Clear(); Scores.Clear(); Holder = 0; PendingRank = null;
                    FightUntil = Clock().Add(FightLimit); PeaceUntil = Time32.Now.AddSeconds(10); NextPulse = PeaceUntil;
                    foreach (var p in enter) { Alive.Add(p.Entity.UID); Scores[p.Entity.UID] = 0; }
                    var center = EventMap.RandomCoordinates(); PlaceRelic(center.Item1, center.Item2);
                    foreach (var p in enter)
                    {
                        SendIntoArena(p);
                        if (!InEvent(p.Entity)) throw new InvalidOperationException("Relic transfer failed.");
                    }
                }
                catch (Exception ex)
                {
                    AbortRound = true; Console.WriteLine("[relic-admission] error=" + ex.GetType().Name); return;
                }
                finally { foreach (var p in locked) System.Threading.Monitor.Exit(p.ItemSyncRoot); }
            }
            Kernel.SendWorldMessage(new Message("RelicRush has begun. " + enter.Count + " fighters. Hold the relic to score. Top three claim at RelicRush.", Color.Red, Message.Center), Server.GamePool);
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
            client.Send(new Message("The relic is on the field. Fight begins in 10 seconds. Hold it to score; the final minute gives double points. Death drops it and sends you out.", Color.Red, Message.Talk));
        }

        private static void PlaceRelic(ushort x, ushort y)
        {
            if (EventMap == null) return;
            RelicX = x;
            RelicY = y;
            INpc old;
            if (EventMap.Npcs.TryGetValue(RelicNpcId, out old) && old != null)
            {
                EventMap.RemoveNpc(old);
                if (Map.SecoundNpc.ContainsKey(RelicNpcId))
                    Map.SecoundNpc.Remove(RelicNpcId);
            }
            CityGuardSystem.SpawnTalker(RelicNpcId, EventMap.ID, x, y, RelicMesh, "Relic", (byte)Enums.ConquerAngle.South);
        }

        private static void HideRelic()
        {
            if (EventMap == null) return;
            INpc old;
            if (EventMap.Npcs.TryGetValue(RelicNpcId, out old) && old != null)
            {
                EventMap.RemoveNpc(old);
                if (Map.SecoundNpc.ContainsKey(RelicNpcId))
                    Map.SecoundNpc.Remove(RelicNpcId);
            }
        }

        private static void Pulse()
        {
            List<GameState> still = new List<GameState>();
            lock (Gate)
            {
                if (!Running || AbortRound) return;
                List<uint> drop = new List<uint>();
                foreach (uint uid in Alive)
                {
                    GameState client;
                    if (!Kernel.GamePool.TryGetValue(uid, out client) || client.Entity == null || client.Entity.Dead || client.Entity.MapID != EventMap.ID)
                    {
                        drop.Add(uid);
                        continue;
                    }
                    still.Add(client);
                }
                for (int i = 0; i < drop.Count; i++)
                {
                    if (Holder == drop[i])
                        DropRelic(RelicX,RelicY,Holder);
                    Alive.Remove(drop[i]);
                    PendingReturns.Add(drop[i]);
                }
                if (Holder != 0 && !Alive.Contains(Holder))
                    Holder = 0;
            }
            if (still.Count == 0)
            {
                FinishByScore();
                return;
            }
            if (Holder == 0)
                TryTakeRelic(still);
            else
                ScoreHolder();
            string holderName = HolderName();
            uint lead = LeadScore();
            if(!FinalMinuteAnnounced && HoldPoints(Clock(),FightUntil)==2) {
                FinalMinuteAnnounced=true;
                foreach(var c in still)c.Send(new Message("The relic surges! Holding it earns double points for the final minute.",Color.Gold,Message.Center));
            }
            for (int i = 0; i < still.Count; i++)
            {
                uint mine = 0;
                lock (Gate)
                    Scores.TryGetValue(still[i].Entity.UID, out mine);
                still[i].Send(new Message("RelicRush  "+Math.Max(0,(int)(FightUntil-Clock()).TotalSeconds)+"s  You:" + mine + "  Lead:" + lead, Color.Yellow, Message.FirstRightCorner));
                still[i].Send(new Message("Relic: "+holderName+" ("+RelicX+","+RelicY+")"+(HoldPoints(Clock(),FightUntil)==2?"  DOUBLE POINTS":""),Color.Gold,Message.ContinueRightCorner));
            }
        }

        private static void TryTakeRelic(List<GameState> still)
        {
            GameState taker = null;
            int best = 3;
            for (int i = 0; i < still.Count; i++)
            {
                int d = Kernel.GetDistance(still[i].Entity.X, still[i].Entity.Y, RelicX, RelicY);
                if (d <= 2 && d < best)
                {
                    best = d;
                    taker = still[i];
                }
            }
            if (taker == null) return;
            lock (Gate)
                Holder = taker.Entity.UID;
            HideRelic();
            taker.Send(new Message("You hold the relic! Stay alive to score.", Color.Lime, Message.TopLeft));
            Kernel.SendWorldMessage(new Message(taker.Entity.Name + " seized the relic!", Color.Orange, Message.Center), Server.GamePool);
        }

        private static void ScoreHolder()
        {
            GameState holder;
            if (!Kernel.GamePool.TryGetValue(Holder, out holder) || holder.Entity == null || holder.Entity.Dead)
            {
                DropRelic(RelicX, RelicY, Holder);
                return;
            }
            lock (Gate)
            {
                if(!Running || PendingRank!=null || Clock()>=FightUntil || !Alive.Contains(Holder) || holder.Entity.MapID!=EventMap.ID)return;
                uint score;
                Scores.TryGetValue(Holder, out score);
                Scores[Holder] = score + HoldPoints(Clock(),FightUntil);
                RelicX=holder.Entity.X;RelicY=holder.Entity.Y;
            }
        }

        private static void DropRelic(ushort x, ushort y, uint fromUid)
        {
            bool ours = false;
            lock (Gate)
            {
                if (Holder == fromUid || Holder == 0)
                {
                    Holder = 0;
                    RelicX = x;
                    RelicY = y;
                    ours = true;
                }
            }
            if (ours)
                PlaceRelic(x, y);
        }

        private static void Eliminate(GameState client, bool teleport)
        {
            lock (Gate)
            {
                if (!Running) return;
                Alive.Remove(client.Entity.UID);
                PendingReturns.Add(client.Entity.UID);
            }
            if (teleport)
                ProcessReturns();
            client.Send(new Message("You are out of RelicRush. Your score still counts.", Color.Red, Message.TopLeft));
        }

        private static string HolderName()
        {
            if (Holder == 0) return "none";
            GameState client;
            if (!Kernel.GamePool.TryGetValue(Holder, out client) || client.Entity == null)
                return "none";
            return client.Entity.Name;
        }

        private static uint LeadScore()
        {
            uint lead = 0;
            lock (Gate)
            {
                foreach (KeyValuePair<uint, uint> pair in Scores)
                {
                    if (pair.Value > lead)
                        lead = pair.Value;
                }
            }
            return lead;
        }

        private static void FinishByScore()
        {
            if (System.Threading.Interlocked.CompareExchange(ref Finishing, 1, 0) != 0) return;
            try
            {
            List<KeyValuePair<uint, uint>> rank;
            string cycle;
            lock (Gate)
            {
                if (!Running || AbortRound) return;
                if (PendingRank == null)
                    PendingRank = Scores.Where(p => p.Value > 0).OrderByDescending(p => p.Value).ThenBy(p => p.Key).Take(3).ToList();
                rank = PendingRank;
                cycle = CycleStart.ToString("yyyyMMddHHmm", System.Globalization.CultureInfo.InvariantCulture);
            }
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); using (var t = c.BeginTransaction())
                {
                    for (int i = 0; i < rank.Count; i++)
                    {
                        QuestInventory.Execute(c, t, @"INSERT INTO revora_relic_placements(cycle,place,uid,score)
VALUES(@cycle,@place,@uid,@score) ON CONFLICT(cycle,place) DO NOTHING",
                            "cycle", cycle, "place", i+1, "uid", (long)rank[i].Key, "score", (long)rank[i].Value);
                        using (var q = QuestInventory.Command(c, t, @"SELECT EXISTS(SELECT 1 FROM revora_relic_placements
WHERE cycle=@cycle AND place=@place AND uid=@uid AND score=@score)",
                            "cycle", cycle, "place", i+1, "uid", (long)rank[i].Key, "score", (long)rank[i].Value))
                            if (!Equals(q.ExecuteScalar(), true)) throw new InvalidOperationException("Relic result was not saved.");
                    }
                    using (var q = QuestInventory.Command(c,t,"SELECT count(*) FROM revora_relic_placements WHERE cycle=@cycle","cycle",cycle))
                        if ((long)q.ExecuteScalar() != rank.Count) throw new InvalidOperationException("Relic result differs from its saved roster.");
                    t.Commit();
                }
            }
            string[] place = new string[] { "first", "second", "third" };
            for (int i = 0; i < rank.Count && i < 3; i++)
            {
                if (rank[i].Value == 0) continue;
                GameState client;
                if (!Kernel.GamePool.TryGetValue(rank[i].Key, out client) || client.Entity == null)
                    continue;
                client.Send(new Message("You placed " + place[i] + " in RelicRush with " + rank[i].Value + " points. Claim at RelicRush.", Color.Yellow, Message.TopLeft));
                Kernel.SendWorldMessage(new Message(client.Entity.Name + " placed " + place[i] + " in RelicRush.", Color.Red, Message.Center), Server.GamePool);
            }
            if (rank.Count == 0 || rank[0].Value == 0)
                Kernel.SendWorldMessage(new Message("RelicRush ended with no scores.", Color.Orange, Message.Center), Server.GamePool);
            ResetCycle(true);
            }
            catch (Exception ex) { Console.WriteLine("[relic-result] error=" + ex.GetType().Name); }
            finally { System.Threading.Interlocked.Exchange(ref Finishing, 0); }
        }

        private static void ResetCycle(bool kickMap)
        {
            lock (Gate)
            {
                try { RelicAdmissions.End(CycleStart.ToString("yyyyMMddHHmm", System.Globalization.CultureInfo.InvariantCulture)); }
                catch (Exception ex) { AbortRound = true; Console.WriteLine("[relic-end] error=" + ex.GetType().Name); return; }
                foreach (uint uid in Registered) PendingReturns.Add(uid);
                foreach (uint uid in Alive) PendingReturns.Add(uid);
                foreach (uint uid in Scores.Keys) PendingReturns.Add(uid);
                AbortRound = false;
                Running = false;
                FinalMinuteAnnounced=false;
                RegisterOpen = false;
                Invited = false;
                Holder = 0;
                Registered.Clear();
                Alive.Clear();
                Scores.Clear();
                PendingRank = null;
                CycleStart = NextStart(Clock().AddSeconds(2));
            }
            HideRelic();
            ProcessReturns();
        }

        private static bool HasClaim(GameState client)
        {
            if (!QuestInventory.Player(client, false) || client.Variables == null) return false;
            lock (client.ItemSyncRoot) lock (client.Variables.SyncRoot)
            try
            {
                uint place = (uint)client[ClaimKey];
                if (place != 0) return place <= 3;
                using (var c = DataHolder.MySqlConnection)
                {
                    c.Open(); using (var t = c.BeginTransaction())
                    {
                        byte[] before;
                        using (var q = QuestInventory.Command(c,t,"SELECT data FROM variablevault WHERE entityid=@uid FOR UPDATE","uid",(long)client.Entity.UID))
                            before = q.ExecuteScalar() as byte[];
                        if (before == null)
                            throw new InvalidOperationException("Relic prize variables changed.");
                        var saved = new VariableVault(); saved.Deserialize(before);
                        if ((uint)saved[ClaimKey] != 0)
                            throw new InvalidOperationException("Relic prize placement changed.");
                        string cycle;
                        using (var q = QuestInventory.Command(c,t,@"SELECT cycle,place FROM revora_relic_placements
WHERE uid=@uid AND NOT imported ORDER BY cycle LIMIT 1 FOR UPDATE","uid",(long)client.Entity.UID))
                        using (var r = q.ExecuteReader())
                        {
                            if (!r.Read()) return false;
                            cycle = r.GetString(0); place = (uint)r.GetInt32(1);
                        }
                        var next = new VariableVault(); next.Deserialize(client.Variables.Serialize()); next[ClaimKey] = place;
                        if (QuestInventory.Execute(c,t,"UPDATE variablevault SET data=@data WHERE entityid=@uid AND data=@before",
                            "data",next.Serialize(),"uid",(long)client.Entity.UID,"before",before) != 1
                            || QuestInventory.Execute(c,t,"UPDATE revora_relic_placements SET imported=true WHERE cycle=@cycle AND uid=@uid AND NOT imported",
                            "cycle",cycle,"uid",(long)client.Entity.UID) != 1)
                            throw new InvalidOperationException("Relic prize import was not saved.");
                        t.Commit(); next.Changed = false; client.Variables = next;
                        return true;
                    }
                }
            }
            catch (Exception ex) { QuestInventory.Fail(client,"relic-import",ex); return false; }
        }

        private static string TryClaim(GameState client)
        {
            if (!QuestInventory.Player(client) || client.Variables == null)
                return "Return when you are ready to claim your prize.";
            lock (client.ItemSyncRoot) lock (client.Variables.SyncRoot)
            try
            {
                if (!QuestInventory.Player(client) || !HasClaim(client))
                    return "You have no RelicRush prize to claim.";
                uint place = (uint)client[ClaimKey];
                if (client.Inventory.Count >= 40)
                    return "Leave one space for your soul. Your prize will wait.";
                uint cp = ThirdCp;
                ulong gold = ThirdGold;
                uint rp = ThirdRevoraPoints;
                string label = "third";
                if (place == 1)
                {
                    cp = FirstCp;
                    gold = FirstGold;
                    rp = FirstRevoraPoints;
                    label = "first";
                }
                else if (place == 2)
                {
                    cp = SecondCp;
                    gold = SecondGold;
                    rp = SecondRevoraPoints;
                    label = "second";
                }
                long nextCp = (long)client.Entity.ConquerPoints + cp;
                long nextRp = (long)client.Entity.RevoraPoint + rp;
                decimal nextGold = (decimal)client.Entity.Money + gold;
                if (nextCp > uint.MaxValue || nextRp > uint.MaxValue || nextGold > Entity.MaximumSilver)
                    return "Make room in your purse. Your prize will wait.";
                var next = new VariableVault(); next.Deserialize(client.Variables.Serialize());
                next[ClaimKey] = 0u;
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
                        if (before == null) throw new InvalidOperationException("Relic Rush entitlement is not saved.");
                        var saved = new VariableVault(); saved.Deserialize(before);
                        if ((uint)saved[ClaimKey] != place)
                            throw new InvalidOperationException("Relic Rush placement changed.");
                        if (QuestInventory.Execute(c, t, @"UPDATE entities SET conquerpoints=@cp,revorapoint=@rp,money=@gold
    WHERE uid=@uid AND conquerpoints=@oldcp AND revorapoint=@oldrp",
                            "cp", nextCp, "rp", nextRp, "gold", nextGold, "uid", (long)client.Entity.UID,
                            "oldcp", (long)client.Entity.ConquerPoints, "oldrp", (long)client.Entity.RevoraPoint) != 1)
                            throw new InvalidOperationException("Relic Rush purse changed.");
                        if (QuestInventory.Execute(c, t, @"UPDATE variablevault SET data=@data
    WHERE entityid=@uid AND data=@before", "data", next.Serialize(), "uid", (long)client.Entity.UID, "before", before) != 1)
                            throw new InvalidOperationException("Relic Rush entitlement changed.");
                        QuestInventory.Insert(c, t, client.Entity.UID, soul);
                        t.Commit();
                    }
                }
                next.Changed = false; client.Variables = next;
                client.Entity.ApplyCommittedConquerPoints((uint)nextCp);
                client.Entity.ApplyCommittedRevoraPoints((uint)nextRp);
                SilverBalance.ApplyCommitted(client, (ulong)nextGold);
                QuestInventory.Install(client, new uint[0], new[] { soul });
                return "Your " + label + "-place purse and bound P7 soul are yours.";
                }
            catch (Exception ex)
            {
                QuestInventory.Fail(client, "relic-prize", ex);
                return "Your prize could not be collected. Reconnect and try again.";
            }
        }
    }
}
