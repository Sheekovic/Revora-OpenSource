using System;
using System.Linq;
using System.Text;
using System.Drawing;
using Revora.Client;
using System.Collections.Generic;
using Revora.Network.GamePackets;
using System.Collections.Concurrent;

namespace Revora.Game
{
    public unsafe class KillTournament
    {
        private ushort mapID;
        private Map map;
        private WeekDay day;
        private int hour, minute;
        private Func<GameState, bool> selectFunc;
        private IDisposable Subscriber;
        private bool onGoing, announcement, canAttack;
        private string name;
        private ConcurrentDictionary<uint, GameState> players;
        private Action<GameState> prize;
        private Time32 cantAttack;
        private string message;
        public bool IsOn
        {
            get { return onGoing; } 
        }
        public KillTournament(ushort mapID, WeekDay day, int hour, int minute, Action<GameState> prize, string name, Func<GameState, bool> selectFunc, string message = "")
        {
            this.mapID = mapID;
            Constants.PKFreeMaps.Add(this.mapID);
            this.day = day;
            this.hour = hour;
            this.minute = minute;
            this.prize = prize;
            this.name = name;
            this.message = message;
            this.selectFunc = selectFunc;
            this.Subscriber = Thread.Subscribe(work, 1000);
            this.onGoing = this.announcement = this.canAttack = false;
            this.players = new ConcurrentDictionary<uint, GameState>();
        }
        private void work(int time)
        {
            if (map == null)
            {
                if (!Kernel.Maps.ContainsKey(mapID)) return;
                map = Kernel.Maps[mapID];
            }
            DateTime time64 = DateTime.Now;
            DateTime nextTournament = setDate(time64);
            if (day.Contains(time64.DayOfWeek))
            {
                if (!onGoing)
                {
                    if (!announcement)
                    {
                        if (time64 >= nextTournament.Subtract(TimeSpan.FromMinutes(5)) && time64 <= nextTournament)
                        {
                            announcement = true;
                            Kernel.SendWorldMessage(new Message(name + " will begin in 5 minutes.", Color.Red, Message.Center));
                        }
                    }
                    else
                    {
                        if (time64 >= nextTournament)
                        {
                            onGoing = true;
                            canAttack = false;
                            cantAttack = Time32.Now.AddMinutes(1);
                            foreach (var client in Server.GamePool)
                            {
                                if (selectFunc(client))
                                {
                                    client.SelectionKillTournament = this;
                                    client.MessageOK = (pClient) =>
                                    {
                                        if (pClient.SelectionKillTournament.onGoing)
                                            pClient.SelectionKillTournament.joinClient(pClient);
                                    };
                                    if (message != "") client.Send(message);
                                    client.Send(new NpcReply(NpcReply.MessageBox, name + " has begun. Will you join?"));
                                    client.Send(new Data(true) { UID = client.Entity.UID, dwParam = 60, ID = Data.CountDown });
                                }
                            }
                        }
                    }
                }
                else
                {
                    if (!canAttack)
                    {
                        if (time64 >= nextTournament.AddMinutes(1))
                        {
                            canAttack = true;
                            foreach (var client in players.Values)
                            {
                                client.Send(new Message("You may attack now!", Color.Red, Message.Talk));
                                client.Send(new Message("The tournament ends at " + nextTournament.AddMinutes(10).Minute + "!", Color.Red, Message.Talk));
                            }
                        }
                    }
                    else
                    {
                        if (time64 >= nextTournament.AddMinutes(10))
                            checkLives();
                    }
                }
            }
        }
        private DateTime setDate(DateTime time64)
        {
            return new DateTime(time64.Year, time64.Month, time64.Day, hour, minute, 0);
        }
        public static KillTournament Select(GameState client, uint mapID)
        {
            DateTime now = DateTime.Now;
            foreach (var tournament in Server.Thread.Tournaments)
                if (tournament.map.BaseID == mapID)
                    if (tournament.onGoing)
                        if (now <= tournament.setDate(now).AddMinutes(10))
                            if (tournament.selectFunc(client))
                                return tournament;
            return null;
        }
        public bool OnGoing()
        {
            return onGoing;
        }
        public void Join(GameState client)
        {
            joinClient(client);
        }
        private void joinClient(GameState client)
        {
            if (!client.InQualifier() && client.Entity.PokerTable == 0 && client.PlayRouletteUID == 0 && client.Map.BaseID != 6001 && client.Map.BaseID != 6004 && client.Map.BaseID != 6000 && !client.Entity.Dead)
            {
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
                var coordinates = map.RandomCoordinates();
                client.Entity.Teleport(map.BaseID, map.ID, coordinates.Item1, coordinates.Item2);
                client.CantAttack = cantAttack;
                players.Add(client.Entity.UID, client);
                client.Entity.OnDeath = (pEntity) =>
                {
                    pEntity.Teleport(1002, 408, 330);
                    players.Remove(pEntity.UID);
                    checkLives();
                };
                client.OnDisconnect = (pClient) =>
                {
                    pClient.Entity.Teleport(1002, 408, 330);
                    players.Remove(pClient.Entity.UID);
                    checkLives();
                };
            }
        }
        private void checkLives()
        {
            if (players.Values.Count((o) => { return o.Entity.MapID == map.ID; }) <= 1)
            {
                var player = players.Values.FirstOrDefault((o) => { return o.Entity.MapID == map.ID; });
                if (player != null)
                {
                    player.Entity.Teleport(1002, 408, 330);
                    prize(player);
                    Kernel.SendWorldMessage(new Message(player.Entity.Name + " has won the " + name + "!", Color.Red, Message.Talk));
                    Kernel.SendWorldMessage(new Message(player.Entity.Name + " has won the " + name + "!", Color.Red, Message.Center));
                }
                players.Clear();
                announcement = false;
                onGoing = false;
            }
        }
        public static readonly List<ulong> ArenaHitList = new List<ulong>()
        {
           12080,
           1045,
           1046,
           12350,
           12930,
           12940, 
           10381, 
           11110,
           11980, 
           12170,          
           12770, 
           11005,
        };
    }
}