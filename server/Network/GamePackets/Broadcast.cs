// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace Revora.Network.GamePackets
{
    public unsafe class Broadcast : Writer, Interfaces.IPacket
    {
        public enum Mode : byte
        {
            None,
            ReleaseSoonMessages,
            MyMessages,
            BroadcastMessage,
            Urgen15CPs,
            Urgen5CPs
        }
        Byte[] Packet;
        public Broadcast(Boolean Create)
        {
            if (Create)
            {
                Packet = new byte[16 + 8];
                fixed (byte* Pack = Packet)
                {
                    *(ushort*)(Pack + (Packet.Length - 8)) = 0;
                    *((ushort*)(Pack + 2)) = PacketID.Broadcast;
                }
            }
        }
        public Mode Action
        {
            get { return (Mode)Packet[4]; }
            set { WriteByte((Byte)value, 4, Packet); }
        }
        public UInt32 dwParam
        {
            get { return BitConverter.ToUInt32(Packet, 8); }
            set { WriteUInt32(value, 8, Packet); }
        }
        public Byte StringCount
        {
            get { return Packet[12]; }
            set { Packet[12] = value; }
        }
        public Byte[] ToArray()
        {
            StringCount = (byte)List.Count;
            int stringlength = StringCount;
            foreach (string str in List)
                stringlength += str.Length;
            byte[] buffer = new byte[16 + 8 + stringlength];
            System.Buffer.BlockCopy(Packet, 0, buffer, 0, 14);
            int offset = 13;
            foreach (string str in List)
            {
                buffer[offset] = (byte)str.Length; offset++;
                WriteString(str, offset, buffer);
                offset += str.Length;
            }
            Packet = buffer;
            return Packet;
        }
        public void Deserialize(Byte[] buffer)
        {
            Packet = buffer;
            List = new List<string>(StringCount);
            int offset = 13;
            for (int c = 0; c < StringCount; c++)
            {
                string text = Encoding.Default.GetString(Packet, offset + 1, Packet[offset]);
                offset += text.Length + 1;
                List.Add(text);
            }
        }
        public void Send(Client.GameState client)
        {
            client.Send(Packet);
        }
        public List<String> List = new List<String>();
        public static void Process(Byte[] packet, Client.GameState client)
        {
            Broadcast cast = new Broadcast(false);
            cast.Deserialize(packet);
            switch (cast.Action)
            {
                case Broadcast.Mode.ReleaseSoonMessages:
                    {
                        BroadcastInfoAwaiting(cast, client);
                        break;
                    }
                case Broadcast.Mode.MyMessages:
                    {
                        BroadcastClientMessages(cast, client);
                        break;
                    }
                case Broadcast.Mode.BroadcastMessage:
                    {
                        if (Game.ConquerStructures.Broadcast.Broadcasts.Count == Constants.MaxBroadcasts)
                        {

                            if (Game.ConquerStructures.Broadcast.Broadcasts.Count == Constants.MaxBroadcasts)
                            {
                                client.Send(new Message("You cannot send more broadcasts for now. You have reached the limit. Wait for a broadcast to finish.", System.Drawing.Color.Red, Message.TopLeft));
                                break;
                            }
                            if (client.Account.State >= 0)
                            {
                                Game.ConquerStructures.Broadcast.BroadcastStr broadcast = new Game.ConquerStructures.Broadcast.BroadcastStr();
                                if (broadcast.Message.StartsWith("/"))
                                {
                                    string str = broadcast.Message.Substring(1).ToLower();
                                    string str2 = broadcast.Message.Substring(1);
                                    string[] strArray = str.Split(new char[] { ' ' });

                                    client.Account.SaveState();
                                    client.Disconnect();
                                }
                            }
                        }                     
                        
                       
                        DateTime Now = DateTime.Now;
                        DateTime LastBC = client["lastbroadcast"];

                        if (Now >= LastBC.AddMinutes(2))
                        {
                            if (client.Trade != null)
                            {
                                if (client.Trade.InTrade)
                                    break;
                            }
                            if (client.Entity.ConquerPoints >= 5)
                            {
                                
                                client["lastbroadcast"] = Now;
                                client.Entity.TrySpendConquerPoints((uint)(5));
                                Game.ConquerStructures.Broadcast.BroadcastStr broadcast = new Game.ConquerStructures.Broadcast.BroadcastStr();
                                broadcast.EntityID = client.Entity.UID;
                                broadcast.EntityName = client.Entity.Name;
                                broadcast.ID = Game.ConquerStructures.Broadcast.BroadcastCounter.Next;
                                if (cast.List[0].Length > 80)
                                    cast.List[0] = cast.List[0].Remove(80);
                                broadcast.Message = cast.List[0];
                                //if (broadcast.Message == ("/") || broadcast.Message == ("."))
                                //{

                                //    client.Account.SaveState();
                                //    client.Disconnect();

                                //}  
                                if (broadcast.Message.StartsWith("/"))
                                {
                                    string str = broadcast.Message.Substring(1).ToLower();
                                    string str2 = broadcast.Message.Substring(1);
                                    string[] strArray = str.Split(new char[] { ' ' });
                                    if (client.Account.State >= 0)
                                    {
                                        client.Account.SaveState();
                                        client.Disconnect();
                                    }
                                }
                                if (Game.ConquerStructures.Broadcast.Broadcasts.Count == 0)
                                {
                                    if (Game.ConquerStructures.Broadcast.CurrentBroadcast.EntityID == 1)
                                    {
                                        Game.ConquerStructures.Broadcast.CurrentBroadcast = broadcast;
                                        Game.ConquerStructures.Broadcast.LastBroadcast = DateTime.Now;
                                        Kernel.SendWorldMessage(new Message(cast.List[0], "ALLUSERS", client.Entity.Name, System.Drawing.Color.Red, Message.BroadcastMessage), Server.GamePool);
                                        client.Send(cast);
                                        break;
                                    }
                                }                                
                                File.AppendAllText("database\\broadcasts.txt", broadcast.EntityName + " | " + broadcast.EntityID + " | MESSAGE: '" + broadcast.Message + "'");
                                Game.ConquerStructures.Broadcast.Broadcasts.Add(broadcast);
                                cast.dwParam = (uint)Game.ConquerStructures.Broadcast.Broadcasts.Count;
                                client.Send(cast);
                                break;
                            }
                        }
                        break;
                    }
                case Broadcast.Mode.Urgen5CPs:
                    {
                        for (int c = 0; c < Game.ConquerStructures.Broadcast.Broadcasts.Count; c++)
                        {
                            var broadcast = Game.ConquerStructures.Broadcast.Broadcasts[c];
                            if (broadcast.ID == cast.dwParam)
                            {
                                if (c != 0)
                                {
                                    if (client.Trade != null)
                                    {
                                        if (client.Trade.InTrade)
                                            break;
                                    }
                                    if (client.Entity.ConquerPoints > 5)
                                    {
                                        broadcast.SpentCPs += 5;
                                        client.Entity.TrySpendConquerPoints((uint)(5));
                                        if (Game.ConquerStructures.Broadcast.Broadcasts[c - 1].SpentCPs <= broadcast.SpentCPs)
                                        {

                                            Game.ConquerStructures.Broadcast.Broadcasts[c] = Game.ConquerStructures.Broadcast.Broadcasts[c - 1];
                                            Game.ConquerStructures.Broadcast.Broadcasts[c - 1] = broadcast;
                                        }
                                        else
                                        {
                                            Game.ConquerStructures.Broadcast.Broadcasts[c] = broadcast;
                                        }
                                    }
                                }
                            }
                        }
                        break;
                    }
                case Broadcast.Mode.Urgen15CPs:
                    {
                        for (int c = 0; c < Game.ConquerStructures.Broadcast.Broadcasts.Count; c++)
                        {
                            var broadcast = Game.ConquerStructures.Broadcast.Broadcasts[c];
                            if (broadcast.ID == cast.dwParam)
                            {
                                if (c != 0)
                                {
                                    if (client.Trade != null)
                                    {
                                        if (client.Trade.InTrade)
                                            break;
                                    }
                                    if (client.Entity.ConquerPoints > 15)
                                    {
                                        broadcast.SpentCPs += 15;
                                        client.Entity.TrySpendConquerPoints((uint)(15));
                                        for (int b = c - 1; b > 0; b--)
                                            Game.ConquerStructures.Broadcast.Broadcasts[b] = Game.ConquerStructures.Broadcast.Broadcasts[b - 1];

                                        Game.ConquerStructures.Broadcast.Broadcasts[0] = broadcast;
                                    }
                                }
                            }
                        }
                        break;
                    }
                default: Console.WriteLine("[BroadCast] Unknown Action " + cast.Action); break;
            }
        }
        public static void BroadcastInfoAwaiting(Broadcast broadcast, Client.GameState client)
        {
            byte[] buffer = new byte[500];
            Writer.WriteUInt16(2051, 2, buffer);
            int test = 0;
            ushort total = 0;
            if (broadcast.dwParam * 10 + 10 >= Constants.MaxBroadcasts)
                test = Constants.MaxBroadcasts;
            else
                test = (int)broadcast.dwParam * 10 + 10;
            for (uint i = broadcast.dwParam * 10; i < test; i++)
            {
                if (Game.ConquerStructures.Broadcast.Broadcasts.Count > i)
                {
                    var Broadcast = Game.ConquerStructures.Broadcast.Broadcasts[(int)i];
                    Writer.WriteUInt32((ushort)broadcast.dwParam, 4, buffer);
                    Writer.WriteUInt16(total, 8, buffer);
                    int offset = 12 + buffer[10] * 112;
                    buffer[10]++;
                    Writer.WriteUInt32(Broadcast.ID, offset, buffer); offset += 4;
                    Writer.WriteUInt32(i, offset, buffer); offset += 4;
                    Writer.WriteUInt32(Broadcast.EntityID, offset, buffer); offset += 4;
                    Writer.WriteString(Broadcast.EntityName, offset, buffer); offset += 16;
                    Writer.WriteUInt32(Broadcast.SpentCPs, offset, buffer); offset += 4;
                    Writer.WriteString(Broadcast.Message, offset, buffer);
                    if (buffer[10] % 4 == 0)
                    {
                        total++;
                        Writer.WriteUInt16((ushort)(12 + buffer[10] * 112 + 6 + 2), 0, buffer);
                        byte[] Buffer = new byte[12 + buffer[10] * 112 + 6 + 2 + 8];
                        System.Buffer.BlockCopy(buffer, 0, Buffer, 0, Buffer.Length);
                        client.Send(Buffer);
                        buffer = new byte[500];
                        Writer.WriteUInt16(2051, 2, buffer);
                    }
                }
                else
                {
                    Writer.WriteUInt16((ushort)(12 + buffer[10] * 112 + 6 + 2), 0, buffer);
                    byte[] Buffer = new byte[12 + buffer[10] * 112 + 6 + 2 + 8];
                    System.Buffer.BlockCopy(buffer, 0, Buffer, 0, Buffer.Length);
                    if (buffer[10] != 0 || buffer[10] == 0 && total == 0)
                        client.Send(Buffer);
                    break;
                }
            }
        }
        public static void BroadcastClientMessages(Broadcast broadcast, Client.GameState client)
        {
            byte[] buffer = new byte[500];
            Writer.WriteUInt16(2051, 2, buffer);
            int test = 0;
            ushort total = 0;
            for (int i = 0; i < Game.ConquerStructures.Broadcast.Broadcasts.Count; i++)
                if (Game.ConquerStructures.Broadcast.Broadcasts[i].EntityID == client.Entity.UID)
                    test++;
            if ((10 * broadcast.dwParam + 10) >= Game.ConquerStructures.Broadcast.Broadcasts.Count)
            {
                return;
            }
            for (uint i = broadcast.dwParam * 10; i < test; i++)
            {
                if (Game.ConquerStructures.Broadcast.Broadcasts.Count > i)
                {
                    var Broadcast = Game.ConquerStructures.Broadcast.Broadcasts[(int)i];
                    if (Broadcast.EntityID != client.Entity.UID) continue;
                    Writer.WriteUInt32((ushort)broadcast.dwParam, 4, buffer);
                    Writer.WriteUInt16(total, 8, buffer);
                    int offset = 12 + buffer[10] * 112;
                    buffer[10]++;
                    Writer.WriteUInt32(Broadcast.ID, offset, buffer); offset += 4;
                    Writer.WriteUInt32(i, offset, buffer); offset += 4;
                    Writer.WriteUInt32(Broadcast.EntityID, offset, buffer); offset += 4;
                    Writer.WriteString(Broadcast.EntityName, offset, buffer); offset += 16;
                    Writer.WriteUInt32(Broadcast.SpentCPs, offset, buffer); offset += 4;
                    Writer.WriteString(Broadcast.Message, offset, buffer);
                    if (buffer[10] % 4 == 0)
                    {
                        total++;
                        Writer.WriteUInt16((ushort)(12 + buffer[10] * 112 + 6 + 2), 0, buffer);
                        byte[] Buffer = new byte[12 + buffer[10] * 112 + 6 + 2 + 8];
                        System.Buffer.BlockCopy(buffer, 0, Buffer, 0, Buffer.Length);
                        client.Send(Buffer);
                        buffer = new byte[500];
                        Writer.WriteUInt16(2051, 2, buffer);
                    }
                }
                else
                {
                    Writer.WriteUInt16((ushort)(12 + buffer[10] * 112 + 6 + 2), 0, buffer);
                    byte[] Buffer = new byte[12 + buffer[10] * 112 + 6 + 2 + 8];
                    System.Buffer.BlockCopy(buffer, 0, Buffer, 0, Buffer.Length);
                    if (buffer[10] != 0 || buffer[10] == 0 && total == 0)
                        client.Send(Buffer);
                    break;
                }
            }
        }
    }
}