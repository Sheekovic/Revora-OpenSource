using System;
using System.IO;
using System.Linq;
using System.Text;
using COServer.Client;
using System.Collections.Generic;
using COServer.Network.GamePackets;
using System.Collections.Concurrent;
using COServer.Game.Features.Tournaments;

namespace COServer.Game
{
    public unsafe class ElitePK
    {
        public const int EventTime = 20;// Hora del Pk de Heroes
        public class States
        {
            public const byte
                GUI_Top8Ranking = 0,
                GUI_Knockout = 3,
                GUI_Top8Qualifier = 4,
                GUI_Top4Qualifier = 5,
                GUI_Top2Qualifier = 6,
                GUI_Top3 = 7,
                GUI_Top1 = 8,
                GUI_ReconstructTop = 9;
            public const byte
                T_Organize = 0,
                T_CreateMatches = 1,
                T_Wait = 2,
                T_Fights = 3,
                T_Finished = 4,
                T_ReOrganize = 5;
        }
        public FighterStats[] Top8 = new FighterStats[0];
        public ConcurrentDictionary<uint, GameState> Players;
        public ConcurrentDictionary<uint, Match> Matches;
        private Match[] MatchArray;
        private Match[] Top4MatchArray;
        private Match[] ExtendedMatchArray;
        private Counter MatchCounter;
        private ushort MatchIndex;
        public bool Alive = false;
        public DateTime ConstructTop8 = DateTime.Now;
        public Map WaitingArea;
        public int State;
        private int pState = States.T_Organize;
        public int GroupID;
        private bool willAdvance;
        private IDisposable Subscriber;
        private Time32 pStamp;
        private bool SentNoWagers;
        public DateTime EndDateTime;
        public ElitePK(int group)
        {
            if (!Alive)
            {
                Alive = true;
                Players = new ConcurrentDictionary<uint, GameState>();
                Matches = new ConcurrentDictionary<uint, Match>();

                GroupID = group;
                MatchCounter = new Counter((uint)(GroupID * 100000 + 100000));

                LoadTop8();

                WaitingArea = Kernel.Maps[(ushort)ElitePKTournament.WaitingAreaID].MakeDynamicMap();
                Constants.PKForbiddenMaps.Add(WaitingArea.ID);

                State = States.GUI_Top8Ranking;
                pState = States.T_Organize;
            }
        }

        private void SaveTop8()
        {
            int len = Top8.Count(p => p.UID != 0);
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(len);
                for (int i = 0; i < len; i++)
                {
                    writer.Write(Top8[i].UID);
                    writer.Write(Top8[i].Name);
                    writer.Write(Top8[i].Mesh);
                }
                Server.Vars["epk" + GroupID] = stream.ToArray();
            }
        }
        private void LoadTop8()
        {
            Top8 = new FighterStats[8];
            byte[] bytes = Server.Vars["epk" + GroupID];
            if (bytes == null)
            {
                for (int i = 0; i < 8; i++)
                    Top8[i] = new FighterStats(0, "", 0);
                SaveTop8();
                return;
            }
            using (var stream = new MemoryStream(bytes))
            using (var reader = new BinaryReader(stream))
            {
                int len = reader.ReadInt32();
                for (int i = 0; i < len; i++)
                    Top8[i] = new FighterStats(reader.ReadUInt32(), reader.ReadString(), reader.ReadUInt32());
                for (int i = len; i < 8; i++)
                    Top8[i] = new FighterStats(0, "", 0);
            }
        }
        public static bool GetReward(GameState client, out byte rank, out byte elitestage)
        {
            if (ElitePKTournament.Tournaments != null)

                for (byte x = 0; x < ElitePKTournament.Tournaments.Length; x++)
                {
                    if (ElitePKTournament.Tournaments[x] != null)
                    {
                        if (ElitePKTournament.Tournaments[x].Top8 != null)
                        {
                            for (byte i = 0; i < ElitePKTournament.Tournaments[x].Top8.Length; i++)
                            {
                                if (ElitePKTournament.Tournaments[x].Top8[i] != null)
                                {
                                    if (ElitePKTournament.Tournaments[x].Top8[i].UID == client.Player.UID)
                                    {

                                        if (ElitePKTournament.Tournaments[x].Top8[i].RceiveReward == 0)
                                        {
                                            rank = (byte)(i + 1);
                                            elitestage = x;
                                            ElitePKTournament.Tournaments[x].Top8[i].RceiveReward = 1;
                                            return true;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            rank = 0;
            elitestage = 0;
            return false;
        }
        public class FighterStats
        {
            public enum StatusFlag : int
            {
                None = 0,
                Fighting = 1,
                Lost = 2,
                Qualified = 3,
                Waiting = 4,
                Bye = 5,
                Inactive = 7,
                WonMatch = 8
            }
            public ConcurrentDictionary<uint, uint> Wagers;
            public string Name;
            public uint UID;
            public uint Mesh;
            public uint Wager;
            public uint Cheers;
            public uint Points;
            public StatusFlag Flag;
            public bool Advance;
            public byte RceiveReward;
            public bool Waiting { get { return Flag == StatusFlag.Waiting; } }
            public bool NoFlag { get { return Flag == StatusFlag.None; } }
            public bool Fighting { get { return Flag == StatusFlag.Fighting; } }
            public bool Winner { get { return Flag == StatusFlag.Qualified || Flag == StatusFlag.WonMatch; } }
            public bool Lost { get { return Flag == StatusFlag.Lost; } }

            public FighterStats(uint id, string name, uint mesh)
            {
                Wagers = new ConcurrentDictionary<uint, uint>();
                UID = id;
                Name = name;
                Mesh = mesh;
            }

            public void Reset(bool setflag = false)
            {
                Wagers.Clear();
                Wager = 0;
                Points = 0;
                Cheers = 0;
                if (setflag) Flag = StatusFlag.None;

            }

            public FighterStats Clone()
            {
                FighterStats stats = new FighterStats(UID, Name, Mesh);
                stats.Points = this.Points;
                stats.Flag = this.Flag;
                stats.Wager = this.Wager;
                return stats;
            }
        }
        public class Match
        {
            public enum StatusFlag : int
            {
                AcceptingWagers = 0,
                Watchable = 1,
                SwitchOut = 2,
                InFight = 3,
                OK = 4,
                FinalMatch = 5,
            }
            public GameState[] Players;
            public FighterStats[] MatchStats;
            public FighterStats[] FightersStats { get { return MatchStats.Where(p => p.NoFlag | p.Fighting).ToArray(); } }

            public bool OnGoing
            {
                get { return Flag != StatusFlag.AcceptingWagers; }
            }
            public int GroupID
            {
                get { return (int)ID / 100000 - 1; }
            }
            public uint TimeLeft
            {
                get
                {
                    int val = (int)((ImportTime.AddMinutes(3).TotalMilliseconds - Time32.Now.TotalMilliseconds) / 1000);
                    if (val < 0) val = 0;
                    return (uint)val;
                }
            }
            public ConcurrentDictionary<uint, GameState> Watchers;

            public uint ID;

            public StatusFlag Flag;
            public bool Inside;
            public bool Done;
            public bool Exported;

            public bool Completed;
            public ushort Index;

            private Time32 ImportTime;
            private Map Map;
            private Time32 DoneStamp;

            public int Imports;
            public uint TotalWagers;


            public Match(params GameState[] players)
            {
                Players = players;
                Watchers = new ConcurrentDictionary<uint, GameState>();
                MatchStats = new FighterStats[players.Length];
                if (players.Length == 1)
                {
                    Players[0].ElitePKMatch = this;
                    MatchStats[0] = players[0].ElitePKStats;
                    MatchStats[0].Reset(true);
                    MatchStats[0].Flag = FighterStats.StatusFlag.Qualified;
                }
                else
                {
                    Players = players;
                    for (int i = 0; i < players.Length; i++)
                    {
                        players[i].ElitePKMatch = this;
                        MatchStats[i] = players[i].ElitePKStats;
                        MatchStats[i].Reset(true);
                    }
                    if (MatchStats.Length == 3)
                        if (Players[0] != null)
                            MatchStats[0].Flag = ElitePK.FighterStats.StatusFlag.Waiting;
                }
                Imports = 0;
                Flag = StatusFlag.AcceptingWagers;
            }

            public void Import(StatusFlag flag = StatusFlag.OK)
            {
                Imports++;
                Exported = Done = false;
                Inside = true;
                Flag = flag;
                ImportTime = Time32.Now;
                Map = Kernel.Maps[700].MakeDynamicMap();
                if (Players.Length > 1)
                {
                    GameState player1 = null, player2 = null;
                    for (int i = 0; i < Players.Length; i++)
                    {
                        if (!MatchStats[i].Waiting)
                        {
                            if (player1 == null)
                                player1 = Players[i];
                            else
                                player2 = Players[i];
                        }
                    }
                    if (!player1.Socket.Alive)
                    {
                        Exported = Done = true;
                        Flag = StatusFlag.OK;
                        if (Players.Length == 3)
                            Flag = StatusFlag.SwitchOut;
                        Completed = true;
                        var statsArray = FightersStats;
                        if (Players.Length == 3)
                            foreach (var stats in statsArray)
                                stats.Flag = FighterStats.StatusFlag.WonMatch;
                        else
                            foreach (var stats in statsArray)
                                stats.Flag = FighterStats.StatusFlag.Qualified;
                        player1.ElitePKStats.Flag = FighterStats.StatusFlag.Lost;
                        return;
                    }
                    if (!player2.Socket.Alive)
                    {
                        Exported = Done = true;
                        Flag = StatusFlag.OK;
                        if (Players.Length == 3)
                            Flag = StatusFlag.SwitchOut;
                        Completed = true;
                        var statsArray = FightersStats;
                        if (Players.Length == 3)
                            foreach (var stats in statsArray)
                                stats.Flag = FighterStats.StatusFlag.WonMatch;
                        else
                            foreach (var stats in statsArray)
                                stats.Flag = FighterStats.StatusFlag.Qualified;
                        player2.ElitePKStats.Flag = FighterStats.StatusFlag.Lost;
                        return;
                    }

                    player1.ElitePKStats.Flag = FighterStats.StatusFlag.Fighting;
                    player2.ElitePKStats.Flag = FighterStats.StatusFlag.Fighting;
                    importPlayer(player1, player2);
                    importPlayer(player2, player1);

                    ElitePKBrackets brackets = new ElitePKBrackets(true, 1);
                    brackets.Group = (ushort)GroupID;
                    brackets.GUIType = (ushort)ElitePKTournament.Tournaments[GroupID].State;
                    brackets.TotalMatches = brackets.MatchCount = 1;
                    brackets.Type = ElitePKBrackets.StaticUpdate;
                    brackets.Append(this);
                    Kernel.SendWorldMessage(brackets);
                }
                else
                {
                    Exported = Done = true;
                    Flag = StatusFlag.OK;
                    Completed = true;
                    foreach (var stats in MatchStats)
                        if (stats != null)
                            stats.Flag = FighterStats.StatusFlag.Qualified;
                }
            }
            private void importPlayer(GameState player, GameState opponent)
            {
                if (Players.Length > 1)
                {
                    var coords = Map.RandomCoordinates();
                    player.Player.Teleport(Map.BaseID, Map.ID, coords.Item1, coords.Item2);
                    player.Player.BringToLife();

                    ElitePKMatchUI ui = new ElitePKMatchUI(true);
                    ui.Append(opponent);
                    ui.TimeLeft = TimeLeft;
                    ui.Type = ElitePKMatchUI.BeginMatch;
                    player.Send(ui);

                    player.Send(CreateUpdate());
                    player.CantAttack = Time32.Now.AddSeconds(11);
                    player.Player.PrevPKMode = player.Player.PKMode;
                    player.Player.PKMode = Enums.PKMode.PK;

                    Data dat = new Data(true);
                    dat.UID = player.Player.UID;
                    dat.ID = Data.ChangePKMode;
                    dat.dwParam = (uint)player.Player.PKMode;
                    player.Send(dat);
                }
                player.Player.OnDeath = p => { p.Owner.ElitePKMatch.End(p.Owner); };
            }
            public void End(GameState client)
            {
                if (Players.Length == 1)
                {
                    Players[0].ElitePKStats.Flag = FighterStats.StatusFlag.Qualified;
                    Flag = StatusFlag.OK;
                    return;
                }
                if (Done) return;
                if (client.ElitePKStats.Flag != FighterStats.StatusFlag.Fighting) return;
                Done = true;
                DoneStamp = Time32.Now;
                Flag = StatusFlag.OK;
                client.ElitePKStats.Flag = FighterStats.StatusFlag.Lost;
                ElitePKMatchUI ui = new ElitePKMatchUI(true);
                ui.Type = ElitePKMatchUI.Effect;
                ui.dwParam = ElitePKMatchUI.Effect_Lose;
                client.Send(ui);

                client.ElitePKStats.Wager = 0;
                client.ElitePKStats.Wagers.Clear();
                var target = targetOf(client);
                if (target != null)
                {
                    ui.Append(client);
                    ui.dwParam = ElitePKMatchUI.Effect_Win;
                    target.Send(ui);
                    ui.Type = ElitePKMatchUI.EndMatch;
                    client.Send(ui);
                    target.Send(ui);
                    if (Imports == 2 || Players.Length != 3)
                    {
                        target.ElitePKStats.Flag = FighterStats.StatusFlag.Qualified;
                    }
                    else
                    {
                        Flag = StatusFlag.SwitchOut;
                        target.ElitePKStats.Flag = FighterStats.StatusFlag.None;
                    }
                    if (TotalWagers != 0)
                    {
                        double totalWager = target.ElitePKStats.Wager;
                        foreach (var kvp in target.ElitePKStats.Wagers)
                        {
                            double ratio = (double)kvp.Value / totalWager;
                            Client.GameState pClient;
                            if (Kernel.GamePool.TryGetValue(kvp.Key, out pClient))
                            {
                                uint gain = (uint)(ratio * totalWager * 10);
                                pClient.Player.Money += gain;
                                pClient.Send("You have gained " + gain + " gold from the winning of " + target.ElitePKStats.Name);
                            }
                        }
                        target.ElitePKStats.Wager = 0;
                        target.ElitePKStats.Wagers.Clear();
                        TotalWagers = 0;
                    }
                }

                ElitePKBrackets brackets = new ElitePKBrackets(true, 1);
                brackets.Group = (ushort)GroupID;
                brackets.GUIType = (ushort)ElitePKTournament.Tournaments[GroupID].State;
                brackets.TotalMatches = brackets.MatchCount = 1;
                brackets.Type = ElitePKBrackets.StaticUpdate;
                brackets.Append(this);
                Kernel.SendWorldMessage(brackets);
                //target.Entity.BringToLife();
                var array = Watchers.Values.ToArray();
                foreach (var watcher in array)
                    LeaveWatch(watcher);
            }
            private GameState targetOf(GameState client)
            {
                for (int i = 0; i < Players.Length; i++)
                    if (Players[i] != null)
                        if (Players[i].Player.UID != client.Player.UID)
                            if (!Players[i].ElitePKStats.Waiting)
                                return Players[i];
                return null;
            }

            public ElitePKMatchStats CreateUpdate()
            {
                ElitePKMatchStats stats = new ElitePKMatchStats();
                stats.Append(this);
                return stats;
            }

            public void Export()
            {
                if (Time32.Now > DoneStamp.AddSeconds(3) || TimeLeft == 0)
                {
                    #region !Done
                    if (!Done)
                    {
                        if (Players.Length == 1)
                        {
                            Players[0].ElitePKStats.Flag = FighterStats.StatusFlag.Qualified;
                            Flag = StatusFlag.OK;
                        }
                        else
                        {
                            var fighters = FightersStats;
                            if (fighters[0].Fighting && fighters[1].Fighting)
                            {
                                if (fighters[0].Points == fighters[1].Points)
                                {
                                    if (fighters[0].Wager <= fighters[1].Wager)
                                    {
                                        End(Players.First(p => p.Player.UID == fighters[0].UID));
                                    }
                                    else
                                    {
                                        End(Players.First(p => p.Player.UID == fighters[1].UID));
                                    }
                                }
                                else
                                {
                                    if (fighters[0].Points > fighters[1].Points)
                                    {
                                        End(Players.First(p => p.Player.UID == fighters[0].UID));
                                    }
                                    else
                                    {
                                        End(Players.First(p => p.Player.UID == fighters[1].UID));
                                    }
                                }
                            }
                        }
                    }
                    #endregion
                    foreach (var player in Players)
                    {
                        if (player != null)
                        {
                            if (!player.ElitePKStats.Waiting)
                            {
                                player.CurrentHonor += 1000;
                                player.HistoryHonor += 1000;
                                player.IncreaseExperience(player.ExpBall, false);

                                var map = ElitePKTournament.Tournaments[GroupID].WaitingArea;
                                var coords = map.RandomCoordinates();
                                player.Player.Teleport(map.ID, coords.Item1, coords.Item2);
                                player.Player.BringToLife();
                            }
                        }
                    }
                    Exported = true;
                    Completed = true;
                    var array = Watchers.Values.ToArray();
                    foreach (var watcher in array)
                        LeaveWatch(watcher);
                }
            }

            public void SwitchBetween()
            {
                if (Imports == 1)
                {
                    if (Players.Length == 3)
                    {
                        if (Players[0] != null)
                        {
                            MatchStats[0].Flag = FighterStats.StatusFlag.None;
                            if (MatchStats[1].Winner)
                                MatchStats[1].Flag = FighterStats.StatusFlag.None;
                            if (MatchStats[2].Winner)
                                MatchStats[2].Flag = FighterStats.StatusFlag.None;
                            Completed = false;
                        }
                    }
                }
                else
                    Flag = StatusFlag.OK;
            }

            public void Send(GameState client, int gui_state)
            {
                ElitePKBrackets brackets = new ElitePKBrackets(true, 1);
                brackets.Group = (ushort)GroupID;
                brackets.GUIType = (ushort)gui_state;
                brackets.TotalMatches = brackets.MatchCount = 1;
                brackets.Type = ElitePKBrackets.StaticUpdate;
                brackets.Append(this);
                client.Send(brackets);
            }

            public void Commit()
            {
                var matchStats = new FighterStats[MatchStats.Length];
                for (int i = 0; i < matchStats.Length; i++)
                {
                    matchStats[i] = MatchStats[i].Clone();
                    if (matchStats[i].Winner)
                        matchStats[i].Advance = true;
                }
                MatchStats = matchStats;
            }

            public void BeginWatch(GameState client)
            {
                if (Watchers.ContainsKey(client.Player.UID)) return;
                if (client.ElitePKMatch != null)
                    if (!client.ElitePKStats.Waiting && !client.ElitePKStats.Lost)
                        return;

                Watchers.TryAdd(client.Player.UID, client);
                var coords = Map.RandomCoordinates();
                client.WatchingElitePKMatch = this;
                client.Player.Teleport(Map.BaseID, Map.ID, coords.Item1, coords.Item2);
                if (FightersStats.Length == 2)
                {
                    ElitePKWatch watch = new ElitePKWatch(true, Watchers.Count);
                    watch.ID = ID;
                    watch.Type = ElitePKWatch.Fighters;
                    watch.dwCheers1 = FightersStats[0].Cheers;
                    watch.dwCheers2 = FightersStats[1].Cheers;
                    watch.Append(FightersStats[0].Name);
                    watch.Append(FightersStats[1].Name);
                    client.Send(watch);
                    watch = new ElitePKWatch(true, Watchers.Count);
                    watch.ID = ID;
                    watch.dwCheers1 = FightersStats[0].Cheers;
                    watch.dwCheers2 = FightersStats[1].Cheers;
                    foreach (var pClient in Watchers.Values)
                        watch.Append(pClient.Player.Mesh, pClient.Player.Name);
                    client.Send(watch);
                }
                client.Send(CreateUpdate());

                UpdateWatchers();
            }

            public void Update()
            {
                var update = CreateUpdate();
                if (Players != null)
                    foreach (var player in Players)
                        if (player != null)
                            if (player.ElitePKStats.Fighting)
                                player.Send(update);
            }
            public void UpdateWatchers()
            {
                var watch = new ElitePKWatch(true, Watchers.Count);
                watch.ID = ID;
                watch.Type = ElitePKWatch.Watchers;
                watch.dwCheers1 = FightersStats[0].Cheers;
                watch.dwCheers2 = FightersStats[1].Cheers;
                foreach (var pClient in Watchers.Values)
                    watch.Append(pClient.Player.Mesh, pClient.Player.Name);
                foreach (var pClient in Watchers.Values)
                    pClient.Send(watch);
                foreach (var pClient in Players)
                    if (pClient != null)
                        pClient.Send(watch);
            }
            public void LeaveWatch(GameState client)
            {
                var epk = ElitePKTournament.Tournaments[GroupID];
                client.WatchingElitePKMatch = null;
                Watchers.Remove(client.Player.UID);
                try
                {
                    ElitePKWatch watch = new ElitePKWatch(true);
                    watch.ID = ID;
                    watch.Type = ElitePKWatch.Leave;
                    client.Send(watch);
                    if (!epk.Players.ContainsKey(client.Player.UID))
                        client.Player.PreviousTeleport();
                    else
                    {
                        var map = epk.WaitingArea;
                        var coords = map.RandomCoordinates();
                        client.Player.Teleport(map.ID, coords.Item1, coords.Item2);
                    }
                }
                catch
                {
                    var map = epk.WaitingArea;
                    var coords = map.RandomCoordinates();
                    client.Player.Teleport(map.ID, coords.Item1, coords.Item2);
                }
            }

            public void Cheer(GameState client, uint fighter)
            {
                Players.First(p => p != null && p.Player.UID == fighter)
                    .ElitePKStats.Cheers++;
                UpdateWatchers();
            }

            public GameState Return(Func<FighterStats, bool> fn)
            {
                foreach (var stat in MatchStats)
                    if (stat != null)
                        if (fn(stat))
                            return ElitePKTournament.Tournaments[GroupID].Players[stat.UID];
                return null;
            }

            public void Check()
            {
                if (Exported) return;

                foreach (var player in Players)
                {
                    if (!player.ElitePKStats.Lost && !player.ElitePKStats.Waiting)
                    {
                        if (player.Player.MapID != Map.ID)
                        {
                            End(player);
                        }
                    }
                }
            }
        }
        public void SignUp(GameState client)
        {
            var coords = WaitingArea.RandomCoordinates();
            client.Player.Teleport(WaitingArea.ID, coords.Item1, coords.Item2);
            Players.Add(client.Player.UID, client);
            ElitePKMatchUI ui = new ElitePKMatchUI(true);
            ui.Type = ElitePKMatchUI.Information;
            ui.Append(client);
            ui.Send(client);
            client.SignedUpForEPK = true;
            client.OnDisconnect = p =>
            {
                if (p.ElitePKStats.Fighting)
                    p.ElitePKMatch.End(p);
                p.Player.Teleport(1002, 301, 288);
            };
        }

        public void SubscribeTimer()
        {
            if (Subscriber == null)
                Subscriber = Thread.Subscribe(timerCallback, 1000);
        }

        private void timerCallback(int time)
        {
            try
            {
                DateTime Now64 = DateTime.Now;
                if (State == States.GUI_ReconstructTop)
                {
                    if (Now64.AddMinutes(1) > ConstructTop8)
                    {
                        Server.Vars["epk_" + GroupID + "_edt"] = EndDateTime = DateTime.Now;
                        State = States.GUI_Top8Ranking;
                        var brackets = CreateBrackets(new Match[0], 0, ElitePKBrackets.RequestInformation);
                        Kernel.SendWorldMessage(brackets);

                        foreach (var clients in Kernel.GamePool.Values)
                            ElitePKTournament.GiveClientReward(clients);

                        Kernel.SendWorldMessage(new ElitePKBrackets(true) { Group = (ushort)GroupID, Type = 6, OnGoing = false });
                        Subscriber.Dispose();
                    }
                    return;
                }
                if (Now64.Hour == (EventTime + 1) && Now64.Minute >= (3 - GroupID) * 2)
                {
                    #region GetState
                    if (State == States.GUI_Top8Ranking)
                    {
                        willAdvance = Players.Count > 8;

                        Top8 = new FighterStats[8];
                        if (Players.Count == 8)
                            State = States.GUI_Top4Qualifier;
                        else if (willAdvance && Players.Count <= 24)
                            State = States.GUI_Top8Qualifier;
                        else
                            State = States.GUI_Knockout;
                    }
                    #endregion
                    #region Knockout
                    if (State == States.GUI_Knockout)
                    {
                        if (pState == States.T_Organize)
                        {
                            if (Players.Count < 8)
                            {
                                foreach (var player in Players.Values)
                                    player.Player.Teleport(1002, 439, 390);
                                Players.Clear();
                                State = States.GUI_Top8Ranking;
                                Subscriber.Dispose();
                                return;
                            }
                            if (willAdvance && Players.Count <= 24)
                            {
                                State = States.GUI_Top8Qualifier;
                            }
                            else
                            {
                                MatchIndex = 0;
                                var array = Players.Values.ToArray();
                                try
                                {
                                    foreach (var player in array)
                                    {
                                        player["epk_id"] = GroupID;
                                        player["epk_prize"] = false;
                                    }
                                }
                                catch { }
                                ushort counter = 0;
                                if (array.Length % 2 == 0)
                                {
                                    for (ushort x = 0; x < array.Length; x++)
                                    {
                                        int r = counter++;
                                        int t = counter++;
                                        if (counter <= array.Length)
                                        {
                                            Match match = new Match(array[r], array[t]);
                                            match.Index = MatchIndex++;
                                            match.ID = MatchCounter.Next;
                                            Matches.Add(match.ID, match);
                                        }
                                    }
                                }
                                else
                                {
                                    for (ushort x = 0; x < array.Length - 1; x++)
                                    {
                                        int r = counter++;
                                        int t = counter++;
                                        if (counter <= array.Length)
                                        {
                                            Match match = new Match(array[r], array[t]);
                                            match.Index = MatchIndex++;
                                            match.ID = MatchCounter.Next;
                                            Matches.Add(match.ID, match);
                                        }
                                    }
                                    Match match_single = new Match(array[array.Length - 1]);
                                    match_single.Index = MatchIndex++;
                                    match_single.ID = MatchCounter.Next;
                                    Matches.Add(match_single.ID, match_single);
                                }
                                pStamp = Time32.Now.AddSeconds(60);
                                pState = States.T_Wait;
                                SentNoWagers = false;
                                MatchArray = Matches.Values.OrderByDescending(p => p.Players.Length).ToArray();
                                var brackets = CreateBrackets(MatchArray, 0);
                                Kernel.SendWorldMessage(brackets);
                                Kernel.SendWorldMessage(new ElitePKBrackets(true) { Group = (ushort)GroupID, Type = 6, OnGoing = true });
                            }
                        }
                        else if (pState == States.T_Wait)
                        {
                            if (TimeLeft == 0)
                            {
                                int doneMatches = 0;
                                SendNoWagers();

                                foreach (var match in MatchArray)
                                {
                                    if (!match.Completed)
                                    {
                                        if (!match.Inside)
                                        {
                                            match.Import(Match.StatusFlag.InFight);
                                        }
                                        else
                                        {
                                            match.Check();
                                            if (!match.Exported && (match.Done || match.TimeLeft == 0))
                                                match.Export();
                                            if (match.Exported)
                                                doneMatches++;
                                            foreach (var stats in match.MatchStats)
                                                if (stats.Winner && match.Imports == 2)
                                                    stats.Flag = FighterStats.StatusFlag.Qualified;
                                        }
                                    }
                                    else
                                        doneMatches++;
                                }
                                if (doneMatches == Matches.Count)
                                {
                                    foreach (var match in Matches.Values)
                                        foreach (var stats in match.MatchStats)
                                            if (stats != null)
                                                if (stats.Flag != FighterStats.StatusFlag.Qualified)
                                                    Remove(stats.UID);

                                    Matches.Clear();
                                    pState = States.T_Organize;
                                }
                            }
                        }
                    }
                    #endregion
                    #region Top8Qualifier
                    if (State == States.GUI_Top8Qualifier)
                    {
                        if (pState == States.T_Organize)
                        {
                            if (Players.Count == 8)
                            {
                                State = States.GUI_Top4Qualifier;
                            }
                            else
                            {
                                MatchIndex = 0;
                                var array = Players.Values.ToArray();
                                int[] taken = new int[array.Length];

                                if (array.Length <= 16)
                                {
                                    ushort counter = 0;
                                    int t1Group = array.Length - 8;
                                    int lim = taken.Length / 2;
                                    for (int i = 0; i < t1Group; i++)
                                    {
                                        int r = counter++;
                                        int t = counter++;
                                        Match match = new Match(array[r], array[t]);
                                        match.Index = MatchIndex++;
                                        match.ID = MatchCounter.Next;
                                        Matches.TryAdd(match.ID, match);
                                    }
                                    for (int i = 0; i < 8 - t1Group; i++)
                                    {
                                        ushort r = counter++;
                                        Match match = new Match(array[r]);
                                        match.Index = MatchIndex++;
                                        match.ID = MatchCounter.Next;
                                        Matches.TryAdd(match.ID, match);
                                    }
                                }
                                else
                                {
                                    ushort counter = 0;
                                    int t3GroupCount = array.Length - 16;
                                    for (int i = 0; i < t3GroupCount; i++)
                                    {
                                        int r = counter++;
                                        int t = counter++;
                                        int y = counter++;
                                        Match match = new Match(array[r], array[t], array[y]);
                                        match.Index = MatchIndex++;
                                        match.ID = MatchCounter.Next;
                                        Matches.TryAdd(match.ID, match);
                                    }
                                    int t2GroupCount = array.Length - counter;
                                    for (int i = 0; i < t2GroupCount / 2; i++)
                                    {
                                        int r = counter++;
                                        int t = counter++;
                                        Match match = new Match(array[r], array[t]);
                                        match.Index = MatchIndex++;
                                        match.ID = MatchCounter.Next;
                                        Matches.TryAdd(match.ID, match);
                                    }
                                }
                                pStamp = Time32.Now.AddSeconds(60);
                                pState = States.T_Wait;
                                SentNoWagers = false;
                                MatchArray = Matches.Values.OrderByDescending(p => p.Players.Length).ToArray();
                                Kernel.SendWorldMessage(CreateBrackets(MatchArray, 0, ElitePKBrackets.UpdateList));
                                ElitePKBrackets brackets = new ElitePKBrackets(true);
                                brackets.Type = ElitePKBrackets.EPK_State;
                                brackets.OnGoing = true;
                                Kernel.SendWorldMessage(brackets);
                            }
                        }
                        else if (pState == States.T_Wait)
                        {
                            if (TimeLeft == 0)
                            {
                                int doneMatches = 0;
                                SendNoWagers();
                                foreach (var match in MatchArray)
                                {
                                    if (!match.Completed)
                                    {
                                        if (!match.Inside)
                                        {
                                            match.Import(Match.StatusFlag.InFight);
                                        }
                                        else
                                        {
                                            match.Check();
                                            if (!match.Exported && (match.Done || match.TimeLeft == 0))
                                            {
                                                match.Export();
                                                if (match.Players.Length == 3)
                                                    match.SwitchBetween();
                                                else
                                                    match.Flag = Match.StatusFlag.OK;
                                            }

                                            if (match.Exported)
                                                doneMatches++;
                                        }
                                    }
                                    else
                                    {
                                        doneMatches++;
                                    }
                                }
                                if (doneMatches == Matches.Count)
                                {
                                    bool finishedRound = true;
                                    foreach (var match in MatchArray)
                                    {
                                        if (!match.Completed)
                                        {
                                            finishedRound = false;
                                            break;
                                        }
                                    }
                                    if (!finishedRound)
                                    {
                                        foreach (var match in MatchArray)
                                        {
                                            if (!match.Completed)
                                            {
                                                foreach (var stats in match.MatchStats)
                                                    stats.Reset(false);
                                                match.Inside = false;
                                                match.Done = false;
                                                match.Exported = false;
                                                if (match.Players.Length != 1)
                                                    match.Flag = Match.StatusFlag.AcceptingWagers;
                                            }
                                        }
                                        pStamp = Time32.Now.AddSeconds(60);
                                        pState = States.T_Wait;
                                        SentNoWagers = false;
                                        Kernel.SendWorldMessage(CreateBrackets(MatchArray, 0, ElitePKBrackets.UpdateList));
                                    }
                                    else
                                    {
                                        foreach (var match in Matches.Values)
                                            foreach (var stats in match.MatchStats)
                                                if (stats != null)
                                                    if (stats.Flag != FighterStats.StatusFlag.Qualified)
                                                        Remove(stats.UID);
                                        Matches.Clear();
                                        pState = States.T_Organize;
                                    }
                                }
                            }
                        }
                    }
                    #endregion
                    #region Top4Qualifier
                    if (State == States.GUI_Top4Qualifier)
                    {
                        if (pState == States.T_Organize)
                        {
                            MatchIndex = 0;
                            var array = Players.Values.ToArray();
                            foreach (var player in array)
                                player["epk_prize"] = true;

                            int[] taken = new int[array.Length];
                            int lim = taken.Length / 2;
                            for (int i = 0; i < taken.Length; i += 2)
                            {
                                taken[i] = taken[i + 1] = 1;
                                Match match = new Match(array[i], array[i + 1]);
                                match.Index = MatchIndex++;
                                match.Flag = Match.StatusFlag.FinalMatch;
                                match.ID = MatchCounter.Next;
                                Matches.TryAdd(match.ID, match);
                            }
                            pStamp = Time32.Now.AddSeconds(60);
                            pState = States.T_Wait;
                            SentNoWagers = false;
                            MatchArray = Matches.Values.ToArray();
                            var brackets = CreateBrackets(MatchArray, 0, ElitePKBrackets.GUIEdit);
                            Kernel.SendWorldMessage(brackets);
                            brackets = CreateBrackets(MatchArray, 0, ElitePKBrackets.UpdateList);
                            Kernel.SendWorldMessage(brackets);
                            Kernel.SendWorldMessage(new ElitePKBrackets(true) { Group = (ushort)GroupID, Type = 6, OnGoing = true });
                        }
                        else if (pState == States.T_Wait)
                        {
                            if (TimeLeft == 0)
                            {
                                SendNoWagers();

                                int doneMatches = 0;

                                foreach (var match in MatchArray)
                                {
                                    if (!match.Completed)
                                    {
                                        if (!match.Inside)
                                        {
                                            match.Import();
                                        }
                                        else
                                        {
                                            match.Check();
                                            if (!match.Exported && (match.Done || match.TimeLeft == 0))
                                                match.Export();
                                            if (match.Exported)
                                                doneMatches++;
                                        }
                                    }
                                    else
                                        doneMatches++;
                                }
                                if (doneMatches == MatchArray.Length)
                                {
                                    int i = 7;
                                    foreach (var match in MatchArray)
                                    {
                                        foreach (var stats in match.MatchStats)
                                            if (stats.Flag == FighterStats.StatusFlag.Lost)
                                                Top8[i--] = stats.Clone();
                                        match.Commit();
                                    }
                                    foreach (var match in Matches.Values)
                                        foreach (var stats in match.MatchStats)
                                            if (stats != null)
                                                if (stats.Flag != FighterStats.StatusFlag.Qualified)
                                                    Remove(stats.UID);
                                    State = States.GUI_Top2Qualifier;
                                    pState = States.T_Organize;
                                }
                            }
                        }
                    }
                    #endregion
                    #region Top2Qualifier
                    if (State == States.GUI_Top2Qualifier)
                    {
                        if (pState == States.T_Organize)
                        {
                            MatchIndex = 0;
                            var brackets = CreateBrackets(MatchArray, 0, ElitePKBrackets.GUIEdit);
                            Kernel.SendWorldMessage(brackets);
                            Top4MatchArray = new Match[2];
                            for (int i = 0; i < Top4MatchArray.Length; i++)
                            {
                                Match match = new Match(
                                    MatchArray[i].Return(p => p.Winner),
                                    MatchArray[i + 2].Return(p => p.Winner));
                                match.Index = MatchIndex++;
                                match.ID = MatchCounter.Next;
                                Top4MatchArray[i] = match;
                                Matches.TryAdd(match.ID, match);
                            }
                            pStamp = Time32.Now.AddSeconds(60);
                            pState = States.T_Wait;
                            SentNoWagers = false;
                            brackets = CreateBrackets(Top4MatchArray, 0, ElitePKBrackets.UpdateList);
                            Kernel.SendWorldMessage(brackets);
                            Kernel.SendWorldMessage(new ElitePKBrackets(true) { Group = (ushort)GroupID, Type = 6, OnGoing = true });
                        }
                        else if (pState == States.T_Wait)
                        {
                            if (TimeLeft == 0)
                            {
                                SendNoWagers();
                                int doneMatches = 0;
                                foreach (var match in Top4MatchArray)
                                {
                                    if (!match.Completed)
                                    {
                                        if (!match.Inside)
                                        {
                                            match.Import();
                                        }
                                        else
                                        {
                                            match.Check();
                                            if (!match.Exported && (match.Done || match.TimeLeft == 0))
                                                match.Export();
                                            if (match.Exported)
                                                doneMatches++;
                                        }
                                    }
                                    else
                                        doneMatches++;
                                }
                                if (doneMatches == Top4MatchArray.Length)
                                {
                                    foreach (var match in Top4MatchArray)
                                        match.Commit();

                                    pState = States.T_Organize;
                                    State = States.GUI_Top3;
                                }
                            }
                        }
                    }
                    #endregion
                    #region Top3
                    if (State == States.GUI_Top3)
                    {
                        if (pState == States.T_Organize)
                        {
                            MatchIndex = 0;
                            var brackets = CreateBrackets(MatchArray, 0, ElitePKBrackets.GUIEdit);
                            Kernel.SendWorldMessage(brackets);
                            ExtendedMatchArray = new Match[1];
                            Match match = new Match(
                                Top4MatchArray[0].Return(p => !p.Winner),
                                Top4MatchArray[1].Return(p => !p.Winner));

                            match.Index = MatchIndex++;
                            match.ID = MatchCounter.Next;
                            ExtendedMatchArray[0] = match;
                            Matches.TryAdd(match.ID, match);
                            pStamp = Time32.Now.AddSeconds(60);
                            pState = States.T_Wait;
                            SentNoWagers = false;
                            brackets = CreateBrackets(ExtendedMatchArray, 0, ElitePKBrackets.UpdateList);
                            Kernel.SendWorldMessage(brackets);
                            Kernel.SendWorldMessage(new ElitePKBrackets(true) { Group = (ushort)GroupID, Type = 6, OnGoing = true });
                        }
                        else if (pState == States.T_Wait)
                        {
                            if (TimeLeft == 0)
                            {
                                SendNoWagers();
                                int doneMatches = 0;
                                foreach (var match in ExtendedMatchArray)
                                {
                                    if (!match.Completed)
                                    {
                                        if (!match.Inside)
                                        {
                                            match.Import();
                                        }
                                        else
                                        {
                                            match.Check();
                                            if (!match.Exported && (match.Done || match.TimeLeft == 0))
                                                match.Export();
                                            if (match.Exported)
                                                doneMatches++;
                                        }
                                    }
                                    else
                                        doneMatches++;
                                }
                                if (doneMatches == ExtendedMatchArray.Length)
                                {
                                    var top3 = ExtendedMatchArray[0].Players
                                        .First(p => p.ElitePKStats.Winner).ElitePKStats;
                                    var top4 = ExtendedMatchArray[0].Players
                                        .First(p => !p.ElitePKStats.Winner).ElitePKStats;
                                    Top8[2] = top3.Clone();
                                    Top8[3] = top4.Clone();
                                    top3.Flag = FighterStats.StatusFlag.Lost;

                                    pState = States.T_Organize;
                                    State = States.GUI_Top1;
                                    foreach (var client in Server.GamePool) Rankings(client);
                                    var brackets = CreateBrackets(ExtendedMatchArray, 0, ElitePKBrackets.UpdateList);
                                    Kernel.SendWorldMessage(brackets);
                                }
                            }
                        }
                    }
                    #endregion
                    #region Top1
                    if (State == States.GUI_Top1)
                    {
                        if (pState == States.T_Organize)
                        {
                            MatchIndex = 0;
                            var brackets = CreateBrackets(MatchArray, 0, ElitePKBrackets.GUIEdit);
                            Kernel.SendWorldMessage(brackets);
                            ExtendedMatchArray = new Match[1];
                            Match match = new Match(
                                Top4MatchArray[0].Return(p => p.Winner),
                                Top4MatchArray[1].Return(p => p.Winner));
                            match.Index = MatchIndex++;
                            match.ID = MatchCounter.Next;
                            ExtendedMatchArray[0] = match;
                            Matches.TryAdd(match.ID, match);
                            pStamp = Time32.Now.AddSeconds(60);
                            pState = States.T_Wait;
                            SentNoWagers = false;
                            brackets = CreateBrackets(ExtendedMatchArray, 0, ElitePKBrackets.UpdateList);
                            Kernel.SendWorldMessage(brackets);
                            Kernel.SendWorldMessage(new ElitePKBrackets(true) { Group = (ushort)GroupID, Type = 6, OnGoing = true });
                        }
                        else if (pState == States.T_Wait)
                        {
                            if (TimeLeft == 0)
                            {
                                SendNoWagers();
                                int doneMatches = 0;
                                foreach (var match in ExtendedMatchArray)
                                {
                                    if (!match.Inside)
                                    {
                                        match.Import();
                                    }
                                    else
                                    {
                                        match.Check();
                                        if (!match.Exported && (match.Done || match.TimeLeft == 0))
                                            match.Export();
                                        if (match.Exported)
                                            doneMatches++;
                                    }
                                }
                                if (doneMatches == ExtendedMatchArray.Length)
                                {
                                    var top1 = ExtendedMatchArray[0].Players
                                        .First(p => p.ElitePKStats.Winner).ElitePKStats;
                                    var top2 = ExtendedMatchArray[0].Players
                                        .First(p => !p.ElitePKStats.Winner).ElitePKStats;
                                    Top8[0] = top1.Clone();
                                    Top8[1] = top2.Clone();
                                    ConstructTop8 = DateTime.Now;
                                    SaveTop8();
                                    State = States.GUI_ReconstructTop;
                                    foreach (var client in Server.GamePool) Rankings(client);
                                    var brackets = CreateBrackets(ExtendedMatchArray, 0, ElitePKBrackets.UpdateList);
                                    Kernel.SendWorldMessage(brackets);
                                    Kernel.SendWorldMessage(new ElitePKBrackets(true) { Group = (ushort)GroupID, Type = 6, OnGoing = true });

                                    foreach (var player in Players.Values)
                                        Remove(player.Player.UID);
                                }
                            }
                        }
                    }
                    #endregion
                }
            }
            catch (Exception e)
            { Console.WriteLine(e.ToString()); }
        }
        private void SendNoWagers()
        {
            if (!SentNoWagers)
            {
                SentNoWagers = true;
                ElitePKBrackets noWagers = new ElitePKBrackets(true);
                noWagers.Group = (ushort)GroupID;
                noWagers.GUIType = (ushort)State;
                noWagers.Type = ElitePKBrackets.StopWagers;
                Kernel.SendWorldMessage(noWagers);
            }
        }

        private ElitePKBrackets CreateBrackets(Match[] matches, int page = 0, byte type = 0)
        {
            int lim = 0, count = 0;
            if (matches == null) return new ElitePKBrackets(true);
            if (State == States.GUI_Knockout)
            {
                if (page > matches.Length / 5) page = 0;
                lim = 5;
            }
            else lim = matches.Length;
            count = Math.Min(lim, matches.Length - page * lim);

            ElitePKBrackets brackets = new ElitePKBrackets(true, count);
            brackets.Group = (ushort)GroupID;
            brackets.GUIType = (ushort)State;
            brackets.TotalMatches = (ushort)count;
            brackets.Page = (byte)page;
            brackets.TimeLeft = TimeLeft;
            if (type == States.GUI_Top2Qualifier)
                brackets.MatchCount = 2;
            else
                brackets.MatchCount = (ushort)matches.Length;
            brackets.Type = type;
            for (int i = page * lim; i < page * lim + count; i++)
                brackets.Append(matches[i]);
            return brackets;
        }

        private void Remove(uint uid)
        {
            Players[uid].Player.Teleport(1002, 301, 288);
            Players.Remove(uid);
        }

        public ushort TimeLeft
        {
            get
            {
                int value = (int)((pStamp.TotalMilliseconds - Time32.Now.TotalMilliseconds) / 1000);
                if (value < 0) return 0;
                return (ushort)value;
            }
        }

        public void Update(GameState client, int page = 0)
        {
            try
            {
                if (State == States.GUI_Top8Ranking)
                {
                    Rankings(client);
                }
                else
                {
                    if (State >= States.GUI_Top4Qualifier)
                    {
                        var brackets = CreateBrackets(MatchArray, 0, ElitePKBrackets.GUIEdit);
                        client.Send(brackets);
                        if (ExtendedMatchArray != null)
                        {
                            brackets = CreateBrackets(ExtendedMatchArray, 0, ElitePKBrackets.UpdateList);
                            client.Send(brackets);
                            foreach (var match in ExtendedMatchArray)
                                match.Send(client, State);
                        }
                        else if (Top4MatchArray != null)
                        {
                            brackets = CreateBrackets(Top4MatchArray, 0, ElitePKBrackets.UpdateList);
                            client.Send(brackets);
                            foreach (var match in Top4MatchArray)
                                match.Send(client, State);
                        }
                        else
                        {
                            brackets = CreateBrackets(MatchArray, 0, ElitePKBrackets.UpdateList);
                            client.Send(brackets);
                            foreach (var match in MatchArray)
                            {
                                if (match.Inside && !match.Done)
                                    match.Flag = Match.StatusFlag.Watchable;
                                match.Send(client, State);
                            }
                        }
                    }
                    else
                    {
                        var brackets = CreateBrackets(MatchArray, page);
                        client.Send(brackets);
                    }
                    if (State >= States.GUI_Top1)
                        Rankings(client);
                }
            }
            catch (Exception e) { Console.WriteLine(e.ToString()); }
        }

        public void Rankings(GameState client)
        {
            ElitePKRanking ranks = new ElitePKRanking(true);
            ranks.Type = ElitePKRanking.Top8;
            ranks.Group = (uint)GroupID;
            ranks.GroupStatus = (uint)State;
            ranks.UID = client.Player.UID;
            if (State >= States.GUI_Top1)
            {
                ranks.Type = ElitePKRanking.Top3;
                if (State == States.GUI_Top1)
                {
                    ranks.Count = 1;
                    ranks.Append(Top8[2], 3);
                }
                else
                {
                    ranks.Count = 3;
                    for (int i = 0; i < 3; i++)
                        ranks.Append(Top8[i], i + 1);
                }
                client.Send(ranks);
            }
            else
            {
                ranks.Count = (uint)Top8.Length;
                for (int i = 0; i < ranks.Count; i++)
                    if (Top8[i] != null)
                        ranks.Append(Top8[i], i);
                client.Send(ranks);
            }
        }
    }
}
