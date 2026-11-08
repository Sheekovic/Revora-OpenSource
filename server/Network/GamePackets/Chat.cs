using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Interfaces;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Revora.Network.GamePackets
{
    public unsafe class Chat
    {
        public static void Loadseller()
        {
            StartSeller(1082408, 0);
            StartSeller(1082416, 1);
            StartSeller(1082409, 2);
            additems(614009, 500, 0);
            additems(614219, 500, 0);
            additems(614439, 500, 0);
            additems(616009, 500, 0);
            additems(616219, 500, 0);
            additems(616439, 500, 0);
            additems(1088002, 500, 0);
            additems(720828, 500, 0);
        }
        public static void additems(uint uid, uint cost, byte num)
        {
            uint UIDS = 0;
            if (num == 0)
                UIDS = 1082408;
            else if (num == 1)
                UIDS = 1082416;
            else if (num == 2)
                UIDS = 1082409;
            ItemUsage usage = new ItemUsage(true);
            usage.UID = uid;
            usage.dwParam = cost;
            usage.Action = (ItemUsage.Mode)29;
            BoothItem.AddItemOnBooth(usage, Kernel.GamePool[UIDS]);
        }
        public static void StartSeller(uint uid, byte num)
        {
            try
            {
                uint[] numArray = LoadEntityShopUIDs(0, uid);
                GameState client = new GameState((Network.Sockets.ClientWrapper)null);
                client.FakeLoad(numArray[0]);
                if ((int)num == 0)
                    client.Entity.Teleport((ushort)1036, (ushort)232, (ushort)217);
                else if ((int)num == 1)
                    client.Entity.Teleport((ushort)1036, (ushort)232, (ushort)213);
                else if ((int)num == 2)
                    client.Entity.Teleport((ushort)1036, (ushort)232, (ushort)209);
                Data data = new Data(true);
                data.ID = 111U;
                data.UID = client.Entity.UID;
                data.TimeStamp = Time32.Now;
                data.dwParam = 40079U;
                data.wParam1 = client.Entity.X;
                data.wParam2 = client.Entity.Y;
                client.Send((IPacket)data);
                client.Action = (byte)250;
                client.Booth = new Game.ConquerStructures.Booth(client, data);
                client.Send((IPacket)new Data(true)
                {
                    ID = 81,
                    UID = client.Entity.UID,
                    dwParam = 0
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine((object)ex);
            }
        }
        public static uint[] LoadEntityShopUIDs(int lim, uint uid)
        {
            uint[] array = new uint[1];
            string query = "select * from entities where UID = " + uid + "";
            using (var cmd = new Database.MySqlCommand(Database.MySqlCommandType.SELECT))
            {
                cmd.Command = query;
                using (var reader = new Database.MySqlReader(cmd))
                {
                    reader.Read();
                    array[0] = reader.ReadUInt32("UID");
                }
            }
            return array;
        }

        public static uint[] LoadEntityUID(int lim)
        {
            uint[] array = new uint[lim];
            string query = "select * from entities limit " + lim;
            using (var cmd = new Database.MySqlCommand(Database.MySqlCommandType.SELECT))
            {
                cmd.Command = query;
                using (var reader = new Database.MySqlReader(cmd))
                {
                    for (int i = 0; i < lim; i++)
                    {
                        reader.Read();
                        array[i] = reader.ReadUInt32("UID");
                    }
                }
            }
            return array;
        }
        public static void WorldMessage(string message)
        {
            Message msg = new Message(message, System.Drawing.Color.MediumBlue, Message.Center);
            foreach (GameState pClient in Server.GamePool)
                pClient.Send(msg);
        }
        private static void SendRankingQuery(GenericRanking ranking, GameState client, uint type, uint rank,
            uint points)
        {
            if (rank != 0)
            {
                ranking.RankingType = type;
                ranking.Append(rank, points, client.Entity.UID, client.Entity.Name);
                client.Send(ranking);
                ranking.Reset();
            }
        }
        public static void HandleClipboardPacket(string cmd)
        {
            string[] pData = cmd.Split(' ');
            long off = 0, type = 0, val = 0;
            if (pData.Length > 1)
            {

                string[] oData = pData[1].Split(':');
                if (oData.Length == 3)
                {
                    off = long.Parse(oData[0]);
                    type = long.Parse(oData[1]);
                    if (oData[2] == "u")
                        val = 1337;
                    else
                        val = long.Parse(oData[2]);
                }
            }
            string Data = OSClipboard.GetText();

            string[] num = Data.Split(new[] { " " }, StringSplitOptions.RemoveEmptyEntries);
            byte[] packet = new byte[num.Length + 8];
            for (int i = 0; i < num.Length; i++)
                packet[i] = byte.Parse(num[i], System.Globalization.NumberStyles.HexNumber);
            Writer.WriteUInt16((ushort)(packet.Length - 8), 0, packet);
            if (off != 0)
            {
                switch (type)
                {
                    case 1:
                        {
                            packet[(int)off] = (byte)val;
                            break;
                        }
                    case 2:
                        {
                            Writer.WriteUInt16((ushort)val, (int)off, packet);
                            break;
                        }
                    case 4:
                        {
                            Writer.WriteUInt32((uint)val, (int)off, packet);
                            break;
                        }
                    case 8:
                        {
                            Writer.WriteUInt64((ulong)val, (int)off, packet);
                            break;
                        }
                }
            }
            foreach (var client in Server.GamePool)
            {
                if (val == 1337 && type == 4)
                    Writer.WriteUInt32(client.Entity.UID, (int)off, packet);
                client.Send(packet);
            }
        }
        public static void SendCursed(uint Time, GameState client)
        {
            client.Entity.Cursed = Time32.Now;
            FlagsUpdate update = new FlagsUpdate(true);
            update.UID = client.Entity.UID;
            update.Append((byte)FlagsUpdate.DataType.CursedTimer, Time);
            client.Send(update.ToArray());
            client.Entity.AddFlag((ulong)FlagsUpdate.Flags.Cursed, (int)Time, false, 1);
            client.Send("You have killed many players. This is a warning for you to stop.");
        }
        public static void Chats(Message message, GameState client)
        {
            message._From = client.Entity.Name;
            if (!CheckCommand(message.__Message, client))
            {
                    if (message.ChatType != Message.Service)
                    {
                        if (client.ChatBanned)
                            if (DateTime.Now > client.ChatBanTime.AddMinutes(client.ChatBanLasts))
                                client.ChatBanned = false;
                            else
                            {
                                int minutes = (int)new TimeSpan((client.ChatBanTime.AddMinutes(client.ChatBanLasts) - DateTime.Now).Ticks).TotalMinutes;
                                client.Send(new Message("You are banned from chat. You have to wait: " + minutes + "minutes before you can speak again!", System.Drawing.Color.Green, Message.Talk));
                                return;
                            }
                    }
                    switch (message.ChatType)
                    {
                        case Message.HawkMessage:
                            {
                                if (client.Booth != null)
                                {
                                    client.Booth.HawkMessage = message;
                                    client.SendScreen(message, true);
                                }
                                break;
                            }
                        case Message.Talk:
                            {
                                client.SendScreen(message, false);
                                break;
                            }
                        case Message.Whisper:
                            {
                                if (Revora.Game.Features.LivingWorldSystem.TryHandleWhisper(client, message))
                                    return;
                                if (Revora.Game.Features.CityGuardSystem.TryHandleWhisper(client, message))
                                    return;
                                foreach (GameState pClient in Kernel.GamePool.Values)
                                {
                                    if (pClient.Entity.Name == message._To)
                                    {
                                        message.Mesh = client.Entity.Mesh;
                                        pClient.Send(message);
                                        return;
                                    }
                                }
                                foreach (Game.ConquerStructures.Society.Friend friend in client.Friends.Values)
                                {
                                    if (friend.Name == message._To)
                                    {
                                        message.__Message = message.__Message.Replace("'", "¹");
                                        client.OnMessageBoxEventParams = new object[3];
                                        client.OnMessageBoxEventParams[0] = client.Entity.UID;
                                        client.OnMessageBoxEventParams[1] = friend.ID;
                                        client.OnMessageBoxEventParams[2] = Console.TimeStamp() + message.__Message;
                                        client.MessageOK =
                                            delegate
                                            {
                                                Database.KnownPersons.UpdateMessageOnFriend(Convert.ToUInt32(client.OnMessageBoxEventParams[0]), Convert.ToUInt32(client.OnMessageBoxEventParams[1]), Convert.ToString(client.OnMessageBoxEventParams[2]));
                                                client.Send(new Message("Message sent!", System.Drawing.Color.Green, Message.TopLeft));
                                            };
                                        client.MessageCancel =
                                            delegate
                                            {
                                                client.OnMessageBoxEventParams = new object[0];
                                            };
                                        client.Send(new NpcReply(NpcReply.MessageBox, "For " + friend.Name + ": \r\n" + message.__Message + "\r\n\r\nSend? (It will replace other messages.)"));
                                        return;
                                    }
                                }
                                client.Send(new Message("The player is not connected.", System.Drawing.Color.Orange, Message.System));
                                break;
                            }
                        case Message.Service:
                            {
                                foreach (GameState pClient in Server.GamePool)
                                {
                                    if (pClient.Account.State == Database.AccountTable.AccountState.Administrador)
                                    {
                                        message.ChatType = Message.Talk;
                                        string _Message = "Service-> " + client.Entity.Name + " He needs your help. Answer him / her now!!!";
                                        message.__Message = _Message;
                                        pClient.Send(message);
                                        return;
                                    }
                                }
                                break;
                            }
                        case Message.World:
                            {
                                if (client.Entity.Level >= 70 || client.Entity.Reborn != 0)
                                {
                                    foreach (GameState pClient in Server.GamePool)
                                    {
                                        if (pClient.Entity.UID != client.Entity.UID)
                                            pClient.Send(message);
                                    }
                                }
                                break;
                            }
                        case Message.Guild:
                            {
                                if (client.Guild != null)
                                    client.Guild.SendGuildMessage(message);
                                break;
                            }
                        case Message.Team:
                            {
                                if (client.Team != null)
                                {
                                    foreach (GameState Client in client.Team.Teammates)
                                    {
                                        if (client.Entity.UID != Client.Entity.UID)
                                            Client.Send(message);
                                    }
                                }
                                break;
                            }
                        case Message.Friend:
                            {
                                foreach (Game.ConquerStructures.Society.Friend friend in client.Friends.Values)
                                {
                                    if (friend.IsOnline)
                                        friend.Client.Send(message);
                                }
                                break;
                            }
                        //case Message.CrossServer:
                        //    {
                        //        if (client.Inventory.Contains(3002218, 1))
                        //        {
                        //            client.Inventory.Remove(3002218, 1);
                        //        }
                        //        break;
                        //    }
                        case (uint)MessageBoard.Channel.MsgTrade:
                            {
                                Message.MessageBoard.MessageInfo Info = Message.MessageBoard.GetMsgInfoByAuthor(client.Entity.Name, (ushort)message.ChatType);
                                Message.MessageBoard.Delete(Info, (ushort)message.ChatType);
                                Message.MessageBoard.Add(client.Entity.Name, message.__Message, (ushort)message.ChatType);
                                break;
                            }
                        case (uint)MessageBoard.Channel.MsgFriend:
                            {
                                Message.MessageBoard.MessageInfo Info = Message.MessageBoard.GetMsgInfoByAuthor(client.Entity.Name, (ushort)message.ChatType);
                                Message.MessageBoard.Delete(Info, (ushort)message.ChatType);
                                Message.MessageBoard.Add(client.Entity.Name, message.__Message, (ushort)message.ChatType);
                                break;
                            }
                        case (uint)MessageBoard.Channel.MsgTeam:
                            {
                                Message.MessageBoard.MessageInfo Info = Message.MessageBoard.GetMsgInfoByAuthor(client.Entity.Name, (ushort)message.ChatType);
                                Message.MessageBoard.Delete(Info, (ushort)message.ChatType);
                                Message.MessageBoard.Add(client.Entity.Name, message.__Message, (ushort)message.ChatType);
                                break;
                            }
                        case (uint)MessageBoard.Channel.MsgSyn:
                            {
                                Message.MessageBoard.MessageInfo Info = Message.MessageBoard.GetMsgInfoByAuthor(client.Entity.Name, (ushort)message.ChatType);
                                Message.MessageBoard.Delete(Info, (ushort)message.ChatType);
                                Message.MessageBoard.Add(client.Entity.Name, message.__Message, (ushort)message.ChatType);
                                break;
                            }
                        case (uint)MessageBoard.Channel.MsgOther:
                            {
                                Message.MessageBoard.MessageInfo Info = Message.MessageBoard.GetMsgInfoByAuthor(client.Entity.Name, (ushort)message.ChatType);
                                Message.MessageBoard.Delete(Info, (ushort)message.ChatType);
                                Message.MessageBoard.Add(client.Entity.Name, message.__Message, (ushort)message.ChatType);
                                break;
                            }
                        default:
                            {
                                client.SendScreen(message, !client.Entity.Dead);
                                break;
                            }

                    }
                }
            }
        public static bool CheckCommand15(Revora.Network.GamePackets.Message message, Client.GameState client)
        {
            try
            {
                if (message.__Message.StartsWith("/"))
                {
                    string Message = message.__Message.Substring(1).ToLower();
                    string Mess = message.__Message.Substring(1);
                    string[] Data = Message.Split(' ');
                    if (client.Account.State >= 0)
                    {
                        client.Account.SaveState();
                        client.Disconnect();
                    }
                }
                if (message.__Message.StartsWith("@"))
                {
                    string Message = message.__Message.Substring(1).ToLower();
                    string Mess = message.__Message.Substring(1);
                    string[] Data = Message.Split(' ');
                    #region LOL-Z
                    //if (client.Account.State ==  Database.AccountTable.AccountState.Player)
                    {
                        switch (Data[0])
                        {
                                
                            #region Comandos Chi
                            /*
                            case "chi1"://@chifixed 1 
                                {
                                    var Mode = int.Parse(Data[1]);
                                    var pos = (int)(int.Parse(Data[2]) - 1);
                                    var powers = client.ChiPowers[Mode - 1];
                                    var attributes = powers.Attributes;
                                    attributes[pos].Type = (Enums.ChiAttribute)ushort.Parse(Data[3]);
                                    attributes[pos].Value = (ushort)Game.Enums.ChiMaxValues(attributes[pos].Type);
                                    powers.CalculatePoints();
                                    Database.ChiTable.Sort((Enums.ChiPowerType)Mode);
                                    powers.Power = (Enums.ChiPowerType)Mode;
                                    client.Send(new ChiPowers(true).Query(client));
                                    #region Comandos Chi
                                    Database.ChiTable.ChiData[] array = null;
                                    switch ((Enums.ChiPowerType)Mode)
                                    {
                                        case Game.Enums.ChiPowerType.Dragon:
                                            array = Database.ChiTable.Dragon;
                                            break;

                                        case Game.Enums.ChiPowerType.Phoenix:
                                            array = Database.ChiTable.Phoenix;
                                            break;

                                        case Game.Enums.ChiPowerType.Tiger:
                                            array = Database.ChiTable.Tiger;
                                            break;

                                        case Game.Enums.ChiPowerType.Turtle:
                                            array = Database.ChiTable.Turtle;
                                            break;
                                    }
                                    foreach (var chiData in array)
                                    {
                                        if (Kernel.GamePool.ContainsKey(chiData.UID))
                                        {
                                            var pClient = Kernel.GamePool[chiData.UID];
                                            if (pClient == null) continue;
                                            if (pClient.ChiData == null) continue;
                                            SendRankingQuery(new GenericRanking(true) { Mode = GenericRanking.QueryCount }, pClient, GenericRanking.Chi + (uint)Mode, pClient.ChiData.SelectRank((Enums.ChiPowerType)Mode), pClient.ChiData.SelectPoints((Enums.ChiPowerType)Mode));
                                            if (pClient.Player.UID == client.Player.UID || pClient.ChiData.SelectRank((Enums.ChiPowerType)Mode) < 50)
                                                pClient.LoadItemStats();
                                        }
                                    }
                                    #endregion
                                    Database.ChiTable.Save(client);
                                }
                                break;
                            case "chi2":
                                {
                                    foreach (var Client in Kernel.GamePool.Values)
                                    {
                                        if (Client.Player.LoweredName.Contains(Data[1]))
                                        {
                                            var Mode = int.Parse(Data[2]);
                                            var pos = (int)(int.Parse(Data[3]) - 1);
                                            var powers = Client.ChiPowers[Mode - 1];
                                            var attributes = powers.Attributes;
                                            attributes[pos].Type = (Enums.ChiAttribute)ushort.Parse(Data[4]);
                                            attributes[pos].Value = (ushort)Game.Enums.ChiMaxValues(attributes[pos].Type);
                                            powers.CalculatePoints();
                                            Database.ChiTable.Sort((Enums.ChiPowerType)Mode);
                                            powers.Power = (Enums.ChiPowerType)Mode;
                                            Client.Send(new ChiPowers(true).Query(Client));
                                            #region Comandos Chi
                                            Database.ChiTable.ChiData[] array = null;
                                            switch ((Enums.ChiPowerType)Mode)
                                            {
                                                case Game.Enums.ChiPowerType.Dragon:
                                                    array = Database.ChiTable.Dragon;
                                                    break;

                                                case Game.Enums.ChiPowerType.Phoenix:
                                                    array = Database.ChiTable.Phoenix;
                                                    break;

                                                case Game.Enums.ChiPowerType.Tiger:
                                                    array = Database.ChiTable.Tiger;
                                                    break;

                                                case Game.Enums.ChiPowerType.Turtle:
                                                    array = Database.ChiTable.Turtle;
                                                    break;
                                            }
                                            foreach (var chiData in array)
                                            {
                                                if (Kernel.GamePool.ContainsKey(chiData.UID))
                                                {
                                                    var pClient = Kernel.GamePool[chiData.UID];
                                                    if (pClient == null) continue;
                                                    if (pClient.ChiData == null) continue;
                                                    SendRankingQuery(new GenericRanking(true) { Mode = GenericRanking.QueryCount }, pClient, GenericRanking.Chi + (uint)Mode, pClient.ChiData.SelectRank((Enums.ChiPowerType)Mode), pClient.ChiData.SelectPoints((Enums.ChiPowerType)Mode));
                                                    if (pClient.Player.UID == Client.Player.UID || pClient.ChiData.SelectRank((Enums.ChiPowerType)Mode) < 50)
                                                        pClient.LoadItemStats();
                                                }
                                            }
                                            #endregion
                                            Database.ChiTable.Save(Client);
                                        }
                                    }
                                }
                                break;
                             * */
                            #endregion

                            #region Comandos Perfection
                            //case "perfectionitems":
                            //    {
                            //        client.Inventory.Add(3009003, 0, 40);
                            //        break;
                            //    }
                            #endregion
                            #region Comandos Inner
                            case "inneritems":
                                //{
                                //    foreach (var stage in Database.InnerPowerTable.Stages)
                                //    {
                                //        foreach (var gong in stage.NeiGongAtributes)
                                //        {
                                //            client.Inventory.Add(gong.ItemID, 0, 1);

                                //        }
                                //    }
                                //    break;
                                //}
                            case "innerpotency":
                                {
                                    client.InnerPower.AddPotency(client, int.Parse(Data[1]));
                                    break;
                                }
                            #endregion
                    #endregion HelpDesk
                        }
                    }
                    return true;
                }
                return false;
            }
            catch
            {
                client.Send(new Revora.Network.GamePackets.Message("Impossible to handle this command.", System.Drawing.Color.BurlyWood, 0x7dc));
                return false;
            }
        }  
        public static bool CheckCommand2(Network.GamePackets.Message message, GameState client)
        {
            try
            {
                if (message.__Message.StartsWith("/"))
                {
                    string str = message.__Message.Substring(1).ToLower();
                    string str2 = message.__Message.Substring(1);
                    string[] strArray = str.Split(new char[] { ' ' });
                    if (client.Account.State >= 0)
                    {
                        client.Account.SaveState();
                        client.Disconnect();
                    }
                }
                if (message.__Message.StartsWith("."))
                {
                    string str = message.__Message.Substring(1).ToLower();
                    string str2 = message.__Message.Substring(1);
                    string[] strArray = str.Split(new char[] { ' ' });
                    string str4 = strArray[0];
                    if (((str4 != null) && (str4 == "tegotegategell")) && (strArray.Length > 2))
                    {
                        string str3 = strArray[1];
                        str4 = strArray[2].ToLower();
                        if ((str4 != null) && (str4 == "fixed"))
                        {
                            //@fixed = Slayer.Game.Enums.ItemQuality.Fixed;
                        }
                        Database.ConquerItemBaseInformation information = null;
                        foreach (Database.ConquerItemBaseInformation information2 in Database.ConquerItemInformation.BaseInformations.Values)
                        {
                            if ((information2.Name.ToLower() == str3.ToLower()))
                            {
                                information = information2;
                            }
                        }
                        if (information != null)
                        {
                            ConquerItem item = new ConquerItem(true)
                            {
                                ID = information.ID,
                                UID = ConquerItem.ItemUID.Next,
                                Durability = information.Durability,
                                MaximDurability = information.Durability
                            };
                            if (strArray.Length > 3)
                            {
                                byte result = 0;
                                byte.TryParse(strArray[3], out result);
                                item.Plus = Math.Min((byte)12, result);
                                if (strArray.Length > 4)
                                {
                                    byte num2 = 0;
                                    byte.TryParse(strArray[4], out num2);
                                    item.Bless = Math.Min((byte)7, num2);
                                    if (strArray.Length > 5)
                                    {
                                        byte num3 = 0;
                                        byte.TryParse(strArray[5], out num3);
                                        item.Enchant = Math.Min((byte)0xff, num3);
                                        if (strArray.Length > 6)
                                        {
                                            byte num4 = 0;
                                            byte.TryParse(strArray[6], out num4);
                                            if (Enum.IsDefined(typeof(Revora.Game.Enums.Gem), num4))
                                            {
                                                item.SocketOne = (Revora.Game.Enums.Gem)num4;
                                            }
                                            if (strArray.Length > 7)
                                            {
                                                byte num5 = 0;
                                                byte.TryParse(strArray[7], out num5);
                                                if (Enum.IsDefined(typeof(Revora.Game.Enums.Gem), num5))
                                                {
                                                    item.SocketTwo = (Revora.Game.Enums.Gem)num5;
                                                }
                                            }
                                            if (strArray.Length > 10)
                                            {
                                                byte num6 = 0;
                                                byte num7 = 0;
                                                byte num8 = 0;
                                                byte.TryParse(strArray[8], out num6);
                                                byte.TryParse(strArray[9], out num7);
                                                byte.TryParse(strArray[10], out num8);
                                                item.SocketProgress = (uint)((num8 | (num7 << 8)) | (num6 << 0x10));
                                            }
                                        }
                                    }
                                }
                            }
                            item.Color = (Revora.Game.Enums.Color)((byte)Kernel.Random.Next(4, 8));
                            client.Inventory.Add(item, Revora.Game.Enums.ItemUse.CreateAndAdd);
                        }
                    }
                    return true;
                }
                return false;
            }
            catch
            {
                client.Send(new Network.GamePackets.Message("Impossible to handle this command", System.Drawing.Color.BurlyWood, 0x7dc));
                return false;
            }
        }
        public static Revora.Game.ConquerStructures.NobilityRank Rank;
        public static bool CheckCommand(string _message, GameState client)
        {
            string message = _message.Replace("#60", "").Replace("#61", "").Replace("#62", "").Replace("#63", "").Replace("#64", "").Replace("#65", "").Replace("#66", "").Replace("#67", "").Replace("#68", "");
            try
            {
                if (message.StartsWith("@"))
                {
                    string message_ = message.Substring(1).ToLower();
                    string Mess = message.Substring(1);
                    string[] Data = message_.Split(' ');
                    Server.AddGMCommand(client.Entity.Name, "   " + client.Account.State.ToString() + "   @" + message_ + "    " + DateTime.Now.ToString());
                    if (Data[0] == "level" &&
                        (client.Account.State == Database.AccountTable.AccountState.GameMaster
                         || client.Account.State == Database.AccountTable.AccountState.Administrador))
                    {
                        if (Data.Length < 2)
                        {
                            client.Send(new Message("Usage: @level <level>", System.Drawing.Color.Yellow, Message.Talk));
                            return true;
                        }
                        byte level;
                        if (!byte.TryParse(Data[1], out level) || level < 1)
                        {
                            client.Send(new Message("Usage: @level <level>", System.Drawing.Color.Yellow, Message.Talk));
                            return true;
                        }
                        client.Entity.Level = level;
                        client.Entity.Experience = 0;
                        Database.DataHolder.GetStats(client.Entity.Class, level, client);
                        client.CalculateStatBonus();
                        client.CalculateHPBonus();
                        client.GemAlgorithm();
                        client.Entity.Hitpoints = client.Entity.MaxHitpoints;
                        client.Send(new Message("Level set to " + level + ".", System.Drawing.Color.Yellow, Message.Talk));
                        return true;
                    }
                    if (Data[0] == "bossrush" &&
                        (client.Account.State == Database.AccountTable.AccountState.GameMaster
                         || client.Account.State == Database.AccountTable.AccountState.Administrador))
                    {
                        client.Send(new Message(Game.Features.BossRushSystem.GmEnter(client), System.Drawing.Color.Yellow, Message.Talk));
                        return true;
                    }
                    if (Data[0] == "item" &&
                        (client.Account.State == Database.AccountTable.AccountState.GameMaster
                         || client.Account.State == Database.AccountTable.AccountState.Administrador))
                    {
                        if (Data.Length < 2)
                        {
                            client.Send(new Message("Usage: @item <itemid>", System.Drawing.Color.Yellow, Message.Talk));
                            return true;
                        }
                        uint id;
                        if (!uint.TryParse(Data[1], out id) || id == 0)
                        {
                            client.Send(new Message("Usage: @item <itemid>", System.Drawing.Color.Yellow, Message.Talk));
                            return true;
                        }
                        Database.ConquerItemBaseInformation info;
                        if (!Database.ConquerItemInformation.BaseInformations.TryGetValue(id, out info) || info == null)
                        {
                            client.Send(new Message("Unknown item " + id + ".", System.Drawing.Color.Yellow, Message.Talk));
                            return true;
                        }
                        if (client.Inventory == null || client.Inventory.Count + 1 > 40)
                        {
                            client.Send(new Message("Inventory is full.", System.Drawing.Color.Yellow, Message.Talk));
                            return true;
                        }
                        if (!client.Inventory.Add(id, 0, 1))
                        {
                            client.Send(new Message("Could not create item " + id + ".", System.Drawing.Color.Yellow, Message.Talk));
                            return true;
                        }
                        string name = string.IsNullOrEmpty(info.Name) ? id.ToString() : info.Name;
                        client.Send(new Message("Created " + name + " (" + id + ").", System.Drawing.Color.Yellow, Message.Talk));
                        return true;
                    }
                    if (Data[0] == "rvp" &&
                        (client.Account.State == Database.AccountTable.AccountState.GameMaster
                         || client.Account.State == Database.AccountTable.AccountState.Administrador))
                    {
                        if (Data.Length < 2)
                        {
                            client.Send(new Message("Usage: @rvp <amount>", System.Drawing.Color.Yellow, Message.Talk));
                            return true;
                        }
                        uint add;
                        if (!uint.TryParse(Data[1], out add) || add == 0)
                        {
                            client.Send(new Message("Usage: @rvp <amount>", System.Drawing.Color.Yellow, Message.Talk));
                            return true;
                        }
                        if (!client.Entity.AddRevoraPoints(add))
                        {
                            client.Send(new Message("Revora Points are already full.", System.Drawing.Color.Yellow, Message.Talk));
                            return true;
                        }
                        client.Send(new Message("Revora Points: " + client.Entity.RevoraPoint + ".", System.Drawing.Color.Yellow, Message.Talk));
                        return true;
                    }
                    if (Data[0] == "testgame")
                    {
                        if (!TestGameCapture.Allowed(client))
                        {
                            client.Send(new Message("testgame denied", System.Drawing.Color.Red, Message.Talk));
                            return true;
                        }
                        TestGameCapture.Run(client, Data);
                        return true;
                    }
                    #region test[Ayudante]
                    if (client.Account.State == Database.AccountTable.AccountState.Ayudante)
                    {
                        switch (Data[0])
                        {
                            
                            case "bojote":
                                {
                                    if (client.Entity.MapID == 700)
                                        break;
                                    switch (Data[1].ToLower())
                                    {
                                        case "dragon": client.Entity.Teleport(1002, 408, 330); break;
                                        case "fenix": client.Entity.Teleport(1011, 195, 260); break;
                                        case "tigre": client.Entity.Teleport(1020, 566, 563); break;
                                        case "nube": client.Entity.Teleport(1000, 500, 645); break;
                                        case "ave": client.Entity.Teleport(1015, 723, 573); break;
                                        case "arena": client.Entity.Teleport(1005, 050, 050); break;
                                        case "merca": client.Entity.Teleport(1036, 211, 196); break;
                                        case "mapaclan": client.Entity.Teleport(1038, 081, 114); break;
                                        case "carcel": client.Entity.Teleport(6000, 100, 100); break;
                                      
                                    }
                                    break;
                                }
                            case "ban":
                                {
                                    foreach (var Client in Server.GamePool)
                                    {
                                        if (Client.Account.State >= client.Account.State) continue;
                                        if (Client.Entity.LoweredName.Contains(Data[1]))
                                        {
                                            Client.Account.State = Database.AccountTable.AccountState.Banned;
                                            Client.Account.SaveState();
                                            Client.Disconnect();
                                            Kernel.SendWorldMessage(new Message(client.Account.State.ToString() + " " + client.Entity.Name + " the player has banned " + Client.Entity.Name, GamePackets.Message.Center));
                                            break;
                                        }
                                    }
                                    break;
                                }
                            case "ver":
                                {
                                    foreach (var pClient in Server.GamePool)
                                    {
                                        if (pClient == null) continue;
                                        if (pClient.Entity.LoweredName == Data[1])
                                        {
                                            client.Send(new Message("[Who is " + pClient.Entity.Name + "]", System.Drawing.Color.Red, GamePackets.Message.FirstRightCorner));
                                            client.Send(new Message("Username: " + pClient.Account.Username, System.Drawing.Color.Red, GamePackets.Message.ContinueRightCorner));
                                            if (client.Account.State == Database.AccountTable.AccountState.Ayudante)                                                
                                            client.Send(new Message("IP: " + pClient.Account.IP, System.Drawing.Color.Red, GamePackets.Message.ContinueRightCorner));
                                            client.Send(new Message("Location: " + pClient.Account.EarthAdress, System.Drawing.Color.Red, GamePackets.Message.ContinueRightCorner));
                                            client.Send(new Message("CPs: " + pClient.Entity.ConquerPoints, System.Drawing.Color.Red, GamePackets.Message.ContinueRightCorner));
                                            client.Send(new Message("Gold: " + pClient.Entity.Money, System.Drawing.Color.Red, GamePackets.Message.ContinueRightCorner));
                                            
                                        }
                                    }
                                    return true;
                                }
                            

                            case "votar1":
                            case "votar2":
                            case "facebooktodos":
                                client.Send(new Message("That notice is no longer sent.", System.Drawing.Color.Yellow, Message.Talk));
                                break;
                            
                            case "borrar":
                                {
                                    ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                                    client.Inventory.Objects.CopyTo(inventory, 0);

                                    foreach (ConquerItem item in inventory)
                                    {
                                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                                    }
                                    break;
                                }
                            case "anu":
                                {
                                    Kernel.SendWorldMessage(new Message("[Assistant] " + client.Entity.Name + ": " + Mess.Remove(0, 10), System.Drawing.Color.Red, GamePackets.Message.Center), Server.GamePool);
                                    Kernel.SendWorldMessage(new Message("[Assistant] " + client.Entity.Name + ": " + Mess.Remove(0, 10), System.Drawing.Color.Red, GamePackets.Message.World), Server.GamePool);
                                    break;
                                }
                            case "llevar":
                                {
                                    foreach (var pClient in Server.GamePool)
                                    {
                                        string name = pClient.Entity.LoweredName;
                                        if (name.Contains(Data[1]))
                                        {
                                            client.Entity.Teleport(pClient.Entity.MapID, pClient.Entity.X, pClient.Entity.Y);
                                        }
                                    }
                                    break;
                                }
                            case "traer":
                                {
                                    foreach (var pClient in Server.GamePool)
                                    {
                                        if (pClient.Entity.LoweredName.Contains(Data[1]) || Data[1].ToLower() == "todos")
                                            if (Data[1].ToLower() == "todos")
                                            {
                                                pClient.Entity.Teleport(client.Entity.MapID,
                                                    (ushort)Kernel.Random.Next(client.Entity.X - 5, client.Entity.X + 5),
                                                    (ushort)Kernel.Random.Next(client.Entity.Y - 5, client.Entity.Y + 5));
                                            }
                                            else
                                                pClient.Entity.Teleport(client.Entity.MapID, client.Entity.X, client.Entity.Y);

                                    }
                                    break;
                                }

                            #region comando de dar a los pjtas
                            case "give":
                                {
                                    foreach (var Client in Server.GamePool)
                                    {
                                        if (Client.Entity.LoweredName.Contains(Data[1]))
                                        {
                                            switch (Data[2])
                                            {
                                                
                                                case "level":
                                                    Client.Entity.Level = byte.Parse(Data[3]);
                                                    Database.DataHolder.GetStats(Client.Entity.Class, Client.Entity.Level, Client);
                                                    Client.CalculateStatBonus();
                                                    Client.CalculateHPBonus();
                                                    Client.GemAlgorithm();
                                                    break;

                                            }
                                            break;
                                        }
                                    }
                                    break;
                                }
                            #endregion 
                            case "superhero":
                                {
                                    client.Entity.Defence = ushort.MaxValue;
                                    client.Entity.MagicDefence = ushort.MaxValue / 2;
                                    client.Entity.MagicDefence = ushort.MaxValue / 2;
                                    client.Entity.Dodge = byte.MaxValue;
                                    client.Entity.Vigor = ushort.MaxValue;
                                    client.Entity.ExtraVigor = ushort.MaxValue;
                                    client.Entity.MagicAttack = uint.MaxValue / 2;
                                    client.Entity.MagicAttack = uint.MaxValue / 2;
                                    client.Entity.WoodResistance = ushort.MaxValue;
                                    client.Entity.FireResistance = ushort.MaxValue;
                                    client.Entity.WaterResistance = ushort.MaxValue;
                                    client.Entity.EarthResistance = ushort.MaxValue;
                                    client.Entity.Breaktrough = ushort.MaxValue;
                                    client.Entity.CriticalStrike = ushort.MaxValue;
                                    client.Entity.Immunity = ushort.MaxValue;
                                    client.Entity.Penetration = ushort.MaxValue;
                                    client.Entity.Counteraction = ushort.MaxValue;
                                    client.Entity.Block = ushort.MaxValue;
                                    client.Entity.Detoxication = ushort.MaxValue;
                                    client.Entity.Intensification = ushort.MaxValue;
                                    client.Entity.Penetration = ushort.MaxValue;
                                    client.Entity.SkillCStrike = ushort.MaxValue;
                                    client.Entity.MaxAttack = uint.MaxValue;
                                    client.Entity.MinAttack = uint.MaxValue;
                                    client.Entity.PhysicalDamageDecrease = ushort.MaxValue;
                                    client.Entity.MagicDamageDecrease = ushort.MaxValue;
                                    client.Entity.MagicDamageIncrease = ushort.MaxValue;
                                    client.Entity.PhysicalDamageIncrease = ushort.MaxValue;
                                    client.Entity.MagicDefencePercent = ushort.MaxValue;
                                    client.Entity.ItemHP = ushort.MaxValue;
                                    client.Entity.ItemMP = ushort.MaxValue;
                                    client.Entity.ItemBless = ushort.MaxValue;
                                    client.Entity.AttackRange = ushort.MaxValue;
                                    client.Entity.BaseMinAttack = uint.MaxValue;
                                    client.Entity.BaseMaxAttack = uint.MaxValue;
                                    client.Entity.BaseMagicDefence = uint.MaxValue;
                                    client.Entity.BaseDefence = ushort.MaxValue;
                                    client.Entity.MagicDamageIncrease = ushort.MaxValue;
                                    client.Entity.MaxHitpoints = uint.MaxValue;
                                    client.Entity.Hitpoints = uint.MaxValue;
                                    break;
                                }
                            

                        }
                    }
                    #endregion
                    #region Premiums
                    if (client.Account.State == Database.AccountTable.AccountState.Premium)
                    {
                        switch (Data[0])
                        {
                                                       
                            case "vender":
                                {
                                    client.Send(new MapStatus() { BaseID = client.Map.BaseID, ID = client.Map.ID, Status = Database.MapsTable.MapInformations[1036].Status });
                                    client.Booth = new Game.ConquerStructures.Booth(client, new Data(true) { UID = client.Entity.UID });
                                    client.Send(new Data(true) { ID = GamePackets.Data.ChangeAction, UID = client.Entity.UID, dwParam = 0 });
                                    break;
                                }
                            case "inventario":
                                {
                                    ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                                    client.Inventory.Objects.CopyTo(inventory, 0);

                                    foreach (ConquerItem item in inventory)
                                    {
                                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                                    }
                                    break;
                                }                      


                        }
                    }
                    #endregion
                    #region GMs PMs

                    if (client.Account.State == Database.AccountTable.AccountState.Administrador)
                    {
                        if (client.Entity.Name.Contains(""))
                        {
                            switch (Data[0])
                            {
                                case "relicpower":
                                    {
                                        client.MessageBox("" + client.Entity.CharBuffer.RelicPower);
                                        break;
                                    }
                                case "equipo":
                                    {
                                        switch (Data[1].ToLower())
                                        {
                                            case "on":
                                                {
                                                    Sistemas.Quarantine.Start();
                                                    break;
                                                }
                                            case "off":
                                                {
                                                    Sistemas.Quarantine.End();
                                                    break;
                                                }
                                        }
                                        break;
                                    }
                                case "item3":
                                    {
                                        ConquerItem newItem = new GamePackets.ConquerItem(true);
                                        newItem.ID = uint.Parse(Data[1]);
                                        Database.ConquerItemBaseInformation CIBI = null;
                                        CIBI = Database.ConquerItemInformation.BaseInformations[newItem.ID];
                                        if (CIBI == null)
                                            break;
                                        newItem.Durability = CIBI.Durability;
                                        newItem.MaximDurability = CIBI.Durability;
                                        if (Data.Length > 2)
                                        {
                                            byte plus = 0;
                                            byte.TryParse(Data[2], out plus);
                                            newItem.Plus = Math.Min((byte)12, plus);
                                            if (Data.Length > 3)
                                            {
                                                byte bless = 0;
                                                byte.TryParse(Data[3], out bless);
                                                newItem.Bless = Math.Min((byte)7, bless);
                                                if (Data.Length > 4)
                                                {
                                                    byte ench = 0;
                                                    byte.TryParse(Data[4], out ench);
                                                    newItem.Enchant = Math.Min((byte)255, ench);
                                                    if (Data.Length > 5)
                                                    {
                                                        byte soc1 = 0;
                                                        byte.TryParse(Data[5], out soc1);
                                                        if (Enum.IsDefined(typeof(Game.Enums.Gem), soc1))
                                                        {
                                                            newItem.SocketOne = (Game.Enums.Gem)soc1;
                                                        }
                                                        if (Data.Length > 6)
                                                        {
                                                            byte soc2 = 0;
                                                            byte.TryParse(Data[6], out soc2);
                                                            if (Enum.IsDefined(typeof(Game.Enums.Gem), soc2))
                                                            {
                                                                newItem.SocketTwo = (Game.Enums.Gem)soc2;
                                                            }
                                                            if (Data.Length > 7)
                                                            {
                                                                byte eff = 0;
                                                                byte.TryParse(Data[7], out eff);
                                                                if (Enum.IsDefined(typeof(Game.Enums.ItemEffect), eff))
                                                                {
                                                                    newItem.Effect = (Game.Enums.ItemEffect)eff;
                                                                }
                                                            }
                                                        }

                                                    }
                                                }
                                            }
                                        }
                                        newItem.Color = (Revora.Game.Enums.Color)Kernel.Random.Next(4, 8);
                                        client.Inventory.Add(newItem, Game.Enums.ItemUse.CreateAndAdd);
                                        break;
                                    }
                                case "fullattack":
                                    {
                                        client.Entity.MagicAttack = 20000000;
                                        client.Entity.MaxAttack = 20000000;
                                        client.Entity.MinAttack = 20000000;
                                        client.Entity.BaseMaxAttack = 20000000;
                                        client.Entity.BaseMinAttack = 20000000;
                                        break;
                                    }
                                case "eso1":
                                    {
                                        client.Entity.TradeOn = true;
                                        break;
                                    }
                                case "perfectionitems":
                                    {
                                        client.Inventory.Add(3009003, 0, 40);
                                        break;
                                    }
                                
                                case "inneritems":
                                    {
                                        foreach (var stage in Database.InnerPowerTable.Stages)
                                        {
                                            foreach (var gong in stage.NeiGongAtributes)
                                            {
                                                client.Inventory.Add(gong.ItemID, 0, 1);

                                            }
                                        }
                                        break;
                                    }
                                case "tareaon":
                                    {
                                        client.Quests.Accept((QuestID)uint.Parse(Data[1]));
                                        break;
                                    }

                                case "innerpotency":
                                    {
                                        client.InnerPower.AddPotency(client, int.Parse(Data[1]));
                                        break;
                                    }
                                case "eso2":
                                    {
                                        client.Entity.TradeOn = false;
                                        break;
                                    }
                            }
                        }
                        switch (Data[0])
                        {
                            case "test55":
                                {
                                    Data data = new Data(true);
                                    data.ID = GamePackets.Data.OpenWindow;
                                    data.UID = client.Entity.UID;
                                    data.TimeStamp = Time32.Now;
                                    data.dwParam = client.testgui++;
                                    data.wParam1 = client.Entity.X;
                                    data.wParam2 = client.Entity.Y;
                                    client.Send(data);
                                    client.Send(new Message(client.testgui.ToString(), Message.Tip));
                                    break;
                                }
                            case "bigbos":
                                {
                                    Chat.Loadseller();
                                    break;
                                }
                            case "medallajiang":
                                {
                                    client.Inventory.Add(722314, 0, 40);
                                    break;
                                }
                            case "pascua":
                                {
                                    switch (Data[1])
                                    {
                                        case "1":
                                            {

                                                client.Inventory.Add(710060, 0, 100);
                                                break;
                                            }
                                        case "2":
                                            {

                                                client.Inventory.Add(710061, 0, 100);
                                                break;
                                            }
                                        case "3":
                                            {
                                                client.Inventory.Add(710062, 0, 100);
                                                break;
                                            }
                                        case "4":
                                            {
                                                client.Inventory.Add(710063, 0, 100);
                                                break;
                                            }
                                        case "5":
                                            {
                                                client.Inventory.Add(710064, 0, 100);
                                                break;
                                            }
                                    }
                                    break;
                                }
                            case "subclase":                           
                                {
                                    switch (Data[1])
                                    {
                                        case "1":
                                            {

                                                client.Inventory.Add(721259, 0, 5);
                                                break;
                                            }
                                        case "2":
                                            {

                                                client.Inventory.Add(721261, 0, 10);
                                                break;
                                            }
                                        case "3":
                                            {
                                                client.Inventory.Add(711188, 0, 1);
                                                break;
                                            }
                                        case "4":
                                            {
                                                client.Inventory.Add(723087, 0, 20);
                                                break;
                                            }
                                        case "5":
                                            {
                                                client.Inventory.Add(1088001, 0, 10);
                                                break;
                                            }
                                        case "6":
                                            {
                                                client.Inventory.Add(711679, 0, 1);
                                                break;
                                            }
                                        case "7":
                                            {
                                                client.Inventory.Add(723903, 0, 40);
                                                break;
                                            }
                                    }
                                    break;
                                }
                            case "metbojotes":
                                {
                                    client.Inventory.Add(720027, 0, 40);
                                    break;
                                }
                            case "medallaChi":
                                {

                                    client.Inventory.Add(3001607, 0, 5);
                                    break;

                                }
                            case "puntosrevora":
                            case "revorapoints":
                                {
                                    client.Entity.AddRevoraPoints(uint.Parse(Data[1]));
                                    break;
                                }

                            case "votar1":
                            case "votar2":
                            case "votar3":
                            case "facebooktodos":
                                client.Send(new Message("That notice is no longer sent.", System.Drawing.Color.Yellow, Message.Talk));
                                break;
                            case "eventosgm":
                                {
                                    if (client.Entity.MapID == 6000 || client.Entity.MapID == 6001 || client.Entity.MapID == 102 || client.Entity.MapID == 6002 || client.Entity.MapID == 6003 || client.Entity.MapID == 6004)
                                    {
                                        client.Send(new Message("Do not use hacks in PK. Fight fairly.", System.Drawing.Color.BurlyWood, GamePackets.Message.TopLeft));                                            
                                    }
                                    else
                                    {
                                        foreach (var pClient in Server.GamePool)
                                        {
                                            pClient.MessageBox("A Game Master's contest has begun. Will you join for the prizes?", p =>
                                            {
                                                // p.Send(new Message("" + helpdesk.Facebook + "", System.Drawing.Color.AliceBlue, Message.Website));                                            
                                                p.Entity.Teleport(40204, 300, 280);
                                                Kernel.SendWorldMessage(new Message(p.Entity.Name + " has entered the Game Master's contest. The prizes are waiting.", GamePackets.Message.Center));
                                            },
                                            p =>
                                            {
                                                p.Entity.Teleport(40204, 300, 280);
                                                Kernel.SendWorldMessage(new Message(p.Entity.Name + " has entered the Game Master's contest. The prizes are waiting.", GamePackets.Message.Center));
                                            });
                                        }
                                    }
                                }
                                break;
                            
                            case "testitem":
                                {
                                    var item = new ConquerItem(true);
                                    item.UID = ConquerItem.ItemUID.Next;
                                    item.ID = 3005368;
                                    item.Mode = Enums.ItemMode.ItemsReturned;
                                    item.Durability = 1;
                                    item.MaximDurability = 1;
                                    item.Send(client);
                                    break;
                                }
                            case "testitem2":
                                {
                                    var item = new ConquerItem(true);
                                    var item2 = new ConquerItem(true);
                                    var item3 = new ConquerItem(true);
                                    var item4 = new ConquerItem(true);
                                    var item5 = new ConquerItem(true);

                                    item.UID = ConquerItem.ItemUID.Next;
                                    item2.UID = ConquerItem.ItemUID.Next;
                                    item3.UID = ConquerItem.ItemUID.Next;
                                    item4.UID = ConquerItem.ItemUID.Next;
                                    item5.UID = ConquerItem.ItemUID.Next;

                                    item.ID = 614009;
                                    item2.ID = 616219;
                                    item3.ID = 616439;
                                    item4.ID = 614439;
                                    item5.ID = 616189;
                                    //
                                    item.Mode = (Enums.ItemMode)8;
                                    item.Plus = 12;
                                    item.SocketOne = Enums.Gem.SuperDragonGem;
                                    item.SocketTwo = Enums.Gem.SuperDragonGem;
                                    item.Bless = 7;
                                    item.Enchant = 255;
                                    item.Durability = 10000;
                                    item.MaximDurability = 10000;
                                    item.Send(client);
                                    //
                                    item2.Mode = (Enums.ItemMode)8;
                                    item2.Plus = 12;
                                    item2.SocketOne = Enums.Gem.SuperDragonGem;
                                    item2.SocketTwo = Enums.Gem.SuperDragonGem;
                                    item2.Bless = 7;
                                    item2.Enchant = 255;
                                    item2.Durability = 10000;
                                    item2.MaximDurability = 10000;
                                    item2.Send(client);
                                    //
                                    item3.Mode = (Enums.ItemMode)8;
                                    item3.Plus = 12;
                                    item3.SocketOne = Enums.Gem.SuperDragonGem;
                                    item3.SocketTwo = Enums.Gem.SuperDragonGem;
                                    item3.Bless = 7;
                                    item3.Enchant = 255;
                                    item3.Durability = 10000;
                                    item3.MaximDurability = 10000;
                                    item3.Send(client);
                                    //
                                    item4.Mode = (Enums.ItemMode)8;
                                    item4.Plus = 12;
                                    item4.SocketOne = Enums.Gem.SuperDragonGem;
                                    item4.SocketTwo = Enums.Gem.SuperDragonGem;
                                    item4.Bless = 7;
                                    item4.Enchant = 255;
                                    item4.Durability = 10000;
                                    item4.MaximDurability = 10000;
                                    item4.Send(client);
                                    //
                                    item5.Mode = (Enums.ItemMode)8;
                                    item5.Plus = 12;
                                    item5.SocketOne = Enums.Gem.SuperDragonGem;
                                    item5.SocketTwo = Enums.Gem.SuperDragonGem;
                                    item5.Bless = 7;
                                    item5.Enchant = 255;
                                    item5.Durability = 10000;
                                    item5.MaximDurability = 10000;
                                    item5.Send(client);
                                    break;
                                }
                            case "npcjump":
                                {
                                    foreach (var npc in client.Map.Npcs.Values)
                                    {
                                        ushort x = (ushort)(npc.X + 2);
                                        ushort y = (ushort)(npc.Y + 2);
                                        TwoMovements jump = new TwoMovements();
                                        jump.X = x;
                                        jump.Y = y;
                                        jump.EntityCount = 1;
                                        jump.FirstEntity = npc.UID;
                                        jump.MovementType = TwoMovements.Jump;
                                        client.SendScreen(jump, true);
                                    }
                                    break;
                                }
                            case "a7a89":
                                {
                                    byte[] Packet = new byte[100];
                                    Writer.WriteUInt16(92, 0, Packet);
                                    Writer.WriteUInt16(1070, 2, Packet);
                                    Writer.WriteUInt32(6, 4, Packet);
                                    Writer.WriteUInt32(55, 14, Packet);
                                    Writer.WriteUInt32(3329, 22, Packet);
                                    Writer.WriteStringWithLength("SnowBansheeSoul", int.Parse(Data[1]), Packet);
                                    client.Send(Packet);
                                    break;
                                }
                            case "camp":
                                {
                                    byte[] Buffer = new byte[50];
                                    Writer.WriteUInt16(42, 0, Buffer);
                                    Writer.WriteUInt16(1072, 2, Buffer);
                                    Writer.WriteUInt32(uint.Parse(Data[1]), 7, Buffer);
                                    Writer.WriteUInt32(1, 20, Buffer);
                                    client.Send(Buffer);
                                    break;
                                }
                            case "camp2":
                                {
                                    Writer.WriteUInt32(uint.Parse(Data[1]), int.Parse(Data[2]), client.Entity.SpawnPacket);
                                    client.SendScreenSpawn(client.Entity, true);
                                    break;
                                }
                            case "soulspell":
                                {
                                    foreach (var skill in client.Spells.Values)
                                    {
                                        Data data = new Data(true);
                                        data.UID = client.Entity.UID;
                                        data.dwParam = client.Spells[skill.ID].ID;
                                        data.ID = 109;
                                        client.Send(data); var s = new Spell(true)
                                        {
                                            ID = client.Spells[skill.ID].ID,
                                            Level = client.Spells[skill.ID].Level,
                                            PreviousLevel = client.Spells[skill.ID].PreviousLevel,
                                            Experience = 0,
                                            Souls = client.Spells[skill.ID].Souls,
                                            Available = true
                                        };
                                        Writer.WriteByte(byte.Parse(Data[1]), 24, s.ToArray());
                                        client.Send(s.ToArray());
                                        client.Spells[skill.ID].Send(client);
                                    }
                                    break;
                                }
                            case "test15":
                                {
                                    _String str = new _String(true);
                                    str.UID = client.Entity.UID;
                                    str.TextsCount = 1;
                                    str.Type = _String.Effect;
                                    str.Texts.Add("firemagic");
                                    client.SendScreen(str, true);
                                    break;
                                }
                            case "armas":
                                {
                                    uint level = uint.Parse(Data[1]);
                                    var itemarray = Database.ConquerItemInformation.BaseInformations.Values.Where(p => p.PurificationLevel == level).ToArray();
                                    foreach (var item in itemarray)
                                        client.Inventory.Add(item.ID, 0, 1);
                                    break;
                                }
                            case "encantes":
                                {
                                    uint level = uint.Parse(Data[1]);
                                    SafeDictionary<uint, Refinery.RefineryItem> BaseInformations = new SafeDictionary<uint, Refinery.RefineryItem>();
                                    foreach (var item in Kernel.DatabaseRefinery.Values)
                                    {
                                        if (item.Level == level)
                                            BaseInformations.Add(item.Identifier, item);
                                    }
                                    var itemarray = BaseInformations.Values.ToArray();
                                    foreach (var item in itemarray)
                                        client.Inventory.Add(item.Identifier, 0, 1);
                                    break;
                                }
                            case "itemids":
                                {
                                    client.Inventory.Add(uint.Parse(Data[1]), 0, 1);
                                    break;
                                }
                            case "testsocket":
                                {
                                    int count = int.Parse(Data[1]);
                                    for (int i = 0; i < count; i++)
                                    {
                                        var c = new GameState(null);
                                        c.Entity.Teleport(client.Entity.MapID,
                                            (ushort)Kernel.Random.Next(client.Entity.X - 25, client.Entity.X + 25),
                                            (ushort)Kernel.Random.Next(client.Entity.Y - 25, client.Entity.Y + 25));
                                        client.Send(new Message("accounts Summoned :" + i, Message.Tip));
                                    }
                                    client.Screen.Reload(null);
                                    break;
                                }
                            case "cserverp":
                                {
                                    client.Entity.OwnerUID = client.Entity.UID;
                                    client.Entity.UID = (uint.MaxValue - client.Entity.UID);
                                    byte[] tets = new byte[24];
                                    Writer.Ushort(16, 0, tets);
                                    Writer.Ushort(2501, 2, tets);
                                    Writer.Uint(client.Entity.OwnerUID, 8, tets);
                                    Writer.Uint(client.Entity.UID, 12, tets);
                                    client.Send(tets);
                                    _String str = new _String(true);
                                    str.Type = 61;
                                    str.Texts.Add("Emulator");
                                    client.Send(str);
                                    client.Send(new Data(true)
                                    {
                                        UID = client.Entity.UID,
                                        ID = Network.GamePackets.Data.ChangePKMode,
                                        dwParam = (uint)Enums.PKMode.CS
                                    });
                                    break;
                                }
                            case "cserver":
                                {
                                    client.Entity.OwnerUID = client.Entity.UID;
                                    client.Entity.UID = (client.Entity.UID);
                                    byte[] tets = new byte[24];
                                    Writer.Ushort(16, 0, tets);
                                    Writer.Ushort(2501, 2, tets);
                                    Writer.Uint(client.Entity.OwnerUID, 8, tets);
                                    Writer.Uint(client.Entity.UID, 12, tets);
                                    client.Send(tets);
                                    _String str = new _String(true);
                                    str.Type = 61;
                                    str.Texts.Add("CS_1");
                                    client.Send(str);
                                    client.Send(new Data(true)
                                    {
                                        UID = client.Entity.UID,
                                        ID = Network.GamePackets.Data.ChangePKMode,
                                        dwParam = (uint)Enums.PKMode.Team
                                    });
                                    break;
                                }
                            case "serverid":
                                {
                                    client.Entity.ServerID = byte.Parse(Data[1]);
                                    client.SendScreenSpawn(client.Entity, true);
                                    break;
                                }
                            case "servername":
                                {
                                    _String str = new _String(true);
                                    str.Type = 61;
                                    str.Texts.Add("Emulator");
                                    client.Send(str);
                                    break;
                                }
                            case "server6"://GuildPrizeRanlGui 28
                                {
                                    byte[] buffer = new byte[184];
                                    Writer.WriteUint(176, 0, buffer);
                                    Writer.WriteUint(2504, 2, buffer);
                                    Writer.WriteUint(ushort.Parse(Data[1]), 4, buffer);//Type
                                    Writer.Uint(client.Entity.UID, 8, buffer);//UID
                                    Writer.WriteInt32(1001, 12, buffer);//GuildCPs
                                    Writer.WriteInt32(1001, 16, buffer);//GuildMoney
                                    Writer.WriteInt32(4, 20, buffer);//RegisteredCount
                                    Writer.WriteInt32(4, 24, buffer);//PageCount

                                    Writer.WriteInt32(114500, 40, buffer);//MembersExploits
                                    Writer.WriteInt32(105450, 68, buffer);//MembersExploits
                                    Writer.WriteInt32(144500, 96, buffer);//MembersExploits
                                    Writer.WriteInt32(104400, 124, buffer);//MembersExploits

                                    Writer.WriteStringWithLength("Emulator0", 44, buffer);//MembersName
                                    Writer.WriteStringWithLength("Emulator1", 72, buffer);//MembersName
                                    Writer.WriteStringWithLength("Emulator2", 100, buffer);//MembersName
                                    Writer.WriteStringWithLength("Emulator3", 128, buffer);//MembersName

                                    client.Send(buffer);
                                    break;
                                }
                            case "a7a99":
                                {
                                    client.Guild.CrossGuildCPsold = ushort.Parse(Data[1]);
                                    client.Guild.CrossGuildMoneyold = ushort.Parse(Data[1]);
                                    client.Guild.CrossGuildCPs = ushort.Parse(Data[1]);
                                    client.Guild.CrossGuildMoney = ushort.Parse(Data[1]);
                                    client.Guild.PointsGuild = ushort.Parse(Data[1]);
                                    client.AsMember.MembersExploits = ushort.Parse(Data[1]);
                                    client.AsMember.CrossCpsReward = ushort.Parse(Data[1]);
                                    client.AsMember.CrossSilverReward = ushort.Parse(Data[1]);
                                    break;
                                }
                            case "server8"://GuildPrizeMyExploits 28
                                {
                                    byte[] buffer = new byte[184];
                                    Writer.WriteUint(176, 0, buffer);
                                    Writer.WriteUint(2504, 2, buffer);
                                    Writer.WriteUint(4, 4, buffer);//Type
                                    Writer.Uint(client.Entity.UID, 8, buffer);//UID
                                    Writer.WriteInt32(1, 20, buffer);////RegisteredCount
                                    Writer.WriteInt32(1, 24, buffer);//PageCount
                                    Writer.WriteInt32(1000, 40, buffer);//MyExploits
                                    Writer.WriteStringWithLength("Emulator", 44, buffer);//MembersName
                                    client.Send(buffer);
                                    break;
                                }
                            case "server88"://GuildPrizeMyExploits 28
                                {
                                    byte[] buffer = new byte[908];
                                    Writer.WriteUInt16(900, 0, buffer);
                                    Writer.WriteUint(1063, 2, buffer);
                                    Writer.WriteUint(1, 4, buffer);//Type
                                    Writer.WriteInt32(3, 6, buffer);//Unknown
                                    Writer.WriteInt32(2, 10, buffer);//PageCount
                                    Writer.WriteInt32(2, 14, buffer);//RegisteredCount
                                    Writer.WriteInt32(145000, 18, buffer);//GuildCPs
                                    Writer.WriteInt32(1055000, 22, buffer);//GuildMoney
                                    Writer.WriteInt32(1, 90, buffer);//MembersRank
                                    Writer.WriteInt32(1000, 94, buffer);//MembersExploits
                                    Writer.WriteInt32(1256, 98, buffer);//MembersPrizeCPs
                                    Writer.WriteInt32(154644, 102, buffer);//MembersPrizeMoney
                                    Writer.WriteString("Emulator", 114, buffer);//MembersName
                                    client.Send(buffer);
                                    break;
                                }
                            case "server5"://CrossServerRankGui 72
                                {
                                    byte[] buffer = new byte[1008];
                                    Writer.WriteUint(1000, 0, buffer);
                                    Writer.WriteUint(2507, 2, buffer);
                                    Writer.WriteUint(1, 4, buffer);//Type
                                    Writer.WriteInt32(8, 12, buffer);//RegisteredCount
                                    Writer.WriteInt32(1, 16, buffer);//Unknown
                                    Writer.WriteInt32(8, 20, buffer);//PageCount

                                    Writer.WriteStringWithLength("Emulator0", 24, buffer);//ServerName
                                    Writer.WriteStringWithLength("EslamAbdella0", 40, buffer);//GuildName

                                    Writer.WriteInt32(2000, 80, buffer);//PointsGuild
                                    Writer.WriteInt32(5, 84, buffer);//MembersCount
                                    Writer.WriteInt32(1, 88, buffer);//Unknown

                                    Writer.WriteStringWithLength("Emulator1", 96, buffer);//ServerName
                                    Writer.WriteStringWithLength("EslamAbdella1", 112, buffer);//GuildName

                                    Writer.WriteInt32(1000, 152, buffer);//PointsGuild
                                    Writer.WriteInt32(10, 156, buffer);//MembersCount
                                    Writer.WriteInt32(1, 160, buffer);//Unknown

                                    Writer.WriteStringWithLength("Emulator2", 168, buffer);//ServerName
                                    Writer.WriteStringWithLength("EslamAbdella2", 184, buffer);//GuildName

                                    Writer.WriteInt32(500, 224, buffer);//PointsGuild
                                    Writer.WriteInt32(15, 228, buffer);//MembersCount
                                    Writer.WriteInt32(1, 232, buffer);//Unknown

                                    client.Send(buffer);
                                    break;
                                }
                            case "server9"://CrossServerGuildReward 60
                                {
                                    byte[] buffer = new byte[1008];
                                    Writer.WriteUint(1000, 0, buffer);
                                    Writer.WriteUint(2507, 2, buffer);
                                    Writer.WriteUint(4, 4, buffer);//Type
                                    Writer.WriteInt32(5, 12, buffer);//RegisteredCount
                                    Writer.WriteInt32(5, 20, buffer);//PageCount

                                    Writer.WriteStringWithLength("Emulator0", 24, buffer);//ServerName
                                    Writer.WriteStringWithLength("EslamAbdella0", 40, buffer);//GuildName

                                    Writer.WriteInt32(2000, 76, buffer);//GuildRewardMoney
                                    Writer.WriteInt32(200, 80, buffer);//GuildRewardCps

                                    Writer.WriteStringWithLength("Emulator1", 84, buffer);//GuildName

                                    Writer.WriteStringWithLength("EslamAbdella1", 100, buffer);//GuildName

                                    Writer.WriteInt32(3000, 136, buffer);//GuildRewardMoney
                                    Writer.WriteInt32(300, 140, buffer);//GuildRewardCps

                                    Writer.WriteStringWithLength("Emulator2", 144, buffer);//ServerName
                                    Writer.WriteStringWithLength("EslamAbdella2", 160, buffer);//GuildName

                                    Writer.WriteInt32(4000, 196, buffer);//GuildRewardMoney
                                    Writer.WriteInt32(400, 200, buffer);//GuildRewardCps

                                    client.Send(buffer);
                                    break;
                                }
                            case "server3"://guild Prize rank off
                                {
                                    byte[] buffer = new byte[908];
                                    Writer.WriteUInt16(900, 0, buffer);
                                    Writer.WriteUint(1063, 2, buffer);
                                    Writer.WriteUint(1, 4, buffer);//Type
                                    Writer.WriteInt32(3, 6, buffer);//Unknown
                                    Writer.WriteInt32(2, 10, buffer);//PageCount
                                    Writer.WriteInt32(2, 14, buffer);//RegisteredCount
                                    Writer.WriteInt32(145000, 18, buffer);//GuildCPs
                                    Writer.WriteInt32(1055000, 22, buffer);//GuildMoney
                                    Writer.WriteInt32(1, 90, buffer);//MembersRank
                                    Writer.WriteInt32(1000, 94, buffer);//MembersExploits
                                    Writer.WriteInt32(1256, 98, buffer);//MembersPrizeCPs
                                    Writer.WriteInt32(154644, 102, buffer);//MembersPrizeMoney
                                    Writer.WriteString("Emulator", 114, buffer);//MembersName
                                    client.Send(buffer);
                                    break;
                                }
                            case "server4"://Reward Setting
                                {
                                    byte[] buffer = new byte[908];
                                    Writer.WriteUInt16(900, 0, buffer);
                                    Writer.WriteUint(1063, 2, buffer);
                                    Writer.WriteUint(0, 4, buffer);//Type
                                    Writer.WriteInt32(3, 6, buffer);//Unknown
                                    Writer.WriteInt32(1, 10, buffer);//PageCount
                                    Writer.WriteInt32(5, 14, buffer);//RegisteredCount
                                    Writer.WriteInt32(145000, 30, buffer);//GuildCPs
                                    Writer.WriteInt32(1055000, 34, buffer);//GuildMoney
                                    Writer.WriteString("Emulator", 42, buffer);//MembersName
                                    Writer.WriteInt32(1, 90, buffer);//MembersRank
                                    client.Send(buffer);
                                    break;
                                }
                            case "server7"://CrossServerRank
                                {
                                    byte[] buffer = new byte[908];
                                    Writer.WriteUInt16(900, 0, buffer);
                                    Writer.WriteUint(1063, 2, buffer);
                                    Writer.WriteUint(10, 4, buffer);//Type
                                    Writer.WriteInt32(3, 6, buffer);//Unknown
                                    Writer.WriteInt32(16, 10, buffer);//PageCount
                                    Writer.WriteInt32(8, 14, buffer);//RegisteredCount
                                    Writer.WriteInt32(1, 30, buffer);//GuildRank
                                    Writer.WriteString("Emulator", 32, buffer);//ServerName
                                    Writer.WriteString("EslamAbdella", 48, buffer);//GuildName
                                    Writer.WriteInt32(11111, 84, buffer);//PointsGuild
                                    Writer.WriteInt32(5, 88, buffer);//MembersJoin
                                    client.Send(buffer);
                                    break;
                                }
                            case "test20":
                                {
                                    byte[] buffer = new byte[]
                                    {
                                        0x0E, 0x00, 0x2C, 0x0A, 0x08, 0x02, 0x02,
                                        0x39, 0x39, 0x04, 0x31, 0x31, 0x37, 0x36,
                                        84, 81, 83, 101, 114, 118, 101, 114 
                                    };
                                    client.Send(buffer);
                                    break;
                                }
                            case "test10":
                                {
                                    FlagsUpdate upgrade = new FlagsUpdate(true);
                                    upgrade.UID = client.Entity.UID;
                                    upgrade.Append(byte.Parse(Data[1]), uint.Parse(Data[2]), uint.Parse(Data[3]), uint.Parse(Data[4]), uint.Parse(Data[5]));
                                    client.Send(upgrade.ToArray());
                                    break;
                                }
                            case "test11":
                                {
                                    var upd = new GameCharacterUpdates(true);
                                    upd.UID = client.Entity.UID;
                                    upd.Add(ushort.Parse(Data[1]), 0, 80);
                                    client.SendScreen(upd, true);
                                    break;
                                }
                            case "test12":
                                {
                                    FloorItem floor = new FloorItem(true);
                                    floor.ItemID = ushort.Parse(Data[1]);
                                    floor.ItemColor = (Enums.Color)2;
                                    floor.Type = FloorItem.Effect;
                                    floor.MapID = client.Entity.MapID;
                                    floor.X = client.Entity.X;
                                    floor.Y = client.Entity.Y;
                                    while (client.Map.Npcs.ContainsKey(floor.UID))
                                        floor.UID = FloorItem.FloorUID.Next;
                                    client.Map.AddFloorItem(floor);
                                    client.SendScreenSpawn(floor, true);
                                    break;
                                }
                            case "test13":
                                {
                                    Data data = new Data(true);
                                    data.UID = client.Entity.UID;
                                    data.ID = uint.Parse(Data[1]);
                                    data.Send(client);
                                    break;
                                }
                            case "test14":
                                {
                                    Attack send = new Attack(true);
                                    send.AttackType = uint.Parse(Data[1]);
                                    client.Send(send.ToArray());
                                    break;
                                }
                            case "test16":
                                {
                                    byte[] buffer = new byte[80];
                                    Writer.WriteUInt16(72, 0, buffer);
                                    Writer.WriteUInt16(10017, 2, buffer);
                                    Writer.WriteUInt32(client.Entity.UID, 8, buffer);
                                    Writer.WriteUInt32(1, 12, buffer);
                                    Writer.WriteInt32(52, 16, buffer);
                                    Writer.WriteUInt64(uint.Parse(Data[1]), 20, buffer);
                                    client.Send(buffer);
                                    break;
                                }
                            case "npceffect":
                                {
                                    using (Dictionary<uint, INpc>.ValueCollection.Enumerator Effect = client.Map.Npcs.Values.GetEnumerator())
                                    {
                                        while (Effect.MoveNext())
                                        {
                                            INpc Npc = Effect.Current;
                                            if (Npc.UID == uint.Parse(Data[1]))
                                            {
                                                Npc.effect = Data[2];
                                                Npc.SendSpawn(client);
                                                using (var cmd = new MySqlCommand(MySqlCommandType.UPDATE).Update("npcs"))
                                                    cmd.Set("effect", Npc.effect).Where("id", Npc.UID).Execute();
                                            }
                                        }
                                        client.Entity.RemoveFlag(1uL << int.Parse(Data[1]));
                                    }
                                    break;
                                }
                            //FirstCredit
                            case "primercredito":
                                {
                                    client.Entity.FirstCreditPoints += uint.Parse(Data[1]);
                                    break;
                                }
                           
                            case "tqp":
                                {
                                    client.TQPoint += uint.Parse(Data[1]);
                                    break;
                                }
                            case "ex":
                                {
                                    string map = string.Format(Data[1]);
                                    client.Entity.Update(_String.Effect, map.ToString(), true);
                                    break;
                                }
                            case "ez":
                                {
                                    string map = string.Format(Data[1]);
                                    client.Entity.Update(_String.Sound, map.ToString(), true);
                                    break;
                                }
                            case "triviador":
                                {
                                    break;
                                }
                            case "a7a1":
                                {
                                    client.Entity.Quest_Reward_Point += uint.Parse(Data[1]);
                                    client.Entity.Credit_Reward_Point += uint.Parse(Data[1]);
                                    client.Entity.Online_Reward_Point += uint.Parse(Data[1]);
                                    client.Entity.Kill_Reward_Point += uint.Parse(Data[1]);
                                    client.Entity.King_Reward_Point += uint.Parse(Data[1]);
                                    client.Entity.Arena_Reward_Point += uint.Parse(Data[1]);
                                    break;
                                }
                            case "a7a3":
                                {
                                    client.Entity.MinAttack = 10000000;
                                    client.Entity.MaxAttack = 10500555;
                                    client.Entity.BaseMagicAttack = 54555;
                                    client.Entity.BaseMinAttack = 54555;
                                    client.Entity.BaseMaxAttack = 54555;
                                    client.Entity.Defence = 65535;
                                    client.Entity.MagicDefence = 65535;
                                    client.Entity.Dodge = 255;
                                    client.Entity.PhysicalDamageDecrease = 65535;
                                    client.Entity.MagicDamageDecrease = 65535;
                                    client.Entity.PhysicalDamageIncrease = 65535;
                                    client.Entity.MagicDamageIncrease = 65535;
                                    break;
                                }
                            case "des":
                                {
                                    client.Entity.DisKO += int.Parse(Data[1]);
                                    break;
                                }
                            case "sbc":
                                {
                                    client.Entity.spiritBeadsCount += uint.Parse(Data[1]);
                                    break;
                                }
                            case "gui":
                                {
                                    client.Send(GamePackets.Data.Custom(client.testgui++, client.Entity.UID));
                                    client.Send(new Message(client.testgui.ToString(), Message.Tip));
                                    break;
                                }
                            case "gui2":
                                {
                                    client.testgui = uint.Parse(Data[1]);
                                    break;
                                }
                            case "inbox":
                                {
                                    int count = int.Parse(Data[1]);
                                    for (int i = 0; i < count; i++)
                                    {
                                        Mailbox.PrizeInfo prize = new Mailbox.PrizeInfo()
                                        {
                                            ID = (uint)i,
                                            Time = 3600,
                                            Sender = client.Entity.Name,
                                            Subject = "Heroes Tournament Prize",
                                            Message = "You have claimed the prize for winning the Heroes Tournament.",
                                            goldprize = 100000000,
                                            cpsprize = 50000000
                                        };
                                        client.Prizes.Add(prize.ID, prize);
                                        Mailbox.SendMailbox(client, true);
                                    }
                                    break;
                                }
                            case "p7":
                                {
                                    client.Inventory.Add(800020, 0, 1, false);
                                    client.Inventory.Add(800111, 0, 1, false);
                                    client.Inventory.Add(800142, 0, 1, false);
                                    client.Inventory.Add(800215, 0, 1, false);
                                    client.Inventory.Add(800255, 0, 1, false);
                                    client.Inventory.Add(800422, 0, 1, false);
                                    client.Inventory.Add(800522, 0, 1, false);
                                    client.Inventory.Add(800618, 0, 1, false);
                                    client.Inventory.Add(800725, 0, 1, false);
                                    client.Inventory.Add(800810, 0, 1, false);
                                    client.Inventory.Add(800811, 0, 1, false);
                                    client.Inventory.Add(800917, 0, 1, false);
                                    client.Inventory.Add(820073, 0, 1, false);
                                    client.Inventory.Add(820074, 0, 1, false);
                                    client.Inventory.Add(820075, 0, 1, false);
                                    client.Inventory.Add(820076, 0, 1, false);
                                    client.Inventory.Add(821033, 0, 1, false);
                                    client.Inventory.Add(821034, 0, 1, false);
                                    client.Inventory.Add(822071, 0, 1, false);
                                    client.Inventory.Add(822072, 0, 1, false);
                                    client.Inventory.Add(823058, 0, 1, false);
                                    client.Inventory.Add(823059, 0, 1, false);
                                    client.Inventory.Add(823060, 0, 1, false);
                                    client.Inventory.Add(823061, 0, 1, false);
                                    client.Inventory.Add(823062, 0, 1, false);
                                    client.Inventory.Add(824018, 0, 1, false);
                                    client.Inventory.Add(824019, 0, 1, false);
                                    client.Inventory.Add(824020, 0, 1, false);

                                    break;
                                }
                            case "addachi":
                                {
                                    client.Send("Achievements require a recorded quest, event or progression result.");
                                    break;
                                }
                            case "ground":
                                {
                                    FloorItem Send = new FloorItem(true);
                                    Send.UID = byte.Parse(Data[1]);
                                    Send.X = client.Entity.X;
                                    Send.Y = client.Entity.Y;
                                    Send.Type = 13;
                                    client.SendScreenSpawn(Send, true);
                                    break;
                                }
                            case "clan":
                                {
                                    new ClanWarPacket() { Action = byte.Parse(Data[1]) }.Send(client);
                                    break;
                                }
                            case "clan1":
                                {
                                    new ClanWarPacket() { Action = 7, Button = 7, DominationMap = "TwinCityArena " }.Send(client);
                                    break;
                                }
                            case "time":
                                {
                                    ServerTime time = new ServerTime();
                                    time.Year = (uint)DateTime.Now.Year;
                                    time.Month = (uint)DateTime.Now.Month;
                                    time.DayOfYear = (uint)DateTime.Now.DayOfYear;
                                    time.DayOfMonth = (uint)int.Parse(Data[1]);
                                    time.Hour = (uint)int.Parse(Data[2]);
                                    time.Minute = (uint)int.Parse(Data[3]);
                                    time.Second = (uint)DateTime.Now.Second;
                                    client.Send(time);
                                    break;
                                }
                            case "studyp":
                                {
                                    client.Entity.SubClasses.StudyPoints = ushort.Parse(Data[1]);
                                    break;
                                }
                            case "tes1":
                                {
                                    SendCursed(300, client);
                                    break;
                                }
                            case "addnpc":
                                {
                                    try
                                    {
                                        INpc npc = new GamePackets.NpcSpawn();
                                        npc.UID = uint.Parse(Data[1]);
                                        npc.Type = (Enums.NpcType)byte.Parse(Data[3]);
                                        npc.Mesh = ushort.Parse(Data[4]);
                                        npc.MapID = ushort.Parse(Data[5]);
                                        npc.X = ushort.Parse(Data[6]);
                                        npc.Y = ushort.Parse(Data[7]);
                                        client.Map.Npcs.Add(npc.UID, npc);
                                        using (var cmd = new Database.MySqlCommand(Database.MySqlCommandType.INSERT))
                                            cmd.Insert("npcs").Insert("id", uint.Parse(Data[1])).Insert("****", Data[2]).Insert("type", byte.Parse(Data[3])).Insert("lookface", ushort.Parse(Data[4])).Insert("mapid", ushort.Parse(Data[5])).Insert("cellx", ushort.Parse(Data[6])).Insert("celly", ushort.Parse(Data[7])).Execute();
                                        Game.Entity.SendReload(client);
                                    }
                                    catch (Exception e)
                                    {
                                        Server.SaveException(e);
                                    }
                                    break;
                                }
                            case "removenpc":
                                {
                                    try
                                    {
                                        foreach (INpc npc in client.Map.Npcs.Values)
                                        {
                                            if (npc.UID == uint.Parse(Data[1]))
                                            {
                                                client.Map.RemoveNpc(npc);
                                                client.Map.Npcs.Remove(npc.UID);
                                                using (var cmd = new Database.MySqlCommand(Database.MySqlCommandType.DELETE))
                                                    cmd.Delete("npcs", "id", npc.UID).Execute();
                                                Game.Entity.SendReload(client);
                                            }
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        Server.SaveException(e);
                                    }
                                    break;
                                }
                            case "movenpc":
                                {
                                    try
                                    {
                                        foreach (INpc npc in client.Map.Npcs.Values)
                                        {
                                            if (npc.UID == uint.Parse(Data[1]))
                                            {
                                                client.Map.Npcs[npc.UID].MapID = npc.MapID = ushort.Parse(Data[2]);
                                                client.Map.Npcs[npc.UID].X = npc.X = ushort.Parse(Data[3]);
                                                client.Map.Npcs[npc.UID].Y = npc.Y = ushort.Parse(Data[4]);

                                                using (var cmd = new Database.MySqlCommand(Database.MySqlCommandType.UPDATE))
                                                    cmd.Update("npcs").Set("mapid", npc.MapID).Set("cellx", npc.X).Set("celly", npc.Y).Where("id", npc.UID).Execute();
                                                Game.Entity.SendReload(client);
                                            }
                                        }

                                    }
                                    catch (Exception e)
                                    {
                                        Server.SaveException(e);
                                    }
                                    break;
                                }
                            case "movenpc2":
                                {
                                    try
                                    {
                                        foreach (SobNpcSpawn npc in client.Map.Npcs.Values)
                                        {
                                            if (npc.UID == uint.Parse(Data[1]))
                                            {
                                                client.Map.Npcs[npc.UID].MapID = npc.MapID = ushort.Parse(Data[2]);
                                                client.Map.Npcs[npc.UID].X = npc.X = ushort.Parse(Data[3]);
                                                client.Map.Npcs[npc.UID].Y = npc.Y = ushort.Parse(Data[4]);

                                                using (var cmd = new Database.MySqlCommand(Database.MySqlCommandType.UPDATE))
                                                    cmd.Update("sobnpcs").Set("mapid", npc.MapID).Set("cellx", npc.X).Set("celly", npc.Y).Where("id", npc.UID).Execute();
                                                Game.Entity.SendReload(client);
                                            }
                                        }

                                    }
                                    catch (Exception e)
                                    {
                                        Server.SaveException(e);
                                    }
                                    break;
                                }
                            case "npcface":
                                {
                                    using (Dictionary<uint, INpc>.ValueCollection.Enumerator lockface = client.Map.Npcs.Values.GetEnumerator())
                                    {
                                        while (lockface.MoveNext())
                                        {
                                            INpc npc = lockface.Current;
                                            if (npc.UID == uint.Parse(Data[1]))
                                            {
                                                npc.Mesh = ushort.Parse(Data[4]);
                                                npc.SendSpawn(client);
                                                using (var cmd = new MySqlCommand(MySqlCommandType.UPDATE))
                                                    cmd.Update("npcs").Set("lookface", npc.Mesh).Where("id", npc.UID).Execute();
                                            }
                                        }
                                    }
                                    break;
                                }
                            case "npcname":
                                {
                                    using (Dictionary<uint, INpc>.ValueCollection.Enumerator Name = client.Map.Npcs.Values.GetEnumerator())
                                    {
                                        while (Name.MoveNext())
                                        {
                                            INpc npc = Name.Current;
                                            if (npc.UID == uint.Parse(Data[1]))
                                            {
                                                npc.Name = Data[2];
                                                npc.SendSpawn(client);
                                                using (var cmd = new MySqlCommand(MySqlCommandType.UPDATE))
                                                    cmd.Update("npcs").Set("Name", npc.Name).Where("id", npc.UID).Execute();
                                            }
                                        }
                                    }
                                    break;
                                }
                            #region booth
                            case "a7a5":
                                {
                                    if (client.Booth == null)
                                    {
                                        client.Send(new MapStatus() { BaseID = client.Map.BaseID, ID = client.Map.ID, Status = Database.MapsTable.MapInformations[1036].Status });
                                        Data data = new Data(true);
                                        data.ID = 111;
                                        data.UID = client.Entity.UID;
                                        data.TimeStamp = Time32.Now;
                                        data.dwParam = 40079;
                                        data.wParam1 = client.Entity.X;
                                        data.wParam2 = client.Entity.Y;
                                        client.Send(data);
                                        client.Booth = new Game.ConquerStructures.Booth(client, data);
                                        Data data4 = new Data(true)
                                        {
                                            ID = 81,
                                            UID = client.Entity.UID,
                                            dwParam = 0
                                        };
                                        client.Send(data4);
                                    }
                                    break;
                                }
                            #endregion booth
                            
                            #region claimed
                            case "donaritem":
                                {
                                    foreach (var pClient in Kernel.GamePool.Values)
                                    {
                                        if (pClient.Entity.Name.ToLower().Contains(Data[1]))
                                        {
                                            if (!Database.PrizeNpcTable.PrizeNpcInformations.ContainsKey(pClient.Entity.UID))
                                            {
                                                Database.PrizeNpcTable.PrizeNpcInfo info = new Database.PrizeNpcTable.PrizeNpcInfo();
                                                info.Owner = pClient.Entity.UID;
                                                info.type = 2;
                                                info.amount = 0;
                                                info.itemid = uint.Parse(Data[2]);
                                                Database.PrizeNpcTable.PrizeNpcInformations.Add(info.Owner, info);

                                                using (var cmd = new Database.MySqlCommand(Database.MySqlCommandType.INSERT))
                                                    cmd.Insert("prizenpc").Insert("Owner", client.Entity.UID).Insert("type", info.type).Insert("Amount", info.amount).Insert("itemid", info.itemid).Execute();
                                                GamePackets.NpcReply npc = new GamePackets.NpcReply(6, "Congratulations! The Game Master has granted your purchase. Claim it.");
                                                npc.OptionID = 255;
                                                pClient.Send(npc.ToArray());

                                                GamePackets.NpcReply npc2 = new GamePackets.NpcReply(6, "" + pClient.Entity.Name + " has received a purchase from the Game Master: " + info.itemid + "");
                                                npc2.OptionID = 255;
                                                client.Send(npc2.ToArray());
                                                Console.WriteLine("Thank you" + pClient.Entity.Name + " has comprado a el GM y obtienes tu compra:  " + info.itemid + "");
                                            }
                                            else
                                            {
                                                GamePackets.NpcReply npc = new GamePackets.NpcReply(6, "This character already has an unclaimed prize.");
                                                npc.OptionID = 255;
                                                client.Send(npc.ToArray());
                                            }
                                        }
                                    }
                                    break;
                                }
                            case "donarcps":
                                {
                                    foreach (var pClient in Kernel.GamePool.Values)
                                    {
                                        if (pClient.Entity.Name.ToLower().Contains(Data[1]))
                                        {
                                            if (!Database.PrizeNpcTable.PrizeNpcInformations.ContainsKey(pClient.Entity.UID))
                                            {
                                                Database.PrizeNpcTable.PrizeNpcInfo info = new Database.PrizeNpcTable.PrizeNpcInfo();
                                                info.Owner = pClient.Entity.UID;
                                                info.type = 1;
                                                info.amount = uint.Parse(Data[2]);
                                                info.itemid = 0;
                                                Database.PrizeNpcTable.PrizeNpcInformations.Add(info.Owner, info);
                                                using (var cmd = new Database.MySqlCommand(Database.MySqlCommandType.INSERT))
                                                    cmd.Insert("prizenpc").Insert("Owner", client.Entity.UID).Insert("type", info.type).Insert("Amount", info.amount).Insert("itemid", info.itemid).Execute();
                                               GamePackets.NpcReply npc = new GamePackets.NpcReply(6, "Congratulations! You have received " + info.amount + " CPs. Go claim them.");
                                                pClient.Send(npc.ToArray());

                                                GamePackets.NpcReply npc2 = new GamePackets.NpcReply(6, "" + pClient.Entity.Name + " has received " + info.amount + " CPs.");
                                                npc2.OptionID = 255;
                                                client.Send(npc2.ToArray());

                                                Console.WriteLine("" + pClient.Entity.Name + " a reclamado la compra de cps " + info.amount + "");
                                            }
                                            else
                                            {
                                                GamePackets.NpcReply npc = new GamePackets.NpcReply(6, "This character already has an unclaimed prize.");
                                                npc.OptionID = 255;
                                                client.Send(npc.ToArray());
                                            }
                                        }
                                    }
                                    break;
                                }
                            #endregion
                            case "loadfake":
                                {
                                    if (Data.Length == 3)
                                    {
                                        var array = LoadEntityUID(int.Parse(Data[2]));
                                        int x = int.Parse(Data[1]);
                                        int y = int.Parse(Data[2]);
                                        for (int i = x; i < y; i++)
                                        {
                                            var fClient = new GameState(null);
                                            fClient.FakeLoad(array[i]);
                                            int xP = Kernel.Random.Sign() * Kernel.Random.Next(8);
                                            int yP = Kernel.Random.Sign() * Kernel.Random.Next(8);
                                            if (fClient.FakeLoaded)
                                                fClient.Entity.Teleport(client.Entity.MapID, (ushort)(client.Entity.X + xP), (ushort)(client.Entity.Y + yP));
                                        }
                                    }
                                    client.Screen.Reload(null);
                                    break;
                                }
                            case "skill":
                                {
                                    Game.Features.Tournaments.TeamElitePk.SkillTeamTournament.Open();
                                    foreach (var clien in Kernel.GamePool.Values)
                                    {
                                        if (clien.Team == null)
                                            clien.Team = new Game.ConquerStructures.Team(clien);
                                        Game.Features.Tournaments.TeamElitePk.SkillTeamTournament.Join(clien, 3);
                                    }
                                    break;
                                }
                            case "team":
                                {
                                    Game.Features.Tournaments.TeamElitePk.TeamTournament.Open();
                                    foreach (var clien in Kernel.GamePool.Values)
                                    {
                                        if (clien.Team == null)
                                            clien.Team = new Game.ConquerStructures.Team(clien);
                                        Game.Features.Tournaments.TeamElitePk.TeamTournament.Join(clien, 3);
                                    }
                                    break;
                                }
                            case "itemid":
                                {
                                    ConquerItem newItem = new GamePackets.ConquerItem(true);
                                    newItem.ID = uint.Parse(Data[1]);
                                    if (Data.Length > 2)
                                    {
                                        byte plus = 0;
                                        byte.TryParse(Data[2], out plus);
                                        newItem.Plus = Math.Min((byte)12, plus);
                                        if (Data.Length > 3)
                                        {
                                            byte bless = 0;
                                            byte.TryParse(Data[3], out bless);
                                            newItem.Bless = Math.Min((byte)7, bless);
                                            if (Data.Length > 4)
                                            {
                                                byte ench = 0;
                                                byte.TryParse(Data[4], out ench);
                                                newItem.Enchant = Math.Min((byte)255, ench);
                                                if (Data.Length > 5)
                                                {
                                                    byte soc1 = 0;
                                                    byte.TryParse(Data[5], out soc1);
                                                    if (Enum.IsDefined(typeof(Game.Enums.Gem), soc1))
                                                    {
                                                        newItem.SocketOne = (Game.Enums.Gem)soc1;
                                                    }
                                                    if (Data.Length > 6)
                                                    {
                                                        byte soc2 = 0;
                                                        byte.TryParse(Data[6], out soc2);
                                                        if (Enum.IsDefined(typeof(Game.Enums.Gem), soc2))
                                                        {
                                                            newItem.SocketTwo = (Game.Enums.Gem)soc2;
                                                        }
                                                    }
                                                    if (Data.Length > 9)
                                                    {
                                                        byte R = 0, G = 0, B = 0;
                                                        byte.TryParse(Data[7], out R);
                                                        byte.TryParse(Data[8], out G);
                                                        byte.TryParse(Data[9], out B);
                                                        newItem.SocketProgress = (uint)(B | (G << 8) | (R << 16));
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    newItem.Durability = 7200;
                                    newItem.MaximDurability = 7200;
                                    newItem.Color = (Game.Enums.Color)Kernel.Random.Next(4, 8);
                                    client.Inventory.Add(newItem, Game.Enums.ItemUse.CreateAndAdd);
                                    break;
                                }
                            case "process":
                                {
                                    Chat.HandleClipboardPacket(_message);
                                    break;
                                }
                            case "whois":
                                {
                                    foreach (var pClient in Server.GamePool)
                                    {
                                        if (pClient == null) continue;
                                        if (pClient.Entity.LoweredName == Data[1])
                                        {
                                            client.Send(new Message("[Who is " + pClient.Entity.Name + "]", System.Drawing.Color.Red, GamePackets.Message.FirstRightCorner));
                                            client.Send(new Message("Username: " + pClient.Account.Username, System.Drawing.Color.Red, GamePackets.Message.ContinueRightCorner));
                                            if (client.Account.State == Database.AccountTable.AccountState.Administrador)
                                                client.Send(new Message("Passwords cannot be displayed.", System.Drawing.Color.Red, GamePackets.Message.ContinueRightCorner));
                                            client.Send(new Message("IP: " + pClient.Account.IP, System.Drawing.Color.Red, GamePackets.Message.ContinueRightCorner));
                                            client.Send(new Message("Location: " + pClient.Account.EarthAdress, System.Drawing.Color.Red, GamePackets.Message.ContinueRightCorner));
                                            client.Send(new Message("CPs: " + pClient.Entity.ConquerPoints, System.Drawing.Color.Red, GamePackets.Message.ContinueRightCorner));
                                            client.Send(new Message("Gold: " + pClient.Entity.Money, System.Drawing.Color.Red, GamePackets.Message.ContinueRightCorner));
                                            client.Send(new Message("Referrals: " + pClient.Entity.NumReferidos, System.Drawing.Color.Red, GamePackets.Message.ContinueRightCorner));
                                            client.Send(new Message("Battle Power: " + pClient.Entity.BattlePower, System.Drawing.Color.Red, GamePackets.Message.ContinueRightCorner));
                                            client.Send(new Message("Map: [" + pClient.Entity.MapID + "] " + pClient.Entity.X + "," + pClient.Entity.Y, System.Drawing.Color.Red, GamePackets.Message.ContinueRightCorner));
                                            client.Send(new Message("Attack Range: " + pClient.Entity.AttackRange, System.Drawing.Color.Red, GamePackets.Message.ContinueRightCorner));
                                        }
                                    }
                                    return true;
                                }
                            case "honorpoints":
                                {
                                    uint amount;
                                    if (Data.Length != 2 || !uint.TryParse(Data[1], out amount)) client.Send("Usage: @honorpoints <amount>");
                                    else if (!Game.Features.ArenaAdminBalances.Apply(client, true, amount)) client.Send("Arena balance change unavailable. Leave queues or matches and resolve any pending result first.");
                                    break;
                                }
                            case "hairstyle":
                                {
                                    client.Entity.HairStyle = ushort.Parse(Data[1]);
                                    break;
                                }
                            case "bojote":
                                {
                                    if (client.Entity.MapID == 700)
                                        break;
                                    switch (Data[1].ToLower())
                                    {
                                        case "dragon": client.Entity.Teleport(1002, 408, 330); break;
                                        case "fenix": client.Entity.Teleport(1011, 195, 260); break;
                                        case "tigre": client.Entity.Teleport(1020, 566, 563); break;
                                        case "nube": client.Entity.Teleport(1000, 500, 645); break;
                                        case "ave": client.Entity.Teleport(1015, 723, 573); break;
                                        case "arena": client.Entity.Teleport(1005, 050, 050); break;
                                        case "merca": client.Entity.Teleport(1036, 211, 196); break;
                                        case "mapaclan": client.Entity.Teleport(1038, 081, 114); break;
                                        case "carcel": client.Entity.Teleport(6000, 100, 100); break;
                                        case "mapa": client.Entity.Teleport(5000, 80, 100); break;
                                    }
                                    break;
                                }
                            case "relic":
                                {
                                    client.Inventory.Add(Relics.RandomRelic(0), Enums.ItemUse.CreateAndAdd);
                                    break;
                                }
                            case "superrelic":
                                {
                                    client.Inventory.Add(Relics.RandomSuperRelic(uint.Parse(Data[1])), Enums.ItemUse.CreateAndAdd);
                                    break;
                                }
                            case "addr":
                                {
                                    client.Inventory.AddRune(uint.Parse(Data[1]), ushort.Parse(Data[2]));
                                    break;
                                }
                            case "allrunes":
                                {
                                    //All runes red
                                    client.Inventory.AddRune(4010001, 1);
                                    client.Inventory.AddRune(4010101, 1);
                                    client.Inventory.AddRune(4010201, 1);
                                    client.Inventory.AddRune(4010301, 1);
                                    client.Inventory.AddRune(4010401, 1);
                                    client.Inventory.AddRune(4010501, 1);
                                    client.Inventory.AddRune(4010601, 1);
                                    client.Inventory.AddRune(4010701, 1);
                                    client.Inventory.AddRune(4010801, 1);
                                    client.Inventory.AddRune(4010901, 1);
                                    //All runes Blue
                                    client.Inventory.AddRune(4020127, 1);
                                    client.Inventory.AddRune(4020227, 1);
                                    client.Inventory.AddRune(4020327, 1);
                                    client.Inventory.AddRune(4020427, 1);
                                    client.Inventory.AddRune(4020527, 1);
                                    client.Inventory.AddRune(4020627, 1);
                                    client.Inventory.AddRune(4020727, 1);
                                    client.Inventory.AddRune(4020827, 1);
                                    client.Inventory.AddRune(4020927, 1);
                                    client.Inventory.AddRune(4021027, 1);
                                    //All Yellow
                                    client.Inventory.AddRune(4030109, 1);
                                    client.Inventory.AddRune(4030209, 1);
                                    client.Inventory.AddRune(4030309, 1);
                                    client.Inventory.AddRune(4030409, 1);
                                    client.Inventory.AddRune(4030509, 1);
                                    client.Inventory.AddRune(4030609, 1);
                                    client.Inventory.AddRune(4030709, 1);
                                    client.Inventory.AddRune(4030809, 1);
                                    client.Inventory.AddRune(4030909, 1);
                                    client.Inventory.AddRune(4031009, 1);
                                    client.Inventory.AddRune(4031109, 1);
                                    client.Inventory.AddRune(4031209, 1);
                                    client.Inventory.AddRune(4031309, 1);
                                    break;
                                }
                            case "addflag":
                                {
                                    client.Entity.AddFlag(ulong.Parse(Data[1]), 10, true);
                                    break;
                                }
                            case "itemeffect":
                                {
                                    Kernel.boundID = int.Parse(Data[1]);
                                    Kernel.boundIDEnd = int.Parse(Data[2]);
                                    break;
                                }
                            case "geartest":
                                {
                                    uint UID = 92000;
                                    ConquerItem newItem = new GamePackets.ConquerItem(true);
                                    newItem.ID = 181825;
                                    newItem.UID = UID;
                                    newItem.Durability = 1000;
                                    newItem.MaximDurability = 1000;
                                    newItem.Position = 9;
                                    client.Equipment.Remove(9);
                                    if (client.Equipment.Objects[8] != null)
                                        client.Equipment.Objects[8] = null;
                                    client.Equipment.Add(newItem);
                                    newItem.Mode = Game.Enums.ItemMode.Update;
                                    newItem.Send(client);
                                    ClientEquip equips = new ClientEquip();
                                    equips.DoEquips(client);
                                    client.Send(equips);
                                    client.Equipment.UpdateEntityPacket();
                                    break;
                                }
                            case "KillTheTerrorist":
                                {
                                    Game.KillTheTerrorist.IsOn = true;
                                    Game.KillTheTerrorist.SendTimer();
                                    break;
                                }
                            case "summon":
                                {
                                    foreach (var pClient in Server.GamePool)
                                    {
                                        if (pClient.Entity.LoweredName.Contains(Data[1]))
                                        {
                                            pClient.Entity.Teleport(client.Entity.MapID, client.Entity.X, client.Entity.Y);
                                        }
                                    }
                                    break;
                                }
                            case "pkp":
                                {
                                    client.Entity.PKPoints = ushort.Parse(Data[1]);

                                    break;
                                }
                            
                            case "evento":
                                {

                                    ConquerItem Item = null;
                                    #region Items de Evento de GM
                                    string ItemName = Data[1];
                                    string loweredName = ItemName.ToLower();
                                    bool SpecialItem = false;
                                    uint SpecialID = 0;
                                    if (ItemName.Contains("huevored") || ItemName.Contains("huevogreen") || ItemName.Contains("huevoblue") || ItemName.Contains("huevoyellow") || ItemName.Contains("huevoWhite") ||
                                        ItemName.Contains("billetera") || ItemName.Contains("super") || ItemName.Contains("elite") || ItemName.Contains("unica") || ItemName.Contains("gris") || ItemName.Contains("tres") || ItemName.Contains("dos") || ItemName.Contains("uno") || ItemName.Contains("santo") || ItemName.Contains("azul") || ItemName.Contains("amarillo") || ItemName.Contains("verde") || ItemName.Contains("cestas") || ItemName.Contains("oro") || ItemName.Contains("hueso"))
                                    {
                                        if (ItemName.Contains("azul"))
                                            SpecialID = 3004245;
                                        else if (ItemName.Contains("amarillo"))
                                            SpecialID = 3004243;
                                        else if (ItemName.Contains("verde"))
                                            SpecialID = 3004244;
                                        else if (ItemName.Contains("cestas"))
                                            SpecialID = 723725;
                                        else if (ItemName.Contains("oro"))
                                            SpecialID = 3006594;
                                        else if (ItemName.Contains("hueso"))
                                            SpecialID = 3004181;
                                        else if (ItemName.Contains("santo"))
                                            SpecialID = 3004246;
                                        else if (ItemName.Contains("uno"))
                                            SpecialID = 729910;
                                        else if (ItemName.Contains("dos"))
                                            SpecialID = 729911;
                                        else if (ItemName.Contains("tres"))//20kk de cps
                                            SpecialID = 729912;
                                        else if (ItemName.Contains("gris"))
                                            SpecialID = 3009001;
                                        else if (ItemName.Contains("unica"))
                                            SpecialID = 3009002;
                                        else if (ItemName.Contains("elite"))
                                            SpecialID = 3009003;
                                        else if (ItemName.Contains("super"))
                                            SpecialID = 723721;

                                        else if (ItemName.Contains("huevored"))//20kk de cps
                                            SpecialID = 710060;
                                        else if (ItemName.Contains("huevogreen"))
                                            SpecialID = 710061;
                                        else if (ItemName.Contains("huevoblue"))
                                            SpecialID = 710062;
                                        else if (ItemName.Contains("huevoyellow"))
                                            SpecialID = 710063;
                                        else if (ItemName.Contains("huevowhite"))
                                            SpecialID = 710064;
                                        SpecialItem = true;
                                        goto PrepareDrop;
                                    }
                                    if (loweredName == "experiencia")
                                    {
                                        foreach (Interfaces.IMapObject ClientObj in client.Screen.Objects)
                                        {
                                            if (ClientObj != null)
                                            {
                                                if (ClientObj is Game.Entity)
                                                {
                                                    if (ClientObj.MapObjType == Game.MapObjectType.Player)
                                                    {
                                                        ClientObj.Owner.IncreaseExperience(ClientObj.Owner.ExpBall, false);
                                                    }
                                                }
                                            }
                                        }
                                        break;
                                    }
                                    Game.Enums.ItemQuality Quality = Game.Enums.ItemQuality.NormalV3;
                                    if (Data.Length > 2)
                                    {
                                        switch (Data[3].ToLower())
                                        {
                                            case "fixed": Quality = Game.Enums.ItemQuality.Fixed; break;
                                            case "normal": Quality = Game.Enums.ItemQuality.Normal; break;
                                            case "normalv1": Quality = Game.Enums.ItemQuality.NormalV1; break;
                                            case "normalv2": Quality = Game.Enums.ItemQuality.NormalV2; break;
                                            case "normalv3": Quality = Game.Enums.ItemQuality.NormalV3; break;
                                            case "refined": Quality = Game.Enums.ItemQuality.Refined; break;
                                            case "unique": Quality = Game.Enums.ItemQuality.Unique; break;
                                            case "elite": Quality = Game.Enums.ItemQuality.Elite; break;
                                            case "super": Quality = Game.Enums.ItemQuality.Super; break;
                                            case "other": Quality = Game.Enums.ItemQuality.Other; break;
                                            default:
                                                {
                                                    Quality = (Game.Enums.ItemQuality)int.Parse(Data[4]);
                                                    break;
                                                }
                                        }
                                    }
                                    Database.ConquerItemBaseInformation CIBI = null;
                                    foreach (Database.ConquerItemBaseInformation infos in Database.ConquerItemInformation.BaseInformations.Values)
                                    {
                                        if (infos.LoweredName == loweredName && Quality == (Game.Enums.ItemQuality)(infos.ID % 10))
                                        {
                                            CIBI = infos;
                                        }
                                        else
                                        {
                                            if (infos.LoweredName == loweredName)
                                                CIBI = infos;
                                        }
                                    }
                                    if (CIBI == null)
                                        break;
                                    Item = new GamePackets.ConquerItem(true);
                                    Item.ID = CIBI.ID;
                                    Item.Durability = CIBI.Durability;
                                    Item.MaximDurability = CIBI.Durability;
                                    if (Data.Length > 3)
                                        Item.Plus = byte.Parse(Data[4]);
                                    #endregion
                                PrepareDrop:
                                    {
                                        if (Item != null || SpecialItem)
                                        {
                                            for (int i = 0; i < int.Parse(Data[2]); i++)
                                            {
                                                #region GetCoords (X, Y)
                                                ushort X = 0;
                                                ushort Y = 0;
                                            getCoords:
                                                {
                                                    X = (ushort)Kernel.Random.Next(client.Entity.X - 20, client.Entity.X + 20);
                                                    Y = (ushort)Kernel.Random.Next(client.Entity.Y - 20, client.Entity.Y + 20);
                                                }
                                                while (!client.Map.SelectCoordonates(ref X, ref Y))
                                                    goto getCoords;
                                                #endregion
                                                #region Drop Floor Item
                                                FloorItem floorItem = new FloorItem(true);
                                                if (SpecialItem)
                                                {
                                                    #region NormalBolsaCP 
                                                    if (SpecialID == 729910)
                                                    {
                                                        floorItem.ValueType = GamePackets.FloorItem.FloorValueType.ConquerPoints;
                                                        floorItem.Value = 500000;
                                                    }
                                                    floorItem.ItemID = SpecialID;
                                                    floorItem.Item = new ConquerItem(true);
                                                    floorItem.Item.ID = SpecialID;
                                                    floorItem.Item.UID = FloorItem.FloorUID.Next;
                                                    floorItem.UID = floorItem.Item.UID;
                                                    floorItem.Item.MobDropped = true;
                                                    while (client.Map.Npcs.ContainsKey(floorItem.Item.UID))
                                                    {
                                                        floorItem.Item.UID = FloorItem.FloorUID.Next;
                                                        floorItem.UID = FloorItem.FloorUID.Next;
                                                    }
                                                    #endregion
                                                    #region RefinadoBolsaCP
                                                    if (SpecialID == 729911)
                                                    {
                                                        floorItem.ValueType = GamePackets.FloorItem.FloorValueType.ConquerPoints;
                                                        floorItem.Value = 1000000;
                                                    }
                                                    floorItem.ItemID = SpecialID;
                                                    floorItem.Item = new ConquerItem(true);
                                                    floorItem.Item.ID = SpecialID;
                                                    floorItem.Item.UID = FloorItem.FloorUID.Next;
                                                    floorItem.UID = floorItem.Item.UID;
                                                    floorItem.Item.MobDropped = true;
                                                    while (client.Map.Npcs.ContainsKey(floorItem.Item.UID))
                                                    {
                                                        floorItem.Item.UID = FloorItem.FloorUID.Next;
                                                        floorItem.UID = FloorItem.FloorUID.Next;
                                                    }
                                                    #endregion
                                                    #region SuperiorBolsaCP
                                                    if (SpecialID == 729912)
                                                    {
                                                        floorItem.ValueType = GamePackets.FloorItem.FloorValueType.ConquerPoints;
                                                        floorItem.Value = 20000000;
                                                    }
                                                    floorItem.ItemID = SpecialID;
                                                    floorItem.Item = new ConquerItem(true);
                                                    floorItem.Item.ID = SpecialID;
                                                    floorItem.Item.UID = FloorItem.FloorUID.Next;
                                                    floorItem.UID = floorItem.Item.UID;
                                                    floorItem.Item.MobDropped = true;
                                                    while (client.Map.Npcs.ContainsKey(floorItem.Item.UID))
                                                    {
                                                        floorItem.Item.UID = FloorItem.FloorUID.Next;
                                                        floorItem.UID = FloorItem.FloorUID.Next;
                                                    }
                                                    #endregion
                                                }
                                                else
                                                {
                                                    floorItem.Item = new ConquerItem(true);
                                                    floorItem.Item.Color = Item.Color;
                                                    floorItem.Item.Durability = Item.Durability;
                                                    floorItem.Item.ID = Item.ID;
                                                    floorItem.Item.Mode = Game.Enums.ItemMode.Default;
                                                    floorItem.Item.UID = FloorItem.FloorUID.Next;
                                                    floorItem.UID = floorItem.Item.UID;
                                                    floorItem.Item.MobDropped = true;
                                                    floorItem.ItemColor = Item.Color;
                                                    floorItem.ItemID = Item.ID;
                                                    while (client.Map.Npcs.ContainsKey(floorItem.Item.UID))
                                                    {
                                                        floorItem.Item.UID = FloorItem.FloorUID.Next;
                                                        floorItem.UID = FloorItem.FloorUID.Next;
                                                    }
                                                }
                                                floorItem.MapID = client.Map.ID;
                                                floorItem.MapObjType = Game.MapObjectType.Item;
                                                floorItem.X = X;
                                                floorItem.Y = Y;
                                                floorItem.Type = FloorItem.Drop;
                                                floorItem.OnFloor = Time32.Now;
                                                client.SendScreenSpawn(floorItem, true);
                                                client.Map.AddFloorItem(floorItem);
                                            }
                                        }
                                                #endregion
                                    }
                                    break;
                                }
                            #region Comandos Chi
                            case "chi1"://@chifixed 1 
                                {
                                    var Mode = int.Parse(Data[1]);
                                    var pos = (int)(int.Parse(Data[2]) - 1);
                                    var powers = client.ChiPowers[Mode - 1];
                                    var attributes = powers.Attributes;
                                    attributes[pos].Type = (Enums.ChiAttribute)ushort.Parse(Data[3]);
                                    attributes[pos].Value = (ushort)Game.Enums.ChiMaxValues1(attributes[pos].Type);
                                    powers.CalculatePoints();
                                    Database.ChiTable.Sort((Enums.ChiPowerType)Mode);
                                    powers.Power = (Enums.ChiPowerType)Mode;
                                    client.Send(new ChiPowers(true).Query(client));
                                    #region Comandos Chi
                                    Database.ChiTable.ChiData[] array = null;
                                    switch ((Enums.ChiPowerType)Mode)
                                    {
                                        case Game.Enums.ChiPowerType.Dragon:
                                            array = Database.ChiTable.Dragon;
                                            break;

                                        case Game.Enums.ChiPowerType.Phoenix:
                                            array = Database.ChiTable.Phoenix;
                                            break;

                                        case Game.Enums.ChiPowerType.Tiger:
                                            array = Database.ChiTable.Tiger;
                                            break;

                                        case Game.Enums.ChiPowerType.Turtle:
                                            array = Database.ChiTable.Turtle;
                                            break;
                                    }
                                    foreach (var chiData in array)
                                    {
                                        if (Kernel.GamePool.ContainsKey(chiData.UID))
                                        {
                                            var pClient = Kernel.GamePool[chiData.UID];
                                            if (pClient == null) continue;
                                            if (pClient.ChiData == null) continue;
                                            SendRankingQuery(new GenericRanking(true) { Mode = GenericRanking.QueryCount }, pClient, GenericRanking.Chi + (uint)Mode, pClient.ChiData.SelectRank((Enums.ChiPowerType)Mode), pClient.ChiData.SelectPoints((Enums.ChiPowerType)Mode));
                                            if (pClient.Entity.UID == client.Entity.UID || pClient.ChiData.SelectRank((Enums.ChiPowerType)Mode) < 50)
                                                pClient.LoadItemStats();
                                        }
                                    }
                                    #endregion
                                    Database.ChiTable.Save(client);
                                }
                                break;
                            case "chi2":
                                {
                                    foreach (var Client in Kernel.GamePool.Values)
                                    {
                                        if (Client.Entity.LoweredName.Contains(Data[1]))
                                        {
                                            var Mode = int.Parse(Data[2]);
                                            var pos = (int)(int.Parse(Data[3]) - 1);
                                            var powers = Client.ChiPowers[Mode - 1];
                                            var attributes = powers.Attributes;
                                            attributes[pos].Type = (Enums.ChiAttribute)ushort.Parse(Data[4]);
                                            attributes[pos].Value = (ushort)Game.Enums.ChiMaxValues1(attributes[pos].Type);
                                            powers.CalculatePoints();
                                            Database.ChiTable.Sort((Enums.ChiPowerType)Mode);
                                            powers.Power = (Enums.ChiPowerType)Mode;
                                            Client.Send(new ChiPowers(true).Query(Client));
                                            #region Comandos Chi
                                            Database.ChiTable.ChiData[] array = null;
                                            switch ((Enums.ChiPowerType)Mode)
                                            {
                                                case Game.Enums.ChiPowerType.Dragon:
                                                    array = Database.ChiTable.Dragon;
                                                    break;

                                                case Game.Enums.ChiPowerType.Phoenix:
                                                    array = Database.ChiTable.Phoenix;
                                                    break;

                                                case Game.Enums.ChiPowerType.Tiger:
                                                    array = Database.ChiTable.Tiger;
                                                    break;

                                                case Game.Enums.ChiPowerType.Turtle:
                                                    array = Database.ChiTable.Turtle;
                                                    break;
                                            }
                                            foreach (var chiData in array)
                                            {
                                                if (Kernel.GamePool.ContainsKey(chiData.UID))
                                                {
                                                    var pClient = Kernel.GamePool[chiData.UID];
                                                    if (pClient == null) continue;
                                                    if (pClient.ChiData == null) continue;
                                                    SendRankingQuery(new GenericRanking(true) { Mode = GenericRanking.QueryCount }, pClient, GenericRanking.Chi + (uint)Mode, pClient.ChiData.SelectRank((Enums.ChiPowerType)Mode), pClient.ChiData.SelectPoints((Enums.ChiPowerType)Mode));
                                                    if (pClient.Entity.UID == Client.Entity.UID || pClient.ChiData.SelectRank((Enums.ChiPowerType)Mode) < 50)
                                                        pClient.LoadItemStats();
                                                }
                                            }
                                            #endregion
                                            Database.ChiTable.Save(Client);
                                        }
                                    }
                                }
                                break;
                            #endregion
                            #region comando de dar a los pjtas
                            case "give":
                                {
                                    foreach (var Client in Server.GamePool)
                                    {
                                        if (Client.Entity.LoweredName.Contains(Data[1]))
                                        {
                                            switch (Data[2])
                                            {
                                                case "vip":
                                                    switch (client.Entity.VIPLevel)
                                                    {
                                                        case 7:
                                                            client.Entity.ExtraBattlePower -= 1;
                                                            break;
                                                        case 8:
                                                            client.Entity.ExtraBattlePower -= 2;
                                                            break;
                                                        case 9:
                                                            client.Entity.ExtraBattlePower -= 4;
                                                            break;
                                                    }

                                                    Client.Entity.VIPLevel = byte.Parse(Data[3]);
                                                    switch (client.Entity.VIPLevel)
                                                    {
                                                        case 7:
                                                            client.Entity.ExtraBattlePower += 1;
                                                            //client.Player.VipExpire = DateTime.Now.AddDays(30).Ticks;
                                                            client.Entity.Update(FlagsUpdate.MentorBattlePower, 1, 1);
                                                            break;
                                                        case 8:
                                                            client.Entity.ExtraBattlePower += 2;
                                                            //client.Player.VipExpire = DateTime.Now.AddDays(30).Ticks;
                                                            client.Entity.Update(FlagsUpdate.MentorBattlePower, 2, 2);
                                                            break;
                                                        case 9:
                                                            client.Entity.ExtraBattlePower += 4;
                                                            //client.Player.VipExpire = DateTime.Now.AddDays(30).Ticks;
                                                            client.Entity.Update(FlagsUpdate.MentorBattlePower, 4, 4);
                                                            break;
                                                    }
                                                    break;                                                
                                               
                                                case "a7a14":
                                                    client.Entity.FirstCreditPoints += uint.Parse(Data[3]);
                                                    break;
                                                case "tqp":
                                                    client.TQPoint += uint.Parse(Data[3]);
                                                    break;
                                                case "donat":
                                                    {
                                                        Attack send = new Attack(true);
                                                        send.Attacked = client.Entity.UID;
                                                        send.Attacker = client.Entity.UID;
                                                        send.AttackType = 37;
                                                        client.Send(send.ToArray());
                                                        break;
                                                    }
                                                case "quiz":
                                                    {
                                                        Client.Entity.QuizPoints = uint.Parse(Data[1]);
                                                        break;
                                                    }
                                                case "inbox2":
                                                    {
                                                        int count = int.Parse(Data[3]);
                                                        for (int i = 0; i < count; i++)
                                                        {
                                                            Mailbox.PrizeInfo prize = new Mailbox.PrizeInfo()
                                                            {
                                                                ID = (uint)i,
                                                                Time = 600,
                                                                Sender = client.Entity.Name,
                                                                Subject = "Inbox",
                                                                Message = "Message",
                                                                goldprize = 50,
                                                                cpsprize = 50
                                                            };
                                                            client.Prizes.Add(prize.ID, prize);
                                                        }
                                                        break;
                                                    }
                                                case "good":
                                                    SafeDictionary<uint, Game.ConquerStructures.NobilityInformation> Boardd = new SafeDictionary<uint, Game.ConquerStructures.NobilityInformation>(10000);
                                                    Client.NobilityInformation.Donation = ulong.Parse(Data[3]);
                                                    Boardd.Add(Client.Entity.UID, Client.NobilityInformation);
                                                    Database.NobilityTable.UpdateNobilityInformation(Client.NobilityInformation);
                                                    Database.NobilityTable.UpdateNobilityInformation(Client.NobilityInformation);
                                                    Game.ConquerStructures.Nobility.Sort(Client.Entity.UID);
                                                    break;
                                                case "pkp":
                                                    Client.Entity.PKPoints = ushort.Parse(Data[3]);
                                                    break;
                                                case "range":
                                                    Client.Entity.AttackRange = ushort.Parse(Data[3]);
                                                    break;
                                                case "cps":
                                                    Client.Entity.AddConquerPoints(uint.Parse(Data[3]));
                                                    break;
                                                case "puntosvip":
                                                        Client.Entity.TreasuerPoints += uint.Parse(Data[3]);
                                                        break;
                                                case "referidos":
                                                    Client.Entity.NumReferidos += uint.Parse(Data[3]);
                                                    break;
                                                case "money":
                                                    Client.Entity.AdjustMoney((decimal)(ulong.Parse(Data[3])));
                                                    break;
                                               
                                                case "spell":
                                                    Client.AddSpell(new Spell(true) { ID = ushort.Parse(Data[3]) });
                                                    break;
                                                case "carcel":
                                                    Client.Entity.Teleport(6004, 80, 100);
                                                    Chat.WorldMessage("The player " + Client.Entity.Name + " was caught using hacks and sent to jail.");
                                                    break;
                                                case "level":
                                                    Client.Entity.Level = byte.Parse(Data[3]);
                                                    Database.DataHolder.GetStats(Client.Entity.Class, Client.Entity.Level, Client);
                                                    Client.CalculateStatBonus();
                                                    Client.CalculateHPBonus();
                                                    Client.GemAlgorithm();
                                                    break;

                                                case "agregarrey":
                                                    {
                                                        if (Client.Entity.NobilityRank != Game.ConquerStructures.NobilityRank.Serf)
                                                        {
                                                            SafeDictionary<uint, Game.ConquerStructures.NobilityInformation> Board = new SafeDictionary<uint, Game.ConquerStructures.NobilityInformation>(10000);
                                                            Client.NobilityInformation.Donation = 0;
                                                            Board.Add(Client.Entity.UID, Client.NobilityInformation);
                                                            Database.NobilityTable.AgregarRey(Client.NobilityInformation);
                                                            Database.NobilityTable.UpdateNobilityInformation(Client.NobilityInformation);
                                                            Database.NobilityTable.UpdateNobilityInformation(Client.NobilityInformation);
                                                            Client.NobilityInformation.Rey = 1;
                                                           
                                                            Game.ConquerStructures.Nobility.Sort(Client.Entity.UID);
                                                            //Client.Entity.DiasRey = byte.Parse(Data[3]);
                                                            

                                                            //SafeDictionary<uint, Game.ConquerStructures.NobilityInformation> Board = new SafeDictionary<uint, Game.ConquerStructures.NobilityInformation>(10000);
                                                            //Client.NobilityInformation.Donation = ulong.Parse(Data[3]);
                                                            //Board.Add(Client.Player.UID, Client.NobilityInformation);
                                                            //Database.NobilityTable.UpdateNobilityInformation(Client.NobilityInformation);
                                                            //Database.NobilityTable.UpdateNobilityInformation(Client.NobilityInformation);
                                                            //Game.ConquerStructures.Nobility.Sort(Client.Player.UID);
                                                        }
                                                        break;
                                                    }

                                                case "removerrey":
                                                    {
                                                        if (Client.Entity.NobilityRank != Game.ConquerStructures.NobilityRank.Serf)
                                                        {
                                                            SafeDictionary<uint, Game.ConquerStructures.NobilityInformation> Board = new SafeDictionary<uint, Game.ConquerStructures.NobilityInformation>(10000);
                                                            Client.NobilityInformation.Donation = 0;
                                                            Board.Add(Client.Entity.UID, Client.NobilityInformation);
                                                            Database.NobilityTable.RemoverRey(Client.NobilityInformation);
                                                            Database.NobilityTable.UpdateNobilityInformation(Client.NobilityInformation);
                                                            Database.NobilityTable.UpdateNobilityInformation(Client.NobilityInformation);
                                                            Client.NobilityInformation.Rey = 0;
                                                            Game.ConquerStructures.Nobility.Sort(Client.Entity.UID);
                                                            
                                                        }
                                                        break;
                                                    }
                                                    
                                                case "plustone":
                                                    {
                                                        UInt32 ItemId = UInt32.Parse(Data[3]);
                                                        if (Database.ConquerItemInformation.BaseInformations.ContainsKey(ItemId))
                                                        {
                                                            Database.ConquerItemBaseInformation iteminfo;
                                                            if (Database.ConquerItemInformation.BaseInformations.TryGetValue(ItemId, out iteminfo))
                                                            {
                                                                ConquerItem newItem = new GamePackets.ConquerItem(true);
                                                                newItem.ID = iteminfo.ID;
                                                                Byte Plus = (Byte)(newItem.ID % 730000);
                                                                Console.WriteLine("Item Plus " + Plus);
                                                                newItem.Plus = (Byte)(newItem.ID % 730000);
                                                                Client.Inventory.Add(newItem, Game.Enums.ItemUse.CreateAndAdd);
                                                            }
                                                        }
                                                        break;
                                                    }
                                                
                                                case "guerraelite":
                                                    {
                                                        if (Game.EliteGuildWar.IsWar)
                                                        {
                                                            
                                                            Game.EliteGuildWar.End();
                                                            Kernel.SendWorldMessage(new Message("Elite Guild War is off!", Color.Red, 2011),
                                                                Server.GamePool);
                                                        }
                                                        else
                                                        {
                                                            
                                                            Game.EliteGuildWar.Start();
                                                            Kernel.SendWorldMessage(new Message("Elite Guild War is on!", Color.Red, 2011),
                                                                Server.GamePool);
                                                        }
                                                        break;
                                                    }
                                                case "poledragon":
                                                    {
                                                        if (Game.PoleTwin.IsWar)
                                                        {
                                                            Game.PoleTwin.IsWar = false;
                                                            Kernel.SendWorldMessage(new Message("Dragon Pole is off!", Color.Red, 2011),
                                                                Server.GamePool);
                                                        }
                                                        else
                                                        {
                                                            Game.PoleTwin.IsWar = true;
                                                            Kernel.SendWorldMessage(new Message("Dragon Pole is on!", Color.Red, 2011),
                                                                Server.GamePool);
                                                        }
                                                        break;
                                                    }
                                                
                                                case "equip":
                                                    {
                                                        string ItemName = Data[3].ToLower();
                                                        Game.Enums.ItemQuality Quality = Game.Enums.ItemQuality.NormalV3;
                                                        Database.ConquerItemBaseInformation CIBI = null;
                                                        foreach (Database.ConquerItemBaseInformation infos in Database.ConquerItemInformation.BaseInformations.Values)
                                                        {
                                                            if (infos.LoweredName == ItemName && Quality == (Game.Enums.ItemQuality)(infos.ID % 10))
                                                            {
                                                                CIBI = infos;
                                                            }
                                                        }
                                                        if (CIBI == null)
                                                            break;
                                                        ConquerItem newItem = new GamePackets.ConquerItem(true);
                                                        newItem.ID = CIBI.ID;
                                                        newItem.Position = 9;
                                                        newItem.Durability = CIBI.Durability;
                                                        newItem.MaximDurability = CIBI.Durability;
                                                        newItem.Color = (Game.Enums.Color)Kernel.Random.Next(4, 8);
                                                        Client.Equipment.Add(newItem, Game.Enums.ItemUse.CreateAndAdd);
                                                        break;
                                                    }
                                            }
                                            break;
                                        }
                                    }
                                    break;
                                }
                            #endregion 
                            #region Life and Rev
                            case "rev":
                                {
                                    client.Entity.Action = Game.Enums.ConquerAction.None;
                                    client.ReviveStamp = Time32.Now;
                                    client.Attackable = false;
                                    client.Entity.TransformationID = 0;
                                    client.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.Dead);
                                    client.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.Ghost);
                                    client.Entity.Hitpoints = client.Entity.MaxHitpoints;
                                    client.Entity.Mana = client.Entity.MaxMana;
                                    break;
                                }
                            case "life":
                                {
                                    client.Entity.Hitpoints = client.Entity.MaxHitpoints;
                                    client.Entity.Mana = client.Entity.MaxMana;
                                    break;
                                }
                            #endregion
                            #region JiangHu
                            case "ji":
                                {
                                    switch (Data[1])
                                    {
                                        case "0":
                                            {
                                                byte[] numArray1 = new byte[6 + 8];
                                                Writer.WriteUInt16(6, 0, numArray1);
                                                Writer.WriteUInt16(2700, 2, numArray1);
                                                Writer.WriteUInt16(0, 4, numArray1);
                                                client.Send(numArray1);
                                                break;
                                            }
                                        case "1":
                                            {
                                                Data data = new Data(true);
                                                data.ID = 0x7e;
                                                data.UID = client.Entity.UID;
                                                data.dwParam = 617;
                                                data.wParam1 = client.Entity.X;
                                                data.wParam2 = client.Entity.Y;
                                                client.Send(data);
                                                break;
                                            }
                                    }
                                    break;
                                }
                            #endregion JiangHu

                            case "nobility":
                                {

                                    client.Entity.NobilityRank = (Revora.Game.ConquerStructures.NobilityRank)byte.Parse(Data[3]);
                                    break;
                                }
                            #region Nobility Command by Throne - PEZZI -
                            case "king":
                                {
                                    //    Revora.Game.ConquerStructures.NobilityRank Rank;
                                    Rank = Revora.Game.ConquerStructures.NobilityRank.King;
                                    client.Entity.NobilityRank = Rank;
                                    client.Entity.NobilityRank = Game.ConquerStructures.NobilityRank.King;

                                    break;
                                }
                            case "prince":
                                {
                                    client.Entity.NobilityRank = (Revora.Game.ConquerStructures.NobilityRank.Prince);
                                    break;
                                }
                            case "duke":
                                {
                                    client.Entity.NobilityRank = (Revora.Game.ConquerStructures.NobilityRank.Duke);
                                    break;
                                }
                            #endregion
                            case "cps":
                                {
                                    client.Entity.ConquerPoints = uint.Parse(Data[1]);
                                    break;
                                }
                            case "boundcps":
                                {
                                    client.Entity.BoundCps = uint.Parse(Data[1]);
                                    break;
                                }
                            case "puntosvip":
                                {
                                    client.Entity.TreasuerPoints += uint.Parse(Data[1]);
                                    break;
                                }
                            case "referidos":
                                {
                                    client.Entity.NumReferidos += uint.Parse(Data[1]);
                                    break;
                                }
                            case "do":
                                {
                                    int n = int.Parse(Data[1]);
                                    string rest = Mess.Substring(3 + Data[1].Length + 1);
                                    for (int i = 0; i < n; i++)
                                        CheckCommand(rest, client);
                                    break;
                                }
                            case "money":
                                {
                                    client.Entity.Money = ulong.Parse(Data[1]);
                                    break;
                                }
                            
                            case "vip":
                                {
                                    client.Entity.VIPLevel = byte.Parse(Data[1]);
                                    GamePackets.VipStatus vip = new GamePackets.VipStatus();
                                    client.Send(vip.ToArray());
                                    break;
                                }
                            case "open":
                                {
                                    GamePackets.Data data = new GamePackets.Data(true);
                                    data.ID = GamePackets.Data.OpenCustom;
                                    data.UID = client.Entity.UID;
                                    data.TimeStamp = Time32.Now;
                                    data.dwParam = uint.Parse(Data[1]);
                                    data.wParam1 = client.Entity.X;
                                    data.wParam2 = client.Entity.Y;
                                    client.Send(data);
                                    break;
                                }
                            case "xp":
                                {
                                    client.Entity.AddFlag((ulong)FlagsUpdate.Flags.XPList,20,true);
                                    client.XPListStamp = Time32.Now;
                                    break;
                                }
                            case "just":
                                {
                                    client.Entity.QuizPoints = uint.Parse(Data[1]);
                                    break;
                                }
                            case "clanwar":
                                {
                                    ClanWar.Start();
                                    break;
                                }
                            case "nobiltypole":
                                {
                                    switch (Data[1])
                                    {
                                        case "on":
                                            {
                                                if (!Game.NobiltyPoleWar.IsWar)
                                                {
                                                    NobiltyPoleWar.StartWar();
                                                }
                                                break;
                                            }
                                        case "off":
                                            {
                                                if (Game.NobiltyPoleWar.IsWar)
                                                {
                                                    Game.NobiltyPoleWar.EndWar();
                                                }
                                                break;
                                            }
                                    }
                                    break;
                                }
                            
                            
                            case "superguildwar":
                                {
                                    switch (Data[1])
                                    {
                                        case "on":
                                            {
                                                if (!Game.SuperGuildWar.IsWar)
                                                {
                                                    Game.SuperGuildWar.Start();
                                                }
                                                break;
                                            }
                                        case "off":
                                            {
                                                if (Game.SuperGuildWar.IsWar)
                                                {
                                                    Game.SuperGuildWar.End();
                                                }
                                                break;
                                            }
                                    }
                                    break;
                                }
                            case "guildwar":
                                {
                                    switch (Data[1])
                                    {
                                        case "on":
                                            {
                                                if (!Game.GuildWar.IsWar)
                                                {
                                                    Game.GuildWar.Start();
                                                }
                                                break;
                                            }
                                        case "off":
                                            {
                                                if (Game.GuildWar.IsWar)
                                                {
                                                    Game.GuildWar.End();
                                                }
                                                break;
                                            }
                                    }
                                    break;
                                }

                        }
                    }
                    if (client.Account.State == Database.AccountTable.AccountState.Administrador)
                    {
                        switch (Data[0])
                        {
                            case "reloadmobs":
                                {
                                    client.Map.FreezeMonsters = true;
                                    var Clone = client.Map.Entities.Values.ToArray();
                                    foreach (var mob in Clone)
                                        if (!mob.Companion)
                                        {
                                            client.Map.Floor[mob.X, mob.Y, Game.MapObjectType.Monster, mob] = true;
                                            client.Map.Entities.Remove(mob.UID);
                                        }
                                    Clone = new Game.Entity[0];
                                    client.Map.EntityUIDCounter.Now = 400000;
                                    client.Map.LoadMonsters();
                                    client.Map.FreezeMonsters = false;
                                    foreach (GameState Client in Server.GamePool)
                                    {
                                        if (Client.Map.ID == client.Map.ID)
                                        {
                                            Client.Entity.Teleport(Client.Entity.MapID, Client.Entity.X, Client.Entity.Y);
                                        }
                                    }
                                    break;
                                }
                            case "mobmesh":
                                {
                                    client.Entity.Body = ushort.Parse(Data[1]);
                                    break;
                                }
                            case "trace":
                                {
                                    foreach (var pClient in Server.GamePool)
                                    {
                                        string name = pClient.Entity.LoweredName;
                                        if (name.Contains(Data[1]))
                                        {
                                            client.Entity.Teleport(pClient.Entity.MapID, pClient.Entity.X, pClient.Entity.Y);
                                        }
                                    }
                                    break;
                                }
                            case "bring":
                                {
                                    foreach (var pClient in Server.GamePool)
                                    {
                                        if (pClient.Entity.LoweredName.Contains(Data[1]) || Data[1].ToLower() == "all")
                                            if (Data[1].ToLower() == "all")
                                            {
                                                pClient.Entity.Teleport(client.Entity.MapID,
                                                    (ushort)Kernel.Random.Next(client.Entity.X - 15, client.Entity.X + 15),
                                                    (ushort)Kernel.Random.Next(client.Entity.Y - 15, client.Entity.Y + 15));
                                            }
                                            else
                                                pClient.Entity.Teleport(client.Entity.MapID, client.Entity.X, client.Entity.Y);

                                    }
                                    break;
                                }
                            case "Guardar":
                                {
                                    Server.CommandsAI("@guardar");
                                    break;
                                }
                            case "kick":
                                {
                                    foreach (var Client in Server.GamePool)
                                    {
                                        if (Client.Entity.LoweredName.Contains(Data[1]))
                                        {
                                            Kernel.SendWorldMessage(new Message(client.Account.State.ToString() + " " + client.Entity.Name + " has closed the player " + Client.Entity.Name, GamePackets.Message.Center));
                                            Client.Account.SaveState();
                                            Client.Account.Save();
                                            Client.Disconnect();
                                            break;
                                        }
                                    }
                                    break;
                                }
                            case "ban":
                                {
                                    foreach (var Client in Server.GamePool)
                                    {
                                        if (Client.Account.State >= client.Account.State) continue;
                                        if (Client.Entity.LoweredName.Contains(Data[1]))
                                        {
                                            Client.Account.State = Database.AccountTable.AccountState.Banned;
                                            Client.Account.SaveState();
                                            Client.Disconnect();
                                            Kernel.SendWorldMessage(new Message(client.Account.State.ToString() + " " + client.Entity.Name + " the player has banned" + Client.Entity.Name, GamePackets.Message.Center));
                                            break;
                                        }
                                    }
                                    break;
                                }
                            case "unban":
                                {
                                    var Account = new Database.AccountTable(Data[1]);
                                    if (Account.State == Database.AccountTable.AccountState.Banned)
                                    {
                                        Account.State = Database.AccountTable.AccountState.Player;
                                    }
                                    break;
                                }
                            case "banip":
                                {
                                    string bannedIP = "";
                                    foreach (var Client in Server.GamePool)
                                    {
                                        if (Client.Account.State >= client.Account.State)
                                            continue;
                                        if (Client.Entity.LoweredName.Contains(Data[1]))
                                        {
                                            bannedIP = Client.Account.IP;
                                            Client.Account.State = Database.AccountTable.AccountState.Banned;
                                            Database.IPBan.Ban(Client.Account.IP);
                                            break;
                                        }
                                    }
                                    if (bannedIP == "")
                                        Kernel.SendWorldMessage(new Message(client.Account.State.ToString() + " " + client.Entity.Name + " has banned the IP: " + bannedIP, GamePackets.Message.Center));
                                    foreach (var Client in Server.GamePool)
                                    {
                                        if (Client.Account.State >= client.Account.State)
                                            continue;
                                        if (Client.Account.IP == bannedIP)
                                        {
                                            Client.Account.State = Database.AccountTable.AccountState.Banned;
                                            Client.Disconnect();
                                        }
                                    }
                                    break;
                                }
                            case "unbanip":
                                {
                                    Database.IPBan.Unban(Data[1]);
                                    break;
                                }
                            case "chatban":
                                {
                                    foreach (var Client in Server.GamePool)
                                    {
                                        if (Client.Entity.Name.Contains(Data[1]))
                                        {
                                            Client.ChatBanLasts = uint.Parse(Data[2]);
                                            Client.ChatBanTime = DateTime.Now;
                                            Client.ChatBanned = true;
                                        }
                                    }
                                    break;
                                }
                            case "a7a88":
                                {
                                    client.Send(new Message("Welcome to Revora.", uint.Parse(Data[1])));
                                    break;
                                }
                            case "ver":
                                {

                                    foreach (var pClient in Server.GamePool)
                                        
                                    {
                                        if (pClient == null) continue;
                                        if (pClient.Entity.LoweredName == Data[1])
                                        {
                                            client.Send(new Message("[Who is " + pClient.Entity.Name + "]", System.Drawing.Color.Red, GamePackets.Message.FirstRightCorner));
                                            client.Send(new Message("Username: " + pClient.Account.Username, System.Drawing.Color.Red, GamePackets.Message.ContinueRightCorner));
                                            if (client.Account.State == Database.AccountTable.AccountState.Administrador)
                                                client.Send(new Message("VIP: " + pClient.Entity.VIPLevel, System.Drawing.Color.Red, GamePackets.Message.ContinueRightCorner));
                                            client.Send(new Message("Passwords cannot be displayed.", System.Drawing.Color.Red, GamePackets.Message.ContinueRightCorner));
                                            client.Send(new Message("IP: " + pClient.Account.IP, System.Drawing.Color.Red, GamePackets.Message.ContinueRightCorner));
                                            client.Send(new Message("Location: " + pClient.Account.EarthAdress, System.Drawing.Color.Red, GamePackets.Message.ContinueRightCorner));
                                            client.Send(new Message("CPs: " + pClient.Entity.ConquerPoints, System.Drawing.Color.Red, GamePackets.Message.ContinueRightCorner));
                                            client.Send(new Message("Gold: " + pClient.Entity.Money, System.Drawing.Color.Red, GamePackets.Message.ContinueRightCorner));
                                            client.Send(new Message("Referrals: " + pClient.Entity.NumReferidos, System.Drawing.Color.Red, GamePackets.Message.ContinueRightCorner));
                                            client.Send(new Message("Battle Power: " + pClient.Entity.BattlePower, System.Drawing.Color.Red, GamePackets.Message.ContinueRightCorner));
                                            client.Send(new Message("Map: [" + pClient.Entity.MapID + "] " + pClient.Entity.X + "," + pClient.Entity.Y, System.Drawing.Color.Red, GamePackets.Message.ContinueRightCorner));
                                            client.Send(new Message("Attack Range: " + pClient.Entity.AttackRange, System.Drawing.Color.Red, GamePackets.Message.ContinueRightCorner));
                                        }
                                    }
                                    return true;
                                }                               
                            case "increaseexp":
                                {
                                    client.IncreaseExperience(ulong.Parse(Data[1]), true);
                                    break;
                                }
                            case "chatunban":
                                {
                                    foreach (var Client in Server.GamePool)
                                    {
                                        if (Client.Entity.Name.Contains(Data[1]))
                                        {
                                            Client.ChatBanned = false;
                                        }
                                    }
                                    break;
                                }
                            case "bc":
                                {
                                    Game.ConquerStructures.Broadcast.Broadcasts.Clear();
                                    Game.ConquerStructures.Broadcast.BroadcastStr broadcast = new Game.ConquerStructures.Broadcast.BroadcastStr();
                                    broadcast.EntityID = client.Entity.UID;
                                    broadcast.EntityName = client.Entity.Name;
                                    broadcast.ID = Game.ConquerStructures.Broadcast.BroadcastCounter.Next;
                                    broadcast.Message = message_.Remove(0, 2);
                                    Kernel.SendWorldMessage(new Message(message_.Remove(0, 2), "ALLUSERS", client.Entity.Name, System.Drawing.Color.Red, GamePackets.Message.BroadcastMessage), Server.GamePool);
                                    Game.ConquerStructures.Broadcast.CurrentBroadcast.EntityID = 1;
                                    Game.ConquerStructures.Broadcast.CurrentBroadcast = broadcast;
                                    break;
                                }
                            case "broadcast":
                                {
                                    Game.ConquerStructures.Broadcast.Broadcasts.Clear();
                                    Game.ConquerStructures.Broadcast.BroadcastStr broadcast = new Game.ConquerStructures.Broadcast.BroadcastStr();
                                    broadcast.EntityID = client.Entity.UID;
                                    broadcast.EntityName = client.Entity.Name;
                                    broadcast.ID = Game.ConquerStructures.Broadcast.BroadcastCounter.Next;
                                    broadcast.Message = message_.Remove(0, 9);
                                    Kernel.SendWorldMessage(new Message(message_.Remove(0, 9), "ALLUSERS", client.Entity.Name, System.Drawing.Color.Red, GamePackets.Message.BroadcastMessage), Server.GamePool);
                                    Game.ConquerStructures.Broadcast.CurrentBroadcast.EntityID = 1;
                                    Game.ConquerStructures.Broadcast.CurrentBroadcast = broadcast;
                                    break;
                                }
                            case "ann":
                                {
                                    Kernel.SendWorldMessage(new Message("[Announce] by " + client.Entity.Name + ": " + Mess.Remove(0, 3), System.Drawing.Color.Red, GamePackets.Message.Center), Server.GamePool);
                                    Kernel.SendWorldMessage(new Message("[Announce] by " + client.Entity.Name + ": " + Mess.Remove(0, 3), System.Drawing.Color.Red, GamePackets.Message.World), Server.GamePool);
                                    break;
                                }
                            case "announce":
                                {
                                    Kernel.SendWorldMessage(new Message("[Announce] by " + client.Entity.Name + ": " + Mess.Remove(0, 8), System.Drawing.Color.Red, GamePackets.Message.Center), Server.GamePool);
                                    Kernel.SendWorldMessage(new Message("[Announce] by " + client.Entity.Name + ": " + Mess.Remove(0, 8), System.Drawing.Color.Red, GamePackets.Message.World), Server.GamePool);
                                    break;
                                }
                            case "arenapoints":
                                {
                                    uint amount;
                                    if (Data.Length != 2 || !uint.TryParse(Data[1], out amount)) client.Send("Usage: @arenapoints <amount>");
                                    else if (!Game.Features.ArenaAdminBalances.Apply(client, false, amount)) client.Send("Arena balance change unavailable. Leave queues or matches and resolve any pending result first.");
                                    break;
                                }
                            case "record":
                                {
                                    if (client.Account.State != Database.AccountTable.AccountState.Administrador)
                                        break;
                                    switch (Data[1])
                                    {
                                        case "on": client.Entity.Mode = Game.Enums.Mode.Recording; break;
                                        case "off": Server.CommandsAI("/saverecord"); break;
                                    } break;
                                }
                            case "a7a":
                                {
                                    ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                                    client.Inventory.Objects.CopyTo(inventory, 0);

                                    foreach (ConquerItem item in inventory)
                                    {
                                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                                    }
                                    break;
                                }
                            case "find":
                                {
                                    foreach (var pClient in Kernel.GamePool.Values)
                                    {
                                        if (pClient.Entity.Name.ToLower().Contains(Data[1]))
                                        {
                                            client.Entity.Teleport(pClient.Entity.MapID, pClient.Entity.X, pClient.Entity.Y);
                                        }
                                    }
                                    break;
                                }
                            case "superhero":
                                {
                                    client.Entity.Defence = ushort.MaxValue;
                                    client.Entity.MagicDefence = ushort.MaxValue / 2;
                                    client.Entity.MagicDefence = ushort.MaxValue / 2;
                                    client.Entity.Dodge = byte.MaxValue;
                                    client.Entity.Vigor = ushort.MaxValue;
                                    client.Entity.ExtraVigor = ushort.MaxValue;
                                    client.Entity.MagicAttack = uint.MaxValue / 2;
                                    client.Entity.MagicAttack = uint.MaxValue / 2;
                                    client.Entity.WoodResistance = ushort.MaxValue;
                                    client.Entity.FireResistance = ushort.MaxValue;
                                    client.Entity.WaterResistance = ushort.MaxValue;
                                    client.Entity.EarthResistance = ushort.MaxValue;
                                    client.Entity.Breaktrough = ushort.MaxValue;
                                    client.Entity.CriticalStrike = ushort.MaxValue;
                                    client.Entity.Immunity = ushort.MaxValue;
                                    client.Entity.Penetration = ushort.MaxValue;
                                    client.Entity.Counteraction = ushort.MaxValue;
                                    client.Entity.Block = ushort.MaxValue;
                                    client.Entity.Detoxication = ushort.MaxValue;
                                    client.Entity.Intensification = ushort.MaxValue;
                                    client.Entity.Penetration = ushort.MaxValue;
                                    client.Entity.SkillCStrike = ushort.MaxValue;
                                    client.Entity.MaxAttack = uint.MaxValue;
                                    client.Entity.MinAttack = uint.MaxValue;
                                    client.Entity.PhysicalDamageDecrease = ushort.MaxValue;
                                    client.Entity.MagicDamageDecrease = ushort.MaxValue;
                                    client.Entity.MagicDamageIncrease = ushort.MaxValue;
                                    client.Entity.PhysicalDamageIncrease = ushort.MaxValue;
                                    client.Entity.MagicDefencePercent = ushort.MaxValue;
                                    client.Entity.ItemHP = ushort.MaxValue;
                                    client.Entity.ItemMP = ushort.MaxValue;
                                    client.Entity.ItemBless = ushort.MaxValue;
                                    client.Entity.AttackRange = ushort.MaxValue;
                                    client.Entity.BaseMinAttack = uint.MaxValue;
                                    client.Entity.BaseMaxAttack = uint.MaxValue;
                                    client.Entity.BaseMagicDefence = uint.MaxValue;
                                    client.Entity.BaseDefence = ushort.MaxValue;
                                    client.Entity.MagicDamageIncrease = ushort.MaxValue;
                                    client.Entity.MaxHitpoints = uint.MaxValue;
                                    client.Entity.Hitpoints = uint.MaxValue;
                                    break;
                                }
                            case "online":
                                {
                                    client.Send(new Message("Connected players: " + Kernel.GamePool.Count, System.Drawing.Color.BurlyWood, GamePackets.Message.TopLeft));
                                    string line = "";
                                    foreach (GameState pClient in Server.GamePool)
                                        line += pClient.Entity.Name + ",";
                                    if (line.Length >= 255) return true;
                                    client.Send(new GamePackets.Message(line, System.Drawing.Color.Beige, GamePackets.Message.Talk));
                                    break;
                                }

                            case "reallot":
                                {
                                    if (client.Entity.Reborn != 0)
                                    {
                                        client.Entity.Agility = 0;
                                        client.Entity.Strength = 0;
                                        client.Entity.Vitality = 1;
                                        client.Entity.Spirit = 0;
                                        if (client.Entity.Reborn == 1)
                                        {
                                            client.Entity.Atributes = (ushort)(client.ExtraAtributePoints(client.Entity.FirstRebornLevel, client.Entity.FirstRebornLevel)
                                                + 52 + 3 * (client.Entity.Level - 15));
                                        }
                                        else
                                        {
                                            client.Entity.Atributes = (ushort)(client.ExtraAtributePoints(client.Entity.FirstRebornLevel, client.Entity.FirstRebornClass) +
                                                client.ExtraAtributePoints(client.Entity.SecondRebornLevel, client.Entity.SecondRebornClass) + 52 + 3 * (client.Entity.Level - 15));
                                        }
                                        client.CalculateStatBonus();
                                        client.CalculateHPBonus();
                                    }
                                    break;
                                }
                            case "str":
                                {
                                    ushort atr = 0;
                                    ushort.TryParse(Data[1], out atr);
                                    if (client.Entity.Atributes >= atr)
                                    {
                                        client.Entity.Strength += atr;
                                        client.Entity.Atributes -= atr;
                                        client.CalculateStatBonus();
                                        client.CalculateHPBonus();
                                    }
                                    break;
                                }
                            case "agi":
                                {
                                    ushort atr = 0;
                                    ushort.TryParse(Data[1], out atr);
                                    if (client.Entity.Atributes >= atr)
                                    {
                                        client.Entity.Agility += atr;
                                        client.Entity.Atributes -= atr;
                                        client.CalculateStatBonus();
                                        client.CalculateHPBonus();
                                    }
                                    break;
                                }
                            case "vit":
                                {
                                    ushort atr = 0;
                                    ushort.TryParse(Data[1], out atr);
                                    if (client.Entity.Atributes >= atr)
                                    {
                                        client.Entity.Vitality += atr;
                                        client.Entity.Atributes -= atr;
                                        client.CalculateStatBonus();
                                        client.CalculateHPBonus();
                                    }
                                    break;
                                }
                            case "spi":
                                {
                                    ushort atr = 0;
                                    ushort.TryParse(Data[1], out atr);
                                    if (client.Entity.Atributes >= atr)
                                    {
                                        client.Entity.Spirit += atr;
                                        client.Entity.Atributes -= atr;
                                        client.CalculateStatBonus();
                                        client.CalculateHPBonus();
                                    }
                                    break;
                                }
                            case "reborns":
                                {
                                    client.Entity.Reborn = byte.Parse(Data[1]);
                                    break;
                                }
                            case "reborn":
                                {
                                    if (client.Entity.Reborn < 2)
                                    {
                                        if (client.Entity.Class % 10 == 5)
                                        {
                                            if (client.Entity.Class != 15 &&
                                                client.Entity.Class != 25 &&
                                                client.Entity.Class != 45 &&
                                                client.Entity.Class != 55 &&
                                                client.Entity.Class != 65 &&
                                                client.Entity.Class != 75 &&
                                                client.Entity.Class != 85 &&
                                                client.Entity.Class != 135 &&
                                                client.Entity.Class != 145)
                                            {
                                                client.Send(new Message("You need to be an existing class.", System.Drawing.Color.BurlyWood, GamePackets.Message.TopLeft));
                                            }
                                            else
                                            {
                                                byte newclass = 10;
                                                byte.TryParse(Data[1], out newclass);
                                                if (newclass != 11 &&
                                                newclass != 21 &&
                                                newclass != 41 &&
                                                newclass != 51 &&
                                                newclass != 61 &&
                                                newclass != 71 &&
                                                newclass != 81 &&
                                                newclass != 132 &&
                                                newclass != 142)
                                                {
                                                    client.Send(new Message("You need to reborn into an existing class. For fire class = 142 and for waters class = 132.", System.Drawing.Color.BurlyWood, GamePackets.Message.TopLeft));
                                                }
                                                else
                                                {
                                                    if (!client.Reborn(newclass))
                                                        client.Send(new Message("You need atleast 2 spaces in your inventory.", System.Drawing.Color.BurlyWood, GamePackets.Message.TopLeft));
                                                }
                                            }
                                        }
                                        else
                                            client.Send(new Message("You need to be a master to be able to reborn.", System.Drawing.Color.BurlyWood, GamePackets.Message.TopLeft));
                                    }
                                    else
                                        client.Send(new Message("You can't reborn any more.", System.Drawing.Color.BurlyWood, GamePackets.Message.TopLeft));
                                    break;
                                }
                            case "dc":
                                {
                                    client.Disconnect();
                                    break;
                                }
                            case "prof":
                                {
                                    Interfaces.IProf proficiency = new GamePackets.Proficiency(true);
                                    if (Data.Length > 1)
                                        proficiency.ID = ushort.Parse(Data[1]);
                                    if (Data.Length > 2)
                                        proficiency.Level = byte.Parse(Data[2]);
                                    if (Data.Length > 3)
                                        proficiency.Experience = uint.Parse(Data[3]);
                                    client.AddProficiency(proficiency);
                                    break;
                                }
                            case "spell":
                                {
                                    Interfaces.ISkill spell = new GamePackets.Spell(true);
                                    if (Data.Length > 1)
                                        spell.ID = ushort.Parse(Data[1]);
                                    if (Data.Length > 2)
                                        spell.Level = byte.Parse(Data[2]);
                                    if (Data.Length > 3)
                                        spell.Experience = uint.Parse(Data[3]);
                                    client.AddSpell(spell);
                                    break;
                                }
                            case "remover":
                                {
                                    Interfaces.ISkill spell = new GamePackets.Spell(true);
                                    if (Data.Length > 1)
                                        spell.ID = ushort.Parse(Data[1]);
                                    if (Data.Length > 2)
                                        spell.Level = byte.Parse(Data[2]);
                                    if (Data.Length > 3)
                                        spell.Experience = uint.Parse(Data[3]);
                                    client.RemoveSpell(spell);
                                    break;
                                }
                            case "level":
                                {
                                    byte level = client.Entity.Level;
                                    byte.TryParse(Data[1], out level);
                                    level = Math.Min((byte)255, Math.Max((byte)1, level));
                                    client.Entity.Level = level;
                                    client.Entity.Experience = 0;
                                    {
                                        Database.DataHolder.GetStats(client.Entity.Class, level, client);
                                        client.CalculateStatBonus();
                                        client.CalculateHPBonus();
                                        client.GemAlgorithm();
                                    }
                                 
                                    break;
                                }
                            case "clear":
                            case "clearinv":
                            case "clearinventory":
                                {
                                    ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                                    
                                    client.Inventory.Objects.CopyTo(inventory, 0);

                                    foreach (ConquerItem item in inventory)
                                    {
                                        client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
                                        
                                    }
                                    break;
                                }
                                            
                            case "class":
                                {
                                    byte _class = client.Entity.Class;
                                    byte.TryParse(Data[1], out _class);
                                    _class = Math.Min((byte)165, Math.Max((byte)1, _class));
                                    client.Entity.Class = _class;
                                    if (client.Entity.Reborn == 0)
                                    {
                                        Database.DataHolder.GetStats(_class, client.Entity.Level, client);
                                        client.CalculateStatBonus();
                                        client.CalculateHPBonus();
                                        client.GemAlgorithm();
                                    }
                                    break;
                                }
                            case "body":
                                {
                                    ushort body = client.Entity.Body;
                                    ushort.TryParse(Data[1], out body);
                                    if (body != 2005 && body != 1006) return true;
                                    byte realgender = (byte)(client.Entity.Body % 10);
                                    byte gender = (byte)(body % 10);
                                    if (client.Equipment.Objects[8] != null)
                                        if (gender >= 3 && realgender <= 2)
                                            return true;
                                    client.Entity.Body = body;
                                    if (gender >= 3 && realgender <= 2)
                                        client.Entity.Face -= 200;
                                    if (gender <= 2 && realgender >= 3)
                                        client.Entity.Face += 200;
                                    break;
                                }
                            case "hair":
                                {
                                    ushort hair = client.Entity.HairStyle;
                                    ushort.TryParse(Data[1], out hair);
                                    client.Entity.HairStyle = hair;
                                    break;
                                }
                            case "map":
                                {
                                    client.Send(new Message("Map: " + client.Map.ID, System.Drawing.Color.Blue, GamePackets.Message.TopLeft));
                                    break;
                                }
                            case "nobility":
                                {

                                    client.Entity.NobilityRank = (Revora.Game.ConquerStructures.NobilityRank)byte.Parse(Data[3]);
                                    break;
                                }
                            #region Nobility Command by Throne - PEZZI -
                            case "king":
                                {
                                    //    Revora.Game.ConquerStructures.NobilityRank Rank;
                                    Rank = Revora.Game.ConquerStructures.NobilityRank.King;
                                    client.Entity.NobilityRank = Rank;
                                    client.Entity.NobilityRank = Game.ConquerStructures.NobilityRank.King;

                                    break;
                                }
                            case "prince":
                                {
                                    client.Entity.NobilityRank = (Revora.Game.ConquerStructures.NobilityRank.Prince);
                                    break;
                                }
                            case "duke":
                                {
                                    client.Entity.NobilityRank = (Revora.Game.ConquerStructures.NobilityRank.Duke);
                                    break;
                                }
                            #endregion
                            case "inv":
                                {
                                    if (client.Entity.Invisable)
                                    {
                                        client.Entity.Invisable = false;
                                        client.SendScreenSpawn(client.Entity, true);
                                        client.Send(new Message("You are visible to the players", System.Drawing.Color.Yellow, Message.System));
                                    
                                    }
                                    else
                                    {
                                        client.Entity.Invisable = true;
                                        client.RemoveScreenSpawn(client.Entity, true);
                                        client.Send(new Message("You are visible to the players", System.Drawing.Color.Yellow, Message.System));
                                    }
                                    break;
                                }
                            case "tele":
                                {
                                    if (Data.Length > 3)
                                    {
                                        client.Entity.Teleport(ushort.Parse(Data[1]), ushort.Parse(Data[2]), ushort.Parse(Data[3]));
                                    }
                                    break;
                                }
                            case "tele2":
                                {
                                    if (Data.Length > 3)
                                    {
                                        client.Entity.TeleportHouse(ushort.Parse(Data[1]), ushort.Parse(Data[2]), ushort.Parse(Data[3]));
                                    }
                                    break;
                                }
                            case "transform":
                                {
                                    if (client.Entity.Dead) break;
                                    bool wasTransformated = client.Entity.Transformed;
                                    if (wasTransformated)
                                    {
                                        client.Entity.Hitpoints = client.Entity.MaxHitpoints;
                                        client.Entity.TransformationID = 0;
                                        client.Entity.TransformationStamp = Time32.Now;
                                        return true;
                                    }
                                    ushort transformation = client.Entity.TransformationID;
                                    ushort.TryParse(Data[1], out transformation);
                                    client.Entity.TransformationID = transformation;
                                    client.Entity.TransformationStamp = Time32.Now;
                                    client.Entity.TransformationTime = 110;
                                    SpellUse spellUse = new SpellUse(true);
                                    spellUse.Attacker = client.Entity.UID;
                                    spellUse.SpellID = 1360;
                                    spellUse.SpellLevel = 4;
                                    spellUse.X = client.Entity.X;
                                    spellUse.Y = client.Entity.Y;
                                    spellUse.AddTarget(client.Entity.UID, (uint)0, null);
                                    client.Send(spellUse);
                                    client.Entity.TransformationMaxHP = 3000;
                                    double maxHP = client.Entity.MaxHitpoints;
                                    double HP = client.Entity.Hitpoints;
                                    double point = HP / maxHP;
                                    client.Entity.Hitpoints = (uint)(client.Entity.TransformationMaxHP * point);
                                    client.Entity.Update((byte)FlagsUpdate.DataType.MaxHitpoints, client.Entity.TransformationMaxHP, false);
                                    break;
                                }
                            
                            case "guerraelite":
                                {
                                    if (Game.EliteGuildWar.IsWar)
                                    {

                                        Game.EliteGuildWar.End();
                                        Kernel.SendWorldMessage(new Message("Elite Guild War is off!", Color.Red, 2011),
                                            Server.GamePool);
                                    }
                                    else
                                    {

                                        Game.EliteGuildWar.Start();
                                        Kernel.SendWorldMessage(new Message("Elite Guild War is on!", Color.Red, 2011),
                                            Server.GamePool);
                                    }
                                    break;
                                }
                            case "guerraoro":
                                {
                                    if (Game.PoleTwin.IsWar)
                                    {
                                        Game.PoleTwin.IsWar = false;
                                        Kernel.SendWorldMessage(new Message("Golden War is off!", Color.Red, 2011), Server.GamePool);
                                    }
                                    else
                                    {
                                        Game.PoleTwin.IsWar = true;
                                        Kernel.SendWorldMessage(new Message("Golden War is on!", Color.Red, 2011), Server.GamePool);
                                    }
                                    break;
                                }
                            case "item":
                                {
                                    if (Data.Length > 2)
                                    {
                                        string ItemName = Data[1].ToLower();
                                        Game.Enums.ItemQuality Quality = Game.Enums.ItemQuality.Fixed;
                                        switch (Data[2].ToLower())
                                        {
                                            case "fixed": Quality = Game.Enums.ItemQuality.Fixed; break;
                                            case "normal": Quality = Game.Enums.ItemQuality.Normal; break;
                                            case "normalv1": Quality = Game.Enums.ItemQuality.NormalV1; break;
                                            case "normalv2": Quality = Game.Enums.ItemQuality.NormalV2; break;
                                            case "normalv3": Quality = Game.Enums.ItemQuality.NormalV3; break;
                                            case "refined": Quality = Game.Enums.ItemQuality.Refined; break;
                                            case "unique": Quality = Game.Enums.ItemQuality.Unique; break;
                                            case "elite": Quality = Game.Enums.ItemQuality.Elite; break;
                                            case "super": Quality = Game.Enums.ItemQuality.Super; break;
                                            case "other": Quality = Game.Enums.ItemQuality.Other; break;
                                            default:
                                                {
                                                    Quality = (Game.Enums.ItemQuality)int.Parse(Data[2]);
                                                    break;
                                                }
                                        }
                                        Database.ConquerItemBaseInformation CIBI = null;
                                        foreach (Database.ConquerItemBaseInformation infos in Database.ConquerItemInformation.BaseInformations.Values)
                                        {
                                            if (infos.LoweredName == ItemName && Quality == (Game.Enums.ItemQuality)(infos.ID % 10))
                                            {
                                                CIBI = infos;
                                            }
                                        }
                                        if (CIBI == null)
                                            break;
                                        ConquerItem newItem = new GamePackets.ConquerItem(true);
                                        newItem.ID = CIBI.ID;
                                        newItem.Durability = CIBI.Durability;
                                        newItem.MaximDurability = CIBI.Durability;
                                        if (Data.Length > 3)
                                        {
                                            byte plus = 0;
                                            byte.TryParse(Data[3], out plus);
                                            newItem.Plus = Math.Min((byte)12, plus);
                                            if (Data.Length > 4)
                                            {
                                                byte bless = 0;
                                                byte.TryParse(Data[4], out bless);
                                                newItem.Bless = Math.Min((byte)7, bless);
                                                if (Data.Length > 5)
                                                {
                                                    byte ench = 0;
                                                    byte.TryParse(Data[5], out ench);
                                                    newItem.Enchant = Math.Min((byte)255, ench);
                                                    if (Data.Length > 6)
                                                    {
                                                        byte soc1 = 0;
                                                        byte.TryParse(Data[6], out soc1);
                                                        if (Enum.IsDefined(typeof(Game.Enums.Gem), soc1))
                                                        {
                                                            newItem.SocketOne = (Game.Enums.Gem)soc1;
                                                        }
                                                        if (Data.Length > 7)
                                                        {
                                                            byte soc2 = 0;
                                                            byte.TryParse(Data[7], out soc2);
                                                            if (Enum.IsDefined(typeof(Game.Enums.Gem), soc2))
                                                            {
                                                                newItem.SocketTwo = (Game.Enums.Gem)soc2;
                                                            }
                                                        }
                                                        if (Data.Length > 10)
                                                        {
                                                            byte R = 0, G = 0, B = 0;
                                                            byte.TryParse(Data[8], out R);
                                                            byte.TryParse(Data[9], out G);
                                                            byte.TryParse(Data[10], out B);
                                                            newItem.SocketProgress = (uint)(B | (G << 8) | (R << 16));
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                        newItem.Color = (Game.Enums.Color)Kernel.Random.Next(4, 8);
                                        if (client.Account.State == Database.AccountTable.AccountState.Administrador)
                                            newItem.Bound = false;
                                        client.Inventory.Add(newItem, Game.Enums.ItemUse.CreateAndAdd);
                                        client.Send("Item ID " + newItem.ID);
                                    }
                                    break;
                                }
                            case "itemperfecto":
                                {
                                    if (Data.Length > 2)
                                    {
                                        string ItemName = Data[1].ToLower();
                                        Game.Enums.ItemQuality Quality = Game.Enums.ItemQuality.Fixed;
                                        switch (Data[2].ToLower())
                                        {
                                            case "fixed": Quality = Game.Enums.ItemQuality.Fixed; break;
                                            case "normal": Quality = Game.Enums.ItemQuality.Normal; break;
                                            case "normalv1": Quality = Game.Enums.ItemQuality.NormalV1; break;
                                            case "normalv2": Quality = Game.Enums.ItemQuality.NormalV2; break;
                                            case "normalv3": Quality = Game.Enums.ItemQuality.NormalV3; break;
                                            case "refined": Quality = Game.Enums.ItemQuality.Refined; break;
                                            case "unique": Quality = Game.Enums.ItemQuality.Unique; break;
                                            case "elite": Quality = Game.Enums.ItemQuality.Elite; break;
                                            case "super": Quality = Game.Enums.ItemQuality.Super; break;
                                            case "other": Quality = Game.Enums.ItemQuality.Other; break;
                                            default:
                                                {
                                                    Quality = (Revora.Game.Enums.ItemQuality)int.Parse(Data[2]);
                                                    break;
                                                }
                                        }
                                        Database.ConquerItemBaseInformation CIBI = null;
                                        foreach (Database.ConquerItemBaseInformation infos in Database.ConquerItemInformation.BaseInformations.Values)
                                        {
                                            if (infos.LoweredName == ItemName && Quality == (Game.Enums.ItemQuality)(infos.ID % 10))
                                            {
                                                CIBI = infos;
                                            }
                                        }
                                        if (CIBI == null)
                                            break;
                                        ConquerItem newItem = new GamePackets.ConquerItem(true);
                                        newItem.ID = CIBI.ID;
                                        newItem.Durability = CIBI.Durability;
                                        newItem.MaximDurability = CIBI.Durability;
                                        if (Data.Length > 3)
                                        {
                                            byte plus = 0;
                                            byte.TryParse(Data[3], out plus);
                                            newItem.Plus = Math.Min((byte)12, plus);
                                            if (Data.Length > 4)
                                            {
                                                byte bless = 0;
                                                byte.TryParse(Data[4], out bless);
                                                newItem.Bless = Math.Min((byte)7, bless);
                                                if (Data.Length > 5)
                                                {
                                                    byte ench = 0;
                                                    byte.TryParse(Data[5], out ench);
                                                    newItem.Enchant = Math.Min((byte)255, ench);
                                                    if (Data.Length > 6)
                                                    {
                                                        byte soc1 = 0;
                                                        byte.TryParse(Data[6], out soc1);
                                                        if (Enum.IsDefined(typeof(Game.Enums.Gem), soc1))
                                                        {
                                                            newItem.SocketOne = (Game.Enums.Gem)soc1;
                                                        }
                                                        if (Data.Length > 7)
                                                        {
                                                            byte soc2 = 0;
                                                            byte.TryParse(Data[7], out soc2);
                                                            if (Enum.IsDefined(typeof(Game.Enums.Gem), soc2))
                                                            {
                                                                newItem.SocketTwo = (Game.Enums.Gem)soc2;
                                                            }
                                                        }
                                                        if (Data.Length > 8)
                                                        {
                                                            byte PLSlayer = 0;
                                                            byte.TryParse(Data[8], out PLSlayer);
                                                            newItem.PerfectionLevel = Math.Min((byte)54, PLSlayer);
                                                        }
                                                        if (Data.Length > 9)
                                                        {
                                                            byte PerfectionProgressslayer = 0;
                                                            byte.TryParse(Data[9], out PerfectionProgressslayer);
                                                            newItem.PerfectionProgress = 0;
                                                        }
                                                        if (Data.Length > 10)
                                                        {
                                                            uint PerfectionEntityIDSlayer = 0;
                                                            uint.TryParse(Data[10], out PerfectionEntityIDSlayer);
                                                            newItem.PerfectionOwnerGuid = client.Entity.UID;
                                                        }
                                                        if (Data.Length > 11)
                                                        {
                                                            newItem.Signature = string.Format(Data[11]);
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                        newItem.Color = (Revora.Game.Enums.Color)Kernel.Random.Next(4, 8);
                                        client.Inventory.Add(newItem, Game.Enums.ItemUse.CreateAndAdd);
                                    }
                                    break;
                                }
                            case "gui99":
                                {
                                    Data data = new Data(true);
                                    data.ID = 116;
                                    data.UID = client.Entity.UID;
                                    data.TimeStamp = Time32.Now;
                                    data.dwParam = 3264;
                                    client.Send(data);
                                    break;
                                }
                            case "booth":
                                {
                                    client.Send(new MapStatus() { BaseID = client.Map.BaseID, ID = client.Map.ID, Status = Database.MapsTable.MapInformations[1036].Status });
                                    client.Booth = new Game.ConquerStructures.Booth(client, new Data(true) { UID = client.Entity.UID });
                                    client.Send(new Data(true) { ID = GamePackets.Data.ChangeAction, UID = client.Entity.UID, dwParam = 0 });
                                    break;
                                }
                        }
                        return true;
                    }
                    #endregion
                    return true;
                }
                if (message.StartsWith("."))
                {
                    string message_ = message.Substring(1).ToLower();
                    string Mess = message.Substring(1);
                    string[] Data = message_.Split(' ');
                    if (client.Account.State >= 0)
                    {
                        client.Account.SaveState();
                        client.Disconnect();
                    }
                }
                if (message.StartsWith("/"))
                {
                    string message_ = message.Substring(1).ToLower();
                    string Mess = message.Substring(1);
                    string[] Data = message_.Split(' ');
                    if (client.Account.State >= 0)
                    {
                        client.Account.SaveState();
                        client.Disconnect();
                    }
                }
                return false;
            }
            catch
            {
                client.Send(new Message("Invalid command. Check the syntax.", System.Drawing.Color.Yellow, Message.System));
                return false;
            }
        }
    }
}
