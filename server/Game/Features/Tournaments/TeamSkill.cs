// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Revora.Network.GamePackets;

namespace Revora.Game.Features.Tournaments
{
    public unsafe class ElitePkStatus
    {
        public static bool InSkillTeam(byte grupe, Game.ConquerStructures.Team team)
        {
            if (grupe > 3 || team == null || team.EliteMatch == null || team.EliteFighterStats == null) return false;
            return Registered(TeamElitePk.SkillTeamTournament.Tournaments, grupe, team)
                || Registered(TeamElitePk.TeamTournament.Tournaments, grupe, team);
        }
        private static bool Registered(TeamElitePk[] tournaments, byte group, Game.ConquerStructures.Team team)
        {
            if (tournaments == null || group >= tournaments.Length) return false;
            var tournament = tournaments[group];
            Game.ConquerStructures.Team registered;
            return tournament != null && tournament.Alive && tournament.Teams != null
                && tournament.Teams.TryGetValue(team.UID, out registered) && ReferenceEquals(registered, team);
        }
        internal static byte LevelGroup(byte level)
        {
            return level >= 130 ? (byte)3 : level >= 120 ? (byte)2 : level >= 100 ? (byte)1 : (byte)0;
        }
        public void GetSkillElitePkReward(Client.GameState client, byte Rank)
        {
            uint ITEM_ID = 0;
            switch (Rank)
            {
                case 0:
                    ITEM_ID = (uint)(721300 + GetEliteGroup(client.Entity.Level));
                    break;
                case 1:
                    ITEM_ID = (uint)(721304 + GetEliteGroup(client.Entity.Level));
                    break;
                case 2:
                    ITEM_ID = (uint)(721308 + GetEliteGroup(client.Entity.Level));
                    break;
                default:
                    ITEM_ID = (uint)(721312 + GetEliteGroup(client.Entity.Level));
                    break;

            }
            client.Inventory.Add(ITEM_ID, 0, 1);
        }
        public void GetTeamElitePkReward(Client.GameState client, byte Rank)
        {
            uint ITEM_ID = 0;
            switch (Rank)
            {
                case 0:
                    ITEM_ID = (uint)(720794 + GetEliteGroup(client.Entity.Level));
                    break;
                case 1:
                    ITEM_ID = (uint)(720798 + GetEliteGroup(client.Entity.Level));
                    break;
                case 2:
                    ITEM_ID = (uint)(720802 + GetEliteGroup(client.Entity.Level));
                    break;
                default:
                    ITEM_ID = (uint)(720806 + GetEliteGroup(client.Entity.Level));
                    break;

            }
            client.Inventory.Add(ITEM_ID, 0, 1);
        }
        public static byte[] BlockOpenArena = new byte[] 
        { 
            20, 0, 86, 4, 255, 87, 14, 0, 160, 187, 13, 0, 103, 0,
            21, 64, 204, 17, 0, 0, 84, 81, 83, 101, 114, 118, 101, 114 
        };
        public byte GetEliteGroup(byte level)
        {
            return LevelGroup(level);
        }
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
    }
    public unsafe class TeamElitePk : ElitePkStatus
    {
        public enum GamePackets : ushort
        {
            SkillEliteSetTeamName = 2260,
            SkillElitePkTop = 2253,
            SkillElitePkBrackets = 2252,
            SkillElitePKMatchStats = 2251,
            SkillElitePKMatchUI = 2250,
            TeamEliteSetTeamName = 2240,
            TeamElitePkTop = 2233,
            TeamElitePkBrackets = 2232,
            TeamElitePKMatchStats = 2231,
            TeamElitePKMatchUI = 2230,
        }
        public class TeamTournament
        {
            public static uint WaitingAreaID = 2068;
            public static TeamElitePk[] Tournaments;
            public static bool Opened = false;
            public static void Create()
            {
                try
                {
                    LocalTeamPkLedger.Initialize();
                    Tournaments = new TeamElitePk[4];
                    for (byte x = 0; x < Tournaments.Length; x++)
                        Tournaments[x] = new TeamElitePk(x, GamePackets.TeamElitePkBrackets);

                    LoadTop(Tournaments, "TeamElitePkTop8");
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }

            }
            public static void Save()
            {
                SaveTop(Tournaments, "TeamElitePkTop8");
            }
            public static bool GetReward(Client.GameState client, out byte rank, out byte elitestage)
            {
                if (Tournaments != null)

                    for (byte x = 0; x < Tournaments.Length; x++)
                    {
                        if (Tournaments[x] != null)
                        {
                            if (Tournaments[x].Top8 != null)
                            {
                                for (byte i = 0; i < Tournaments[x].Top8.Length; i++)
                                {
                                    if (Tournaments[x].Top8[i] != null)
                                    {
                                        if (Tournaments[x].Top8[i].LeaderUID == client.Entity.UID)
                                        {

                                            if (Tournaments[x].ResultPayable && Tournaments[x].Top8[i].RceiveReward == 0)
                                            {
                                                rank = (byte)(i + 1);
                                                elitestage = x;
                                                Tournaments[x].Top8[i].RceiveReward = 1;
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
            private static readonly object openingGate=new object();
            internal static bool IsOpeningTime(DateTime local) {
                return local.DayOfWeek==DayOfWeek.Saturday && local.Hour==18 && local.Minute==56 && local.Second<=35;
            }
            public static bool Open()
            {
                lock(openingGate) {
                    if(Opened || Tournaments==null)return false;
                    bool started=false;
                    foreach(var tournament in Tournaments) {
                        if(tournament!=null && tournament.SubscribeTimer(1))started=true;
                    }
                    if(!started)return false;
                    Opened=true;
                    Game.Entity.SendWorldMessage("Team PK registration is open for one minute. Gather your team at the Team Registrar in Twin City (291,146).");
                    return true;
                }
            }
            public static bool Join(Client.GameState client, byte Grope)
            {
                if (client==null || client.Team == null)
                    return false;

                if (Grope <= 3 && Tournaments!=null && Grope<Tournaments.Length && Tournaments[Grope]!=null)
                    return Tournaments[Grope].SignUp(client);
                return false;
            }
        }
        public class SkillTeamTournament
        {
            public static bool GetReward(Client.GameState client, out byte rank, out byte elitestage)
            {
                if (Tournaments != null)

                    for (byte x = 0; x < Tournaments.Length; x++)
                    {
                        if (Tournaments[x] != null)
                        {
                            if (Tournaments[x].Top8 != null)
                            {
                                for (byte i = 0; i < Tournaments[x].Top8.Length; i++)
                                {
                                    if (Tournaments[x].Top8[i] != null)
                                    {
                                        if (Tournaments[x].Top8[i].LeaderUID == client.Entity.UID)
                                        {

                                            if (Tournaments[x].ResultPayable && Tournaments[x].Top8[i].RceiveReward == 0)
                                            {
                                                rank = (byte)(i + 1);
                                                elitestage = x;
                                                Tournaments[x].Top8[i].RceiveReward = 1;
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
            public static uint WaitingAreaID = 2068;
            public static TeamElitePk[] Tournaments;
            public static bool Opened = false;
            public static void Create()
            {
                try
                {
                    LocalTeamPkLedger.Initialize();
                    Tournaments = new TeamElitePk[4];
                    for (byte x = 0; x < Tournaments.Length; x++)
                        Tournaments[x] = new TeamElitePk(x, GamePackets.SkillElitePkBrackets);
                    LoadTop(Tournaments, "SkillTeamElitePkTop8");
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
            }
            public static void Save()
            {
                SaveTop(Tournaments, "SkillTeamElitePkTop8");
            }
            private static readonly object openingGate=new object();
            // Revora edition: server-local Wednesday registration, fixed 20:00 close.
            internal static bool IsOpeningTime(DateTime local) {
                return local.DayOfWeek==DayOfWeek.Wednesday && local.Hour==19 && local.Minute>=45;
            }
            internal static DateTime RegistrationClose(DateTime local) {
                return local.Date.AddHours(20);
            }
            // Explicit console opening is independent of the recurring calendar.
            // Both paths retain the same durable once-per-date identity.
            public static bool Open() {
                DateTime local=DateTime.Now;
                return OpenWindow(local,local.AddMinutes(15));
            }
            internal static bool OpenAt(DateTime local) {
                return IsOpeningTime(local) && OpenWindow(local,RegistrationClose(local));
            }
            private static bool OpenWindow(DateTime local,DateTime deadline)
            {
                lock(openingGate) {
                    if(Opened || Tournaments==null || deadline<=local)return false;
                    bool started=false;
                    foreach(var tournament in Tournaments) {
                        if(tournament!=null && tournament.SubscribeUntil(local,deadline))started=true;
                    }
                    if(!started)return false;
                    Opened=true;
                    Game.Entity.SendWorldMessage("Skill Team PK registration is open until "+deadline.ToString("HH:mm",System.Globalization.CultureInfo.InvariantCulture)+" server time. Gather your team at the Skill Registrar in Twin City (300,146).");
                    return true;
                }
            }
            public static bool Join(Client.GameState client, byte Grope)
            {
                if (client==null || client.Team == null)
                    return false;

                if (Grope <= 3 && Tournaments!=null && Grope<Tournaments.Length && Tournaments[Grope]!=null)
                    return Tournaments[Grope].SignUp(client);
                return false;
            }
        }
        public static void SaveTop(TeamElitePk[] elit, string Ini_file)
        {
            if (elit == null) return;
            using (Database.Write _wr = new Database.Write("database//Slayer//" + Ini_file + ".ini"))
            {
                uint count = 0;
                for (byte x = 0; x < elit.Length; x++)
                    if (elit[x].Top8 != null)
                        count += (byte)elit[x].Top8.Length;
                string[] lines = new string[count];
                uint amount = 0;
                for (byte x = 0; x < elit.Length; x++)
                {
                    if (elit[x].Top8 != null)
                        for (byte i = 0; i < elit[x].Top8.Length; i++)
                        {
                            lines[amount] = x + "^" + elit[x].Top8[i].ToString();
                            amount++;
                        }
                }

                _wr.Add(lines, lines.Length).Execute(Database.Mode.Open);
            }
        }
        public static void LoadTop(TeamElitePk[] elit, string Ini_file)
        {
            try
            {
                using (Database.Read read = new Database.Read("database//Slayer//" + Ini_file + ".ini"))
                {
                    if (read.Reader())
                    {
                        byte[] counts = new byte[4];
                        byte group = 0;
                        while (read.UseRead())
                        {
                            string[] line = read.ReadString("").Split('^');
                            group = byte.Parse(line[0]);
                            if (elit[group].Top8 == null)
                                elit[group].Top8 = new FighterStats[8];
                            if (elit[group].Top8.Length < 8)
                                elit[group].Top8 = new FighterStats[8];
                            FighterStats fight = new FighterStats(0, "", 0, null);
                            fight.Load(line[1]);
                            elit[group].Top8[counts[group]] = fight;
                            counts[group]++;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
        public class FighterStats
        {
            public enum StatusFlag : int
            {
                None = 0,
                Fighting = 2,
                Lost = 4,
                Qualified = 3,
                Waiting = 1,
            }
            public Game.ConquerStructures.Team Team;
            public string Name;
            public uint LeaderUID;
            public uint LeaderMesh;
            public Client.GameState Owner;
            public bool OnNextMatch = false;
            public uint Cheers;
            public uint Points;
            public StatusFlag Flag;
            public bool Teleported;
            public byte RceiveReward;
            public byte Title;
            public byte Rank;
            public bool Waiting { get { return Flag == StatusFlag.Waiting; } }
            public bool NoFlag { get { return Flag == StatusFlag.None; } }
            public bool Fighting { get { return Flag == StatusFlag.Fighting; } }
            public bool Winner { get { return Flag == StatusFlag.Qualified; } }
            public bool Lost { get { return Flag == StatusFlag.Lost; } }
            public FighterStats(uint id, string name, uint mesh, Game.ConquerStructures.Team temate)
            {
                LeaderUID = id;
                Name = name;
                LeaderMesh = mesh;
                Team = temate;
            }
            public void Reset(bool noflag = false)
            {
                Points = 0;
                Cheers = 0;
                if (!noflag)
                {
                    if (!Lost)
                        Flag = StatusFlag.Waiting;
                }
            }
            public FighterStats Clone()
            {
                FighterStats stats = new FighterStats(LeaderUID, Name, LeaderMesh, Team);
                stats.Points = this.Points;
                stats.Flag = this.Flag;

                return stats;
            }
            public override string ToString()
            {
                if (Name.Contains('^'))
                    Name.Replace("^", "");
                if (Name.Contains('#'))
                    Name.Replace('#', ' ');
                StringBuilder build = new StringBuilder();
                build.Append(LeaderUID + "#" + Name + "#" + LeaderMesh + "#" + Rank + "#" + Title + "#" + RceiveReward + "");
                return build.ToString();
            }
            public void Load(string Line)
            {
                string[] data = Line.Split('#');
                LeaderUID = uint.Parse(data[0]);
                Name = data[1];
                LeaderMesh = uint.Parse(data[2]);
                Rank = byte.Parse(data[3]);
                Title = byte.Parse(data[4]);
                RceiveReward = byte.Parse(data[5]);
            }
        }
        public class Match
        {
            public enum StatusFlag : int
            {
                SwitchOut = 0,
                Watchable = 1,
                Waiting = 3,
                OK = 2,
            }
            public IEnumerable<Client.GameState> Players
            {
                get
                {

                    foreach (var team in MatchStats)
                        foreach (var player in team.Team.Players)
                            yield return player;
                }
            }
            public FighterStats[] MatchStats;
            public GamePackets T_ID;
            public bool OnGoing { get { return Flag == StatusFlag.Watchable; } }
            public int GroupID { get { return (int)ID / 100000 - 1; } }
            internal int RoundMinutes;
            public uint TimeLeft {get{return TimeLeftAt(Time32.Now);}}
            internal uint TimeLeftAt(Time32 now) {
                if(MatchStats.Length==1)return 0;
                int minutes=RoundMinutes>0?RoundMinutes:(T_ID==GamePackets.SkillElitePkBrackets?10:1);
                int value=(int)(ImportTime.AddMinutes(minutes).AllMilliseconds()-now.AllMilliseconds())/1000;
                return (uint)Math.Max(0,value);
            }
            public void Commit()
            {
                var matchStats = new FighterStats[MatchStats.Length];
                for (int i = 0; i < matchStats.Length; i++)
                {
                    matchStats[i] = MatchStats[i].Clone();
                }
                MatchStats = matchStats;
            }
            public ConcurrentDictionary<uint, Client.GameState> Watchers;
            public FighterStats Return(Func<FighterStats, bool> fn)
            {
                foreach (var stat in MatchStats)
                    if (stat != null)
                        if (fn(stat))
                        {
                            if (T_ID == GamePackets.SkillElitePkBrackets)
                                return SkillTeamTournament.Tournaments[GroupID].Teams[stat.Team.UID].EliteFighterStats;
                            else
                                return TeamTournament.Tournaments[GroupID].Teams[stat.Team.UID].EliteFighterStats;
                        }
                MatchStats[0].Flag = FighterStats.StatusFlag.Qualified;
                return MatchStats[0];
            }
            public uint ID;
            public StatusFlag Flag;
            public bool Inside;
            public bool Done;
            public bool Exported;
            public bool Completed;
            public ushort Index;
            public Time32 ImportTime;
            public Map Map;
            private Time32 DoneStamp;
            private int Imports;
            private LocalSkillPkRounds.Result pendingRound;
            public void AliveMatch()
            {

                if (Flag != StatusFlag.OK)
                {
                    if (Inside && !Done)
                    {
                        foreach (var dismised in MatchStats)
                        {
                            if (Flag != StatusFlag.OK)
                            {
                                if (MatchStats.Length == 2)
                                {
                                    if (MatchStats[0].Winner)
                                        return;
                                    if (MatchStats[1].Winner)
                                        return;
                                }
                                if (!dismised.Team.OnPopulates)
                                {
                                    if (dismised.Waiting)
                                    {
                                        dismised.Flag = FighterStats.StatusFlag.Lost;
                                        dismised.Team.EliteFighterStats.Flag = FighterStats.StatusFlag.Lost;
                                    }
                                    if (!dismised.Lost)
                                    {
                                        Console.WriteLine("team " + dismised.Team.EliteFighterStats.Name + " has lost");
                                        this.End(dismised.Team);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            public Match(TeamElitePk.GamePackets ID, params TeamElitePk.FighterStats[] teamates)
            {
                try
                {
                    Map = Kernel.Maps[700].MakeDynamicMap();
                    this.T_ID = ID;
                    if (teamates.Length == 1)
                    {
                        this.Flag = TeamElitePk.Match.StatusFlag.OK;
                        this.MatchStats = new TeamElitePk.FighterStats[1];
                        this.MatchStats[0] = teamates[0].Team.EliteFighterStats;
                        this.MatchStats[0].Team.EliteMatch = this;
                        this.MatchStats[0].Points = 0u;
                        this.MatchStats[0].Cheers = 0u;
                        this.MatchStats[0].Flag = TeamElitePk.FighterStats.StatusFlag.Qualified;
                        this.Flag = TeamElitePk.Match.StatusFlag.OK;
                    }
                    else
                    {
                        this.Watchers = new ConcurrentDictionary<uint, Client.GameState>();
                        this.MatchStats = new TeamElitePk.FighterStats[teamates.Length];
                        for (int i = 0; i < teamates.Length; i++)
                        {
                            this.MatchStats[i] = teamates[i].Team.EliteFighterStats;
                            this.MatchStats[i].Team.EliteMatch = this;
                            this.MatchStats[i].Flag = TeamElitePk.FighterStats.StatusFlag.Waiting;
                            this.MatchStats[i].Points = 0u;
                            this.MatchStats[i].Cheers = 0u;
                            this.MatchStats[i].Teleported = false;
                        }
                        if (this.MatchStats.Length == 3)
                        {
                            this.MatchStats[0].Flag = TeamElitePk.FighterStats.StatusFlag.None;
                        }
                        this.Imports = 0;
                        this.Flag = TeamElitePk.Match.StatusFlag.Waiting;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
            }
            public void Import(StatusFlag flag = StatusFlag.OK) { lock(this){
                if(T_ID==GamePackets.SkillElitePkBrackets)LocalTeamPkCombat.ImportSkill(this,()=>ImportOwned(flag));
                else ImportOwned(flag);
            } }
            private void ImportOwned(StatusFlag flag)
            {
                Imports++;pendingRound=null;
                Exported = false;
                Done = false;
                Inside = true;
                Flag = flag;
                ImportTime = Time32.Now;
                if (MatchStats.Length > 1)
                {
                    Game.ConquerStructures.Team team1 = null, team2 = null;
                    for (int i = 0; i < MatchStats.Length; i++)
                        if (!MatchStats[i].NoFlag && !MatchStats[i].Lost)
                            if (team1 == null)
                                team1 = MatchStats[i].Team;
                            else
                                team2 = MatchStats[i].Team;

                    team1.EliteFighterStats.Flag = FighterStats.StatusFlag.Fighting;
                    team2.EliteFighterStats.Flag = FighterStats.StatusFlag.Fighting;
                    importPlayer(team1, team2);
                    importPlayer(team2, team1);

                    Network.GamePackets.TeamElitePkBrackets brackets = new Network.GamePackets.TeamElitePkBrackets(T_ID, 1);
                    {
                        brackets.Group = (ushort)GroupID;
                        brackets.GUIType = (ushort)(T_ID==GamePackets.SkillElitePkBrackets?SkillTeamTournament.Tournaments[GroupID]:TeamTournament.Tournaments[GroupID]).State;
                        brackets.TotalMatchesOnRoom = brackets.MatchCount = 1;
                        brackets.Type = Network.GamePackets.TeamElitePkBrackets.StaticUpdate;
                        brackets.Append(this);
                        Kernel.SendWorldMessage(brackets.ToArray());
                    }
                }
                else
                {
                    Done = true;
                    Flag = StatusFlag.OK;
                    Exported = true;
                    Completed = true;
                    MatchStats[0].Flag = FighterStats.StatusFlag.Qualified;
                }
            }
            private void importPlayer(Game.ConquerStructures.Team teamone, Game.ConquerStructures.Team opponent)
            {
                if (this.MatchStats.Length > 1)
                {
                    teamone.EliteFighterStats.Teleported = true;
                    opponent.EliteFighterStats.Teleported = true;
                    using (IEnumerator<Client.GameState> enumerator = teamone.Players.GetEnumerator())
                    {
                        while (enumerator.MoveNext())
                        {
                            Client.GameState current = enumerator.Current;
                            var waiting=T_ID==GamePackets.SkillElitePkBrackets?SkillTeamTournament.Tournaments[GroupID].WaitingArea:TeamTournament.Tournaments[GroupID].WaitingArea;
                            if(!QuestInventory.Player(current) || current.Entity.MapID!=waiting.ID)continue;
                            if (current.Entity.ContainsFlag(1125899906842624))
                            {
                                current.Entity.RemoveFlag(1125899906842624);
                            }
                            if (this.T_ID == TeamElitePk.GamePackets.SkillElitePkBrackets)
                            {
                                current.Entity.InSkillPk = true;
                            }
                            Tuple<ushort, ushort> tuple = this.Map.RandomCoordinates();
                            current.Entity.Teleport(this.Map.BaseID, this.Map.ID, tuple.Item1, tuple.Item2);
                            current.Entity.BringToLife();
                            current.Entity.SkillTeamWatchingElitePKMatch = null;
                            if (this.T_ID == TeamElitePk.GamePackets.SkillElitePkBrackets)
                            {
                                current.LoadItemStats();
                                current.Entity.Hitpoints = current.Entity.MaxHitpoints;
                            }
                            Revora.Network.GamePackets.TeamElitePKMatchUI teamElitePKMatchUI = new Revora.Network.GamePackets.TeamElitePKMatchUI(this.T_ID);
                            teamElitePKMatchUI.Append(opponent);
                            teamElitePKMatchUI.TimeLeft = this.TimeLeft;
                            teamElitePKMatchUI.Type = 2u;
                            current.Send(teamElitePKMatchUI.ToArray());
                            current.Send(this.CreateUpdate().ToArray());
                            current.CantAttack = Time32.Now.AddSeconds(11);
                            current.Entity.PrevPKMode = current.Entity.PKMode;
                            current.Entity.PKMode = Enums.PKMode.Team;
                            current.Send(new Data(true)
                            {
                                UID = current.Entity.UID,
                                ID = 96u,
                                dwParam = (uint)current.Entity.PKMode
                            }.ToArray());
                        }
                        return;
                    }
                }
                this.MatchStats[0].Flag = TeamElitePk.FighterStats.StatusFlag.Qualified;
                this.Done = true;
                this.Flag = TeamElitePk.Match.StatusFlag.OK;
                this.Exported = true;
                this.Completed = true;
            }
            public void End(Game.ConquerStructures.Team team) { lock(this){EndOwned(team);} }
            private void EndOwned(Game.ConquerStructures.Team team)
            {
                try
                {

                    if (MatchStats.Length == 1)
                    {
                        MatchStats[0].Flag = FighterStats.StatusFlag.Qualified;
                        Flag = StatusFlag.OK;
                        return;
                    }
                    if (Done) return;
                    if(T_ID==GamePackets.TeamElitePkBrackets && GroupID==3)LocalTeamPkWardrobe.RecordCombat(this,Imports,targetOfWin(team),team);
                    if(T_ID==GamePackets.SkillElitePkBrackets) {
                        if(pendingRound==null)pendingRound=LocalSkillPkRounds.Freeze(this,Imports,targetOfWin(team),team);
                        team=MatchStats.Single(f=>f.Team.UID==pendingRound.Loser).Team;
                        LocalSkillPkRounds.Record(pendingRound);
                    }
                    Done = true;
                    DoneStamp = Time32.Now;
                    Flag = StatusFlag.OK;
                    team.EliteFighterStats.Flag = FighterStats.StatusFlag.Lost;
                    Network.GamePackets.TeamElitePKMatchUI ui = new Network.GamePackets.TeamElitePKMatchUI(T_ID);
                    {
                        ui.Type = Network.GamePackets.ElitePKMatchUI.Effect;
                        ui.dwParam = Network.GamePackets.ElitePKMatchUI.Effect_Lose;
                        team.SendMesageTeam(ui.ToArray(), 0);

                        var team_win = targetOfWin(team);
                        if (team_win != null)
                        {
                            ui.Append(team_win.Team);
                            ui.dwParam = Network.GamePackets.TeamElitePKMatchUI.Effect_Win;

                            foreach (var target in team_win.Team.Players)
                            {
                                target.Entity.InSkillPk = false;
                                target.Send(ui.ToArray());
                                ui.Type = Network.GamePackets.TeamElitePKMatchUI.EndMatch;
                                team.SendMesageTeam(ui.ToArray(), 0);
                                target.Send(ui.ToArray());

                                if (T_ID == GamePackets.SkillElitePkBrackets)
                                    LocalSkillPkRounds.Deliver(target);

                                ui.Type = Network.GamePackets.TeamElitePKMatchUI.Reward;
                                target.Send(ui.ToArray());

                                if (Imports == 2 || MatchStats.Length != 3 || MatchStats[0] == null)
                                {
                                    team_win.Flag = FighterStats.StatusFlag.Qualified;
                                }
                                else
                                {
                                    var dictionar = MatchStats.Where(p => !p.Lost).ToArray();
                                    if (dictionar.Length == 2)
                                    {
                                        Flag = StatusFlag.SwitchOut;
                                        team_win.Flag = FighterStats.StatusFlag.Waiting;
                                        team_win.OnNextMatch = true;
                                    }
                                    else
                                        team_win.Flag = FighterStats.StatusFlag.Qualified;
                                }
                            }
                        }
                    }
                    Network.GamePackets.TeamElitePkBrackets brackets = new Network.GamePackets.TeamElitePkBrackets(T_ID, 1);
                    {
                        brackets.Group = (ushort)GroupID;
                        brackets.GUIType = (ushort)(T_ID==GamePackets.SkillElitePkBrackets?SkillTeamTournament.Tournaments[GroupID]:TeamTournament.Tournaments[GroupID]).State;
                        brackets.TotalMatchesOnRoom = brackets.MatchCount = 1;
                        brackets.Type = Network.GamePackets.TeamElitePkBrackets.StaticUpdate;
                        brackets.Append(this);
                        Kernel.SendWorldMessage(brackets.ToArray());
                    }
                    foreach (var clien in Watchers.Values)
                        if (clien.Entity.MapID == Map.ID)
                            LeaveWatch(clien);
                }
                catch (Exception e) { Console.WriteLine(e.ToString()); }
            }
            public void Export() { lock(this){ExportOwned();} }
            private void ExportOwned()
            {
                try
                {
                    if (Time32.Now > DoneStamp.AddSeconds(3) || TimeLeft == 0)
                    {
                        #region !Done
                        if (!Done)
                        {
                            if (MatchStats.Length == 1)
                            {
                                MatchStats[0].Flag = FighterStats.StatusFlag.Qualified;
                                Flag = StatusFlag.OK;
                                Exported = true;

                                Exported = true;
                                Completed = true;
                                return;
                            }
                            else
                            {
                                var fighters = MatchStats.Where(p => p.Fighting).ToArray();
                                if (fighters.Length > 1)
                                {
                                    if (fighters[0].Fighting && fighters[1].Fighting)
                                    {
                                        if (fighters[0].Points == fighters[1].Points)
                                            End(fighters.First(p => p.Team.UID == fighters[0].Team.UID).Team);
                                        else
                                        {
                                            // End takes the losing team. Preserve the higher-scoring team.
                                            if (fighters[0].Points < fighters[1].Points)
                                                End(fighters.First(p => p.Team.UID == fighters[0].Team.UID).Team);
                                            else
                                                End(fighters.First(p => p.Team.UID == fighters[1].Team.UID).Team);
                                        }
                                    }
                                }
                                else if (fighters.Length == 1)
                                    End(fighters.First(p => p.Team.UID == fighters[0].Team.UID).Team);
                            }
                        }
                        #endregion
                        foreach (var teams in MatchStats)
                        {
                            if (teams != null)
                            {
                                if (!teams.Waiting && teams.Teleported || teams.OnNextMatch)
                                {
                                    foreach (var player in teams.Team.Players)
                                    {

                                        if (player.Entity.MapID == Map.ID)
                                        {
                                            player.Entity.InSkillPk = false;
                                            if (T_ID == GamePackets.TeamElitePkBrackets)
                                            {
                                                var map = TeamTournament.Tournaments[GroupID].WaitingArea;
                                                var coords = map.RandomCoordinates();
                                                player.Entity.Teleport(map.ID, coords.Item1, coords.Item2);
                                            }

                                            else
                                            {
                                                player.LoadItemStats();
                                                var map = SkillTeamTournament.Tournaments[GroupID].WaitingArea;
                                                var coords = map.RandomCoordinates();
                                                player.Entity.Teleport(map.ID, coords.Item1, coords.Item2);
                                            }
                                            player.Entity.PKMode=player.Entity.PrevPKMode;
                                            player.Send(new Data(true){UID=player.Entity.UID,ID=Data.ChangePKMode,dwParam=(uint)player.Entity.PKMode});
                                            if (player.Entity.Hitpoints == 0)
                                                player.Entity.BringToLife();
                                        }
                                    }
                                    teams.Teleported = true;
                                }
                            }
                        }

                        Exported = true;
                        Completed = true;
                    }
                }
                catch (Exception e) { Console.WriteLine(e.ToString()); }
            }
            public void SwitchBetween()
            {
                if (Imports == 1 && MatchStats.Length == 3 && MatchStats[0] != null && !MatchStats[0].Lost)
                {
                    MatchStats[0].Flag = FighterStats.StatusFlag.Waiting;
                    if (MatchStats[1].Winner)
                    {
                        MatchStats[1].Teleported = false;
                        MatchStats[1].Flag = FighterStats.StatusFlag.Waiting;

                    }
                    if (MatchStats[2].Winner)
                    {
                        MatchStats[2].Teleported = false;
                        MatchStats[2].Flag = FighterStats.StatusFlag.Waiting;
                    }
                    Completed = false;
                }
            }
            private TeamElitePk.FighterStats targetOfWin(Game.ConquerStructures.Team team)
            {
                var dictionar = MatchStats.Where(p => p.Fighting).ToArray();
                for (int i = 0; i < dictionar.Length; i++)
                    if (dictionar[i] != null)
                        if (dictionar[i].Team.UID != team.UID)
                            return dictionar[i];
                return null;
            }
            public Network.GamePackets.TeamElitePKMatchStats CreateUpdate()
            {
                Network.GamePackets.TeamElitePKMatchStats stats = new Network.GamePackets.TeamElitePKMatchStats(T_ID);
                stats.Append(this);
                return stats;
            }
            public void BeginWatch(Client.GameState client)
            {
                if (Watchers.ContainsKey(client.Entity.UID))
                    return;
                Watchers.TryAdd(client.Entity.UID, client);
                client.Entity.SkillTeamWatchingElitePKMatch = this;
                var coords = Map.RandomCoordinates();
                client.Entity.Teleport(Map.BaseID, Map.ID, coords.Item1, coords.Item2);

                Network.GamePackets.ElitePKWatch watch = new Network.GamePackets.ElitePKWatch(true, MatchStats.Length);

                watch.ID = ID;
                watch.Type = Network.GamePackets.ElitePKWatch.Fighters;
                watch.dwCheers1 = MatchStats[0].Cheers;
                watch.dwCheers2 = MatchStats[1].Cheers;
                watch.Append(MatchStats[0].Name);
                watch.Append(MatchStats[1].Name);
                client.Send(watch.ToArray());
                watch = new Network.GamePackets.ElitePKWatch(true, Watchers.Count);
                watch.ID = ID;
                watch.dwCheers1 = MatchStats[0].Cheers;
                watch.dwCheers2 = MatchStats[1].Cheers;
                foreach (var pClient in Watchers.Values)
                    watch.Append(pClient.Entity.Mesh, pClient.Entity.Name);
                client.Send(watch.ToArray());
                client.Send(CreateUpdate().ToArray());

                UpdateWatchers();
            }
            public void Update()
            {
                var update = CreateUpdate();
                foreach (var player in Players)
                    if (player != null)
                        if (player.Team.EliteFighterStats.Fighting)
                            player.Send(update.ToArray());
            }
            public void UpdateWatchers()
            {
                Network.GamePackets.ElitePKWatch watch = new Network.GamePackets.ElitePKWatch(true, Watchers.Count);
                {
                    watch.ID = ID;
                    watch.Type = Network.GamePackets.ElitePKWatch.Watchers;
                    watch.dwCheers1 = MatchStats[0].Cheers;
                    watch.dwCheers2 = MatchStats[1].Cheers;
                    foreach (var pClient in Watchers.Values)
                        watch.Append(pClient.Entity.Mesh, pClient.Entity.Name);
                    foreach (var pClient in Watchers.Values)
                        pClient.Send(watch.ToArray());
                    foreach (var pClient in Players)
                        if (pClient != null)
                            pClient.Send(watch.ToArray());
                }
            }
            public void LeaveWatch(Client.GameState client)
            {
                if (this.Watchers.TryRemove(client.Entity.UID, out client))
                {
                    client.Entity.SkillTeamWatchingElitePKMatch = null;
                    ElitePKWatch elitePKWatch = new ElitePKWatch(true, 0);
                    elitePKWatch.ID = this.ID;
                    elitePKWatch.Type = 3;
                    client.Send(elitePKWatch.ToArray());
                    client.Entity.PreviousTeleport();
                }
            }
            public void Cheer(Client.GameState client, uint fighter)
            {
                //client.WatchingEm = false;
                MatchStats.First(p => p != null && p.Team.Contain(fighter)).Team
                    .EliteFighterStats.Cheers++;
                UpdateWatchers();
            }
        }
        public FighterStats[] Top8 = new FighterStats[0];
        public ConcurrentDictionary<uint, Game.ConquerStructures.Team> Teams;
        private readonly ConcurrentDictionary<uint,Enums.PKMode> entrants=new ConcurrentDictionary<uint,Enums.PKMode>();
        private readonly HashSet<Map> ownedMatchMaps=new HashSet<Map>();
        internal static int MatchMinutes(GamePackets series,int stage) {
            if(series!=GamePackets.SkillElitePkBrackets)return 1;
            return stage==States.GUI_Top4Qualifier || stage==States.GUI_Top2Qualifier
                || stage==States.GUI_Top3 || stage==States.GUI_Top1?15:10;
        }
        private Match CreateOwnedMatch(params FighterStats[] fighters) {
            var match=new Match(ID,fighters){RoundMinutes=MatchMinutes(ID,State)};
            if(match.Map!=null)ownedMatchMaps.Add(match.Map);
            if(match.Map==null || match.MatchStats==null)throw new InvalidOperationException("Local Team PK match allocation failed.");
            return match;
        }
        private bool ReleaseMatchMaps() {
            foreach(var map in ownedMatchMaps.ToArray()) {
                Map current;
                if(!Kernel.Maps.TryGetValue(map.ID,out current) || !ReferenceEquals(current,map)){ownedMatchMaps.Remove(map);continue;}
                if(Kernel.GamePool.Values.Any(p=>p.Entity!=null && p.Entity.MapID==map.ID))continue;
                map.Dispose();ownedMatchMaps.Remove(map);
            }
            if(ownedMatchMaps.Count!=0)return false;
            Matches.Clear();MatchArray=null;Top4MatchArray=null;ExtendedMatchArray=null;
            return true;
        }
        public ConcurrentDictionary<uint, Match> Matches;
        private Match[] MatchArray;
        private Match[] Top4MatchArray;
        private Match[] ExtendedMatchArray;
        private Counter MatchCounter;
        private ushort MatchIndex;
        public bool Alive = false;
        public DateTime ConstructTop8 = new DateTime();
        public Map WaitingArea;
        public int State;
        private int pState = States.T_Organize;
        public int GroupID;
        private bool willAdvance;
        private Time32 pStamp;
        public GamePackets ID;
        public ushort TimeLeft
        {
            get
            {
                int value = (int)(((pStamp.AllMilliseconds() - Time32.Now.AllMilliseconds())) / 1000);
                if (value < 0) return 0;
                return (ushort)value;
            }
        }
        public TeamElitePk(int group, GamePackets T_ID)
        {
            if (!Alive)
            {

                ID = T_ID;
                Alive = true;
                Teams = new ConcurrentDictionary<uint, ConquerStructures.Team>();
                Matches = new ConcurrentDictionary<uint, Match>();

                GroupID = group;
                MatchCounter = new Counter((uint)(GroupID * 100000 + 100000));

                WaitingArea = Kernel.Maps[(ushort)ElitePKTournament.WaitingAreaID].MakeDynamicMap();
                Constants.PKForbiddenMaps.Add(WaitingArea.ID);

                State = States.GUI_Top8Ranking;
                pState = States.T_Organize;
            }
        }
        public bool SignUp(Client.GameState client)
        {
            if(System.Threading.Interlocked.CompareExchange(ref callbackOwner,1,0)!=0)return false;
            try {
                if(client==null || client.Entity==null || client.Team==null || !SessionInstall.TryEnter(client.Entity.UID))return false;
                try {
                    using(var owned=client.Team.OwnCommand(client,client,false)) {
                        if(owned==null)return false;
                        return SignUpOwned(client);
                    }
                } finally {SessionInstall.Exit(client.Entity.UID);}
            } finally {System.Threading.Volatile.Write(ref callbackOwner,0);}
        }
        private bool SignUpOwned(Client.GameState client)
        {
            uint clerk=ID==GamePackets.TeamElitePkBrackets?417171u:801717u;
            if (!Alive || !AllowJoin || DateTime.Now>=StarTimer || !QuestInventory.Near(client,clerk,1002) || WaitingArea==null
                || client.Team.Lider==null || !client.Team.RightTeamElite() || LevelGroup(client.Entity.Level)!=GroupID
                || client.Team.Members.Count<1 || client.Team.Members.Count>5
                || client.Team.Players.Any(p=>!QuestInventory.Player(p) || !ReferenceEquals(p.Team,client.Team)))return false;
            Game.ConquerStructures.Team registered;
            if(Teams.TryGetValue(client.Team.UID,out registered) && !ReferenceEquals(registered,client.Team))return false;
            {
                if(ID==GamePackets.SkillElitePkBrackets) {
                    string issue=client.SkillEquipmentIssue();
                    if(issue!=null){client.Send(issue);return false;}
                }
                if(!LocalTeamPkAdmissions.Reserve(ledgerRun,client))return false;
                var coords = WaitingArea.RandomCoordinates();
                client.Entity.Teleport(WaitingArea.ID, coords.Item1, coords.Item2);
                if(client.Entity.MapID!=WaitingArea.ID){LocalTeamPkAdmissions.Return(client,true);return false;}
                entrants.TryAdd(client.Entity.UID,client.Entity.PKMode);
                if (!Teams.ContainsKey(client.Team.UID)) {
                    client.Team.SetEliteFighterStats(true);
                    Teams.TryAdd(client.Team.UID, client.Team);
                }

                Network.GamePackets.TeamElitePKMatchUI ui = new Network.GamePackets.TeamElitePKMatchUI(ID);
                {
                    ui.Type = Network.GamePackets.TeamElitePKMatchUI.Information;
                    ui.Append(client.Team);
                    client.Send(ui.ToArray());
                    ui.Type = 5;
                    client.Send(ui.ToArray());
                }
                if (client.Team.TeamLider(client))
                {
                    Network.GamePackets.SkillEliteSetTeamName teamname = new Network.GamePackets.SkillEliteSetTeamName(ID);
                    {
                        teamname.TeamID = client.Team.UID;
                        teamname.TeamName = client.Team.EliteFighterStats.Name;
                        client.Send(teamname.ToArray());
                    }

                }
                return true;
            }
        }
        internal bool CurrentParticipant(Client.GameState player) {
            if(!entrants.ContainsKey(player.Entity.UID))return false;
            // A running bracket callback owns its mutable maps. Defer recovery until it finishes.
            if(System.Threading.Interlocked.CompareExchange(ref callbackOwner,1,0)!=0)return true;
            try {
                return Alive && Teams.Values.Any(t=>t.Players.Any(p=>ReferenceEquals(p,player)))
                    && (player.Entity.MapID==WaitingArea.ID || ownedMatchMaps.Any(m=>ReferenceEquals(m,player.Map)));
            } finally {System.Threading.Volatile.Write(ref callbackOwner,0);}
        }
        private void ReturnEntrants() {
            foreach(var admission in entrants) {
                Client.GameState player;
                if(!Kernel.GamePool.TryGetValue(admission.Key,out player) || !QuestInventory.Player(player,false))continue;
                if(!LocalTeamPkAdmissions.Return(player,true))throw new InvalidOperationException("Local Team PK return is pending.");
                if(ledgerFinalized && ID==GamePackets.TeamElitePkBrackets && GroupID==3) {
                    if(player.Entity.MyAchievement!=null)player.Entity.MyAchievement.Invalidate();
                    TitleRequirements.Reconcile(player,false);
                }
            }
        }
        DateTime StarTimer = new DateTime();
        private bool AllowJoin = false;
        private IDisposable Subscriber;
        private Guid ledgerRun;
        internal Guid LedgerRun {get{return ledgerRun;}}
        private bool ledgerFinalized;
        private bool importRecoveryPending;
        internal void FailedSkillImport(Exception error) {
            importRecoveryPending=true;Alive=false;AllowJoin=false;pendingResult=null;ledgerFinalized=false;
            Console.WriteLine("[skill-team-pk] import_recovery group="+GroupID+" error="+error.GetType().Name);
        }
        internal bool ResultPayable {get{return !importRecoveryPending && (ledgerRun==Guid.Empty || ledgerFinalized);}}
        private LocalTeamPkLedger.Result pendingResult;
        public bool SubscribeTimer(int minutes)
        {
            DateTime local=DateTime.Now;
            return SubscribeUntil(local,local.AddMinutes(minutes));
        }
        internal bool SubscribeUntil(DateTime local, DateTime deadline)
        {
            if(System.Threading.Interlocked.CompareExchange(ref callbackOwner,1,0)!=0)return false;
            try {return SubscribeOwned(local,deadline);}
            finally {System.Threading.Volatile.Write(ref callbackOwner,0);}
        }
        private bool SubscribeOwned(DateTime local, DateTime deadline)
        {
            if(deadline<=local)return false;
            Guid candidate=LocalTeamPkLedger.Identity(ID,GroupID,local);
            if(candidate==ledgerRun || ownedMatchMaps.Count!=0)return false;
            if(!LocalTeamPkLedger.Begin(candidate,ID,GroupID,DateTime.UtcNow))return false;
            ledgerRun=candidate;pendingResult=null;ledgerFinalized=false;importRecoveryPending=false;
            Alive=true;State=States.GUI_Top8Ranking;pState=States.T_Organize;
            Teams.Clear();Matches.Clear();entrants.Clear();
            StarTimer = deadline;
            AllowJoin = true;
            if (Subscriber == null)
                Subscriber = Thread.Subscribe(timerCallback, 1000);
            return true;
        }
        public void Purge()
        {
            foreach (var team in Teams.Values)
            {
                if (State == States.GUI_Top8Qualifier)
                {
                    if (Teams.Count > 8)
                    {
                        if (team.EliteFighterStats.Lost || !team.OnPopulates)
                        {
                            Game.ConquerStructures.Team remover;
                            Teams.TryRemove(team.UID, out remover);
                        }
                    }
                }
                else if (team.EliteFighterStats.Lost)
                {
                    if (Teams.Count > 8)
                    {
                        Game.ConquerStructures.Team remover;
                        Teams.TryRemove(team.UID, out remover);
                    }
                }
            }
        }
        private int callbackOwner;
        private void timerCallback(int tim)
        {
            if(System.Threading.Interlocked.CompareExchange(ref callbackOwner,1,0)!=0)return;
            try {TimerOwned(tim);}
            finally {System.Threading.Volatile.Write(ref callbackOwner,0);}
        }
        private void TimerOwned(int tim)
        {
            try
            {

                if(importRecoveryPending) {
                    LocalTeamPkLedger.Cancel(ledgerRun);
                    ReturnEntrants();
                    if(!ReleaseMatchMaps())return;
                    importRecoveryPending=false;
                }
                if (!Alive)
                {
                    AllowJoin=false;
                    if (ID == GamePackets.SkillElitePkBrackets)
                        SkillTeamTournament.Opened = false;
                    else
                        if (ID == GamePackets.TeamElitePkBrackets)
                            TeamTournament.Opened = false;
                    if(Subscriber!=null){Subscriber.Dispose();Subscriber=null;}
                    return;
                }
                #region ReconstructTop
                if (State == States.GUI_ReconstructTop)
                {
                    if (DateTime.Now > ConstructTop8.AddMinutes(2))
                    {
                        ReturnEntrants();
                        if(!ReleaseMatchMaps())return;
                        State = States.GUI_Top8Ranking;
                        Kernel.SendWorldMessage(new Network.GamePackets.TeamElitePkBrackets(ID) { Type = 6, OnGoing = false }.ToArray());
                        Alive = false;
                    }
                    return;
                }
                #endregion
                DateTime Now64 = DateTime.Now;
                if (Now64 > StarTimer)
                {
                    AllowJoin=false;
                    SkillTeamTournament.Opened = false;
                    TeamTournament.Opened = false;
                    #region GetState
                    if (State == States.GUI_Top8Ranking)
                    {
                        foreach(var team in Teams.Values.ToArray()) {
                            if(team.Lider!=null && team.Members.Count>0 && team.RightTeamElite()
                                && team.Players.All(p=>QuestInventory.Player(p) && p.Entity.MapID==WaitingArea.ID && LevelGroup(p.Entity.Level)==GroupID))continue;
                            foreach(var p in team.Players)if(QuestInventory.Player(p,false) && p.Entity.MapID==WaitingArea.ID)p.Entity.Teleport(1002,327,303);
                            Game.ConquerStructures.Team removed;Teams.TryRemove(team.UID,out removed);
                        }
                        if(ID==GamePackets.TeamElitePkBrackets)
                            LocalTeamPkAdmissions.EarnEntry(ledgerRun,Teams.Values.SelectMany(t=>t.Players).Select(p=>(long)p.Entity.UID).Distinct().ToArray());
                        willAdvance = Teams.Count > 8;
                        Kernel.SendWorldMessage(new Network.GamePackets.TeamElitePkBrackets(ID) { Type = 6, OnGoing = true }.ToArray());
                        foreach (var team in Teams.Values)
                        {
                            foreach (var player in team.Players)
                            {
                                if (player.Team != null)
                                {
                                    Network.GamePackets.SkillEliteSetTeamName teamname = new Network.GamePackets.SkillEliteSetTeamName(ID);
                                    {
                                        teamname.Type = Network.GamePackets.SkillEliteSetTeamName.SuccessfulName;
                                        teamname.TeamID = player.Team.UID;
                                        teamname.TeamName = player.Team.EliteFighterStats.Name;
                                        player.Send(teamname.ToArray());

                                        if (player.Team.TeamLider(player))
                                        {
                                            teamname.Type = Network.GamePackets.SkillEliteSetTeamName.Remove;
                                            player.Send(teamname.ToArray());
                                        }
                                    }
                                    if (ID == GamePackets.TeamElitePkBrackets)
                                        LocalTeamPkAdmissions.DeliverEntry(player);
                                }
                            }
                        }
                        if (Teams.Count < 8)
                        {
                            ReturnEntrants();
                            LocalTeamPkLedger.Cancel(ledgerRun);
                            Teams.Clear();
                            Alive = false;
                            Kernel.SendWorldMessage(new Network.GamePackets.TeamElitePkBrackets(ID) { Type = 6, OnGoing = false }.ToArray());

                            return;
                        }
                        Kernel.SendWorldMessage(new Network.GamePackets.TeamElitePkBrackets(ID) { Type = 6, OnGoing = true }.ToArray());
                        LocalTeamPkAdmissions.RecordParticipation(ledgerRun,Teams.Values.SelectMany(t=>t.Players).Select(p=>(long)p.Entity.UID).Distinct().ToArray());
                        if(ID==GamePackets.TeamElitePkBrackets && GroupID==3)LocalTeamPkWardrobe.FreezeRoster(ledgerRun,Teams.Values.ToArray());
                        Top8 = new FighterStats[8];
                        if (Teams.Count == 8)
                            State = States.GUI_Top4Qualifier;
                        else if (willAdvance && Teams.Count <= 24)
                            State = States.GUI_Top8Qualifier;
                        else
                            State = States.GUI_Knockout;
                    }
                    #endregion
                    #region Knockout
                    if (State == States.GUI_Knockout)
                    {
                        AllowJoin = false;
                        if (pState == States.T_Organize)
                        {
                            if (willAdvance && Teams.Count <= 24)
                            {
                                State = States.GUI_Top8Qualifier;
                            }
                            else
                            {
                                MatchIndex = 0;
                                var array = Teams.Values.ToArray();
                                ushort counter = 0;
                                bool NoPar = array.Length % 2 == 0;
                                if (NoPar)
                                {
                                    for (ushort x = 0; x < array.Length; x++)
                                    {
                                        int r = counter;
                                        counter++;
                                        int t = counter;
                                        counter++;
                                        if (counter <= array.Length)
                                        {
                                            Match match = CreateOwnedMatch( array[r].EliteFighterStats, array[t].EliteFighterStats);
                                            match.Index = MatchIndex++;
                                            match.ID = MatchCounter.Next;
                                            Matches.TryAdd(match.ID, match);
                                        }
                                    }
                                }
                                else
                                {
                                    for (ushort x = 0; x < array.Length - 1; x++)
                                    {
                                        int r = counter;
                                        counter++;
                                        int t = counter;
                                        counter++;
                                        if (counter <= array.Length)
                                        {
                                            Match match = CreateOwnedMatch( array[r].EliteFighterStats, array[t].EliteFighterStats);
                                            match.Index = MatchIndex++;
                                            match.ID = MatchCounter.Next;
                                            Matches.TryAdd(match.ID, match);
                                        }
                                    }
                                    Match math_bye = CreateOwnedMatch( array[array.Length - 1].EliteFighterStats);
                                    math_bye.Index = MatchIndex++;
                                    math_bye.ID = MatchCounter.Next;
                                    Matches.TryAdd(math_bye.ID, math_bye);
                                }
                                pStamp = Time32.Now.AddSeconds(60);
                                pState = States.T_Wait;
                                MatchArray = (from match in Matches.Values orderby match.MatchStats.Length descending select match).ToArray();
                                var brackets = CreateBrackets(MatchArray, 0);
                                foreach (var packet in brackets)
                                {
                                    Kernel.SendWorldMessage(packet.ToArray());
                                }
                            }
                        }
                        else if (pState == States.T_Wait)
                        {
                            if (TimeLeft == 0)
                            {
                                int doneMatches = 0;

                                foreach (var match in MatchArray)
                                {
                                    match.AliveMatch();
                                    if (!match.Inside)
                                    {
                                        match.Import(Match.StatusFlag.Watchable);
                                    }
                                    else
                                    {
                                        if (!match.Exported && (match.Done || match.TimeLeft == 0))
                                        {
                                            match.Export();
                                        }
                                        if (match.Exported || match.MatchStats.Length == 1)
                                            doneMatches++;
                                    }
                                }
                                if (doneMatches == Matches.Count)
                                {
                                    Matches.Clear();
                                    Purge();
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
                            if (Teams.Count == 8)
                            {
                                State = States.GUI_Top4Qualifier;
                            }
                            else
                            {
                                MatchIndex = 0;
                                var array = Teams.Values.ToArray();
                                int[] taken = new int[array.Length];

                                if (array.Length <= 16)
                                {
                                    ushort counter = 0;
                                    int t1Group = array.Length - 8;
                                    int lim = taken.Length / 2;
                                    for (int i = 0; i < t1Group; i++)
                                    {
                                        int r = counter;
                                        counter++;
                                        int t = counter;
                                        counter++;
                                        Match match = CreateOwnedMatch( array[r].EliteFighterStats, array[t].EliteFighterStats);
                                        match.Index = MatchIndex++;
                                        match.ID = MatchCounter.Next;
                                        Matches.TryAdd(match.ID, match);
                                    }
                                    for (int i = 0; i < 8 - t1Group; i++)
                                    {
                                        ushort r = counter;
                                        counter++;
                                        Match match = CreateOwnedMatch( array[r].EliteFighterStats);
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
                                        int r = counter;
                                        counter++;
                                        int t = counter;
                                        counter++;
                                        int y = counter;
                                        counter++;
                                        Match match = CreateOwnedMatch( array[r].EliteFighterStats, array[t].EliteFighterStats, array[y].EliteFighterStats);
                                        match.Index = MatchIndex++;
                                        match.ID = MatchCounter.Next;
                                        Matches.TryAdd(match.ID, match);
                                    }
                                    int t2GroupCount = array.Length - counter;
                                    for (int i = 0; i < t2GroupCount / 2; i++)
                                    {
                                        int r = counter;
                                        counter++;
                                        int t = counter;
                                        counter++;
                                        Match match = CreateOwnedMatch( array[r].EliteFighterStats, array[t].EliteFighterStats);
                                        match.Index = MatchIndex++;
                                        match.ID = MatchCounter.Next;
                                        Matches.TryAdd(match.ID, match);
                                    }
                                }
                                pStamp = Time32.Now.AddSeconds(60);
                                pState = States.T_Wait;
                                MatchArray = (from match in Matches.Values orderby match.MatchStats.Length descending select match).ToArray();

                                var brackets = CreateBrackets(MatchArray, 0, Network.GamePackets.TeamElitePkBrackets.UpdateList);
                                foreach (var packet in brackets)
                                {
                                    Kernel.SendWorldMessage(packet.ToArray());
                                }
                                Network.GamePackets.TeamElitePkBrackets brea = new Network.GamePackets.TeamElitePkBrackets(0);
                                brea.Type = Network.GamePackets.TeamElitePkBrackets.EPK_State;
                                brea.OnGoing = true;
                                Kernel.SendWorldMessage(brea.ToArray());
                            }
                        }
                        else if (pState == States.T_Wait)
                        {
                            if (TimeLeft == 0)
                            {
                                int doneMatches = 0;
                                foreach (var match in MatchArray)
                                {
                                    match.AliveMatch();
                                    if (!match.Inside)
                                    {
                                        if (match.MatchStats.Length > 1)
                                        {
                                            match.Import(Match.StatusFlag.Watchable);
                                        }
                                        else
                                        {
                                            match.Inside = true;
                                            match.Completed = true;
                                            match.Done = true;
                                            match.MatchStats
                                                .First(p => p != null).Team
                                                .EliteFighterStats.Flag = FighterStats.StatusFlag.Qualified;
                                        }
                                    }
                                    else
                                    {
                                        if (!match.Exported && (match.Done || match.TimeLeft == 0))
                                        {
                                            match.Export();
                                            if (match.MatchStats.Length == 3)
                                                match.SwitchBetween();
                                            else
                                                match.Flag = Match.StatusFlag.OK;
                                        }

                                        if (match.Exported || match.MatchStats.Length == 1)
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
                                                    stats.Reset(true);
                                                match.Inside = false;
                                                match.Done = false;
                                                match.Exported = false;
                                                if (match.MatchStats.Length != 1)
                                                    match.Flag = Match.StatusFlag.Waiting;
                                            }
                                        }
                                        pStamp = Time32.Now.AddSeconds(60);
                                        pState = States.T_Wait;
                                        var brakets = CreateBrackets(MatchArray, 0, Network.GamePackets.TeamElitePkBrackets.UpdateList);
                                        foreach (var packet in brakets)
                                        {
                                            Kernel.SendWorldMessage(packet.ToArray());
                                        }

                                    }
                                    else
                                    {
                                        Matches.Clear();
                                        Purge();
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
                            try
                            {
                                MatchIndex = 0;
                                var array = Teams.Values.ToArray();
                                int[] taken = new int[array.Length];
                                int lim = taken.Length / 2;
                                for (int i = 0; i < taken.Length; i += 2)
                                {
                                    taken[i] = taken[i + 1] = 1;
                                    Match match = CreateOwnedMatch( array[i].EliteFighterStats, array[i + 1].EliteFighterStats);
                                    match.Index = MatchIndex++;
                                    //match.Flag = Match.StatusFlag.FinalMatch;
                                    match.ID = MatchCounter.Next;
                                    Matches.TryAdd(match.ID, match);
                                }
                                pStamp = Time32.Now.AddSeconds(60);
                                pState = States.T_Wait;
                                MatchArray = Matches.Values.ToArray();
                                var brackets = CreateBrackets(MatchArray, 0, Network.GamePackets.TeamElitePkBrackets.UpdateList);
                                foreach (var packet in brackets)
                                    Kernel.SendWorldMessage(packet.ToArray());
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine(e.ToString());
                            }

                        }
                        else if (pState == States.T_Wait)
                        {
                            if (TimeLeft == 0)
                            {
                                try
                                {
                                    int doneMatches = 0;

                                    foreach (var match in MatchArray)
                                    {
                                        match.AliveMatch();
                                        if (!match.Inside)
                                        {
                                            match.Import(Match.StatusFlag.Watchable);
                                        }
                                        else
                                        {
                                            if (!match.Exported && (match.Done || match.TimeLeft == 0))
                                                match.Export();
                                            if (match.Exported)
                                                doneMatches++;
                                        }
                                    }
                                    if (doneMatches == MatchArray.Length)
                                    {
                                        int i = 7;
                                        foreach (var match in MatchArray)
                                        {
                                            foreach (var stats in match.MatchStats)
                                                if (stats.Flag == FighterStats.StatusFlag.Lost)
                                                    Top8[i--] = stats.Clone();//fara clone;
                                            match.Commit();
                                        }
                                        State = States.GUI_Top2Qualifier;
                                        pState = States.T_Organize;
                                    }
                                }
                                catch (Exception e)
                                {
                                    Console.WriteLine(e.ToString());
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
                            try
                            {
                                MatchIndex = 0;
                                Top4MatchArray = new Match[2];

                                for (int i = 0; i < Top4MatchArray.Length; i++)
                                {
                                    Match match = CreateOwnedMatch(//p.winer este
                                        MatchArray[i].Return(p => p.Team.EliteFighterStats.Winner),//.MatchStats.First(p => p.Team.EliteFighterStats.Winner),
                                        MatchArray[i + 2].Return(p => p.Team.EliteFighterStats.Winner));//.MatchStats.First(p => p.Team.EliteFighterStats.Winner));
                                    match.Index = MatchIndex++;
                                    match.ID = MatchCounter.Next;
                                    Top4MatchArray[i] = match;
                                    Matches.TryAdd(match.ID, match);
                                }

                                pStamp = Time32.Now.AddSeconds(60);
                                pState = States.T_Wait;
                                var brackets = CreateBrackets(Top4MatchArray, 0, Network.GamePackets.TeamElitePkBrackets.UpdateList);
                                foreach (var packet in brackets)
                                    Kernel.SendWorldMessage(packet.ToArray());
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine(e.ToString());
                            }

                        }
                        else if (pState == States.T_Wait)
                        {
                            if (TimeLeft == 0)
                            {
                                try
                                {
                                    int doneMatches = 0;
                                    foreach (var match in Top4MatchArray)
                                    {
                                        match.AliveMatch();
                                        if (!match.Inside)
                                        {
                                            match.Import(Match.StatusFlag.Watchable);
                                        }
                                        else
                                        {
                                            if (!match.Exported && (match.Done || match.TimeLeft == 0))
                                                match.Export();
                                            if (match.Exported)
                                                doneMatches++;
                                        }
                                    }
                                    if (doneMatches == Top4MatchArray.Length)
                                    {
                                        foreach (var match in Top4MatchArray)
                                            match.Commit();

                                        pState = States.T_Organize;
                                        State = States.GUI_Top3;
                                    }
                                }
                                catch (Exception e)
                                {
                                    Console.WriteLine(e.ToString());
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
                            try
                            {
                                MatchIndex = 0;

                                ExtendedMatchArray = new Match[2];
                                Match match = CreateOwnedMatch(
                                    Top4MatchArray[0].Return(p => !p.Team.EliteFighterStats.Winner),//.MatchStats.First(p => !p.Team.EliteFighterStats.Winner),
                                    Top4MatchArray[1].Return(p => !p.Team.EliteFighterStats.Winner));//.MatchStats.First(p => !p.Team.EliteFighterStats.Winner));
                                match.Index = MatchIndex++;
                                match.ID = MatchCounter.Next;
                                ExtendedMatchArray[0] = match;

                                Match finalmatch = CreateOwnedMatch(
        Top4MatchArray[0].Return(p => p.Winner).Clone(),//.MatchStats.First(p => p.Team.EliteFighterStats.Winner).Clone(),
        Top4MatchArray[1].Return(p => p.Winner).Clone());//.MatchStats.First(p => p.Team.EliteFighterStats.Winner).Clone());
                                finalmatch.Flag = Match.StatusFlag.SwitchOut;
                                ExtendedMatchArray[1] = finalmatch;

                                Matches.TryAdd(match.ID, match);
                                pStamp = Time32.Now.AddSeconds(60);
                                pState = States.T_Wait;
                                var brackets = CreateBrackets(ExtendedMatchArray, 0, Network.GamePackets.TeamElitePkBrackets.UpdateList);
                                foreach (var packet in brackets)
                                    Kernel.SendWorldMessage(packet.ToArray());

                                Kernel.SendWorldMessage(new Network.GamePackets.TeamElitePkBrackets(ID) { Type = 6, OnGoing = true }.ToArray());
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine(e.ToString());
                            }
                        }
                        else if (pState == States.T_Wait)
                        {
                            if (TimeLeft == 0)
                            {
                                try
                                {
                                    var match = ExtendedMatchArray[0];
                                    match.AliveMatch();
                                    if (!match.Inside)
                                    {
                                        match.Import(Match.StatusFlag.Watchable);
                                    }
                                    else
                                    {
                                        if (!match.Exported && (match.Done || match.TimeLeft == 0))
                                            match.Export();
                                        if (match.Exported)
                                        {

                                            //var a_math =ExtendedMatchArray[0].MatchStats;
                                            var top3 = ExtendedMatchArray[0].Return(p => p.Team.EliteFighterStats.Winner);
                                            //.First(p => p.Team.EliteFighterStats.Winner).Team.EliteFighterStats;
                                            var top4 = ExtendedMatchArray[0].Return(p => !p.Team.EliteFighterStats.Winner);
                                            //ExtendedMatchArray[0].MatchStats
                                            //.First(p => !p.Team.EliteFighterStats.Winner).Team.EliteFighterStats;

                                            Top8[2] = top3;
                                            Top8[3] = top4;
                                            top3.Flag = FighterStats.StatusFlag.Lost;

                                            pState = States.T_Organize;
                                            State = States.GUI_Top1;
                                            foreach (var client in Kernel.GamePool.Values)
                                                Rankings(client);
                                            var brackets = CreateBrackets(ExtendedMatchArray, 0, Network.GamePackets.TeamElitePkBrackets.UpdateList);
                                            foreach (var packet in brackets)
                                                Kernel.SendWorldMessage(packet.ToArray());

                                        }
                                    }
                                }
                                catch (Exception e)
                                {
                                    Console.WriteLine(e.ToString());
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
                            try
                            {
                                MatchIndex = 0;
                                //  var brackets = CreateBrackets(MatchArray, 0, Client.GamePackets.SkillElitePkBrackets.GUIEdit);
                                //  foreach (var packet in brackets)
                                //       CMD.MapPackets.Enqueue(new CMD.MapPackets.Models() { data = packet.ToArray() });
                                Match match = CreateOwnedMatch( ExtendedMatchArray[1].MatchStats);
                                ExtendedMatchArray = new Match[1];
                                match.Index = MatchIndex++;
                                match.ID = MatchCounter.Next;
                                ExtendedMatchArray[0] = match;
                                Matches.TryAdd(match.ID, match);
                                pStamp = Time32.Now.AddSeconds(60);
                                pState = States.T_Wait;
                                var brackets = CreateBrackets(ExtendedMatchArray, 0, Network.GamePackets.TeamElitePkBrackets.UpdateList);
                                foreach (var packet in brackets)
                                    Kernel.SendWorldMessage(packet.ToArray());

                                Kernel.SendWorldMessage(new Network.GamePackets.TeamElitePkBrackets(ID) { Type = 6, OnGoing = true }.ToArray());
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine(e.ToString());
                            }
                        }
                        else if (pState == States.T_Wait)
                        {
                            if (TimeLeft == 0)
                            {
                                try
                                {
                                    int doneMatches = 0;
                                    foreach (var match in ExtendedMatchArray)
                                    {
                                        match.AliveMatch();
                                        if (!match.Inside)
                                        {
                                            match.Import(Match.StatusFlag.Watchable);
                                        }
                                        else
                                        {
                                            if (!match.Exported && (match.Done || match.TimeLeft == 0))
                                                match.Export();
                                            if (match.Exported)
                                                doneMatches++;
                                        }
                                    }
                                    if (doneMatches == ExtendedMatchArray.Length)
                                    {
                                        try
                                        {
                                            var top1 = ExtendedMatchArray[0].MatchStats
                                                .First(p => p.Team.EliteFighterStats.Winner).Team.EliteFighterStats;
                                            var top2 = ExtendedMatchArray[0].MatchStats
                                                .First(p => !p.Team.EliteFighterStats.Winner).Team.EliteFighterStats;
                                            Top8[0] = top1;
                                            Top8[1] = top2;
                                            if(pendingResult==null)pendingResult=LocalTeamPkLedger.Freeze(Top8,ID,DateTime.UtcNow);
                                            LocalTeamPkLedger.Finish(ledgerRun,pendingResult);
                                            ledgerFinalized=true;
                                            ConstructTop8 = DateTime.Now;

                                            State = States.GUI_ReconstructTop;
                                            foreach (var client in Kernel.GamePool.Values)
                                                Rankings(client);
                                            var brackets = CreateBrackets(ExtendedMatchArray, 0, Network.GamePackets.TeamElitePkBrackets.UpdateList);
                                            foreach (var packet in brackets)
                                                Kernel.SendWorldMessage(packet.ToArray());
                                            Kernel.SendWorldMessage(new Network.GamePackets.TeamElitePkBrackets(ID) { Type = 6, OnGoing = true }.ToArray());
                                        }
                                        catch (Exception e)
                                        {
                                            Console.WriteLine(e.ToString());
                                        }
                                    }
                                }
                                catch (Exception e)
                                {
                                    Console.WriteLine(e.ToString());
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
        public void Update(Client.GameState client, int page = 0)
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
                        var brackets = CreateBrackets(MatchArray, 0, Network.GamePackets.TeamElitePkBrackets.GUIEdit);
                        foreach (var packet in brackets)
                            client.Send(packet.ToArray());
                    }
                    else
                    {
                        var brackets = CreateBrackets(MatchArray, page);
                        foreach (var packet in brackets)
                            client.Send(packet.ToArray());
                    }
                    if (State >= States.GUI_Top1)
                        Rankings(client);
                }
            }
            catch (Exception e) { Console.WriteLine(e.ToString()); }
        }
        public void Rankings(Client.GameState client)
        {
            Network.GamePackets.TeamElitePkTop ranks = new Network.GamePackets.TeamElitePkTop(ID);
            {
                ranks.Type = Network.GamePackets.TeamElitePkTop.Top8;
                ranks.Group = (uint)GroupID;
                ranks.GroupStatus = (uint)State;
                if (State >= States.GUI_Top1)
                {
                    ranks.Type = Network.GamePackets.TeamElitePkTop.Top3;
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
                    client.Send(ranks.ToArray());
                }
                else
                {
                    ranks.Count = (uint)Top8.Length;
                    for (int i = 0; i < ranks.Count; i++)
                        if (Top8[i] != null)
                            ranks.Append(Top8[i], i);
                    client.Send(ranks.ToArray());
                }
            }
        }
        private Network.GamePackets.TeamElitePkBrackets[] CreateBrackets(Match[] matches, int page = 0, byte type = 0)
        {
            try
            {
                int lim = 0, count = 0;
                if (matches == null) return new Network.GamePackets.TeamElitePkBrackets[0];
                if (State == States.GUI_Knockout)
                {
                    const int max = 5;
                    int offset = page * max;

                    int Do_count = matches.Length - page * max;

                    if (page * max > matches.Length)
                        Do_count = 0;
                    uint ccount = (uint)Math.Min(max, Do_count);

                    Network.GamePackets.TeamElitePkBrackets brackets = new Network.GamePackets.TeamElitePkBrackets(ID, (int)ccount);
                    brackets.Group = (ushort)GroupID;
                    brackets.GUIType = (ushort)State;
                    brackets.TotalMatchesOnRoom = (ushort)ccount;
                    brackets.Page = (byte)page;
                    brackets.TimeLeft = TimeLeft;
                    brackets.MatchCount = (ushort)matches.Length;
                    brackets.Type = type;
                    for (int i = 0; i < ccount; i++)
                    {
                        var element = matches[i + offset];
                        brackets.Append(element);
                    }
                    Network.GamePackets.TeamElitePkBrackets[] buffer = new Network.GamePackets.TeamElitePkBrackets[1];
                    buffer[0] = brackets;
                    return buffer;
                }
                else if (State == States.GUI_Top8Qualifier)
                {
                    const int max = 5;
                    int offset = page * max;

                    int Do_count = matches.Length - page * max;

                    if (page * max > matches.Length)
                        Do_count = 0;
                    uint ccount = (uint)Math.Min(max, Do_count);

                    Network.GamePackets.TeamElitePkBrackets[] buffer;
                    if (matches.Length > ccount)
                        buffer = new Network.GamePackets.TeamElitePkBrackets[2];
                    else
                        buffer = new Network.GamePackets.TeamElitePkBrackets[1];

                    Network.GamePackets.TeamElitePkBrackets brackets = new Network.GamePackets.TeamElitePkBrackets(ID, (int)ccount);
                    brackets.Group = (ushort)GroupID;
                    brackets.GUIType = (ushort)State;
                    brackets.TotalMatchesOnRoom = (ushort)ccount;
                    brackets.Page = (byte)page;
                    brackets.TimeLeft = TimeLeft;
                    brackets.MatchCount = (ushort)matches.Length;
                    brackets.Type = type;
                    for (int i = 0; i < ccount; i++)
                    {
                        var element = matches[i + offset];
                        brackets.Append(element);
                    }
                    buffer[0] = brackets;
                    if (matches.Length > ccount)
                    {
                        ushort towcount = (ushort)(matches.Length - ccount);
                        Network.GamePackets.TeamElitePkBrackets twobrackets = new Network.GamePackets.TeamElitePkBrackets(ID, (int)towcount);
                        twobrackets.Group = (ushort)GroupID;
                        twobrackets.GUIType = (ushort)State;
                        twobrackets.TotalMatchesOnRoom = (ushort)towcount;
                        twobrackets.Page = (byte)page;
                        twobrackets.TimeLeft = TimeLeft;
                        twobrackets.ListCount = 1;
                        twobrackets.MatchCount = (ushort)matches.Length;
                        twobrackets.Type = type;
                        for (int i = 0; i < towcount; i++)
                        {
                            var element = matches[i + ccount];
                            twobrackets.Append(element);
                        }
                        buffer[1] = twobrackets;
                        return buffer;
                    }
                    return buffer;
                }
                lim = matches.Length;
                count = Math.Min(lim, matches.Length - page * lim);

                if (State >= States.GUI_Top2Qualifier)
                {
                    lim = MatchArray.Length;
                    count = Math.Min(lim, MatchArray.Length - page * lim);
                    Network.GamePackets.TeamElitePkBrackets[] buffer = new Network.GamePackets.TeamElitePkBrackets[2];
                    if (Top4MatchArray == null)
                        return new Network.GamePackets.TeamElitePkBrackets[0];
                    Network.GamePackets.TeamElitePkBrackets qbrackets = new Network.GamePackets.TeamElitePkBrackets(ID, count);
                    {
                        qbrackets.Group = (ushort)GroupID;
                        qbrackets.GUIType = (ushort)State;
                        qbrackets.TotalMatchesOnRoom = (ushort)(count);
                        qbrackets.Page = (byte)page;
                        qbrackets.TimeLeft = TimeLeft;
                        qbrackets.MatchCount = (byte)(MatchArray.Length * 2);
                        qbrackets.Type = type;
                        for (int i = page * lim; i < page * lim + count; i++)
                            qbrackets.Append(MatchArray[i]);
                        buffer[0] = qbrackets;
                    }
                    if (State >= States.GUI_Top3)
                    {
                        lim = 4;
                        count = (byte)(Top4MatchArray.Length + ExtendedMatchArray.Length);
                        Network.GamePackets.TeamElitePkBrackets Twoqbrackets = new Network.GamePackets.TeamElitePkBrackets(ID, count);
                        {
                            Twoqbrackets.Group = (ushort)GroupID;
                            Twoqbrackets.GUIType = (ushort)State;
                            Twoqbrackets.TotalMatchesOnRoom = (ushort)count;
                            Twoqbrackets.Page = (byte)1;
                            Twoqbrackets.TimeLeft = TimeLeft;
                            Twoqbrackets.MatchCount = (byte)((Top4MatchArray.Length + ExtendedMatchArray.Length) * 2);
                            Twoqbrackets.Type = type;
                            for (int i = 0; i < 2; i++)
                                Twoqbrackets.Append(Top4MatchArray[i]);
                            for (int x = 0; x < ExtendedMatchArray.Length; x++)
                                Twoqbrackets.Append(ExtendedMatchArray[x]);
                            buffer[1] = Twoqbrackets;
                        }
                        buffer[1] = Twoqbrackets;
                    }
                    else
                    {
                        lim = 2;
                        count = Top4MatchArray.Length;
                        Network.GamePackets.TeamElitePkBrackets Twoqbrackets = new Network.GamePackets.TeamElitePkBrackets(ID, count);
                        {
                            Twoqbrackets.Group = (ushort)GroupID;
                            Twoqbrackets.GUIType = (ushort)State;
                            // Twoqbrackets.ListCount = 1;
                            Twoqbrackets.TotalMatchesOnRoom = (ushort)count;
                            Twoqbrackets.Page = (byte)1;
                            Twoqbrackets.TimeLeft = TimeLeft;
                            Twoqbrackets.MatchCount = (byte)MatchArray.Length;
                            Twoqbrackets.Type = type;
                            for (int i = page * lim; i < page * lim + count; i++)
                                Twoqbrackets.Append(Top4MatchArray[i]);
                            buffer[1] = Twoqbrackets;
                        }
                    }
                    return buffer;
                }

                Network.GamePackets.TeamElitePkBrackets aqbrackets = new Network.GamePackets.TeamElitePkBrackets(ID, count);
                {
                    aqbrackets.Group = (ushort)GroupID;
                    aqbrackets.GUIType = (ushort)State;
                    aqbrackets.TotalMatchesOnRoom = (ushort)count;
                    aqbrackets.Page = (byte)page;
                    aqbrackets.TimeLeft = TimeLeft;
                    aqbrackets.MatchCount = (ushort)matches.Length;
                    aqbrackets.Type = type;
                    for (int i = page * lim; i < page * lim + count; i++)
                        aqbrackets.Append(matches[i]);
                    Network.GamePackets.TeamElitePkBrackets[] buffer = new Network.GamePackets.TeamElitePkBrackets[1];
                    buffer[0] = aqbrackets;
                    return buffer;
                }
            }
            catch (Exception e) { Console.WriteLine(e.ToString()); return new Network.GamePackets.TeamElitePkBrackets[0]; }
        }
    }
}

namespace Revora.Game.Features.Tournaments
{
    // New local results only. Historical INI rankings are never imported as unpaid prizes.
    internal static class LocalTeamPkLedger
    {
        private static readonly object gate=new object();
        private static bool ready;
        internal sealed class Result {
            internal long[] Leaders,Silver;
            internal int[] Cps,Souls;
            internal DateTime Ended;
        }
        internal static int Kind(TeamElitePk.GamePackets kind) {
            if(kind==TeamElitePk.GamePackets.TeamElitePkBrackets)return 0;
            if(kind==TeamElitePk.GamePackets.SkillElitePkBrackets)return 1;
            throw new ArgumentOutOfRangeException("kind");
        }
        internal static Guid Identity(TeamElitePk.GamePackets kind,int bracket,DateTime local) {
            if(bracket<0 || bracket>3)throw new ArgumentOutOfRangeException("bracket");
            string key="revora.local-team-pk.v1:"+Kind(kind)+":"+bracket+":"+local.ToString("yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture);
            using(var hash=System.Security.Cryptography.SHA256.Create())return new Guid(hash.ComputeHash(Encoding.UTF8.GetBytes(key)).Take(16).ToArray());
        }
        internal static void Initialize() {
            lock(gate) {
                if(ready)return;
                using(var c=Database.DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    QuestInventory.Execute(c,t,@"CREATE TABLE IF NOT EXISTS revora_local_team_pk_runs(
run uuid PRIMARY KEY,kind integer NOT NULL CHECK(kind IN(0,1)),bracket integer NOT NULL CHECK(bracket BETWEEN 0 AND 3),
started_at timestamptz NOT NULL,ended_at timestamptz,state text NOT NULL CHECK(state IN('active','complete','cancelled')),
leaders bigint[] NOT NULL DEFAULT '{}',silver bigint[] NOT NULL DEFAULT '{}',cps integer[] NOT NULL DEFAULT '{}',souls integer[] NOT NULL DEFAULT '{}',
CHECK((state='active')=(ended_at IS NULL)),CHECK(ended_at IS NULL OR ended_at>=started_at),
CHECK((state='complete' AND cardinality(leaders)=8 AND cardinality(silver)=8 AND cardinality(cps)=8 AND cardinality(souls)=8)
OR (state<>'complete' AND cardinality(leaders)=0 AND cardinality(silver)=0 AND cardinality(cps)=0 AND cardinality(souls)=0)));
CREATE TABLE IF NOT EXISTS revora_local_team_pk_claims(
run uuid NOT NULL REFERENCES revora_local_team_pk_runs(run),uid bigint NOT NULL CHECK(uid>0),
place integer NOT NULL CHECK(place BETWEEN 1 AND 8),soul_uid bigint NOT NULL UNIQUE CHECK(soul_uid>0),
claimed_at timestamptz NOT NULL,PRIMARY KEY(run,uid),UNIQUE(run,place));
CREATE TABLE IF NOT EXISTS revora_local_team_pk_admissions(
run uuid NOT NULL REFERENCES revora_local_team_pk_runs(run),uid bigint NOT NULL CHECK(uid>0),
pkmode integer NOT NULL CHECK(pkmode BETWEEN 0 AND 255),returned boolean NOT NULL DEFAULT false,PRIMARY KEY(run,uid));
ALTER TABLE revora_local_team_pk_admissions ADD COLUMN IF NOT EXISTS participated_at timestamptz;
ALTER TABLE revora_local_team_pk_admissions ADD COLUMN IF NOT EXISTS entry_earned boolean NOT NULL DEFAULT false;
ALTER TABLE revora_local_team_pk_admissions ADD COLUMN IF NOT EXISTS entry_item bigint;
CREATE UNIQUE INDEX IF NOT EXISTS revora_local_team_pk_entry_item ON revora_local_team_pk_admissions(entry_item) WHERE entry_item IS NOT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS revora_local_team_pk_return_owner ON revora_local_team_pk_admissions(uid) WHERE NOT returned;
ALTER TABLE revora_local_team_pk_runs ADD COLUMN IF NOT EXISTS local_wardrobe boolean NOT NULL DEFAULT false;
ALTER TABLE revora_local_team_pk_runs ADD COLUMN IF NOT EXISTS roster_frozen boolean NOT NULL DEFAULT false;
CREATE TABLE IF NOT EXISTS revora_local_team_pk_roster(
run uuid NOT NULL,uid bigint NOT NULL,leader bigint NOT NULL CHECK(leader>0),contested boolean NOT NULL DEFAULT false,
PRIMARY KEY(run,uid),FOREIGN KEY(run,uid) REFERENCES revora_local_team_pk_admissions(run,uid));
CREATE TABLE IF NOT EXISTS revora_local_team_pk_battles(
run uuid NOT NULL REFERENCES revora_local_team_pk_runs(run),match bigint NOT NULL,round integer NOT NULL,
winner bigint NOT NULL CHECK(winner>0),loser bigint NOT NULL CHECK(loser>0 AND loser<>winner),members bigint[] NOT NULL,
PRIMARY KEY(run,match,round));
CREATE TABLE IF NOT EXISTS revora_skill_pk_rounds(
run uuid NOT NULL REFERENCES revora_local_team_pk_runs(run),match bigint NOT NULL,round integer NOT NULL CHECK(round>0),
winner bigint NOT NULL CHECK(winner>0),loser bigint NOT NULL CHECK(loser>0 AND loser<>winner),owners bigint[] NOT NULL,
PRIMARY KEY(run,match,round));
CREATE TABLE IF NOT EXISTS revora_skill_pk_round_rewards(
run uuid NOT NULL,match bigint NOT NULL,round integer NOT NULL,uid bigint NOT NULL,item_uid bigint UNIQUE,
PRIMARY KEY(run,match,round,uid),FOREIGN KEY(run,match,round) REFERENCES revora_skill_pk_rounds(run,match,round),
FOREIGN KEY(run,uid) REFERENCES revora_local_team_pk_admissions(run,uid));
UPDATE revora_local_team_pk_runs SET state='cancelled',ended_at=GREATEST(started_at,now()) WHERE state='active';");
                    t.Commit();ready=true;
                }}
            }
        }
        internal static bool Begin(Guid run,TeamElitePk.GamePackets kind,int bracket,DateTime utc) {
            if(run==Guid.Empty || bracket<0 || bracket>3)throw new ArgumentException("Invalid local Team PK run.");
            Initialize();int type=Kind(kind);
            using(var c=Database.DataHolder.MySqlConnection){c.Open();
                return QuestInventory.Execute(c,null,@"INSERT INTO revora_local_team_pk_runs(run,kind,bracket,started_at,state,local_wardrobe)
VALUES(@run,@kind,@bracket,@now,'active',@wardrobe) ON CONFLICT(run) DO NOTHING","run",run,"kind",type,"bracket",bracket,"now",utc,"wardrobe",type==0 && bracket==3)==1;
            }
        }
        internal static Result Freeze(TeamElitePk.FighterStats[] placements,TeamElitePk.GamePackets kind,DateTime utc) {
            if(placements==null || placements.Length!=8 || placements.Any(p=>p==null || p.LeaderUID==0)
                || placements.Select(p=>p.LeaderUID).Distinct().Count()!=8)throw new InvalidOperationException("Local Team PK needs eight distinct placed leaders.");
            int type=Kind(kind);var result=new Result{Leaders=placements.Select(p=>(long)p.LeaderUID).ToArray(),Silver=new long[8],Cps=new int[8],Souls=new int[8],Ended=utc};
            for(int i=0;i<8;i++) {
                result.Silver[i]=type==0?(i==0?1000000000L:i==1?500000000L:i==2?250000000L:10000000L):0;
                result.Cps[i]=type==1?(i==0?10000:i==1?7000:i==2?5000:3000):0;
                result.Souls[i]=checked((int)QuestSoulReward.Create().ID);
            }
            return result;
        }
        internal static void Finish(Guid run,Result result) {
            if(result==null)throw new ArgumentNullException("result");
            using(var c=Database.DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                using(var q=QuestInventory.Command(c,t,"SELECT state FROM revora_local_team_pk_runs WHERE run=@run FOR UPDATE","run",run))
                    if(q.ExecuteScalar()==null)throw new InvalidOperationException("Local Team PK run missing.");
                int changed=QuestInventory.Execute(c,t,@"UPDATE revora_local_team_pk_runs SET state='complete',ended_at=@ended,leaders=@leaders,silver=@silver,cps=@cps,souls=@souls
WHERE run=@run AND state='active'","run",run,"ended",result.Ended,"leaders",result.Leaders,"silver",result.Silver,"cps",result.Cps,"souls",result.Souls);
                using(var q=QuestInventory.Command(c,t,@"SELECT count(*) FROM revora_local_team_pk_runs WHERE run=@run AND state='complete'
AND ended_at=@ended AND leaders=@leaders AND silver=@silver AND cps=@cps AND souls=@souls",
                    "run",run,"ended",result.Ended,"leaders",result.Leaders,"silver",result.Silver,"cps",result.Cps,"souls",result.Souls))
                    if(Convert.ToInt64(q.ExecuteScalar())!=1)throw new InvalidOperationException("Local Team PK terminal snapshot changed.");
                if(changed==1)LocalTeamPkWardrobe.Grant(c,t,run,result);
                t.Commit();
            }}
        }
        internal const uint NpcId=417171;
        internal const uint SkillNpcId=801717;
        internal static void PlaceNpc() {
            PlaceClerk(NpcId,291,146,58,"TeamRegistrar","local-team-pk");
            PlaceClerk(SkillNpcId,300,146,62,"SkillRegistrar","skill-team-pk");
        }
        private static void PlaceClerk(uint npcId,ushort x,ushort y,ushort retiredX,string name,string source) {
            Game.Map town;
            if(!Kernel.Maps.TryGetValue(1002,out town) || town.BaseID!=1002)throw new InvalidOperationException("Local Team PK town unavailable.");
            Game.Map retiredMap=null;Revora.Interfaces.INpc retired=null;int count=0;
            foreach(var map in Kernel.Maps.Values) {
                Revora.Interfaces.INpc npc;if(!map.Npcs.TryGetValue(npcId,out npc))continue;
                if(++count>1)throw new InvalidOperationException("Local Team PK duplicate clerk.");
                // Database-loaded talkers omit Name; restore the label after checking identity and placement.
                if(map.ID==1002 && npc.X==x && npc.Y==y && npc.Mesh==50340 && npc.Type==Enums.NpcType.Talker){npc.Name=name;continue;}
                // Exact retired slot assigned by TwinCityPlaza.Apply for this identity.
                if(map.ID==6000 && npc.X==retiredX && npc.Y==74){retiredMap=map;retired=npc;continue;}
                throw new InvalidOperationException("Local Team PK clerk identity collision.");
            }
            if(town.Npcs.ContainsKey(npcId))return;
            if(!town.Floor[x,y,Game.MapObjectType.Player] || town.Npcs.Values.Any(n=>Kernel.GetDistance(n.X,n.Y,x,y)<2))throw new InvalidOperationException("Local Team PK clerk site occupied.");
            if(retired!=null) {
                Revora.Interfaces.INpc secondary;
                if(Game.Map.SecoundNpc.TryGetValue(npcId,out secondary) && !ReferenceEquals(secondary,retired))throw new InvalidOperationException("Local Team PK secondary clerk collision.");
                retiredMap.RemoveNpc(retired);Game.Map.SecoundNpc.Remove(npcId);
            }
            town.AddNpc(new Network.GamePackets.NpcSpawn{UID=npcId,MapID=1002,X=x,Y=y,Mesh=50340,Name=name,Type=Enums.NpcType.Talker});
            Console.WriteLine("["+source+"] clerk_ready map=1002 x="+x+" y="+y);
        }
        internal static bool HandleNpc(Revora.Client.GameState p,Network.GamePackets.NpcRequest request) {
            if(p==null || request==null || p.ActiveNpc!=NpcId)return false;
            if(!QuestInventory.Near(p,NpcId,1002))return true;
            var dialog=new Npcs(p);
            if(request.OptionID==3)dialog.Text(Claim(p,TeamElitePk.GamePackets.TeamElitePkBrackets));
            else if(request.OptionID==2) {
                if(!TeamElitePk.TeamTournament.Opened)dialog.Text("Registration is closed. Return on Saturday evening.");
                else if(p.Team==null)dialog.Text("Gather your team first.");
                else if(TeamElitePk.TeamTournament.Join(p,p.Team.GetEliteGroup(p.Entity.Level)))return true;
                else dialog.Text("Your team must share a level group. Each fighter must enter here before registration closes.");
            } else if(request.OptionID==255)return true;
            else {
                dialog.Text("Bring your team on Saturday evening. Top-eight leaders earn silver and a bound soul. Level 130+ fighters earn fourteen-day team titles through contested battles.");
                if(TeamElitePk.TeamTournament.Opened)dialog.Option("Enter with my team.",2);
                dialog.Option("Claim my purse.",3);
            }
            dialog.Option("Farewell.",255);dialog.Send();return true;
        }
        private static bool Near(Revora.Client.GameState p,int kind) {
            return QuestInventory.Near(p,kind==0?417171u:801717u,1002);
        }
        internal static string Claim(Revora.Client.GameState p,TeamElitePk.GamePackets series) {
            int kind=Kind(series);
            if(!Near(p,kind) || !SessionInstall.TryEnter(p.Entity.UID))return "Speak with the tournament clerk to claim your purse.";
            try {
                if(!System.Threading.Monitor.TryEnter(p.ItemSyncRoot))return "Please try again in a moment.";
                try {return ClaimOwned(p,kind);} finally {System.Threading.Monitor.Exit(p.ItemSyncRoot);}
            }catch(Exception error){QuestInventory.Fail(p,"local-team-pk-purse",error);return "Your purse is safe. Reconnect before claiming it.";}
            finally {SessionInstall.Exit(p.Entity.UID);}
        }
        private static string ClaimOwned(Revora.Client.GameState p,int kind) {
            if(!Near(p,kind))return "Speak with the tournament clerk to claim your purse.";
            Network.GamePackets.ConquerItem item;Guid run;int place,soul;long cp,gold,nextCp;decimal nextGold;
            using(var c=Database.DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                using(var q=QuestInventory.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                    if(q.ExecuteScalar()==null)throw new InvalidOperationException("Local Team PK owner missing.");
                using(var q=QuestInventory.Command(c,t,@"SELECT r.run,array_position(r.leaders,@uid),r.cps[array_position(r.leaders,@uid)],
r.silver[array_position(r.leaders,@uid)],r.souls[array_position(r.leaders,@uid)] FROM revora_local_team_pk_runs r
WHERE r.state='complete' AND r.kind=@kind AND @uid=ANY(r.leaders) AND r.ended_at<=@now
AND NOT EXISTS(SELECT 1 FROM revora_local_team_pk_claims p WHERE p.run=r.run AND p.uid=@uid)
ORDER BY r.ended_at,r.run LIMIT 1 FOR UPDATE OF r","uid",(long)p.Entity.UID,"kind",kind,"now",DateTime.UtcNow))
                using(var r=q.ExecuteReader()) {
                    if(!r.Read())return "No unclaimed tournament purse awaits you.";
                    run=r.GetGuid(0);place=r.GetInt32(1);cp=r.GetInt32(2);gold=r.GetInt64(3);soul=r.GetInt32(4);
                }
                Database.ConquerItemBaseInformation info;
                if(place<1 || place>8 || cp<0 || gold<0 || soul<800000 || soul>=900000
                    || !Database.ConquerItemInformation.BaseInformations.TryGetValue((uint)soul,out info) || info.PurificationLevel!=7)
                    throw new InvalidOperationException("Local Team PK saved prize invalid.");
                if(p.Inventory.Count>=40)return "Make a place in your bag. Your purse will wait.";
                nextCp=checked(p.Entity.ConquerPoints+cp);nextGold=p.Entity.Money+(decimal)gold;
                if(nextCp>uint.MaxValue || nextGold>Game.Entity.MaximumSilver)return "Make room in your purse before claiming.";
                SilverBalance.AssertSaved(c,t,p);
                using(var q=QuestInventory.Command(c,t,"SELECT count(*) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0","uid",(long)p.Entity.UID))
                    if(Convert.ToInt64(q.ExecuteScalar())!=p.Inventory.Count)throw new InvalidOperationException("Local Team PK inventory changed.");
                if(QuestInventory.Execute(c,t,"UPDATE entities SET conquerpoints=@cp,money=@gold WHERE uid=@uid AND conquerpoints=@before",
                    "cp",nextCp,"gold",nextGold,"uid",(long)p.Entity.UID,"before",(long)p.Entity.ConquerPoints)!=1)
                    throw new InvalidOperationException("Local Team PK currency changed.");
                item=QuestInventory.New((uint)soul,true);QuestInventory.Insert(c,t,p.Entity.UID,item);
                QuestInventory.Execute(c,t,@"INSERT INTO revora_local_team_pk_claims(run,uid,place,soul_uid,claimed_at)
VALUES(@run,@uid,@place,@soul,@now)","run",run,"uid",(long)p.Entity.UID,"place",place,"soul",(long)item.UID,"now",DateTime.UtcNow);
                if(!Near(p,kind))throw new InvalidOperationException("Local Team PK claiming session changed.");
                t.Commit();p.Entity.ApplyCommittedConquerPoints((uint)nextCp);SilverBalance.ApplyCommitted(p,(ulong)nextGold);
            }}
            QuestInventory.Install(p,new uint[0],new[]{item});return "Your tournament purse and bound soul are yours.";
        }
        internal static void Cancel(Guid run) {
            using(var c=Database.DataHolder.MySqlConnection){c.Open();QuestInventory.Execute(c,null,
                "UPDATE revora_local_team_pk_runs SET state='cancelled',ended_at=GREATEST(started_at,now()) WHERE run=@run AND state='active'","run",run);}
        }
    }
}

namespace Revora.Game.Features.Tournaments
{
    internal static class LocalTeamPkAdmissions
    {
        private static readonly object returnGate=new object();
        private static DateTime nextReturn;
        private static long cursor;
        internal static bool Reserve(Guid run,Client.GameState p) {
            if(run==Guid.Empty || !QuestInventory.Player(p) || !SessionInstall.TryEnter(p.Entity.UID))return false;
            try {
                if(!QuestInventory.Player(p))return false;
                using(var c=Database.DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    using(var q=QuestInventory.Command(c,t,"SELECT run FROM revora_local_team_pk_runs WHERE run=@run AND state='active' FOR UPDATE","run",run))
                        if(q.ExecuteScalar()==null)return false;
                    using(var q=QuestInventory.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                        if(q.ExecuteScalar()==null)return false;
                    using(var q=QuestInventory.Command(c,t,"SELECT count(*) FROM revora_local_team_pk_admissions WHERE uid=@uid AND (NOT returned OR run=@run)","uid",(long)p.Entity.UID,"run",run))
                        if(Convert.ToInt64(q.ExecuteScalar())!=0)return false;
                    QuestInventory.Execute(c,t,"INSERT INTO revora_local_team_pk_admissions(run,uid,pkmode) VALUES(@run,@uid,@mode)","run",run,"uid",(long)p.Entity.UID,"mode",(int)p.Entity.PKMode);
                    t.Commit();return true;
                }}
            } finally {SessionInstall.Exit(p.Entity.UID);}
        }
        internal static void RecordParticipation(Guid run,long[] owners) {
            using(var c=Database.DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                using(var q=QuestInventory.Command(c,t,"SELECT run FROM revora_local_team_pk_runs WHERE run=@run AND state='active' FOR UPDATE","run",run))
                    if(q.ExecuteScalar()==null)throw new InvalidOperationException("Team participation run is not active.");
                QuestInventory.Execute(c,t,@"UPDATE revora_local_team_pk_admissions SET participated_at=now()
WHERE run=@run AND uid=ANY(@owners) AND NOT returned AND participated_at IS NULL","run",run,"owners",owners);
                using(var q=QuestInventory.Command(c,t,"SELECT count(*) FROM revora_local_team_pk_admissions WHERE run=@run AND uid=ANY(@owners) AND NOT returned AND participated_at IS NOT NULL","run",run,"owners",owners))
                    if(Convert.ToInt64(q.ExecuteScalar())!=owners.Length)throw new InvalidOperationException("Team participation roster changed.");
                t.Commit();
            }}
        }
        internal static void EarnEntry(Guid run,long[] owners) {
            using(var c=Database.DataHolder.MySqlConnection){c.Open();
                QuestInventory.Execute(c,null,@"UPDATE revora_local_team_pk_admissions a SET entry_earned=true
FROM revora_local_team_pk_runs r WHERE a.run=@run AND r.run=a.run AND r.kind=0 AND r.state='active'
AND a.uid=ANY(@owners) AND NOT a.returned AND NOT a.entry_earned","run",run,"owners",owners);
            }
        }
        internal static bool DeliverEntry(Client.GameState p) {
            if(!QuestInventory.Player(p,false) || !SessionInstall.TryEnter(p.Entity.UID))return false;
            bool owned=false;
            try {
                owned=System.Threading.Monitor.TryEnter(p.ItemSyncRoot);if(!owned || !QuestInventory.Player(p,false))return false;
                Network.GamePackets.ConquerItem item;
                using(var c=Database.DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    using(var q=QuestInventory.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                        if(q.ExecuteScalar()==null)throw new InvalidOperationException("Team PK entry reward owner missing.");
                    Guid run;
                    using(var q=QuestInventory.Command(c,t,@"SELECT a.run FROM revora_local_team_pk_admissions a
JOIN revora_local_team_pk_runs r ON r.run=a.run WHERE a.uid=@uid AND a.entry_earned AND a.entry_item IS NULL AND r.kind=0
ORDER BY r.started_at,a.run LIMIT 1 FOR UPDATE OF a","uid",(long)p.Entity.UID)) {
                        var value=q.ExecuteScalar();if(value==null)return true;run=(Guid)value;
                    }
                    if(p.Inventory.Count>=40)return false;
                    using(var q=QuestInventory.Command(c,t,"SELECT count(*) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0","uid",(long)p.Entity.UID))
                        if(Convert.ToInt64(q.ExecuteScalar())!=p.Inventory.Count)throw new InvalidOperationException("Team PK entry inventory changed.");
                    item=QuestInventory.New(720793,false);QuestInventory.Insert(c,t,p.Entity.UID,item);
                    if(QuestInventory.Execute(c,t,"UPDATE revora_local_team_pk_admissions SET entry_item=@item WHERE run=@run AND uid=@uid AND entry_earned AND entry_item IS NULL","item",(long)item.UID,"run",run,"uid",(long)p.Entity.UID)!=1)
                        throw new InvalidOperationException("Team PK entry receipt changed.");
                    if(!QuestInventory.Player(p,false))throw new InvalidOperationException("Team PK entry session changed.");
                    t.Commit();
                }}
                QuestInventory.Install(p,new uint[0],new[]{item});return true;
            }catch(Exception error){QuestInventory.Fail(p,"local-team-pk-entry",error);return false;}
            finally {if(owned)System.Threading.Monitor.Exit(p.ItemSyncRoot);SessionInstall.Exit(p.Entity.UID);}
        }
        // Projection only: retain the pending receipt until the installed session is returned.
        internal static void Loaded(Client.GameState p) {
            using(var c=Database.DataHolder.MySqlConnection){c.Open();using(var q=QuestInventory.Command(c,null,"SELECT pkmode FROM revora_local_team_pk_admissions WHERE uid=@uid AND NOT returned","uid",(long)p.Entity.UID)) {
                var value=q.ExecuteScalar();if(value==null)return;
                p.Entity.MapID=1002;p.Entity.X=327;p.Entity.Y=303;p.Entity.PKMode=Mode(Convert.ToInt32(value));
            }}
        }
        private static Enums.PKMode Mode(int value) {
            if(value<0 || value>255 || !Enum.IsDefined(typeof(Enums.PKMode),(byte)value))throw new InvalidOperationException("Invalid Team PK return mode.");
            return (Enums.PKMode)value;
        }
        private static bool Current(Client.GameState p) {
            return new[]{TeamElitePk.TeamTournament.Tournaments,TeamElitePk.SkillTeamTournament.Tournaments}
                .Where(a=>a!=null).SelectMany(a=>a).Any(t=>t!=null && t.CurrentParticipant(p));
        }
        internal static bool Return(Client.GameState p,bool force) {
            if(!QuestInventory.Player(p,false) || !SessionInstall.TryEnter(p.Entity.UID))return false;
            bool item=false,pk=false;
            try {
                if(!QuestInventory.Player(p,false))return false;
                item=System.Threading.Monitor.TryEnter(p.ItemSyncRoot);if(!item)return false;
                pk=System.Threading.Monitor.TryEnter(p.PkSyncRoot);if(!pk)return false;
                if(!force && Current(p))return true;
                Guid run;Enums.PKMode mode;
                using(var c=Database.DataHolder.MySqlConnection){c.Open();using(var q=QuestInventory.Command(c,null,"SELECT run,pkmode FROM revora_local_team_pk_admissions WHERE uid=@uid AND NOT returned","uid",(long)p.Entity.UID))
                using(var r=q.ExecuteReader()){if(!r.Read())return true;run=r.GetGuid(0);mode=Mode(r.GetInt32(1));}}
                if(p.Entity.MapID!=1002 || p.Entity.X!=327 || p.Entity.Y!=303)p.Entity.Teleport(1002,327,303);
                p.RestoreSkillStats();
                if(p.Entity.MapID!=1002 || p.Entity.X!=327 || p.Entity.Y!=303)throw new InvalidOperationException("Team PK return did not reach town.");
                p.Entity.PKMode=mode;p.Send(new Network.GamePackets.Data(true){UID=p.Entity.UID,ID=Network.GamePackets.Data.ChangePKMode,dwParam=(uint)mode});
                using(var c=Database.DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    using(var q=QuestInventory.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                        if(q.ExecuteScalar()==null)throw new InvalidOperationException("Team PK returning owner missing.");
                    if(!QuestInventory.Player(p,false))throw new InvalidOperationException("Team PK returning session changed.");
                    if(QuestInventory.Execute(c,t,"UPDATE entities SET mapid=1002,x=327,y=303 WHERE uid=@uid","uid",(long)p.Entity.UID)!=1
                        || QuestInventory.Execute(c,t,"UPDATE revora_local_team_pk_admissions SET returned=true WHERE run=@run AND uid=@uid AND NOT returned","run",run,"uid",(long)p.Entity.UID)!=1)
                        throw new InvalidOperationException("Team PK return receipt changed.");
                    t.Commit();
                }}
                return true;
            }catch(Exception error){QuestInventory.Fail(p,"local-team-pk-return",error);return false;}
            finally {if(pk)System.Threading.Monitor.Exit(p.PkSyncRoot);if(item)System.Threading.Monitor.Exit(p.ItemSyncRoot);SessionInstall.Exit(p.Entity.UID);}
        }
        internal static void Tick() {
            if(!System.Threading.Monitor.TryEnter(returnGate))return;
            try {
                var now=DateTime.UtcNow;if(now<nextReturn)return;nextReturn=now.AddSeconds(10);
                var ids=Kernel.GamePool.Values.Where(p=>QuestInventory.Player(p,false)).Select(p=>(long)p.Entity.UID).ToArray();if(ids.Length==0)return;
                var due=new List<uint>();using(var c=Database.DataHolder.MySqlConnection){c.Open();using(var q=QuestInventory.Command(c,null,"SELECT uid FROM (SELECT uid FROM revora_local_team_pk_admissions WHERE NOT returned OR (entry_earned AND entry_item IS NULL) UNION SELECT uid FROM revora_skill_pk_round_rewards WHERE item_uid IS NULL) due WHERE uid=ANY(@ids) AND uid>@cursor ORDER BY uid LIMIT 32","ids",ids,"cursor",cursor))
                using(var r=q.ExecuteReader())while(r.Read())due.Add(Convert.ToUInt32(r[0]));}
                if(due.Count==0){cursor=0;return;}cursor=due.Last();
                foreach(uint uid in due){Client.GameState p;if(Kernel.GamePool.TryGetValue(uid,out p)){Return(p,false);DeliverEntry(p);LocalSkillPkRounds.Deliver(p);}}
            }catch(Exception error){Console.WriteLine("[local-team-pk-return] poll_failure="+error.GetType().Name);}
            finally {System.Threading.Monitor.Exit(returnGate);}
        }
    }
}

namespace Revora.Game.Features.Tournaments
{
    internal static class LocalTeamPkCombat
    {
        internal static void ImportSkill(TeamElitePk.Match match,Action import) {
            if(match.Inside)return;
            var brackets=TeamElitePk.SkillTeamTournament.Tournaments;
            if(brackets==null || match.GroupID<0 || match.GroupID>=brackets.Length)return;
            var owner=brackets[match.GroupID];TeamElitePk.Match registered;
            if(owner==null || !owner.Alive || owner.WaitingArea==null || owner.Matches==null
                || !owner.Matches.TryGetValue(match.ID,out registered) || !ReferenceEquals(registered,match))return;
            var sessions=new List<uint>();var locks=new List<object>();
            try {
                var players=match.MatchStats.Where(f=>f!=null && !f.NoFlag && !f.Lost).SelectMany(f=>f.Team.Players)
                    .Where(p=>QuestInventory.Player(p) && p.Entity.MapID==owner.WaitingArea.ID).Distinct().OrderBy(p=>p.Entity.UID).ToArray();
                foreach(var p in players) {
                    if(!SessionInstall.TryEnter(p.Entity.UID))return;sessions.Add(p.Entity.UID);
                    if(!System.Threading.Monitor.TryEnter(p.ItemSyncRoot))return;locks.Add(p.ItemSyncRoot);
                    if(!QuestInventory.Player(p) || p.Entity.MapID!=owner.WaitingArea.ID)return;
                    if(!p.ValidateSkillEquipment())throw new InvalidOperationException("Skill equipment import preflight failed.");
                }
                import();
                if(match.MatchStats.Length>1 && players.Any(p=>!ReferenceEquals(p.Map,match.Map) || !SkillStats(p)))
                    throw new InvalidOperationException("Skill participant import did not complete.");
            }catch(Exception error){owner.FailedSkillImport(error);}
            finally {
                for(int i=locks.Count-1;i>=0;i--)System.Threading.Monitor.Exit(locks[i]);
                for(int i=sessions.Count-1;i>=0;i--)SessionInstall.Exit(sessions[i]);
            }
        }
        // Same numeric family and quality as the existing item-upgrade catalog path.
        // At most 100 catalog lookups; never scan the whole catalog during movement/combat.
        internal static uint SkillItemType(ConquerItem item) {
            int position=item.Position;if(position>20)position-=20;
            uint family=item.ID/1000;int limit;
            switch(position) {
                case ConquerItem.Head:limit=family==117?112:120;break;
                case ConquerItem.Necklace:limit=family==121?116:120;break;
                case ConquerItem.Ring:limit=family==151 || family==152?117:116;break;
                case ConquerItem.Armor:case ConquerItem.Boots:
                case ConquerItem.RightWeapon:case ConquerItem.LeftWeapon:limit=120;break;
                default:return item.ID;
            }
            var catalog=Database.ConquerItemInformation.BaseInformations;
            Database.ConquerItemBaseInformation original;
            if(catalog==null || !catalog.TryGetValue(item.ID,out original) || original==null)
                throw new InvalidOperationException("Skill PK equipment catalog entry is missing.");
            if(original.Level<=limit)return item.ID;
            Database.ConquerItemBaseInformation best=null;
            uint first=family*1000+item.ID%10;
            for(uint index=0;index<100;index++) {
                Database.ConquerItemBaseInformation candidate;
                if(!catalog.TryGetValue(first+index*10,out candidate) || candidate==null
                    || candidate.ID!=first+index*10 || candidate.Level>limit
                    || candidate.Class!=original.Class || candidate.Gender!=original.Gender)continue;
                if(best==null || candidate.Level>best.Level)best=candidate;
            }
            if(best==null)throw new InvalidOperationException("Skill PK equipment has no compatible capped catalog entry.");
            return best.ID;
        }
        // A flag or a shared arena map alone is not authority to alter combat stats.
        internal static bool SkillStats(Client.GameState p) {
            if(!QuestInventory.Player(p,false) || !p.Entity.InSkillPk || p.Team==null)return false;
            var team=p.Team;var match=team.EliteMatch;var tournaments=TeamElitePk.SkillTeamTournament.Tournaments;
            if(match==null || match.T_ID!=TeamElitePk.GamePackets.SkillElitePkBrackets || !match.Inside || match.Done
                || match.Map==null || !ReferenceEquals(p.Map,match.Map) || p.Entity.MapID!=match.Map.ID
                || team.EliteFighterStats==null || !team.EliteFighterStats.Fighting || match.MatchStats==null
                || !match.MatchStats.Contains(team.EliteFighterStats) || team.Members==null
                || !team.Players.Any(member=>ReferenceEquals(member,p)) || tournaments==null
                || match.GroupID<0 || match.GroupID>=tournaments.Length)return false;
            var tournament=tournaments[match.GroupID];Game.ConquerStructures.Team registered;TeamElitePk.Match owned;
            return tournament!=null && tournament.Alive && tournament.Teams!=null && tournament.Matches!=null
                && tournament.Teams.TryGetValue(team.UID,out registered) && ReferenceEquals(registered,team)
                && tournament.Matches.TryGetValue(match.ID,out owned) && ReferenceEquals(owned,match);
        }
        internal static TeamElitePk.Match MatchFor(Entity attacker,Entity target) {
            if(attacker==null || target==null || ReferenceEquals(attacker,target) || attacker.Owner==null || target.Owner==null
                || !ReferenceEquals(attacker.Owner.Entity,attacker) || !ReferenceEquals(target.Owner.Entity,target)
                || !QuestInventory.Player(attacker.Owner,false) || !QuestInventory.Player(target.Owner,false))return null;
            var a=attacker.Owner.Team;var b=target.Owner.Team;
            if(a==null || b==null || ReferenceEquals(a,b) || a.EliteMatch==null || !ReferenceEquals(a.EliteMatch,b.EliteMatch)
                || a.EliteFighterStats==null || b.EliteFighterStats==null)return null;
            var match=a.EliteMatch;
            if(!match.Inside || match.Done || match.Map==null || !ReferenceEquals(attacker.Owner.Map,match.Map) || !ReferenceEquals(target.Owner.Map,match.Map)
                || match.MatchStats==null || !match.MatchStats.Contains(a.EliteFighterStats) || !match.MatchStats.Contains(b.EliteFighterStats)
                || !a.EliteFighterStats.Fighting || !b.EliteFighterStats.Fighting
                || !a.Players.Any(p=>ReferenceEquals(p,attacker.Owner)) || !b.Players.Any(p=>ReferenceEquals(p,target.Owner)))return null;
            bool registered=new[]{TeamElitePk.TeamTournament.Tournaments,TeamElitePk.SkillTeamTournament.Tournaments}
                .Where(x=>x!=null).SelectMany(x=>x).Any(t=>t!=null && t.Alive && t.Teams.Values.Any(team=>ReferenceEquals(team,a))
                    && t.Teams.Values.Any(team=>ReferenceEquals(team,b)) && t.Matches.Values.Any(m=>ReferenceEquals(m,match)));
            return registered?match:null;
        }
        internal static void Damage(Entity attacker,Entity target,uint requested) {
            var match=MatchFor(attacker,target);if(match==null || attacker.Dead || target.Dead)return;
            lock(match) {
                if(!ReferenceEquals(MatchFor(attacker,target),match))return;
                uint actual=Math.Min(requested,target.Hitpoints);if(actual==0)return;
                var fighter=attacker.Owner.Team.EliteFighterStats;
                fighter.Points=(uint)Math.Min(uint.MaxValue,(ulong)fighter.Points+actual);
                var update=match.CreateUpdate().ToArray();attacker.Owner.Team.SendMesageTeam(update,0);target.Owner.Team.SendMesageTeam(update,0);
            }
        }
        internal static void Death(Entity attacker,Entity target) {
            var match=MatchFor(attacker,target);if(match==null || !target.Dead)return;
            lock(match) {
                if(ReferenceEquals(MatchFor(attacker,target),match) && !target.Owner.Team.Alive)match.End(target.Owner.Team);
            }
        }
    }
}

namespace Revora.Game.Features.Tournaments
{
    // Keep a hit's HP mutation, score and defeat in one match lifecycle boundary.
    internal sealed class LocalTeamPkCombatLease : IDisposable
    {
        private readonly List<object> gates=new List<object>();
        private readonly List<uint> sessions=new List<uint>();
        internal bool Allowed {get;private set;}
        private bool Own(object gate) {
            if(gate==null || !System.Threading.Monitor.TryEnter(gate))return false;
            gates.Add(gate);return true;
        }
        internal static LocalTeamPkCombatLease Acquire(Entity attacker,Entity target) {
            var a=attacker==null?null:attacker.Owner;var b=target==null?null:target.Owner;
            var first=a==null || a.Team==null?null:a.Team.EliteMatch;
            var second=b==null || b.Team==null?null:b.Team.EliteMatch;
            bool relevant=first!=null && first.Map!=null && ReferenceEquals(a.Map,first.Map)
                || second!=null && second.Map!=null && ReferenceEquals(b.Map,second.Map);
            if(!relevant)return null;
            var lease=new LocalTeamPkCombatLease();
            try {
                if(first==null || !ReferenceEquals(first,second) || a.Entity==null || b.Entity==null || !lease.Own(first))return lease;
                foreach(var p in new[]{a,b}.Distinct().OrderBy(p=>p.Entity.UID)) {
                    if(!SessionInstall.TryEnter(p.Entity.UID))return lease;lease.sessions.Add(p.Entity.UID);
                    if(!lease.Own(p.ItemSyncRoot) || !lease.Own(p.Entity.HealthMutationSync))return lease;
                }
                lease.Allowed=ReferenceEquals(LocalTeamPkCombat.MatchFor(attacker,target),first) && !attacker.Dead && !target.Dead;
                return lease;
            }catch{lease.Dispose();throw;}
        }
        public void Dispose() {
            for(int i=gates.Count-1;i>=0;i--)System.Threading.Monitor.Exit(gates[i]);gates.Clear();
            for(int i=sessions.Count-1;i>=0;i--)SessionInstall.Exit(sessions[i]);sessions.Clear();
        }
    }
}

namespace Revora.Game.Features.Tournaments
{
    internal static class LocalSkillPkRounds
    {
        internal sealed class Result {
            internal Guid Run;internal long Match,Winner,Loser;internal int Round;internal long[] Owners;
        }
        internal static Result Freeze(TeamElitePk.Match match,int round,TeamElitePk.FighterStats winner,Game.ConquerStructures.Team loser) {
            var brackets=TeamElitePk.SkillTeamTournament.Tournaments;
            if(!match.Inside || match.Done || round<1 || winner==null || loser==null || brackets==null || match.GroupID<0 || match.GroupID>=brackets.Length)
                throw new InvalidOperationException("Skill round is not active.");
            var owner=brackets[match.GroupID];TeamElitePk.Match owned;
            if(owner==null || !owner.Alive || owner.LedgerRun==Guid.Empty || !owner.Matches.TryGetValue(match.ID,out owned) || !ReferenceEquals(owned,match)
                || !match.MatchStats.Any(f=>f!=null && ReferenceEquals(f.Team,loser) && f.Fighting) || !match.MatchStats.Contains(winner) || !winner.Fighting)
                throw new InvalidOperationException("Skill round ownership changed.");
            var players=winner.Team.Players.Where(p=>LocalTeamPkCombat.SkillStats(p)).Select(p=>(long)p.Entity.UID).Distinct().OrderBy(uid=>uid).ToArray();
            if(players.Length>5 || winner.Team.UID==loser.UID)throw new InvalidOperationException("Invalid Skill round participants.");
            return new Result{Run=owner.LedgerRun,Match=match.ID,Round=round,Winner=winner.Team.UID,Loser=loser.UID,Owners=players};
        }
        internal static void Record(Result result) {
            using(var c=Database.DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                using(var q=QuestInventory.Command(c,t,"SELECT state FROM revora_local_team_pk_runs WHERE run=@run AND kind=1 FOR UPDATE","run",result.Run)) {
                    var state=q.ExecuteScalar();if(state==null)throw new InvalidOperationException("Skill run missing.");
                    if((string)state=="active")QuestInventory.Execute(c,t,@"INSERT INTO revora_skill_pk_rounds(run,match,round,winner,loser,owners)
VALUES(@run,@match,@round,@winner,@loser,@owners) ON CONFLICT(run,match,round) DO NOTHING","run",result.Run,"match",result.Match,"round",result.Round,"winner",result.Winner,"loser",result.Loser,"owners",result.Owners);
                }
                using(var q=QuestInventory.Command(c,t,"SELECT winner,loser,owners FROM revora_skill_pk_rounds WHERE run=@run AND match=@match AND round=@round","run",result.Run,"match",result.Match,"round",result.Round))
                using(var r=q.ExecuteReader())if(!r.Read() || r.GetInt64(0)!=result.Winner || r.GetInt64(1)!=result.Loser || !((long[])r[2]).SequenceEqual(result.Owners))throw new InvalidOperationException("Skill round result changed.");
                foreach(long uid in result.Owners) {
                    using(var q=QuestInventory.Command(c,t,"SELECT uid FROM revora_local_team_pk_admissions WHERE run=@run AND uid=@uid","run",result.Run,"uid",uid))
                        if(q.ExecuteScalar()==null)throw new InvalidOperationException("Skill reward owner was not admitted.");
                    QuestInventory.Execute(c,t,"INSERT INTO revora_skill_pk_round_rewards(run,match,round,uid) VALUES(@run,@match,@round,@uid) ON CONFLICT DO NOTHING","run",result.Run,"match",result.Match,"round",result.Round,"uid",uid);
                }
                t.Commit();
            }}
        }
        internal static bool Deliver(Client.GameState p) {
            if(!QuestInventory.Player(p,false) || !SessionInstall.TryEnter(p.Entity.UID))return false;
            bool owned=false;
            try {
                owned=System.Threading.Monitor.TryEnter(p.ItemSyncRoot);if(!owned || !QuestInventory.Player(p,false))return false;
                ConquerItem item;
                using(var c=Database.DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    using(var q=QuestInventory.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))if(q.ExecuteScalar()==null)throw new InvalidOperationException("Skill reward character missing.");
                    Guid run;long match;int round;
                    using(var q=QuestInventory.Command(c,t,"SELECT run,match,round FROM revora_skill_pk_round_rewards WHERE uid=@uid AND item_uid IS NULL ORDER BY run,match,round LIMIT 1 FOR UPDATE","uid",(long)p.Entity.UID))
                    using(var r=q.ExecuteReader()){if(!r.Read())return true;run=r.GetGuid(0);match=r.GetInt64(1);round=r.GetInt32(2);}
                    if(p.Inventory.Count>=40)return false;
                    using(var q=QuestInventory.Command(c,t,"SELECT count(*) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0","uid",(long)p.Entity.UID))
                        if(Convert.ToInt64(q.ExecuteScalar())!=p.Inventory.Count)throw new InvalidOperationException("Skill reward inventory changed.");
                    item=QuestInventory.New(720981,false);QuestInventory.Insert(c,t,p.Entity.UID,item);
                    if(QuestInventory.Execute(c,t,"UPDATE revora_skill_pk_round_rewards SET item_uid=@item WHERE run=@run AND match=@match AND round=@round AND uid=@uid AND item_uid IS NULL","item",(long)item.UID,"run",run,"match",match,"round",round,"uid",(long)p.Entity.UID)!=1)throw new InvalidOperationException("Skill reward receipt changed.");
                    if(!QuestInventory.Player(p,false))throw new InvalidOperationException("Skill reward session changed.");
                    t.Commit();
                }}
                QuestInventory.Install(p,new uint[0],new[]{item});return true;
            }catch(Exception error){QuestInventory.Fail(p,"skill-pk-round",error);return false;}
            finally {if(owned)System.Threading.Monitor.Exit(p.ItemSyncRoot);SessionInstall.Exit(p.Entity.UID);}
        }
    }
}

namespace Revora.Game.Features.Tournaments {
    // Owner-approved local edition: frozen level-130+ regular Team PK rosters,
    // personally present in a contested match, with saved top-eight placement.
    internal static class LocalTeamPkWardrobe {
        private static bool Enabled(Npgsql.NpgsqlConnection c,Npgsql.NpgsqlTransaction t,Guid run,bool active) {
            using(var q=QuestInventory.Command(c,t,"SELECT local_wardrobe AND kind=0 AND bracket=3 AND (NOT @active OR state='active') FROM revora_local_team_pk_runs WHERE run=@run FOR UPDATE","run",run,"active",active))
                return Equals(q.ExecuteScalar(),true);
        }
        internal static void FreezeRoster(Guid run,Game.ConquerStructures.Team[] teams) {
            if(teams==null || teams.Length<8)throw new InvalidOperationException("Team wardrobe roster is incomplete.");
            var rows=new Dictionary<long,long>();
            foreach(var team in teams) {
                if(team==null || team.Lider==null || team.Members.Count<1 || team.Members.Count>5)throw new InvalidOperationException("Team wardrobe roster changed.");
                long leader=team.Lider.Entity.UID;bool hasLeader=false;
                foreach(var p in team.Players) {
                    if(!QuestInventory.Player(p) || p.Entity.Level<130 || !ReferenceEquals(p.Team,team))throw new InvalidOperationException("Team wardrobe member changed.");
                    rows.Add(p.Entity.UID,leader);hasLeader|=p.Entity.UID==leader;
                }
                if(!hasLeader)throw new InvalidOperationException("Team wardrobe captain is absent.");
            }
            using(var c=Database.DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                if(!Enabled(c,t,run,true))return;
                bool frozen;
                using(var q=QuestInventory.Command(c,t,"SELECT roster_frozen FROM revora_local_team_pk_runs WHERE run=@run","run",run))frozen=(bool)q.ExecuteScalar();
                foreach(var row in rows.OrderBy(x=>x.Key)) {
                    if(!frozen)QuestInventory.Execute(c,t,@"INSERT INTO revora_local_team_pk_roster(run,uid,leader)
SELECT @run,@uid,@leader FROM revora_local_team_pk_admissions WHERE run=@run AND uid=@uid AND NOT returned
ON CONFLICT(run,uid) DO NOTHING","run",run,"uid",row.Key,"leader",row.Value);
                    using(var q=QuestInventory.Command(c,t,"SELECT leader=@leader FROM revora_local_team_pk_roster WHERE run=@run AND uid=@uid","run",run,"uid",row.Key,"leader",row.Value))
                        if(!Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("Team wardrobe admission or captain changed.");
                }
                using(var q=QuestInventory.Command(c,t,"SELECT count(*) FROM revora_local_team_pk_roster WHERE run=@run","run",run))
                    if(Convert.ToInt64(q.ExecuteScalar())!=rows.Count)throw new InvalidOperationException("Team wardrobe roster replay changed.");
                QuestInventory.Execute(c,t,"UPDATE revora_local_team_pk_runs SET roster_frozen=true WHERE run=@run","run",run);t.Commit();
            }}
        }
        internal static void RecordCombat(TeamElitePk.Match match,int round,TeamElitePk.FighterStats winner,Game.ConquerStructures.Team loser) {
            if(!match.Inside || winner==null || loser==null || winner.Team==null || winner.Team==loser)return;
            var defeated=loser.EliteFighterStats;
            if(defeated==null || (ulong)winner.Points+defeated.Points==0)return;
            var tournament=TeamElitePk.TeamTournament.Tournaments[match.GroupID];TeamElitePk.Match owned;
            if(tournament==null || !tournament.Alive || !tournament.Matches.TryGetValue(match.ID,out owned) || !ReferenceEquals(owned,match)
                || !match.MatchStats.Contains(winner) || !match.MatchStats.Contains(defeated) || !winner.Fighting || !defeated.Fighting)
                throw new InvalidOperationException("Team wardrobe combat ownership changed.");
            var present=new Dictionary<long,long>();
            foreach(var fighter in new[]{winner,defeated})foreach(var p in fighter.Team.Players)
                if(QuestInventory.Player(p,false) && ReferenceEquals(p.Map,match.Map) && ReferenceEquals(p.Team,fighter.Team))present.Add(p.Entity.UID,fighter.LeaderUID);
            if(present.Count==0)return;
            var ids=present.Keys.OrderBy(x=>x).ToArray();Guid run=tournament.LedgerRun;
            using(var c=Database.DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                if(!Enabled(c,t,run,true))return;
                using(var q=QuestInventory.Command(c,t,"SELECT roster_frozen FROM revora_local_team_pk_runs WHERE run=@run","run",run))
                    if(!Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("Team wardrobe roster was not frozen.");
                // A lost commit acknowledgement may be retried after a disconnect.
                // The atomic saved receipt owns its original participants; never
                // expand or shrink that receipt from the current live roster.
                using(var q=QuestInventory.Command(c,t,"SELECT winner=@winner AND loser=@loser FROM revora_local_team_pk_battles WHERE run=@run AND match=@match AND round=@round",
                    "run",run,"match",(long)match.ID,"round",round,"winner",(long)winner.LeaderUID,"loser",(long)defeated.LeaderUID)) {
                    object prior=q.ExecuteScalar();
                    if(prior!=null) {
                        if(!Equals(prior,true))throw new InvalidOperationException("Team wardrobe combat result changed.");
                        t.Commit();return;
                    }
                }
                foreach(var p in present)
                    if(QuestInventory.Execute(c,t,"UPDATE revora_local_team_pk_roster SET contested=true WHERE run=@run AND uid=@uid AND leader=@leader","run",run,"uid",p.Key,"leader",p.Value)!=1)
                        throw new InvalidOperationException("Team wardrobe combatant was not registered.");
                QuestInventory.Execute(c,t,@"INSERT INTO revora_local_team_pk_battles(run,match,round,winner,loser,members)
VALUES(@run,@match,@round,@winner,@loser,@members) ON CONFLICT DO NOTHING","run",run,"match",(long)match.ID,"round",round,"winner",(long)winner.LeaderUID,"loser",(long)defeated.LeaderUID,"members",ids);
                using(var q=QuestInventory.Command(c,t,"SELECT winner=@winner AND loser=@loser AND members=@members FROM revora_local_team_pk_battles WHERE run=@run AND match=@match AND round=@round",
                    "run",run,"match",(long)match.ID,"round",round,"winner",(long)winner.LeaderUID,"loser",(long)defeated.LeaderUID,"members",ids))
                    if(!Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("Team wardrobe combat replay changed.");
                t.Commit();
            }}
        }
        internal static void Grant(Npgsql.NpgsqlConnection c,Npgsql.NpgsqlTransaction t,Guid run,LocalTeamPkLedger.Result result) {
            if(!Enabled(c,t,run,false))return;
            using(var q=QuestInventory.Command(c,t,"SELECT roster_frozen FROM revora_local_team_pk_runs WHERE run=@run","run",run))
                if(!Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("Team wardrobe final roster missing.");
            for(int place=0;place<result.Leaders.Length;place++) {
                long leader=result.Leaders[place];var members=new List<uint>();
                using(var q=QuestInventory.Command(c,t,"SELECT uid FROM revora_local_team_pk_roster WHERE run=@run AND leader=@leader AND contested ORDER BY uid","run",run,"leader",leader))
                using(var r=q.ExecuteReader())while(r.Read())members.Add(Convert.ToUInt32(r[0]));
                // No-show or bye-only teams cannot gain these titles.
                if(members.Count==0)continue;
                using(var q=QuestInventory.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_local_team_pk_battles WHERE run=@run AND (winner=@leader OR loser=@leader))","run",run,"leader",leader))
                    if(!Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("Team wardrobe combat receipt missing.");
                int key=place==0?20592059:place==1?20602060:place==2?20612061:20622062;
                foreach(uint uid in members)
                    if(!ProgressionEvidence.Grant(c,t,uid,"team-pk-local-wardrobe-v1",run.ToString("N")+":"+key,key,result.Ended))
                        throw new InvalidOperationException("Team wardrobe reward was not saved.");
            }
        }
    }
}
