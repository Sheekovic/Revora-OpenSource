// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Drawing;
using Revora.Database;
using System.Collections.Generic;
using Revora.Network.GamePackets;
using System.Collections.Concurrent;

namespace Revora.Roulette.Database
{
    public unsafe class Roulettes
    {
        public class NumberInformation
        {
            public List<byte> Red = new List<byte>() { 1, 3, 5, 7, 9, 12, 14, 16, 18, 19, 21, 23, 25, 27, 30, 32, 34, 36 };
            public List<byte> Black = new List<byte>() { 2, 4, 6, 8, 10, 11, 13, 15, 17, 20, 22, 24, 26, 28, 29, 31, 33, 35 };
            public List<byte> Line1 = new List<byte>() { 1, 4, 7, 10, 13, 16, 19, 22, 25, 28, 31, 34 };
            public List<byte> Line2 = new List<byte>() { 2, 5, 8, 11, 14, 17, 20, 23, 26, 29, 32, 35 };
            public List<byte> Line3 = new List<byte>() { 3, 6, 9, 12, 15, 18, 21, 24, 27, 30, 33, 36 };

            public bool IsOdd(byte Number)
            {
                if (Number == 0 || Number >= 37)
                    return false;
                return Number % 2 != 0;
            }
            public bool IsEven(byte Number)
            {
                if (Number == 0 || Number >= 37)
                    return false;
                return Number % 2 == 0;
            }
            public bool IsSmall(byte Number)
            {
                return Number >= 1 && Number <= 18;
            }
            public bool IsBig(byte Number)
            {
                return Number >= 19 && Number <= 36;
            }
            public bool IsFront(byte Number)
            {
                return Number >= 1 && Number <= 12;
            }
            public bool IsMiddle(byte Number)
            {
                return Number >= 13 && Number <= 24;
            }
            public bool IsBack(byte Number)
            {
                return Number >= 25 && Number <= 36;
            }
        }
        public class RouletteTable : NumberInformation
        {
            public Random Rand;
            public const uint MapID = 1858, MaxPlayerOnTable = 5, MaxTimerStamp = 30;
            public ushort TimerStamp = 0;
            public Time32 StampRound = new Time32();
            public byte LuckyNumber = 1;
            public class Member
            {
                public Client.GameState Owner;
                public uint MyCost
                {
                    get
                    {
                        long Cost = 0;
                        Cost += MyLuckNumber.Values.Sum(p => p.BetPrice);
                        Cost += MyLuckExtra.Values.Sum(p => p.BetPrice);
                        return (uint)Cost;
                    }
                }
                public MsgRouletteOpenGui.Color Color;
                public uint Betting = 0;
                public uint Winning = 0;
                public ConcurrentDictionary<byte, MsgRouletteCheck.Item> MyLuckNumber = new ConcurrentDictionary<byte, MsgRouletteCheck.Item>();
                public ConcurrentDictionary<byte, MsgRouletteCheck.Item> MyLuckExtra = new ConcurrentDictionary<byte, MsgRouletteCheck.Item>();
                public void ShareBetting(RouletteTable table)
                {
                    MsgRouletteShareBetting BetPacket = new MsgRouletteShareBetting(MyLuckNumber.Count + MyLuckExtra.Count, Color);
                    BetPacket.DisplayBettings(this);
                    table.SendPacketTable(BetPacket.GetArray());
                }
            }
            public ConcurrentDictionary<uint, Member> RegistredPlayers;
            public ConcurrentDictionary<uint, Client.GameState> ClientsWatch;
            public MsgRouletteTable SpawnPacket;
            public RouletteTable()
            {
                Rand = new System.Random();
                TimerStamp = (ushort)MaxTimerStamp;
                ClientsWatch = new ConcurrentDictionary<uint, Client.GameState>();
                RegistredPlayers = new ConcurrentDictionary<uint, Member>();
                SpawnPacket = MsgRouletteTable.Create();
                Thread.Subscribe(work, 1000);
            }
            public void AddWatch(Client.GameState client)
            {
                if (RegistredPlayers.Count == 0)
                {
                    client.Send(new Message("No Entity plays here.", Message.System));
                    return;
                }
                if (client.PlayRouletteUID != 0)
                {
                    client.Send(new Message("You are play another table. Please quit before you spectate this one.", Message.System));
                    return;
                }
                if (client.WatchRoulette != 0)
                {
                    client.Send(new Message("You are spectating another table. Please quit before you spectate this one.", Message.System));
                    return;
                }
                if (!ClientsWatch.ContainsKey(client.Entity.UID))
                {
                    client.WatchRoulette = SpawnPacket.UID;
                    ClientsWatch.TryAdd(client.Entity.UID, client);

                    MsgRouletteScreen ScreenPacket = MsgRouletteScreen.Create();
                    ScreenPacket.UID = client.Entity.UID;
                    SendPacketTable(ScreenPacket.ToArray());

                    MsgRouletteOpenGui GuiPacket = MsgRouletteOpenGui.Create();
                    GuiPacket.Type = SpawnPacket.MoneyType;
                    GuiPacket.TimerStamp = (byte)TimerStamp;
                    GuiPacket.PlayerColor = MsgRouletteOpenGui.Color.Watch;
                    GuiPacket.FinalizePacket(client, RegistredPlayers.Values.Where(p => p.Owner.Entity.UID != client.Entity.UID).ToArray());
                }
            }
            public void RemoveWatch(uint UID)
            {
                Client.GameState client;
                if (ClientsWatch.TryRemove(UID, out client))
                {
                    client.WatchRoulette = 0;
                    MsgRouletteSignUp QuitPacket = MsgRouletteSignUp.Create();
                    QuitPacket.UID = client.Entity.UID;
                    QuitPacket.Action = MsgRouletteSignUp.ActionJoin.Quit;
                    client.Send(QuitPacket);
                }
            }
            public void AddPlayer(Client.GameState client)
            {
                if (RegistredPlayers.Count >= MaxPlayerOnTable)
                {
                    client.Send(new Message("Sorry, this game table is full.", Message.System));
                    return;
                }
                if (client.PlayRouletteUID != 0)
                {
                    client.Send(new Message("You are play another table. Please quit before you playing this one.", Message.System));//, Message.MsgColor.red);
                    return;
                }
                if (!RegistredPlayers.ContainsKey(client.Entity.UID))
                {
                    client.PlayRouletteUID = SpawnPacket.UID;

                    client.OnDisconnect = p =>
                    {
                        Database.Roulettes.RouletteTable table;
                        if (p.PlayRouletteUID != 0)
                        {
                            if (Database.Roulettes.RoulettesPoll.TryGetValue(p.PlayRouletteUID, out table))
                            {
                                table.RemovePlayer(p);
                            }
                        }
                        else if (p.WatchRoulette != 0)
                        {
                            if (Database.Roulettes.RoulettesPoll.TryGetValue(p.WatchRoulette, out table))
                            {
                                table.RemoveWatch(p.Entity.UID);
                            }
                        }
                    };
                    Member Entity = new Member();
                    Entity.Owner = client;
                    GeneratePlayerColor(out Entity.Color);
                    RegistredPlayers.TryAdd(Entity.Owner.Entity.UID, Entity);
                    ApplyTime(Entity);
                    if (RegistredPlayers.Count > 1)
                    {
                        ShareTableBetting(client);

                        MsgRoulettedAddNewPlayer packet = MsgRoulettedAddNewPlayer.Create();
                        packet.UID = client.Entity.UID;
                        packet.Mesh = client.Entity.Mesh;
                        packet.Color = Entity.Color;
                        packet.Name = client.Entity.Name;
                        foreach (var user in RegistredPlayers.Values)
                        {
                            if (user.Owner.Entity.UID != client.Entity.UID)
                            {
                                user.Owner.Send(packet);
                            }
                        }
                        foreach (var user in ClientsWatch.Values)
                            user.Send(packet);
                    }
                }
            }
            public void ShareTableBetting(Client.GameState client)
            {
                foreach (var use in RegistredPlayers.Values)
                {
                    if (use.MyLuckExtra.Count != 0 || use.MyLuckNumber.Count != 0)
                    {
                        MsgRouletteShareBetting BetPacket = new MsgRouletteShareBetting(use.MyLuckNumber.Count + use.MyLuckExtra.Count, use.Color);
                        BetPacket.DisplayBettings(use);
                        client.Send(BetPacket.GetArray());
                    }
                }
            }
            public void RemovePlayer(Client.GameState client)
            {
                MsgRouletteSignUp QuitPacket = MsgRouletteSignUp.Create();
                QuitPacket.UID = client.Entity.UID;
                QuitPacket.Action = MsgRouletteSignUp.ActionJoin.Quit;
                SendPacketTable(QuitPacket.ToArray());
                Member Entity;
                if (RegistredPlayers.TryRemove(client.Entity.UID, out Entity))
                {
                    client.PlayRouletteUID = 0;
                    if (RegistredPlayers.Count == 0)
                    {
                        foreach (var user in ClientsWatch.Values)
                            RemoveWatch(user.Entity.UID);
                    }
                }
            }
            public void SendPacketTable(byte[] packet)
            {
                foreach (var Entity in RegistredPlayers.Values)
                    Entity.Owner.Send(packet);
                foreach (var user in ClientsWatch.Values)
                    user.Send(packet);
            }
            public bool ContainColor(MsgRouletteOpenGui.Color color)
            {
                foreach (var Entity in RegistredPlayers.Values)
                {
                    if (Entity.Color == color)
                        return true;
                }
                return false;
            }
            public void GeneratePlayerColor(out MsgRouletteOpenGui.Color color)
            {
                color = MsgRouletteOpenGui.Color.None;
                for (int x = 0; x < 5; x++)
                {
                    if (!ContainColor((MsgRouletteOpenGui.Color)x))
                    {
                        color = (MsgRouletteOpenGui.Color)x;
                        break;
                    }
                }
            }
            public void ApplyTime(Member Entity)
            {
                MsgRouletteOpenGui GuiPacket = MsgRouletteOpenGui.Create();
                GuiPacket.Type = SpawnPacket.MoneyType;
                GuiPacket.TimerStamp = (byte)TimerStamp;
                GuiPacket.PlayerColor = Entity.Color;
                if (RegistredPlayers.Count == 1)
                    GuiPacket.FinalizePacket(Entity.Owner, new Member[0]);
                else
                    GuiPacket.FinalizePacket(Entity.Owner, RegistredPlayers.Values.Where(p => p.Owner.Entity.UID != Entity.Owner.Entity.UID).ToArray());
            }
            public void ApplayNumberWinner(Client.GameState client)
            {
                MsgRouletteNoWinner Winner = MsgRouletteNoWinner.Create();
                Winner.Number = (byte)LuckyNumber;
                client.Send(Winner);
            }
            private bool Reset = false;
            public void work(int timer)
            {
                Time32 TimerNow = new Time32(timer);
                if (RegistredPlayers.Count > 0)
                {
                    if (TimerStamp == 0)
                    {
                        if (TimerNow > StampRound && Reset == true)
                        {
                            ResetRoulette();
                        }
                        else if (!Reset)
                        {
                            GenerateLuckyNumber();
                            Reset = true;
                            StampRound = TimerNow.AddSeconds(3);
                            foreach (var Entity in RegistredPlayers.Values)
                            {
                                ApplayNumberWinner(Entity.Owner);
                                GetRewrad(Entity);
                            }
                            foreach (var user in ClientsWatch.Values)
                            {
                                ApplayNumberWinner(user);
                            }
                        }
                    }
                    else
                        TimerStamp--;
                }
                else
                {
                    ResetRoulette();
                }
            }
            public void ResetRoulette()
            {
                foreach (var Entity in RegistredPlayers.Values)
                {
                    Entity.MyLuckExtra.Clear();
                    Entity.MyLuckNumber.Clear();
                }
                Reset = false;
                TimerStamp = (ushort)MaxTimerStamp;
            }
            public void CheckUpMember(Member Entity)
            {
                switch (SpawnPacket.MoneyType)
                {
                    case MsgRouletteTable.TableType.ConquerPoints:
                        {
                            uint Cost = Entity.MyCost;
                            if (Entity.Owner.Entity.ConquerPoints >= Cost)
                            {
                                Entity.Owner.Entity.TrySpendConquerPoints((uint)(Cost));
                            }
                            else
                            {
                                Entity.Owner.MessageBox("You do not have " + Cost.ToString() + " ConquerPoints with you.");
                                RegistredPlayers.TryRemove(Entity.Owner.Entity.UID, out Entity);
                                Entity.Owner.PlayRouletteUID = 0;
                            }
                            break;
                        }
                    case MsgRouletteTable.TableType.Money:
                        {
                            uint Cost = Entity.MyCost;
                            if (Entity.Owner.Entity.Money >= Cost)
                            {
                                Entity.Owner.Entity.AdjustMoney(-(decimal)(Cost));
                            }
                            else
                            {
                                Entity.Owner.MessageBox("You do not have " + Cost.ToString() + " silvers with you.");
                                RegistredPlayers.TryRemove(Entity.Owner.Entity.UID, out Entity);
                                Entity.Owner.PlayRouletteUID = 0;
                            }
                            break;
                        }
                }
            }
            public bool Rate(int value)
            {
                return value > Rand.Next() % 100;
            }
            public void GenerateLuckyNumber()
            {
                if (Rate(30))
                {
                    LuckyNumber = (byte)Rand.Next(0, 38);
                    return;
                }
                else if (Rate(20))
                {
                    LuckyNumber = 37;
                    return;
                }
                var Array = RegistredPlayers.Values.ToArray();
                uint Trying = 1000;
                do
                {
                    bool Contain = false;
                    foreach (var Entity in Array)
                    {
                        LuckyNumber = (byte)Rand.Next(0, 38);
                        if (Entity.MyLuckExtra.ContainsKey((byte)LuckyNumber))
                            Contain = true;
                    }
                    if (!Contain)
                        break;
                }
                while (Trying > 1);
            }
            public void GetRewrad(Member Member)
            {
                Member.Winning = Member.Betting = 0;

                CheckUpMember(Member);

                foreach (var item in Member.MyLuckNumber.Values)
                {
                    if (item.Number == LuckyNumber)
                    {
                        Member.Winning += (uint)(item.BetPrice * 36);
                        Member.Betting += item.BetPrice;
                    }
                }
                foreach (var item in Member.MyLuckExtra.Values)
                {
                    switch (item.Number)
                    {
                        case 152:
                            {
                                if (Black.Contains(LuckyNumber))
                                {
                                    Member.Winning += item.BetPrice * 2;
                                    Member.Betting += item.BetPrice;
                                }
                                break;
                            }
                        case 151:
                            {
                                if (Red.Contains(LuckyNumber))
                                {
                                    Member.Winning += item.BetPrice * 2;
                                    Member.Betting += item.BetPrice;
                                }
                                break;
                            }
                        case 112:
                            {
                                if (IsOdd(LuckyNumber))
                                {
                                    Member.Winning += item.BetPrice * 2;
                                    Member.Betting += item.BetPrice;
                                }
                                break;
                            }
                        case 111:
                            {
                                if (IsEven(LuckyNumber))
                                {
                                    Member.Winning += item.BetPrice * 2;
                                    Member.Betting += item.BetPrice;
                                }
                                break;
                            }
                        case 141:
                            {
                                if (IsSmall(LuckyNumber))
                                {
                                    Member.Winning += item.BetPrice * 2;
                                    Member.Betting += item.BetPrice;
                                }
                                break;
                            }
                        case 142:
                            {
                                if (IsBig(LuckyNumber))
                                {
                                    Member.Winning += item.BetPrice * 2;
                                    Member.Betting += item.BetPrice;
                                }
                                break;
                            }
                        case 121:
                            {
                                if (IsFront(LuckyNumber))
                                {
                                    Member.Winning += item.BetPrice * 3;
                                    Member.Betting += item.BetPrice;
                                }
                                break;
                            }
                        case 122:
                            {
                                if (IsMiddle(LuckyNumber))
                                {
                                    Member.Winning += item.BetPrice * 3;
                                    Member.Betting += item.BetPrice;
                                }
                                break;
                            }
                        case 123:
                            {
                                if (IsBack(LuckyNumber))
                                {
                                    Member.Winning += item.BetPrice * 3;
                                    Member.Betting += item.BetPrice;
                                }
                                break;
                            }
                        case 131:
                            {
                                if (Line1.Contains(LuckyNumber))
                                {
                                    Member.Winning += item.BetPrice * 3;
                                    Member.Betting += item.BetPrice;
                                }
                                break;
                            }
                        case 132:
                            {
                                if (Line2.Contains(LuckyNumber))
                                {
                                    Member.Winning += item.BetPrice * 3;
                                    Member.Betting += item.BetPrice;
                                }
                                break;
                            }
                        case 133:
                            {
                                if (Line3.Contains(LuckyNumber))
                                {
                                    Member.Winning += item.BetPrice * 3;
                                    Member.Betting += item.BetPrice;
                                }
                                break;
                            }
                    }
                }
                if (Member.Winning > 0)
                {
                    switch (SpawnPacket.MoneyType)
                    {
                        case MsgRouletteTable.TableType.Money:
                            {
                                Member.Owner.Entity.AdjustMoney((decimal)(Member.Winning));

                                break;
                            }
                        case MsgRouletteTable.TableType.ConquerPoints:
                            {
                                Member.Owner.Entity.AddConquerPoints(Member.Winning);

                                break;
                            }
                    }
                }
            }
        }
        public static Dictionary<uint, RouletteTable> RoulettesPoll = new Dictionary<uint, RouletteTable>();
        internal static void Load()
        {
            Console.WriteLine("Cargando Roulettes Tables");
            string[] baseText = File.ReadAllLines(Constants.DataHolderPath + "Slayer\\Roulettes.txt");
            if (baseText.Length <= 1) return;
            for (int x = 1; x < baseText.Length; x++)
            {
                //System.Console.Write("\b{0}", Cargando.NextChar());
                string[] line = baseText[x].Split(',');
                RouletteTable Roulette = new RouletteTable();
                Roulette.SpawnPacket.UID = uint.Parse(line[0]);
                Roulette.SpawnPacket.TableNumber = uint.Parse(line[1]);
                Roulette.SpawnPacket.MoneyType = (MsgRouletteTable.TableType)uint.Parse(line[2]);
                ushort MapID = ushort.Parse(line[3]);
                Roulette.SpawnPacket.X = ushort.Parse(line[4]);
                Roulette.SpawnPacket.Y = ushort.Parse(line[5]);
                Roulette.SpawnPacket.Mesh = ushort.Parse(line[6]);
                if (!RoulettesPoll.ContainsKey(Roulette.SpawnPacket.UID))
                    RoulettesPoll.Add(Roulette.SpawnPacket.UID, Roulette);
            }
            //Console.WriteLine("Done!");
        }
    }
}