// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Revora.Client;
using Revora.Network.GamePackets;
using System.Threading;
using System.Globalization;
using System.Collections.Concurrent;
using Revora.Game;
using System.Drawing;
using Revora.Network;

namespace Revora.Game
{
    public unsafe class Champion
    {
        internal static Func<Time32> CombatClock = () => Time32.Now;
        internal static uint Elapsed(Time32 now, Time32 start) { return unchecked((uint)(now.Value - start.Value)); }
        private static readonly object QueueSync = new object();
        private static readonly ConcurrentQueue<ShutdownRetention> shutdownQueue = new ConcurrentQueue<ShutdownRetention>();
        private static int shutdownTick,retainedCount;
        internal static Action<GameState> ResumeShutdown = p => p.ResumeArenaShutdown();
        internal static bool HasRetainedShutdown { get { return Volatile.Read(ref retainedCount)!=0 || Volatile.Read(ref shutdownTick)!=0; } }
        private sealed class ShutdownRetention : Features.IArenaShutdownRetention
        {
            private readonly QualifierList.QualifierGroup group;
            private int firstRequested,secondRequested;
            internal ShutdownRetention(QualifierList.QualifierGroup owner) { group=owner; }
            public bool ShouldDefer(GameState player) {
                GameState current;
                if(!Kernel.GamePool.TryGetValue(player.Entity.UID,out current) || !ReferenceEquals(current,player))return false;
                if(ReferenceEquals(player,group.Player1))Interlocked.Exchange(ref firstRequested,1);
                else if(ReferenceEquals(player,group.Player2))Interlocked.Exchange(ref secondRequested,1);
                else return false;
                player.ChampionStats.Withdrawn=true;
                Interlocked.CompareExchange(ref group.PendingDeparture,player,null);
                Kernel.DisconnectPool.TryAdd(player.Entity.UID,player);
                return !group.CleanupCompleted;
            }
            private bool Requested(GameState p) { return ReferenceEquals(p,group.Player1)?Volatile.Read(ref firstRequested)!=0:Volatile.Read(ref secondRequested)!=0; }
            internal void ReleaseHealthy() {
                foreach(var p in new[]{group.Player1,group.Player2})
                    if(!Requested(p) && !p.Entity.PersistenceRequiresReload && ReferenceEquals(p.ArenaShutdownRetention,this))p.ArenaShutdownRetention=null;
            }
            internal bool Tick() {
                if(!group.CleanupCompleted)return false;
                bool complete=true;
                foreach(var p in new[]{group.Player1,group.Player2}) {
                    if(!Features.SessionInstall.TryEnter(p.Entity.UID)){complete=false;continue;}
                    try {
                        if(!Monitor.TryEnter(p.ItemSyncRoot)){complete=false;continue;}
                        try {
                            if(!ReferenceEquals(p.ArenaShutdownRetention,this))continue;
                            p.ArenaShutdownRetention=null;
                            GameState current;
                            if(!Kernel.GamePool.TryGetValue(p.Entity.UID,out current) || !ReferenceEquals(current,p))continue;
                            try {
                                if(Requested(p))ResumeShutdown(p);
                                else if(p.Entity.PersistenceRequiresReload)p.Disconnect();
                                if((Requested(p) || p.Entity.PersistenceRequiresReload) && !p.ShutdownStarted
                                    && Kernel.GamePool.TryGetValue(p.Entity.UID,out current) && ReferenceEquals(current,p)) {
                                    p.ArenaShutdownRetention=this;complete=false;
                                }
                            }catch(Exception ex) {
                                p.ArenaShutdownRetention=this;complete=false;
                                Console.WriteLine("[champion] shutdown resume failed type="+ex.GetType().Name);
                            }
                        }finally{Monitor.Exit(p.ItemSyncRoot);}
                    }finally{Features.SessionInstall.Exit(p.Entity.UID);}
                }
                return complete;
            }
        }
        internal static void TickShutdowns() {
            if(Interlocked.CompareExchange(ref shutdownTick,1,0)!=0)return;
            try {
                int count=Math.Min(128,shutdownQueue.Count);ShutdownRetention owner;
                for(int n=0;n<count && shutdownQueue.TryDequeue(out owner);n++) {
                    bool complete=false;
                    try{complete=owner.Tick();}
                    finally{if(!complete)shutdownQueue.Enqueue(owner);else Interlocked.Decrement(ref retainedCount);}
                }
            }finally{Volatile.Write(ref shutdownTick,0);}
        }
        public static ConcurrentDictionary<uint, GameState> PlayerList = new ConcurrentDictionary<uint, GameState>();
        public static ConcurrentDictionary<uint, GameState> WaitingPlayerList = new ConcurrentDictionary<uint, GameState>();
        public static ConcurrentDictionary<uint, ChampionStatistic> ChampionStats = new ConcurrentDictionary<uint, ChampionStatistic>();
        public static SafeDictionary<uint, ChampionStatistic> YesterdayChampionStatss = new SafeDictionary<uint, ChampionStatistic>();
        public static List<ChampionStatistic>[] ChampionStatsList;
        public static List<ChampionStatistic> YesterdayChampionList;
        public static int InArenaCount = 0, HistoryArenaCount = 0;
        public static void Sort()
        {
            ChampionStatsList = new List<ChampionStatistic>[8];
            for (int j = 1; j < 8; j++)
            {
                try
                {
                    var where = ChampionStats.Values.Where((a) => a.Points != 0 && a.Grade == j);
                    List<ChampionStatistic> stats = where.OrderByDescending((p) => p.Points).ToList();
                    InArenaCount = stats.Count;
                    for (uint i = 0; i < stats.Count; i++)
                        stats[(int)i].Rank = (byte)(i + 1);
                    ChampionStatsList[j] = stats;
                }
                catch { ChampionStatsList[j] = new List<ChampionStatistic>(); }
            }
        }
        public static void YesterdaySort()
        {
            var where = ChampionStats.Values.Where((a) => a.YesterdayPoints != 0);
            List<ChampionStatistic> stats = where.OrderByDescending((p) => p.YesterdayPoints).ToList();
            HistoryArenaCount = stats.Count;
            for (uint i = 0; i < stats.Count; i++)
                stats[(int)i].YesterdayRank = i + 1;
            YesterdayChampionList = stats;
        }
        private static void SaveArenaStats()
        {
            var array = ChampionStats.Values.ToArray();
            foreach (ChampionStatistic stats in array)
                if (stats != null)
                    Database.ChampionTable.SaveStats(stats);
        }
        public static class QualifierList
        {
            public static ConcurrentDictionary<uint, QualifierGroup> Groups = new ConcurrentDictionary<uint, QualifierGroup>();
            public static Counter GroupCounter = new Revora.Counter();
            public class QualifierGroup
            {
                public Time32 CreateTime;
                public Time32 DoneStamp;
                private ulong firstDamage,secondDamage;
                public uint Player1Damage { get { return (uint)Math.Min(uint.MaxValue,firstDamage); } }
                public uint Player2Damage { get { return (uint)Math.Min(uint.MaxValue,secondDamage); } }
                public bool Done;
                private Game.Enums.PKMode P1Mode, P2Mode;
                public uint ID;
                public Client.GameState Winner, Loser;
                public Client.GameState Player1, Player2;
                private Map dynamicMap;
                public Time32 ImportTime;
                internal readonly Guid EntryKey = Guid.NewGuid();
                private readonly object lifecycle = new object();
                private bool attempted, started, exported;
                private bool resultSettled;
                private bool endingCaptured;
                internal GameState PendingDeparture;
                private ShutdownRetention retention;
                private Database.ChampionTable.ResultAward[] resultAwards;
                internal int FirstGrade,SecondGrade;
                internal readonly DateTime EventDay;
                internal readonly int Round;
                private Map return1, return2;
                private ushort x1, y1, x2, y2;
                internal static Action<string> EntryProbe;
                internal bool EntryConfirmed { get { return started; } }
                internal bool CleanupCompleted { get { return exported; } }
                internal bool OwnsImportedMap {
                    get {
                        Map map; QualifierGroup group;
                        return started && !exported && dynamicMap != null && dynamicMap.ID != 700 && dynamicMap.BaseID == 700
                            && Kernel.Maps.TryGetValue(dynamicMap.ID, out map) && ReferenceEquals(map, dynamicMap)
                            && Groups.TryGetValue(ID, out group) && ReferenceEquals(group, this)
                            && ReferenceEquals(Player1.ChampionGroup, this) && ReferenceEquals(Player2.ChampionGroup, this)
                            && ReferenceEquals(Player1.Map, dynamicMap) && ReferenceEquals(Player2.Map, dynamicMap);
                    }
                }
                internal bool CombatOpen(Time32 now) {
                    lock (lifecycle) {
                        uint elapsed = Elapsed(now, ImportTime);
                        return !Done && OwnsImportedMap && elapsed >= 10000 && elapsed < 190000
                            && Features.QuestInventory.Player(Player1, false) && Features.QuestInventory.Player(Player2, false)
                            && !Player1.ShutdownStarted && !Player2.ShutdownStarted && !Player1.DisconnectStarted && !Player2.DisconnectStarted;
                    }
                }
                internal bool AllowsCombat(GameState attacker, GameState target, Time32 now) {
                    lock (lifecycle) return CombatOpen(now) && Features.QuestInventory.Player(attacker) && Features.QuestInventory.Player(target)
                        && (ReferenceEquals(attacker, Player1) && ReferenceEquals(target, Player2)
                            || ReferenceEquals(attacker, Player2) && ReferenceEquals(target, Player1));
                }
                // Keep a hit's health, score and terminal callbacks within the
                // same match ownership boundary as entry, departure and settlement.
                internal sealed class CombatLease : IDisposable {
                    private readonly List<object> gates = new List<object>();
                    private readonly List<uint> sessions = new List<uint>();
                    internal bool Allowed { get; private set; }
                    private bool Own(object gate) {
                        if (gate == null || !Monitor.TryEnter(gate)) return false;
                        gates.Add(gate); return true;
                    }
                    internal static CombatLease Acquire(Entity attacker, Entity target) {
                        var a = attacker == null ? null : attacker.Owner;
                        var b = target == null ? null : target.Owner;
                        var group = a == null ? null : a.ChampionGroup;
                        var other = b == null ? null : b.ChampionGroup;
                        if (group == null && other == null) return null;
                        var lease = new CombatLease();
                        try {
                            if (group == null || !ReferenceEquals(group, other)
                                || !ReferenceEquals(a.Entity, attacker) || !ReferenceEquals(b.Entity, target)
                                || !lease.Own(group.lifecycle)) return lease;
                            var players = new[] { a, b }.Distinct().OrderBy(p => p.Entity.UID).ToArray();
                            foreach (var player in players) {
                                if (!Features.SessionInstall.TryEnter(player.Entity.UID)) return lease;
                                lease.sessions.Add(player.Entity.UID);
                            }
                            foreach (var player in players) if (!lease.Own(player.ItemSyncRoot)) return lease;
                            lease.Allowed = group.AllowsCombat(a, b, CombatClock());
                            return lease;
                        } catch { lease.Dispose(); throw; }
                    }
                    public void Dispose() {
                        // Item locks follow session leases, which follow lifecycle.
                        for (int i = gates.Count - 1; i >= 1; i--) Monitor.Exit(gates[i]);
                        for (int i = sessions.Count - 1; i >= 0; i--) Features.SessionInstall.Exit(sessions[i]);
                        if (gates.Count != 0) Monitor.Exit(gates[0]);
                        gates.Clear(); sessions.Clear();
                    }
                }
                private bool WithOwners(Action work) {
                    if (Player1 == null || Player2 == null || Player1.Entity == null || Player2.Entity == null
                        || Player1.Entity.UID == Player2.Entity.UID || !Monitor.TryEnter(lifecycle)) return false;
                    var players = new[] { Player1, Player2 }.OrderBy(p => p.Entity.UID).ToArray();
                    int leases = 0, locks = 0;
                    try {
                        foreach (var p in players) { if (!Features.SessionInstall.TryEnter(p.Entity.UID)) return false; leases++; }
                        foreach (var p in players) { if (p.ItemSyncRoot == null || !Monitor.TryEnter(p.ItemSyncRoot)) return false; locks++; }
                        work(); return true;
                    } finally {
                        for (int i = locks - 1; i >= 0; i--) Monitor.Exit(players[i].ItemSyncRoot);
                        for (int i = leases - 1; i >= 0; i--) Features.SessionInstall.Exit(players[i].Entity.UID);
                        Monitor.Exit(lifecycle);
                    }
                }
                private bool Ready(GameState p, GameState other) {
                    QualifierGroup registered;
                    return Groups.TryGetValue(ID, out registered) && ReferenceEquals(registered, this)
                        && EventDay==QualifyEngine.RegistrationClock().Date && Round>0 && Round==InvitationRound(QualifyEngine.RegistrationClock())
                        && p.ChampionStats!=null && p.ChampionStats.EventDay==EventDay && p.ChampionStats.OfferedRound==Round && !p.ChampionStats.Withdrawn
                        && Features.QuestInventory.Player(p) && !p.ShutdownStarted && !p.DisconnectStarted
                        && p.Account != null && p.Account.EntityID == p.Entity.UID
                        && p.ArenaStatistic != null && p.ArenaStatistic.Status == QualifierStatistic.NotSignedUp
                        && p.TeamArenaStatistic != null && p.TeamArenaStatistic.Status == TeamQualifierStatistic.NotSignedUp
                        && p.Entity.Level >= 70 && p.Booth == null && p.Trade != null && !p.Trade.InTrade
                        && p.QualifierGroup == null && p.TeamQualifierGroup == null
                        && p.ChampionStats != null && p.ChampionStats.UID == p.Entity.UID && p.ChampionStats.SignedUp && p.ChampionStats.AcceptBox
                        && other.ChampionStats != null && p.ChampionStats.Grade == other.ChampionStats.Grade
                        && ReferenceEquals(p.ChampionStats.Opponent, other) && ReferenceEquals(p.ChampionGroup, this)
                        && p.ArenaState == Fight && !p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.SoulShackle);
                }
                public QualifierGroup(Client.GameState player1, Client.GameState player2)
                {
                    Player1 = player1;
                    Player2 = player2;
                    var local=QualifyEngine.RegistrationClock();EventDay=local.Date;Round=InvitationRound(local);
                    CreateTime = CombatClock();
                    Done = false;
                    ID = GroupCounter.Next;
                    Done = false;
                    Groups.Add(ID, this);
                }
                public GameState OppositeClient(GameState client)
                {
                    if (client == Player1)
                        return Player2;
                    else
                        return Player1;
                }
                public void Import()
                {
                    using(var admission=Features.ArenaShutdownDrain.Enter()) {
                    if(admission==null)return;
                    WithOwners(delegate {
                        if (attempted || exported) return;
                        if (Done || !Ready(Player1, Player2) || !Ready(Player2, Player1)) { AbortEntry(); return; }
                        if(Player1.ArenaShutdownRetention!=null || Player2.ArenaShutdownRetention!=null){AbortEntry();return;}
                        if(Interlocked.Increment(ref retainedCount)>4096){Interlocked.Decrement(ref retainedCount);AbortEntry();return;}
                        retention=new ShutdownRetention(this);Player1.ArenaShutdownRetention=Player2.ArenaShutdownRetention=retention;
                        shutdownQueue.Enqueue(retention);
                        attempted = true;
                        return1 = Player1.Map; return2 = Player2.Map;
                        x1 = Player1.Entity.X; y1 = Player1.Entity.Y; x2 = Player2.Entity.X; y2 = Player2.Entity.Y;
                        P1Mode = Player1.Entity.PKMode; P2Mode = Player2.Entity.PKMode;
                        try {
                            Map original;
                            if (!Kernel.Maps.TryGetValue(700, out original)) throw new InvalidOperationException("Champion arena map is unavailable.");
                            dynamicMap = original.MakeDynamicMap();
                            PlaceClerk(dynamicMap, StewardNpc, 50, 50, "ChampionSteward");
                            Database.ChampionEntryLedger.Reserve(EntryKey,Player1,Player2,dynamicMap.ID,return1,x1,y1,return2,x2,y2,EventDay,Round);
                            if (EntryProbe != null) EntryProbe("reserved");
                            Player1.Entity.Teleport(original.ID, dynamicMap.ID, 40, 40);
                            if (EntryProbe != null) EntryProbe("first-arrived");
                            Player2.Entity.Teleport(original.ID, dynamicMap.ID, 60, 60);
                            if (EntryProbe != null) EntryProbe("both-arrived");
                            if (Done || !Ready(Player1, Player2) || !Ready(Player2, Player1)
                                || !ReferenceEquals(Player1.Map, dynamicMap) || !ReferenceEquals(Player2.Map, dynamicMap))
                                throw new InvalidOperationException("Champion paired arrival failed.");
                            Player1.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.Ride);
                            Player2.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.Ride);
                            Player1.Entity.PKMode = Player2.Entity.PKMode = Enums.PKMode.PK;
                            Player1.LoadItemStats(); Player2.LoadItemStats();
                            ImportTime = CreateTime = CombatClock();
                            FirstGrade=(int)Player1.ChampionStats.IsGrade;SecondGrade=(int)Player2.ChampionStats.IsGrade;
                            started = true;
                            Database.ChampionEntryLedger.Confirm(EntryKey);
                            Player1.Send(new Data(true) { UID = Player1.Entity.UID, ID = Data.ChangePKMode, dwParam = (uint)Enums.PKMode.PK });
                            Player2.Send(new Data(true) { UID = Player2.Entity.UID, ID = Data.ChangePKMode, dwParam = (uint)Enums.PKMode.PK });
                            Player1.Send(ChampionKernel.OpponentInfo(Player2.ChampionStats).BuildPacket());
                            Player2.Send(ChampionKernel.OpponentInfo(Player1.ChampionStats).BuildPacket());
                            Player1.Send(ChampionKernel.Stats(0, 0).BuildPacket()); Player2.Send(ChampionKernel.Stats(0, 0).BuildPacket());
                        } catch (Exception ex) {
                            Console.WriteLine("[champion] entry failed type=" + ex.GetType().Name);
                            AbortEntry();
                        }
                    });
                    }
                }
                private bool ReturnPlayer(GameState p, Map destination, ushort x, ushort y, Enums.PKMode mode)
                {
                    GameState current;
                    if (!Kernel.GamePool.TryGetValue(p.Entity.UID, out current) || !ReferenceEquals(current, p)) return false;
                    if (!ReferenceEquals(p.ChampionGroup, this)) return !ReferenceEquals(p.Map, dynamicMap);
                    if (dynamicMap != null && ReferenceEquals(p.Map, dynamicMap)) {
                        Map canonical;
                        if (destination == null || !Kernel.Maps.TryGetValue(destination.ID, out canonical) || !ReferenceEquals(destination, canonical))
                            p.Entity.Teleport(1002, 400, 400);
                        else p.Entity.Teleport(destination.ID, x, y);
                        if (ReferenceEquals(p.Map, dynamicMap)) return false;
                    }
                    if (destination != null) p.Entity.PKMode = mode;
                    p.ChampionGroup = null;
                    p.LoadItemStats();
                    p.Send(new Data(true) { UID = p.Entity.UID, ID = Data.ChangePKMode, dwParam = (uint)p.Entity.PKMode });
                    return true;
                }
                private bool ReturnBoth()
                {
                    bool first = ReturnPlayer(Player1, return1, x1, y1, P1Mode);
                    bool second = ReturnPlayer(Player2, return2, x2, y2, P2Mode);
                    if (!first || !second) return false;
                    Database.ChampionEntryLedger.Close(EntryKey,!started);
                    if (dynamicMap != null) dynamicMap.Dispose();
                    ((ICollection<KeyValuePair<uint, QualifierGroup>>)Groups).Remove(new KeyValuePair<uint, QualifierGroup>(ID, this));
                    exported = true;
                    if(retention!=null)retention.ReleaseHealthy();
                    return true;
                }
                private void AbortEntry()
                {
                    started = false; Done = true; Winner = Loser = null;
                    try {
                        if (!ReturnBoth()) throw new InvalidOperationException("Champion entry rollback requires recovery.");
                        Clear(Player1); Clear(Player2);
                    } catch (Exception ex) {
                        Features.QuestInventory.Fail(Player1, "champion-entry-rollback", ex);
                        Features.QuestInventory.Fail(Player2, "champion-entry-rollback", ex);
                    }
                }
                public void Export()
                {
                    WithOwners(delegate {
                        if (exported) return;
                        if (!started) { AbortEntry(); return; }
                        if (!Done) return;
                        if(!resultSettled) {
                            var stats=new[]{Player1.ChampionStats,Player2.ChampionStats}.OrderBy(s=>s.UID).ToArray();
                            lock(stats[0])lock(stats[1])resultSettled=Database.ChampionTable.Settle(this,out resultAwards);
                            if(!resultSettled)return;
                        }
                        if (!ReturnBoth()) throw new InvalidOperationException("Champion match return requires recovery.");
                        Player1.Entity.BringToLife(); Player2.Entity.BringToLife();
                        Clear(Player1);Clear(Player2);Sort();
                        QueueNextRound(Player1,this);QueueNextRound(Player2,this);
                        if(resultAwards!=null)foreach(var p in new[]{Player1,Player2}) {
                            var award=resultAwards.First(a=>ReferenceEquals(a.Stats,p.ChampionStats));
                            p.Send(p.ChampionStats);
                            if(Winner==null)p.Send(new Message("The Champion match ended in a draw. You earned "+award.Award+" Champion Points.",Color.White,Message.TopLeft));
                            else if(ReferenceEquals(p,Winner))p.Send(ChampionKernel.Win(false,award.Award,award.Streak,award.Extra).BuildPacket());
                            else p.Send(ChampionKernel.Lose(award.Award).BuildPacket());
                        }
                    });
                }
                public void End()
                {
                    lock (lifecycle)
                    {
                        if (Done) return;
                        if (!started) { Done = true; DoneStamp = CombatClock(); return; }
                        var departure=Volatile.Read(ref PendingDeparture);
                        if(departure!=null){End(departure);return;}
                        if (firstDamage == secondDamage) { Winner=Loser=null; }
                        else if (firstDamage > secondDamage)
                        {
                            Winner = Player1;
                            Loser = Player2;
                        }
                        else
                        {
                            Winner = Player2;
                            Loser = Player1;
                        }

                        Done = true;
                        DoneStamp = CombatClock();
                    }
                    PersistEnding();
                }
                public void End(GameState loser)
                {
                    lock (lifecycle)
                    {
                        if (Done || (!ReferenceEquals(loser, Player1) && !ReferenceEquals(loser, Player2))) return;
                        if (!started) { Done = true; DoneStamp = CombatClock(); return; }
                        if (ReferenceEquals(Player1, loser))
                        {
                            Winner = Player2;
                            Loser = Player1;
                        }
                        else
                        {
                            Winner = Player1;
                            Loser = Player2;
                        }
                        Done = true;
                        DoneStamp = CombatClock();
                    }
                    PersistEnding();
                }
                internal void PersistEnding() {
                    WithOwners(delegate {
                        if(endingCaptured || !Done || !started || exported)return;
                        var stats=new[]{Player1.ChampionStats,Player2.ChampionStats}.OrderBy(s=>s.UID).ToArray();
                        lock(stats[0])lock(stats[1])endingCaptured=Database.ChampionTable.CaptureOutcome(this);
                    });
                }
                public void UpdateDamage(GameState client, uint damage)
                {
                    lock (lifecycle)
                    {
                        if (CombatOpen(CombatClock()) && (ReferenceEquals(client, Player1) || ReferenceEquals(client, Player2)))
                        {
                            if (ReferenceEquals(client, Player1))
                                firstDamage = firstDamage > ulong.MaxValue-damage ? ulong.MaxValue : firstDamage+damage;
                            else
                                secondDamage = secondDamage > ulong.MaxValue-damage ? ulong.MaxValue : secondDamage+damage;
                            var packet = ChampionKernel.Stats(Player1Damage, Player2Damage).BuildPacket();
                            Player1.Send(packet);
                            Player2.Send(packet);
                        }
                    }
                }
            }
        }
        public const int
        None = 0,
        FindMatch = 1,
        WaitForBox = 2,
        WaitForOther = 3,
        Fight = 4;
        public class QualifyEngine
        {
            public static void DoQuit(Client.GameState client)
            {
                QualifierList.QualifierGroup group;
                lock (QueueSync)
                {
                    if (client == null || client.Entity == null || client.ChampionStats == null) return;
                    GameState canonical;
                    if(!Kernel.GamePool.TryGetValue(client.Entity.UID,out canonical) || !ReferenceEquals(canonical,client))return;
                    client.ChampionStats.Withdrawn=true;
                    group = client.ChampionGroup;
                    if (group == null) { CancelPending(client); return; }
                    if (!ReferenceEquals(group.Player1, client) && !ReferenceEquals(group.Player2, client)) return;
                }
                // Group cleanup may acquire the queue lock; never wait for it under QueueSync.
                group.End(client);
            }
            internal static Func<DateTime> RegistrationClock = () => DateTime.Now;
            internal static bool RegistrationOpen(DateTime local)
            {
                return local.DayOfWeek != DayOfWeek.Saturday && local.DayOfWeek != DayOfWeek.Sunday
                    && local.Hour == 20 && local.Minute >= 55;
            }
            internal static bool EligiblePlayer(Client.GameState client)
            {
                return Features.QuestInventory.Player(client) && !client.ShutdownStarted && !client.DisconnectStarted && client.Account != null
                    && client.Account.EntityID == client.Entity.UID && client.Entity.Level >= 70
                    && client.ChampionStats != null && client.ChampionStats.UID == client.Entity.UID && client.ChampionGroup == null
                    && client.TeamArenaStatistic != null && client.TeamArenaStatistic.Status == TeamQualifierStatistic.NotSignedUp
                    && client.ArenaStatistic != null && client.ArenaStatistic.Status == QualifierStatistic.NotSignedUp
                    && !client.InQualifier() && client.Booth == null && client.Trade != null && !client.Trade.InTrade
                    && client.Map != null && client.Entity.MapID != 601 && client.Entity.MapID != 1081
                    && !(client.Map.BaseID >= 6000 && client.Map.BaseID <= 6003);
            }
            internal static bool CanRegister(Client.GameState client)
            {
                return !Features.ArenaShutdownDrain.Stopping && EligiblePlayer(client) && client.ArenaShutdownRetention==null
                    && !client.ChampionStats.SignedUp && client.ChampionStats.Opponent == null;
            }
            public static void DoSignup(Client.GameState client)
            {
                if (client == null || client.ItemSyncRoot == null) return;
                lock (QueueSync)
                {
                    if (!RegistrationOpen(RegistrationClock()) || !CanRegister(client)) return;
                    uint uid = client.Entity.UID;
                    // Never evict another session or an invitation from the queue.
                    if (WaitingPlayerList.ContainsKey(uid) || !PlayerList.TryAdd(uid, client)) return;
                    client.ChampionStats.SignUpTime = Time32.Now;
                    client.ChampionStats.EventDay=RegistrationClock().Date;
                    client.ChampionStats.OfferedRound=0;client.ChampionStats.Withdrawn=false;
                    client.ChampionStats.SignedUp = true;
                    client.ArenaState = FindMatch;
                    client.Send(client.ChampionStats);
                }
            }
            public static void DoGiveUp(Client.GameState client)
            {
                DoQuit(client);
            }
            public static void DoAccept(Client.GameState client)
            {
                lock (QueueSync)
                {
                    if (!OwnsInvitation(client) || !EligiblePlayer(client) || client.ArenaState != WaitForBox
                        || !InvitationCurrent(client,RegistrationClock())) return;
                    client.ChampionStats.AcceptBox = true;
                    client.ArenaState = WaitForOther;
                }
            }
        }
        internal const uint RegistrarNpc = 703001, StewardNpc = 703002;
        internal static void PlaceRegistrar() { PlaceClerk(Kernel.Maps[1002], RegistrarNpc, 323, 295, "ChampionRegistrar"); }
        private static void PlaceClerk(Map map, uint id, ushort x, ushort y, string name) {
            Revora.Interfaces.INpc existing;
            if (map.Npcs.TryGetValue(id, out existing)) {
                if (existing.Name == name && existing.Mesh == 50340 && existing.Type == Enums.NpcType.Talker) return;
                throw new InvalidOperationException("Champion clerk identity collision.");
            }
            for (int radius = 0; radius <= 8; radius++)
                for (int dx = -radius; dx <= radius; dx++)
                    for (int dy = -radius; dy <= radius; dy++) {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius) continue;
                        int nx = x + dx, ny = y + dy;
                        if (nx < 2 || ny < 2 || nx >= map.Floor.Bounds.Width - 2 || ny >= map.Floor.Bounds.Height - 2
                            || !map.Floor[nx, ny, MapObjectType.Player]
                            || map.Npcs.Values.Any(n => Kernel.GetDistance(n.X, n.Y, (ushort)nx, (ushort)ny) < 3)) continue;
                        map.AddNpc(new NpcSpawn { UID = id, MapID = map.ID, X = (ushort)nx, Y = (ushort)ny,
                            Mesh = 50340, Type = Enums.NpcType.Talker, Name = name });
                        return;
                    }
            throw new InvalidOperationException("Champion clerk landing unavailable.");
        }
        internal static bool HandleNpc(GameState player, NpcRequest request) {
            if (player == null || request == null || (player.ActiveNpc != RegistrarNpc && player.ActiveNpc != StewardNpc)) return false;
            if (request.NpcID != player.ActiveNpc) return true;
            bool steward = player.ActiveNpc == StewardNpc;
            var group = player.ChampionGroup;
            if (steward && (group == null || !group.OwnsImportedMap)) return true;
            if (!Features.QuestInventory.Near(player, steward ? StewardNpc : RegistrarNpc, steward ? player.Map.ID : (ushort)1002)) return true;
            var dialog = new Npcs(player);
            if (request.OptionID == 255) return true;
            if (steward) {
                if (request.OptionID == 4) { QualifyEngine.DoQuit(player); dialog.Text("Your withdrawal has been recorded."); }
                else { dialog.Text("Yield this bout and leave the contest?"); dialog.Option("I yield.", 4); }
            } else {
                if (request.OptionID == 5) {
                    dialog.Text("The arena tempers your gear and Chi. Runes, JiangHu, Inner Power, relics, Perfection and outside stat boosts lend no strength here.");
                    dialog.Option("Understood.", 0); dialog.Send(); return true;
                }
                dialog.Option("What are the rules?", 5);
                if (request.OptionID == 1) QualifyEngine.DoSignup(player);
                else if (request.OptionID == 2) QualifyEngine.DoAccept(player);
                else if (request.OptionID == 3) QualifyEngine.DoGiveUp(player);
                else if (request.OptionID == 4) QualifyEngine.DoQuit(player);
                if (player.ChampionStats != null && player.ChampionStats.SignedUp) {
                    if (OwnsInvitation(player)) {
                        dialog.Text(player.ChampionStats.AcceptBox ? "Wait here. Your opponent must answer." : "Your opponent awaits. Will you enter the arena?");
                        if (!player.ChampionStats.AcceptBox) dialog.Option("I am ready.", 2);
                        dialog.Option("Withdraw me.", 3);
                    } else { dialog.Text("Your name is entered. Stay near me and be ready for your bout."); dialog.Option("Withdraw me.", 4); }
                } else {
                    dialog.Text("Level 70 fighters may enter on weekdays, from 20:55 to 21:00, server time. Five bouts await.");
                    if (QualifyEngine.RegistrationOpen(QualifyEngine.RegistrationClock())) dialog.Option("Enter my name.", 1);
                }
            }
            dialog.Option("Farewell.", 255); dialog.Send(); return true;
        }
        private static void OfferInvitation(GameState player) {
            var opponent = player.ChampionStats.Opponent;
            var day = player.ChampionStats.EventDay;
            int round = player.ChampionStats.OfferedRound;
            Action<GameState, bool> reply = (current, accept) => {
                lock (QueueSync) {
                    if (!ReferenceEquals(current, player) || !OwnsInvitation(player) || !QualifyEngine.EligiblePlayer(player)
                        || player.ChampionStats.EventDay != day || player.ChampionStats.OfferedRound != round
                        || !ReferenceEquals(player.ChampionStats.Opponent, opponent)
                        || !InvitationCurrent(player, QualifyEngine.RegistrationClock())) return;
                    if (accept) QualifyEngine.DoAccept(player);
                    else { player.ChampionStats.Withdrawn = true; CancelPending(player); }
                }
            };
            player.Send(new Message("Your Champion bout is ready. Accept now or speak with the Champion Registrar.", Message.System));
            player.MessageBox("Your Champion opponent awaits. Enter the arena?", current => reply(current, true), current => reply(current, false), 60);
        }
        public class Statistics
        {
            public static void ShowTop(Client.GameState client, int type = 2602)
            {
                MemoryStream strm = new MemoryStream();
                BinaryWriter wtr = new BinaryWriter(strm);
                wtr.Write((ushort)0);
                wtr.Write((ushort)type);
                List<ChampionStatistic> list = null;
                IEnumerable<ChampionStatistic> ienum = null;
                if (type == 2603)
                {
                    list = new List<ChampionStatistic>();
                    for (int i = 7; i > 0; i--)
                        if (ChampionStatsList[i].Count != 0)
                            list.Add(ChampionStatsList[i][0]);
                        else
                            list.Add(new ChampionStatistic(true) { Grade = (byte)i, Name = "" });
                    ienum = list;
                }
                else
                    ienum = YesterdayChampionList;//.OrderByDescending(p => p.YesterdayPoints);

                int len = Math.Min(10, ienum.Count());
                wtr.Write((byte)len);
                var _enum = ienum.GetEnumerator();

                for (int i = 0; i < len; i++)
                {
                    if (!_enum.MoveNext()) break;
                    var entry = _enum.Current;

                    wtr.Write((byte)entry.Level);
                    if (type == 2603)
                        wtr.Write((byte)entry.Grade);
                    else
                        wtr.Write((byte)i);
                    wtr.Write((byte)entry.Class);
                    wtr.Write((uint)entry.Model);
                    if (type == 2603)
                        wtr.Write((uint)entry.Points);
                    else
                        wtr.Write((uint)entry.YesterdayPoints);
                    byte[] array = Encoding.Default.GetBytes(entry.Name);
                    for (int j = 0; j < 16; j++)
                    {
                        if (j < entry.Name.Length)
                            wtr.Write(array[j]);
                        else
                            wtr.Write((byte)0);
                    }
                }
                int packetlength = (int)strm.Length;
                strm.Position = 0;
                wtr.Write((ushort)packetlength);
                strm.Position = strm.Length;
                wtr.Write(Encoding.Default.GetBytes("TQServer"));
                strm.Position = 0;
                byte[] buf = new byte[strm.Length];
                strm.Read(buf, 0, buf.Length);
                wtr.Close();
                strm.Close();
                client.Send(buf);
            }
            public static void ShowRankingPage(ushort group, int pageIndex, Client.GameState client)
            {
                MemoryStream strm = new MemoryStream();
                BinaryWriter wtr = new BinaryWriter(strm);
                wtr.Write((ushort)0);
                wtr.Write((ushort)2600);
                int first = (pageIndex - 1) * 10;
                //var list = ChampionStatsList[group];
                int len = 0;
                //if (list.Count >= first) len = list.Count - first;
                len = Math.Min(10, len);
                wtr.Write((byte)group);
                wtr.Write((byte)pageIndex);
                wtr.Write((byte)(first + len));
                //wtr.Write((byte)list.Count);

                for (int i = 0; i < len; i++)
                {
                    //var entry = list[first + i];
                    //wtr.Write((byte)entry.Level);
                    //wtr.Write((byte)entry.Rank);
                    //wtr.Write((byte)entry.Class);
                    //wtr.Write((uint)entry.Model);
                    //wtr.Write((uint)entry.Points);
                    //byte[] array = Program.Encoding.GetBytes(entry.Name);
                    for (int j = 0; j < 16; j++)
                    {
                        //if (j < entry.Name.Length)
                        //wtr.Write(array[j]);
                        //else
                        wtr.Write((byte)0);
                    }
                }
                int packetlength = (int)strm.Length;
                strm.Position = 0;
                wtr.Write((ushort)packetlength);
                strm.Position = strm.Length;
                wtr.Write(Server.Encoding.GetBytes("TQServer"));
                strm.Position = 0;
                byte[] buf = new byte[strm.Length];
                strm.Read(buf, 0, buf.Length);
                wtr.Close();
                strm.Close();
                client.Send(buf);
            }
        }
        public class ChampionKernel : Writer
        {
            public enum KernelType : int
            {
                SignUp = 0,
                DialogCountdown = 1,
                OpponentInfo = 4,
                WinDialog = 6,
                LoseDialog = 7,
                MatchStats = 8
            }
            object[] strs;
            KernelType type;
            public static ChampionKernel SignUp()
            {
                return new ChampionKernel(KernelType.SignUp);
            }
            public static ChampionKernel DialogCountdown()
            {
                return new ChampionKernel(KernelType.DialogCountdown);
            }
            public static ChampionKernel Stats(uint damage1, uint damage2)
            {
                //Console.WriteLine("SendStats");
                return new ChampionKernel(KernelType.MatchStats, damage1.ToString(), damage2.ToString());
            }
            public static ChampionKernel OpponentInfo(ChampionStatistic stats)
            {
                return new ChampionKernel(KernelType.OpponentInfo,
                    stats.UID, stats.Name, stats.Class, "1", stats.Rank, stats.Level,
                    stats.Class, stats.BattlePower);
            }
            public static ChampionKernel Win(bool forfeit, uint pts, int streak, uint extrapts)
            {
                return new ChampionKernel(KernelType.WinDialog,
                    forfeit ? 1 : 0, pts, streak, extrapts);
            }
            public static ChampionKernel Lose(uint pts)
            {
                return new ChampionKernel(KernelType.LoseDialog, pts);
            }
            public ChampionKernel(KernelType type, params object[] strs)
            {
                this.type = type;
                this.strs = strs;
            }
            public byte[] BuildPacket()
            {
                var array = new string[strs.Length];
                for (int i = 0; i < array.Length; i++)
                    array[i] = strs[i].ToString();
                int len = strs.Length;
                for (int i = 0; i < strs.Length; i++)
                    len += array[i].Length;
                byte[] data = new byte[23 + len];
                WriteUInt16((ushort)(data.Length - 8), 0, data);
                WriteUInt16(2604, 2, data);
                data[4] = (byte)type;
                WriteStringList(array, 5, data);
                //data[6] = 2;
                return data;
            }
        }
        public static uint[][] ChampionPoints = new uint[][]
        {
            new uint[] {0,0,0,0,0,0},
            new uint[] {6,10,13,16,19,22},
            new uint[] {8,14,20,23,26,29},
            new uint[] {10,18,27,30,33,36},
            new uint[] {20,30,42,45,48,51},
            new uint[] {35,50,65,68,71,74},
            new uint[] {50,80,98,101,104,107 },
            new uint[] {60,100,121,124,127,130 }
        };
        // A caller-supplied pair cannot mint rewards; only its ended, imported match can settle.
        public static void Win(GameState winner, GameState loser)
        {
            if(winner==null || loser==null || winner.ChampionGroup==null)return;
            var group=winner.ChampionGroup;
            if(ReferenceEquals(group,loser.ChampionGroup) && group.Done
                && ReferenceEquals(group.Winner,winner) && ReferenceEquals(group.Loser,loser))group.Export();
        }
        private static DateTime YesterdaySorted = DateTime.Now;
        private static bool Owns(ConcurrentDictionary<uint, GameState> queue, GameState client)
        {
            GameState current;
            return client != null && client.Entity != null && queue.TryGetValue(client.Entity.UID, out current) && ReferenceEquals(current, client);
        }
        private static void RemoveOwned(ConcurrentDictionary<uint, GameState> queue, GameState client)
        {
            if (client == null || client.Entity == null) return;
            ((ICollection<KeyValuePair<uint, GameState>>)queue).Remove(new KeyValuePair<uint, GameState>(client.Entity.UID, client));
        }
        private static bool OwnsInvitation(GameState client)
        {
            if (!Owns(WaitingPlayerList, client) || client.ChampionStats == null || client.ChampionGroup != null) return false;
            var other = client.ChampionStats.Opponent;
            return other != null && !ReferenceEquals(other, client) && Owns(WaitingPlayerList, other)
                && other.ChampionStats != null && other.ChampionGroup == null
                && ReferenceEquals(other.ChampionStats.Opponent, client);
        }
        private static bool ResetPending(GameState client)
        {
            GameState current;
            if ((PlayerList.TryGetValue(client.Entity.UID, out current) && !ReferenceEquals(current, client))
                || (WaitingPlayerList.TryGetValue(client.Entity.UID, out current) && !ReferenceEquals(current, client))) return false;
            RemoveOwned(PlayerList, client);
            RemoveOwned(WaitingPlayerList, client);
            client.ChampionStats.AcceptBox = false;
            client.ChampionStats.GivenUp = false;
            client.ChampionStats.Opponent = null;
            client.ChampionStats.SignedUp = false;
            // ArenaState is shared by the legacy arenas. Never reset another group's state.
            if (client.QualifierGroup == null && client.TeamQualifierGroup == null) client.ArenaState = None;
            client.Send(client.ChampionStats);
            return true;
        }
        private static void CancelPending(GameState client)
        {
            if (client == null || client.Entity == null || client.ChampionStats == null || client.ChampionGroup != null) return;
            bool paired = OwnsInvitation(client);
            var other = paired ? client.ChampionStats.Opponent : null;
            // A stale connection can clean only entries it still owns.
            if (!ResetPending(client) || other == null) return;
            if (!ResetPending(other)) return;
            if (CanContinue(other,QualifyEngine.RegistrationClock()) && PlayerList.TryAdd(other.Entity.UID, other))
            {
                other.ChampionStats.SignedUp = true;
                other.ChampionStats.SignUpTime = Time32.Now;
                other.ArenaState = FindMatch;
            }
        }
        public static void Clear(GameState client)
        {
            lock (QueueSync)
            {
                if (client == null || client.ChampionStats == null) return;
                if (client.ChampionGroup == null) CancelPending(client);
                else
                {
                    // Disconnect must not erase the opponent needed by the owned match.
                    RemoveOwned(PlayerList, client);
                    RemoveOwned(WaitingPlayerList, client);
                    client.ChampionStats.SignedUp = false;
                }
            }
        }
        internal static int InvitationRound(DateTime local)
        {
            if(local.DayOfWeek==DayOfWeek.Saturday || local.DayOfWeek==DayOfWeek.Sunday
                || local.Hour!=21 || local.Minute>20 || local.Minute%5!=0)return 0;
            return local.Minute/5+1;
        }
        private static bool InvitationCurrent(GameState p,DateTime local)
        {
            return p!=null && p.ChampionStats!=null && !p.ChampionStats.Withdrawn
                && p.ChampionStats.EventDay==local.Date && p.ChampionStats.OfferedRound>0
                && p.ChampionStats.OfferedRound==InvitationRound(local);
        }
        private static bool CanContinue(GameState p,DateTime local)
        {
            return !Features.ArenaShutdownDrain.Stopping && QualifyEngine.EligiblePlayer(p) && !p.ChampionStats.Withdrawn && p.ChampionStats.EventDay==local.Date
                && p.ChampionStats.OfferedRound<5 && local.DayOfWeek!=DayOfWeek.Saturday && local.DayOfWeek!=DayOfWeek.Sunday
                && local.TimeOfDay>=new TimeSpan(20,55,0) && local.TimeOfDay<new TimeSpan(21,21,0);
        }
        private static void QueueNextRound(GameState p,QualifierList.QualifierGroup group)
        {
            lock(QueueSync) {
                if(!CanContinue(p,QualifyEngine.RegistrationClock()) || p.ChampionStats.EventDay!=group.EventDay
                    || p.ChampionStats.OfferedRound!=group.Round || WaitingPlayerList.ContainsKey(p.Entity.UID)
                    || !PlayerList.TryAdd(p.Entity.UID,p))return;
                p.ChampionStats.SignedUp=true;p.ChampionStats.SignUpTime=Time32.Now;p.ArenaState=FindMatch;
                p.Send(p.ChampionStats);
            }
        }
        public static void EngagePlayers()
        {
            lock (QueueSync)
            {
                var local=QualifyEngine.RegistrationClock();int round=InvitationRound(local);
                if(Features.ArenaShutdownDrain.Stopping) {
                    foreach(var p in PlayerList.Values.Concat(WaitingPlayerList.Values).Distinct().ToArray())CancelPending(p);
                    return;
                }
                foreach(var queued in PlayerList.Values.Concat(WaitingPlayerList.Values).Distinct().ToArray())
                    if(queued.ChampionStats==null || queued.ChampionStats.EventDay!=local.Date || local.TimeOfDay>=new TimeSpan(21,21,0))CancelPending(queued);
                if(round==0)return;
                var candidates = PlayerList.Values.ToArray();
                foreach (var player in candidates)
                {
                    if (!Owns(PlayerList, player)) continue;
                    if (!QualifyEngine.EligiblePlayer(player)) { CancelPending(player); continue; }
                    if(player.ChampionStats.EventDay!=local.Date || player.ChampionStats.Withdrawn || player.ChampionStats.OfferedRound>=round)continue;
                    if (!CanJoin(player, Time32.Now) || Time32.Now <= player.ChampionStats.SignUpTime.AddSeconds(10)) continue;
                    foreach (var other in candidates)
                    {
                        if (ReferenceEquals(player, other) || !Owns(PlayerList, other) || !QualifyEngine.EligiblePlayer(other)
                            || other.ChampionStats.EventDay!=local.Date || other.ChampionStats.Withdrawn || other.ChampionStats.OfferedRound>=round
                            || !CanJoin(other, Time32.Now) || Time32.Now <= other.ChampionStats.SignUpTime.AddSeconds(10)
                            || player.ChampionStats.Grade != other.ChampionStats.Grade) continue;
                        if (WaitingPlayerList.ContainsKey(player.Entity.UID) || WaitingPlayerList.ContainsKey(other.Entity.UID)) continue;
                        if (!WaitingPlayerList.TryAdd(player.Entity.UID, player)) continue;
                        if (!WaitingPlayerList.TryAdd(other.Entity.UID, other)) { RemoveOwned(WaitingPlayerList, player); continue; }
                        RemoveOwned(PlayerList, player); RemoveOwned(PlayerList, other);
                        player.ChampionStats.OfferedRound=other.ChampionStats.OfferedRound=round;
                        player.ChampionStats.Opponent = other; other.ChampionStats.Opponent = player;
                        player.ChampionStats.AcceptBox = other.ChampionStats.AcceptBox = false;
                        player.ChampionStats.AcceptBoxShow = other.ChampionStats.AcceptBoxShow = Time32.Now;
                        player.ArenaState = other.ArenaState = WaitForBox;
                        player.Send(player.ChampionStats); other.Send(other.ChampionStats);
                        OfferInvitation(player); OfferInvitation(other);
                        break;
                    }
                }
            }
        }
        public static void VerifyAwaitingPeople()
        {
            var admitted = new List<QualifierList.QualifierGroup>();
            lock (QueueSync)
            {
                foreach (var player in WaitingPlayerList.Values.ToArray())
                {
                    if (!Owns(WaitingPlayerList, player)) continue;
                    if (player.ChampionStats == null) { RemoveOwned(WaitingPlayerList, player); continue; }
                    if (!OwnsInvitation(player)) { CancelPending(player); continue; }
                    var other = player.ChampionStats.Opponent;
                    if(!InvitationCurrent(player,QualifyEngine.RegistrationClock()) || !InvitationCurrent(other,QualifyEngine.RegistrationClock())) {CancelPending(player);continue;}
                    if (!QualifyEngine.EligiblePlayer(player) || !QualifyEngine.EligiblePlayer(other)
                        || player.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.SoulShackle)
                        || other.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.SoulShackle)) { CancelPending(player); continue; }
                    if (Time32.Now > player.ChampionStats.AcceptBoxShow.AddSeconds(60)) { CancelPending(player); continue; }
                    if (player.ArenaState != WaitForOther || other.ArenaState != WaitForOther
                        || !player.ChampionStats.AcceptBox || !other.ChampionStats.AcceptBox) continue;
                    RemoveOwned(WaitingPlayerList, player); RemoveOwned(WaitingPlayerList, other);
                    player.ArenaState = other.ArenaState = Fight;
                    var group = new QualifierList.QualifierGroup(player, other);
                    player.ChampionGroup = other.ChampionGroup = group;
                    admitted.Add(group);
                }
            }
            // Teleport/stat work can acquire player locks; never hold QueueSync here.
            foreach (var group in admitted) group.Import();
        }
        public static void CheckGroups()
        {
            if (QualifierList.Groups.Count > 0)
            {
                foreach (var group in QualifierList.Groups.Values)
                {
                    var departure=Volatile.Read(ref group.PendingDeparture);
                    if(departure!=null)group.End(departure);
                    if(group.Done)group.PersistEnding();
                    if(Features.ArenaShutdownDrain.Stopping && !group.EntryConfirmed){group.End();group.Export();continue;}
                    if (!group.EntryConfirmed && !group.Done) group.Import();
                    if (Elapsed(CombatClock(), group.CreateTime) > 5000)
                    {
                        if (!group.Done)
                        {
                            if (Elapsed(CombatClock(), group.CreateTime) >= 190000)
                            {
                                group.End();
                            }
                        }
                        else
                        {
                            if (Elapsed(CombatClock(), group.DoneStamp) > 4000)
                            {
                                group.Export();
                            }
                        }
                    }
                }
            }
            TickShutdowns();
        }
        public static void Reset()
        {
            var today=DateTime.Now.Date;
            foreach(var stat in ChampionStats.Values.ToArray())lock(stat) {
                if(stat.LastReset.Date==today)continue;
                Database.ChampionTable.Reset(stat);
                try{Database.ChampionTable.SaveStats(stat);}
                catch(Exception ex){Console.WriteLine("[champion] daily reset failed type="+ex.GetType().Name);}
            }
        }
        private static bool CanJoin(GameState client, Time32 now)
        {
            if (QualifyEngine.EligiblePlayer(client) && client.ChampionStats.SignedUp)
            {
                if (client.Entity.MapID == 1081) return false;
                if (client.ChampionStats.Opponent == null)
                {
                    if (client.ChampionGroup == null)
                    {
                        if (client.Map.BaseID == 1038) return false;
                        if (client.Map.BaseID == 700) return false;
                        if (client.Entity.MapID >= 1090 && client.Entity.MapID <= 1094) return false;
                        if (client.Entity.MapID >= 1505 && client.Entity.MapID <= 1509) return false;
                        if (client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.SoulShackle)) return false;
                        if (!Constants.PKFreeMaps.Contains(client.Map.ID) || client.Map.ID == 1005)
                            if (client.ArenaState == Champion.FindMatch)
                                return true;
                    }
                }
            }
            return false;
        }
        // The legacy2604 input layout has no native acceptance evidence.
        // Revora controls use the established NPC request/reply path instead.
        public static void Handle(GameState client, byte[] packet) { }

    }
}
