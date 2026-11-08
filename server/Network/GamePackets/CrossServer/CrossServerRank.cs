// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

//using System;
//using System.Linq;
//using System.Text;
//using Revora.Game;
//using System.Collections.Generic;
//using Revora.Game.ConquerStructures.Society;

//namespace Revora.Network.GamePackets
//{
//    public unsafe class CrossServer
//    {
//        public static void Process(byte[] packet, Client.GameState client)
//        {
//            switch (packet[4])
//            {
//                case 0:
//                    {
//                        if (client.Guild == null || client.AsMember == null) break;
//                        if (!CaptureTheFlag.IsWar)
//                        {
//                            var array = Kernel.Guilds.Values.Where(p => p.CTFdonationCPs != 0).OrderByDescending(p => p.CTFdonationSilver).ToArray();
//                            const byte maxcount = 5;
//                            byte page = packet[10];
//                            byte[] buffer = new byte[908];
//                            Writer.WriteUInt16(900, 0, buffer);
//                            Writer.WriteUInt16(1063, 2, buffer);
//                            Writer.WriteUInt16(0, 4, buffer);
//                            Writer.WriteUInt32(1, 6, buffer);
//                            Writer.WriteUInt32((uint)(array.Length), 10, buffer);
//                            Writer.WriteUInt32((uint)(array.Length), 14, buffer);
//                            Writer.WriteUInt32(client.Guild.CTFdonationCPsold, 18, buffer);
//                            Writer.WriteUInt64((ulong)client.Guild.CTFdonationSilverold, 22, buffer);
//                            int offset = 30;
//                            for (ushort x = (ushort)(page * maxcount - maxcount); x < page * maxcount; x++)
//                            {
//                                if (x >= array.Length) break;
//                                var guild = array[x];
//                                Writer.WriteUInt32(guild.CTFdonationCPs, offset, buffer);
//                                offset += 4;
//                                Writer.WriteUInt64((ulong)guild.CTFdonationSilver, offset, buffer);
//                                offset += 8;
//                                Writer.WriteString(guild.Name, offset, buffer);
//                                offset += 36;
//                                Writer.WriteUInt32(guild.ID, offset, buffer);
//                                offset += 4;
//                            }
//                            client.Send(buffer);
//                        }
//                        else
//                        {
//                            byte page = packet[10];
//                            var array = client.Guild.Members.Values.Where(p => p.Exploits != 0).OrderByDescending(p => p.Exploits).ToArray();
//                            byte[] buffer = new byte[908];
//                            Writer.WriteUInt16(900, 0, buffer);
//                            Writer.WriteUInt16(1063, 2, buffer);
//                            Writer.WriteUInt16(8, 4, buffer);
//                            Writer.WriteUInt32(page, 6, buffer);
//                            Writer.WriteUInt32((uint)array.Length, 10, buffer);
//                            Writer.WriteUInt32(8, 14, buffer);
//                            Writer.WriteUInt32(client.AsMember.Exploits, 18, buffer);
//                            int offset = 30;
//                            for (ushort x = 0; x < array.Length; x++)
//                            {
//                                if (x > 20) break;
//                                if (x >= array.Length) break;
//                                var member = array[x];
//                                Writer.WriteString(member.Name, offset, buffer);
//                                offset += 16;
//                                Writer.WriteUInt32(member.Exploits, offset, buffer);
//                                offset += 4;
//                            }
//                            client.Send(buffer);
//                        }
//                        break;
//                    }
//                case 1:
//                    {
//                        if (client.Guild == null || client.AsMember == null) break;
//                        const byte maxcount = 5;
//                        byte page = packet[10];
//                        var array = client.Guild.Members.Values.Where(p => p.Exploits != 0).OrderByDescending(p => p.Exploits).ToArray();
//                        byte[] buffer = new byte[908];
//                        Writer.WriteUInt16(900, 0, buffer);
//                        Writer.WriteUInt16(1063, 2, buffer);
//                        Writer.WriteUInt16(1, 4, buffer);
//                        Writer.WriteUInt32(page, 6, buffer);
//                        Writer.WriteUInt32((uint)array.Length, 10, buffer);
//                        Writer.WriteUInt32(5, 14, buffer);
//                        Writer.WriteUInt32(client.Guild.CTFdonationCPsold, 18, buffer);
//                        Writer.WriteUInt64((ulong)client.Guild.CTFdonationSilverold, 22, buffer);
//                        Writer.WriteUInt32((ushort)array.Length, 30, buffer);
//                        int offset = 30;
//                        for (ushort x = (ushort)(page * maxcount - maxcount); x < page * maxcount; x++)
//                        {
//                            if (x >= array.Length) break;
//                            var member = array[x];
//                            Writer.WriteUInt32((ushort)(x + 1), offset, buffer);
//                            offset += 4;
//                            Writer.WriteUInt32(member.Exploits, offset, buffer);
//                            offset += 4;
//                            Writer.WriteUInt32(member.CTFCpsReward, offset, buffer);
//                            offset += 4;
//                            Writer.WriteUInt64(member.CTFSilverReward, offset, buffer);
//                            offset += 8;
//                            Writer.WriteUInt32(member.ID, offset, buffer);
//                            offset += 4;
//                            Writer.WriteString(member.Name, offset, buffer);
//                            offset += 36;
//                        }
//                        client.Send(buffer);
//                        break;
//                    }
//                case 3:
//                    {
//                        if (client.Guild == null || client.AsMember == null) break;
//                        uint setCTFCps = BitConverter.ToUInt32(packet, 18);
//                        if (client.Guild.ConquerPointFund > setCTFCps)
//                        {
//                            client.Guild.ConquerPointFund -= setCTFCps;
//                            client.Guild.CTFdonationCPsold += setCTFCps;
//                            client.Guild.CTFdonationCPs += setCTFCps;
//                            Database.GuildTable.SaveFunds(client.Guild);
//                        }
//                        goto case 0;
//                    }
//                case 4:
//                    {
//                        if (client.Guild == null || client.AsMember == null) break;
//                        uint setCTFMoney = BitConverter.ToUInt32(packet, 22);
//                        if (client.Guild.SilverFund > setCTFMoney)
//                        {
//                            client.Guild.SilverFund -= setCTFMoney;
//                            client.Guild.CTFdonationSilverold += setCTFMoney;
//                            client.Guild.CTFdonationSilver += setCTFMoney;
//                            Database.GuildTable.SaveFunds(client.Guild);
//                        }
//                        goto case 0;
//                    }
//                case 5:
//                    {
//                        if (client.Guild == null || client.AsMember == null) break;
//                        uint setCTFCps = BitConverter.ToUInt32(packet, 18);
//                        uint setCTFMoney = BitConverter.ToUInt32(packet, 22);
//                        if (client.Guild.ConquerPointFund > setCTFCps)
//                        {
//                            client.Guild.ConquerPointFund -= setCTFCps;
//                            client.Guild.CTFdonationCPsold += setCTFCps;
//                            client.Guild.CTFdonationCPs += setCTFCps;
//                            Database.GuildTable.SaveFunds(client.Guild);
//                        }
//                        if (client.Guild.SilverFund > setCTFMoney)
//                        {
//                            client.Guild.SilverFund -= setCTFMoney;
//                            client.Guild.CTFdonationSilverold += setCTFMoney;
//                            client.Guild.CTFdonationSilver += setCTFMoney;
//                            Database.GuildTable.SaveFunds(client.Guild);
//                        }
//                        goto case 0;
//                    }
//                case 8:
//                    {
//                        if (client.Guild == null || client.AsMember == null) break;
//                        const byte maxcount = 5;
//                        byte page = packet[10];
//                        var guild_array = Kernel.Guilds.Values.Where(p => p.Points != 0).OrderByDescending(p => p.Points).ToArray();
//                        byte[] buffer = new byte[908];
//                        Writer.WriteUInt16(900, 0, buffer);
//                        Writer.WriteUInt16(1063, 2, buffer);
//                        Writer.WriteUInt16(0, 4, buffer);
//                        Writer.WriteUInt16(page, 6, buffer);
//                        Writer.WriteUInt32(10, 10, buffer);
//                        Writer.WriteUInt32((uint)guild_array.Length, 14, buffer);
//                        client.Guild = client.AsMember.Guild;
//                        Writer.WriteUInt32(client.Guild.CTFdonationCPsold, 18, buffer);
//                        Writer.WriteUInt64((ulong)client.Guild.CTFdonationSilverold, 22, buffer);
//                        ushort offset = 30;
//                        for (ushort x = (ushort)(page * maxcount - maxcount); x < page * maxcount; x++)
//                        {
//                            if (x >= guild_array.Length) break;
//                            var guild = guild_array[x];
//                            Writer.WriteUInt32(guild.CTFdonationCPsold, offset, buffer);
//                            offset += 4;
//                            Writer.WriteUInt64((ulong)guild.CTFdonationSilverold, offset, buffer);
//                            offset += 8;
//                            Writer.WriteString(guild.Name, offset, buffer);
//                            offset += 36;
//                            Writer.WriteUInt32(guild.ID, offset, buffer);
//                            offset += 4;
//                        }
//                        client.Send(buffer);
//                        break;
//                    }
//                case 9:
//                    {
//                        if (client.Guild == null || client.AsMember == null) break;
//                        if (CaptureTheFlag.IsWar)
//                        {
//                            new CaptureTheFlagRank(client).Send(client);//
//                        }
//                        else
//                        {
//                            var array = Kernel.Guilds.Values.Where(p => p.Points != 0).OrderByDescending(p => p.Points).ToArray();
//                            byte[] Packet = new byte[356];
//                            Writer.WriteUInt16(348, 0, Packet);
//                            Writer.WriteUInt16(1063, 2, Packet);
//                            Packet[4] = 9;
//                            Packet[6] = 1;
//                            Packet[14] = 8;
//                            ushort index = 26;
//                            Writer.WriteUInt32(client.Player.UID, 18, Packet);
//                            for (byte i = 0; i < array.Length; i++)
//                            {
//                                if (i >= 8) break;
//                                if (i >= array.Length) break;
//                                var guild = array[i];
//                                Packet[index] = (byte)(i + 1);
//                                index = (ushort)(index + 4);
//                                Writer.WriteString(guild.Name, index, Packet);
//                                index = (ushort)(index + 16);
//                                Writer.WriteUInt32(guild.Points, index, Packet);
//                                index = (ushort)(index + 4);
//                                Writer.WriteUInt32(guild.MemberCount, index, Packet);
//                                index = (ushort)(index + 12);
//                            }
//                            client.Send(Packet);
//                        }
//                        break;
//                    }
//                default: Console.WriteLine("[CrossServer] Unknown Type: " + packet[4] + ""); break;
//            }
//        }
//    }
//}