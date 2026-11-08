using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Revora.Network.GamePackets;
using Revora.Game.ConquerStructures.Society;

namespace Revora.Game
{
    public unsafe class GuildWar
    {
        public static SobNpcSpawn Pole, RightGate, LeftGate;
        public static SafeDictionary<uint, Guild> Scores = new SafeDictionary<uint, Guild>(100);
        public static bool IsWar = false, Flame10th = false, FirstRound = false;
        public static Time32 ScoreSendStamp, LastWin;
        public static Guild PoleKeeper, CurrentTopLeader;
        private static bool changed = false;
        private static string[] scoreMessages;
        public static DateTime StartTime;
        public static void Initiate()
        {
            var Map = Kernel.Maps[1038];
            Pole = (SobNpcSpawn)Map.Npcs[810];
            LeftGate = (SobNpcSpawn)Map.Npcs[516074];
            RightGate = (SobNpcSpawn)Map.Npcs[516075];
        }
        public static void Start()
        {
            if (Features.GuildSeasonWar.Enabled) { Features.GuildSeasonWar.Start(); return; }
            if (LeftGate == null) return;
            Scores = new SafeDictionary<uint, Guild>(100);
            StartTime = DateTime.Now;
            LeftGate.Mesh = (ushort)(240 + LeftGate.Mesh % 10);
            RightGate.Mesh = (ushort)(270 + LeftGate.Mesh % 10);
            Kernel.SendWorldMessage(new Message("Guild War has begun!", System.Drawing.Color.Red, Message.Center), Server.GamePool);
            FirstRound = true;
            foreach (Guild guild in Kernel.Guilds.Values)
            {
                guild.WarScore = 0;
            }
            FlagsUpdate upd = new FlagsUpdate(true);
            upd.UID = LeftGate.UID;
            upd.Append((byte)FlagsUpdate.DataType.Mesh, LeftGate.Mesh);
            upd.Append((byte)FlagsUpdate.DataType.Hitpoints, LeftGate.Hitpoints);
            Kernel.SendWorldMessage(upd, Server.GamePool, (ushort)1038);
            upd.Clear();
            upd.UID = RightGate.UID;
            upd.Append((byte)FlagsUpdate.DataType.Mesh, RightGate.Mesh);
            upd.Append((byte)FlagsUpdate.DataType.Hitpoints, RightGate.Hitpoints);
            Kernel.SendWorldMessage(upd, Server.GamePool, (ushort)1038);
            IsWar = true;
        }
        public static void Reset()
        {
            if (Features.GuildSeasonWar.Enabled) return;
            Scores = new SafeDictionary<uint, Guild>(100);
            LeftGate.Mesh = (ushort)(240 + LeftGate.Mesh % 10);
            RightGate.Mesh = (ushort)(270 + LeftGate.Mesh % 10);

            LeftGate.Hitpoints = LeftGate.MaxHitpoints;
            RightGate.Hitpoints = RightGate.MaxHitpoints;
            Pole.Hitpoints = Pole.MaxHitpoints;

            FlagsUpdate upd = new FlagsUpdate(true);
            upd.UID = LeftGate.UID;
            upd.Append((byte)FlagsUpdate.DataType.Mesh, LeftGate.Mesh);
            upd.Append((byte)FlagsUpdate.DataType.Hitpoints, LeftGate.Hitpoints);
            Kernel.SendWorldMessage(upd, Server.GamePool, (ushort)1038);
            upd.Clear();
            upd.UID = RightGate.UID;
            upd.Append((byte)FlagsUpdate.DataType.Mesh, RightGate.Mesh);
            upd.Append((byte)FlagsUpdate.DataType.Hitpoints, RightGate.Hitpoints);
            Kernel.SendWorldMessage(upd, Server.GamePool, (ushort)1038);
            foreach (Guild guild in Kernel.Guilds.Values)
            {
                guild.WarScore = 0;
            }
            IsWar = true;
        }
        public static void FinishRound()
        {
            if (Features.GuildSeasonWar.Enabled) return;
            if (PoleKeeper != null && !FirstRound)//PoleKeeper != null
            {
                if (PoleKeeper.Wins == 0)
                    PoleKeeper.Losts++;
                else
                    PoleKeeper.Wins--;
                Database.GuildTable.UpdateGuildWarStats(PoleKeeper);
            }
            LastWin = Time32.Now;

            FirstRound = false;
            SortScores(out PoleKeeper);
            if (PoleKeeper != null)//PoleKeeper != null
            {
                Kernel.SendWorldMessage(new Message("Guild, " + PoleKeeper.Name + ", directed by " + PoleKeeper.LeaderName + " has won this Guild war round!", System.Drawing.Color.Red, Message.Center), Server.GamePool);
               // Kernel.SendWorldMessage(new Message("It is generald pardon time. You have 5 minutes to leave, run for your life!", System.Drawing.Color.White, Message.TopLeft), Server.GamePool, (ushort)6001);
                if (PoleKeeper.Losts == 0)
                    PoleKeeper.Wins++;
                else
                    PoleKeeper.Losts--;
                Database.GuildTable.UpdateGuildWarStats(PoleKeeper);
                Pole.Name = PoleKeeper.Name;
            }
            Pole.Hitpoints = Pole.MaxHitpoints;
            Kernel.SendWorldMessage(Pole, Server.GamePool, (ushort)1038);
            Reset();
        }
        public static void End()
        {
            if (Features.GuildSeasonWar.Enabled) { Features.GuildSeasonWar.Finish(); return; }
            EndLegacy();
        }
        internal static void EndLegacy(bool payLegacy = true)
        {
            if (PoleKeeper != null)
            {
                Kernel.SendWorldMessage(new Message("The Guild, " + PoleKeeper.Name + ", directed by " + PoleKeeper.LeaderName + " has won this Guild War! Guild War has ended!", System.Drawing.Color.White, Message.Center), Server.GamePool);
            }
            else
            {
                Kernel.SendWorldMessage(new Message("Guild War End and there was no winner!", System.Drawing.Color.Red, Message.Center), Server.GamePool);
            }
            IsWar = false;
            UpdatePole(Pole);
            if (!payLegacy) return;
            foreach (var client in Server.GamePool)
            {
                client.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.TopDeputyLeader);
                client.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.TopGuildLeader);
            }
            foreach (var pclient in Kernel.GamePool.Values)
            {
                try
                {
                    if (pclient.Guild != null && ReferenceEquals(pclient.Guild, PoleKeeper))
                    {
                        if (pclient.Entity.MapID == 1038)
                        {
                            if (pclient.AsMember.Rank == Enums.GuildMemberRank.GuildLeader)
                            {
                                pclient.Entity.AddConquerPoints(Revora.Config.GuildWarCp);
                                pclient.Entity.AdjustMoney((decimal)(Revora.Config.GuildWarGold));
                                //pclient.Inventory.Add(723467, 0, 4);
                                ///////////Guild War item///////////
                                uint itemid1 = 3302411;
                                uint itemid2 = 723467;
                                bool Bound = true;
                                byte Bless = 0;//Item damage                            
                                byte Days = 1;//Item duration in days                            
                                DateTime time = DateTime.Now;
                                pclient.Inventory.Addd(itemid1, 0, Bless, 0, 0, 0, Days, 1, Bound);
                                pclient.Inventory.Addd(itemid2, 0, Bless, 0, 0, 0, Days, 1, Bound);
                                pclient.Inventory.Addd(itemid2, 0, Bless, 0, 0, 0, Days, 1, Bound);
                                pclient.Inventory.Addd(itemid2, 0, Bless, 0, 0, 0, Days, 1, Bound);
                                pclient.Inventory.Addd(itemid2, 0, Bless, 0, 0, 0, Days, 1, Bound);
                                ///////////Guild War item///////////
                                //pclient.Inventory.Add(3004248, 0, 4);
                                //pclient.Inventory.Add(3004249, 0, 4);
                                pclient.Entity.AddRevoraPoints(Revora.Config.GuildWarLeaderRevoraPoints);
                                pclient.Entity.AddTopStatus((ulong)FlagsUpdate.Flags.TopGuildLeader, DateTime.Now.AddDays(6));
                                Kernel.SendWorldMessage(new Message("Congratulations! " + pclient.Entity.Name + " Leader of " + pclient.Guild.PoleKeeper + "  winning Guild has claimed the prize of war! " + Database.PremioEventos.GClanesOro + " Gold + " + Database.PremioEventos.GClanesCps + " Cps +  Slayer points for the entire Guild", Message.Center), Server.GamePool);
                            }
                            if (pclient.AsMember.Rank == Enums.GuildMemberRank.LeaderSpouse)
                            {
                                pclient.Entity.AddTopStatus((ulong)FlagsUpdate.Flags.TopGuildLeader, DateTime.Now.AddDays(6));
                                pclient.Entity.AddConquerPoints(Revora.Config.GuildWarCp);
                                pclient.Entity.AdjustMoney((decimal)(Revora.Config.GuildWarGold));
                                pclient.Entity.AddRevoraPoints(Revora.Config.GuildWarOfficerRevoraPoints);
                                //pclient.Inventory.Add(723467, 0, 2);
                                ///////////Guild War item///////////
                                uint itemid1 = 3302411;
                                uint itemid2 = 723467;
                                bool Bound = true;
                                byte Bless = 0;//Item damage                            
                                byte Days = 1;//Item duration in days                            
                                DateTime time = DateTime.Now;
                                pclient.Inventory.Addd(itemid1, 0, Bless, 0, 0, 0, Days, 1, Bound);
                                pclient.Inventory.Addd(itemid2, 0, Bless, 0, 0, 0, Days, 1, Bound);
                                pclient.Inventory.Addd(itemid2, 0, Bless, 0, 0, 0, Days, 1, Bound);

                                ///////////Guild War item///////////
                            }
                            if (pclient.AsMember.Rank == Enums.GuildMemberRank.DeputyLeader || pclient.AsMember.Rank == Enums.GuildMemberRank.DLeaderSpouse)
                            {
                                pclient.Entity.AddRevoraPoints(Revora.Config.GuildWarOfficerRevoraPoints);
                                //pclient.Inventory.Add(723467, 0, 2);
                                ///////////Guild War item///////////
                                uint itemid1 = 3302411;
                                uint itemid2 = 723467;
                                bool Bound = true;
                                byte Bless = 0;//Item damage                            
                                byte Days = 1;//Item duration in days                            
                                DateTime time = DateTime.Now;
                                pclient.Inventory.Addd(itemid1, 0, Bless, 0, 0, 0, Days, 1, Bound);
                                pclient.Inventory.Addd(itemid2, 0, Bless, 0, 0, 0, Days, 1, Bound);
                                pclient.Inventory.Addd(itemid2, 0, Bless, 0, 0, 0, Days, 1, Bound);

                                ///////////Guild War item///////////
                                //pclient.Inventory.Add(3004247, 0, 2);
                                //pclient.Inventory.Add(3004248, 0, 2);
                                //pclient.Inventory.Add(3004249, 0, 2);
                                pclient.Entity.AddConquerPoints(Revora.Config.GuildWarCp);
                                pclient.Entity.AdjustMoney((decimal)(Revora.Config.GuildWarGold));
                                pclient.Entity.AddTopStatus((ulong)FlagsUpdate.Flags.TopDeputyLeader, DateTime.Now.AddDays(6));
                            }

                            if (pclient.AsMember.Rank == Enums.GuildMemberRank.Member
                                || pclient.AsMember.Rank == Enums.GuildMemberRank.Agent
                                || pclient.AsMember.Rank == Enums.GuildMemberRank.Aide
                                || pclient.AsMember.Rank == Enums.GuildMemberRank.ArsenalAgent
                                || pclient.AsMember.Rank == Enums.GuildMemberRank.ArsFollower
                                || pclient.AsMember.Rank == Enums.GuildMemberRank.ASupervisor
                                || pclient.AsMember.Rank == Enums.GuildMemberRank.CPAgent
                                || pclient.AsMember.Rank == Enums.GuildMemberRank.CPFollower
                                || pclient.AsMember.Rank == Enums.GuildMemberRank.CPSupervisor
                                || pclient.AsMember.Rank == Enums.GuildMemberRank.DeputySteward
                                || pclient.AsMember.Rank == Enums.GuildMemberRank.DLeaderAide
                                || pclient.AsMember.Rank == Enums.GuildMemberRank.Follower
                                || pclient.AsMember.Rank == Enums.GuildMemberRank.GSupervisor
                                || pclient.AsMember.Rank == Enums.GuildMemberRank.GuideAgent
                                || pclient.AsMember.Rank == Enums.GuildMemberRank.GuideFollower
                                || pclient.AsMember.Rank == Enums.GuildMemberRank.HDeputyLeader
                                || pclient.AsMember.Rank == Enums.GuildMemberRank.HonoraryManager
                                || pclient.AsMember.Rank == Enums.GuildMemberRank.HonorarySteward
                                || pclient.AsMember.Rank == Enums.GuildMemberRank.HonorarySuperv
                                || pclient.AsMember.Rank == Enums.GuildMemberRank.LilyAgent
                                || pclient.AsMember.Rank == Enums.GuildMemberRank.LilyFollower
                                || pclient.AsMember.Rank == Enums.GuildMemberRank.LilySupervisor
                                || pclient.AsMember.Rank == Enums.GuildMemberRank.LSpouseAide
                                || pclient.AsMember.Rank == Enums.GuildMemberRank.Manager
                                || pclient.AsMember.Rank == Enums.GuildMemberRank.ManagerAide
                                || pclient.AsMember.Rank == Enums.GuildMemberRank.ManagerSpouse
                                || pclient.AsMember.Rank == Enums.GuildMemberRank.OrchidAgent
                                || pclient.AsMember.Rank == Enums.GuildMemberRank.OrchidFollower
                                || pclient.AsMember.Rank == Enums.GuildMemberRank.OSupervisor
                                || pclient.AsMember.Rank == Enums.GuildMemberRank.PKAgent
                                || pclient.AsMember.Rank == Enums.GuildMemberRank.PKFollower
                                || pclient.AsMember.Rank == Enums.GuildMemberRank.PKSupervisor
                                || pclient.AsMember.Rank == Enums.GuildMemberRank.RoseAgent
                                || pclient.AsMember.Rank == Enums.GuildMemberRank.RoseFollower
                                || pclient.AsMember.Rank == Enums.GuildMemberRank.RoseSupervisor
                                || pclient.AsMember.Rank == Enums.GuildMemberRank.SeniorMember
                                || pclient.AsMember.Rank == Enums.GuildMemberRank.SilverAgent
                                || pclient.AsMember.Rank == Enums.GuildMemberRank.SilverFollower
                                || pclient.AsMember.Rank == Enums.GuildMemberRank.SSupervisor
                                || pclient.AsMember.Rank == Enums.GuildMemberRank.Steward
                                || pclient.AsMember.Rank == Enums.GuildMemberRank.StewardSpouse
                                || pclient.AsMember.Rank == Enums.GuildMemberRank.Supervisor
                                || pclient.AsMember.Rank == Enums.GuildMemberRank.SupervisorAide
                                || pclient.AsMember.Rank == Enums.GuildMemberRank.SupervSpouse
                                || pclient.AsMember.Rank == Enums.GuildMemberRank.TSupervisor
                                || pclient.AsMember.Rank == Enums.GuildMemberRank.TulipAgent
                                || pclient.AsMember.Rank == Enums.GuildMemberRank.TulipFollower)
                            {
                                ///////////Guild War item///////////
                                uint itemid1 = 3302411;
                                bool Bound = true;
                                byte Bless = 0;//Item damage                            
                                byte Days = 1;//Item duration in days                            
                                DateTime time = DateTime.Now;
                                pclient.Inventory.Addd(itemid1, 0, Bless, 0, 0, 0, Days, 1, Bound);
                                ///////////Guild War item///////////
                                pclient.Entity.AddRevoraPoints(Revora.Config.GuildWarMemberRevoraPoints);
                                pclient.Entity.AddConquerPoints(Revora.Config.GuildWarMemberCp);
                                pclient.Entity.AdjustMoney((decimal)(Revora.Config.GuildWarMemberGold));
                                pclient.Entity.AddTopStatus((ulong)FlagsUpdate.Flags.TopDeputyLeader, DateTime.Now.AddDays(6));
                            }
                        }
                    }
                }
                catch
                {

                }
            }
        }
        public static void AddScore(uint addScore, Guild guild)
        {
            if (Features.GuildSeasonWar.Enabled) return;
            if (guild != null )
            {
                guild.WarScore += addScore;
                changed = true;
                if (!Scores.ContainsKey(guild.ID))
                    Scores.Add(guild.ID, guild);
                if ((int)Pole.Hitpoints <= 0)
                {
                    FinishRound();
                    return;
                }
            }
        }
        public static void SendScores()
        {
            if (Features.GuildSeasonWar.Enabled) { Features.GuildSeasonWar.SendScores(); return; }
            if (scoreMessages == null)
                scoreMessages = new string[0];
            if (Scores.Count == 0) return;
            if (changed)
                SortScores(out CurrentTopLeader);
            for (int c = 0; c < scoreMessages.Length; c++)
            {
                Message msg = new Message(scoreMessages[c], System.Drawing.Color.Red, c == 0 ? Message.FirstRightCorner : Message.ContinueRightCorner);
                Kernel.SendWorldMessage(msg, Server.GamePool, (ushort)1038);
                Kernel.SendWorldMessage(msg, Server.GamePool, (ushort)6001);
            }
        }
        private static void SortScores(out Guild winner)
        {
            winner = null;
            List<string> ret = new List<string>();

            int Place = 0;
            foreach (Guild guild in Scores.Values.OrderByDescending((p) => p.WarScore))
            {
                if (Place == 0)
                    winner = guild;
                string str = "No  " + (Place + 1).ToString() + ": " + guild.Name + "--(" + guild.WarScore + ")";
                ret.Add(str);
                Place++;
                if (Place == 4)
                    break;
            }

            changed = false;
            scoreMessages = ret.ToArray();
        }
        private static void UpdatePole(SobNpcSpawn pole)
        {
            new Database.MySqlCommand(Revora.Database.MySqlCommandType.UPDATE)
            .Update("sobnpcs").Set("name", pole.Name).Set("life", Pole.Hitpoints).Where("id", pole.UID).Execute();
        }
    }
}
