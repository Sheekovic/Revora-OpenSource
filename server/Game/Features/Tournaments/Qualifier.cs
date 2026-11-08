// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.IO;
using System.Linq;
using System.Text;
using Revora.Game;
using System.Drawing;
using Revora.Client;
using System.Threading;
using System.Globalization;
using System.Collections.Generic;
using Revora.Network.GamePackets;
using System.Collections.Concurrent;

namespace Revora.Game
{
    public class Qualifier
    {
        internal static readonly Features.ArenaSignupWithdrawal SignupWithdrawals=new Features.ArenaSignupWithdrawal(false);
        private static readonly object invitationGate=new object();
        internal static void RejectInvitation(GameState a,GameState b) {
            if(a==null || b==null || ReferenceEquals(a,b) || a.ArenaStatistic==null || b.ArenaStatistic==null
                || a.QualifierGroup!=null || b.QualifierGroup!=null)return;
            QualifierList.QualifierGroup pending;
            lock(invitationGate) {
                if(!ReferenceEquals(a.ArenaStatistic.PlayWith,b) || !ReferenceEquals(b.ArenaStatistic.PlayWith,a))return;
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
        private static readonly Features.ArenaDisconnectRetention ownedEntries=new Features.ArenaDisconnectRetention(1024,null);
        internal static void ImportMatched(QualifierList.QualifierGroup group,Features.ArenaDisconnectRetention owner) {
            if(!Features.ArenaStartupPreparation.Prepared){group.Import();return;}
            group.OwnedEntryRequested=true;
            if(Features.ArenaShutdownDrain.Stopping){group.RejectUnimported();return;}
            if(SignupWithdrawals.Pending(group.Player1) || SignupWithdrawals.Pending(group.Player2)){group.RejectUnimported();return;}
            if(!owner.Import(group) && group.CompletionOwner==null)group.RejectUnimported();
        }
        public static ConcurrentDictionary<uint, GameState> PlayerList = new ConcurrentDictionary<uint, GameState>();
        public static ConcurrentDictionary<uint, GameState> WaitingPlayerList = new ConcurrentDictionary<uint, GameState>();
        public static ConcurrentDictionary<uint, QualifierStatistic> ArenaStatistics = new ConcurrentDictionary<uint, QualifierStatistic>();
        public static SafeDictionary<uint, QualifierStatistic> YesterdayArenaStatistics = new SafeDictionary<uint, QualifierStatistic>();
        public static List<QualifierStatistic> ArenaStatisticsList, YesterdayArenaStatisticsList;
        public static int InArenaCount = 0, HistoryArenaCount = 0;

        public static void Sort()
        {
            var where = ArenaStatistics.Values.Where((a) => !Features.ArenaBotStore.IsBotUid(a.EntityID) && (a.TodayWin != 0 || a.TodayBattles != 0));
            List<QualifierStatistic> stats = where.OrderByDescending((p) => p.ArenaPoints).ToList();
            InArenaCount = stats.Count;
            for (uint i = 0; i < stats.Count; i++)
                stats[(int)i].Rank = i + 1;
            ArenaStatisticsList = stats;
        }
        public static void YesterdaySort()
        {
            var where = ArenaStatistics.Values.Where((a) => !Features.ArenaBotStore.IsBotUid(a.EntityID) && (a.LastSeasonWin != 0 || a.LastSeasonLose != 0));
            List<QualifierStatistic> stats = where.OrderByDescending((p) => p.LastSeasonArenaPoints).ToList();
            HistoryArenaCount = stats.Count;
            for (uint i = 0; i < stats.Count; i++)
                stats[(int)i].LastSeasonRank = i + 1;
            YesterdayArenaStatisticsList = stats;
        }

        private static void SaveArenaStats()
        {
            var array = ArenaStatistics.Values.ToArray();

            foreach (QualifierStatistic stats in array)
                if (stats != null)
                    Database.ArenaTable.SaveArenaStatistics(stats, null);
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
                wtr.Write((ushort)2206);
                wtr.Write((uint)page);
                wtr.Write((uint)Game.Enums.ArenaIDs.QualifierList);
                wtr.Write((uint)Groups.Count);
                wtr.Write((uint)PlayerList.Count);
                wtr.Write((uint)0);
                page--;
                wtr.Write((uint)(Groups.Count - page));
                QualifierGroup[] GroupsList = Groups.Values.ToArray();
                for (int count = page; count < page + 6; count++)
                {
                    if (count >= Groups.Count)
                        break;

                    QualifierGroup entry = GroupsList[count];

                    wtr.Write((uint)0);
                    wtr.Write((uint)entry.Player1.ArenaStatistic.EntityID);
                    wtr.Write((uint)entry.Player1.ArenaStatistic.Model);
                    byte[] array = Server.Encoding.GetBytes(entry.Player1.ArenaStatistic.Name);
                    for (int i = 0; i < 16; i++)
                    {
                        if (i < entry.Player1.ArenaStatistic.Name.Length)
                        {
                            wtr.Write(array[i]);
                        }
                        else
                            wtr.Write((byte)0);
                    }

                    wtr.Write((uint)entry.Player1.ArenaStatistic.Level);
                    wtr.Write((uint)entry.Player1.ArenaStatistic.Class);
                    wtr.Write((uint)0);
                    wtr.Write((uint)entry.Player1.ArenaStatistic.Rank);
                    wtr.Write((uint)entry.Player1.ArenaPoints);
                    wtr.Write((uint)entry.Player1.ArenaStatistic.TodayWin);
                    wtr.Write((uint)(entry.Player1.ArenaStatistic.TodayBattles - entry.Player1.ArenaStatistic.TodayWin));
                    wtr.Write((uint)entry.Player1.CurrentHonor);
                    wtr.Write((uint)entry.Player1.HistoryHonor);
                    wtr.Write((uint)0);
                    wtr.Write((uint)entry.Player2.ArenaStatistic.EntityID);

                    wtr.Write((uint)entry.Player2.ArenaStatistic.Model);

                    byte[] array2 = Server.Encoding.GetBytes(entry.Player2.ArenaStatistic.Name);
                    for (int i = 0; i < 16; i++)
                    {
                        if (i < entry.Player2.ArenaStatistic.Name.Length)
                        {
                            wtr.Write(array2[i]);
                        }
                        else
                            wtr.Write((byte)0);
                    }

                    wtr.Write((uint)entry.Player2.ArenaStatistic.Level);
                    wtr.Write((uint)entry.Player2.ArenaStatistic.Class);
                    wtr.Write((uint)0);
                    wtr.Write((uint)entry.Player2.ArenaStatistic.Rank);
                    wtr.Write((uint)entry.Player2.ArenaPoints);
                    wtr.Write((uint)entry.Player2.ArenaStatistic.TodayWin);
                    wtr.Write((uint)(entry.Player2.ArenaStatistic.TodayBattles - entry.Player2.ArenaStatistic.TodayWin));
                    wtr.Write((uint)entry.Player2.CurrentHonor);
                    wtr.Write((uint)entry.Player2.HistoryHonor);
                }
                GroupsList = null;
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
                return buf;
            }

            public class QualifierGroup
            {
                #region Watchers
                public byte[] BuildWatcherList(List<Client.GameState> list, ushort id)
                {
                    MemoryStream strm = new MemoryStream();
                    BinaryWriter wtr = new BinaryWriter(strm);
                    wtr.Write((ushort)38);
                    wtr.Write((ushort)2211);
                    wtr.Write((ushort)id);
                    wtr.Write((ulong)0);
                    wtr.Write((uint)list.Count);
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
                        wtr.Write((uint)client.Entity.UID);
                        wtr.Write((uint)client.ArenaStatistic.Level);
                        wtr.Write((uint)client.ArenaStatistic.Class);
                        wtr.Write((uint)client.ArenaStatistic.Rank);
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
                    return buf;
                }
                public byte[] BuildWatcherList(List<Client.GameState> list, ushort id, uint Fighter = 0)
                {
                    var lenght = 58;
                    if (list.Count > 0)
                        lenght = 130;
                    byte[] buf = new byte[lenght + 8];

                    Network.Writer.Write((ushort)lenght, 0, buf);

                    Network.Writer.Write((ushort)2211, 2, buf);

                    Network.Writer.Write((ushort)id, 4, buf);

                    Network.Writer.Write((uint)0, 6, buf);

                    Network.Writer.Write((uint)Fighter, 10, buf);

                    Network.Writer.Write((uint)list.Count, 14, buf);

                    Network.Writer.Write((uint)Player1Cheers, 18, buf);

                    Network.Writer.Write((uint)Player2Cheers, 22, buf);
                    var offset = 26;
                    foreach (Client.GameState client in list)
                    {
                        Network.Writer.Write((uint)client.Entity.Mesh, offset, buf);
                        offset += 4;
                        Network.Writer.Write(client.ArenaStatistic.Name, offset, buf);
                        offset += 16;
                        Network.Writer.Write((uint)client.Entity.UID, offset, buf);
                        offset += 4;
                        Network.Writer.Write((uint)client.ArenaStatistic.Level, offset, buf);
                        offset += 4;
                        Network.Writer.Write((uint)client.ArenaStatistic.Class, offset, buf);
                        offset += 4;
                        Network.Writer.Write((uint)client.ArenaStatistic.Rank, offset, buf);
                        offset += 4;
                    }
                    //MemoryStream strm = new MemoryStream();
                    //BinaryWriter wtr = new BinaryWriter(strm);
                    //wtr.Write((ushort)58);
                    //wtr.Write((ushort)2211);
                    //wtr.Write((ushort)id);
                    //wtr.Write((uint)0);
                    //wtr.Write((uint)Fighter);
                    //wtr.Write((uint)list.Count);
                    //wtr.Write((uint)Player1Cheers);
                    //wtr.Write((uint)Player2Cheers);
                    //foreach (Client.GameState client in list)
                    //{
                    //    wtr.Write((uint)client.Entity.Mesh);
                    //    for (int i = 0; i < 16; i++)
                    //    {
                    //        if (i < client.ArenaStatistic.Name.Length)
                    //        {
                    //            wtr.Write((byte)client.ArenaStatistic.Name[i]);
                    //        }
                    //        else
                    //            wtr.Write((byte)0);
                    //    }
                    //    wtr.Write((uint)client.Entity.UID);
                    //    wtr.Write((uint)client.ArenaStatistic.Level);
                    //    wtr.Write((uint)client.ArenaStatistic.Class);
                    //    wtr.Write((uint)client.ArenaStatistic.Rank);
                    //}
                    //int packetlength = (int)strm.Length;
                    //strm.Position = 0;
                    //wtr.Write((ushort)packetlength);
                    //strm.Position = strm.Length;
                    //wtr.Write(Encoding.Default.GetBytes("TQServer"));
                    //strm.Position = 0;
                    //byte[] buf = new byte[strm.Length];
                    //strm.Read(buf, 0, buf.Length);
                    //wtr.Close();
                    //strm.Close();
                    return buf;
                }
                public void BeginWatching(Client.GameState client, uint Fighter)
                {
                    BeginWatchingOwned(client, Fighter);
                }
                public void BeginWatching(Client.GameState client)
                {
                    BeginWatchingOwned(client, 0);
                }
                private void BeginWatchingOwned(Client.GameState client, uint fighter)
                {
                    using (var admission = Features.ArenaShutdownDrain.Enter())
                    {
                        if (admission == null) return;
                        BeginWatchingOwnedCore(client, fighter);
                    }
                }
                private void BeginWatchingOwnedCore(Client.GameState client, uint fighter)
                {
                    if (Features.ArenaShutdownDrain.Stopping) return;
                    if (client == null || client.ItemSyncRoot == null || !Monitor.TryEnter(ParticipationSync)) return;
                    try
                    {
                        if (!Monitor.TryEnter(client.ItemSyncRoot)) return;
                        try
                        {
                            if (!StartedOnOwnedMap || CleanupCompleted || !Features.QuestInventory.Player(client)
                                || client.WatchingGroup != null || client.QualifierGroup != null
                                || client.TeamWatchingGroup != null || client.TeamQualifierGroup != null
                                || (fighter != 0 && fighter != Player1.Entity.UID && fighter != Player2.Entity.UID)) return;
                            client.WatchingGroup = this;
                            client.Entity.Teleport(1005, dynamicMap.ID, (ushort)Kernel.Random.Next(35, 70), (ushort)Kernel.Random.Next(35, 70));
                            if (!Features.QuestInventory.Player(client) || !ReferenceEquals(client.WatchingGroup, this)
                                || !ReferenceEquals(client.Map, dynamicMap))
                            {
                                if (ReferenceEquals(client.WatchingGroup, this)) client.WatchingGroup = null;
                                return;
                            }
                            Watchers.Add(client);
                            client.Send(fighter == 0 ? BuildWatcherList(new List<GameState>(), 0) : BuildWatcherList(new List<GameState>(), 0, fighter));
                            client.Send(fighter == 0 ? match.BuildPacket() : match.BuildPacket(0));
                            byte[] packet = fighter == 0 ? BuildWatcherList(Watchers, 2) : BuildWatcherList(Watchers, 2, fighter);
                            foreach (var watcher in Watchers.ToArray()) watcher.Send(packet);
                            Player1.Send(packet); Player2.Send(packet);
                        }
                        finally { Monitor.Exit(client.ItemSyncRoot); }
                    }
                    finally { Monitor.Exit(ParticipationSync); }
                }
                internal bool ContainsWatcherMap(GameState client)
                {
                    return dynamicMap != null && ReferenceEquals(client.Map, dynamicMap) && client.Entity.MapID == dynamicMap.ID;
                }
                internal bool LeaveWatching(GameState client)
                {
                    if (client == null || client.ItemSyncRoot == null || !Monitor.TryEnter(ParticipationSync)) return false;
                    try
                    {
                        if (!Monitor.TryEnter(client.ItemSyncRoot)) return false;
                        try
                        {
                            GameState canonical;
                            if (!ReferenceEquals(client.WatchingGroup, this)
                                || !Kernel.GamePool.TryGetValue(client.Entity.UID, out canonical) || !ReferenceEquals(canonical, client)) return false;
                            if (dynamicMap != null && ReferenceEquals(client.Map, dynamicMap))
                            {
                                if (!Features.QuestInventory.Player(client, false)) return false;
                                client.Entity.PreviousTeleport();
                                if (ReferenceEquals(client.Map, dynamicMap)) client.Entity.Teleport(1002, 408, 330);
                                if (ReferenceEquals(client.Map, dynamicMap)) return false;
                            }
                            Watchers.Remove(client);
                            if (ReferenceEquals(client.WatchingGroup, this)) client.WatchingGroup = null;
                            client.Send(BuildWatcherList(new List<GameState>(), 3));
                            byte[] packet = BuildWatcherList(Watchers, 2);
                            foreach (var watcher in Watchers.ToArray()) watcher.Send(packet);
                            Player1.Send(packet); Player2.Send(packet);
                            return true;
                        }
                        finally { Monitor.Exit(client.ItemSyncRoot); }
                    }
                    finally { Monitor.Exit(ParticipationSync); }
                }
                public List<Client.GameState> Watchers = new List<GameState>();
                #endregion

                public Time32 CreateTime;
                public Time32 DoneStamp;

                public uint Player1Damage, Player2Damage;
                public uint Player1Cheers, Player2Cheers;

                public List<uint> Cheerers = new List<uint>();

                public bool Done;

                private Game.Enums.PKMode P1Mode, P2Mode;

                public uint ID;

                public GroupMatch match = new GroupMatch();

                public Client.GameState Winner, Loser;
                public Client.GameState Player1, Player2;

                private Map dynamicMap;
                private int imported;
                private bool fightStarted;
                internal readonly object ParticipationSync=new object();
                internal readonly object DepartureSync=new object();
                internal Features.ArenaResultCoordinator.Departure PendingDeparture;
                internal readonly Guid ParticipationKey=Guid.NewGuid();
                internal bool ParticipationRecorded;
                // Set by staged lifecycle composition before import; not activated by live startup.
                internal bool RegisterLedgerOnEntry;
                internal bool EntryRecoveryRequired;
                internal bool StartedOnOwnedMap {
                    get {
                        return !Done && Volatile.Read(ref PendingDeparture)==null && OwnsImportedMap;
                    }
                }
                internal bool OwnsImportedMap {
                    get {
                        QualifierGroup registered;Map canonical;
                        return fightStarted && dynamicMap!=null && dynamicMap.ID!=700 && dynamicMap.BaseID==700
                            && Kernel.Maps.TryGetValue(dynamicMap.ID,out canonical) && ReferenceEquals(canonical,dynamicMap)
                            && Groups.TryGetValue(ID,out registered) && ReferenceEquals(registered,this)
                            && Player1!=null && Player2!=null && ReferenceEquals(Player1.QualifierGroup,this) && ReferenceEquals(Player2.QualifierGroup,this)
                            && Player1.Entity.MapID==dynamicMap.ID && Player2.Entity.MapID==dynamicMap.ID
                            && ReferenceEquals(Player1.Map,dynamicMap) && ReferenceEquals(Player2.Map,dynamicMap);
                    }
                }
                public Time32 ImportTime;
                public QualifierGroup(Client.GameState player1, Client.GameState player2)
                {
                    Player1 = player1;
                    Player2 = player2;
                    CreateTime = Time32.Now;
                    Player1Damage = 0;
                    Player2Damage = 0;
                    Done = false;
                    ID = GroupCounter.Next;
                    match.Group = this;
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
                    if(Volatile.Read(ref imported)!=0)return;
                    if(RegisterLedgerOnEntry && (!Features.ArenaStartupPreparation.Prepared || !Features.ArenaParticipation.Initialized
                        || !Features.ArenaMatchLedger.ReadyForAdmission(this)))return;
                    if (Features.TeamArenaResultStatistics.Initialized)
                    {
                        Features.ArenaAdmissionGuard.Run(ParticipationSync, new[] { Player1, Player2 },
                            p => (!RegisterLedgerOnEntry || (Features.ArenaMatchLedger.ReadyForAdmission(this) && !SignupWithdrawals.Pending(p)))
                                && !Done && (p.QualifierGroup == null || ReferenceEquals(p.QualifierGroup, this))
                                && p.TeamQualifierGroup == null && p.WatchingGroup == null && p.TeamWatchingGroup == null,
                            ImportReserved,
                            (c,t) => Features.ArenaAdmissions.Reserve(c,t,this), true);
                        return;
                    }
                    ImportCore();
                }
                internal bool ImportStarted {get{return Volatile.Read(ref imported)!=0;}}
                internal volatile Features.ArenaDisconnectRetention CompletionOwner;
                internal volatile bool OwnedEntryRequested;
                private void ImportCore()
                {
                    if(Interlocked.Exchange(ref imported,1)!=0)return;
                    P1Mode=Player1.Entity.PKMode;P2Mode=Player2.Entity.PKMode;
                    Player1.QualifierGroup = this;
                    Player2.QualifierGroup = this;
                    if(RegisterLedgerOnEntry && (SignupWithdrawals.Pending(Player1) || SignupWithdrawals.Pending(Player2)))return;

                    if (!Kernel.Maps.ContainsKey(700))
                        new Map(700, Database.DMaps.MapPaths[700]);
                    Map origMap = Kernel.Maps[700];
                    dynamicMap = origMap.MakeDynamicMap();
                    Player1.Entity.Teleport(origMap.ID, dynamicMap.ID, (ushort)Kernel.Random.Next(35, 70), (ushort)Kernel.Random.Next(35, 70));
                    Player2.Entity.Teleport(origMap.ID, dynamicMap.ID, (ushort)Kernel.Random.Next(35, 70), (ushort)Kernel.Random.Next(35, 70));
                    ImportTime = Time32.Now;
                    if (ReferenceEquals(Player1.Map,dynamicMap) && ReferenceEquals(Player2.Map,dynamicMap))
                    {
                        if(RegisterLedgerOnEntry && (SignupWithdrawals.Pending(Player1) || SignupWithdrawals.Pending(Player2)))return;
                        ArenaSignup sign = new ArenaSignup();
                        sign.DialogID = ArenaSignup.MainIDs.StartTheFight;
                        sign.Stats = Player1.ArenaStatistic;
                        Player2.Send(sign.BuildPacket());
                        sign.Stats = Player2.ArenaStatistic;
                        Player1.Send(sign.BuildPacket());
                        sign.DialogID = ArenaSignup.MainIDs.Match;
                        sign.OptionID = ArenaSignup.DialogButton.MatchOn;
                        Player1.Send(sign.BuildPacket());
                        Player2.Send(sign.BuildPacket());
                        Player1.Send(match.BuildPacket());
                        Player2.Send(match.BuildPacket());
                        Player1.Entity.BringToLife();
                        Player2.Entity.BringToLife();
                        Player1.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.Ride);
                        Player2.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.Ride);
                        Player1.Entity.PKMode = Revora.Game.Enums.PKMode.PK;
                        Player1.Send(new Data(true) { UID = Player1.Entity.UID, ID = Data.ChangePKMode, dwParam = (uint)Player1.Entity.PKMode });
                        Player2.Entity.PKMode = Revora.Game.Enums.PKMode.PK;
                        Player2.Send(new Data(true) { UID = Player2.Entity.UID, ID = Data.ChangePKMode, dwParam = (uint)Player2.Entity.PKMode });
                        fightStarted=true;
                        Features.ArenaParticipation.RecordStart(this);
                        if(RegisterLedgerOnEntry && !ParticipationRecorded && !EntryRecoveryRequired
                            && (SignupWithdrawals.Pending(Player1) || SignupWithdrawals.Pending(Player2)))fightStarted=false;
                    }
                    else if(!DurableAdmissionReserved)
                        End();
                }

                private void ImportReserved()
                {
                    DurableAdmissionReserved=Features.ArenaAdmissions.Initialized;
                    if(!DurableAdmissionReserved){ImportCore();return;}
                    try{ImportCore();}
                    catch(Exception ex){Console.WriteLine("Solo Arena transfer interrupted: "+ex.GetType().Name);}
                    if(fightStarted && OwnsImportedMap)return;
                    AdmissionInterrupted=true;
                    Features.ArenaResultCoordinator.CancelInterrupted(this);
                    Features.ArenaResultCoordinator.Cleanup(this);
                }

                internal bool CleanupCompleted;
                internal bool DurableAdmissionReserved;
                internal bool AdmissionInterrupted,DurableAdmissionCancelled;
                internal int DurableResultReady;

                // Called only after the coordinator has verified the committed bundle and acquired
                // the match plus participant/watcher locks. Legacy Export uses this cleanup too.
                internal bool CleanupOwned(bool durable = false)
                {
                    if (!Monitor.IsEntered(ParticipationSync)) throw new InvalidOperationException("Arena cleanup requires its match lock.");
                    if (CleanupCompleted) return true;
                    foreach (var player in new[] { Player1, Player2 }.Concat(Watchers.ToArray()).Distinct())
                    {
                        if (player == null) continue;
                        if (!Monitor.IsEntered(player.ItemSyncRoot)) throw new InvalidOperationException("Arena cleanup requires owner locks.");
                        GameState canonical;
                        bool fighter = ReferenceEquals(player, Player1) || ReferenceEquals(player, Player2);
                        if (!Kernel.GamePool.TryGetValue(player.Entity.UID, out canonical) || !ReferenceEquals(canonical, player))
                        {
                            if (durable && fighter) RemoveOwnedQueues(player);
                            continue;
                        }
                        if (fighter ? !ReferenceEquals(player.QualifierGroup, this) : !ReferenceEquals(player.WatchingGroup, this)) continue;
                        if (durable && fighter && (!Features.ArenaActors.Player(player, false) || player.ArenaStatistic == null || player.ArenaStatistic.EntityID != player.Entity.UID
                            || (player.ArenaStatistic.PlayWith != null && !ReferenceEquals(player.ArenaStatistic.PlayWith, OppositeClient(player))))) return false;
                        if (dynamicMap != null && ReferenceEquals(player.Map, dynamicMap) && player.Entity.MapID == dynamicMap.ID)
                        {
                            // Fenced sessions must reload before further player-state mutation.
                            if (!Features.ArenaActors.Player(player, false)) return false;
                            player.Entity.PreviousTeleport();
                            if (ReferenceEquals(player.Map, dynamicMap)) player.Entity.Teleport(1002, 408, 330);
                            if (ReferenceEquals(player.Map, dynamicMap)) return false;
                            if (fighter && (fightStarted || DurableAdmissionCancelled))
                            {
                                player.Entity.PKMode = ReferenceEquals(player, Player1) ? P1Mode : P2Mode;
                                player.Send(new Data(true) { UID = player.Entity.UID, ID = Data.ChangePKMode, dwParam = (uint)player.Entity.PKMode });
                            }
                        }
                        // Teleport/send can synchronously disconnect. Do not mutate a replacement's cached statistics.
                        if (durable && fighter && (!Kernel.GamePool.TryGetValue(player.Entity.UID, out canonical) || !ReferenceEquals(canonical, player)))
                        {
                            RemoveOwnedQueues(player);continue;
                        }
                        // Never clear a later match or resurrect/teleport someone who already left.
                        if (fighter && ReferenceEquals(player.QualifierGroup, this))
                        {
                            if (durable)
                            {
                                RemoveOwnedQueues(player);
                                player.ArenaStatistic.AcceptBox = false;
                                player.ArenaStatistic.AcceptBoxShow = Time32.Now;player.ArenaStatistic.PlayWith = null;
                                player.ArenaStatistic.Status = QualifierStatistic.NotSignedUp;player.ArenaState = Qualifier.None;
                            }
                            player.Send(new ArenaSignup { DialogID = ArenaSignup.MainIDs.Match,
                                OptionID = ArenaSignup.DialogButton.MatchOff, Stats = player.ArenaStatistic }.BuildPacket());
                            player.Send(ArenaSignup.EndMatchPacket(player.ArenaStatistic, ReferenceEquals(player, Winner)));
                            if (!player.Fake) Console.WriteLine("[arena] match-ui-ended uid=" + player.Entity.UID);
                            player.QualifierGroup = null;
                            if (durable) player.ArenaStatistic.Send(player);
                        }
                        if (!fighter && ReferenceEquals(player.WatchingGroup, this))
                        {
                            player.WatchingGroup = null;
                            player.Send(BuildWatcherList(new List<GameState>(), 3));
                        }
                    }
                    Watchers.Clear();
                    ((ICollection<KeyValuePair<uint, QualifierGroup>>)Groups).Remove(new KeyValuePair<uint, QualifierGroup>(ID, this));
                    if (dynamicMap != null) dynamicMap.Dispose();
                    CleanupCompleted = true;
                    return true;
                }

                private static void RemoveOwnedQueues(GameState player)
                {
                    var keys = new[] { player.Entity.UID, player.Account == null ? 0u : player.Account.EntityID };
                    foreach (uint key in keys.Distinct())
                    {
                        var entry = new KeyValuePair<uint, GameState>(key, player);
                        ((ICollection<KeyValuePair<uint, GameState>>)Qualifier.PlayerList).Remove(entry);
                        ((ICollection<KeyValuePair<uint, GameState>>)Qualifier.WaitingPlayerList).Remove(entry);
                    }
                }

                internal bool RejectUnimported()
                {
                    if(!Monitor.TryEnter(ParticipationSync))return false;
                    var owners=new[]{Player1,Player2}.OrderBy(p=>p.Entity.UID).ToArray();int acquired=0;
                    try {
                        if(CleanupCompleted)return true;
                        if(ImportStarted || DurableAdmissionReserved || Done || dynamicMap!=null)return false;
                        foreach(var p in owners){if(!Monitor.TryEnter(p.ItemSyncRoot))return false;acquired++;}
                        foreach(var p in owners) {
                            GameState current;
                            if(!Kernel.GamePool.TryGetValue(p.Entity.UID,out current) || !ReferenceEquals(current,p)){RemoveOwnedQueues(p);continue;}
                            if(p.QualifierGroup!=null || p.TeamQualifierGroup!=null || p.ArenaStatistic==null
                                || (p.ArenaStatistic.PlayWith!=null && !ReferenceEquals(p.ArenaStatistic.PlayWith,OppositeClient(p))))continue;
                            if(!Features.ArenaActors.Player(p,false))return false;
                            RemoveOwnedQueues(p);p.ArenaStatistic.AcceptBox=false;p.ArenaStatistic.PlayWith=null;
                            p.ArenaStatistic.Status=QualifierStatistic.NotSignedUp;p.ArenaState=Qualifier.None;
                            p.ArenaStatistic.Send(p);
                        }
                        ((ICollection<KeyValuePair<uint,QualifierGroup>>)Groups).Remove(new KeyValuePair<uint,QualifierGroup>(ID,this));
                        CleanupCompleted=true;return true;
                    }finally{for(int n=acquired-1;n>=0;n--)Monitor.Exit(owners[n].ItemSyncRoot);Monitor.Exit(ParticipationSync);}
                }

                public bool Export()
                {
                    if (!Monitor.TryEnter(ParticipationSync)) return false;
                    GameState[] owners = null; int acquired = 0;
                    try
                    {
                        if (!Done || CleanupCompleted) return false;
                        owners = new[] { Player1, Player2 }.Concat(Watchers.ToArray()).Where(p => p != null)
                            .Distinct().OrderBy(p => p.Entity.UID).ToArray();
                        foreach (var player in owners)
                        {
                            if (player.ItemSyncRoot == null || !Monitor.TryEnter(player.ItemSyncRoot)) return false;
                            acquired++;
                        }
                        return CleanupOwned();
                    }
                    finally
                    {
                        for (int n = acquired - 1; n >= 0; n--) Monitor.Exit(owners[n].ItemSyncRoot);
                        Monitor.Exit(ParticipationSync);
                    }
                }

                public void End()
                {
                    lock (ParticipationSync)
                    {
                        if (FreezeOutcome(Player1Damage > Player2Damage ? Player2 : Player1) && PendingDeparture == null && CompletionOwner==null) NotifyOutcome();
                    }
                }

                internal bool FreezeOutcome(GameState loser)
                {
                    if (!Monitor.IsEntered(ParticipationSync)) throw new InvalidOperationException("Arena outcome requires its match lock.");
                    lock (DepartureSync)
                    {
                        if (Done || AdmissionInterrupted || DurableAdmissionCancelled || (DurableAdmissionReserved && !fightStarted)
                            || (RegisterLedgerOnEntry && !ParticipationRecorded)
                            || (!ReferenceEquals(loser, Player1) && !ReferenceEquals(loser, Player2))) return false;
                        if (PendingDeparture != null) loser = PendingDeparture.Player;
                        Loser = loser;
                        Winner = ReferenceEquals(loser, Player1) ? Player2 : Player1;
                        Done = true;
                        DoneStamp = Time32.Now;
                        return true;
                    }
                }

                internal bool ResultNotificationReady;
                private readonly HashSet<GameState> outcomeNotified=new HashSet<GameState>();
                internal void NotifyCommittedOutcome()
                {
                    if(!Monitor.IsEntered(ParticipationSync))throw new InvalidOperationException("Arena notification requires its match lock.");
                    if(CompletionOwner==null || !ResultNotificationReady || !Done || !OwnsImportedMap)return;
                    foreach(var player in new[]{Loser,Winner}) {
                        if(player==null || !Monitor.IsEntered(player.ItemSyncRoot) || !Features.QuestInventory.Player(player,false)
                            || !ReferenceEquals(player.QualifierGroup,this)
                            || Features.ArenaCacheRecovery.RequiresReload(player.ArenaStatistic)
                            || Features.ArenaCacheRecovery.RequiresReload(player.TeamArenaStatistic)
                            || !outcomeNotified.Add(player))continue;
                        var sign=new ArenaSignup();sign.Stats=player.ArenaStatistic;sign.DialogID=ArenaSignup.MainIDs.Dialog;
                        sign.OptionID=ReferenceEquals(player,Winner)?ArenaSignup.DialogButton.Win:ArenaSignup.DialogButton.Lose;
                        player.Send(sign.BuildPacket());
                    }
                }
                internal void NotifyOutcome()
                {
                    if (CompletionOwner!=null || !Done || Winner == null || Loser == null) return;
                    var sign = new ArenaSignup();
                    sign.Stats = Loser.ArenaStatistic;
                    sign.DialogID = ArenaSignup.MainIDs.Dialog;
                    sign.OptionID = ArenaSignup.DialogButton.Lose;
                    Loser.Send(sign.BuildPacket());
                    sign.OptionID = ArenaSignup.DialogButton.Win;
                    sign.Stats = Winner.ArenaStatistic;
                    Winner.Send(sign.BuildPacket());
                }

                public void End(GameState loser)
                {
                    lock (ParticipationSync)
                    {
                        if (FreezeOutcome(loser) && PendingDeparture == null && CompletionOwner==null) NotifyOutcome();
                    }
                }

                public void UpdateDamage(GameState client, uint damage)
                {
                    byte[] packet;
                    lock (ParticipationSync)
                    {
                        if (!StartedOnOwnedMap || !Features.ArenaActors.Player(client, false)
                            || (!ReferenceEquals(client, Player1) && !ReferenceEquals(client, Player2))) return;
                        if (ReferenceEquals(client, Player1)) Player1Damage = (uint)Math.Min(uint.MaxValue, (ulong)Player1Damage + damage);
                        else Player2Damage = (uint)Math.Min(uint.MaxValue, (ulong)Player2Damage + damage);
                        packet = match.BuildPacket();
                    }
                    // Sending may synchronously disconnect a session; do not hold the match lock.
                    Player1.Send(packet);
                    Player2.Send(packet);
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
                var owned=client==null?null:client.QualifierGroup;
                if(owned!=null && owned.CompletionOwner!=null) {
                    Features.ArenaResultCoordinator.EndForPlayer(owned,client,Features.ArenaResultIntents.Cause.Forfeit);return;
                }
                if(Features.ArenaStartupPreparation.Prepared){SignupWithdrawals.Request(client);return;}
                PlayerList.Remove(client.Account.EntityID);
                RequestGroupList(client, 1);

                if (client.QualifierGroup != null)
                {
                    client.QualifierGroup.End(client);
                    client.QualifierGroup.CreateTime = Time32.Now.AddSeconds(-100);
                }
                else
                {
                    ArenaSignup sign = new ArenaSignup();
                    sign.DialogID = ArenaSignup.MainIDs.OpponentGaveUp;
                    if (client.ArenaStatistic.PlayWith != null)
                    {
                        Client.GameState other = client.ArenaStatistic.PlayWith;
                        other.Send(sign.BuildPacket());
                        Win(other, client);
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
                if(Features.ArenaStartupPreparation.Prepared && !Features.ArenaDayRollover.Prepare(client))return;
                if (Revora.Game.Features.LocalPkWar.InEvent(client.Entity) || Revora.Game.Features.CouplePkEvent.InSession(client)) return;
                if (client.ArenaStatistic.Status != QualifierStatistic.NotSignedUp || client.TeamArenaStatistic.Status != TeamQualifierStatistic.NotSignedUp)
                {
                    client.Send(new Message("You have already joined a qualifier arena! Leave the other and sign up again.", Color.Beige, Message.Agate));
                    return;
                }
                if (client.InQualifier())
                {
                    return;
                }
                if (client.ArenaStatistic.ArenaPoints == 0)
                    return;
                if (client.Entity.MapID == 601) return;
                if (client.Map.BaseID >= 6000 && client.Map.BaseID <= 6003) return;

                if (WaitingPlayerList.ContainsKey(client.Account.EntityID))
                {
                    if (client.QualifierGroup == null)
                        WaitingPlayerList.Remove(client.Account.EntityID);
                    else
                        return;
                }
                PlayerList.Add(client.Account.EntityID, client);
                client.ArenaStatistic.Status = Network.GamePackets.QualifierStatistic.WaitingForOpponent;
                client.ArenaState = FindMatch;
                client.Send(client.ArenaStatistic);
                RequestGroupList(client, 1);
            }
            public static void DoGiveUp(Client.GameState client)
            {
                var owned=client==null?null:client.QualifierGroup;
                if(owned!=null && owned.CompletionOwner!=null) {
                    Features.ArenaResultCoordinator.EndForPlayer(owned,client,Features.ArenaResultIntents.Cause.Forfeit);return;
                }
                if(Features.ArenaStartupPreparation.Prepared){SignupWithdrawals.Request(client);return;}
                if (client.ArenaState == WaitForBox)
                {
                    client.ArenaStatistic.AcceptBox = false;
                    client.ArenaState = WaitForOther;
                }
                else
                {
                    client.ArenaStatistic.Status = Network.GamePackets.QualifierStatistic.WaitingInactive;
                    client.Send(client.ArenaStatistic);
                    RequestGroupList(client, 1);
                    ArenaSignup sign = new ArenaSignup();
                    sign.DialogID = ArenaSignup.MainIDs.OpponentGaveUp;

                    if (client.ArenaStatistic.PlayWith != null)
                    {
                        client.ArenaStatistic.PlayWith.Send(sign.BuildPacket());

                        client.ArenaStatistic.PlayWith.ArenaStatistic.Status = Network.GamePackets.QualifierStatistic.NotSignedUp;
                        client.ArenaStatistic.Status = Network.GamePackets.QualifierStatistic.NotSignedUp;
                        client.ArenaStatistic.PlayWith.Send(client.ArenaStatistic.PlayWith.ArenaStatistic);
                        client.Send(client.ArenaStatistic);

                        if (WaitingPlayerList.ContainsKey(client.Account.EntityID))
                        {
                            WaitingPlayerList.Remove(client.Account.EntityID);
                            WaitingPlayerList.Remove(client.ArenaStatistic.PlayWith.Account.EntityID);
                        }
                        if (client.QualifierGroup != null)
                        {
                            if (!client.QualifierGroup.Done)
                            {
                                client.QualifierGroup.End(client);
                            }
                            else
                            {
                                Win(client.ArenaStatistic.PlayWith, client);
                            }
                        }
                        else
                        {
                            Win(client.ArenaStatistic.PlayWith, client);
                        }
                    }
                }
            }

            public static void DoAccept(Client.GameState client)
            {
                if (client.ArenaState == WaitForBox)
                {
                    client.ArenaStatistic.AcceptBox = true;
                    client.ArenaState = WaitForOther;
                }
            }

            public static void DoLeave(Client.GameState client)
            {
                var group = client == null ? null : client.WatchingGroup;
                if (group != null) group.LeaveWatching(client);
            }

            public static void DoCheer(Client.GameState client, uint uid)
            {
                var group = client == null ? null : client.WatchingGroup;
                if (group == null || !Monitor.TryEnter(group.ParticipationSync)) return;
                try
                {
                    if (!group.StartedOnOwnedMap || !Features.QuestInventory.Player(client)
                        || !ReferenceEquals(client.WatchingGroup, group) || !group.Watchers.Contains(client)
                        || !group.ContainsWatcherMap(client) || group.Cheerers.Contains(client.Entity.UID)
                        || (uid != group.Player1.Entity.UID && uid != group.Player2.Entity.UID)) return;
                    group.Cheerers.Add(client.Entity.UID);
                    if (uid == group.Player1.Entity.UID) group.Player1Cheers++;
                    else group.Player2Cheers++;
                    byte[] packet = group.BuildWatcherList(group.Watchers, 2);
                    foreach (var watcher in group.Watchers.ToArray()) watcher.Send(packet);
                    group.Player1.Send(packet); group.Player2.Send(packet);
                }
                finally { Monitor.Exit(group.ParticipationSync); }
            }
        }

        public class Statistics
        {
            public static void MsgQualifyingSeasonRankList(Client.GameState client)
            {
                MemoryStream strm = new MemoryStream();
                BinaryWriter wtr = new BinaryWriter(strm);
                int MyCount = 0;
                wtr.Write((ushort)0);
                wtr.Write((ushort)2208);
                wtr.Write((uint)Game.Enums.ArenaIDs.ShowPlayerRankList);
                foreach (QualifierStatistic entry in YesterdayArenaStatisticsList)
                {
                    MyCount++;
                    wtr.Write((uint)entry.EntityID);
                    byte[] array = Encoding.Default.GetBytes(entry.Name);
                    for (int i = 0; i < 16; i++)
                    {
                        if (i < entry.Name.Length)
                            wtr.Write(array[i]);
                        else
                            wtr.Write((byte)0);
                    }
                    wtr.Write((uint)entry.Model);
                    wtr.Write((uint)entry.Level);
                    wtr.Write((uint)entry.Class);
                    wtr.Write((uint)entry.LastSeasonRank);
                    wtr.Write((uint)entry.LastSeasonRank);
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
                ArenaList list = new ArenaList(pageIndex);
                list.ID = Game.Enums.ArenaIDs.ShowPlayerRankList;
                list.PageNumber = (ushort)pageIndex;
                list.Subtype = thisSeason;
                if (list.Subtype == 0)
                {
                    var Array = ArenaStatisticsList;
                    if (Array.Count > (((pageIndex) * 10) - 10))
                    {
                        list.Players.Clear();
                        for (int i = ((pageIndex) * 10 - 10); i < ((pageIndex) * 10 - 10) + 10; i++)
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
                else
                {
                    var Array = YesterdayArenaStatisticsList;
                    if (Array.Count > (((pageIndex) * 10) - 10))
                    {
                        list.Players.Clear();
                        for (int i = ((pageIndex) * 10 - 10); i < ((pageIndex) * 10 - 10) + 10; i++)
                        {
                            if (i < Array.Count)
                            {
                                if (Array[i].LastSeasonRank > 0)
                                {
                                    list.Players.Add(Array[i]);
                                }
                            }
                        }
                    }
                }
                client.Send(list.BuildPacket());
            }
            public static void MsgArenicWitness(byte[] packet, Client.GameState client)
            {
                #region MaxsMap
                if (client.Entity.MapID == 1090 || client.Entity.MapID == 1559
                    || client.Entity.MapID == 1518 || client.Entity.MapID == 4021
                    || client.Entity.MapID == 12345 || client.Entity.MapID == 12346
                    || client.Entity.MapID == 1707 || client.Entity.MapID == 14785
                    || client.Entity.MapID == 3333 || client.Entity.MapID == 6412
                    || client.Entity.MapID == 5095 || client.Entity.MapID == 3260
                    || client.Entity.MapID == 5689 || client.Entity.MapID == 5690
                    || client.Entity.MapID == 6689 || client.Entity.MapID == 6690
                    || client.Entity.MapID == 3261 || client.Entity.MapID == 3262
                    || client.Entity.MapID == 10360 || client.Entity.MapID == 4025
                    || client.Entity.MapID == 3071 || client.Entity.MapID == 2527
                    || client.Entity.MapID == 5238 || client.Entity.MapID == 5239
                    || client.Entity.MapID == 2522 || client.Entity.MapID == 1655
                    || client.Entity.MapID == 2090 || client.Entity.MapID == 2091)
                    return;
                #endregion
                if (client.Entity.MapID == 601 || client.Entity.MapID == 3846) return;
                if (client.Map.BaseID >= 6000 && client.Map.BaseID <= 6003) return;
                ushort Type = BitConverter.ToUInt16(packet, 4);
                uint Fighter = BitConverter.ToUInt32(packet, 10);
                if (Type == 0)
                {
                    if (!Constants.PKFreeMaps.Contains(client.Entity.MapID))
                    {
                        if (Kernel.GamePool.ContainsKey(Fighter))
                        {
                            Client.GameState Client = Kernel.GamePool[Fighter];
                            if (Client.InQualifier())
                            {
                                if (Client.QualifierGroup != null)
                                {
                                    if (!Client.QualifierGroup.Done)
                                    {
                                        Client.QualifierGroup.BeginWatching(client, Fighter);
                                    }
                                }
                                else if (Client.TeamQualifierGroup != null)
                                {
                                    if (!Client.TeamQualifierGroup.Done)
                                    {
                                        Client.TeamQualifierGroup.BeginWatching(client);
                                    }
                                }
                            }
                            else if (Client.Team != null)
                            {
                                if (Client.Team.EliteMatch != null)
                                {
                                    if (Client.Team.EliteMatch.OnGoing)
                                    {
                                        Client.Team.EliteMatch.BeginWatch(client);
                                    }
                                }
                            }
                            else if (Client.ElitePKMatch != null)
                            {
                                if (Client.ElitePKMatch.OnGoing)
                                {
                                    Client.ElitePKMatch.BeginWatch(client);
                                }
                            }
                        }
                        else if (client.ElitePKMatch != null)
                        {
                            if (client.ElitePKMatch.OnGoing)
                            {
                                client.ElitePKMatch.BeginWatch(client);
                            }
                        }
                        else if (client.InQualifier())
                        {
                            if (client.QualifierGroup != null)
                            {
                                if (!client.QualifierGroup.Done)
                                {
                                    client.QualifierGroup.BeginWatching(client, Fighter);
                                }
                            }
                            else if (client.TeamQualifierGroup != null)
                            {
                                if (!client.TeamQualifierGroup.Done)
                                {
                                    client.TeamQualifierGroup.BeginWatching(client);
                                }
                            }
                        }
                    }
                }
                else if (Type == 1)
                {
                    if (client.WatchingElitePKMatch != null)
                    {
                        client.WatchingElitePKMatch.LeaveWatch(client);
                    }
                    else if (client.WatchingGroup != null)
                    {
                        Qualifier.QualifyEngine.DoLeave(client);
                    }
                    else if (client.TeamWatchingGroup != null)
                    {
                        TeamQualifier.QualifyEngine.DoLeave(client);
                    }
                    else if (client.Entity.SkillTeamWatchingElitePKMatch != null)
                    {
                        client.Entity.SkillTeamWatchingElitePKMatch.LeaveWatch(client);
                    }
                }
                else if (Type == 4)
                {
                    uint fighter = BitConverter.ToUInt32(packet, 10);
                    if (client.WatchingElitePKMatch != null)
                    {
                        client.WatchingElitePKMatch.Cheer(client, fighter);
                    }
                    else if (client.WatchingGroup != null)
                    {
                        Qualifier.QualifyEngine.DoCheer(client, fighter);
                    }
                    else if (client.TeamWatchingGroup != null)
                    {
                        TeamQualifier.QualifyEngine.DoCheer(client, fighter);
                    }
                    else if (client.Entity.SkillTeamWatchingElitePKMatch != null)
                    {
                        client.Entity.SkillTeamWatchingElitePKMatch.Cheer(client, fighter);
                    }
                }
            }
            public static void MsgQualifyingInteractive(byte[] packet, Client.GameState client)
            {
                #region ReturnMap
                if (client.Entity.MapID == 1090 || client.Entity.MapID == 1559
                    || client.Entity.MapID == 1518 || client.Entity.MapID == 4021
                    || client.Entity.MapID == 12345 || client.Entity.MapID == 12346
                    || client.Entity.MapID == 1707 || client.Entity.MapID == 14785
                    || client.Entity.MapID == 9888 || client.Entity.MapID == 3333
                    || client.Entity.MapID == 5095 || client.Entity.MapID == 3260
                    || client.Entity.MapID == 5238 || client.Entity.MapID == 5239
                    || client.Entity.MapID == 5689 || client.Entity.MapID == 5690
                    || client.Entity.MapID == 6689 || client.Entity.MapID == 6690
                    || client.Entity.MapID == 3261 || client.Entity.MapID == 3262
                    || client.Entity.MapID == 8877 || client.Entity.MapID == 6417
                    || client.Entity.MapID == 1239 || client.Entity.MapID == 1238
                    || client.Entity.MapID == 1237 || client.Entity.MapID == 1238
                    || client.Entity.MapID == 1236 || client.Entity.MapID == 1235
                    || client.Entity.MapID == 1234 || client.Entity.MapID == 1038
                    || client.Entity.MapID == 1113 || client.Entity.MapID == 6412
                    || client.Entity.MapID == 10360 || client.Entity.MapID == 4025
                    || client.Entity.MapID == 3071 || client.Entity.MapID == 2527
                    || client.Entity.MapID == 2522 || client.Entity.MapID == 1655
                    || client.Entity.MapID == 2090 || client.Entity.MapID == 2091)
                    return;
                #endregion
                uint DialogID = BitConverter.ToUInt32(packet, 4);
                uint ButtonID = BitConverter.ToUInt32(packet, 8);
                switch (DialogID)
                {
                    case 4:
                        {
                            switch (ButtonID)
                            {
                                case 0:
                                    {
                                        Game.Qualifier.QualifyEngine.DoQuit(client);
                                        break;
                                    }
                            }
                            break;
                        }
                    case 0: Game.Qualifier.QualifyEngine.DoSignup(client); client.Send(packet); break;
                    case 1: Game.Qualifier.QualifyEngine.DoQuit(client); client.Send(packet); break;
                    case 3:
                        {
                            switch (ButtonID)
                            {
                                case 2: Game.Qualifier.QualifyEngine.DoGiveUp(client); break;
                                case 1: Game.Qualifier.QualifyEngine.DoAccept(client); break;
                            }
                            break;
                        }
                    case 5:
                        {
                            Revora.Game.Features.ArenaPointPurchases.Buy(client);
                            break;
                        }
                    case 11:
                        {
                            switch (ButtonID)
                            {
                                case 0: Game.Qualifier.QualifyEngine.DoSignup(client); break;
                            }
                            break;
                        }
                }
            }
            public static void ShowWiners(Client.GameState client)
            {
                MemoryStream strm = new MemoryStream();
                BinaryWriter wtr = new BinaryWriter(strm);
                int MyCount = 0;
                wtr.Write((ushort)0);
                wtr.Write((ushort)2208);
                wtr.Write((uint)Game.Enums.ArenaIDs.ShowPlayerRankList);
                foreach (QualifierStatistic entry in YesterdayArenaStatisticsList)
                {
                    MyCount++;
                    wtr.Write((uint)entry.EntityID);
                    byte[] array = Server.Encoding.GetBytes(entry.Name);
                    for (int i = 0; i < 16; i++)
                    {
                        if (i < entry.Name.Length)
                            wtr.Write(array[i]);
                        else
                            wtr.Write((byte)0);
                    }
                    wtr.Write((uint)entry.Model);
                    wtr.Write((uint)entry.Level);
                    wtr.Write((uint)entry.Class);
                    wtr.Write((uint)entry.LastSeasonRank);
                    wtr.Write((uint)entry.LastSeasonRank);
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
                wtr.Write(Server.Encoding.GetBytes("TQServer"));
                strm.Position = 0;
                byte[] buf = new byte[strm.Length];
                strm.Read(buf, 0, buf.Length);
                wtr.Close();
                strm.Close();
                client.Send(buf);
            }
        }

        public class ArenaList
        {
            public ushort Size;
            public ushort Type;
            public ushort Subtype;
            public ushort PageNumber;
            public Game.Enums.ArenaIDs ID;
            public List<QualifierStatistic> Players = new List<QualifierStatistic>();
            public ArenaList(int PageIndex, ushort type = 2207)
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
                Type = 2207;
            }
            public byte[] BuildPacket()
            {
                ushort mRank = (ushort)(PageNumber * 10 - 10);
                MemoryStream strm = new MemoryStream();
                BinaryWriter wtr = new BinaryWriter(strm);

                wtr.Write((ushort)0);
                wtr.Write((ushort)Type);
                wtr.Write((ushort)Subtype);
                wtr.Write((ushort)PageNumber);
                if (Subtype == 0)
                    wtr.Write((uint)InArenaCount);
                else
                    wtr.Write((uint)HistoryArenaCount);
                wtr.Write((uint)ID);
                foreach (QualifierStatistic entry in Players)
                {
                    if (Subtype == 0)
                        wtr.Write((ushort)entry.Rank);
                    else
                        wtr.Write((ushort)entry.LastSeasonRank);

                    for (int i = 0; i < 16; i++)
                    {
                        if (i < entry.Name.Length)
                            wtr.Write((byte)entry.Name[i]);
                        else
                            wtr.Write((byte)0);
                    }
                    if (Subtype == 1)
                        wtr.Write((ushort)6004);
                    else
                        wtr.Write((ushort)0);
                    if (Subtype == 1)
                        wtr.Write((uint)entry.CurrentHonor);
                    else
                        wtr.Write((uint)entry.ArenaPoints);

                    wtr.Write((uint)entry.Class);
                    wtr.Write((uint)entry.Level);

                    if (Subtype == 1)
                        wtr.Write((uint)33942209);
                    else
                        wtr.Write((uint)1);
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


            internal static byte[] EndMatchPacket(QualifierStatistic stats, bool won)
            {
                return new ArenaSignup { DialogID = MainIDs.Dialog2, OptionID = won ? DialogButton.Win : 0,
                    Stats = stats }.BuildPacket();
            }

            public ushort Type = 2205;
            public uint DialogID;
            public uint OptionID;
            public QualifierStatistic Stats;
            public byte[] BuildPacket()
            {
                byte[] buf = new byte[56 + 8];
                if (Stats == null)
                {
                    Stats = new QualifierStatistic(true);
                    Stats.Name = "";
                }
                Network.Writer.Write((ushort)56, 0, buf);
                Network.Writer.Write((ushort)Type, 2, buf);
                Network.Writer.Write((uint)DialogID, 4, buf);
                Network.Writer.Write((uint)OptionID, 8, buf);
                Network.Writer.Write((uint)Stats.EntityID, 12, buf);
                Network.Writer.Write(Stats.Name, 20, buf);
                Network.Writer.Write((uint)Stats.Class, 40, buf);
                Network.Writer.Write((uint)Stats.Rank, 44, buf);
                Network.Writer.Write((uint)Stats.ArenaPoints, 48, buf);
                Network.Writer.Write((uint)Stats.Level, 52, buf);
                return buf;
            }
        }

        public class GroupMatch
        {
            public byte[] BuildPacket(byte valu = 1)
            {
                byte[] buff = new byte[68];
                Network.Writer.Write(60, 0, buff);
                Network.Writer.Write(Type, 2, buff);
                Network.Writer.Write((uint)Group.Player1.ArenaStatistic.EntityID, 4, buff);
                Network.Writer.Write(Group.Player1.ArenaStatistic.Name, 12, buff);
                Network.Writer.Write((uint)Group.Player1Damage, 28, buff);
                Network.Writer.Write((uint)Group.Player2.ArenaStatistic.EntityID, 32, buff);
                Network.Writer.Write(Group.Player2.ArenaStatistic.Name, 40, buff);
                Network.Writer.Write((uint)Group.Player2Damage, 56, buff);
                return buff;
            }
            public ushort Type = 2210;
            public QualifierList.QualifierGroup Group;
            public byte[] BuildPacket()
            {
                byte[] buff = new byte[68];
                Network.Writer.Write(60, 0, buff);
                Network.Writer.Write(Type, 2, buff);
                Network.Writer.Write((uint)Group.Player1.ArenaStatistic.EntityID, 4, buff);
                Network.Writer.Write(Group.Player1.ArenaStatistic.Name, 12, buff);
                Network.Writer.Write((uint)Group.Player1Damage, 28, buff);
                Network.Writer.Write((uint)Group.Player2.ArenaStatistic.EntityID, 32, buff);
                Network.Writer.Write(Group.Player2.ArenaStatistic.Name, 40, buff);
                Network.Writer.Write((uint)Group.Player2Damage, 56, buff);
                return buff;
            }
        }

        public static void Win(Client.GameState winner, Client.GameState loser)
        {
            var owned=loser==null?null:loser.QualifierGroup;
            if(owned!=null && owned.CompletionOwner!=null) {
                if(ReferenceEquals(owned.OppositeClient(loser),winner))
                    Features.ArenaResultCoordinator.EndForPlayer(owned,loser,Features.ArenaResultIntents.Cause.Defeat);
                return;
            }
            if(Features.ArenaStartupPreparation.Prepared){RejectInvitation(winner,loser);return;}
            
            if (winner.ArenaStatistic.PlayWith != null && loser.ArenaStatistic.PlayWith != null)
            {
                int diff = Kernel.Random.Next(30, 50);

                winner.ArenaStatistic.PlayWith = null;
                loser.ArenaStatistic.PlayWith = null;

                WaitingPlayerList.Remove(loser.Account.EntityID);
                WaitingPlayerList.Remove(winner.Account.EntityID);

                winner.ArenaStatistic.Status = Network.GamePackets.QualifierStatistic.NotSignedUp;
                winner.Send(winner.ArenaStatistic);
                winner.ArenaState = FindMatch;
                winner.QualifierGroup = null;
                loser.ArenaStatistic.Status = Network.GamePackets.QualifierStatistic.NotSignedUp;
                loser.Send(winner.ArenaStatistic);
                loser.ArenaState = FindMatch;
                loser.QualifierGroup = null;

                winner.ArenaPoints += (uint)diff;
                #region Remover Habilidades Ganador
                winner.Entity.ToxicFogLeft = 0;
                winner.Entity.ScurbyBomb = 0;
                if (!Features.HealingMedicine.TryClearNoDrugs(winner.Entity, false) && !Features.HealingMedicine.Enabled)
                    winner.Entity.NoDrugsTime = 0;
                winner.Entity.DragonFuryTime = 0;
                winner.Entity.lianhuaranLeft = 0;
                winner.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.DefensiveStance);
                winner.Entity.IsDefensiveStance = false;
                winner.Entity.DefensiveStance = 0;
                winner.Entity.BlockShield = 0;
                winner.Entity.BlockShieldCheck = false;
                winner.Entity.Teleport(1002, 344, 230);
                #endregion
                #region Remover Habilidades Perdedor
                loser.Entity.ToxicFogLeft = 0;
                loser.Entity.ScurbyBomb = 0;
                if (!Features.HealingMedicine.TryClearNoDrugs(loser.Entity, false) && !Features.HealingMedicine.Enabled)
                    loser.Entity.NoDrugsTime = 0;
                loser.Entity.DragonFuryTime = 0;
                loser.Entity.lianhuaranLeft = 0;
                loser.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.DefensiveStance);
                loser.Entity.IsDefensiveStance = false;
                loser.Entity.DefensiveStance = 0;
                loser.Entity.BlockShield = 0;
                loser.Entity.BlockShieldCheck = false;
                loser.Entity.Teleport(1002, 339, 234);
                #endregion
                loser.ArenaPoints -= (uint)diff;
                if (loser.ArenaPoints > 80000)
                    loser.ArenaPoints = 0;

                winner.ArenaStatistic.TodayWin++;
                winner.ArenaStatistic.TotalWin++;
                loser.ArenaStatistic.TodayBattles++;
                loser.ArenaStatistic.TotalLose++;

                //if (winner.ArenaStatistic.TodayBattles == 100 && winner.Player.ConquerPoints >= 100000000 && winner.Player.VIPLevel >= 5)
                //{
                //    winner.CurrentHonor += 500;
                //    winner.HistoryHonor += 500;
                //    winner.Player.RevoraPoint += 10000;
                //    winner.Player.ConquerPoints += 10000000;
                //    winner.Player.Money += 200000000;
                //    winner.Inventory.Add(3009002, 0, 1);
                //    winner.Inventory.Add(3004243, 0, 2);
                //    winner.Inventory.Add(3004244, 0, 2);
                //    winner.Inventory.Add(3004245, 0, 1);
                //    winner.Send(new Message("Usted ganó 50 pelea en Arena, y recibió un Premio de Arena!", System.Drawing.Color.Red, Message.System));
                //}
                if (Revora.Config.ArenaFiftyLossesPenaltyEnabled && loser.ArenaStatistic.TodayBattles == 50 && winner.Entity.VIPLevel >= Revora.Config.ArenaHundredWinsMinVip)
                {
                    lock(loser.ItemSyncRoot) { loser.CurrentHonor += 500; loser.HistoryHonor += 500; }
                    loser.Send(new Message("You have lost 50 Arena matches.", System.Drawing.Color.Red, Message.System));
                }
                if (winner.ArenaStatistic.TodayWin == 100 && winner.Entity.VIPLevel >= Revora.Config.ArenaHundredWinsMinVip)
                {
                    lock(winner.ItemSyncRoot) { winner.CurrentHonor += 500; winner.HistoryHonor += 500; }
                    winner.Entity.AddRevoraPoints(Revora.Config.ArenaHundredWinsRevoraPoints);
                    winner.Entity.AddConquerPoints(Revora.Config.ArenaHundredWinsCp);
                    winner.Entity.AdjustMoney((decimal)(Revora.Config.ArenaHundredWinsGold));
                    winner.Inventory.Add(3009002, 0, 1);
                    winner.Inventory.Add(3004243, 0, 2);
                    winner.Inventory.Add(3004244, 0, 2);
                    winner.Inventory.Add(3004245, 0, 1);
                    winner.Send(new Message("You have won 100 Arena matches: " + Revora.Config.ArenaHundredWinsCp + " CPs, " + Revora.Config.ArenaHundredWinsGold + " gold, " + Revora.Config.ArenaHundredWinsRevoraPoints + " Revora Points.", System.Drawing.Color.Red, Message.System));
                }

                Sort();
                //     winner.Activenes.SendSinglePacket(winner, Activeness.Types.QualiferTask, (byte)winner.ArenaStatistic.TodayBattles);
                //   loser.Activenes.SendSinglePacket(loser, Activeness.Types.QualiferTask, (byte)loser.ArenaStatistic.TodayBattles);
                winner.Send(winner.ArenaStatistic);
                loser.Send(loser.ArenaStatistic);

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
                if (winner.ArenaStatistic.Rank > 0)
                {
                    builder.Append(" in the Arena, and now stands at rank No. "); builder.Append(winner.ArenaStatistic.Rank);
                }
                else { builder.Append(" in the Arena"); }

                builder.Append(".");
                if (loser.ArenaStatistic.Rank <= 500 && loser.ArenaStatistic.Rank != 0)
                {
                    if (loser.ArenaStatistic.Rank <= 3)
                        loser.Send(new Message("You now stand at rank No. " + loser.ArenaStatistic.Rank + " in the Arena! You are among the top 3!", System.Drawing.Color.Red, Message.System));
                    else if (loser.ArenaStatistic.Rank <= 10)
                        loser.Send(new Message("You now stand at rank No. " + loser.ArenaStatistic.Rank + " in the Arena! You are among the top 10!", System.Drawing.Color.Red, Message.System));
                    else if (loser.ArenaStatistic.Rank <= 20)
                        loser.Send(new Message("You now stand at rank No. " + loser.ArenaStatistic.Rank + " in the Arena! You are among the top 20!", System.Drawing.Color.Red, Message.System));
                    else if (loser.ArenaStatistic.Rank <= 50)
                        loser.Send(new Message("You now stand at rank No. " + loser.ArenaStatistic.Rank + " in the Arena! You are among the top 50!", System.Drawing.Color.Red, Message.System));
                    else if (loser.ArenaStatistic.Rank <= 150)
                        loser.Send(new Message("You now stand at rank No. " + loser.ArenaStatistic.Rank + " in the Arena! You are among the top 150!", System.Drawing.Color.Red, Message.System));
                    else if (loser.ArenaStatistic.Rank <= 500)
                        loser.Send(new Message("You now stand at rank No. " + loser.ArenaStatistic.Rank + " in the Arena! You are among the top 500!", System.Drawing.Color.Red, Message.System));
                }
                if (winner.ArenaStatistic.Rank <= 500 && winner.ArenaStatistic.Rank != 0)
                {
                    if (winner.ArenaStatistic.Rank <= 3)
                        winner.Send(new Message("You now stand at rank No. " + winner.ArenaStatistic.Rank + " in the Arena! You are among the top 3!", System.Drawing.Color.Red, Message.System));
                    else if (winner.ArenaStatistic.Rank <= 10)
                        winner.Send(new Message("You now stand at rank No. " + winner.ArenaStatistic.Rank + " in the Arena! You are among the top 10!", System.Drawing.Color.Red, Message.System));
                    else if (winner.ArenaStatistic.Rank <= 20)
                        winner.Send(new Message("You now stand at rank No. " + winner.ArenaStatistic.Rank + " in the Arena! You are among the top 20!", System.Drawing.Color.Red, Message.System));
                    else if (winner.ArenaStatistic.Rank <= 50)
                        winner.Send(new Message("You now stand at rank No. " + winner.ArenaStatistic.Rank + " in the Arena! You are among the top 50!", System.Drawing.Color.Red, Message.System));
                    else if (winner.ArenaStatistic.Rank <= 150)
                        winner.Send(new Message("You now stand at rank No. " + winner.ArenaStatistic.Rank + " in the Arena! You are among the top 150!", System.Drawing.Color.Red, Message.System));
                    else if (winner.ArenaStatistic.Rank <= 500)
                        winner.Send(new Message("You now stand at rank No. " + winner.ArenaStatistic.Rank + " in the Arena! You are among the top 500!", System.Drawing.Color.Red, Message.System));
                }
                foreach (var client in Kernel.GamePool.Values)
                    client.Send(new Message(builder.ToString(), System.Drawing.Color.Red, Message.ArenaQualifier));
                winner.QualifierGroup = null;
                loser.QualifierGroup = null;

                ArenaSignup sign = new ArenaSignup();
                sign.Stats = loser.ArenaStatistic;
                sign.DialogID = ArenaSignup.MainIDs.Dialog2;
                loser.Send(sign.BuildPacket());
                sign.Stats = winner.ArenaStatistic;
                sign.OptionID = ArenaSignup.DialogButton.Win;
                winner.Send(sign.BuildPacket());

                Clear(loser);
                loser.Send(loser.ArenaStatistic);

                Clear(winner);
                winner.Send(winner.ArenaStatistic);
            }
        }

        private static DateTime YesterdaySorted = DateTime.Now;

        public static void Clear(Client.GameState client)
        {
            if (client == null || client.ArenaStatistic == null) return;
            var arena_stat = client.ArenaStatistic;
            arena_stat.AcceptBox = false;
            arena_stat.AcceptBoxShow = Time32.Now;
            arena_stat.PlayWith = null;
            arena_stat.Status = QualifierStatistic.NotSignedUp;
            client.Send(arena_stat);
            client.ArenaState = Qualifier.None;
            client.QualifierGroup = null;
            client.TeamQualifierGroup = null;
            Qualifier.WaitingPlayerList.Remove(arena_stat.EntityID);
            Qualifier.PlayerList.Remove(arena_stat.EntityID);
        }

        public const int
            None = 0,
            FindMatch = 1,
            WaitForBox = 2,
            WaitForOther = 3,
            Fight = 4;

        private static readonly object matchmakingGate = new object();
        public static void EngagePlayers()
        {
            using (var admission = Features.ArenaShutdownDrain.Enter())
            {
                if (admission == null) return;
                if(!Monitor.TryEnter(matchmakingGate))return;
                try { EngagePlayersCore(); Features.ArenaBots.Fill(); }
                finally { Monitor.Exit(matchmakingGate); }
            }
        }
        private static void EngagePlayersCore()
        {
            if (PlayerList.Count < 2)
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

                if (CanJoin(Challanger, Now) && Challanger.ArenaState == FindMatch)
                {
                    for (; j != jEnd; j += jPlus)
                    {
                        var Challanged = Players[j];
                        if (CanJoin(Challanged, Now) && Challanged.ArenaState == FindMatch)
                        {
                            if (Challanger.Entity.UID != Challanged.Entity.UID)
                            {
                                Challanger.ArenaState = Challanged.ArenaState = WaitForBox;
                                Challanged.ArenaStatistic.AcceptBoxShow = Challanger.ArenaStatistic.AcceptBoxShow = Time32.Now;
                                Challanged.ArenaStatistic.Status = Network.GamePackets.QualifierStatistic.WaitingInactive;
                                Challanger.ArenaStatistic.Status = Network.GamePackets.QualifierStatistic.WaitingInactive;
                                Challanged.Send(Challanged.ArenaStatistic);
                                Challanger.Send(Challanger.ArenaStatistic);
                                Challanged.ArenaStatistic.PlayWith = Challanger;
                                Challanger.ArenaStatistic.PlayWith = Challanged;
                                ArenaSignup sign = new ArenaSignup();
                                sign.DialogID = ArenaSignup.MainIDs.StartCountDown;
                                sign.Stats = Challanged.ArenaStatistic;
                                Challanged.Send(sign.BuildPacket());
                                sign.Stats = Challanger.ArenaStatistic;
                                Challanger.Send(sign.BuildPacket());
                                PlayerList.Remove(Challanged.Account.EntityID);
                                PlayerList.Remove(Challanger.Account.EntityID);
                                WaitingPlayerList.Add(Challanged.Account.EntityID, Challanged);
                                WaitingPlayerList.Add(Challanger.Account.EntityID, Challanger);
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
                if (Challanger != null)
                {
                    if (Challanger.ArenaStatistic == null)
                    {
                        WaitingPlayerList.Remove(Challanger.Entity.UID);
                        continue;
                    }
                    if (Challanger.ArenaStatistic.PlayWith != null)
                    {
                        Client.GameState Challanged = Challanger.ArenaStatistic.PlayWith;
                        if(Features.ArenaStartupPreparation.Prepared && SignupWithdrawals.Pending(Challanged))continue;
                        if (Challanger.ArenaState == WaitForBox || Challanged.ArenaState == WaitForBox)
                        {
                            if (Time32.Now > Challanger.ArenaStatistic.AcceptBoxShow.AddSeconds(60))
                            {
                                if (Challanger.ArenaState == WaitForBox)
                                {
                                    Win(Challanged, Challanger);
                                }
                                else
                                {
                                    Win(Challanger, Challanged);
                                }
                                return;
                            }
                        }
                        if (Challanger.ArenaState == WaitForOther && !Challanger.ArenaStatistic.AcceptBox)
                        {
                            Win(Challanged, Challanger);
                        }
                        else if (Challanged.ArenaState == WaitForOther && !Challanged.ArenaStatistic.AcceptBox)
                        {
                            Win(Challanger, Challanged);
                        }
                        else if (Challanger.ArenaState == WaitForOther && Challanged.ArenaState == WaitForOther)
                        {
                            if (!Challanger.ArenaStatistic.AcceptBox || !Challanged.ArenaStatistic.AcceptBox)
                            {
                                if (!Challanger.ArenaStatistic.AcceptBox)
                                {
                                    Win(Challanged, Challanger);
                                }
                                else
                                {
                                    Win(Challanger, Challanged);
                                }
                            }
                            else
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
                                if(Features.ArenaStartupPreparation.Prepared) {
                                    ImportAccepted(Challanger,Challanged);continue;
                                }
                                Challanger.ArenaState = Challanged.ArenaState = Fight;
                                QualifierList.QualifierGroup group = new QualifierList.QualifierGroup(Challanger, Challanged);
                                ImportMatched(group,ownedEntries);
                            }
                        }
                    }
                }
            }
        }
        private static void ImportAccepted(GameState a,GameState b) {
            if(a==null || b==null || ReferenceEquals(a,b))return;
            var owners=new[]{a,b}.OrderBy(p=>p.Entity.UID).ToArray();int acquired=0;
            try {
                foreach(var p in owners){if(!Monitor.TryEnter(p.ItemSyncRoot))return;acquired++;}
                GameState queued;
                if(!WaitingPlayerList.TryGetValue(a.Entity.UID,out queued) || !ReferenceEquals(queued,a)
                    || !WaitingPlayerList.TryGetValue(b.Entity.UID,out queued) || !ReferenceEquals(queued,b)
                    || !Features.ArenaDayRollover.PrepareAccepted(a,b))return;
                a.ArenaState=b.ArenaState=Fight;
                ImportMatched(new QualifierList.QualifierGroup(a,b),ownedEntries);
            }catch(Exception ex){Console.WriteLine("[arena-admission] error="+ex.GetType().Name);}
            finally{for(int n=acquired-1;n>=0;n--)Monitor.Exit(owners[n].ItemSyncRoot);}
        }
        public static void CheckGroups()
        {
            SignupWithdrawals.Tick();
            Features.ArenaDisconnectRetention.TickScheduled();
            Features.ArenaRankingSettlement.Tick();
            if (QualifierList.Groups.Count > 0)
            {
                foreach (var group in QualifierList.Groups.Values)
                {
                    if(group.CompletionOwner!=null)continue;
                    if(group.OwnedEntryRequested){group.RejectUnimported();continue;}
                    if (Time32.Now > group.CreateTime.AddSeconds(5))
                    {
                        if (!group.Done)
                        {
                            if (Time32.Now > group.CreateTime.AddMinutes(3))
                            {
                                group.End();
                            }
                        }
                        else
                        {
                            if (Time32.Now > group.DoneStamp.AddSeconds(4))
                            {
                                if (group.Export()) Win(group.Winner, group.Loser);
                            }
                        }
                    }
                }
            }
        }

        private static bool DayPassed = false;
        private static DateTime StartDateTime = DateTime.Now;
        public static void Reset()
        {
            if (!DayPassed)
            {
                if (DateTime.Now.DayOfYear != StartDateTime.DayOfYear)
                {
                    DayPassed = true;
                    var array = ArenaStatistics.Values.ToArray();
                    foreach (QualifierStatistic stat in array)
                    {
                        try
                        {
                            if (Kernel.GamePool.ContainsKey(stat.EntityID))
                            {
                                Client.GameState client = Kernel.GamePool[stat.EntityID];
                                Database.ArenaTable.Reset(client, stat);
                                client.Send(stat);
                            }
                            else
                                Database.ArenaTable.Reset(null, stat);
                        }
                        catch { }
                    }
                    try
                    {
                        SaveArenaStats();
                    }
                    catch
                    {
                    }
                    YesterdaySort();
                }
            }
            else
            {
                if (DateTime.Now.DayOfYear == StartDateTime.DayOfYear)
                {
                    DayPassed = false;
                    StartDateTime = DateTime.Now;
                }
            }
        }

        internal static bool CanJoin(Client.GameState client, Time32 now)
        {
            if(Features.ArenaStartupPreparation.Prepared && SignupWithdrawals.Pending(client))return false;
            if (client != null)
            {
                if (client.Entity.MapID == 1081) return false;
                #region MaxsMap

                if (client.Entity.MapID == 1766 || client.Entity.MapID == 8877
                    || client.Entity.MapID == 1734 || client.Entity.MapID == 1733
                    || client.Entity.MapID == 1731 || client.Entity.MapID == 1732
                    || client.Entity.MapID == 1735 || client.Entity.MapID == 3842
                    || client.Entity.MapID == 3820 || client.Entity.MapID == 8839
                    || client.Entity.MapID == 1826 || client.Entity.MapID == 3055
                    || client.Entity.MapID == 3844 || client.Entity.MapID == 3845
                    || client.Entity.MapID == 38200 || client.Entity.MapID == 4573
                    || client.Entity.MapID == 4574 || client.Entity.MapID == 4575
                    || client.Entity.MapID == 4576 || client.Entity.MapID == 4577
                    || client.Entity.MapID == 4578)
                    return false;

                #endregion
                if (client.ArenaStatistic.PlayWith == null)
                {
                    if (client.QualifierGroup == null)
                    {
                        if (client.Map.BaseID == 1038 || client.Map.BaseID == 2072 || client.Map.BaseID == 2073 || client.Map.BaseID == 2071 || client.Map.BaseID == 2074 || client.Map.BaseID == 2075) return false;
                        if (client.Map.BaseID == 700) return false;
                        if (client.Entity.MapID >= 1090 && client.Entity.MapID <= 1094) return false;
                        if (client.Entity.MapID >= 1505 && client.Entity.MapID <= 1509) return false;
                        if (client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.SoulShackle)) return false;
                        if (client.Map.ID == 1002 || client.Map.ID == 1005 || !Constants.PKFreeMaps.Contains(client.Map.ID))
                            if (client.ArenaStatistic.Status == Network.GamePackets.QualifierStatistic.WaitingForOpponent)
                                return true;
                    }
                }
            }
            return false;
        }
    }
}
