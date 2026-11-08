using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Revora.Network.GamePackets;
using Revora.Game.ConquerStructures.Society;

namespace Revora.Game
{
    public class PoleTwin
    {
        public static SobNpcSpawn Pole, RightGate, LeftGate;
        public static SafeDictionary<uint, Guild> Scores = new SafeDictionary<uint, Guild>(100);
        public static bool IsWar = false, FirstRound = false;
        public static Time32 ScoreSendStamp, LastWin;
        public static Guild PoleKeeper3, CurrentTopLeader;
        private static bool changed = false;
        private static string[] scoreMessages;
        public static DateTime StartTime;
        public static bool Claim
        {
            get { return Server.Vars["egwclaim"]; }
            set { Server.Vars["egwclaim"] = value; }
        }
        public static uint KeeperID
        {
            get { return Server.Vars["egwkeeperid"]; }
            set { Server.Vars["egwkeeperid"] = value; }
        }
        public static void Initiate()
        {
            var Map = Kernel.Maps[2072];
            Pole = (SobNpcSpawn)Map.Npcs[819];
            LeftGate = (SobNpcSpawn)Map.Npcs[15961];
            RightGate = (SobNpcSpawn)Map.Npcs[15962];
        }
        public static void Start()
        {
            if (LeftGate == null) return;
            Scores = new SafeDictionary<uint, Guild>(100);
            StartTime = DateTime.Now;
            LeftGate.Mesh = (ushort)(240 + LeftGate.Mesh % 10);
            RightGate.Mesh = (ushort)(270 + LeftGate.Mesh % 10);
            Kernel.SendWorldMessage(new Message("The Gold War has begun! Prove that you are one of the best!", System.Drawing.Color.Red, Message.Center), Server.GamePool);
            FirstRound = true;
            foreach (Guild guild in Kernel.Guilds.Values)
            {
                guild.PTScore = 0;
            }
            FlagsUpdate upd = new FlagsUpdate(true);
            upd.UID = LeftGate.UID;
            upd.Append((byte)FlagsUpdate.DataType.Mesh, LeftGate.Mesh);
            upd.Append((byte)FlagsUpdate.DataType.Hitpoints, LeftGate.Hitpoints);
            Kernel.SendWorldMessage(upd, Server.GamePool, (ushort)2072);
            upd.Clear();
            upd.UID = RightGate.UID;
            upd.Append((byte)FlagsUpdate.DataType.Mesh, RightGate.Mesh);
            upd.Append((byte)FlagsUpdate.DataType.Hitpoints, RightGate.Hitpoints);
            Kernel.SendWorldMessage(upd, Server.GamePool, (ushort)2072);
            IsWar = true;
        }
        public static void Reset()
        {
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
            Kernel.SendWorldMessage(upd, Server.GamePool, (ushort)2072);
            upd.Clear();
            upd.UID = RightGate.UID;
            upd.Append((byte)FlagsUpdate.DataType.Mesh, RightGate.Mesh);
            upd.Append((byte)FlagsUpdate.DataType.Hitpoints, RightGate.Hitpoints);
            Kernel.SendWorldMessage(upd, Server.GamePool, (ushort)2072);
            foreach (Guild guild in Kernel.Guilds.Values)
            {
                guild.PTScore = 0;
            }
            IsWar = true;
        }

        public static void FinishRound()
        {
            if (PoleKeeper3 != null && !FirstRound)
            {
                if (PoleKeeper3.Wins == 0)
                    PoleKeeper3.Losts++;
                else
                    PoleKeeper3.Wins--;
                Database.GuildTable.UpdateGuildWarStats(PoleKeeper3);
            }
            LastWin = Time32.Now;

            FirstRound = false;
            SortScores(out PoleKeeper3);
            if (PoleKeeper3 != null)
            {
                KeeperID = PoleKeeper3.ID;
                Kernel.SendWorldMessage(new Message("The guild, " + PoleKeeper3.Name + ", owned by " + PoleKeeper3.LeaderName + " has claimed the pole in the Gold War!", System.Drawing.Color.Red, Message.Center), Server.GamePool);
                if (PoleKeeper3.Losts == 0)
                    PoleKeeper3.Wins++;
                else
                    PoleKeeper3.Losts--;
                Database.GuildTable.UpdateGuildWarStats(PoleKeeper3);
                Pole.Name = PoleKeeper3.Name;
            }
            Pole.Hitpoints = Pole.MaxHitpoints;
            Kernel.SendWorldMessage(Pole, Server.GamePool, (ushort)2072);
            Reset();
        }
        public static void End()
        {
            IsWar = false;
            UpdatePole(Pole);
            foreach (var client in Server.GamePool)
            {

            }
        }
        public static void AddScore(uint addScore, Guild guild)
        {
            if (guild != null)
            {
                guild.PTScore += addScore;
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
            if (scoreMessages == null)
                scoreMessages = new string[0];
            if (Scores.Count == 0) return;
            if (changed)
                SortScores(out CurrentTopLeader);
            for (int c = 0; c < scoreMessages.Length; c++)
            {
                Message msg = new Message(scoreMessages[c], System.Drawing.Color.Red, c == 0 ? Message.FirstRightCorner : Message.ContinueRightCorner);
                Kernel.SendWorldMessage(msg, Server.GamePool, (ushort)2072);
            }
        }
        private static void SortScores(out Guild winner)
        {
            winner = null;
            List<string> ret = new List<string>();

            int Place = 0;
            foreach (Guild guild in Scores.Values.OrderByDescending((p) => p.PTScore))
            {
                if (Place == 0)
                    winner = guild;
                string str = "No " + (Place + 1).ToString() + ": " + guild.Name + " (" + guild.PTScore + ")";
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