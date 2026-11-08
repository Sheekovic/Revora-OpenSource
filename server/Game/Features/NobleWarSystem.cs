using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using Revora.Client;
using Revora.Database;
using Revora.Game.ConquerStructures;
using Revora.Interfaces;
using Revora.Network.GamePackets;

namespace Revora.Game.Features
{
    public static class NobleWarSystem
    {
        public const uint NpcId = 900000004;
        public const string NpcName = "NobleWar";
        // Twin City Captain Li look. Distinct from LastManStanding and ExpeditionClerk.
        public const ushort Mesh = 50040;
        public const uint ParkedNpcId = 10620;
        public const ushort TwinCityMap = 1002;
        public const ushort TwinCityX = 408;
        public const ushort TwinCityY = 330;
        public const ushort ParkMap = 1000;
        public const ushort ParkX = 50;
        public const ushort ParkY = 50;
        public const int StartHour = 15;
        public const int StartMinute = 0;
        public const int MinPlayers = 2;
        private const string CoordFile = "database\\NobleWar.txt";
        private static readonly TimeSpan RegisterWindow = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan FightLimit = TimeSpan.FromMinutes(15);
        private static readonly object Gate = new object();

        private static bool RewardsReady;
        internal static void InitializeRewards()
        {
            using (var c=DataHolder.MySqlConnection) {
                c.Open(); QuestInventory.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_noble_prizes(
cycle text NOT NULL,rank integer NOT NULL,uid bigint NOT NULL,cp bigint NOT NULL,gold numeric(20,0) NOT NULL,
rvp bigint NOT NULL,soul_type bigint NOT NULL,paid boolean NOT NULL DEFAULT false,soul_uid bigint NOT NULL DEFAULT 0,
PRIMARY KEY(cycle,rank));");
            }
            NobleAdmissions.Initialize();
            RewardsReady=true;
        }
        private sealed class Prize
        {
            public string Cycle;
            public uint Uid, Cp, Rvp, Soul;
            public ulong Gold;
        }

        private static readonly HashSet<uint> PendingReturns = new HashSet<uint>();
        public static void OnLogin(GameState client)
        {
            if (!RewardsReady || !QuestInventory.Player(client,false)) return;
            lock (Gate) {
                    if (NobleAdmissions.HasPendingReturn(client.Entity.UID)) PendingReturns.Add(client.Entity.UID);
                    else PendingReturns.Remove(client.Entity.UID);
                }
            ProcessReturns();
            lock (Gate) if (PendingReturns.Contains(client.Entity.UID))
                QuestInventory.Fail(client,"noble-login-return",new InvalidOperationException("NobleWar return pending."));
        }
        private static void ProcessReturns()
        {
            uint[] pending; lock (Gate) pending=PendingReturns.Take(64).ToArray();
            foreach(uint uid in pending) {
                if(!SessionInstall.TryEnter(uid))continue;
                try {
                    NobleAdmissions.Forfeit(uid);
                    GameState p;
                    if(!Kernel.GamePool.TryGetValue(uid,out p) || p.Entity.PersistenceRequiresReload || NobleAdmissions.Return(p))
                        lock(Gate)PendingReturns.Remove(uid);
                } catch(Exception ex){Console.WriteLine("[noble-return] error="+ex.GetType().Name);}
                finally{SessionInstall.Exit(uid);}
            }
        }

        private static ushort ClaimX = 309;
        private static ushort ClaimY = 248;
        private static DateTime CycleStart;
        private static bool Invited;
        private static bool RegisterOpen;
        private static readonly Bracket[] Brackets = new Bracket[]
        {
            new Bracket(NobilityRank.King, "King"),
            new Bracket(NobilityRank.Prince, "Prince"),
            new Bracket(NobilityRank.Duke, "Duke"),
            new Bracket(NobilityRank.Earl, "Earl")
        };

        private sealed class Bracket
        {
            public readonly NobilityRank Rank;
            public readonly string Title;
            public uint CpPrize
            {
                get
                {
                    if (Rank == NobilityRank.King) return Revora.Config.NobleWarKingCp;
                    if (Rank == NobilityRank.Prince) return Revora.Config.NobleWarPrinceCp;
                    if (Rank == NobilityRank.Duke) return Revora.Config.NobleWarDukeCp;
                    return Revora.Config.NobleWarEarlCp;
                }
            }
            public ulong GoldPrize
            {
                get
                {
                    if (Rank == NobilityRank.King) return Revora.Config.NobleWarKingGold;
                    if (Rank == NobilityRank.Prince) return Revora.Config.NobleWarPrinceGold;
                    if (Rank == NobilityRank.Duke) return Revora.Config.NobleWarDukeGold;
                    return Revora.Config.NobleWarEarlGold;
                }
            }
            public uint RevoraPointPrize
            {
                get
                {
                    if (Rank == NobilityRank.King) return Revora.Config.NobleWarKingRevoraPoints;
                    if (Rank == NobilityRank.Prince) return Revora.Config.NobleWarPrinceRevoraPoints;
                    if (Rank == NobilityRank.Duke) return Revora.Config.NobleWarDukeRevoraPoints;
                    return Revora.Config.NobleWarEarlRevoraPoints;
                }
            }
            public string AdmissionCycle;
            public Prize Pending;
            public int Finishing;
            public Map Arena;
            public bool Running;
            public DateTime FightUntil;
            public Time32 PeaceUntil;
            public readonly HashSet<uint> Registered = new HashSet<uint>();
            public readonly HashSet<uint> Alive = new HashSet<uint>();

            public Bracket(NobilityRank rank, string title)
            {
                Rank = rank;
                Title = title;
            }
        }

        public static void Create()
        {
            InitializeRewards();
            NobleAdmissions.RecoverInterrupted();
            RelocateLegacyClerk();
            Map source = null;
            if (Kernel.Maps.ContainsKey(700))
                source = Kernel.Maps[700];
            else if (Kernel.Maps.ContainsKey(1005))
                source = Kernel.Maps[1005];
            else if (Kernel.Maps.ContainsKey(1002))
                source = Kernel.Maps[1002];
            if (source == null)
            {
                Console.WriteLine("NobleWar skipped: no source map");
                return;
            }
            for (int i = 0; i < Brackets.Length; i++)
            {
                Brackets[i].Arena = source.MakeDynamicMap();
                if (!Constants.PKFreeMaps.Contains(Brackets[i].Arena.ID))
                    Constants.PKFreeMaps.Add(Brackets[i].Arena.ID);
                Console.WriteLine("NobleWar " + Brackets[i].Title + " map " + Brackets[i].Arena.ID + " (base " + Brackets[i].Arena.BaseID + ")");
            }
            CityGuardSystem.SpawnTalker(NpcId, TwinCityMap, ClaimX, ClaimY, Mesh, NpcName, (byte)Enums.ConquerAngle.South);
            if (Kernel.Maps.ContainsKey(TwinCityMap) && Kernel.Maps[TwinCityMap].Npcs.ContainsKey(NpcId))
                Console.WriteLine(NpcName + " at Twin City " + ClaimX + "," + ClaimY);
            CycleStart = NextStart(DateTime.Now);
        }

        public static DateTime NextStart(DateTime now)
        {
            DateTime slot = now.Date.AddHours(StartHour).AddMinutes(StartMinute);
            if (now < slot)
                return slot;
            return slot.AddDays(1);
        }

        public static bool InEvent(Entity entity)
        {
            if (entity == null) return false;
            for (int i = 0; i < Brackets.Length; i++)
            {
                if (Brackets[i].Arena != null && entity.MapID == Brackets[i].Arena.ID)
                    return true;
            }
            return false;
        }

        public static void Tick()
        {
            if(RewardsReady)ProcessReturns();
            if (CycleStart == default(DateTime)) return;
            DateTime now = DateTime.Now;
            if (!AnyRunning() && !Invited && now >= CycleStart.Subtract(RegisterWindow) && now < CycleStart)
            {
                Invited = true;
                RegisterOpen = true;
                Kernel.SendWorldMessage(new Message("NobleWar registration is open for 5 minutes. King, Prince, Duke, and Earl. Accept the popup or talk to NobleWar in Twin City (" + ClaimX + ", " + ClaimY + ").", Color.Yellow, Message.Center), Server.GamePool);
                InviteAll();
            }
            if (!AnyRunning() && now >= CycleStart)
                StartFight();
            if (!AnyRunning()) return;
            for (int i = 0; i < Brackets.Length; i++)
            {
                Bracket b = Brackets[i];
                if (!b.Running) continue;
                if (Time32.Now > b.PeaceUntil)
                    SweepAndCheck(b, now >= b.FightUntil);
            }
        }

        public static bool HandleNpc(GameState client, NpcRequest request)
        {
            if (client == null || request == null || client.Entity == null || client.ActiveNpc != NpcId)
                return false;
            Npcs dialog = new Npcs(client);
            switch (request.OptionID)
            {
                case 0:
                    dialog.Text(StatusText(client));
                    if (RegisterOpen && !AnyRunning() && BracketFor(client) != null)
                        dialog.Option("Register", 1);
                    if (HasPrize(client)) dialog.Option("Claim my prize", 2);
                    dialog.Option("Goodbye", 255);
                    dialog.Send();
                    return true;
                case 2:
                    dialog.Text(TryClaim(client));
                    dialog.Option("Farewell.",255);
                    dialog.Send();
                    return true;
                case 1:
                    dialog.Text(TryRegister(client));
                    dialog.Option("Alright.", 255);
                    dialog.Send();
                    return true;
            }
            return true;
        }

        public static void OnDeath(GameState client)
        {
            if (client == null || client.Entity == null) return;
            Bracket b = BracketByMap(client.Entity.MapID);
            if (b == null)
            {
                lock (Gate)
                {
                    for (int i = 0; i < Brackets.Length; i++)
                    {
                        if (Brackets[i].Alive.Contains(client.Entity.UID))
                        {
                            b = Brackets[i];
                            break;
                        }
                    }
                }
            }
            if (b == null || !b.Running) return;
            Eliminate(b, client, true);
        }

        public static void OnDisconnect(GameState client)
        {
            if (client == null || client.Entity == null) return;
            Bracket hit = null;
            lock (Gate)
            {
                for (int i = 0; i < Brackets.Length; i++)
                {
                    Bracket b = Brackets[i];
                    if (!b.Running || !b.Alive.Contains(client.Entity.UID)) continue;
                    b.Alive.Remove(client.Entity.UID);
                    PendingReturns.Add(client.Entity.UID);
                    hit = b;
                    break;
                }
            }
            if (hit != null)
                SweepAndCheck(hit, false);
        }

        private static void RelocateLegacyClerk()
        {
            if (Kernel.Maps.ContainsKey(TwinCityMap) && Kernel.Maps[TwinCityMap].Npcs.ContainsKey(ParkedNpcId))
            {
                INpc old = Kernel.Maps[TwinCityMap].Npcs[ParkedNpcId];
                ClaimX = old.X;
                ClaimY = old.Y;
                try
                {
                    File.WriteAllText(CoordFile, ClaimX + "," + ClaimY);
                }
                catch
                {
                }
                Kernel.Maps[TwinCityMap].RemoveNpc(old);
                if (Map.SecoundNpc.ContainsKey(ParkedNpcId))
                    Map.SecoundNpc.Remove(ParkedNpcId);
                if (Kernel.Maps.ContainsKey(ParkMap))
                {
                    old.MapID = ParkMap;
                    old.X = ParkX;
                    old.Y = ParkY;
                    Kernel.Maps[ParkMap].AddNpc(old);
                    Console.WriteLine("NPC " + ParkedNpcId + " parked on map " + ParkMap + " " + ParkX + "," + ParkY);
                }
            }
            else if (File.Exists(CoordFile))
            {
                try
                {
                    string[] parts = File.ReadAllText(CoordFile).Split(',');
                    if (parts.Length >= 2)
                    {
                        ClaimX = ushort.Parse(parts[0].Trim());
                        ClaimY = ushort.Parse(parts[1].Trim());
                    }
                }
                catch
                {
                }
            }
            try
            {
                using (var cmd = new MySqlCommand(MySqlCommandType.UPDATE))
                    cmd.Update("npcs").Set("mapid", ParkMap).Set("cellx", ParkX).Set("celly", ParkY).Where("id", ParkedNpcId).Execute();
            }
            catch
            {
            }
        }

        private static string StatusText(GameState client)
        {
            Bracket mine = BracketFor(client);
            if (AnyRunning())
            {
                string line = "NobleWar is live.";
                for (int i = 0; i < Brackets.Length; i++)
                {
                    if (Brackets[i].Running)
                        line += " " + Brackets[i].Title + ": " + AliveCount(Brackets[i]) + " remain.";
                }
                return line;
            }
            if (RegisterOpen)
            {
                if (mine == null)
                    return "Registration is open until " + CycleStart.ToString("HH:mm") + ". NobleWar is for King, Prince, Duke, and Earl.";
                return "Registration is open until " + CycleStart.ToString("HH:mm") + ". Your bracket: " + mine.Title + ". Seats " + mine.Registered.Count + ".";
            }
            return "Next NobleWar " + CycleStart.ToString("HH:mm") + ". King, Prince, Duke, and Earl. Registration opens 5 minutes before. Each rank fights on its own map.";
        }

        private static void InviteAll()
        {
            foreach (GameState client in Kernel.GamePool.Values)
            {
                if (!CanInvite(client)) continue;
                if (BracketFor(client) == null) continue;
                GameState captured = client;
                captured.MessageOK = p => { TryRegister(p); };
                captured.Send(new NpcReply(NpcReply.MessageBox, "NobleWar registration is open. Your rank fights its own bracket. Register now? Talk to NobleWar in Twin City (" + ClaimX + ", " + ClaimY + ")."));
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

        private static Bracket BracketFor(GameState client)
        {
            if (client == null || client.Entity == null) return null;
            NobilityRank rank = client.Entity.NobilityRank;
            for (int i = 0; i < Brackets.Length; i++)
            {
                if (Brackets[i].Rank == rank)
                    return Brackets[i];
            }
            return null;
        }

        private static Bracket BracketByMap(ushort mapId)
        {
            for (int i = 0; i < Brackets.Length; i++)
            {
                if (Brackets[i].Arena != null && Brackets[i].Arena.ID == mapId)
                    return Brackets[i];
            }
            return null;
        }

        private static string TryRegister(GameState client)
        {
            if (!CanInvite(client)) return "Unable to register.";
            if (client.Entity.Dead) return "You cannot register while dead.";
            Bracket b = BracketFor(client);
            if (b == null) return "NobleWar is for King, Prince, Duke, and Earl.";
            lock (Gate)
            {
                if (!RegisterOpen || AnyRunning()) return "Registration is closed.";
                if (b.Registered.Contains(client.Entity.UID)) return "You are already registered for the " + b.Title + " bracket.";
                b.Registered.Add(client.Entity.UID);
                return "You are registered for the " + b.Title + " bracket. You will be sent at " + CycleStart.ToString("HH:mm") + ".";
            }
        }

        private static void StartFight()
        {
            RegisterOpen = false;
            Invited = false;
            int started = 0;
            for (int i = 0; i < Brackets.Length; i++)
                if (StartBracket(Brackets[i]))
                    started++;
            if (started == 0)
            {
                Kernel.SendWorldMessage(new Message("NobleWar did not start. Each bracket needs at least two registered fighters.", Color.Orange, Message.Center), Server.GamePool);
                ResetCycle(false);
            }
        }

        private static bool StartBracket(Bracket b)
        {
            List<GameState> enter = new List<GameState>();
            lock (Gate)
            {
                if (b.Running || b.Arena == null) return false;
                foreach (uint uid in b.Registered)
                {
                    GameState client;
                    if (!Kernel.GamePool.TryGetValue(uid, out client)) continue;
                    if (!CanInvite(client)) continue;
                    if (BracketFor(client) != b) continue;
                    enter.Add(client);
                }
                if (enter.Count < MinPlayers)
                {
                    Kernel.SendWorldMessage(new Message("NobleWar " + b.Title + " did not start. Need at least two registered fighters.", Color.Orange, Message.Center), Server.GamePool);
                    b.Registered.Clear();
                    return false;
                }
                var leased=new List<uint>();var locked=new List<GameState>();
                try {
                    foreach(var p in enter.OrderBy(p=>p.Entity.UID)) {
                        if(!SessionInstall.TryEnter(p.Entity.UID))return false;
                        leased.Add(p.Entity.UID);
                        if(!Monitor.TryEnter(p.ItemSyncRoot))return false;
                        locked.Add(p);
                    }
                    b.AdmissionCycle=CycleStart.ToString("yyyyMMddHHmm",System.Globalization.CultureInfo.InvariantCulture)+"-"+(int)b.Rank;
                    NobleAdmissions.Begin(b.AdmissionCycle,b.Arena.ID,enter.ToArray());
                }catch(Exception ex){Console.WriteLine("[noble-admission] error="+ex.GetType().Name);return false;}
                finally{foreach(var p in locked)Monitor.Exit(p.ItemSyncRoot);foreach(uint uid in leased)SessionInstall.Exit(uid);}
                b.Running = true;
                b.Alive.Clear();
                b.FightUntil = DateTime.Now.Add(FightLimit);
                b.PeaceUntil = Time32.Now.AddSeconds(10);
                for (int i = 0; i < enter.Count; i++)
                    b.Alive.Add(enter[i].Entity.UID);
            }
            for (int i = 0; i < enter.Count; i++)
                SendIntoArena(b, enter[i]);
            Kernel.SendWorldMessage(new Message("NobleWar " + b.Title + " has begun. " + enter.Count + " fighters.", Color.Red, Message.Center), Server.GamePool);
            return true;
        }

        private static void SendIntoArena(Bracket b, GameState client)
        {
            if (client == null || client.Entity == null || b.Arena == null) return;
            Tuple<ushort, ushort> xy = b.Arena.RandomCoordinates();
            client.Entity.Teleport(b.Arena.BaseID, b.Arena.ID, xy.Item1, xy.Item2);
            client.Entity.PKMode = Enums.PKMode.PK;
            client.Send(new Data(true)
            {
                UID = client.Entity.UID,
                ID = Data.ChangePKMode,
                dwParam = (uint)client.Entity.PKMode
            });
            client.CantAttack = b.PeaceUntil;
            client.Send(new Message("NobleWar " + b.Title + ". Fight begins in 10 seconds. Death sends you to Twin City. No return.", Color.Red, Message.Talk));
        }

        private static void Eliminate(Bracket b, GameState client, bool teleport)
        {
            lock (Gate)
            {
                if (!b.Running) return;
                if (!b.Alive.Remove(client.Entity.UID) && (b.Arena == null || client.Entity.MapID != b.Arena.ID))
                    return;
            }
            if (teleport) {
                lock(Gate)PendingReturns.Add(client.Entity.UID);
                ProcessReturns();
            }
            client.Send(new Message("You are out of NobleWar. No return.", Color.Red, Message.TopLeft));
            SweepAndCheck(b, false);
        }

        private static int AliveCount(Bracket b)
        {
            lock (Gate)
                return b.Alive.Count;
        }

        private static bool AnyRunning()
        {
            for (int i = 0; i < Brackets.Length; i++)
                if (Brackets[i].Running) return true;
            return false;
        }

        private static void SweepAndCheck(Bracket b, bool timeout)
        {
            if (b.Pending != null) { Finish(b,null); return; }
            List<GameState> still = new List<GameState>();
            lock (Gate)
            {
                if (!b.Running || b.Arena == null) return;
                List<uint> drop = new List<uint>();
                foreach (uint uid in b.Alive)
                {
                    GameState client;
                    if (!Kernel.GamePool.TryGetValue(uid, out client) || client.Entity == null)
                    {
                        drop.Add(uid);
                        continue;
                    }
                    if (client.Entity.Dead || client.Entity.MapID != b.Arena.ID)
                    {
                        drop.Add(uid);
                        continue;
                    }
                    still.Add(client);
                }
                for (int i = 0; i < drop.Count; i++) {
                    b.Alive.Remove(drop[i]);PendingReturns.Add(drop[i]);
                }
            }
            ProcessReturns();
            if (still.Count > 1 && !timeout)
            {
                string line = "NobleWar " + b.Title + ": " + still.Count + " remain.";
                for (int i = 0; i < still.Count; i++)
                    still[i].Send(new Message(line, Color.Yellow, Message.FirstRightCorner));
                return;
            }
            if (still.Count == 1)
            {
                Finish(b, still[0]);
                return;
            }
            Kernel.SendWorldMessage(new Message("NobleWar " + b.Title + " ended with no survivor.", Color.Orange, Message.Center), Server.GamePool);
            CloseBracket(b, true);
        }

        private static void Finish(Bracket b, GameState winner)
        {
            if (System.Threading.Interlocked.CompareExchange(ref b.Finishing,1,0)!=0) return;
            try {
                Prize prize;
                lock (Gate) {
                    if (!b.Running || !RewardsReady) return;
                    if (b.Pending==null) {
                        if (!QuestInventory.Player(winner,false) || !b.Alive.Contains(winner.Entity.UID)) return;
                        b.Pending=new Prize { Cycle=CycleStart.ToString("yyyyMMddHHmm",System.Globalization.CultureInfo.InvariantCulture),
                            Uid=winner.Entity.UID,Cp=b.CpPrize,Gold=b.GoldPrize,Rvp=b.RevoraPointPrize,Soul=QuestSoulReward.Create().ID };
                    }
                    prize=b.Pending;
                }
                using (var c=DataHolder.MySqlConnection) {
                    c.Open();using(var t=c.BeginTransaction()) {
                        QuestInventory.Execute(c,t,@"INSERT INTO revora_noble_prizes(cycle,rank,uid,cp,gold,rvp,soul_type)
VALUES(@cycle,@rank,@uid,@cp,@gold,@rvp,@soul) ON CONFLICT(cycle,rank) DO NOTHING",
                            "cycle",prize.Cycle,"rank",(int)b.Rank,"uid",(long)prize.Uid,"cp",(long)prize.Cp,
                            "gold",(decimal)prize.Gold,"rvp",(long)prize.Rvp,"soul",(long)prize.Soul);
                        using(var q=QuestInventory.Command(c,t,@"SELECT EXISTS(SELECT 1 FROM revora_noble_prizes
WHERE cycle=@cycle AND rank=@rank AND uid=@uid AND cp=@cp AND gold=@gold AND rvp=@rvp AND soul_type=@soul)",
                            "cycle",prize.Cycle,"rank",(int)b.Rank,"uid",(long)prize.Uid,"cp",(long)prize.Cp,
                            "gold",(decimal)prize.Gold,"rvp",(long)prize.Rvp,"soul",(long)prize.Soul))
                            if(!Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("NobleWar winner changed.");
                        t.Commit();
                    }
                }
                // Publication is durable before teleport, delivery, or bracket cleanup.
                GameState current;
                if(Kernel.GamePool.TryGetValue(prize.Uid,out current) && QuestInventory.Player(current,false)) {
                    current.Entity.Teleport(TwinCityMap,ClaimX,ClaimY);
                    current.Send(new Message("You won NobleWar "+b.Title+". "+TryClaim(current),Color.Yellow,Message.TopLeft));
                }
                CloseBracket(b,true);
            } catch(Exception ex) {Console.WriteLine("[noble-result] error="+ex.GetType().Name);}
            finally {System.Threading.Interlocked.Exchange(ref b.Finishing,0);}
        }

        private static bool HasPrize(GameState p)
        {
            if(!RewardsReady || !QuestInventory.Player(p,false))return false;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var q=QuestInventory.Command(c,null,
                "SELECT EXISTS(SELECT 1 FROM revora_noble_prizes WHERE uid=@uid AND NOT paid)","uid",(long)p.Entity.UID))return Equals(q.ExecuteScalar(),true);}
        }
        private static string TryClaim(GameState p)
        {
            if(!RewardsReady || !QuestInventory.Player(p) || !SessionInstall.TryEnter(p.Entity.UID))return "Your prize will wait at NobleWar.";
            try {lock(p.ItemSyncRoot) {
                if(!QuestInventory.Player(p))return "Reconnect before claiming your prize.";
                if(p.Inventory.Count>=40)return "Leave one space for your bound P7 soul.";
                ConquerItem soul;long nextCp,nextRvp;decimal nextGold;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    SilverBalance.AssertSaved(c,t,p);
                    string cycle;int rank;long cp,rvp;decimal gold;uint type;
                    using(var q=QuestInventory.Command(c,t,@"SELECT cycle,rank,cp,gold,rvp,soul_type FROM revora_noble_prizes
WHERE uid=@uid AND NOT paid ORDER BY cycle,rank LIMIT 1 FOR UPDATE","uid",(long)p.Entity.UID))
                    using(var r=q.ExecuteReader()) {
                        if(!r.Read())return "You have no NobleWar prize to claim.";
                        cycle=r.GetString(0);rank=r.GetInt32(1);cp=r.GetInt64(2);gold=r.GetDecimal(3);rvp=r.GetInt64(4);type=checked((uint)r.GetInt64(5));
                    }
                    if(cp<0 || cp>uint.MaxValue || rvp<0 || rvp>uint.MaxValue || gold<0 || gold>Entity.MaximumSilver
                        || !ConquerItemInformation.BaseInformations.ContainsKey(type) || type<800000 || type>=900000
                        || ConquerItemInformation.BaseInformations[type].PurificationLevel!=7)
                        throw new InvalidOperationException("NobleWar prize contract invalid.");
                    nextCp=(long)p.Entity.ConquerPoints+cp;nextRvp=(long)p.Entity.RevoraPoint+rvp;nextGold=(decimal)p.Entity.Money+gold;
                    if(nextCp>uint.MaxValue || nextRvp>uint.MaxValue || nextGold>Entity.MaximumSilver)return "Make room in your purse. Your prize will wait.";
                    using(var q=QuestInventory.Command(c,t,"SELECT count(*) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0","uid",(long)p.Entity.UID))
                        if((long)q.ExecuteScalar()>=40)return "Leave one space for your bound P7 soul.";
                    long? oldCp,oldRvp;PurseBalance.CopySaved(p,out oldCp,out oldRvp);
                    if(QuestInventory.Execute(c,t,@"UPDATE entities SET conquerpoints=@cp,revorapoint=@rvp,money=@gold
WHERE uid=@uid AND (CASE WHEN @cpnull THEN conquerpoints IS NULL ELSE conquerpoints=@oldcp END)
AND (CASE WHEN @rpnull THEN revorapoint IS NULL ELSE revorapoint=@oldrp END)",
                        "cp",nextCp,"rvp",nextRvp,"gold",nextGold,"uid",(long)p.Entity.UID,
                        "cpnull",!oldCp.HasValue,"oldcp",oldCp.GetValueOrDefault(),"rpnull",!oldRvp.HasValue,"oldrp",oldRvp.GetValueOrDefault())!=1)
                        throw new InvalidOperationException("NobleWar purse changed.");
                    soul=QuestInventory.New(type,true);QuestInventory.Insert(c,t,p.Entity.UID,soul);
                    if(QuestInventory.Execute(c,t,@"UPDATE revora_noble_prizes SET paid=true,soul_uid=@soul
WHERE cycle=@cycle AND rank=@rank AND uid=@uid AND NOT paid",
                        "cycle",cycle,"rank",rank,"uid",(long)p.Entity.UID,"soul",(long)soul.UID)!=1)
                        throw new InvalidOperationException("NobleWar prize receipt changed.");
                    if(!QuestInventory.Player(p))throw new InvalidOperationException("NobleWar owner changed.");
                    t.Commit();
                }}
                p.Entity.ApplyCommittedConquerPoints((uint)nextCp);p.Entity.ApplyCommittedRevoraPoints((uint)nextRvp);
                SilverBalance.ApplyCommitted(p,(ulong)nextGold);QuestInventory.Install(p,new uint[0],new[]{soul});
                return "Your purse and bound P7 soul are yours.";
            }} catch(Exception ex){QuestInventory.Fail(p,"noble-prize",ex);return "Reconnect before claiming your prize.";}
            finally{SessionInstall.Exit(p.Entity.UID);}
        }

        private static void CloseBracket(Bracket b, bool kickMap)
        {
            List<GameState> kick = new List<GameState>();
            lock (Gate)
            {
                if(b.AdmissionCycle!=null)NobleAdmissions.End(b.AdmissionCycle);
                foreach(uint uid in b.Registered)PendingReturns.Add(uid);
                foreach(uint uid in b.Alive)PendingReturns.Add(uid);
                b.Running = false;
                b.AdmissionCycle = null;
                b.Pending = null;
                b.Registered.Clear();
                b.Alive.Clear();
            }
            if (kickMap && b.Arena != null)
            {
                foreach (GameState client in Kernel.GamePool.Values)
                {
                    if (client != null && client.Entity != null && client.Entity.MapID == b.Arena.ID)
                        kick.Add(client);
                }
                for (int i = 0; i < kick.Count; i++)
                    lock(Gate)PendingReturns.Add(kick[i].Entity.UID);
            }
            ProcessReturns();
            if (!AnyRunning())
                CycleStart = NextStart(DateTime.Now.AddSeconds(2));
        }

        private static void ResetCycle(bool kickMap)
        {
            for (int i = 0; i < Brackets.Length; i++)
                CloseBracket(Brackets[i], kickMap);
            lock (Gate)
            {
                RegisterOpen = false;
                Invited = false;
                CycleStart = NextStart(DateTime.Now.AddSeconds(2));
            }
        }
    }
    internal static class NobleAdmissions
    {
        internal static bool HasPendingReturn(uint uid)
        {
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); using (var q = QuestInventory.Command(c, null,
                    "SELECT EXISTS(SELECT 1 FROM revora_noble_admissions WHERE uid=@uid AND NOT returned)", "uid", (long)uid))
                    return Convert.ToBoolean(q.ExecuteScalar());
            }
        }
        internal static void Initialize()
        {
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); QuestInventory.Execute(c, null, @"CREATE TABLE IF NOT EXISTS revora_noble_admissions(
cycle varchar(20) NOT NULL,uid bigint NOT NULL,arena integer NOT NULL,pkmode integer NOT NULL,
ended boolean NOT NULL DEFAULT false,returned boolean NOT NULL DEFAULT false,
PRIMARY KEY(cycle,uid));
CREATE INDEX IF NOT EXISTS revora_noble_return_owner ON revora_noble_admissions(uid) WHERE NOT returned;");
            }
        }
        internal static void RecoverInterrupted()
        {
            // A process restart forfeits unfinished matches. Committed rankings
            // retain their prizes. Never infer winners from saved map position.
            using (var c = DataHolder.MySqlConnection)
            { c.Open(); QuestInventory.Execute(c, null, "UPDATE revora_noble_admissions SET ended=true WHERE NOT ended"); }
        }
        internal static void Begin(string cycle, ushort arena, GameState[] players)
        {
            if (players.Length < 2
                || players.Any(p => !QuestInventory.Player(p))
                || players.Select(p => p.Entity.UID).Distinct().Count() != players.Length)
                throw new InvalidOperationException("NobleWar admission roster is invalid.");
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); using (var t = c.BeginTransaction())
                {
                    QuestInventory.Execute(c, t, "SET LOCAL lock_timeout='1s'");
                    foreach (var p in players.OrderBy(p => p.Entity.UID))
                    {
                        using (var q = QuestInventory.Command(c, t, "SELECT count(*) FROM revora_noble_admissions WHERE uid=@uid AND cycle<>@cycle AND NOT returned",
                            "uid", (long)p.Entity.UID, "cycle", cycle))
                            if (Convert.ToInt32(q.ExecuteScalar()) != 0) throw new InvalidOperationException("NobleWar previous return is pending.");
                        QuestInventory.Execute(c, t, @"INSERT INTO revora_noble_admissions(cycle,uid,arena,pkmode)
VALUES(@cycle,@uid,@arena,@mode) ON CONFLICT(cycle,uid) DO NOTHING",
                            "cycle", cycle, "uid", (long)p.Entity.UID, "arena", (int)arena, "mode", (int)p.Entity.PKMode);
                        using (var q = QuestInventory.Command(c, t, @"SELECT count(*) FROM revora_noble_admissions
WHERE cycle=@cycle AND uid=@uid AND arena=@arena AND pkmode=@mode AND NOT ended AND NOT returned",
                            "cycle", cycle, "uid", (long)p.Entity.UID, "arena", (int)arena, "mode", (int)p.Entity.PKMode))
                            if (Convert.ToInt32(q.ExecuteScalar()) != 1) throw new InvalidOperationException("NobleWar admission changed.");
                    }
                    using (var q = QuestInventory.Command(c, t, "SELECT count(*) FROM revora_noble_admissions WHERE cycle=@cycle", "cycle", cycle))
                        if (Convert.ToInt32(q.ExecuteScalar()) != players.Length) throw new InvalidOperationException("NobleWar roster changed.");
                    t.Commit();
                }
            }
        }
        internal static void End(string cycle)
        {
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); QuestInventory.Execute(c, null, "UPDATE revora_noble_admissions SET ended=true WHERE cycle=@cycle AND NOT ended", "cycle", cycle);
                using (var q = QuestInventory.Command(c, null, "SELECT count(*) FROM revora_noble_admissions WHERE cycle=@cycle AND NOT ended", "cycle", cycle))
                    if (Convert.ToInt32(q.ExecuteScalar()) != 0) throw new InvalidOperationException("NobleWar end was not saved.");
            }
        }
        internal static void Forfeit(uint uid)
        {
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); QuestInventory.Execute(c, null, "UPDATE revora_noble_admissions SET ended=true WHERE uid=@uid AND NOT returned AND NOT ended", "uid", (long)uid);
                using (var q = QuestInventory.Command(c, null, "SELECT count(*) FROM revora_noble_admissions WHERE uid=@uid AND NOT returned AND NOT ended", "uid", (long)uid))
                    if (Convert.ToInt32(q.ExecuteScalar()) != 0) throw new InvalidOperationException("NobleWar forfeit was not saved.");
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
                    c.Open(); using (var q = QuestInventory.Command(c, null, @"SELECT pkmode FROM revora_noble_admissions
WHERE uid=@uid AND ended AND NOT returned ORDER BY cycle DESC LIMIT 1", "uid", (long)p.Entity.UID))
                    { object value = q.ExecuteScalar(); if (value != null) mode = Convert.ToInt32(value); }
                }
                if (!mode.HasValue) return true;
                if (mode.Value < 0 || mode.Value > byte.MaxValue || !Enum.IsDefined(typeof(Enums.PKMode), (byte)mode.Value))
                    throw new InvalidOperationException("NobleWar return mode is invalid.");
                if (!QuestInventory.Player(p, false)) return false;
                p.Entity.Teleport(1002, NobleWarSystem.TwinCityX, NobleWarSystem.TwinCityY);
                if (p.Entity.MapID != 1002 || p.Entity.X != NobleWarSystem.TwinCityX || p.Entity.Y != NobleWarSystem.TwinCityY)
                    throw new InvalidOperationException("NobleWar return did not reach town.");
                p.Entity.PKMode = (Enums.PKMode)mode.Value;
                p.Send(new Data(true) { UID = p.Entity.UID, ID = Data.ChangePKMode, dwParam = (uint)p.Entity.PKMode });
                if (!EntityTable.SaveEntity(p)) throw new InvalidOperationException("NobleWar returned character was not saved.");
                // Save destination first. A failed acknowledgement retries a safe
                // return on reconnect; it cannot consume a prize or strand a player.
                using (var c = DataHolder.MySqlConnection)
                {
                    c.Open();
                    QuestInventory.Execute(c, null, "UPDATE revora_noble_admissions SET returned=true WHERE uid=@uid AND ended AND NOT returned", "uid", (long)p.Entity.UID);
                    using (var q = QuestInventory.Command(c, null, "SELECT count(*) FROM revora_noble_admissions WHERE uid=@uid AND ended AND NOT returned", "uid", (long)p.Entity.UID))
                        if (Convert.ToInt32(q.ExecuteScalar()) != 0) throw new InvalidOperationException("NobleWar return acknowledgement failed.");
                }
                return true;
            }
            catch (Exception ex) { QuestInventory.Fail(p, "noble-return", ex); return false; }
            finally
            {
                if (pk) Monitor.Exit(p.PkSyncRoot);
                if (virtue) Monitor.Exit(p.VirtueSyncRoot);
                Monitor.Exit(p.ItemSyncRoot);
            }
        }
    }


}
