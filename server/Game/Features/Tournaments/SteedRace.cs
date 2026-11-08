// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using Revora.Network.GamePackets;

namespace Revora.Game
{
    public unsafe class SteedRace
    {
        public uint[][] RaceSettings = new[]
        {
            new uint[]{ (uint)Enums.Maps.DesertRace, 568, 14,
                30, 563, 6,
                564, 14, 623,
                530, 47, 837,
                205, 371, 87, 
                485, 85, 371},
            new uint[]{ (uint)Enums.Maps.CactusRace, 633, 432, 
                85, 371, 6,
                649, 442, 623,
                462, 603, 593,
                519, 633, 279, 
                562, 85, 371},
            new uint[]{ (uint)Enums.Maps.MarketRace, 88, 149, 
                420, 431, 4,  
                65, 174, 621,
                123, 243, 60, 
                214, 334, 70, 
                346, 459, 100},
            new uint[]{ (uint)Enums.Maps.IceRace, 175, 250, 
                200, 153, 6,
                154, 267, 621,
                146, 392, 70,
                283, 351, 100,
                295, 079, 100},
            new uint[]{ (uint)Enums.Maps.IslandRace, 60, 400, 
                899, 816, 10,
                96, 392, 621,
                220, 234, 200,
                472, 160, 200,
                777, 464, 300},
            new uint[]{ (uint)Enums.Maps.DungeonRace, 450, 520,
                682, 484 , 10,
                435, 559, 621,
                471, 759, 200,
                714, 598, 250,
                489, 679, 20},
            new uint[]{ (uint)Enums.Maps.LavaRace, 150, 350,
                330, 170, 6,
                101, 397, 623,
                327, 553, 100,
                526, 477, 200,
                283, 275, 100},
                /*
            new uint[]{ (uint)Enums.Maps.PhoenixRace, 163, 276, 
                571, 31, 6,
                179, 279, 623,
                172, 314, 837,
                205, 371, 87, 
                485, 85, 371},
                */
        };
        public static ushort MAPID = 1950;
        public static uint[] Settings;
        public static uint RaceRecord;
        private Map Map;
        public static bool isOn, InvitationsOut, InvitationsExpired, FiveSecondsLeft, GateOpen;
        private DateTime InvitationsSentOut, InvitationsExpireDate, Last5Seconds, GateOpened;
        public int SecondsLeftUntilStart
        {
            get { return (int)(InvitationsSentOut.AddMinutes(1) - DateTime.Now).TotalSeconds - 5; }
        }
        public bool CanJoin
        {
            get { return isOn && !GateOpen; }
        }
        public bool IsOn
        {
            get { return isOn; }
        }
        private SobNpcSpawn Gate;
        private ushort GateSetX, GateSetY;
        public ushort GateX
        {
            get { return GateSetX; }
        }
        public ushort GateY
        {
            get { return GateSetY; }
        }
        private Guid runId;
        private IDisposable Subscriber;
        public SteedRace():this(true) { }
        internal SteedRace(bool subscribeLegacyTimer)
        {
            if(subscribeLegacyTimer)Subscriber = Thread.Subscribe(work, 1000);
        }
        public void Create()
        {
            Settings = null;
            var available = new System.Collections.Generic.List<uint>();
            foreach (var track in RaceSettings)
                if (Database.MapsTable.MapInformations.ContainsKey((ushort)track[0]))
                    available.Add(track[0]);
            if (available.Count == 0) return;
            uint selected = available[Kernel.Random.Next(available.Count)];
            if (!Kernel.Maps.ContainsKey((ushort)selected)) new Map((ushort)selected, "");
            if (Kernel.Maps.ContainsKey((ushort)selected)) Create(selected);
        }
        public void Create(uint mapId)
        {
            int index = -1;
            for (int i = 0; i < RaceSettings.Length; i++)
            {
                if (RaceSettings[i][0] == mapId)
                {
                    index = i;
                    break;
                }
            }
            if (index == -1) return;
            Settings = RaceSettings[index];
            MAPID = (ushort)Settings[0];
            var info = Database.MapsTable.MapInformations[MAPID];

            RaceRecord = info.RaceRecord;
            Map = Kernel.Maps[MAPID];
            if (!Constants.PKFreeMaps.Contains(MAPID))
                Constants.PKFreeMaps.Add(MAPID);
            foreach (var item in Map.StaticEntities.Values)
                Map.Floor[item.X, item.Y, MapObjectType.StaticEntity, null] = true;
            Map.StaticEntities.Clear();
            Map.Npcs.Clear();
            Map.Entities.Clear();

            Gate = new SobNpcSpawn();
            Gate.UID = 19551;
            Gate.X = (ushort)Settings[6];
            Gate.Y = (ushort)Settings[7];
            Gate.Mesh = (ushort)Settings[8];
            Gate.ShowName = true;
            Gate.Name = " ";
            Gate.Type = Enums.NpcType.Furniture;
            GateSetX = Gate.X;
            GateSetY = Gate.Y;
            Map.AddNpc(Gate);
            Init();
        }
        private void Init()
        {
            isOn = InvitationsOut = InvitationsExpired = GateOpen = FiveSecondsLeft = false;
            GeneratePotions();
            runId = Guid.Empty;
        }
        private void GeneratePotions()
        {
            uint count = 100;
            ushort x, y;
            Tuple<ushort, ushort, int>[] limits = new[]
            {
                new  Tuple<ushort,ushort,int>( (ushort)Settings[9], (ushort)Settings[10], (int)Settings[11] ),
                new  Tuple<ushort,ushort,int>( (ushort)Settings[12], (ushort)Settings[13], (int)Settings[14] ),
                new  Tuple<ushort,ushort,int>( (ushort)Settings[15], (ushort)Settings[16], (int)Settings[17] )
            };

            while (count > 0)
            {
                x = (ushort)Kernel.Random.Next(0, Map.Floor.Bounds.Width);
                y = (ushort)Kernel.Random.Next(0, Map.Floor.Bounds.Height);
                bool valid = false;
                foreach (var range in limits)
                    valid |= (Kernel.GetDistance(x, y, range.Item1, range.Item2) < range.Item3);
                if (valid)
                {
                    if (Map.Floor[x, y, MapObjectType.StaticEntity, null] && Map.Floor[x, y, MapObjectType.Player, null])
                    {
                        bool v = true;
                        for (int i = 0; i < Game.Map.XDir.Length; i++)
                            if ((!Map.Floor[x + Game.Map.XDir[i], y + Game.Map.YDir[i], MapObjectType.Player, null] ||
                                !Map.Floor[x + Game.Map.XDir[i], y + Game.Map.YDir[i], MapObjectType.StaticEntity, null]) && v)
                                v = false;
                        if (!v) continue;

                        StaticEntity item = new StaticEntity((uint)(x * 1000 + y), x, y, MAPID);
                        item.Pick();

                        item.MapID = MAPID;
                        Map.AddStaticEntity(item);
                        count--;
                    }
                }
            }
        }
        public void work(int time)
        {
            return;
            DateTime now = DateTime.Now;
            //bool rightHour = now.Hour == 4 || now.Hour == 8 || now.Hour == 12 || now.Hour == 18 || now.Hour == 23; 
            bool rightHour = now.Hour == 4 || now.Hour == 8 || now.Hour == 12 || now.Hour == 18 || now.Hour == 23;
            if (rightHour && now.Minute < 30)
            {
                if (!InvitationsOut)
                {
                    if (now.Minute == 00)
                    {
                        Create();
                        SendInvitations();
                    }
                }
                else if (!InvitationsExpired)
                {
                    if (now >= InvitationsExpireDate)
                    {
                        InvitationsExpired = true;
                        FiveSecondsLeft = false;
                        Last5Seconds = InvitationsSentOut.AddMinutes(1).AddSeconds(-12);
                    }
                }
                else if (!FiveSecondsLeft)
                {
                    if (now > Last5Seconds)
                    {
                        FiveSecondsLeft = true;
                        SendData(Data.BeginSteedRace, uid: 1);
                        Last5Seconds = Last5Seconds.AddSeconds(5);
                    }
                }
                else if (!GateOpen)
                {
                    if (now > Last5Seconds)
                    {
                        OpenGate();
                    }
                }
            }
            else if (rightHour && now.Minute >= 30)
            {
                if (isOn)
                {
                    End();
                }
            }
        }
        private void SendInvitations()
        {
            isOn = true;
            InvitationsOut = true;
            InvitationsExpired = false;
            InvitationsSentOut = DateTime.Now;
            InvitationsExpireDate = InvitationsSentOut.AddSeconds(30);
            foreach (var client in Server.GamePool)
            {
                if (!client.InQualifier() && client.Entity.PokerTable == 0 && client.PlayRouletteUID == 0 && client.Map.BaseID != 6001 && client.Map.BaseID != 6004 && client.Map.BaseID != 6000 && client.Map.BaseID != 6004 && !client.Entity.Dead)
                {
                    AutoInvite alert = new AutoInvite
                    {
                        StrResID = 10525,
                        Countdown = 30,
                        Action = 1
                    };
                    client.Entity.StrResID = 10525;
                    client.Send(alert.ToArray());
                }
            }
            Kernel.SendWorldMessage(new Message("The Horse Race has begun! You have one minute to join. Go to HorseRaceManager in Twin City!", Message.Center), Server.GamePool);
        }
        public bool Join(Client.GameState client)
        {
            if (!isOn || GateOpen || !Features.SteedRaceSystem.CanEnter(client) || !Features.SteedRaceResults.Admit(runId,client)) return false;
            if (client.Entity.MapID == 3846)
            {
                Data data = new Data(true);
                data.UID = client.Entity.UID;
                data.ID = 104;
                data.TimeStamp = Time32.Now;
                data.dwParam = 0;
                client.Send(data);
                client.SendScreen(data, true);
                client.Screen.Reload(null);
            }
            int seconds = SecondsLeftUntilStart;
            if (seconds > 0)
                client.Send(new Data(true) { UID = client.Entity.UID, ID = Data.CountDown, dwParam = (uint)seconds });
            client.Entity.AddFlag((ulong)FlagsUpdate.Flags.Ride, StatusFlagsBigVector32.PermanentFlag, true);
            client.Entity.Teleport(MAPID, (ushort)Settings[1], (ushort)Settings[2]);
            client.Send(new RaceRecord()
            {
                Type = RaceRecordTypes.BestTime,
                Rank = (int)Database.MapsTable.MapInformations[MAPID].RaceRecord,
                dwParam = 1800000
            });
            client.Send(new RacePotion(true) { PotionType = Enums.RaceItemType.Null, Amount = 1 });
            client.Send(new RacePotion(true) { PotionType = Enums.RaceItemType.Null, Amount = 0 });
            client.Potions = new UsableRacePotion[5];
            return client.Entity.MapID==MAPID && client.Map.ID==MAPID;
        }
        public void OpenGate()
        {
            if (!Features.SteedRaceResults.Open(runId)) return;
            GateOpened = DateTime.Now;
            GateOpen = true;
            if (Gate != null)
            {
                Gate.X = 0;
                Gate.Y = 0;
                Send(new Data(true) { UID = Gate.UID, ID = Data.RemoveEntity });
            }
        }

        public void PrepareEvent()
        {
            isOn = true;
            InvitationsOut = true;
            InvitationsExpired = true;
            GateOpen = false;
            FiveSecondsLeft = true;
            runId = Features.SteedRaceResults.Begin(MAPID,(ushort)Settings[3],(ushort)Settings[4]);
            Features.SteedRaceCourse.Configure(runId,Map,(ushort)Settings[1],(ushort)Settings[2],(ushort)Settings[3],(ushort)Settings[4],(int)Settings[5]);
            GateOpened = DateTime.Now;
        }

        public void SignalStart()
        {
            SendData(Data.BeginSteedRace, uid: 1);
        }
        private void Send(Interfaces.IPacket packet)
        {
            Kernel.SendWorldMessage(packet, Server.GamePool, MAPID);
        }
        private void SendData(ushort ID, uint value = 0, uint uid = 0)
        {
            Data data = null;
            if (uid != 0)
                data = new Data(true) { UID = uid, ID = ID, dwParam = value };

            foreach (var player in Server.GamePool)
            {
                if (player.Entity.MapID == MAPID)
                {
                    if (uid == 0)
                    {
                        data = new Data(true) { UID = player.Entity.UID, ID = ID, dwParam = value };
                        player.Send(data);
                    }
                    else
                    {
                        player.Send(data);
                    }
                }
            }
        }
        private void Status(Client.GameState client, int rank, int time, int award)
        {
            var packet = new RaceRecord()
            {
                Type = RaceRecordTypes.AddRecord,
                Rank = rank,
                Name = client.Entity.Name,
                Time = time,
                Prize = award
            };
            Send(packet);
        }
        public void End()
        {
            Features.SteedRaceResults.End(runId);
            InvitationsOut = false;
            if (Gate != null)
            {
                Gate.X = GateSetX;
                Gate.Y = GateSetY;
            }
            foreach (var player in Server.GamePool)
                if (player.Entity != null && player.Entity.MapID == MAPID)
                    Exit(player);
            Init();
        }
        public void FinishRace(Client.GameState client)
        {
            if (!isOn || !GateOpen) return;
            int rank = Features.SteedRaceResults.Finish(runId,client);
            if (rank == 0) return;
            if (rank > 3) { Exit(client); return; }
            TimeSpan span = DateTime.Now - GateOpened;
            int award = unchecked((int)Features.SteedRaceResults.Prize(runId,client.Entity.UID));
            var key = Database.MapsTable.MapInformations[MAPID];
            if (key.RaceRecord > span.TotalMilliseconds) {
                key.RaceRecord = (uint)span.TotalMilliseconds;
                Database.MapsTable.SaveRecord(key);
            }
            Status(client,rank,(int)span.TotalMilliseconds,award);
            client.Send(new RaceRecord { Type=RaceRecordTypes.EndTime,Rank=rank,dwParam=(int)span.TotalMilliseconds,
                dwParam2=award,Time=(int)span.TotalMilliseconds,Prize=award });
            Features.SteedRaceSystem.OnFinish(client,rank);
        }
        public void Exit(Client.GameState client)
        {
            Features.SteedRaceResults.Withdraw(client);
            if (client == null || client.Entity == null) return;
            switch (client.Entity.PreviousMapID)
            {
                default:
                    {
                        client.Entity.Teleport(1002, 408, 330);
                        break;
                    }
                case 1000:
                    {
                        client.Entity.Teleport(1000, 500, 650);
                        break;
                    }
                case 1020:
                    {
                        client.Entity.Teleport(1020, 565, 562);
                        break;
                    }
                case 1011:
                    {
                        client.Entity.Teleport(1011, 188, 264);
                        break;
                    }
                case 1015:
                    {
                        client.Entity.Teleport(1015, 717, 571);
                        break;
                    }
            }
        }
    }
}