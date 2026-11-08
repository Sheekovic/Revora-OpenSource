// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Revora.Network;
using Revora.Network.GamePackets;

namespace Revora.Game
{
    internal static class PokerPolicy
    {
        // Owner decision: Poker and its tournament rewards remain disabled.
        internal static readonly bool Enabled = false;
        internal static bool HandleNpc(Client.GameState client, NpcRequest request)
        {
            if(Enabled || client==null || request==null)return false;
            uint id=client.ActiveNpc;
            if(id!=27838 && id!=269857 && id!=369857)return false;
            if(id==27838 && request.OptionID==3)return false; // Separate roulette service.
            ushort map=id==27838?(ushort)1002:id==269857?(ushort)8880:(ushort)8881;
            if(!Features.QuestInventory.Near(client,id,map))return true;
            if(request.OptionID==255)return true;
            if(id!=27838 && request.OptionID==1){client.Entity.Teleport(1002,301,288);return true;}
            var dialog=new Npcs(client);
            dialog.Text("The poker rooms are closed.");
            if(id==27838)dialog.Option("Visit the roulette room.",3);
            else dialog.Option("Return to Twin City.",1);
            dialog.Option("Farewell.",255);dialog.Send();return true;
        }
    }
    public unsafe class HandlePoker
    {
        public HandlePoker(byte[] packet, Client.GameState client)
        {
            if (!PokerPolicy.Enabled) return;
            if (packet == null) return;
            if (client == null) return;
            ushort Length = BitConverter.ToUInt16(packet, 0);
            ushort ID = BitConverter.ToUInt16(packet, 2);
            ushort ID2 = BitConverter.ToUInt16(packet, 4);
            switch (ID)
            {
                #region 2171 Join table
                case 2171:
                    {
                        Game.Entity MyChar = client.Entity;
                        uint TableId = BitConverter.ToUInt32(packet, 8);
                        uint PlayerId = BitConverter.ToUInt32(packet, 12);
                        byte Seat = packet[16]; byte Typ = packet[4];
                        switch (Typ)
                        {
                            case 0://join table
                                {
                                    if (Kernel.PokerTables.ContainsKey(TableId))
                                    {
                                        Game.PokerTable T = Kernel.PokerTables[TableId];
                                        if (T.Players.ContainsKey(client.Entity.UID)) T.RemovePlayer(MyChar.UID);
                                        if (T.FreeSeat(Seat))
                                        {
                                            T.AddNewPlayer(client.Entity, Seat, true);
                                            MyChar.PokerTable = T.Id;
                                            byte CurrentState = 1;
                                            if (!T.Players.ContainsKey(MyChar.UID))
                                                if (T.Watchers.ContainsKey(MyChar.UID)) CurrentState = T.Watchers[MyChar.UID].CurrentState;
                                            client.Send(Game.PokerPackets.PokerPlayerInfo(Seat, MyChar.UID, CurrentState, T.Nomber));
                                            foreach (Game.PokerPlayer P in T.Players.Values)
                                            {
                                                if (P.PlayerId == MyChar.UID) continue;
                                                client.Send(Game.PokerPackets.PokerPlayerInfo(P.Seat, P.PlayerId, P.CurrentState, T.Nomber));
                                                P.Send(Game.PokerPackets.PokerPlayerInfo(Seat, MyChar.UID, CurrentState, T.Nomber));
                                            }
                                            foreach (Game.PokerPlayer P in T.Watchers.Values)
                                            {
                                                if (P.PlayerId == MyChar.UID) continue;
                                                client.Send(Game.PokerPackets.PokerPlayerInfo(P.Seat, P.PlayerId, P.CurrentState, T.Nomber));
                                                P.Send(Game.PokerPackets.PokerPlayerInfo(Seat, MyChar.UID, CurrentState, T.Nomber));
                                            }
                                            if (T.Players.Count == 2 && T.Pot == 0) T.SetNewRound(10);
                                        }
                                    }
                                    break;
                                }
                            case 4://watch
                                {
                                    if (Kernel.PokerTables.ContainsKey(TableId))
                                    {
                                        Game.PokerTable T = Kernel.PokerTables[TableId];
                                        if (T.Players.ContainsKey(MyChar.UID) || T.Watchers.ContainsKey(MyChar.UID)) return;
                                        if (T.FreeSeat(Seat))
                                        {
                                            T.AddNewPlayer(MyChar, Seat, false);
                                            T.Watchers[MyChar.UID].CurrentState = 2;
                                            MyChar.PokerTable = T.Id;
                                            client.Send(Game.PokerPackets.PokerPlayerInfo(Seat, MyChar.UID, 2, T.Nomber));
                                            foreach (Game.PokerPlayer P in T.Players.Values)
                                            {
                                                if (P.PlayerId == MyChar.UID) continue;
                                                client.Send(Game.PokerPackets.PokerPlayerInfo(P.Seat, P.PlayerId, P.CurrentState, T.Nomber));
                                                P.Send(Game.PokerPackets.PokerPlayerInfo(Seat, MyChar.UID, 2, T.Nomber));
                                            }
                                            foreach (Game.PokerPlayer P in T.Watchers.Values)
                                            {
                                                if (P.PlayerId == MyChar.UID) continue;
                                                client.Send(Game.PokerPackets.PokerPlayerInfo(P.Seat, P.PlayerId, P.CurrentState, T.Nomber));
                                                P.Send(Game.PokerPackets.PokerPlayerInfo(Seat, MyChar.UID, 2, T.Nomber));
                                            }
                                        }
                                    }
                                    break;
                                }
                            default:
                                {
                                    break;
                                }
                        }
                        break;
                    }
                #endregion 2171 Join table
                #region 2093 Player move
                case 2093:
                    {
                        byte Typ = packet[6];
                        Game.Entity MyChar = client.Entity;
                        Game.PokerTable T = new Game.PokerTable();
                        if (Kernel.PokerTables.ContainsKey(MyChar.PokerTable))
                            T = MyChar.MyPokerTable;
                        else return;
                        switch (Typ)
                        {

                            default:
                                {
                                    T.NewPlayerMove(packet, MyChar.UID);
                                    break;
                                }
                        }
                        break;
                    }
                #endregion 2093 Player move
                #region 2096 Leave table
                case 2096:
                    {
                        Game.Entity MyChar = client.Entity;
                        if (MyChar.MyPokerTable == null) return;
                        if (MyChar.MyPokerTable.Players.ContainsKey(MyChar.UID) && MyChar.MyPokerTable.Pot > 1)
                        {
                            byte[] P = new byte[10];
                            P[6] = 4; P[9] = 200;
                            MyChar.MyPokerTable.NewPlayerMove(P, MyChar.UID);
                        }
                        else
                            MyChar.MyPokerTable.RemovePlayer(MyChar.UID);
                        client.Send(packet);
                        break;
                    }
                #endregion 2096 Leave table
                #region 2090
                case 2090:
                    {
                        byte Typ = packet[6];
                        Game.Entity MyChar = client.Entity;
                        switch (Typ)
                        {
                            case 1:
                                {
                                    if (MyChar.PokerTable > 0)
                                    {
                                        if (Kernel.PokerTables.ContainsKey(MyChar.PokerTable))
                                        {
                                            byte Seat = packet[8];
                                            Game.PokerTable T = MyChar.MyPokerTable;
                                            if (T.Players.ContainsKey(client.Entity.UID)) return;
                                            if (T.FreeSeat(Seat))
                                            {
                                                T.AddNewPlayer(MyChar, Seat, true);
                                                byte CurrentState = 1;
                                                if (!T.Players.ContainsKey(MyChar.UID))
                                                    if (T.Watchers.ContainsKey(MyChar.UID))
                                                    {
                                                        CurrentState = T.Watchers[MyChar.UID].CurrentState;
                                                        T.Watchers.Remove(MyChar.UID);
                                                    }
                                                client.Send(Game.PokerPackets.PokerPlayerInfo(Seat, MyChar.UID, CurrentState, T.Nomber));
                                                foreach (Game.PokerPlayer P in T.Players.Values)
                                                {
                                                    if (P.PlayerId == MyChar.UID) continue;
                                                    client.Send(Game.PokerPackets.PokerPlayerInfo(P.Seat, P.PlayerId, P.CurrentState, T.Nomber));
                                                    P.Send(Game.PokerPackets.PokerPlayerInfo(Seat, MyChar.UID, CurrentState, T.Nomber));
                                                }
                                                foreach (Game.PokerPlayer P in T.Watchers.Values)
                                                {
                                                    if (P.PlayerId == MyChar.UID) continue;
                                                    client.Send(Game.PokerPackets.PokerPlayerInfo(P.Seat, P.PlayerId, P.CurrentState, T.Nomber));
                                                    P.Send(Game.PokerPackets.PokerPlayerInfo(Seat, MyChar.UID, CurrentState, T.Nomber));
                                                }
                                                if (T.Players.Count == 2 && T.Pot == 0) T.SetNewRound(30);
                                            }
                                        }
                                    }
                                    break;
                                }
                            default:
                                {

                                    string D = "";
                                    for (int x = 0; x < packet.Length; x++)
                                        D += packet[x].ToString("X") + "  ";
                                    client.Send(new Message("Unknown type: " + ID + " with length " + packet.Length + " :- " + D, System.Drawing.Color.CadetBlue, Message.Talk));
                                    break;
                                }
                        }
                        break;
                    }
                #endregion 2090
            }
        }
    }
    public unsafe class PokerPackets
    {
        //2172
        public static byte[] PokerTable(PokerTable Table)
        {
            PacketBuilder P = new PacketBuilder(2172, 52 + (Table.Players.Count * 6));
            P.Long(Table.Id);
            P.Long(0);
            P.Long(0);
            P.Short(Table.X);//Table X coord
            P.Short(Table.Y);//Table Y Coord
            P.Long(7217967);//Fixed nomber don't know what it is
            P.Short(0);
            P.Long(Table.Nomber);//table nomber
            P.Int((Table.FreeBet ? 1 : 0));//Limited=0    Unlimited=1
            P.Int(0);
            P.Short(0);
            P.Long(Table.BetType);//table bet type 1=Silver 0=CPs
            P.Long(Table.MinLimit);
            P.Int((byte)Table.State);//table state   0=unopened  1=Pocket  2=flop 3=turn 4=river 5=showdown
            P.ULong(Table.Pot);//Pot
            P.Int(Table.Players.Count);//Players Count
            foreach (PokerPlayer Player in Table.Players.Values)
            {
                if (Player.MyChar == null) { P.Move(6); continue; }
                P.Long(Player.PlayerId);
                P.Int(Player.Seat);
                P.Int(Player.Connected ? 1 : 0);
            }
            return P.getFinal();
        }
        //2171
        public static byte[] PokerJoinAction(uint TableId, uint PlayerId, byte Req, byte Seat)
        {
            PacketBuilder P = new PacketBuilder(2171, 20);
            P.Long(Req);
            P.Long(TableId);
            P.Long(PlayerId);
            P.Long(Seat);
            return P.getFinal();
        }
        //2090
        public static byte[] PokerPlayerInfo(byte Seat, uint PlayerId, byte State, byte TableNo)
        {

            PacketBuilder P = new PacketBuilder(2090, 20);
            P.Short(1);
            P.Short(State);
            P.Long(Seat);
            P.Long(TableNo);
            P.Long(PlayerId);
            return P.getFinal();
        }
        //2091
        public static byte[] PokerCards1Card(PokerTable T)
        {
            PacketBuilder P = new PacketBuilder(2091, 44 + T.Players.Count * 8);
            P.Short(7);
            P.Short(4);
            P.Long(0);
            P.Long(0);
            P.Long(0);
            P.Long(0);
            P.Long(0);
            P.Short(0);
            P.Short(T.Players.Count);
            P.Long(T._StartingPlayer);
            P.Long(0);
            P.Long(0);
            foreach (PokerPlayer Pl in T.Players.Values)
            {
                foreach (PokerCard C in Pl.MyCards.Values)
                {
                    P.Short(C.Val);
                    P.Short((byte)C.Typ);
                }
                P.Long(Pl.PlayerId);
            }
            return P.getFinal();
        }
        public static byte[] PokerCards2Cards(PokerTable T, Dictionary<byte, PokerCard> Cards)
        {
            PacketBuilder P = new PacketBuilder(2091, 44 + T.Players.Count * 8);
            P.Long(0);
            P.Short(2);//packet type players cards
            foreach (PokerCard C in Cards.Values)
            {
                P.Short(C.Val);
            }
            P.Short(0);
            P.Long(0);
            foreach (PokerCard C in Cards.Values)
            {
                P.Short((byte)C.Typ);
            }
            P.Short(0);
            P.Long(0);
            P.Short(T.Players.Count);//Players count
            P.Long(T._StartingPlayer);
            P.Long(T.GetLastPlayer());
            P.Long(T.GetNextPlayer());//get next player
            foreach (PokerPlayer Pl in T.Players.Values)
            {
                P.Short(13);
                P.Short(4);
                P.Long(Pl.PlayerId);
            }
            return P.getFinal();
        }
        public static byte[] PokerTableCards(Dictionary<byte, PokerCard> Cards, PokerTable T, byte RoundStage)
        {
            PacketBuilder P = new PacketBuilder(2091, 44);
            P.Short(0);
            P.Short(RoundStage);//packet type table cards

            P.Short(Cards.Count);
            foreach (PokerCard C in Cards.Values)
            {
                P.Short(C.Val);
            }
            for (byte x = 0; x < 5 - Cards.Count; x++)
                P.Short(0);
            foreach (PokerCard C in Cards.Values)
            {
                P.Short((byte)C.Typ);
            }
            for (byte x = 0; x < 5 - Cards.Count; x++)
                P.Short(0);
            P.Short(0);
            P.Long(T._StartingPlayer);
            P.Long(T.GetLastPlayer());
            P.Long(T.GetNextPlayer());//get next player
            return P.getFinal();
        }
        //2092
        public static byte[] PokerPlayerTurn(uint Id1, uint LastBet, uint RequierdBet, byte Type, byte TimeDown)
        {
            PacketBuilder P = new PacketBuilder(2092, 20);
            P.Short(TimeDown);//timer count
            P.Short(Type);//
            P.Long(LastBet);//last bet
            P.Long(RequierdBet);//requierd bet
            P.Long(Id1);
            return P.getFinal();
        }
        //2093
        public static byte[] PokerPlayerMove(uint PlayerId, byte Typ, ulong Bet, ulong RequierdBet)
        {
            PacketBuilder P = new PacketBuilder(2093, 20);
            P.Short(0);
            P.Short(Typ);
            P.Long(Bet);
            P.Long(RequierdBet);
            P.Long(PlayerId);
            return P.getFinal();
        }
        //2094
        public static byte[] PokerShowAllCards(PokerTable T)
        {
            PacketBuilder P = new PacketBuilder(2094, 8 + T.Players.Count * 12);
            P.Short(0);//
            P.Short(T.Players.Count);
            foreach (PokerPlayer Pl in T.Players.Values)
            {
                byte Card1Val = 0, Card1Type = 0, Card2Val = 0, Card2Type = 0; byte Co = 0;
                foreach (PokerCard C in Pl.MyCards.Values)
                {
                    Co++;
                    if (Co == 1)
                    {
                        Card1Val = C.Val; Card1Type = (byte)C.Typ;
                    }
                    else if (Co == 2)
                    {
                        Card2Val = C.Val; Card2Type = (byte)C.Typ;
                    }
                }
                P.Short(Card1Val);
                P.Short(Card2Val);
                P.Short(Card1Type);
                P.Short(Card2Type);
                P.Long(Pl.PlayerId);
            }
            return P.getFinal();
        }
        //2095
        public static byte[] PokerRoundResult(PokerTable T, uint WinnerId, uint MoneyWins)
        {
            PacketBuilder P = new PacketBuilder(2095, 8 + T.Players.Count * 15);
            P.Short(20);//Timer
            P.Short(T.Players.Count);
            P.Int(0);
            P.Int(0);
            P.Int(0);
            P.Long(WinnerId);
            P.Long(MoneyWins);
            P.Long(0);
            foreach (PokerPlayer Pl in T.Players.Values)
            {
                try
                {
                    byte ContinuePlaying = 0;
                    if (Pl.PlayerId == WinnerId) continue;
                    if (T.BetType == 0)
                        if (Pl.MyChar.Money >= T.MinLimit * 10)
                            ContinuePlaying = 0;
                        else ContinuePlaying = 1;
                    else if (T.BetType == 1)
                        if (Pl.MyChar.ConquerPoints >= T.MinLimit * 10)
                            ContinuePlaying = 0;
                        else ContinuePlaying = 1;
                    if (ContinuePlaying == 0)
                        P.Int(0);
                    else
                    {
                        P.Int(1);
                        Pl.CurrentState = 2;
                    }
                    P.Int(255);
                    P.Int(0);
                    P.Long(Pl.PlayerId);
                    P.Long(0xffffffff - Pl.Bet);
                    P.Short(0xffff);
                    P.Short(0xffff);
                }
                catch
                {
                    P.Int(0);
                    P.Int(255);
                    P.Int(0);
                    P.Long(Pl.PlayerId);
                    P.Long(0xffffffff - Pl.Bet);
                    P.Short(0xffff);
                    P.Short(0xffff);
                }
            }
            return P.getFinal();
        }
        //2096
        public static byte[] PokerLeaveTable(uint Id1)
        {
            PacketBuilder P = new PacketBuilder(2096, 16);

            P.Long(1);//
            P.Long(0);
            P.Long(Id1);
            return P.getFinal();
        }
        //2098
        public static byte[] PokerTableState(PokerTable T, byte CountDown)
        {
            PacketBuilder P = new PacketBuilder(2098, 45 + T.Players.Count * 11);
            uint Id1 = 0, Id2 = 0, Id3 = 0, Id4 = 0, Id5 = 0, Id6 = 0; byte Counter = 0;
            foreach (PokerPlayer Pl in T.Players.Values)
            {
                Counter++;
                if (Counter == 1) Id1 = Pl.PlayerId;
                else if (Counter == 2) Id2 = Pl.PlayerId;
                else if (Counter == 3) Id3 = Pl.PlayerId;
                else if (Counter == 4) Id4 = Pl.PlayerId;
                else if (Counter == 5) Id5 = Pl.PlayerId;
                else if (Counter == 6) Id6 = Pl.PlayerId;
            }
            P.Short(0);
            P.Int(T.Players.Count);//Players Count
            P.Int(CountDown);//Time Count Down
            P.Int(0);
            P.Short(0);
            P.Long(0);
            P.Long(0);
            P.Long(Id1);
            P.Long(Id2);

            P.Long(Id3);
            P.Long(Id4);
            P.Long(Id5);
            P.Long(Id6);
            P.Short(0);
            foreach (PokerPlayer Pl in T.Players.Values)
            {
                P.Int(2);
                P.Int(4);
                P.Int(13);
                P.Int(0);
                P.Int(4);
                P.Int(13);
                P.Int(0);

                P.Long(Pl.PlayerId);
            }
            return P.getFinal();
        }
    }
    public unsafe class PokerTable
    {
        public uint Id = 0;
        public byte Nomber = 0;
        public uint Map = 0;
        public ushort X = 0;
        public ushort Y = 0;
        public uint MinLimit = 100000;
        public bool FreeBet = true;
        public byte _State = (byte)PokerTableState.UnOpened;
        public byte StartSeat = 0;
        public byte CurrentSeat = 0;
        public uint _StartingPlayer = 0;
        public byte RoundStage = 0;
        public uint RoundMaxBet = 0;
        public System.Timers.Timer RoundTimer = new System.Timers.Timer();
        public System.Timers.Timer MoveTimer = new System.Timers.Timer();
        public PokerTableState State
        {
            get
            {
                return (PokerTableState)_State;
            }
            set
            {
                _State = (byte)value;
            }
        }
        public ulong Pot2 = 0;
       public uint Pot = 0;
        public byte BetType = 1;//Silver=0 CPs=1;
        public Dictionary<uint, PokerPlayer> Players = new Dictionary<uint, PokerPlayer>(10);
        public Dictionary<byte, PokerPlayer> Seats
        {
            get
            {
                Dictionary<byte, PokerPlayer> Ses = new Dictionary<byte, PokerPlayer>(10);
                foreach (PokerPlayer P in Players.Values)
                {
                    if (P.CurrentState == 1) Ses.Add(P.Seat, P);
                }
                return Ses;
            }
        }
        public Dictionary<uint, PokerPlayer> Watchers = new Dictionary<uint, PokerPlayer>(10);
        public Dictionary<byte, PokerCard> Cards = new Dictionary<byte, PokerCard>(52);
        public Dictionary<byte, PokerCard> TableCards = new Dictionary<byte, PokerCard>(5);
        public uint PlayerBySeat(byte S)
        {
            uint I = 0;
            foreach (PokerPlayer P in Players.Values)
                if (P.Seat == S) I = P.PlayerId;
            return I;
        }
        public uint GetStartingPlayer()
        {
            if (Players.Count < 2) return 0;
            uint I = 0;
            StartSeat++;
            if (StartSeat > 9) StartSeat = 0;
            for (byte x = StartSeat; x < 10; x++)
            {
                I = PlayerBySeat(x);
                if (I > 0)
                {
                    StartSeat = x;
                    break;
                }
            }
            if (I == 0)
            {
                for (byte x = 0; x < StartSeat; x++)
                {
                    I = PlayerBySeat(x);
                    if (I > 0)
                    {
                        StartSeat = x;
                        break;
                    }
                }
            }
            return I;
        }
        public PokerPlayer StartingPlayer
        {
            get
            {
                if (Players.ContainsKey(_StartingPlayer))
                    return Players[_StartingPlayer];
                else return null;
            }
        }
        public uint GetNextPlayer()
        {
            if (Players.Count < 2) return 0;
            uint I = 0; byte StartSe = StartSeat;
            if (StartSe > 9) StartSe = 0;
            for (byte x = StartSe; x < 10; x++)
            {
                I = PlayerBySeat(x);
                if (I > 0)
                {
                    if (I == GetLastPlayer()) continue;
                    break;
                }
            }
            if (I == 0)
            {
                for (byte x = 0; x < StartSe; x++)
                {
                    I = PlayerBySeat(x);
                    if (I > 0)
                    {
                        if (I == GetLastPlayer()) continue;
                        break;
                    }
                }
            }
            return I;
        }
        public uint GetLastPlayer()
        {
            if (Players.Count < 2) return 0;
            uint I = 0; byte CurrentSeat = StartSeat;
            if (CurrentSeat < 1) CurrentSeat = 9;
            for (byte x = CurrentSeat; x > 0; x--)
            {
                I = PlayerBySeat(x);
                if (I > 0)
                {
                    break;
                }
            }
            if (I == 0)
            {
                if (PlayerBySeat(0) > 0) return PlayerBySeat(0);
                for (byte x = 9; x > StartSeat; x--)
                {
                    I = PlayerBySeat(x);
                    if (I > 0)
                    {
                        break;
                    }
                }
            }
            return I;
        }
        public void SetNewRound(byte CountDown)
        {
            Dictionary<uint, PokerPlayer> NotConnectedAnymore = new Dictionary<uint, PokerPlayer>();
            foreach (PokerPlayer P in Players.Values)
            {
                if (!P.Connected) NotConnectedAnymore.Add(P.PlayerId, P);
                else if (P.CurrentState == 2)
                    if (!Watchers.ContainsKey(P.PlayerId))
                        Watchers.Add(P.PlayerId, P);
            }
            foreach (PokerPlayer P in NotConnectedAnymore.Values)
                RemovePlayer(P.PlayerId);
            foreach (PokerPlayer P in Watchers.Values)
            {
                if (P.CurrentState == 3)
                {
                    if (!Players.ContainsKey(P.PlayerId))
                        Players.Add(P.PlayerId, P);
                }
                else if (P.CurrentState == 2)
                {
                    if (Players.ContainsKey(P.PlayerId))
                        Players.Remove(P.PlayerId);
                }
            }
            foreach (PokerPlayer P in Players.Values)
            {
                if (Watchers.ContainsKey(P.PlayerId))
                    Watchers.Remove(P.PlayerId);
                P.MyCards.Clear();
                P.CurrentState = 1;
                P.RoundState = 0;
                P.HandVals = "";
                P.Bet = MinLimit;
                if (BetType == 0)
                    P.MyChar.AdjustMoney(-(decimal)(MinLimit));
                else if (BetType == 1)
                {
                    if (P.MyChar.ConquerPoints >= MinLimit)
                        P.MyChar.TrySpendConquerPoints((uint)(MinLimit));
                    else P.MyChar.ConquerPoints = 0;
                }
            }
            Cards.Clear();
            TableCards.Clear();
            RoundStage = 0;
            for (byte Y = 0; Y < 4; Y++)
            {
                PokerCardsType T = PokerCardsType.Hearts;
                if (Y == 1) T = PokerCardsType.Spades;
                else if (Y == 2) T = PokerCardsType.Clubs;
                else if (Y == 3) T = PokerCardsType.Diamonds;
                for (byte x = 0; x < 13; x++)
                {
                    PokerCard Pc = new PokerCard();
                    Pc.Id = (byte)(x + (13 * Y));
                    Pc.Typ = T;
                    Pc.Val = x;
                    Cards.Add(Pc.Id, Pc);
                }
            }
            if (RoundTimer != null && RoundTimer.Enabled)
            {
                RoundTimer.Stop();
                RoundTimer.Dispose();
                RoundTimer = null;
                RoundTimer = new System.Timers.Timer();
            }
            else if (RoundTimer == null)
            {
                RoundTimer = new System.Timers.Timer();
            }
            RoundTimer.Interval = CountDown * 1000;
            RoundTimer.Elapsed += delegate
            {
                if (Players.Count > 1 && Pot < 1)
                {
                    DrawCards(1, false);
                    Pot = MinLimit;
                    RoundMaxBet = MinLimit;
                    _StartingPlayer = GetStartingPlayer();
                    SendToAll(PokerPackets.PokerCards1Card(this));
                    DrawCards(1, false);
                    foreach (PokerPlayer Pl in Players.Values)
                    {
                        Pl.Bet = MinLimit;
                        Pl.Send(PokerPackets.PokerCards2Cards(this, Pl.MyCards));
                    }
                    SendToAll(PokerPackets.PokerPlayerTurn(_StartingPlayer, MinLimit, MinLimit * 2, 22, 30));
                    StartMoveTimer(30, _StartingPlayer);
                    RoundTimer.Stop();
                    RoundTimer.Dispose();
                    RoundTimer = null;
                }
            };
            RoundTimer.Start();
            SendToAll(PokerPackets.PokerPlayerTurn(0, 0, 0, 0, CountDown));
            Data D = new Data(true);
            D.ID = 234;
            D.UID = Id;
            D.dwParam = (uint)(MinLimit * Players.Count);
            SendToAll(D.ToArray());
        }
        public void StartMoveTimer(byte CountDown, uint PlayerId)
        {
            if (MoveTimer != null && MoveTimer.Enabled)
            {
                MoveTimer.Stop();
                MoveTimer.Dispose();
                MoveTimer = null;
            }
            MoveTimer = new System.Timers.Timer();
            MoveTimer.Interval = CountDown * 1000;
            MoveTimer.Elapsed += delegate
            {
                byte[] FoldMe = new byte[10];
                FoldMe[6] = 4;
                NewPlayerMove(FoldMe, PlayerId);
                MoveTimer.Stop();
                MoveTimer.Dispose();
            };
            MoveTimer.Start();
        }
        public bool FreeSeat(byte Seat)
        {
            bool Free = true;
            foreach (PokerPlayer P in Players.Values)
            {
                if (P.Seat == Seat) Free = false;
            }
            foreach (PokerPlayer P in Watchers.Values)
            {
                if (P.CurrentState == 3)
                    if (P.Seat == Seat) Free = false;
            }
            return Free;
        }
        public void AddNewPlayer(Entity P, byte Seat, bool Player)
        {
            P.PokerTable = this.Id;
            PokerPlayer Pl = new PokerPlayer();
            Pl.PlayerId = P.UID;
            Pl.TableId = Id;
            Pl.Seat = Seat;
            if (Player)
            {
                if (Pot > 0) Pl.RoundState = 4;
                if (!Players.ContainsKey(Pl.PlayerId))
                    Players.Add(Pl.PlayerId, Pl);
                ToLocal(PokerPackets.PokerTable(this));
            }
            else
            {
                if (!Watchers.ContainsKey(Pl.PlayerId))
                    Watchers.Add(Pl.PlayerId, Pl);
            }
        }
        public void RemovePlayer(uint Id)
        {
            if (Players.ContainsKey(Id))
            {
                try
                {
                    lock (Players)
                    {
                        foreach (PokerPlayer P in Players.Values)
                        {
                            P.Send(PokerPackets.PokerLeaveTable(Id));
                        }
                    }
                    lock (Watchers)
                    {
                        foreach (PokerPlayer P in Watchers.Values)
                        {
                            P.Send(PokerPackets.PokerLeaveTable(Id));
                        }
                    }
                    if (Players[Id].MyChar != null)
                        Players[Id].MyChar.PokerTable = 0;
                    Players.Remove(Id);
                }
                catch { }
            }
            else if (Watchers.ContainsKey(Id))
            {
                lock (Players)
                {
                    foreach (PokerPlayer P in Players.Values)
                        P.Send(PokerPackets.PokerLeaveTable(Id));
                }
                lock (Watchers)
                {
                    foreach (PokerPlayer P in Watchers.Values)
                        P.Send(PokerPackets.PokerLeaveTable(Id));
                }
                if (Watchers[Id].MyChar != null)
                    Watchers[Id].MyChar.PokerTable = 0;
                Watchers.Remove(Id);
            }
            ToLocal(PokerPackets.PokerLeaveTable(Id));
            ToLocal(PokerPackets.PokerTable(this));
        }
        public void SendToAll(byte[] P)
        {
            foreach (PokerPlayer Player in Players.Values)
                Player.Send(P);
            foreach (PokerPlayer Player in Watchers.Values)
                Player.Send(P);
        }
        public PokerCard GetNewCard()
        {
            Random Rand = new Random();
            PokerCard PC = new PokerCard();
            int Rnd = Rand.Next(52);
            PC.Id = (byte)Rnd;
            while (!Cards.ContainsKey(PC.Id))
            {
                PC.Id = (byte)Rand.Next(52);
            }
            PC = Cards[PC.Id];
            return PC;
        }
        public void DrawCards(byte Count, bool Table)
        {
            try
            {
                if (!Table)
                {
                    for (byte x = 0; x < Count; x++)
                    {
                        foreach (PokerPlayer P in Players.Values)
                        {
                            if (!P.Connected) continue;
                            if (P.CurrentState > 1) continue;
                            PokerCard C = GetNewCard();
                            C.PlayerId = P.PlayerId;
                            P.MyCards.Add(C.Id, C);
                            if (Cards.ContainsKey(C.Id)) Cards.Remove(C.Id);
                        }
                    }
                    return;
                }
                byte Co = (byte)TableCards.Count;
                for (byte x = Co; x < (byte)(Count + Co); x++)
                {
                    PokerCard C = GetNewCard();
                    C.PlayerId = 0;
                    TableCards.Add(x, C);
                    if (Cards.ContainsKey(C.Id)) Cards.Remove(C.Id);
                }
            }
            catch (Exception xp) { Console.WriteLine(xp.ToString()); }
        }
        public void NewPlayerMove(byte[] P, uint PlayerId)
        {
            if (Pot == 0) return;
            try
            {
                if (Players.ContainsKey(PlayerId))
                {
                    if (MoveTimer != null && MoveTimer.Enabled)
                    {
                        MoveTimer.Stop();
                        MoveTimer.Dispose();
                        MoveTimer = null;
                    }
                    PokerPlayer Pl = Players[PlayerId];
                    byte Move = P[6]; byte CSeat = Pl.Seat;
                    Pl.RoundState = Move;
                    uint ReqPot = Pot;
                    switch (Move)
                    {
                        #region Rise // Call
                        case 2://call
                            {
                                Pot += MinLimit;
                                Pl.Bet += MinLimit;
                                if (Pl.Bet > RoundMaxBet) RoundMaxBet = Pl.Bet;
                                if (BetType == 0)
                                    Pl.MyChar.AdjustMoney(-(decimal)(MinLimit));
                                else if (BetType == 1)
                                {
                                    if (Pl.MyChar.ConquerPoints >= MinLimit)
                                        Pl.MyChar.TrySpendConquerPoints((uint)(MinLimit));
                                    else Pl.MyChar.ConquerPoints = 0;
                                }
                                Data D = new Data(true);
                                D.ID = 234;
                                D.UID = Id;
                                D.dwParam = Pot;
                                SendToAll(D.ToArray());
                                SendToAll(PokerPackets.PokerPlayerMove(PlayerId, Move, Pl.Bet, Pot));
                                break;
                            }
                        case 8:
                            {

                                break;
                            }
                        case 16://Rise
                            {
                                uint Botting = MinLimit + MinLimit;
                                Pot += Botting;
                                Pl.Bet += Botting;
                                if (Pl.Bet > RoundMaxBet) RoundMaxBet = Pl.Bet;
                                if (BetType == 0)
                                    Pl.MyChar.AdjustMoney(-(decimal)(Botting));
                                else if (BetType == 1)
                                {
                                    if (Pl.MyChar.ConquerPoints >= Botting)
                                        Pl.MyChar.TrySpendConquerPoints((uint)(Botting));
                                    else Pl.MyChar.ConquerPoints = 0;
                                }
                                Data D = new Data(true);
                                D.ID = 234;
                                D.UID = Id;
                                D.dwParam = Pot;
                                D.Data28_Uint = RoundMaxBet;
                                SendToAll(D.ToArray());
                                SendToAll(PokerPackets.PokerPlayerMove(PlayerId, Move, Pl.Bet, Pot));
                                break;
                            }
                        #endregion
                        #region Fold
                        case 4:
                            {
                                if (P[9] == 200)
                                {
                                    //RemoveThis = true;
                                    RemovePlayer(PlayerId);
                                }
                                else if (Players.ContainsKey(PlayerId))
                                {
                                    SendToAll(PokerPackets.PokerPlayerMove(PlayerId, Move, MinLimit, Pot));
                                    Players[PlayerId].MyCards.Clear();
                                    Players[PlayerId].RoundState = 4;
                                }
                                break;
                            }
                        default: SendToAll(P); break;
                        #endregion
                        #region AllIn
                        case 32:
                            {
                                ulong Betting = 0;
                                if (BetType == 0)
                                {
                                    Betting = Pl.MyChar.TakeAllMoney();
                                }
                                else if (BetType == 1)
                                {
                                    Betting = Pl.MyChar.ConquerPoints;
                                    Pl.MyChar.ConquerPoints = 0;
                                }
                                Pot2 = Betting;
                                Pl.Bet2 += Betting;
                                if (Pl.Bet > RoundMaxBet) RoundMaxBet = Pl.Bet;
                                Pl.RoundState = 4;
                                Data D = new Data(true);
                                D.ID = 234;
                                D.UID = Id;
                                //D.dwParam2 = Pot;
                                D.Data28_Uint = RoundMaxBet;
                                SendToAll(D.ToArray());
                                SendToAll(PokerPackets.PokerPlayerMove(PlayerId, Move,Pl.Bet2,Pot2));
                                break;
                            }
                        #endregion
                    }
                    uint NextPlayer = GetNextSeat(CSeat, true);
                    #region No More Players available
                    if (NextPlayer == Pl.PlayerId)
                    {
                        EndRound(NextPlayer);
                        return;
                    }
                    else if (Players.ContainsKey(NextPlayer) && NextPlayer == GetNextSeat(Players[NextPlayer].Seat, false))
                    {
                        EndRound(NextPlayer);
                        return;
                    }
                    #endregion
                    switch (RoundStage)
                    {
                        #region Send First 3 table cards
                        case 1:
                            {
                                if (TableCards.Count < 3)
                                {
                                    DrawCards(3, true);
                                    Dictionary<byte, PokerCard> TC = new Dictionary<byte, PokerCard>(3);
                                    TC.Add(0, TableCards[0]);
                                    TC.Add(1, TableCards[1]);
                                    TC.Add(2, TableCards[2]);
                                    SendToAll(PokerPackets.PokerTableCards(TC, this, 1));
                                }
                                break;
                            }
                        #endregion
                        #region Send Forth table card
                        case 2:
                            {
                                if (TableCards.Count < 4)
                                {
                                    DrawCards(1, true);
                                    Dictionary<byte, PokerCard> TC = new Dictionary<byte, PokerCard>(1);
                                    if (TableCards.ContainsKey(3))
                                        TC.Add(3, TableCards[3]);
                                    SendToAll(PokerPackets.PokerTableCards(TC, this, 2));
                                }
                                break;
                            }
                        #endregion
                        #region Send Fifth table cards
                        case 3:
                            {
                                if (TableCards.Count < 5)
                                {
                                    DrawCards(1, true);
                                    Dictionary<byte, PokerCard> TC = new Dictionary<byte, PokerCard>(1);
                                    if (TableCards.ContainsKey(4))
                                        TC.Add(4, TableCards[4]);
                                    SendToAll(PokerPackets.PokerTableCards(TC, this, 3));
                                }
                                break;
                            }
                        #endregion
                        case 4:
                            {
                                EndRound(0);
                                break;
                            }
                    }
                    if (RoundStage == 4) { return; }
                    else
                    {
                        uint PlayerBet = 0;
                        byte CallType = (byte)PokerCallTypes.Fold;
                        PokerPlayer Playr = null;
                        bool JustAllin = false;
                        if (Players.ContainsKey(NextPlayer))
                        {
                            PlayerBet = Players[NextPlayer].Bet;
                            Playr = Players[NextPlayer];
                            if (RoundMaxBet < PlayerBet)
                            {
                                CallType += (byte)PokerCallTypes.Check;
                            }
                            else CallType += (byte)PokerCallTypes.Call;
                            if (BetType == 0)
                            {
                                if (Playr.MyChar.Money < RoundMaxBet) JustAllin = true;
                                else if (Playr.MyChar.Money < MinLimit) JustAllin = true;
                                if (Playr.MyChar.Money >= MinLimit * 2) CallType += (byte)PokerCallTypes.Rise;
                            }
                            else if (BetType == 1)
                            {
                                if (Playr.MyChar.ConquerPoints < RoundMaxBet) JustAllin = true;
                                else if (Playr.MyChar.ConquerPoints < MinLimit) JustAllin = true;
                                if (Playr.MyChar.ConquerPoints >= MinLimit * 2) CallType += (byte)PokerCallTypes.Rise;
                            }

                        }
                        if (JustAllin) CallType = (byte)PokerCallTypes.AllIn;
                        SendToAll(PokerPackets.PokerPlayerTurn(NextPlayer, MinLimit, RoundMaxBet, CallType, 30));
                        StartMoveTimer(30, NextPlayer);
                    }
                }
            }
            catch (Exception xp)
            {
                Console.WriteLine(xp.ToString()); 
            }
        }
        public void EndRound(uint WinnerId)
        {
            SendToAll(PokerPackets.PokerPlayerTurn(0, 0, 0, 0, 30));
            try
            {
                if (MoveTimer != null || MoveTimer.Enabled)
                {
                    MoveTimer.Stop();
                    MoveTimer.Dispose();
                    MoveTimer = null;
                }
            }
            catch { }
            uint WinsVal = Pot - (Pot / 10);
            #region Check Winner
            if (WinnerId == 0)
            {
                ushort HighestPower = 0;
                byte HighestHandPower = 0;
                SendToAll(PokerPackets.PokerShowAllCards(this));
                foreach (PokerPlayer Pla in Players.Values)
                {
                    if (Pla.RoundState == 4 || !Pla.Connected) continue;
                    Dictionary<byte, PokerCard> TC = new Dictionary<byte, PokerCard>(7);
                    byte Counter = 5;
                    foreach (byte x in TableCards.Keys)
                    {
                        TC.Add(x, TableCards[x]);
                    }
                    foreach (byte x in Pla.MyCards.Keys)
                    {
                        TC.Add(Counter, Pla.MyCards[x]);
                        Counter++;
                    }
                    byte HP = GetHandPower(TC, Pla);
                    if (HP > HighestHandPower)
                    {
                        HighestHandPower = HP;
                        WinnerId = Pla.PlayerId;
                        HighestPower = Pla.GetFullPower(TC);
                    }
                    else if (HP == HighestHandPower)
                    {
                        if (Pla.GetFullPower(TC) > HighestPower)
                        {
                            WinnerId = Pla.PlayerId;
                            HighestPower = Pla.GetFullPower(TC);
                        }
                    }
                }
            }
            #endregion
            if (Players.ContainsKey(WinnerId))
            {
                if (BetType == 0)
                    Players[WinnerId].MyChar.AdjustMoney((decimal)(WinsVal));
                else if (BetType == 1)
                    Players[WinnerId].MyChar.AddConquerPoints(WinsVal);
            }
            SendToAll(PokerPackets.PokerRoundResult(this, WinnerId, WinsVal));
            Pot = 0;
            #region Start new round
            if (Players.Count < 2) return;
            else
            {
                Pot = 0;
                SetNewRound(20);
            }
            #endregion
        }
        public void ToLocal(byte[] P)
        {
            Client.GameState[] Locals = new Client.GameState[Server.GamePool.Length];
            Locals = Kernel.GamePool.Values.ToArray();
            foreach (Client.GameState client in Locals)
            {
                if (client != null)
                {
                    if (client.Map.ID == Map)
                    {
                        if (Kernel.GetDistance(client.Entity.X, client.Entity.Y, X, Y) > 25)
                        {
                            continue;
                        }
                        client.Send(P);
                    }

                }
            }
        }
        public uint GetNextSeat(byte Seat, bool Next)
        {
            try
            {
                Dictionary<byte, PokerPlayer> Ses = Seats;
                uint Id = 0;
                byte Se = (byte)(Seat + 1);
                bool Found = false;
                while (!Found)
                {
                    if (Ses.ContainsKey(Se))
                    {
                        if (Ses[Se].RoundState == 4 || !Ses[Se].Connected) { }
                        else
                        {
                            Found = true;
                            break;
                        }
                    }
                    Se++;
                    if (Se > 9) Se = 0;
                }
                Id = Ses[Se].PlayerId;
                if (Id == _StartingPlayer && Next) RoundStage++;
                return Id;
            }
            catch (Exception xp) { Console.WriteLine(xp.ToString()); return 0; }
        }
        public byte GetHandPower(Dictionary<byte, PokerCard> Hand, PokerPlayer Pl)
        {
            byte Power = 0; bool SameSuit = true;
            string Vals = ""; byte Suit = 5;
            byte Clubs = 0, Diamonds = 0, Hearts = 0, Spades = 0;
            Dictionary<byte, PokerCard> Ha = new Dictionary<byte, PokerCard>();
            #region Sort
            for (byte x = 0; x < Hand.Count; x++)
            {
                byte Val = 53; byte Inde = 100;
                foreach (byte C in Hand.Keys)
                {
                    if (Suit == 5) Suit = (byte)Hand[C].Typ;
                    else if (Suit != (byte)Hand[C].Typ) SameSuit = false;
                    if (Hand[C].Typ == PokerCardsType.Clubs) Clubs++;
                    else if (Hand[C].Typ == PokerCardsType.Diamonds) Diamonds++;
                    else if (Hand[C].Typ == PokerCardsType.Hearts) Hearts++;
                    else if (Hand[C].Typ == PokerCardsType.Spades) Spades++;
                    if (Hand[C].Val < Val && !Ha.ContainsKey(C))
                    {
                        Inde = C; Val = Hand[C].Val;
                    }
                }
                if (Hand.ContainsKey(Inde))
                {
                    Ha.Add(Inde, Hand[Inde]);
                }
            }
            #endregion
            foreach (byte x in Ha.Keys)
            {
                byte Val = Ha[x].Val;
                if (Val == 10) Vals += "A";
                else if (Val == 11) Vals += "B";
                else if (Val == 12) Vals += "C";
                else Vals += Val.ToString();
            }
            if (Clubs > 4 || Diamonds > 4 || Spades > 4 || Hearts > 4) SameSuit = true;
            Pl.HandVals = Vals;
            if (SameSuit && IsRoyal(Vals)) Power = (byte)PokerHandPower.RoyalFlush;
            else if (SameSuit && IsStraight(Vals)) Power = (byte)PokerHandPower.StraightFlush;
            else if (IsFourOfAKind(Vals)) Power = (byte)PokerHandPower.FourOfAKind;
            else if (IsThreeOfAKind(Pl))
            {
                if (IsPair(Pl)) Power = (byte)PokerHandPower.FullHouse;
            }
            else if (SameSuit) Power = (byte)PokerHandPower.Flush;
            else if (IsStraight(Vals)) Power = (byte)PokerHandPower.Straight;
            else Pl.HandVals = Vals;
            if (Power == 0)
            {
                if (IsPair(Pl))
                { if (IsPair(Pl)) Power = (byte)PokerHandPower.TwoPairs; }
                else Pl.HandVals = Vals;
                if (IsThreeOfAKind(Pl)) Power = (byte)PokerHandPower.ThreeOfAKind;
                else if (IsPair(Pl)) Power = (byte)PokerHandPower.Pair;
            }
            return Power;
        }
        public bool IsRoyal(string HandVals)
        {
            if (HandVals.Contains("89ABC")) return true;
            else return false;
        }
        public bool IsStraight(string HandVals)
        {
            bool Straight = false;
            string V = HandVals;
            if (V.Contains("01234") || V.Contains("12345") || V.Contains("23456")) Straight = true;
            else if (V.Contains("34567") || V.Contains("45678") || V.Contains("56789")) Straight = true;
            else if (V.Contains("6789A") || V.Contains("789AB") || V.Contains("89ABC")) Straight = true;
            else if (V.Contains("C0123")) Straight = true;
            return Straight;
        }
        public bool IsFourOfAKind(string HandVals)
        {
            bool Yes = false;
            string V = HandVals;
            if (V.Contains("0123") || V.Contains("1234") || V.Contains("2345")) Yes = true;
            else if (V.Contains("3456") || V.Contains("4567") || V.Contains("5678")) Yes = true;
            else if (V.Contains("6789") || V.Contains("789A") || V.Contains("89AB")) Yes = true;
            else if (V.Contains("9ABC") || V.Contains("C012")) Yes = true;
            else if (V.Contains("0000") || V.Contains("1111") || V.Contains("2222")) Yes = true;
            else if (V.Contains("3333") || V.Contains("4444") || V.Contains("5555")) Yes = true;
            else if (V.Contains("6666") || V.Contains("7777") || V.Contains("8888")) Yes = true;
            else if (V.Contains("9999") || V.Contains("AAAA") || V.Contains("BBBB")) Yes = true;
            else if (V.Contains("CCCC")) Yes = true;
            return Yes;
        }
        public bool IsThreeOfAKind(PokerPlayer Pl)
        {
            bool Yes = false;
            string V = Pl.HandVals;
            if (V.Contains("012") || V.Contains("123") || V.Contains("234")) Yes = true;
            else if (V.Contains("345") || V.Contains("456") || V.Contains("567")) Yes = true;
            else if (V.Contains("678") || V.Contains("789") || V.Contains("89A")) Yes = true;
            else if (V.Contains("9AB") || V.Contains("ABC") || V.Contains("C01")) Yes = true;
            else if (V.Contains("000") || V.Contains("111") || V.Contains("222")) Yes = true;
            else if (V.Contains("333") || V.Contains("444") || V.Contains("555")) Yes = true;
            else if (V.Contains("666") || V.Contains("777") || V.Contains("888")) Yes = true;
            else if (V.Contains("999") || V.Contains("AAA") || V.Contains("BBB")) Yes = true;
            else if (V.Contains("CCC")) Yes = true;
            if (V.Contains("CCC")) V = V.Replace("CCC", "");
            else if (V.Contains("BBB")) V = V.Replace("BBB", "");
            else if (V.Contains("AAA")) V = V.Replace("AAA", "");
            else if (V.Contains("999")) V = V.Replace("999", "");
            else if (V.Contains("888")) V = V.Replace("888", "");
            else if (V.Contains("777")) V = V.Replace("777", "");
            else if (V.Contains("666")) V = V.Replace("666", "");
            else if (V.Contains("555")) V = V.Replace("555", "");
            else if (V.Contains("444")) V = V.Replace("444", "");
            else if (V.Contains("333")) V = V.Replace("333", "");
            else if (V.Contains("222")) V = V.Replace("222", "");
            else if (V.Contains("111")) V = V.Replace("111", "");
            else if (V.Contains("000")) V = V.Replace("000", "");
            else if (V.Contains("ABC")) V = V.Replace("ABC", "");
            else if (V.Contains("C01")) V = V.Replace("C01", "");
            else if (V.Contains("9AB")) V = V.Replace("9AB", "");
            else if (V.Contains("89A")) V = V.Replace("89A", "");
            else if (V.Contains("789")) V = V.Replace("789", "");
            else if (V.Contains("678")) V = V.Replace("678", "");
            else if (V.Contains("567")) V = V.Replace("567", "");
            else if (V.Contains("456")) V = V.Replace("456", "");
            else if (V.Contains("345")) V = V.Replace("345", "");
            else if (V.Contains("234")) V = V.Replace("234", "");
            else if (V.Contains("123")) V = V.Replace("123", "");
            else if (V.Contains("012")) V = V.Replace("012", "");
            Pl.HandVals = V;
            return Yes;
        }
        public bool IsPair(PokerPlayer Pl)
        {
            bool Yes = false;
            string V = Pl.HandVals;
            if (V.Contains("01") || V.Contains("12") || V.Contains("23")) Yes = true;
            else if (V.Contains("34") || V.Contains("45") || V.Contains("56")) Yes = true;
            else if (V.Contains("67") || V.Contains("78") || V.Contains("89")) Yes = true;
            else if (V.Contains("9A") || V.Contains("AB") || V.Contains("BC")) Yes = true;
            else if (V.Contains("C0")) Yes = true;
            else if (V.Contains("00") || V.Contains("11") || V.Contains("22")) Yes = true;
            else if (V.Contains("33") || V.Contains("44") || V.Contains("55")) Yes = true;
            else if (V.Contains("66") || V.Contains("77") || V.Contains("88")) Yes = true;
            else if (V.Contains("99") || V.Contains("AA") || V.Contains("BB")) Yes = true;
            else if (V.Contains("CC")) Yes = true;
            if (V.Contains("CC")) V = V.Replace("CC", "");
            else if (V.Contains("BB")) V = V.Replace("BB", "");
            else if (V.Contains("AA")) V = V.Replace("AA", "");
            else if (V.Contains("99")) V = V.Replace("99", "");
            else if (V.Contains("88")) V = V.Replace("88", "");
            else if (V.Contains("77")) V = V.Replace("77", "");
            else if (V.Contains("66")) V = V.Replace("66", "");
            else if (V.Contains("55")) V = V.Replace("55", "");
            else if (V.Contains("44")) V = V.Replace("44", "");
            else if (V.Contains("33")) V = V.Replace("33", "");
            else if (V.Contains("22")) V = V.Replace("22", "");
            else if (V.Contains("11")) V = V.Replace("11", "");
            else if (V.Contains("00")) V = V.Replace("00", "");
            else if (V.Contains("BC")) V = V.Replace("BC", "");
            else if (V.Contains("AB")) V = V.Replace("AB", "");
            else if (V.Contains("C0")) V = V.Replace("C0", "");
            else if (V.Contains("9A")) V = V.Replace("9A", "");
            else if (V.Contains("89")) V = V.Replace("89", "");
            else if (V.Contains("78")) V = V.Replace("78", "");
            else if (V.Contains("67")) V = V.Replace("67", "");
            else if (V.Contains("56")) V = V.Replace("56", "");
            else if (V.Contains("45")) V = V.Replace("45", "");
            else if (V.Contains("34")) V = V.Replace("34", "");
            else if (V.Contains("23")) V = V.Replace("23", "");
            else if (V.Contains("12")) V = V.Replace("12", "");
            else if (V.Contains("01")) V = V.Replace("01", "");
            Pl.HandVals = V;
            return Yes;
        }
    }
    public unsafe class PokerPlayer
    {
        public uint TableId = 0;
        public uint PlayerId = 0;
        public bool Connected = true;
        public byte CurrentState = 1;
        public byte RoundState = 0;
        public uint Bet = 0;
        public ulong Bet2 = 0;
        public byte Seat = 0;
        public byte CardsPower = 0;
        public string HandVals = "";
        public System.Timers.Timer MoveTimer = new System.Timers.Timer();
        public ushort GetFullPower(Dictionary<byte, PokerCard> TCards)
        {
            ushort P = 0;
            foreach (PokerCard C in MyCards.Values)
            {
                P += (ushort)(C.Id);
            }
            return P;
        }
        public Dictionary<byte, PokerCard> MyCards = new Dictionary<byte, PokerCard>(5);
        public Entity MyChar
        {
            get
            {
                if (Kernel.GamePool.ContainsKey(PlayerId))
                    return Kernel.GamePool[PlayerId].Entity;
                else { Connected = false; return null; }
            }
            set
            {
                PlayerId = value.UID;
            }
        }
        public PokerTable MyTable
        {
            get
            {
                if (Kernel.PokerTables.ContainsKey(TableId)) return Kernel.PokerTables[TableId];
                else return null;
            }
            set
            {
                TableId = value.Id;
            }
        }
        public void Send(byte[] P)
        {
            if (!Connected)
            {
                if (Kernel.PokerTables.ContainsKey(TableId))
                {
                    if (Kernel.PokerTables[TableId].Players.ContainsKey(PlayerId))
                        Kernel.PokerTables[TableId].Players[PlayerId].RoundState = 4;
                    return;
                }
            }
            if (Kernel.GamePool.ContainsKey(PlayerId))
                Kernel.GamePool[PlayerId].Send(P);
            else
            {
                Connected = false;
                if (Kernel.PokerTables.ContainsKey(TableId))
                {
                    if (Kernel.PokerTables[TableId].Players.ContainsKey(PlayerId))
                        Kernel.PokerTables[TableId].Players[PlayerId].RoundState = 4;
                }
            }
        }
    }
    public unsafe class PokerCard
    {
        public byte Id = 0;
        public uint PlayerId;
        public byte Val = 0;
        public PokerCardsType Typ = PokerCardsType.Clubs;
    }
    public enum PokerTableState : byte
    {
        UnOpened = 0,
        Pocket = 1,
        Flop = 2,
        Turn = 3,
        River = 4,
        ShowDown = 5
    }
    public enum PokerJoinTableType : byte
    {
        Join = 0,
        Leave = 1,
        Watching = 2
    }
    public enum PokerCardsType : byte
    {
        Hearts = 0,
        Spades = 1,
        Clubs = 2,
        Diamonds = 3
    }
    public enum PokerHandPower : byte
    {
        RoyalFlush = 10,
        StraightFlush = 9,
        FourOfAKind = 8,
        FullHouse = 7,
        Flush = 6,
        Straight = 5,
        ThreeOfAKind = 4,
        TwoPairs = 3,
        Pair = 2,
        Nothing = 1 
    }
    public enum PokerCallTypes : byte
    {
        Bet = 1,
        Call = 2,
        Fold = 4,
        Check = 8,
        Rise = 16,
        AllIn = 32,
        CallFold = 6,
        CheckFold = 12,
        RiseCall = 18,
        RiseFold = 20,
        RiseCallFold = 22,
        RiseCheck = 24,
        AllInCall = 34,
        AllInFold = 36,
        AllInCallFold = 38,
    }
    public unsafe class PacketBuilder
    {
        protected byte[] _buffer = new byte[1024];
        protected int Position = 0;
        protected int Len = 0;
        protected byte[] TQ_SERVER = Encoding.Default.GetBytes("TQServer");
        public int GetPos()
        {
            return Position;
        }
        public void SetPosition(int Pos)
        {
            Position = Pos;
        }
        public PacketBuilder(int T, int L)
        {
            Len = L;
            Length(L);
            Type(T);
        }
        public void Short(int value)
        {
            _buffer[Position] = ((byte)(value & 0xff));
            Position++;
            _buffer[Position] = ((byte)((value >> 8) & 0xff));
            Position++;
        }
        public void Short(uint value)
        {
            _buffer[Position] = ((byte)(value & 0xff));
            Position++;
            _buffer[Position] = ((byte)((value >> 8) & 0xff));
            Position++;
        }
        public void Length(int value)
        {
            _buffer[Position] = ((byte)(value & 0xff));
            Position++;
            _buffer[Position] = ((byte)((value >> 8) & 0xff));
            Position++;
        }
        public void Type(int value)
        {
            _buffer[Position] = ((byte)(value & 0xff));
            Position++;
            _buffer[Position] = ((byte)((value >> 8) & 0xff));
            Position++;
        }
        public void Long(int value)
        {
            _buffer[Position] = ((byte)(value & 0xff));
            Position++;
            _buffer[Position] = ((byte)(value >> 8 & 0xff));
            Position++;
            _buffer[Position] = (byte)(value >> 16 & 0xff);
            Position++;
            _buffer[Position] = ((byte)(value >> 24 & 0xff));
            Position++;
        }
        public void Long(ulong value)
        {
            _buffer[Position] = ((byte)((ulong)value & 0xffL));
            Position++;
            _buffer[Position] = ((byte)(value >> 8 & 0xff));
            Position++;
            _buffer[Position] = (byte)(value >> 16 & 0xff);
            Position++;
            _buffer[Position] = ((byte)(value >> 24 & 0xff));
            Position++;
        }
        public void ULong(ulong value)
        {
            _buffer[Position] = (byte)(value);
            Position++;
            _buffer[Position] = (byte)(value >> 8);
            Position++;
            _buffer[Position] = (byte)(value >> 16);
            Position++;
            _buffer[Position] = (byte)(value >> 24);
            Position++;
            _buffer[Position] = (byte)(value >> 32);
            Position++;
            _buffer[Position] = (byte)(value >> 40);
            Position++;
            _buffer[Position] = (byte)(value >> 48);
            Position++;
            _buffer[Position] = (byte)(value >> 56);
            Position++;
        }
        public void Int(int value)
        {
            _buffer[Position] = (Convert.ToByte(value & 0xff));
            Position++;
        }
        public void Int(uint value)
        {
            _buffer[Position] = (Convert.ToByte(value & 0xff));
            Position++;
        }
        public void Long(uint value)
        {
            _buffer[Position] = ((byte)(value & 0xff));
            Position++;
            _buffer[Position] = ((byte)(value >> 8 & 0xff));
            Position++;
            _buffer[Position] = (byte)(value >> 16 & 0xff);
            Position++;
            _buffer[Position] = ((byte)(value >> 24 & 0xff));
            Position++;
        }
        public void Move(int value)
        {
            for (int x = 0; x < value; x++)
            {
                _buffer[Position] = 0;
                Position++;
            }
        }
        public void Text(string value)
        {
            byte[] nvalue = Encoding.Default.GetBytes(value);
            Array.Copy(nvalue, 0, _buffer, Position, nvalue.Length);
            Position += nvalue.Length;
        }
        protected void Seal()
        {
            Array.Copy(TQ_SERVER, 0, _buffer, Position, TQ_SERVER.Length);
            Position += TQ_SERVER.Length + 1;
            byte[] x = new byte[Position - 1];
            Array.Copy(_buffer, x, Position - 1);
            _buffer = new byte[x.Length];
            Array.Copy(x, _buffer, x.Length);
            x = null;
        }
        public byte[] getFinal()
        {
            Seal();
            return _buffer;
        }
        internal void Fill(int End)
        {
            for (int x = Position; x < End; x++)
                Int(0);
        }
        internal void PrintThis()
        {
            string Dat = "";
            for (int x = 0; x < Position; x++)
                Dat += _buffer[x].ToString("X") + " ";
            Console.WriteLine(Dat);
        }
        #region Add from offset
        public void Short(int value, int Offset)
        {
            _buffer[Offset] = ((byte)(value & 0xff));
            _buffer[Offset + 1] = ((byte)((value >> 8) & 0xff));
        }
        public void Short(uint value, int Offset)
        {
            _buffer[Offset] = ((byte)(value & 0xff));
            Offset++;
            _buffer[Offset] = ((byte)((value >> 8) & 0xff));
        }
        public void Length(int value, int Offset)
        {
            _buffer[Offset] = ((byte)(value & 0xff));
            Offset++;
            _buffer[Offset] = ((byte)((value >> 8) & 0xff));
        }
        public void Type(int value, int Offset)
        {
            _buffer[Offset] = ((byte)(value & 0xff));
            Offset++;
            _buffer[Offset] = ((byte)((value >> 8) & 0xff));
        }
        public void Long(int value, int Offset)
        {
            _buffer[Offset] = ((byte)(value & 0xff));
            Offset++;
            _buffer[Offset] = ((byte)(value >> 8 & 0xff));
            Offset++;
            _buffer[Offset] = (byte)(value >> 16 & 0xff);
            Offset++;
            _buffer[Offset] = ((byte)(value >> 24 & 0xff));
        }
        public void Long(ulong value, int Offset)
        {
            _buffer[Offset] = ((byte)((ulong)value & 0xffL));
            Offset++;
            _buffer[Offset] = ((byte)(value >> 8 & 0xff));
            Offset++;
            _buffer[Offset] = (byte)(value >> 16 & 0xff);
            Offset++;
            _buffer[Offset] = ((byte)(value >> 24 & 0xff));
        }
        public void ULong(ulong value, int Offset)
        {
            _buffer[Offset] = (byte)(value);
            Offset++;
            _buffer[Offset] = (byte)(value >> 8);
            Offset++;
            _buffer[Offset] = (byte)(value >> 16);
            Offset++;
            _buffer[Offset] = (byte)(value >> 24);
            Offset++;
            _buffer[Offset] = (byte)(value >> 32);
            Offset++;
            _buffer[Offset] = (byte)(value >> 40);
            Offset++;
            _buffer[Offset] = (byte)(value >> 48);
            Offset++;
            _buffer[Offset] = (byte)(value >> 56);
        }
        public void Int(int value, int Offset)
        {
            _buffer[Offset] = (Convert.ToByte(value & 0xff));
            Offset++;
        }
        public void Int(uint value, int Offset)
        {
            _buffer[Offset] = (Convert.ToByte(value & 0xff));
            Offset++;
        }
        public void Long(uint value, int Offset)
        {
            _buffer[Offset] = ((byte)(value & 0xff));
            Offset++;
            _buffer[Offset] = ((byte)(value >> 8 & 0xff));
            Offset++;
            _buffer[Offset] = (byte)(value >> 16 & 0xff);
            Offset++;
            _buffer[Offset] = ((byte)(value >> 24 & 0xff));
            Offset++;
        }
        #endregion
    }
}
