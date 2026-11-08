// * Programador y editor de Sources Chavarro
// * Derechos Reservados © 2015 - 2016
// * Slayer [ Server Privado ] - Proyecto TQ

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using COServer.Client;
using COServer.Network.GamePackets;
using System.Threading;
using System.Globalization;
using System.Collections.Concurrent;
using COServer.Game;
using System.Drawing;

namespace COServer.Game
{
    public unsafe class TeamQualifier
    {
        public static ConcurrentDictionary<uint, GameState> PlayerList = new ConcurrentDictionary<uint, GameState>();
        public static ConcurrentDictionary<uint, GameState> WaitingPlayerList = new ConcurrentDictionary<uint, GameState>();
        public static ConcurrentDictionary<uint, TeamQualifierStatistic> ArenaStatistics = new ConcurrentDictionary<uint, TeamQualifierStatistic>();
        public static SafeDictionary<uint, TeamQualifierStatistic> YesterdayArenaStatistics = new SafeDictionary<uint, TeamQualifierStatistic>();
        public static List<TeamQualifierStatistic> ArenaStatisticsList, YesterdayArenaStatisticsList;
        public static int InArenaCount = 0, HistoryArenaCount = 0;

        public static void Sort()
        {
            var where = ArenaStatistics.Values;
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

                            wtr.Write((uint)client.Player.UID);
                            wtr.Write((uint)client.Player.Level);
                            wtr.Write((uint)client.Player.Class);
                            wtr.Write((uint)client.Player.Body / 1000);
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
                        wtr.Write((uint)client.Player.Mesh);
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
                    if (!client.Player.Dead && client.WatchingGroup == null && client.TeamQualifierGroup == null)
                    {
                        client.Send(BuildWatcherPacket());
                        client.Send(this.MatchPacket.BuildPacket());

                        Watchers.Add(client);
                        byte[] data = BuildWatcherPacket(Watchers);
                        foreach (Client.GameState client2 in Watchers)
                            client.Send(data);
                        Player1.Team.SendMessage(data);
                        Player2.Team.SendMessage(data);
                        client.TeamWatchingGroup = this;
                        client.Player.Teleport(1005, dynamicMap.ID, (ushort)Kernel.Random.Next(35, 70), (ushort)Kernel.Random.Next(35, 70));
                    }
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
                public Time32 ImportTime;
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
                    Inside = true;

                    if (!Kernel.Maps.ContainsKey(700))
                        new Map(700, Database.DMaps.MapPaths[700]);
                    Map origMap = Kernel.Maps[700];
                    dynamicMap = origMap.MakeDynamicMap();

                    ushort t1X = (ushort)Kernel.Random.Next(35, 70),
                           t1Y = (ushort)Kernel.Random.Next(35, 70),
                           t2X = (ushort)Kernel.Random.Next(35, 70),
                           t2Y = (ushort)Kernel.Random.Next(35, 70);

                    foreach (var team1Player in Player1.Team.Teammates)
                    {
                        if (CanFight(team1Player))
                        {
                            team1Player.Player.Teleport(origMap.ID, dynamicMap.ID, t1X, t1Y);
                            if (team1Player.Player.ContainsFlag((ulong)FlagsUpdate.Flags.Ride))
                            {
                                team1Player.Player.RemoveFlag((ulong)FlagsUpdate.Flags.Ride);
                            }
                            team1Player.TeamQualifierGroup = this;
                        }
                    }

                    foreach (var team2Player in Player2.Team.Teammates)
                    {
                        if (CanFight(team2Player))
                        {
                            team2Player.Player.Teleport(origMap.ID, dynamicMap.ID, t2X, t2Y);
                            if (team2Player.Player.ContainsFlag((ulong)FlagsUpdate.Flags.Ride))
                            {
                                team2Player.Player.RemoveFlag((ulong)FlagsUpdate.Flags.Ride);
                            }
                            team2Player.TeamQualifierGroup = this;
                        }
                    }
                    var team1Op = new QualifierParticipants(Player2, QualifierParticipants.KindOfParticipants.Opponents);
                    foreach (var team1Player in Player1.Team.Teammates)
                    {
                        if (team1Player.TeamQualifierGroup != null)
                        {
                            team1Player.Send(team1Op.Build(team1Player.Player.UID));
                            ArenaSignup sign = new ArenaSignup();
                            sign.DialogID = ArenaSignup.MainIDs.StartTheFight;
                            sign.Stats = Player2.TeamArenaStatistic;
                            team1Player.Send(sign.BuildPacket());
                            sign.DialogID = ArenaSignup.MainIDs.Match;
                            sign.OptionID = ArenaSignup.DialogButton.MatchOn;
                            team1Player.Send(sign.BuildPacket());
                            team1Player.Send(MatchPacket.BuildPacket());
                            team1Player.Player.BringToLife();
                            team1Player.Player.RemoveFlag((ulong)Network.GamePackets.FlagsUpdate.Flags.Ride);
                            team1Player.PrevPK = team1Player.Player.PKMode;
                            team1Player.Player.PKMode = Game.Enums.PKMode.Team;
                            team1Player.Send(new Data(true) { UID = team1Player.Player.UID, ID = Data.ChangePKMode, dwParam = (uint)team1Player.Player.PKMode });
                        }
                    }
                    var team2Op = new QualifierParticipants(Player1, QualifierParticipants.KindOfParticipants.Opponents);
                    foreach (var team2Player in Player2.Team.Teammates)
                    {
                        if (team2Player.TeamQualifierGroup != null)
                        {
                            team2Player.Send(team2Op.Build(team2Player.Player.UID));
                            ArenaSignup sign = new ArenaSignup();
                            sign.DialogID = ArenaSignup.MainIDs.StartTheFight;
                            sign.Stats = Player1.TeamArenaStatistic;
                            team2Player.Send(sign.BuildPacket());
                            sign.DialogID = ArenaSignup.MainIDs.Match;
                            sign.OptionID = ArenaSignup.DialogButton.MatchOn;
                            team2Player.Send(sign.BuildPacket());
                            team2Player.Send(MatchPacket.BuildPacket());
                            team2Player.Player.BringToLife();
                            team2Player.Player.RemoveFlag((ulong)Network.GamePackets.FlagsUpdate.Flags.Ride);
                            team2Player.PrevPK = team2Player.Player.PKMode;
                            team2Player.Player.PKMode = Game.Enums.PKMode.Team;
                            team2Player.Send(new Data(true) { UID = team2Player.Player.UID, ID = Data.ChangePKMode, dwParam = (uint)team2Player.Player.PKMode });
                        }
                    }
                    ImportTime = Time32.Now;
                }
                public void Export()
                {
                    Groups.Remove(ID);

                    if (dynamicMap != null)
                        dynamicMap.Dispose();

                    var arr = Watchers.ToArray();
                    foreach (Client.GameState client in arr)
                        QualifyEngine.DoLeave(client);
                    arr = null;
                    Win(Winner, Loser);

                    Inside = false;
                    foreach (var team1Player in Player1.Team.Teammates)
                    {
                        if (team1Player.Map.ID == dynamicMap.ID)
                        {
                            team1Player.Player.PreviousTeleport();
                            if (team1Player.Map.BaseID == 700)
                                team1Player.Player.Teleport(1002, 400, 400);
                            team1Player.Player.Ressurect();
                            team1Player.Player.PKMode = team1Player.PrevPK;
                            team1Player.Send(new Data(true) { UID = team1Player.Player.UID, ID = Data.ChangePKMode, dwParam = (uint)team1Player.Player.PKMode });
                            team1Player.TeamQualifierGroup = null;

                            team1Player.Player.ToxicFogLeft = 0;
                            team1Player.Player.NoDrugsTime = 1 - 0;
                            team1Player.Player.RemoveFlag((ulong)FlagsUpdate.Flags.Poisoned);
                            team1Player.EndTeam = false;
                        }
                    }
                    foreach (var team2Player in Player2.Team.Teammates)
                    {
                        if (team2Player.Map.ID == dynamicMap.ID)
                        {
                            team2Player.Player.PreviousTeleport();
                            if (team2Player.Map.BaseID == 700)
                                team2Player.Player.Teleport(1002, 400, 400);
                            team2Player.Player.Ressurect();
                            team2Player.Player.PKMode = team2Player.PrevPK;
                            team2Player.Send(new Data(true) { UID = team2Player.Player.UID, ID = Data.ChangePKMode, dwParam = (uint)team2Player.Player.PKMode });
                            team2Player.TeamQualifierGroup = null;
                            team2Player.Player.ToxicFogLeft = 0;
                            team2Player.Player.NoDrugsTime = 1 - 0;
                            team2Player.Player.RemoveFlag((ulong)FlagsUpdate.Flags.Poisoned);
                            team2Player.EndTeam = false;
                        }
                    }
                    Player1.TeamArenaStatistic.AcceptBox = Player2.TeamArenaStatistic.AcceptBox = false;
                    Player1.TeamArenaStatistic.AcceptBoxShow = Player2.TeamArenaStatistic.AcceptBoxShow = Player2.TeamArenaStatistic.AcceptBoxShow.AddHours(-2);
                    Player1.TeamArenaStatistic.PlayWith = Player2.TeamArenaStatistic.PlayWith = null;
                }
                public void End()
                {
                    if (Finished) return;
                    Finished = true;
                    Player1.EndTeam = true;
                    Player2.EndTeam = true;
                    if (Player1Damage > Player2Damage)
                    {
                        Winner = Player1;
                        Loser = Player2;
                    }
                    else
                    {
                        Winner = Player2;
                        Loser = Player1;
                    }
                    if (Inside)
                    {
                        var sign = new ArenaSignup();
                        sign.Stats = Loser.TeamArenaStatistic;
                        sign.DialogID = ArenaSignup.MainIDs.Dialog;
                        sign.OptionID = ArenaSignup.DialogButton.Lose;
                        Loser.Send(sign.BuildPacket());
                        sign.OptionID = ArenaSignup.DialogButton.Win;
                        sign.Stats = Winner.TeamArenaStatistic;
                        Winner.Send(sign.BuildPacket());
                    }
                    Done = true;
                    DoneStamp = Time32.Now;
                }
                public void End(GameState loser)
                {
                    if (!loser.Team.TeamLeader) return;
                    if (Finished) return;
                    Finished = true;
                    Player1.EndTeam = true;
                    Player2.EndTeam = true;
                    if (Player1.Account.EntityID == loser.Account.EntityID)
                    {
                        Winner = Player2;
                        Loser = Player1;
                    }
                    else
                    {
                        Winner = Player1;
                        Loser = Player2;
                    }
                    if (Inside)
                    {
                        var sign = new ArenaSignup();
                        sign.Stats = Loser.TeamArenaStatistic;
                        sign.DialogID = ArenaSignup.MainIDs.Dialog;
                        sign.OptionID = ArenaSignup.DialogButton.Lose;
                        Loser.Send(sign.BuildPacket());
                        sign.OptionID = ArenaSignup.DialogButton.Win;
                        sign.Stats = Winner.TeamArenaStatistic;
                        Winner.Send(sign.BuildPacket());
                    }
                    Done = true;
                    DoneStamp = Time32.Now;
                }
                public void CheckEnd(GameState client, bool disconnect = false)
                {
                    bool allDead = true;
                    if (client == null) return;
                    if (client.Team == null) return;
                    if (client.Team.Teammates == null) return;
                    foreach (var teammate in client.Team.Teammates)
                        if (teammate.InTeamQualifier())
                            allDead &= teammate.Player.Dead;
                    if (client.Team.TeamLeader && disconnect)
                        End(client);
                    else if (allDead)
                        if (client.Team.TeamLeader)
                            End(client);
                        else
                            foreach (var teammate in client.Team.Teammates)
                                if (teammate.Team.TeamLeader)
                                    End(teammate);
                }
                public void UpdateDamage(GameState client, uint damage, bool otherParty = false)
                {
                    if (client != null && Player1 != null)
                    {
                        if (client.Account.EntityID == Player1.Account.EntityID)
                        {
                            if (!otherParty)
                                Player1Damage += damage;
                            else
                                Player2Damage += damage;
                        }
                        else
                        {
                            if (!otherParty)
                                Player2Damage += damage;
                            else
                                Player1Damage += damage;
                        }
                        Player1.Team.SendMessage(MatchPacket.BuildPacket());
                        Player2.Team.SendMessage(MatchPacket.BuildPacket());
                    }
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
                PlayerList.Remove(client.Account.EntityID);
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
                        if (WaitingPlayerList.ContainsKey(client.Account.EntityID))
                        {
                            WaitingPlayerList.Remove(client.Account.EntityID);
                            WaitingPlayerList.Remove(client.TeamArenaStatistic.PlayWith.Account.EntityID);
                        }
                        Win(client.TeamArenaStatistic.PlayWith, client);
                        Clear(other);
                    }
                    Clear(client);
                }
            }
            public static void CreateTeam(Team team, Client.GameState client)
            {
                if (!client.Player.Dead && client.Team == null)
                {
                    Leadership lship = new Leadership();
                    lship.Type = 1;
                    lship.UID = client.Player.UID;
                    lship.LeaderUID = client.Player.UID;
                    lship.Count = 1;
                    client.Send(lship);
                    client.Player.AddFlag((ulong)FlagsUpdate.Flags.TeamLeader);
                    client.Team = new Game.ConquerStructures.Team(client);
                    client.Team.AddLider();
                    client.Team.TeamLeader = true;
                    client.Team.Add(client);
                    client.Send(team);
                }
            }
            public static void DoSignup(Client.GameState client)
            {
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
                    CreateTeam(new Network.GamePackets.Team() { UID = client.Player.UID, Type = Network.GamePackets.Team.Create }, client);
                if (client.Team == null)
                    return;
                client.TeamArenaStatistic.HasBox = false;
                if (client.TeamArenaStatistic.ArenaPoints == 0)
                    return;
                if (client.Player.MapID == 601) return;
                if (client.Map.BaseID >= 6000 && client.Map.BaseID <= 6003) return;

                if (WaitingPlayerList.ContainsKey(client.Account.EntityID))
                {
                    if (client.TeamQualifierGroup == null)
                        WaitingPlayerList.Remove(client.Account.EntityID);
                    else
                        return;
                }
                PlayerList.Add(client.Account.EntityID, client);
                client.TeamArenaStatistic.Status = Network.GamePackets.TeamQualifierStatistic.WaitingForOpponent;
                client.Send(client.TeamArenaStatistic);
                RequestGroupList(client, 1);
            }
            public static void DoGiveUp(Client.GameState client)
            {
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

                    if (WaitingPlayerList.ContainsKey(client.Account.EntityID))
                    {
                        WaitingPlayerList.Remove(client.Account.EntityID);
                        WaitingPlayerList.Remove(client.TeamArenaStatistic.PlayWith.Account.EntityID);
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
                if (client.TeamWatchingGroup != null)
                {
                    client.TeamWatchingGroup.Watchers.Remove(client);
                    client.Send(client.TeamWatchingGroup.BuildWatcherPacket(3));
                    byte[] data = client.TeamWatchingGroup.BuildWatcherPacket(client.TeamWatchingGroup.Watchers);
                    foreach (Client.GameState client2 in client.TeamWatchingGroup.Watchers)
                        client.Send(data);
                    client.TeamWatchingGroup.Player1.Team.SendMessage(data);
                    client.TeamWatchingGroup.Player2.Team.SendMessage(data);
                    client.TeamWatchingGroup = null;
                    client.Player.PreviousTeleport();
                }
            }
            public static void DoCheer(Client.GameState client, uint uid)
            {
                if (client.TeamWatchingGroup != null && !client.TeamWatchingGroup.Cheerers.Contains(client.Player.UID))
                {
                    client.TeamWatchingGroup.Cheerers.Add(client.Player.UID);
                    if (client.TeamWatchingGroup.Player1.Player.UID == uid)
                    {
                        client.TeamCheerFor = 1;
                        client.TeamWatchingGroup.Player1Cheers++;
                    }
                    else
                    {
                        client.TeamCheerFor = 2;
                        client.TeamWatchingGroup.Player2Cheers++;
                    }
                    byte[] data = client.TeamWatchingGroup.BuildWatcherPacket(client.TeamWatchingGroup.Watchers);
                    foreach (Client.GameState client2 in client.TeamWatchingGroup.Watchers)
                        client.Send(data);
                    client.TeamWatchingGroup.Player1.Team.SendMessage(data);
                    client.TeamWatchingGroup.Player2.Team.SendMessage(data);
                }
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
                public const uint
                ArenaIconOn = 0,
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
                public const uint
                Lose = 3,
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
        public static void Win(Client.GameState winner, Client.GameState loser)
        {
            if (winner.TeamArenaStatistic.PlayWith != null && loser.TeamArenaStatistic.PlayWith != null)
            {
                winner.TeamArenaStatistic.HasBox =
                    loser.TeamArenaStatistic.HasBox = false;

                int diff = Kernel.Random.Next(30, 50);
                if (winner.Team != null)
                {
                    if (winner.Team.Teammates != null)
                    {
                        foreach (var teammate in winner.Team.Teammates)
                        {
                            if (teammate.Team.TeamLeader)
                                teammate.ArenaPoints += (uint)diff;
                            else
                                teammate.ArenaPoints += (uint)(diff - 10);
                            teammate.TeamArenaStatistic.TodayWin++;
                            teammate.TeamArenaStatistic.TotalWin++;
                            if (teammate.TeamArenaStatistic.TodayWin == 9)
                                teammate.Inventory.Add(723912, 0, 1);
                            if (teammate.TeamArenaStatistic.TodayBattles == 20)
                                teammate.Inventory.Add(723912, 0, 1);
                            teammate.Send(teammate.TeamArenaStatistic);
                            teammate.TeamQualifierGroup = null;
                        }
                    }
                }
                if (loser.Team != null)
                {
                    if (loser.Team.Teammates != null)
                    {
                        foreach (var teammate in loser.Team.Teammates)
                        {
                            if (teammate.Team.TeamLeader)
                                teammate.ArenaPoints -= (uint)diff;
                            else
                                teammate.ArenaPoints -= (uint)(diff - 10);
                            teammate.TeamArenaStatistic.TodayBattles++;
                            teammate.TeamArenaStatistic.TotalLose++;
                            if (teammate.ArenaPoints > 80000)
                                teammate.ArenaPoints = 0;
                            if (teammate.TeamArenaStatistic.TodayBattles == 20)
                                teammate.Inventory.Add(723912, 0, 1);
                            teammate.Send(teammate.TeamArenaStatistic);
                            teammate.TeamQualifierGroup = null;
                        }
                    }
                }

                winner.TeamArenaStatistic.PlayWith = null;
                loser.TeamArenaStatistic.PlayWith = null;

                if (WaitingPlayerList.ContainsKey(loser.Account.EntityID))
                    WaitingPlayerList.Remove(loser.Account.EntityID);
                if (WaitingPlayerList.ContainsKey(winner.Account.EntityID))
                    WaitingPlayerList.Remove(winner.Account.EntityID);

                QualifyEngine.DoQuit(winner);
                QualifyEngine.DoQuit(loser);

                winner.TeamArenaStatistic.AcceptBox = false;
                loser.TeamArenaStatistic.AcceptBox = false;

                Sort();

                StringBuilder builder = new StringBuilder();
                if (winner.Player.GuildID != 0)
                {
                    builder.Append("(");
                    builder.Append(winner.Guild.Name);
                    builder.Append(") ");
                }
                builder.Append(winner.Player.Name);
                builder.Append(" has defeated ");

                if (loser.Player.GuildID != 0)
                {
                    builder.Append("(");
                    builder.Append(loser.Guild.Name);
                    builder.Append(") ");
                }
                builder.Append(loser.Player.Name);
                if (winner.TeamArenaStatistic.Rank > 0)
                {
                    builder.Append(" in the Qualifier, and is currently ranked No. "); builder.Append(winner.TeamArenaStatistic.Rank);
                }
                else { builder.Append(" in the Qualifier"); }

                builder.Append(".");

                Kernel.SendWorldMessage(new Message(builder.ToString(), System.Drawing.Color.Red, Message.ArenaQualifier), Server.GamePool);

                winner.TeamQualifierGroup = null;
                loser.TeamQualifierGroup = null;
                foreach (var teammate in loser.Team.Teammates)
                {
                    ArenaSignup sign = new ArenaSignup();
                    sign.Stats = teammate.TeamArenaStatistic;
                    sign.DialogID = ArenaSignup.MainIDs.Dialog2;
                    teammate.Send(sign.BuildPacket());
                }
                foreach (var teammate in winner.Team.Teammates)
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
                            if (Challanger.Player.UID != Challanged.Player.UID)
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
            foreach (var Challanger in WaitingPlayerList.Values)
            {
                if (Challanger.Team == null) continue;
                if (Challanger.Team.Teammates == null) continue;
                if (Challanger != null)
                {
                    if (Challanger.TeamArenaStatistic == null)
                    {
                        WaitingPlayerList.Remove(Challanger.Player.UID);
                        continue;
                    }
                    if (Challanger.TeamArenaStatistic.HasBox)
                    {
                        if (Challanger.TeamArenaStatistic.PlayWith != null && Challanger.TeamQualifierGroup == null)
                        {
                            Client.GameState Challanged = Challanger.TeamArenaStatistic.PlayWith;
                            if (Challanged != null)
                            {
                                if (Challanged.Team == null) continue;
                                if (Challanged.Team.Teammates == null) continue;
                                if (Challanged.TeamArenaStatistic.PlayWith != null && Challanged.TeamQualifierGroup == null)
                                {
                                    if (Challanger.TeamArenaStatistic.AcceptBox && Challanged.TeamArenaStatistic.AcceptBox)
                                    {
                                        if (Challanged.Player.MapID == 1081 || Challanged.Player.ContainsFlag2((ulong)FlagsUpdate.Flags2.SoulShackle))
                                        {
                                            Qualifier.QualifyEngine.DoQuit(Challanged);
                                            continue;
                                        }
                                        if (Challanger.Player.MapID == 1081 || Challanger.Player.ContainsFlag2((ulong)FlagsUpdate.Flags2.SoulShackle))
                                        {
                                            Qualifier.QualifyEngine.DoQuit(Challanger);
                                            continue;
                                        }
                                        QualifierList.QualifierGroup group = new QualifierList.QualifierGroup(Challanger, Challanged);
                                        group.Import();
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
            if (QualifierList.Groups.Count > 0)
            {
                foreach (var group in QualifierList.Groups.Values)
                {
                    if (Time32.Now > group.CreateTime.AddSeconds(5))
                    {
                        if (group.Inside)
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
                                    group.Done = false;
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
                                    if (OpponentTeamMember.Player.UID != OpponentTeam.Player.UID)
                                    {
                                        OpponentTeamMember.Send(signup.BuildPacket());
                                    }
                                }
                                #endregion
                                #region Sending Accept Box to Client's Team Teammates
                                foreach (var TeamMember in client.Team.Teammates)
                                {
                                    if (TeamMember.Player.UID != OpponentTeam.Player.UID)
                                    {
                                        TeamMember.Send(signup.BuildPacket());
                                    }
                                }
                                #endregion
                                client.TeamArenaStatistic.HasBox = OpponentTeam.TeamArenaStatistic.HasBox = true;
                                PlayerList.Remove(OpponentTeam.Account.EntityID);
                                PlayerList.Remove(client.Account.EntityID);
                                WaitingPlayerList.Add(OpponentTeam.Account.EntityID, OpponentTeam);
                                WaitingPlayerList.Add(client.Account.EntityID, client);
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
            if (client.ArenaPoints <= 0) return false;
            if (client.Player.Level <= 70) return false;
            if (client.Player.ContainsFlag2((ulong)FlagsUpdate.Flags2.SoulShackle)) return false;
            if (client.Map.BaseID == 1038) return false;
            if (client.Map.BaseID == 700) return false;
            if (client.Player.MapID >= 1090 && client.Player.MapID <= 1094) return false;
            if (client.Player.MapID >= 1505 && client.Player.MapID <= 1509) return false;
            if (client.Player.MapID == 1081) return false;
            return (!Constants.PKFreeMaps.Contains(client.Map.ID) || client.Map.ID == 1005);
        }
        private static bool CanJoin(Client.GameState client, Time32 now)
        {
            if (client != null)
            {
                if (client.TeamArenaStatistic.PlayWith == null)
                {
                    if (client.TeamQualifierGroup == null)
                    {
                        if (!client.Player.ContainsFlag((ulong)FlagsUpdate.Flags.TeamLeader)) return false;
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