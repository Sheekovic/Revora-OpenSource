using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Revora.Client;
using Revora.Interfaces;
using Revora.Network.GamePackets;
using System.Drawing;
using Revora.Network;
using Revora.Game.ConquerStructures;
using System.Threading.Generic;
using Revora.Game.ConquerStructures.Society;
using Revora.Database;

namespace Revora.Game
{
    public class NobiltyPoleWar
    {
        public const int
            Serf = 0,
            // Knight = 1,
            // Baron = 3,
        Earl = 5,
        Duke = 7,
        Prince = 9,
        King = 12;

        public static SobNpcSpawn[] Poles = new SobNpcSpawn[13];
        public static uint[] PolesWinners = new uint[13];
        public static SafeDictionary<uint, Entity>[] AllScores = new SafeDictionary<uint, Entity>[13];

        public static DateTime WarStart;
        public static Map Map;
        public static bool IsWar = false;
        public static bool AllPole = false;
        private IDisposable Subscriber;

        public NobiltyPoleWar()
        {
            Map = Kernel.Maps[3842].MakeDynamicMap();
            Subscriber = Thread.Subscribe(work, 1000);

        }
        public void work(int time)
        {
            if (IsWar)
                if (DateTime.Now > WarStart.AddMinutes(60))
                    EndWar();
            //if (Copra_Times.Start.NobiltyWarPole && !IsWar)
            DateTime Now64 = DateTime.Now;
            if (Now64.DayOfWeek == DayOfWeek.Saturday && DateTime.Now.Hour == 20 && DateTime.Now.Minute == 30 && DateTime.Now.Second == 0 && !IsWar)
                    StartWar();
            if (IsWar)
            {
                if (Time32.Now > ScoreSendStamp.AddSeconds(3))
                {
                    ScoreSendStamp = Time32.Now;
                    SendScores();
                }
            }

            if (IsWar)
                if (DateTime.Now > WarStart.AddMinutes(60))
                {
                    AllPole = true;
                    foreach (var item in Server.GamePool)
                    {
                        if (item.Map.ID == Map.ID)
                        {
                            for (int i = 0; i < PolesWinners.Length; i++)
                            {
                                var winner = PolesWinners[i];
                                if (item.Entity.UID != winner)
                                    item.Entity.Teleport(1002, 408, 330);
                            }
                        }
                    }
                }


        }
        public static void Join(Client.GameState client)
        {
            if (IsWar)
            {
                var cooord = Map.RandomCoordinates();
                client.Entity.Teleport(Map.ID, 255, 246);

                client.OnDisconnect = p =>
                {
                    p.Entity.Teleport(1002, 408, 330);
                    client.Entity.PKMode = Game.Enums.PKMode.PK;
                    client.Send(new Data(true) { UID = client.Entity.UID, ID = Data.ChangePKMode, dwParam = (uint)client.Entity.PKMode });
                };
                //client.Entity.OnDeath = p =>
                //{
                //    if (p.MapID == Map.ID)
                //    {
                //        var cooordx = Map.RandomCoordinates();
                //        p.Teleport(Map.ID, cooordx.Item1, cooordx.Item2);
                //        p.BringToLife();
                //    }
                //};
            }
        }

        public static void StartWar()
        {
            try
            {
                if (IsWar)
                    EndWar();
                AllPole = false;
                IsWar = true;
                WarStart = DateTime.Now;
                if (!Constants.PKFreeMaps.Contains(Map.ID))
                    Constants.PKFreeMaps.Add(Map.ID);
                Poles = new SobNpcSpawn[13];
                PolesWinners = new uint[13];

                Map.Statues.Clear();
                Map.Npcs.Clear();

                Start();


                Summon("King", 269, 344, King);
                Summon("Prince", 171, 271, Prince);
                Summon("Duke", 159, 163, Duke);
                Summon("Earl", 264, 164, Earl);
                //Summon("ALL", 348, 268, Serf);
                Kernel.SendWorldMessage(new Message("Nobility Pole War has begun! Run to the field and claim the purse!", Color.White, Message.Center), Server.GamePool);
                foreach (var client in Server.GamePool)
                    
                    client.MessageBox("Nobility Pole War has begun! Unique prizes and a Top title await. Will you attend?",
                            p => { p.Entity.Teleport(1002, 243, 236); }, null, 60);


            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                Server.SaveException (ex);
            }
        }

        public static bool Attack(uint addScore, Entity entity, SobNpcSpawn Pole)
        {
            if (!IsWar)
                return false;
            int _class = (int)entity.NobilityRank;
            if (_class >= 13) return false;
            var pole = Poles[_class];
            if (pole != null)
            {
                if (!AllPole)
                {
                    if (pole != Pole)
                        return false;
                }
                else
                {
                    if (Poles[Serf] != Pole)
                        return false;
                    for (int i = 0; i < PolesWinners.Length; i++)
                    {
                        var winner = PolesWinners[i];
                        if (entity.UID != winner)
                            entity.Teleport(1002, 408, 330);
                    }
                }


                if (PolesWinners[_class] == entity.UID)
                    return false;

                if (addScore > 0)
                    AddScore(addScore, entity, pole);

                return true;
            }
            return false;
        }

        public static void Summon(string Name, ushort X, ushort Y, int rank)
        {

            SobNpcSpawn Base = new SobNpcSpawn();
            Base.UID = Map.EntityUIDCounter2.Next;
            Base.Mesh = 8686;
            Base.Type = Enums.NpcType.Stake;
            Base.Sort = 21;
            Base.ShowName = true;
            Base.Name = Name;
            Base.Hitpoints = Base.MaxHitpoints = 50000000;//99
            Base.MapID = Map.ID;
            Base.X = X;
            Base.Y = Y;
            Poles[rank] = Base;
            if (!Map.Npcs.ContainsKey(Base.UID))
                Map.Npcs.Add(Base.UID, Base);
        }

        #region Score

        public static bool FirstRound = false;
        public static Time32 ScoreSendStamp, LastWin;

        public static void Start()
        {
            AllScores = new SafeDictionary<uint, Entity>[13];
            WarStart = DateTime.Now;
            FirstRound = true;
            foreach (var c in Server.GamePool)
                c.Entity.WarScore = 0;
            IsWar = true;
        }

        public static void Reset(int _Class)
        {
            foreach (var players in Kernel.GamePool.Values)
                players.Entity.WarScore = 0;
            AllScores = new SafeDictionary<uint, Entity>[13];
            IsWar = true;
        }

        public static void FinishRound(SobNpcSpawn Pole, int _Class)
        {
            Entity PoleKeeper;
            LastWin = Time32.Now;
            FirstRound = false;
            SortScores(out PoleKeeper, _Class);
            if (PoleKeeper != null)
            {
                Kernel.SendWorldMessage(new Message("" + PoleKeeper.Name + " has won this round! Hurry, there is still time to take the pole [NobilityPoleWar]!", System.Drawing.Color.Red, Message.Center), Server.GamePool);
                Pole.Name = PoleKeeper.Name;
                PolesWinners[_Class] = PoleKeeper.UID;
            }
            Pole.Hitpoints = Pole.MaxHitpoints;
            foreach (var c in Server.GamePool)
            {
                if (c.Map.ID == Map.ID)
                    Pole.SendSpawn(c);
            }
            Reset(_Class);
        }

        public static void AddScore(uint addScore, Entity entity, SobNpcSpawn Pole)
        {
            if (entity != null)
            {
                if (Pole.Hitpoints <= addScore)
                    Pole.Hitpoints = 0;
                var Scores = AllScores[(ushort)((int)entity.NobilityRank)];
                if (Scores == null)
                    Scores = AllScores[(ushort)((int)entity.NobilityRank)] = new SafeDictionary<uint, Entity>();

                if (!Scores.ContainsKey(entity.UID))
                    Scores.Add(entity.UID, entity);

                entity.WarScore += addScore;

                if ((int)Pole.Hitpoints <= 0)
                {
                    FinishRound(Pole, (int)entity.NobilityRank);
                    return;
                }
            }
        }

        public static void SendScores()
        {
            if (AllPole)
            {
                Entity CurrentTopLeader;

                var scoreMessages = SortScores(out CurrentTopLeader, 0);
                for (int c = 0; c < scoreMessages.Length; c++)
                {
                    Message msg = new Message(scoreMessages[c], System.Drawing.Color.Red, c == 0 ? Message.FirstRightCorner : Message.ContinueRightCorner);
                    Kernel.SendWorldMessage(msg, Server.GamePool, Map.ID);
                }
                return;
            }
            for (int i = 0; i < AllScores.Length; i++)
            {
                var Scores = AllScores[i];

                if (Scores == null)
                    continue;
                if (Scores.Count == 0)
                    continue;

                Entity CurrentTopLeader;

                var scoreMessages = SortScores(out CurrentTopLeader, i);
                if (scoreMessages == null)
                    continue;
                for (int c = 0; c < scoreMessages.Length; c++)
                {
                    Message msg = new Message(scoreMessages[c], System.Drawing.Color.Red, c == 0 ? Message.FirstRightCorner : Message.ContinueRightCorner);
                    foreach (Client.GameState client in Server.GamePool)
                    {
                        if (client != null)
                        {
                            if (client.Map.ID == Map.ID)
                            {
                                if (client.Entity.NobilityRank == (NobilityRank)i)
                                    client.Send(msg);
                            }
                        }
                    }
                }
            }
        }

        private static string[] SortScores(out Entity winner, int i)
        {
            winner = null;
            List<string> ret = new List<string>();
            int Place = 0;

            var Scores = AllScores[i];
            if (Scores == null) return new string[0];
            foreach (var e in Scores.Values.OrderByDescending((p) => p.WarScore))
            {
                if (Place == 0)
                    winner = e;
                string str = "No  " + (Place + 1).ToString() + ": " + e.Name + "(" + e.WarScore + ")";
                ret.Add(str);
                Place++;
                if (Place == 4)
                    break;
            }

            return ret.ToArray();
        }
        #endregion

        public static void EndWar()
        {
            Kernel.SendWorldMessage(new Message("Nobility Pole War has ended. We meet again in eight days, on Saturday!", System.Drawing.Color.Red, Message.Center), Server.GamePool);
            IsWar = false;
            Map.Npcs.Clear();
            foreach (var client in Server.GamePool)
                if (client.Entity.MapID == Map.ID)
                    client.Entity.Teleport(1002, 309, 192);
        }
        public static void GetReward(GameState c, uint cps)
        {
            for (int i = 0; i < PolesWinners.Length; i++)
            {
                var winner = PolesWinners[i];


                if (c.Entity.UID == winner)
                {
                    uint cpss = 0;
                    if (i == 0)
                        cpss = cps;
                    else
                        cpss += cps ;

                    c.Entity.BoundCps += cpss;
                    PolesWinners[i] = 0;
                    if (c.Entity.NobilityRank == NobilityRank.King)
                    {

                        Kernel.SendWorldMessage(new Message("Congratulations! " + c.Entity.Name + " is this week's finest King. The purse: 300,000 Revora Points + 300,000,000,000 gold + 1,500,000 CPs, and a Top title!", System.Drawing.Color.White, Message.Center), Server.GamePool);
                        c.Entity.AddRevoraPoints(300000);
                        c.Entity.AdjustMoney((decimal)(300000000000));
                        c.Entity.AddTopStatus((ulong)FlagsUpdate.Flags.TopRey, DateTime.Now.AddDays(7));
                        //c.Player.AddTopStatus((ulong)FlagsUpdate.Flags.JQ_dunpaichuantou, 3, DateTime.Now.AddDays(7));
                    }
                    if (c.Entity.NobilityRank == NobilityRank.Prince)
                    {
                        Kernel.SendWorldMessage(new Message("Congratulations! " + c.Entity.Name + " is this week's finest Prince. The purse: 200,000 Revora Points + 200,000,000,000 gold + 1,500,000 CPs, and a Top title!", System.Drawing.Color.White, Message.Center), Server.GamePool);
                        c.Entity.AddRevoraPoints(200000);
                        c.Entity.AdjustMoney((decimal)(200000000000));
                        c.Entity.AddTopStatus((ulong)FlagsUpdate.Flags.TopPrincipe, DateTime.Now.AddDays(7));
                        //c.Player.AddTopStatus((ulong)FlagsUpdate.Flags.JQ_dunpaichuantou, 3, DateTime.Now.AddDays(7));
                    }
                    if (c.Entity.NobilityRank == NobilityRank.Duke)
                    {
                        Kernel.SendWorldMessage(new Message("Congratulations! " + c.Entity.Name + " is this week's finest Duke. The purse: 100,000 Revora Points + 100,000,000,000 gold + 1,500,000 CPs, and a Top title!", System.Drawing.Color.White, Message.Center), Server.GamePool);
                        c.Entity.AddRevoraPoints(100000);
                        c.Entity.AdjustMoney((decimal)(100000000000));
                        c.Entity.AddTopStatus((ulong)FlagsUpdate.Flags.TopDuke, DateTime.Now.AddDays(7));
                        //c.Player.AddTopStatus((ulong)FlagsUpdate.Flags.JQ_dunpaichuantou, 3, DateTime.Now.AddDays(7));
                    }
                    if (c.Entity.NobilityRank == NobilityRank.Earl)
                    {
                        Kernel.SendWorldMessage(new Message("Congratulations! " + c.Entity.Name + " is this week's finest Earl. The purse: 100,000 Revora Points + 100,000,000,000 gold + 1,500,000 CPs, and a Top title!", System.Drawing.Color.White, Message.Center), Server.GamePool);
                        c.Entity.AddRevoraPoints(100000);
                        c.Entity.AdjustMoney((decimal)(100000000000));
                        c.Entity.AddTopStatus((ulong)FlagsUpdate.Flags.TopConde, DateTime.Now.AddDays(7));
                    
                    }
                }
            }
        }
    }
}
