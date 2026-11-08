using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HeRo.Client;
using HeRo.Network.GamePackets;

namespace HeRo.Game
{
    public class ElitePKTournament
    {
        public const byte
            EPK_Lvl100Minus = 0,
            EPK_Lvl100To119 = 1,
            EPK_Lvl120To129 = 2,
            EPK_Lvl130Plus = 3;

        public static ElitePK[] Tournaments;
        public const ushort WaitingAreaID = 2068;
        public static bool TimersRegistered;

        public static void Create()
        {
            Tournaments = new ElitePK[4];
            for (int i = 0; i < 4; i++)
                Tournaments[i] = new ElitePK(i);
        }
        public static void GiveClientReward(Client.GameClient client)
        {
            DateTime timer = new DateTime();
            timer = DateTime.Now.AddDays(7);
            bool ReceiveHightRward = false;
            var BigRewaurdDictionary = Tournaments[EPK_Lvl130Plus].Top8;
            if (BigRewaurdDictionary != null)
            {
                sbyte client_rank = -1;
                for (ushort x = 0; x < BigRewaurdDictionary.Length; x++)
                {
                    if (BigRewaurdDictionary[x] != null)
                    {
                        var obj_client = BigRewaurdDictionary[x];
                        if (obj_client.UID == client.Entity.UID)
                            client_rank = (sbyte)x;
                    }
                }
                if (client_rank != -1)
                {
                    ReceiveHightRward = true;

                    if (client_rank == 0)
                    {
                        if (!client.Entity.Titles.ContainsKey(Network.GamePackets.TitlePacket.Titles.Elite_PK_ChampionHigh))
                            client.Entity.AddTopStatus((ulong)Network.GamePackets.TitlePacket.Titles.Elite_PK_ChampionHigh, timer, true);
                        client.Entity.AddConquerPoints(800000);
                        client.Send("Congratulations " + client.Entity.Name + ", You have recived ElitePKChamption High.");
                    }
                    else if (client_rank == 1)
                    {
                        if (!client.Entity.Titles.ContainsKey(Network.GamePackets.TitlePacket.Titles.Elite_PK_2nd_PlaceHigh))
                            client.Entity.AddTopStatus((ulong)Network.GamePackets.TitlePacket.Titles.Elite_PK_2nd_PlaceHigh, timer, true);
                        client.Entity.AddConquerPoints(700000);
                        client.Send("Congratulations " + client.Entity.Name + ", You have recived ElitePK2ndPlace High.");
                    }
                    else if (client_rank == 2)
                    {
                        if (!client.Entity.Titles.ContainsKey(Network.GamePackets.TitlePacket.Titles.Elite_PK_3rd_Place_High))
                            client.Entity.AddTopStatus((ulong)Network.GamePackets.TitlePacket.Titles.Elite_PK_3rd_Place_High, timer, true);
                        client.Entity.AddConquerPoints(600000);
                        client.Send("Congratulations " + client.Entity.Name + ", You have recived ElitePK3ndPlace High.");
                    }
                    else if (client_rank == 3)
                    {
                        if (!client.Entity.Titles.ContainsKey(Network.GamePackets.TitlePacket.Titles.Elite_PK_Top_8High))
                            client.Entity.AddTopStatus((ulong)Network.GamePackets.TitlePacket.Titles.Elite_PK_Top_8High, timer, true);
                        client.Send("Congratulations " + client.Entity.Name + ", You have recived ElitePKTopEight High.");
                        client.Entity.AddConquerPoints(500000);
                    }
                    else if (client_rank == 4)
                    {
                        if (!client.Entity.Titles.ContainsKey(Network.GamePackets.TitlePacket.Titles.Elite_PK_Top_8High))
                            client.Entity.AddTopStatus((ulong)Network.GamePackets.TitlePacket.Titles.Elite_PK_Top_8High, timer, true);
                        client.Send("Congratulations " + client.Entity.Name + ", You have recived ElitePKTopEight High.");
                        client.Entity.AddConquerPoints(400000);
                    }
                    else if (client_rank == 5)
                    {
                        if (!client.Entity.Titles.ContainsKey(Network.GamePackets.TitlePacket.Titles.Elite_PK_Top_8High))
                            client.Entity.AddTopStatus((ulong)Network.GamePackets.TitlePacket.Titles.Elite_PK_Top_8High, timer, true);
                        client.Send("Congratulations " + client.Entity.Name + ", You have recived ElitePKTopEight High.");
                        client.Entity.AddConquerPoints(300000);
                    }
                    else if (client_rank == 6)
                    {
                        if (!client.Entity.Titles.ContainsKey(Network.GamePackets.TitlePacket.Titles.Elite_PK_Top_8High))
                            client.Entity.AddTopStatus((ulong)Network.GamePackets.TitlePacket.Titles.Elite_PK_Top_8High, timer, true);
                        client.Send("Congratulations " + client.Entity.Name + ", You have recived ElitePKTopEight High.");
                        client.Entity.AddConquerPoints(200000);
                    }
                    else if (client_rank == 7)
                    {
                        if (!client.Entity.Titles.ContainsKey(Network.GamePackets.TitlePacket.Titles.Elite_PK_Top_8High))
                            client.Entity.AddTopStatus((ulong)Network.GamePackets.TitlePacket.Titles.Elite_PK_Top_8High, timer, true);
                        client.Send("Congratulations " + client.Entity.Name + ", You have recived ElitePKTopEight High.");
                        client.Entity.AddConquerPoints(100000);
                    }
                    else
                    {
                        if (!client.Entity.Titles.ContainsKey(Network.GamePackets.TitlePacket.Titles.Elite_PK_Top_8High))
                            client.Entity.AddTopStatus((ulong)Network.GamePackets.TitlePacket.Titles.Elite_PK_Top_8High, timer, true);
                        client.Send("Congratulations " + client.Entity.Name + ", You have recived ElitePKTopEight High.");
                    }
                }

            }
            if (!ReceiveHightRward)
            {
                sbyte client_rank = -1;
                for (byte i = 0; i < EPK_Lvl130Plus; i++)
                {
                    var ditionaryLow = Tournaments[i].Top8;
                    if (ditionaryLow != null)
                    {
                        for (byte x = 0; x < ditionaryLow.Length; x++)
                        {
                            if (ditionaryLow[x] != null)
                            {
                                var obj = ditionaryLow[x];
                                if (obj.UID == client.Entity.UID)
                                    client_rank = (sbyte)x;
                            }
                        }
                    }
                }
            }
        }
        public static bool SignUp(GameClient client)
        {
            DateTime Now = DateTime.Now;
            byte tournament = 0;
            if (client.Entity.Level >= 130)
                tournament = EPK_Lvl130Plus;
            else if (client.Entity.Level >= 120)
                tournament = EPK_Lvl120To129;
            else if (client.Entity.Level >= 100)
                tournament = EPK_Lvl100To119;
            else
                tournament = EPK_Lvl100Minus;

            tournament = EPK_Lvl130Plus;

            int extraTime = 3 - tournament;
            extraTime *= 2;
            if ((Now.Hour == ElitePK.EventTime && Now.Minute >= 55) || (Now.Hour == (ElitePK.EventTime + 1) && Now.Minute < extraTime))
            {
                Tournaments[tournament].SignUp(client);
                return true;
            }
            return false;
        }

        public static void RegisterTimers()
        {
            if (Tournaments == null) return;
            TimersRegistered = true;
            foreach (var epk in Tournaments)
                if (epk != null)
                    epk.SubscribeTimer();
        }
    }
}