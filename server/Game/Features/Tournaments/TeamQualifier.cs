/*
 * The following code belongs to Ultimation and impulse.
 * Ultimation is credited for most of this piece of code.
 * Thanks to him, we have arena implented.
 * */
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

namespace Revora.Game
{
    public class TeamQualifier
    {
        internal const int MatchDurationMilliseconds = 5 * 60 * 1000;
        internal const int PreparationMilliseconds = 10 * 1000;
        internal static readonly Features.ArenaSignupWithdrawal SignupWithdrawals=new Features.ArenaSignupWithdrawal(true);
        private static readonly object invitationGate=new object();
        internal static void RejectInvitation(GameState a,GameState b) {
            if(a==null || b==null || ReferenceEquals(a,b) || a.TeamArenaStatistic==null || b.TeamArenaStatistic==null
                || a.TeamQualifierGroup!=null || b.TeamQualifierGroup!=null)return;
            QualifierList.QualifierGroup pending;
            lock(invitationGate) {
                if(!ReferenceEquals(a.TeamArenaStatistic.PlayWith,b) || !ReferenceEquals(b.TeamArenaStatistic.PlayWith,a))return;
                pending=QualifierList.Groups.Values.FirstOrDefault(g=>g.OwnedEntryRequested && !g.ImportStarted
                    && ((ReferenceEquals(g.Player1,a) && ReferenceEquals(g.Player2,b)) || (ReferenceEquals(g.Player1,b) && ReferenceEquals(g.Player2,a))));
                if(pending==null) {
                    if(QualifierList.Groups.Values.Count(g=>g.OwnedEntryRequested && !g.ImportStarted)>=1024)return;
                    pending=new QualifierList.QualifierGroup(a,b){OwnedEntryRequested=true};
                }
            }
            // The existing scheduler retries a busy rejection without legacy payouts.
            pending.RejectUnimported();
        }
        private static readonly Features.TeamArenaDisconnectRetention ownedEntries=new Features.TeamArenaDisconnectRetention(1024,null);
        internal static void ImportMatched(QualifierList.QualifierGroup group,Features.TeamArenaDisconnectRetention owner) {
            if(!Features.ArenaStartupPreparation.Prepared){group.Import();return;}
            group.OwnedEntryRequested=true;
            if(Features.ArenaShutdownDrain.Stopping){group.RejectUnimported();return;}
            if(SignupWithdrawals.Pending(group.Player1) || SignupWithdrawals.Pending(group.Player2)){group.RejectUnimported();return;}
            if(!owner.Import(group) && group.CompletionOwner==null)group.RejectUnimported();
        }
        public static ConcurrentDictionary<uint, GameState> PlayerList = new ConcurrentDictionary<uint, GameState>();
        public static ConcurrentDictionary<uint, GameState> WaitingPlayerList = new ConcurrentDictionary<uint, GameState>();

        public static ConcurrentDictionary<uint, TeamQualifierStatistic> ArenaStatistics = new ConcurrentDictionary<uint, TeamQualifierStatistic>();

        public static SafeDictionary<uint, TeamQualifierStatistic> YesterdayArenaStatistics = new SafeDictionary<uint, TeamQualifierStatistic>();

        public static List<TeamQualifierStatistic> ArenaStatisticsList, YesterdayArenaStatisticsList;

        public static int InArenaCount = 0, HistoryArenaCount = 0;

        public static void Sort()
        {
            var where = ArenaStatistics.Values;//.Where((a) => a.TodayWin != 0 || a.TodayBattles != 0);
            List<TeamQualifierStatistic> stats = where.OrderByDescending((p) => p.ArenaPoints).ToList();
            InArenaCount = stats.Count;
            for (uint i = 0; i < stats.Count; i++)
                stats[(int)i].Rank = i + 1;
            ArenaStatisticsList = stats;
        }
        public static void YesterdaySort()
        {
            var where = ArenaStatistics.Values.Where((a) => a.LastSeasonWin != 0 || a.LastSeasonLose != 0);
            List<TeamQualifierStatistic> stats = where.OrderByDescending((p) => p.LastSeasonArenaPoints).ToList();
            HistoryArenaCount = stats.Count;
            for (uint i = 0; i < stats.Count; i++)
                stats[(int)i].LastSeasonRank = i + 1;
            YesterdayArenaStatisticsList = stats;
        }

        private static void SaveArenaStats()
        {
            var array = ArenaStatistics.Values.ToArray();
            foreach (TeamQualifierStatistic stats in array)
                if (stats != null)
                    Database.TeamArenaTable.SaveArenaStatistics(stats);
        }

        public static class QualifierList
        {
            public static ConcurrentDictionary<uint, QualifierGroup> Groups = new ConcurrentDictionary<uint, QualifierGroup>();
            public static Counter GroupCounter = new Counter();

            public static byte[] BuildPacket(ushort page)
            {
                MemoryStream strm = new MemoryStream();
                BinaryWriter wtr = new BinaryWriter(strm);
                wtr.Write((ushort)0);
                wtr.Write((ushort)2242);
                wtr.Write((uint)page);
                wtr.Write((uint)Game.Enums.ArenaIDs.QualifierList);
                wtr.Write((uint)Groups.Count);
                wtr.Write((uint)PlayerList.Count);
                page--;
                wtr.Write((uint)(Groups.Count - page));
                QualifierGroup[] GroupsList = Groups.Values.ToArray();
                for (int count = page; count < page + 6; count++)
                {
                    if (count >= Groups.Count)
                        break;

                    QualifierGroup entry = GroupsList[count];

                    wtr.Write((uint)(entry.Player1.TeamArenaStatistic.EntityID));
                    byte[] array = Encoding.Default.GetBytes(entry.Player1.TeamArenaStatistic.Name);
                    for (int i = 0; i < 16; i++)
                    {
                        if (i < entry.Player1.TeamArenaStatistic.Name.Length)
                        {
                            wtr.Write(array[i]);
                        }
                        else
                            wtr.Write((byte)0);
                    }
                    uint player1Teammates = 0;
                    if (entry.Player1.Team != null)
                        if (entry.Player1.Team.Teammates != null)
                            player1Teammates = (uint)entry.Player1.Team.Teammates.Length;
                    wtr.Write(player1Teammates);
                    wtr.Write((uint)entry.Player2.TeamArenaStatistic.EntityID);
                    byte[] array2 = Encoding.Default.GetBytes(entry.Player2.TeamArenaStatistic.Name);
                    for (int i = 0; i < 16; i++)
                    {
                        if (i < entry.Player2.TeamArenaStatistic.Name.Length)
                        {
                            wtr.Write(array2[i]);
                        }
                        else
                            wtr.Write((byte)0);
                    }

                    uint player2Teammates = 0;
                    if (entry.Player2.Team != null)
                        if (entry.Player2.Team.Teammates != null)
                            player2Teammates = (uint)entry.Player2.Team.Teammates.Length;
                    wtr.Write(player2Teammates);
                }
                GroupsList = null;
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
                return buf;
            }

            public class QualifierParticipants
            {
                public enum KindOfParticipants : ushort
                {
                    Neutral = 0,
                    Opponents = 1
                }
                private ICollection<Client.GameState> Players;
                private ushort id;
                private byte[] built;

                public QualifierParticipants(Client.GameState player, KindOfParticipants kind)
                {
                    id = (ushort)kind;
                    Players = player.Team.Teammates;
                }
                public byte[] Build(uint uid)
                {
                    if (built != null)
                    {
                        Network.Writer.WriteUInt32(uid, 8, built);
                        return built;
                    }
                    else
                    {
                        uint correctPlayers = (uint)Players.Count;
                        MemoryStream strm = new MemoryStream();
                        BinaryWriter wtr = new BinaryWriter(strm);
                        wtr.Write((ushort)0);
                        wtr.Write((ushort)2247);
                        wtr.Write((uint)id);
                        wtr.Write(uid);
                        wtr.Write((uint)0);
                        foreach (Client.GameState client in Players)
                        {
                            if (id == (ushort)KindOfParticipants.Opponents)
                            {
                                if (client.TeamQualifierGroup == null)
                                {
                                    correctPlayers--;
                                    continue;
                                }
                            }

                            wtr.Write((uint)client.Entity.UID);
                            wtr.Write((uint)client.Entity.Level);
                            wtr.Write((uint)client.Entity.Class);
                            wtr.Write((uint)client.Entity.Body / 1000);
                            wtr.Write((uint)client.TeamArenaStatistic.Rank);
                            wtr.Write((uint)client.ArenaPoints);
                            for (int i = 0; i < 16; i++)
                            {
                                if (i < client.TeamArenaStatistic.Name.Length)
                                {
                                    wtr.Write((byte)client.TeamArenaStatistic.Name[i]);
                                }
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
                        built = buf;
                        Network.Writer.WriteUInt32(correctPlayers, 12, buf);
                        return buf;
                    }
                }
            }

            public class QualifierGroup
            {
                #region Watchers
                public byte[] BuildWatcherPacket(ushort id = 0, uint uid = 0)
                {
                    MemoryStream strm = new MemoryStream();
                    BinaryWriter wtr = new BinaryWriter(strm);
                    wtr.Write((ushort)38);
                    wtr.Write((ushort)2211);
                    wtr.Write((ushort)id);
                    wtr.Write((uint)5);
                    wtr.Write((uint)uid);
                    wtr.Write((uint)0);
                    wtr.Write((uint)Player1Cheers);
                    wtr.Write((uint)Player2Cheers);
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
                    return buf;
                }
                public byte[] BuildWatcherPacket(ICollection<Client.GameState> list, ushort id = 2, uint uid = 0)
                {
                    MemoryStream strm = new MemoryStream();
                    BinaryWriter wtr = new BinaryWriter(strm);
                    wtr.Write((ushort)38);
                    wtr.Write((ushort)2211);
                    wtr.Write((ushort)id);
                    wtr.Write((uint)5);
                    wtr.Write((uint)list.Count);
                    wtr.Write((uint)uid);
                    wtr.Write((uint)Player1Cheers);
                    wtr.Write((uint)Player2Cheers);
                    foreach (Client.GameState client in list)
                    {
                        wtr.Write((uint)client.Entity.Mesh);
                        for (int i = 0; i < 16; i++)
                        {
                            if (i < client.ArenaStatistic.Name.Length)
                            {
                                wtr.Write((byte)client.ArenaStatistic.Name[i]);
                            }
                            else
                                wtr.Write((byte)0);
                        }
                        wtr.Write((uint)0);
                        wtr.Write((uint)0);
                        wtr.Write((uint)0);
                        wtr.Write((uint)0);
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
                    return buf;
                }
                public void BeginWatching(Client.GameState client)
                {
                    using (var admission = Features.ArenaShutdownDrain.Enter())
                    {
                        if (admission == null) return;
                        BeginWatchingCore(client);
                    }
                }
                private void BeginWatchingCore(Client.GameState client)
                {
                    if (Features.ArenaShutdownDrain.Stopping) return;
                    if(client==null || client.ItemSyncRoot==null || !Monitor.TryEnter(outcomeSync))return;
                    try {
                        if(!Monitor.TryEnter(client.ItemSyncRoot))return;
                        try {
                            if(Done || Finished || exported || !admissionReady || PendingDeparture!=null || !ResultMapRegistered
                                || !Features.QuestInventory.Player(client) || client.WatchingGroup!=null || client.TeamWatchingGroup!=null
                                || client.QualifierGroup!=null || client.TeamQualifierGroup!=null)return;
                            client.TeamWatchingGroup=this;
                            client.Entity.Teleport(1005,dynamicMap.ID,(ushort)Kernel.Random.Next(35,70),(ushort)Kernel.Random.Next(35,70));
                            if(!Features.QuestInventory.Player(client) || !ReferenceEquals(client.TeamWatchingGroup,this)
                                || !ReferenceEquals(client.Map,dynamicMap)) {
                                if(ReferenceEquals(client.TeamWatchingGroup,this))client.TeamWatchingGroup=null;
                                return;
                            }
                            Watchers.Add(client);client.Send(BuildWatcherPacket());client.Send(MatchPacket.BuildPacket());BroadcastWatchers();
                        }finally{Monitor.Exit(client.ItemSyncRoot);}
                    }finally{Monitor.Exit(outcomeSync);}
                }
                private void BroadcastWatchers() {
                    var data=BuildWatcherPacket(Watchers);
                    foreach(var p in Watchers.Concat(firstParticipants).Concat(secondParticipants).Distinct().ToArray())
                        if(Features.QuestInventory.Player(p,false) && (ReferenceEquals(p.TeamWatchingGroup,this) || ReferenceEquals(p.TeamQualifierGroup,this)))p.Send(data);
                }
                internal bool LeaveWatching(GameState client) {
                    if(client==null || client.ItemSyncRoot==null || !Monitor.TryEnter(outcomeSync))return false;
                    try {
                        if(!Monitor.TryEnter(client.ItemSyncRoot))return false;
                        try {
                            if(!Features.QuestInventory.Player(client,false) || !ReferenceEquals(client.TeamWatchingGroup,this) || !Watchers.Contains(client))return false;
                            if(ReferenceEquals(client.Map,dynamicMap)) {
                                client.Entity.PreviousTeleport();
                                if(ReferenceEquals(client.Map,dynamicMap))client.Entity.Teleport(1002,400,400);
                                if(ReferenceEquals(client.Map,dynamicMap))return false;
                            }
                            if(!Features.QuestInventory.Player(client,false) || !ReferenceEquals(client.TeamWatchingGroup,this))return false;
                            Watchers.Remove(client);client.TeamWatchingGroup=null;client.Send(BuildWatcherPacket(3));BroadcastWatchers();return true;
                        }finally{Monitor.Exit(client.ItemSyncRoot);}
                    }finally{Monitor.Exit(outcomeSync);}
                }
                internal void Cheer(GameState client,uint uid) {
                    if(client==null || client.ItemSyncRoot==null || !Monitor.TryEnter(outcomeSync))return;
                    try {
                        if(!Monitor.TryEnter(client.ItemSyncRoot))return;
                        try {
                            if(Done || Finished || exported || !admissionReady || PendingDeparture!=null || !ResultMapRegistered
                                || !Features.QuestInventory.Player(client) || !ReferenceEquals(client.TeamWatchingGroup,this)
                                || !Watchers.Contains(client) || !ReferenceEquals(client.Map,dynamicMap)
                                || (uid!=Player1.Entity.UID && uid!=Player2.Entity.UID) || Cheerers.Contains(client.Entity.UID))return;
                            Cheerers.Add(client.Entity.UID);
                            if(uid==Player1.Entity.UID){client.TeamCheerFor=1;if(Player1Cheers<uint.MaxValue)Player1Cheers++;}
                            else{client.TeamCheerFor=2;if(Player2Cheers<uint.MaxValue)Player2Cheers++;}
                            BroadcastWatchers();
                        }finally{Monitor.Exit(client.ItemSyncRoot);}
                    }finally{Monitor.Exit(outcomeSync);}
                }
                public List<Client.GameState> Watchers = new List<GameState>();
                #endregion

                public Time32 CreateTime;
                public Time32 DoneStamp;

                public uint Player1Damage, Player2Damage;
                public uint Player1Cheers, Player2Cheers;

                public List<uint> Cheerers = new List<uint>();

                public bool Inside;
                public bool Done, Finished;

                public uint ID;

                public GroupMatch MatchPacket = new GroupMatch();

                public Client.GameState Winner, Loser;
                public Client.GameState Player1, Player2;

                private Map dynamicMap;
                private GameState[] firstParticipants = new GameState[0], secondParticipants = new GameState[0];
                private GameState[] reservedFirst = new GameState[0], reservedSecond = new GameState[0];
                internal bool AdmissionInterrupted, DurableAdmissionCancelled;
                internal GameState[] ReservationParticipants { get { lock(outcomeSync)return reservedFirst.Concat(reservedSecond).ToArray(); } }
                private int importStarted;
                private bool admissionReady;
                private bool exported;
                private readonly HashSet<GameState> legacyPaid = new HashSet<GameState>();
                private uint legacyDelta;
                internal int DurableResultReady;
                internal bool DurableAdmissionReserved;
                internal bool ResultMapRegistered
                {
                    get { Map registered; return dynamicMap != null && Kernel.Maps.TryGetValue(dynamicMap.ID, out registered) && ReferenceEquals(registered, dynamicMap); }
                }
                internal readonly Guid ResultKey = Guid.NewGuid();
                internal object ResultSync { get { return outcomeSync; } }
                internal readonly object DepartureSync = new object();
                private readonly HashSet<GameState> departedParticipants = new HashSet<GameState>();
                private readonly HashSet<GameState> withdrawnMembers = new HashSet<GameState>();
                internal Features.TeamArenaResultCoordinator.Departure PendingDeparture;
                internal bool ResultAdmissionReady { get { return Volatile.Read(ref admissionReady); } }
                internal bool ResultCleanupCompleted { get { return Volatile.Read(ref exported); } }
                internal DateTime OutcomeUtc { get; private set; }
                internal uint RewardDelta
                {
                    get { lock (outcomeSync) { if (legacyDelta == 0) legacyDelta = (uint)Kernel.Random.Next(30, 50); return legacyDelta; } }
                }

                internal GameState[] RewardParticipants(GameState leader)
                {
                    lock (outcomeSync)
                    {
                        if (!admissionReady || !Finished) return new GameState[0];
                        return ReferenceEquals(leader, Player1) ? Participants(true)
                            : ReferenceEquals(leader, Player2) ? Participants(false) : new GameState[0];
                    }
                }
                internal bool ApplyOwnedLegacyResult(GameState player, bool won)
                {
                    lock (outcomeSync) lock (player.ItemSyncRoot)
                    {
                        var leader = won ? Winner : Loser;
                        if (DurableResultReady != 0 || !RewardParticipants(leader).Contains(player) || legacyPaid.Contains(player)
                            || !Features.QuestInventory.Player(player, false) || !ReferenceEquals(player.TeamQualifierGroup, this)) return false;
                        uint delta = RewardDelta;
                        ApplyLegacyResult(player, won, ReferenceEquals(player, leader) ? delta : delta - 10);
                        legacyPaid.Add(player);
                        return true;
                    }
                }

                internal GameState[] Participants(bool first)
                {
                    lock (outcomeSync) return (GameState[])(first ? firstParticipants : secondParticipants).Clone();
                }

                public Time32 ImportTime;
                internal bool HasTimedOut(Time32 now)
                {
                    return unchecked(now.Value - ImportTime.Value) >= MatchDurationMilliseconds;
                }
                internal bool IsPreparing(Time32 now)
                {
                    return !ResultAdmissionReady || unchecked(now.Value - ImportTime.Value) < PreparationMilliseconds;
                }
                internal bool AllowsCombat(GameState attacker, GameState target, Time32 now)
                {
                    if (!Monitor.TryEnter(outcomeSync)) return false;
                    try
                    {
                        lock (DepartureSync)
                        {
                            QualifierGroup registered;
                            if (IsPreparing(now) || HasTimedOut(now) || Done || Finished || PendingDeparture != null
                                || !Inside || !ResultMapRegistered || !Groups.TryGetValue(ID, out registered) || !ReferenceEquals(registered, this)
                                || !Features.QuestInventory.Player(attacker) || !Features.QuestInventory.Player(target)
                                || departedParticipants.Contains(attacker) || departedParticipants.Contains(target)
                                || !ReferenceEquals(attacker.TeamQualifierGroup, this) || !ReferenceEquals(target.TeamQualifierGroup, this)
                                || !ReferenceEquals(attacker.Map, dynamicMap) || !ReferenceEquals(target.Map, dynamicMap)) return false;
                            return (firstParticipants.Contains(attacker) && secondParticipants.Contains(target))
                                || (secondParticipants.Contains(attacker) && firstParticipants.Contains(target));
                        }
                    }
                    finally { Monitor.Exit(outcomeSync); }
                }
                public QualifierGroup(Client.GameState player1, Client.GameState player2)
                {
                    Player1 = player1;
                    Player2 = player2;
                    CreateTime = Time32.Now;
                    Inside = false;
                    Player1Damage = 0;
                    Player2Damage = 0;
                    Done = false;
                    ID = GroupCounter.Next;
                    MatchPacket.Group = this;
                    Done = false;
                    Inside = false;
                    Finished = false;
                    Groups.Add(ID, this);
                }

                public void Import()
                {
                    ImportWithRetention(null);
                }
                internal bool ImportStarted { get { return Volatile.Read(ref importStarted)!=0; } }
                internal volatile Features.TeamArenaDisconnectRetention CompletionOwner;
                internal volatile bool OwnedEntryRequested;
                internal bool ImportWithRetention(Features.TeamArenaDisconnectRetention retention)
                {
                    if (Done || Volatile.Read(ref importStarted)!=0 || Player1.Team == null || Player2.Team == null) return false;
                    var first = Player1.Team.Teammates.Where(p => Features.QuestInventory.Player(p, false) && CanFight(p)).Distinct().ToArray();
                    var second = Player2.Team.Teammates.Where(p => Features.QuestInventory.Player(p, false) && CanFight(p)).Distinct().ToArray();
                    if (first.Length>5 || second.Length>5 || !first.Contains(Player1) || !second.Contains(Player2) || first.Intersect(second).Any()) return false;
                    if(retention!=null)return retention.Enter(this,first.Concat(second).ToArray(),()=>ImportRoster(first,second));
                    ImportRoster(first,second);return ResultAdmissionReady;
                }
                private void ImportRoster(GameState[] first, GameState[] second)
                {
                    if (Features.TeamArenaResultStatistics.Initialized)
                    {
                        Features.ArenaAdmissionGuard.Run(outcomeSync, first.Concat(second).ToArray(),
                            p => !Done && CanFight(p) && p.QualifierGroup == null && (CompletionOwner==null || !SignupWithdrawals.Pending(p))
                                && (p.TeamQualifierGroup == null || ReferenceEquals(p.TeamQualifierGroup, this))
                                && p.WatchingGroup == null && p.TeamWatchingGroup == null,
                            () => ImportReserved(first,second), (c,t) => Features.TeamArenaAdmissions.Reserve(c,t,this,first,second));
                        return;
                    }
                    ImportCore(first, second);
                }
                private void ImportReserved(GameState[] first, GameState[] second)
                {
                    DurableAdmissionReserved=Features.TeamArenaAdmissions.Initialized;
                    if(!DurableAdmissionReserved){ImportCore(first,second);return;}
                    reservedFirst=first;reservedSecond=second;
                    bool failed=false;
                    try { ImportCore(first,second); }
                    catch(Exception ex) { failed=true;Console.WriteLine("Team transfer interrupted: "+ex.GetType().Name); }
                    if(failed || !admissionReady || first.Concat(second).Any(p=>!Features.QuestInventory.Player(p,false)
                        || p.ShutdownStarted || Kernel.DisconnectPool.ContainsKey(p.Entity.UID)
                        || (CompletionOwner!=null && SignupWithdrawals.Pending(p))
                        || !ReferenceEquals(p.Map,dynamicMap) || !ReferenceEquals(p.TeamQualifierGroup,this)))
                    {
                        AdmissionInterrupted=true;admissionReady=false;
                        Features.TeamArenaResultCoordinator.CancelInterrupted(this);
                        Features.TeamArenaResultCoordinator.Cleanup(this);
                    }
                }
                private void ImportCore(GameState[] first, GameState[] second)
                {
                    if (Interlocked.CompareExchange(ref importStarted, 1, 0) != 0) return;
                    Inside = true;
                    if(CompletionOwner!=null && first.Concat(second).Any(p=>SignupWithdrawals.Pending(p)))return;

                    if (!Kernel.Maps.ContainsKey(700))
                        new Map(700, Database.DMaps.MapPaths[700]);
                    Map origMap = Kernel.Maps[700];
                    dynamicMap = origMap.MakeDynamicMap();


                    foreach (var team1Player in first)
                    {
                        if (CanFight(team1Player))
                        {
                            team1Player.PrevPK = team1Player.Entity.PKMode;
                            if (team1Player.Entity.MyJiang != null)
                            {
                                if (team1Player.Entity.MyJiang.OnJiangHu)
                                {
                                    team1Player.Entity.PKMode = Enums.PKMode.Capture;
                                    team1Player.Entity.MyJiang.OnJiangHu = false;
                                    team1Player.Entity.MyJiang.SendStatusMode(team1Player);

                                }
                            }
                            var x = (ushort)Kernel.Random.Next(35, 70);
                            var y = (ushort)Kernel.Random.Next(35, 70);
                            team1Player.Entity.Teleport(origMap.ID, dynamicMap.ID, x, y);
                            if (!ReferenceEquals(team1Player.Map, dynamicMap) || !Features.QuestInventory.Player(team1Player, false)) continue;
                            if (team1Player.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Ride))
                            {
                                team1Player.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.Ride);
                            }
                            team1Player.TeamQualifierGroup = this;
                        }
                    }

                    foreach (var team2Player in second)
                    {
                        if (CanFight(team2Player))
                        {
                            team2Player.PrevPK = team2Player.Entity.PKMode;
                            if (team2Player.Entity.MyJiang != null)
                            {
                                if (team2Player.Entity.MyJiang.OnJiangHu)
                                {
                                    team2Player.Entity.PKMode = Enums.PKMode.Capture;
                                    team2Player.Entity.MyJiang.OnJiangHu = false;
                                    team2Player.Entity.MyJiang.SendStatusMode(team2Player);

                                }
                            }
                            var x = (ushort)Kernel.Random.Next(35, 70);
                            var y = (ushort)Kernel.Random.Next(35, 70);
                            team2Player.Entity.Teleport(origMap.ID, dynamicMap.ID, x, y);
                            if (!ReferenceEquals(team2Player.Map, dynamicMap) || !Features.QuestInventory.Player(team2Player, false)) continue;
                            if (team2Player.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Ride))
                            {
                                team2Player.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.Ride);
                            }
                            team2Player.TeamQualifierGroup = this;
                        }
                    }
                    lock (outcomeSync)
                    {
                        firstParticipants = first.Where(p => ReferenceEquals(p.TeamQualifierGroup, this) && ReferenceEquals(p.Map, dynamicMap)).ToArray();
                        secondParticipants = second.Where(p => ReferenceEquals(p.TeamQualifierGroup, this) && ReferenceEquals(p.Map, dynamicMap)).ToArray();
                    }
                    var team1Op = new QualifierParticipants(Player2, QualifierParticipants.KindOfParticipants.Opponents);
                    foreach (var team1Player in firstParticipants)
                    {
                        if (team1Player.TeamQualifierGroup != null)
                        {
                            team1Player.Entity.AddFlag((ulong)FlagsUpdate.Flags.OrangeSparkles,5,true);
                            team1Player.Send(team1Op.Build(team1Player.Entity.UID));
                            ArenaSignup sign = new ArenaSignup();
                            sign.DialogID = ArenaSignup.MainIDs.StartTheFight;
                            sign.Stats = Player2.TeamArenaStatistic;
                            team1Player.Send(sign.BuildPacket());
                            sign.DialogID = ArenaSignup.MainIDs.Match;
                            sign.OptionID = ArenaSignup.DialogButton.MatchOn;
                            team1Player.Send(sign.BuildPacket());
                            team1Player.Send(MatchPacket.BuildPacket());
                            team1Player.Entity.BringToLife();
                            team1Player.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.Ride);
                            team1Player.Entity.PKMode = Game.Enums.PKMode.Team;
                            team1Player.Send(new Data(true) { UID = team1Player.Entity.UID, ID = Data.ChangePKMode, dwParam = (uint)team1Player.Entity.PKMode });
                        }
                    }
                    var team2Op = new QualifierParticipants(Player1, QualifierParticipants.KindOfParticipants.Opponents);
                    foreach (var team2Player in secondParticipants)
                    {
                        if (team2Player.TeamQualifierGroup != null)
                        {
                            team2Player.Entity.AddFlag((ulong)FlagsUpdate.Flags.PurpleSparkles,5,true);
                            team2Player.Send(team2Op.Build(team2Player.Entity.UID));
                            ArenaSignup sign = new ArenaSignup();
                            sign.DialogID = ArenaSignup.MainIDs.StartTheFight;
                            sign.Stats = Player1.TeamArenaStatistic;
                            team2Player.Send(sign.BuildPacket());
                            sign.DialogID = ArenaSignup.MainIDs.Match;
                            sign.OptionID = ArenaSignup.DialogButton.MatchOn;
                            team2Player.Send(sign.BuildPacket());
                            team2Player.Send(MatchPacket.BuildPacket());
                            team2Player.Entity.BringToLife();
                            team2Player.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.Ride);
                            team2Player.Entity.PKMode = Game.Enums.PKMode.Team;
                            team2Player.Send(new Data(true) { UID = team2Player.Entity.UID, ID = Data.ChangePKMode, dwParam = (uint)team2Player.Entity.PKMode });
                        }
                    }
                    lock (outcomeSync) { ImportTime = Time32.Now; admissionReady = firstParticipants.Contains(Player1) && secondParticipants.Contains(Player2); }
                }

                internal bool ReturnReady(Time32 now)
                {
                    return Done && Finished && unchecked(now.Value - DoneStamp.Value) >= 5000;
                }
                public void Export()
                {
                    if (!Monitor.TryEnter(outcomeSync)) return;
                    GameState[] owners = null; int acquired = 0;
                    try
                    {
                        if (exported || (!Finished && !DurableAdmissionCancelled)) return;
                        if (!DurableAdmissionCancelled && CompletionOwner != null && Volatile.Read(ref DurableResultReady) == 0) return;
                        if (!DurableAdmissionCancelled && !ReturnReady(Features.TeamArenaResultCoordinator.ReturnClock())) return;
                        var fighters=DurableAdmissionCancelled?reservedFirst.Concat(reservedSecond).ToArray():firstParticipants.Concat(secondParticipants).ToArray();
                        owners = fighters.Concat(Watchers.ToArray())
                            .Concat(new[] { Player1, Player2 }).Distinct().OrderBy(p => p.Entity.UID).ToArray();
                        foreach (var player in owners)
                        {
                            if (!Monitor.TryEnter(player.ItemSyncRoot)) return;
                            acquired++;
                        }
                        // Retain retryable cleanup while a canonical actor needs persistence recovery.
                        foreach (var player in owners)
                        {
                            GameState current;
                            if (Kernel.GamePool.TryGetValue(player.Entity.UID, out current) && ReferenceEquals(current, player)
                                && ReferenceEquals(player.Map, dynamicMap) && !Features.QuestInventory.Player(player, false)) return;
                        }
                        if(!DurableAdmissionCancelled)Win(Winner, Loser, this);
                        foreach (var player in owners)
                        {
                            GameState current;
                            if (!Kernel.GamePool.TryGetValue(player.Entity.UID, out current) || !ReferenceEquals(current, player)) continue;
                            bool fighter = fighters.Contains(player);
                            if (fighter ? player.QualifierGroup != null || (player.TeamQualifierGroup != null && !ReferenceEquals(player.TeamQualifierGroup, this))
                                : !ReferenceEquals(player.TeamWatchingGroup, this)) continue;
                            if (dynamicMap != null && ReferenceEquals(player.Map, dynamicMap))
                            {
                                player.Entity.PreviousTeleport();
                                if (ReferenceEquals(player.Map, dynamicMap)) player.Entity.Teleport(1002, 400, 400);
                                if (ReferenceEquals(player.Map, dynamicMap)) return;
                                if (fighter)
                                {
                                    RestoreFighter(player);
                                }
                            }
                            if (!Kernel.GamePool.TryGetValue(player.Entity.UID, out current) || !ReferenceEquals(current, player)) continue;
                            if (fighter)
                            {
                                player.TeamQualifierGroup = null;player.endteam = false;
                                player.TeamArenaStatistic.AcceptBox = player.TeamArenaStatistic.HasBox = false;
                                player.TeamArenaStatistic.PlayWith = null;player.TeamArenaStatistic.Status = TeamQualifierStatistic.NotSignedUp;
                                foreach (uint key in new[] { player.Entity.UID, player.Account == null ? 0u : player.Account.EntityID }.Distinct())
                                {
                                    var entry = new KeyValuePair<uint, GameState>(key, player);
                                    ((ICollection<KeyValuePair<uint, GameState>>)PlayerList).Remove(entry);
                                    ((ICollection<KeyValuePair<uint, GameState>>)WaitingPlayerList).Remove(entry);
                                }
                                player.TeamArenaStatistic.Send(player);
                            }
                            else { player.TeamWatchingGroup = null;player.Send(BuildWatcherPacket(3)); }
                        }
                        Watchers.Clear();
                        ((ICollection<KeyValuePair<uint, QualifierGroup>>)Groups).Remove(new KeyValuePair<uint, QualifierGroup>(ID, this));
                        if (dynamicMap != null) dynamicMap.Dispose();
                        Inside = false;exported = true;
                    }
                    finally
                    {
                        for (int n = acquired - 1; n >= 0; n--) Monitor.Exit(owners[n].ItemSyncRoot);
                        Monitor.Exit(outcomeSync);
                    }
                }

                private readonly object outcomeSync = new object();

                // Leaders are the original match owners, independent of later team membership.
                private bool FreezeOutcome(GameState loser, bool timedOut)
                {
                    lock (outcomeSync)
                    lock (DepartureSync)
                    {
                        if (Finished || Done || (DurableAdmissionReserved && (!admissionReady || AdmissionInterrupted))
                            || Player1 == null || Player2 == null || ReferenceEquals(Player1, Player2)) return false;
                        if (!timedOut && !ReferenceEquals(loser, Player1) && !ReferenceEquals(loser, Player2)) return false;
                        if (timedOut) loser = Player1Damage > Player2Damage ? Player2 : Player1;
                        if (PendingDeparture != null) loser = PendingDeparture.Player;
                        var observed=PendingDeparture==null?Features.ArenaMatchLedger.Clock():PendingDeparture.Observed;
                        if(observed.Kind!=DateTimeKind.Utc)return false;
                        Loser = loser;
                        Winner = ReferenceEquals(loser, Player1) ? Player2 : Player1;
                        Player1.endteam = Player2.endteam = true;
                        DoneStamp = Time32.Now;
                        OutcomeUtc = observed;
                        Done = Finished = true;
                        return true;
                    }
                }

                private void NotifyOutcome()
                {
                    if (!Inside) return;
                    var sign = new ArenaSignup();
                    sign.Stats = Loser.TeamArenaStatistic;
                    sign.DialogID = ArenaSignup.MainIDs.Dialog;
                    sign.OptionID = ArenaSignup.DialogButton.Lose;
                    Loser.Send(sign.BuildPacket());
                    sign.OptionID = ArenaSignup.DialogButton.Win;
                    sign.Stats = Winner.TeamArenaStatistic;
                    Winner.Send(sign.BuildPacket());
                }

                private readonly HashSet<GameState> outcomeNotified = new HashSet<GameState>();
                internal void NotifyCommittedOutcome()
                {
                    if (!Monitor.IsEntered(outcomeSync)) throw new InvalidOperationException("Team result notification requires its match lock.");
                    if (!Inside || !Finished || DurableResultReady == 0) return;
                    foreach (var player in new[] { Loser, Winner })
                    {
                        // A preceding send may synchronously disconnect a participant.
                        if (player == null || !Monitor.IsEntered(player.ItemSyncRoot)
                            || !Features.QuestInventory.Player(player, false)
                            || !ReferenceEquals(player.TeamQualifierGroup, this)
                            || !RewardParticipants(player).Contains(player)
                            || Features.ArenaCacheRecovery.RequiresReload(player.TeamArenaStatistic)
                            || Features.ArenaCacheRecovery.RequiresReload(player.ArenaStatistic)
                            || !outcomeNotified.Add(player)) continue;
                        var sign = new ArenaSignup();
                        sign.Stats = player.TeamArenaStatistic;
                        sign.DialogID = ArenaSignup.MainIDs.Dialog;
                        sign.OptionID = ReferenceEquals(player, Winner) ? ArenaSignup.DialogButton.Win : ArenaSignup.DialogButton.Lose;
                        player.Send(sign.BuildPacket());
                    }
                }

                public void End()
                {
                    if (FreezeOutcome(null, true) && !Features.TeamArenaResultStatistics.Initialized) NotifyOutcome();
                }

                public void End(GameState loser)
                {
                    if (FreezeOutcome(loser, false) && !Features.TeamArenaResultStatistics.Initialized) NotifyOutcome();
                }

                public void CheckEnd(GameState client, bool disconnect = false)
                {
                    if(CompletionOwner!=null) {
                        if(disconnect){Features.TeamArenaResultCoordinator.ObserveDisconnect(this,client);return;}
                        if(!Features.QuestInventory.Player(client,false) || !ReferenceEquals(client.TeamQualifierGroup,this))return;
                    }
                    GameState leader;
                    lock (outcomeSync)
                    lock (DepartureSync)
                    {
                        if (Done || client == null) return;
                        // A recorded departure already owns its outcome. Return teleport
                        // callbacks must not complete it recursively under the member lock.
                        if (CompletionOwner != null && departedParticipants.Contains(client)) return;
                        if (disconnect && (ReferenceEquals(client, Player1) || ReferenceEquals(client, Player2))) leader = client;
                        else
                        {
                            if (!admissionReady) return;
                            var side = firstParticipants.Contains(client) ? firstParticipants : secondParticipants.Contains(client) ? secondParticipants : null;
                            if (side == null) return;
                            if (side.Any(p => !departedParticipants.Contains(p) && !ReferenceEquals(p, disconnect ? client : null)
                                && Features.QuestInventory.Player(p, false) && ReferenceEquals(p.TeamQualifierGroup, this)
                                && ReferenceEquals(p.Map, dynamicMap) && !p.Entity.Dead)) return;
                            leader = ReferenceEquals(side, firstParticipants) ? Player1 : Player2;
                        }
                    }
                    if(CompletionOwner!=null)Features.TeamArenaResultCoordinator.EndForPlayer(this,leader);
                    else End(leader);
                }

                internal bool RejectUnimported()
                {
                    if(!Monitor.TryEnter(outcomeSync))return false;
                    var owners=new[]{Player1,Player2}.OrderBy(p=>p.Entity.UID).ToArray();int acquired=0;
                    try {
                        if(exported)return true;
                        if(ImportStarted || DurableAdmissionReserved || Done || dynamicMap!=null)return false;
                        foreach(var p in owners){if(!Monitor.TryEnter(p.ItemSyncRoot))return false;acquired++;}
                        foreach(var p in owners) {
                            GameState current;bool original=Kernel.GamePool.TryGetValue(p.Entity.UID,out current) && ReferenceEquals(current,p);
                            if(original) {
                                var opponent=ReferenceEquals(p,Player1)?Player2:Player1;
                                if(p.QualifierGroup!=null || p.TeamQualifierGroup!=null || p.TeamArenaStatistic==null
                                    || (p.TeamArenaStatistic.PlayWith!=null && !ReferenceEquals(p.TeamArenaStatistic.PlayWith,opponent)))continue;
                                if(!Features.QuestInventory.Player(p,false))return false;
                            }
                            foreach(uint key in new[]{p.Entity.UID,p.Account==null?0u:p.Account.EntityID}.Distinct()) {
                                var entry=new KeyValuePair<uint,GameState>(key,p);
                                ((ICollection<KeyValuePair<uint,GameState>>)PlayerList).Remove(entry);
                                ((ICollection<KeyValuePair<uint,GameState>>)WaitingPlayerList).Remove(entry);
                            }
                            if(original) {
                                p.TeamArenaStatistic.AcceptBox=p.TeamArenaStatistic.HasBox=false;p.TeamArenaStatistic.PlayWith=null;
                                p.TeamArenaStatistic.Status=TeamQualifierStatistic.NotSignedUp;p.TeamArenaStatistic.Send(p);
                            }
                        }
                        ((ICollection<KeyValuePair<uint,QualifierGroup>>)Groups).Remove(new KeyValuePair<uint,QualifierGroup>(ID,this));
                        exported=true;return true;
                    }finally{for(int n=acquired-1;n>=0;n--)Monitor.Exit(owners[n].ItemSyncRoot);Monitor.Exit(outcomeSync);}
                }

                internal bool RecordDisconnect(GameState player)
                {
                    // Never acquire the match/actor locks under this short departure gate.
                    lock(DepartureSync)
                    {
                        QualifierGroup registered;
                        if(!ResultAdmissionReady || AdmissionInterrupted || !Inside || !ResultMapRegistered
                            || !Groups.TryGetValue(ID,out registered) || !ReferenceEquals(registered,this)
                            || !Features.QuestInventory.Player(player,false) || !ReferenceEquals(player.TeamQualifierGroup,this))return false;
                        bool first=firstParticipants.Contains(player);
                        var side=first?firstParticipants:secondParticipants;
                        if(!side.Contains(player))return false;
                        var observed=Features.ArenaMatchLedger.Clock();
                        if(observed.Kind!=DateTimeKind.Utc)throw new InvalidOperationException("Team disconnect requires UTC.");
                        departedParticipants.Add(player);
                        var leader=first?Player1:Player2;
                        if(!Done && PendingDeparture==null
                            && !side.Any(p=>!departedParticipants.Contains(p) && Features.QuestInventory.Player(p,false)
                                && ReferenceEquals(p.TeamQualifierGroup,this) && ReferenceEquals(p.Map,dynamicMap) && !p.Entity.Dead))
                            Volatile.Write(ref PendingDeparture,new Features.TeamArenaResultCoordinator.Departure(leader,observed));
                        return true;
                    }
                }

                // Returning a member does not release its admitted result ownership.
                internal bool WithdrawMember(GameState player)
                {
                    if (!Monitor.TryEnter(outcomeSync)) return false;
                    try
                    {
                        if (!Monitor.TryEnter(player.ItemSyncRoot)) return false;
                        try
                        {
                            if (withdrawnMembers.Contains(player)) return true;
                            if (CompletionOwner == null || ReferenceEquals(player, Player1) || ReferenceEquals(player, Player2)
                                || !RecordDisconnect(player)) return false;
                            if (ReferenceEquals(player.Map, dynamicMap))
                            {
                                player.Entity.PreviousTeleport();
                                if (ReferenceEquals(player.Map, dynamicMap)) player.Entity.Teleport(1002, 400, 400);
                                if (ReferenceEquals(player.Map, dynamicMap)) return false;
                            }
                            RestoreFighter(player);
                            withdrawnMembers.Add(player);
                            return true;
                        }
                        finally { Monitor.Exit(player.ItemSyncRoot); }
                    }
                    finally { Monitor.Exit(outcomeSync); }
                }

                private static void RestoreFighter(GameState player)
                {
                    player.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.OrangeSparkles);
                    player.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.PurpleSparkles);
                    player.Entity.Ressurect();
                    player.Entity.PKMode = player.PrevPK;
                    player.Send(new Data(true) { UID = player.Entity.UID, ID = Data.ChangePKMode, dwParam = (uint)player.Entity.PKMode });
                    player.Entity.ToxicFogLeft = 0;
                    if (!Features.HealingMedicine.TryResetNoDrugs(player.Entity, 1) && !Features.HealingMedicine.Enabled)
                        player.Entity.NoDrugsTime = 1;
                    player.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.Poisoned);
                }

                public void UpdateDamage(GameState client, uint damage, bool otherParty = false)
                {
                    byte[] packet;
                    GameState[] recipients;
                    lock (outcomeSync)
                    lock (DepartureSync)
                    {
                        if (!admissionReady || departedParticipants.Contains(client) || PendingDeparture != null || Done || Finished || !Inside || !Features.QuestInventory.Player(client, false)
                            || !ReferenceEquals(client.TeamQualifierGroup, this) || !ReferenceEquals(client.Map, dynamicMap)) return;
                        bool first = firstParticipants.Contains(client);
                        if (!first && !secondParticipants.Contains(client)) return;
                        if (otherParty) first = !first;
                        if (first) Player1Damage = (uint)Math.Min(uint.MaxValue, (ulong)Player1Damage + damage);
                        else Player2Damage = (uint)Math.Min(uint.MaxValue, (ulong)Player2Damage + damage);
                        packet = MatchPacket.BuildPacket();
                        recipients = firstParticipants.Concat(secondParticipants).ToArray();
                    }
                    foreach (var player in recipients)
                        if (Features.QuestInventory.Player(player, false) && ReferenceEquals(player.TeamQualifierGroup, this)) player.Send(packet);
                }

            }
        }

        public class QualifyEngine
        {
            public static void RequestGroupList(Client.GameState client, ushort page)
            {
                client.Send(QualifierList.BuildPacket(page));
            }

            public static void DoQuit(Client.GameState client)
            {
                var owned=client==null?null:client.TeamQualifierGroup;
                if(owned!=null && owned.CompletionOwner!=null && (ReferenceEquals(client,owned.Player1) || ReferenceEquals(client,owned.Player2))) {
                    Features.TeamArenaResultCoordinator.EndForPlayer(owned,client);return;
                }
                if(Features.ArenaStartupPreparation.Prepared){SignupWithdrawals.Request(client);return;}
                PlayerList.Remove(client.Entity.UID);
                RequestGroupList(client, 1);

                if (client.TeamQualifierGroup != null)
                {
                    if (client.Team.TeamLeader)
                    {
                        if (client.TeamQualifierGroup.Inside)
                        {
                            client.TeamQualifierGroup.End(client);
                            client.TeamQualifierGroup.CreateTime = Time32.Now;
                        }
                        else
                        {
                            Win(client.TeamArenaStatistic.PlayWith, client);
                        }
                    }
                }
                else
                {
                    ArenaSignup sign = new ArenaSignup();
                    sign.DialogID = ArenaSignup.MainIDs.OpponentGaveUp;
                    if (client.TeamArenaStatistic.PlayWith != null)
                    {
                        Client.GameState other = client.TeamArenaStatistic.PlayWith;
                        client.TeamArenaStatistic.PlayWith.Send(sign.BuildPacket());
                        if (WaitingPlayerList.ContainsKey(client.Entity.UID))
                        {
                            WaitingPlayerList.Remove(client.Entity.UID);
                            WaitingPlayerList.Remove(client.TeamArenaStatistic.PlayWith.Entity.UID);
                        }
                        Win(client.TeamArenaStatistic.PlayWith, client);
                        Clear(other);
                    }
                    Clear(client);
                }
            }

            public static void DoSignup(Client.GameState client)
            {
                using (var admission = Features.ArenaShutdownDrain.Enter())
                {
                    if (admission == null) return;
                    DoSignupCore(client);
                }
            }
            private static void DoSignupCore(Client.GameState client)
            {
                if (Features.ArenaShutdownDrain.Stopping) return;
                if(Features.ArenaStartupPreparation.Prepared && SignupWithdrawals.Pending(client))return;
                if(Features.ArenaStartupPreparation.Prepared && !Features.TeamArenaDayRollover.Prepare(client))return;
                if (Revora.Game.Features.LocalPkWar.InEvent(client.Entity) || Revora.Game.Features.CouplePkEvent.InSession(client)) return;
                if (client.ArenaStatistic.Status != QualifierStatistic.NotSignedUp || client.TeamArenaStatistic.Status != TeamQualifierStatistic.NotSignedUp)
                {
                    client.Send(new Message("You already joined a qualifier arena! Quit the other one and sign up again.", Color.Beige, Message.Agate));
                    return;
                }
                if (client.InQualifier())
                {
                    return;
                }
                if (client.Team == null)
                    GeneralData.CreateTeam(new Network.GamePackets.Team() { UID = client.Entity.UID, Type = Network.GamePackets.Team.Create }, client);
                if (client.Team == null)
                    return;
                client.TeamArenaStatistic.HasBox = false;
                if (client.TeamArenaStatistic.ArenaPoints == 0)
                    return;
                if (client.Entity.MapID == 601) return;
                if (client.Map.BaseID >= 6000 && client.Map.BaseID <= 6003) return;

                if (WaitingPlayerList.ContainsKey(client.Entity.UID))
                {
                    if (client.TeamQualifierGroup == null)
                        WaitingPlayerList.Remove(client.Entity.UID);
                    else
                        return;
                }
                PlayerList.Add(client.Entity.UID, client);
                client.TeamArenaStatistic.Status = Network.GamePackets.TeamQualifierStatistic.WaitingForOpponent;
                client.Send(client.TeamArenaStatistic);
                RequestGroupList(client, 1);
            }
            public static void DoGiveUp(Client.GameState client)
            {
                var owned=client==null?null:client.TeamQualifierGroup;
                if(owned!=null && owned.CompletionOwner!=null && (ReferenceEquals(client,owned.Player1) || ReferenceEquals(client,owned.Player2))) {
                    Features.TeamArenaResultCoordinator.EndForPlayer(owned,client);return;
                }
                if(Features.ArenaStartupPreparation.Prepared){SignupWithdrawals.Request(client);return;}
                client.TeamArenaStatistic.Status = Network.GamePackets.TeamQualifierStatistic.WaitingInactive;
                client.Send(client.TeamArenaStatistic);
                RequestGroupList(client, 1);
                ArenaSignup sign = new ArenaSignup();
                sign.DialogID = ArenaSignup.MainIDs.OpponentGaveUp;

                if (client.TeamArenaStatistic.PlayWith != null)
                {
                    client.TeamArenaStatistic.PlayWith.Send(sign.BuildPacket());

                    client.TeamArenaStatistic.PlayWith.TeamArenaStatistic.Status = Network.GamePackets.TeamQualifierStatistic.NotSignedUp;
                    client.TeamArenaStatistic.Status = Network.GamePackets.TeamQualifierStatistic.NotSignedUp;
                    client.TeamArenaStatistic.PlayWith.Send(client.TeamArenaStatistic.PlayWith.TeamArenaStatistic);
                    client.Send(client.TeamArenaStatistic);

                    if (WaitingPlayerList.ContainsKey(client.Entity.UID))
                    {
                        WaitingPlayerList.Remove(client.Entity.UID);
                        WaitingPlayerList.Remove(client.TeamArenaStatistic.PlayWith.Entity.UID);
                    }
                    if (client.Team.TeamLeader)
                    {
                        if (client.TeamQualifierGroup != null)
                        {
                            if (client.TeamQualifierGroup.Inside)
                            {
                                client.TeamQualifierGroup.End(client);
                            }
                            else
                            {
                                Win(client.TeamArenaStatistic.PlayWith, client);
                            }
                        }
                        else
                        {
                            Win(client.TeamArenaStatistic.PlayWith, client);
                        }
                    }
                    else
                    {
                        client.Team.Remove(client);
                    }
                }
            }

            public static void DoAccept(Client.GameState client)
            {
                if (!client.TeamArenaStatistic.HasBox)
                    return;
                if (!client.TeamArenaStatistic.AcceptBox)
                {
                    if (client.TeamArenaStatistic.ArenaPoints == 0)
                        return;

                    if (client.TeamArenaStatistic.PlayWith != null)
                    {
                        client.TeamArenaStatistic.AcceptBox = true;
                    }
                }
            }

            public static void DoLeave(Client.GameState client)
            {
                var group=client==null?null:client.TeamWatchingGroup;
                if(group!=null)group.LeaveWatching(client);
            }

            public static void DoCheer(Client.GameState client, uint uid)
            {
                var group=client==null?null:client.TeamWatchingGroup;
                if(group!=null)group.Cheer(client,uid);
            }
        }

        public class Statistics
        {
            public static void ShowWiners(Client.GameState client)
            {
                MemoryStream strm = new MemoryStream();
                BinaryWriter wtr = new BinaryWriter(strm);
                int MyCount = 0;
                wtr.Write((ushort)0);
                wtr.Write((ushort)2244);
                wtr.Write((uint)Game.Enums.ArenaIDs.QualifierList);
                foreach (TeamQualifierStatistic entry in YesterdayArenaStatisticsList)
                {
                    MyCount++;
                    byte[] array = Encoding.Default.GetBytes(entry.Name);
                    for (int i = 0; i < 16; i++)
                    {
                        if (i < entry.Name.Length)
                        {
                            wtr.Write(array[i]);
                        }
                        else
                            wtr.Write((byte)0);
                    }
                    wtr.Write((uint)entry.LastSeasonRank);
                    wtr.Write((uint)entry.Model);
                    wtr.Write((uint)entry.Class);
                    wtr.Write((uint)entry.Level);
                    wtr.Write((uint)entry.LastSeasonArenaPoints);
                    wtr.Write((uint)entry.LastSeasonWin);
                    wtr.Write((uint)entry.LastSeasonLose);
                    if (MyCount == 11)
                        break;
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

            public static void ShowRankingPage(ushort thisSeason, int pageIndex, Client.GameState client)
            {
                ArenaList list = new ArenaList(((pageIndex - 1) * 10) + 1);
                list.ID = Game.Enums.ArenaIDs.ShowPlayerRankList;
                list.PageNumber = (ushort)(((pageIndex - 1) * 10) + 1);
                list.Subtype = thisSeason;
                // if (list.Subtype == 1)
                {
                    var Array = ArenaStatisticsList;
                    if (Array.Count > pageIndex * 10)
                    {
                        list.Players.Clear();
                        for (int i = (pageIndex) * 10 - 10; i < (pageIndex) * 10; i++)
                        {
                            if (i < Array.Count)
                            {
                                if (Array[i].Rank > 0)
                                {
                                    list.Players.Add(Array[i]);
                                }
                            }
                        }
                    }
                }
                //else
                //{
                //    var Array = YesterdayArenaStatisticsList;
                //    if (Array.Count > (((pageIndex) * 10) + 10))
                //    {
                //        list.Players.Clear();
                //        for (int i = ((pageIndex) * 10); i < ((pageIndex) * 10) + 10; i++)
                //        {
                //            if (i < Array.Count)
                //            {
                //                if (Array[i].LastSeasonRank > 0)
                //                {
                //                    list.Players.Add(Array[i]);
                //                }
                //            }
                //        }
                //    }
                //}
                client.Send(list.BuildPacket());
            }
        }

        public class ArenaList
        {
            public ushort Size;
            public ushort Type;
            public ushort Subtype;
            public ushort PageNumber;
            public Game.Enums.ArenaIDs ID;
            public List<TeamQualifierStatistic> Players = new List<TeamQualifierStatistic>();
            public ArenaList(int PageIndex, ushort type = 2243)
            {
                Type = type;
                PageNumber = (ushort)PageIndex;
            }
            public ArenaList(byte[] Packet)
            {
                BinaryReader rdr = new BinaryReader(new MemoryStream(Packet));
                Size = rdr.ReadUInt16();
                Type = rdr.ReadUInt16();
                Subtype = rdr.ReadUInt16();
                PageNumber = rdr.ReadUInt16();
            }
            public ArenaList()
            {
                Type = 2243;
            }
            public byte[] BuildPacket()
            {
                MemoryStream strm = new MemoryStream();
                BinaryWriter wtr = new BinaryWriter(strm);

                wtr.Write((ushort)0);
                wtr.Write((ushort)Type);
                wtr.Write((ushort)Subtype);
                wtr.Write((ushort)InArenaCount);
                wtr.Write((uint)ID);
                foreach (TeamQualifierStatistic entry in Players)
                {


                    wtr.Write((uint)entry.Rank);
                    wtr.Write((uint)entry.HistoryHonor);
                    wtr.Write((uint)entry.ArenaPoints);
                    wtr.Write((uint)entry.Class);
                    wtr.Write((uint)entry.Level);
                    wtr.Write((byte)1);
                    for (int i = 0; i < 16; i++)
                    {
                        if (i < entry.Name.Length)
                        {
                            wtr.Write((byte)entry.Name[i]);
                        }
                        else
                            wtr.Write((byte)0);
                    }
                    wtr.Write((byte)0);
                    wtr.Write((ushort)0);

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
                return buf;
            }
        }

        public class ArenaSignup
        {
            public abstract class MainIDs
            {
                public const uint ArenaIconOn = 0,
                                    ArenaIconOff = 1,
                                    StartCountDown = 2,
                                    OpponentGaveUp = 4,
                                    Match = 6,
                                    YouAreKicked = 7,
                                    StartTheFight = 8,
                                    Dialog = 9,
                                    Dialog2 = 10;
            }
            public abstract class DialogButton
            {
                public const uint Lose = 3,
                                    Win = 1,
                                    MatchOff = 3,
                                    MatchOn = 5;
            }

            public ushort Type = 2241;
            public uint DialogID;
            public uint OptionID;
            public TeamQualifierStatistic Stats;
            public byte[] BuildPacket()
            {
                MemoryStream strm = new MemoryStream();
                BinaryWriter wtr = new BinaryWriter(strm);
                if (Stats == null)
                {
                    Stats = new TeamQualifierStatistic(true);
                    Stats.Name = "";
                }
                wtr.Write((ushort)0);
                wtr.Write((ushort)Type);
                wtr.Write((uint)DialogID);
                wtr.Write((uint)OptionID);
                wtr.Write((uint)Stats.EntityID);
                byte[] array = Encoding.Default.GetBytes(Stats.Name);
                for (int i = 0; i < 20; i++)
                {
                    if (i < Stats.Name.Length)
                    {
                        wtr.Write(array[i]);
                    }
                    else
                        wtr.Write((byte)0);
                }
                wtr.Write((uint)Stats.Class);
                wtr.Write((uint)Stats.Rank);
                wtr.Write((uint)Stats.ArenaPoints);
                wtr.Write((uint)Stats.Level);
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
                return buf;
            }
        }

        public class GroupMatch
        {
            public ushort Type = 2246;
            public QualifierList.QualifierGroup Group;
            public byte[] BuildPacket()
            {
                MemoryStream strm = new MemoryStream();
                BinaryWriter wtr = new BinaryWriter(strm);

                wtr.Write((ushort)64);
                wtr.Write((ushort)Type);
                wtr.Write((uint)Group.Player1.TeamArenaStatistic.EntityID);
                wtr.Write((uint)Group.Player1.TeamArenaStatistic.Rank);
                byte[] array = Encoding.Default.GetBytes(Group.Player1.TeamArenaStatistic.Name);
                for (int i = 0; i < 16; i++)
                {
                    if (i < Group.Player1.TeamArenaStatistic.Name.Length)
                    {
                        wtr.Write(array[i]);
                    }
                    else
                        wtr.Write((byte)0);
                }
                wtr.Write((uint)Group.Player1Damage);
                wtr.Write((uint)Group.Player2.TeamArenaStatistic.EntityID);
                wtr.Write((uint)Group.Player2.TeamArenaStatistic.Rank);
                byte[] array2 = Encoding.Default.GetBytes(Group.Player2.TeamArenaStatistic.Name);
                for (int i = 0; i < 16; i++)
                {
                    if (i < Group.Player2.TeamArenaStatistic.Name.Length)
                    {
                        wtr.Write(array2[i]);
                    }
                    else
                        wtr.Write((byte)0);
                }
                wtr.Write((uint)Group.Player2Damage);
                wtr.Write((uint)1);
                wtr.Write(Encoding.Default.GetBytes("TQServer"));
                strm.Position = 0;
                byte[] buf = new byte[strm.Length];
                strm.Read(buf, 0, buf.Length);
                wtr.Close();
                strm.Close();
                return buf;
            }
        }

        // Legacy result projection only. Durable team result/replay migration remains separate.
        internal static void ApplyLegacyResult(Client.GameState player, bool won, uint delta)
        {
            lock (player.ItemSyncRoot)
            {
                uint before = player.ArenaPoints;
                player.ArenaPoints = won
                    ? (uint)Math.Min((ulong)uint.MaxValue, (ulong)before + delta)
                    : before - Math.Min(before, delta);
                var stats = player.TeamArenaStatistic;
                if (stats.TodayBattles < uint.MaxValue) stats.TodayBattles++;
                if (won)
                {
                    if (stats.TodayWin < uint.MaxValue) stats.TodayWin++;
                    if (stats.TotalWin < uint.MaxValue) stats.TotalWin++;
                }
                else if (stats.TotalLose < uint.MaxValue) stats.TotalLose++;
            }
        }

        public static void Win(Client.GameState winner, Client.GameState loser, QualifierList.QualifierGroup match = null)
        {
            if (winner == null || loser == null) return;
            match = match ?? winner.TeamQualifierGroup ?? loser.TeamQualifierGroup;
            if(match!=null && match.CompletionOwner!=null) {
                if((ReferenceEquals(loser,match.Player1) && ReferenceEquals(winner,match.Player2))
                    || (ReferenceEquals(loser,match.Player2) && ReferenceEquals(winner,match.Player1)))
                    Features.TeamArenaResultCoordinator.EndForPlayer(match,loser);
                return;
            }
            if(Features.ArenaStartupPreparation.Prepared){RejectInvitation(winner,loser);return;}
            if (!Features.QuestInventory.Player(winner, false) || !Features.QuestInventory.Player(loser, false)
                || winner.QualifierGroup != null || loser.QualifierGroup != null
                || (winner.TeamQualifierGroup != null && !ReferenceEquals(winner.TeamQualifierGroup, match))
                || (loser.TeamQualifierGroup != null && !ReferenceEquals(loser.TeamQualifierGroup, match))) return;
            var winning = match == null ? new GameState[0] : match.RewardParticipants(winner);
            var losing = match == null ? new GameState[0] : match.RewardParticipants(loser);
            if (ReferenceEquals(winner.TeamArenaStatistic.PlayWith, loser) && ReferenceEquals(loser.TeamArenaStatistic.PlayWith, winner))
            {
                winner.TeamArenaStatistic.HasBox =
                    loser.TeamArenaStatistic.HasBox = false;

                foreach (var teammate in winning)
                {
                    if (!match.ApplyOwnedLegacyResult(teammate, true)) continue;
                    teammate.Send(teammate.TeamArenaStatistic);
                }
                foreach (var teammate in losing)
                {
                    if (!match.ApplyOwnedLegacyResult(teammate, false)) continue;
                    teammate.Send(teammate.TeamArenaStatistic);
                }
                if (ReferenceEquals(winner.TeamQualifierGroup, match)) winner.TeamQualifierGroup = null;
                if (ReferenceEquals(loser.TeamQualifierGroup, match)) loser.TeamQualifierGroup = null;

                winner.TeamArenaStatistic.PlayWith = null;
                loser.TeamArenaStatistic.PlayWith = null;

                if (WaitingPlayerList.ContainsKey(loser.Entity.UID))
                    WaitingPlayerList.Remove(loser.Entity.UID);
                if (WaitingPlayerList.ContainsKey(winner.Entity.UID))
                    WaitingPlayerList.Remove(winner.Entity.UID);

                QualifyEngine.DoQuit(winner);
                QualifyEngine.DoQuit(loser);

                winner.TeamArenaStatistic.AcceptBox = false;
                loser.TeamArenaStatistic.AcceptBox = false;

                Sort();

                StringBuilder builder = new StringBuilder();
                if (winner.Entity.GuildID != 0)
                {
                    builder.Append("(");
                    builder.Append(winner.Guild.Name);
                    builder.Append(") ");
                }
                builder.Append(winner.Entity.Name);
                builder.Append(" has defeated ");

                if (loser.Entity.GuildID != 0)
                {
                    builder.Append("(");
                    builder.Append(loser.Guild.Name);
                    builder.Append(") ");
                }
                builder.Append(loser.Entity.Name);
                if (winner.TeamArenaStatistic.Rank > 0)
                {
                    builder.Append(" in the Qualifier, and is currently ranked No. "); builder.Append(winner.TeamArenaStatistic.Rank);
                }
                else { builder.Append(" in the Qualifier"); }

                builder.Append(".");

                Kernel.SendWorldMessage(new Message(builder.ToString(), System.Drawing.Color.Red, Message.ArenaQualifier), Server.Values);

                winner.TeamQualifierGroup = null;
                loser.TeamQualifierGroup = null;
                foreach (var teammate in losing)
                {
                    ArenaSignup sign = new ArenaSignup();
                    sign.Stats = teammate.TeamArenaStatistic;
                    sign.DialogID = ArenaSignup.MainIDs.Dialog2;
                    teammate.Send(sign.BuildPacket());
                }
                foreach (var teammate in winning)
                {
                    ArenaSignup sign = new ArenaSignup();
                    sign.Stats = teammate.TeamArenaStatistic;
                    sign.DialogID = ArenaSignup.MainIDs.Dialog2;
                    sign.OptionID = ArenaSignup.DialogButton.Win;
                    teammate.Send(sign.BuildPacket());
                }
                loser.TeamArenaStatistic.Status = TeamQualifierStatistic.NotSignedUp;
                loser.Send(loser.TeamArenaStatistic);

                winner.TeamArenaStatistic.Status = TeamQualifierStatistic.NotSignedUp;
                winner.Send(winner.TeamArenaStatistic);
            }
        }

        private static DateTime YesterdaySorted = DateTime.Now;

        public static void Clear(Client.GameState client)
        {
            if (client == null || client.TeamArenaStatistic == null) return;
            var teamarena_stat = client.TeamArenaStatistic;
            teamarena_stat.HasBox = false;
            teamarena_stat.AcceptBox = false;
            teamarena_stat.AcceptBoxShow = Time32.Now;
            teamarena_stat.PlayWith = null;
            teamarena_stat.Status = TeamQualifierStatistic.NotSignedUp;
            client.Send(teamarena_stat);
            client.QualifierGroup = null;
            client.TeamQualifierGroup = null;
            Game.TeamQualifier.WaitingPlayerList.Remove(teamarena_stat.EntityID);
            Game.TeamQualifier.PlayerList.Remove(teamarena_stat.EntityID);
        }

        public static void EngagePlayers()
        {
            using (var admission = Features.ArenaShutdownDrain.Enter())
            {
                if (admission == null) return;
                EngagePlayersCore();
            }
        }
        private static void EngagePlayersCore()
        {
            if (PlayerList.Count < 2 || !AcceptingNewBattles)
                return;
            var Players = new GameState[PlayerList.Count];
            PlayerList.Values.CopyTo(Players, 0);
            int i, j;
            int iPlus, jPlus;
            int iEnd, jEnd;
            iPlus = Kernel.Random.Next(2);
            jPlus = Kernel.Random.Next(2);
            if (iPlus == 0) { i = 0; iPlus = 1; iEnd = Players.Length; } else { i = Players.Length - 1; iPlus = -1; iEnd = -1; }
            if (jPlus == 0) { j = 0; jPlus = 1; jEnd = Players.Length; } else { j = Players.Length - 1; jPlus = -1; jEnd = -1; }
            Time32 Now = Time32.Now;
            for (; i != iEnd; i += iPlus)
            {
                var Challanger = Players[i];

                if (CanJoin(Challanger, Now))
                {
                    if (Challanger.Team == null) continue;
                    if (Challanger.Team.Teammates == null) continue;
                    for (; j != jEnd; j += jPlus)
                    {
                        var Challanged = Players[j];
                        if (CanJoin(Challanged, Now))
                        {
                            if (Challanged.Team == null) continue;
                            if (Challanged.Team.Teammates == null) continue;
                            if (Challanger.Entity.UID != Challanged.Entity.UID)
                            {
                                Challanged.TeamArenaStatistic.AcceptBoxShow = Challanger.TeamArenaStatistic.AcceptBoxShow = Time32.Now;
                                Challanged.TeamArenaStatistic.Status = Network.GamePackets.TeamQualifierStatistic.WaitingInactive;
                                Challanger.TeamArenaStatistic.Status = Network.GamePackets.TeamQualifierStatistic.WaitingInactive;
                                Challanged.Send(Challanged.TeamArenaStatistic);
                                Challanger.Send(Challanger.TeamArenaStatistic);
                                Challanged.TeamArenaStatistic.PlayWith = Challanger;
                                Challanger.TeamArenaStatistic.PlayWith = Challanged;
                                ArenaSignup sign = new ArenaSignup();
                                sign.DialogID = ArenaSignup.MainIDs.StartCountDown;
                                sign.Stats = Challanged.TeamArenaStatistic;
                                Challanged.Send(sign.BuildPacket());
                                sign.Stats = Challanger.TeamArenaStatistic;
                                Challanger.Send(sign.BuildPacket());
                                Challanger.TeamArenaStatistic.HasBox =
                                Challanged.TeamArenaStatistic.HasBox = true;
                                PlayerList.Remove(Challanged.Entity.UID);
                                PlayerList.Remove(Challanger.Entity.UID);
                                WaitingPlayerList.Add(Challanged.Entity.UID, Challanged);
                                WaitingPlayerList.Add(Challanger.Entity.UID, Challanger);
                                break;
                            }
                        }
                    }
                }
            }
        }
        public static void VerifyAwaitingPeople()
        {
            using (var admission = Features.ArenaShutdownDrain.Enter())
            {
                if (admission == null) return;
                VerifyAwaitingPeopleCore();
            }
        }
        private static void VerifyAwaitingPeopleCore()
        {
            foreach (var Challanger in WaitingPlayerList.Values)
            {
                if(Features.ArenaStartupPreparation.Prepared && SignupWithdrawals.Pending(Challanger))continue;
                if (Challanger.Team == null) continue;
                if (Challanger.Team.Teammates == null) continue;
                if (Challanger != null)
                {
                    if (Challanger.TeamArenaStatistic == null)
                    {
                        WaitingPlayerList.Remove(Challanger.Entity.UID);
                        continue;
                    }
                    if (Challanger.TeamArenaStatistic.HasBox)
                    {
                        if (Challanger.TeamArenaStatistic.PlayWith != null && Challanger.TeamQualifierGroup == null)
                        {
                            Client.GameState Challanged = Challanger.TeamArenaStatistic.PlayWith;
                        if(Features.ArenaStartupPreparation.Prepared && SignupWithdrawals.Pending(Challanged))continue;
                            if (Challanged != null)
                            {
                                if (Challanged.Team == null) continue;
                                if (Challanged.Team.Teammates == null) continue;
                                if (Challanged.TeamArenaStatistic.PlayWith != null && Challanged.TeamQualifierGroup == null)
                                {
                                    if (Challanger.TeamArenaStatistic.AcceptBox && Challanged.TeamArenaStatistic.AcceptBox)
                                    {
                                        if (Challanged.Entity.MapID == 1081 || Challanged.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.SoulShackle))
                                        {
                                            Qualifier.QualifyEngine.DoQuit(Challanged);
                                            continue;
                                        }
                                        if (Challanger.Entity.MapID == 1081 || Challanger.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.SoulShackle))
                                        {
                                            Qualifier.QualifyEngine.DoQuit(Challanger);
                                            continue;
                                        }
                                        QualifierList.QualifierGroup group = new QualifierList.QualifierGroup(Challanger, Challanged);
                                        ImportMatched(group,ownedEntries);
                                        Challanger.TeamArenaStatistic.HasBox =
                                            Challanged.TeamArenaStatistic.HasBox = false;
                                    }
                                    else if (Time32.Now > Challanger.TeamArenaStatistic.AcceptBoxShow.AddSeconds(59))
                                    {
                                        if (Challanger.TeamArenaStatistic.AcceptBox == true)
                                        {
                                            Win(Challanger, Challanged);
                                        }
                                        else
                                        {
                                            if (Challanged.TeamArenaStatistic.AcceptBox)
                                            {
                                                Win(Challanged, Challanger);
                                            }
                                            else
                                            {
                                                if (Challanger.ArenaPoints > Challanged.ArenaPoints)
                                                {
                                                    Win(Challanger, Challanged);
                                                }
                                                else
                                                {
                                                    Win(Challanged, Challanger);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                Challanger.TeamArenaStatistic.HasBox = false;
                            }
                        }
                    }
                }
            }
        }
        public static void CheckGroups()
        {
            SignupWithdrawals.Tick();
            Features.TeamArenaDisconnectRetention.TickScheduled();
            if (QualifierList.Groups.Count > 0)
            {
                foreach (var group in QualifierList.Groups.Values.ToArray())
                {
                    if(group.CompletionOwner!=null)continue;
                    if(group.OwnedEntryRequested){group.RejectUnimported();continue;}
                    if (Time32.Now > group.CreateTime.AddSeconds(5))
                    {
                        if (group.Inside)
                        {
                            if (!group.Done)
                            {
                                if (group.HasTimedOut(Time32.Now))
                                {
                                    group.End();
                                }
                            }
                            else
                            {
                                if (group.ReturnReady(Features.TeamArenaResultCoordinator.ReturnClock()))
                                {
                                    group.Export();
                                }
                            }
                        }
                    }
                }
            }
        }
        public static void PickUpTeams()
        {
            using (var admission = Features.ArenaShutdownDrain.Enter())
            {
                if (admission == null) return;
                PickUpTeamsCore();
            }
        }
        private static void PickUpTeamsCore()
        {
            if (PlayerList.Count >= 2)
            {
                int num;
                int num2;
                int length;
                int num4;
                GameState[] array = new GameState[PlayerList.Count];
                PlayerList.Values.CopyTo(array, 0);
                int num5 = Kernel.Random.Next(2);
                int num6 = Kernel.Random.Next(2);
                if (num5 == 0)
                {
                    num = 0;
                    num5 = 1;
                    length = array.Length;
                }
                else
                {
                    num = array.Length - 1;
                    num5 = -1;
                    length = -1;
                }
                if (num6 == 0)
                {
                    num2 = 0;
                    num6 = 1;
                    num4 = array.Length;
                }
                else
                {
                    num2 = array.Length - 1;
                    num6 = -1;
                    num4 = -1;
                }
                Time32 now = Time32.Now;
                while (num != length)
                {
                    GameState client = array[num];
                    if (CanJoin(client, now))
                    {
                        while (num2 != num4)
                        {
                            GameState OpponentTeam = array[num2];
                            if (CanJoin(OpponentTeam, now))
                            {
                                OpponentTeam.TeamArenaStatistic.AcceptBoxShow = client.TeamArenaStatistic.AcceptBoxShow = Time32.Now;
                                OpponentTeam.TeamArenaStatistic.Status = 2;
                                client.TeamArenaStatistic.Status = 2;
                                OpponentTeam.Send(OpponentTeam.TeamArenaStatistic);
                                client.Send(client.TeamArenaStatistic);
                                OpponentTeam.TeamArenaStatistic.PlayWith = client;
                                client.TeamArenaStatistic.PlayWith = OpponentTeam;
                                ArenaSignup signup = new ArenaSignup
                                {
                                    DialogID = 2,
                                    Stats = OpponentTeam.TeamArenaStatistic
                                };
                                OpponentTeam.Send(signup.BuildPacket());
                                signup.Stats = client.TeamArenaStatistic;
                                client.Send(signup.BuildPacket());
                                #region Sending Accept Box To Opponent's Team Teammates
                                foreach (var OpponentTeamMember in OpponentTeam.Team.Teammates)
                                {
                                    if (OpponentTeamMember.Entity.UID != OpponentTeam.Entity.UID)
                                    {
                                        OpponentTeamMember.Send(signup.BuildPacket());
                                    }
                                }
                                #endregion
                                #region Sending Accept Box to Client's Team Teammates
                                foreach (var TeamMember in client.Team.Teammates)
                                {
                                    if (TeamMember.Entity.UID != OpponentTeam.Entity.UID)
                                    {
                                        TeamMember.Send(signup.BuildPacket());
                                    }
                                }
                                #endregion
                                client.TeamArenaStatistic.HasBox = OpponentTeam.TeamArenaStatistic.HasBox = true;
                                PlayerList.Remove(OpponentTeam.Entity.UID);
                                PlayerList.Remove(client.Entity.UID);
                                WaitingPlayerList.Add(OpponentTeam.Entity.UID, OpponentTeam);
                                WaitingPlayerList.Add(client.Entity.UID, client);
                            }
                            num2 += num6;
                        }
                    }
                    num += num5;
                }
            }
        }

        private static bool DayPassed = false;
        private static DateTime StartDateTime = DateTime.Now;
        public static bool AcceptingNewBattles;
        public static void Reset()
        {
            DateTime Now64 = DateTime.Now;
            AcceptingNewBattles = ((Now64.Hour >= 11 && Now64.Hour < 13) || (Now64.Hour >= 19 && Now64.Hour < 21));
            if (!AcceptingNewBattles && WaitingPlayerList.Count == 0 && PlayerList.Count != 0)
            {
                foreach (var player in PlayerList.Values)
                {
                    player.Send("Team Qualifier has ended!");
                    Clear(player);
                }
            }

            if (!DayPassed)
            {
                if (Now64.DayOfYear != StartDateTime.DayOfYear)
                {
                    DayPassed = true;
                    var array = ArenaStatistics.Values.ToArray();
                    foreach (TeamQualifierStatistic stat in array)
                    {
                        try
                        {
                            Database.TeamArenaTable.Reset(stat);
                            if (Kernel.GamePool.ContainsKey(stat.EntityID))
                            {
                                Client.GameState client = Kernel.GamePool[stat.EntityID];
                                client.Send(stat);
                            }
                        }
                        catch { }
                    }
                    try { SaveArenaStats(); }
                    catch { }
                    YesterdaySort();
                }
            }
            else
            {
                if (Now64.DayOfYear == StartDateTime.DayOfYear)
                {
                    DayPassed = false;
                    StartDateTime = Now64;
                }
            }
        }
        private static bool CanFight(Client.GameState client)
        {
            if (Revora.Game.Features.LocalPkWar.InEvent(client.Entity) || Revora.Game.Features.CouplePkEvent.InSession(client)) return false;
            if (client.ArenaPoints <= 0) return false;
            if (client.Entity.Level < 70) return false;
            if (client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.SoulShackle)) return false;
            if (client.Map.BaseID == 1038) return false;
            if (client.Map.BaseID == 700) return false;
            if (client.Entity.MapID >= 1090 && client.Entity.MapID <= 1094) return false;
            if (client.Entity.MapID >= 1505 && client.Entity.MapID <= 1509) return false;
            if (client.Entity.MapID == 1081) return false;
            return (!Constants.PKFreeMaps.Contains(client.Map.ID) || client.Map.ID == 1005);
        }
        private static bool CanJoin(Client.GameState client, Time32 now)
        {
            if(Features.ArenaStartupPreparation.Prepared && SignupWithdrawals.Pending(client))return false;
            if (client != null)
            {
                if (client.TeamArenaStatistic.PlayWith == null)
                {
                    if (client.TeamQualifierGroup == null)
                    {
                        if (!client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.TeamLeader)) return false;
                        if (!CanFight(client)) return false;
                        if (client.TeamArenaStatistic.Status == Network.GamePackets.TeamQualifierStatistic.WaitingForOpponent)
                            return true;
                    }
                }
            }
            return false;
        }
    }
}
