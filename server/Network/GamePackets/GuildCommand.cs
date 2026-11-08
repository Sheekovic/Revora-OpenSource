using System;
using System.Linq;
using System.Text;
using Revora.Game;
using System.Collections.Generic;

namespace Revora.Network.GamePackets
{
    public unsafe class GuildCommand : Writer, Interfaces.IPacket
    {
        public List<string> Positions = new List<string>();
        public const uint
        JoinRequest = 1,
        InviteRequest = 2,
        Quit = 3,
        Info = 6,
        Allied = 7,
        Neutral1 = 8,
        Enemied = 9,
        Neutral2 = 10,
        DonateSilvers = 11,
        Refresh = 12,
        Disband = 19,
        DonateConquerPoints = 20,
        ChangeGuildRequirements = 24,
        GuildRequirements = 25,
        Bulletin = 27,
        PromoteSendRequest = 28,
        PromoteAcceptRequest = 29,
        Discharge = 30,
        ConfirmPromote = 34,
        Diseharge = 36,
        Promote = 37,
        PromoteInfo = 38,
        Unknown = 45,
        MakeBlackList = 48,
        GuildBlackList = 47,
        SmsBlackList = 49,
        RemoveBlackList = 50;

        private byte[] Buffer;
        public GuildCommand(bool Create)
        {
            if (Create)
            {
                Buffer = new byte[36];
                WriteUInt16(28, 0, Buffer);
                WriteUInt16(1107, 2, Buffer);
            }
        }
        public GuildCommand(uint Date)
        {
            Buffer = new byte[36 + Date];
            WriteUInt16((ushort)(Buffer.Length - 8), 0, Buffer);
            WriteUInt16(1107, 2, Buffer);
        }
        public uint Type
        {
            get { return BitConverter.ToUInt32(Buffer, 4); }
            set { WriteUInt32(value, 4, Buffer); }
        }
        public uint dwParam
        {
            get { return BitConverter.ToUInt32(Buffer, 8); }
            set { WriteUInt32(value, 8, Buffer); }
        }
        public uint Level
        {
            get { return BitConverter.ToUInt32(Buffer, 12); }
            set { WriteUInt32(value, 12, Buffer); }
        }
        public uint Reborn
        {
            get { return BitConverter.ToUInt32(Buffer, 16); }
            set { WriteUInt32(value, 16, Buffer); }
        }
        public uint Class
        {
            get { return BitConverter.ToUInt32(Buffer, 20); }
            set { WriteUInt32(value, 20, Buffer); }
        }
        public string BulletinTime
        {
            set
            {
                WriteByte(1, 24, Buffer);
                WriteByte((byte)(value.Length), 25, Buffer);
                WriteString(value, 26, Buffer);
            }
        }
        public static void Process(byte[] packet, Client.GameState client)
        {
            if (!Revora.Game.Features.QuestInventory.Player(client, false)) return;
            lock (client.ItemSyncRoot)
            {
                if (!Revora.Game.Features.QuestInventory.Player(client, false)) return;
                ProcessCurrent(packet, client);
            }
        }
        private static void ProcessCurrent(byte[] packet, Client.GameState client)
        {
            if (Revora.Game.Features.GuildLeadership.TryHandle(packet, client)) return;
            GuildCommand command = new GuildCommand(false);
            command.Deserialize(packet);
            switch (command.Type)
            {
                case GuildCommand.Info:
                    {
                        var Guild = Kernel.Guilds[command.dwParam];
                        if (Guild != null)
                            Guild.SendName(client);
                        break;
                    }
                case GuildCommand.ChangeGuildRequirements:
                    {
                        if (client.AsMember.Rank == Enums.GuildMemberRank.GuildLeader)
                        {
                            client.Guild.LevelRequirement = Math.Min(command.Level, 140);
                            client.Guild.RebornRequirement = Math.Min(command.Reborn, 2);
                            client.Guild.ClassRequirement = Math.Min(command.Class, 127);
                            foreach (var member in client.Guild.Members.Values)
                                if (member.IsOnline)
                                    client.Guild.SendGuild(member.Client);
                            Database.GuildTable.SaveRequirements(client.Guild);
                        }
                        break;
                    }
                case GuildCommand.Neutral1:
                case GuildCommand.Neutral2:
                    {
                        string name = System.Text.Encoding.Default.GetString(packet, 26, packet[25]);
                        if (client.Guild != null)
                        {
                            if (client.AsMember.Rank == Enums.GuildMemberRank.GuildLeader)
                            {
                                client.Guild.RemoveAlly(name);
                                foreach (var guild in Kernel.Guilds.Values)
                                {
                                    if (guild.Name == name && client.Guild.Name != name)
                                    {
                                        guild.RemoveAlly(client.Guild.Name);
                                    }
                                }
                                client.Guild.RemoveEnemy(name);
                            }
                        }
                        break;
                    }
                case GuildCommand.Allied:
                    {
                        string name = System.Text.Encoding.Default.GetString(packet, 26, packet[25]);
                        if (client.Guild != null)
                        {
                            if (client.AsMember.Rank == Enums.GuildMemberRank.GuildLeader)
                            {
                                if (client.Guild.Ally.Count < 6)
                                {
                                    AllyGuilds(name, client);
                                }
                            }
                        }
                        break;
                    }
                case GuildCommand.Enemied:
                    {
                        string name = System.Text.Encoding.Default.GetString(packet, 26, packet[25]);
                        if (client.Guild != null)
                        {
                            if (client.AsMember.Rank == Enums.GuildMemberRank.GuildLeader)
                            {
                                if (client.Guild.Enemy.Count < 6)
                                {
                                    client.Guild.AddEnemy(name);
                                }
                            }
                        }
                        break;
                    }
                case GuildCommand.Bulletin:
                    {
                        string message = System.Text.Encoding.Default.GetString(packet, 26, packet[25]);
                        if (client.Guild != null)
                        {
                            if (client.AsMember.Rank == Enums.GuildMemberRank.GuildLeader)
                            {
                                client.Guild.Bulletin = message;
                                client.Guild.CreateBuletinTime();
                                client.Guild.SendGuild(client);
                            }
                        }
                        break;
                    }
                case GuildCommand.DonateSilvers:
                case GuildCommand.DonateConquerPoints:
                    {
                        if(packet.Length<36 || packet.Length>128 || BitConverter.ToUInt16(packet,0)!=packet.Length-8
                            || BitConverter.ToUInt16(packet,2)!=1107 || BitConverter.ToUInt64(packet,packet.Length-8)!=Revora.Network.PacketHandler.ClientSeal)return;
                        Revora.Game.Features.GuildDonations.Donate(client,command.dwParam,command.Type==GuildCommand.DonateConquerPoints);
                        break;
                    }
                case GuildCommand.Refresh:
                    {
                        if (client.AsMember != null)
                        {
                            if (client.Guild != null)
                                client.Guild.SendGuild(client);
                        }
                        break;
                    }
                case GuildCommand.PromoteInfo:
                    {
                        if (client.AsMember.Rank == Game.Enums.GuildMemberRank.GuildLeader)
                        {
                            SafeDictionary<uint, Game.ConquerStructures.Society.Guild.Member> array2 = new SafeDictionary<uint, Game.ConquerStructures.Society.Guild.Member>();
                            foreach (var p in client.Guild.Members.Values)
                            {
                                if (p.Rank == (Enums.GuildMemberRank)command.dwParam)
                                {
                                    array2.Add(p.ID, p);
                                }
                            }
                            var array = array2.Values.ToArray();
                            if (array != null)
                            {
                                byte[] Buffer = new byte[8 + 48 + array.Length * 32];
                                Writer.WriteUInt16((ushort)(Buffer.Length - 8), 0, Buffer);
                                Writer.WriteUInt16(2102, 2, Buffer);
                                Writer.Uint(1, 4, Buffer);
                                Writer.Uint((uint)array.Length, 12, Buffer);
                                int offset = 16;
                                for (int i = 0; i < array.Length; i++)
                                {
                                    Writer.Uint((uint)array[i].Level, offset, Buffer);//Level
                                    offset += 4;
                                    Writer.Uint((uint)(array[i].IsOnline ? 1 : 0), offset, Buffer);//Online
                                    offset += 4;
                                    if (array[i].IsOnline)
                                        Writer.Uint((uint)array[i].Client.Entity.BattlePower, offset, Buffer);//BP  
                                    offset += 4;
                                    offset += 4;
                                    Writer.WriteString(array[i].Name, offset, Buffer);
                                    offset += 16;
                                }
                                client.Send(Buffer);
                            }
                        }
                        client.Send(packet);
                        break;
                    }
                case GuildCommand.Promote:
                    {
                        if (client.Guild == null || client.AsMember == null) return;
                        command.SendPromote(client, 37);
                        return;
                    }
                case GuildCommand.JoinRequest:
                    {
                        Client.GameState Target;
                        if (Kernel.GamePool.TryGetValue(command.dwParam, out Target))
                        {
                            if (!Revora.Game.Features.QuestInventory.Player(Target, false)
                                || Target.Guild == null || Target.AsMember == null) break;
                            if (Target.Guild.BlackList.Contains(client.Entity.UID) )
                            {
                                command.Type = GuildCommand.GuildBlackList;
                                client.Send(command);
                                return;
                            }
                            // seccion para que el vice jefe no de clan
                            if (Target.AsMember.Rank == Game.Enums.GuildMemberRank.DeputyLeader)
                            {
                               return;
                            }
                            client.GuildJoinTarget = Target.Entity.UID;
                            if (client.GuildJoinTarget == Target.Entity.UID && Target.GuildJoinTarget == client.Entity.UID)
                            {
                                client.GuildJoinTarget = 0;
                                Target.GuildJoinTarget = 0;
                                Game.ConquerStructures.Society.Guild g;
                                if (Kernel.Guilds.TryGetValue(Target.Entity.GuildID, out g))
                                {
                                    if (Target.AsMember.Rank != Game.Enums.GuildMemberRank.Member )
                                    {
                                        if (client.Entity.GuildID == 0 && !g.BlackList.Contains(client.Entity.UID)
                                            && Guild_Advertise.PassJoinRequirements(client, g))
                                        {
                                            g.AddMember(client);
                                        }
                                    }
                                }
                            }
                            else
                            {
                                Game.ConquerStructures.Society.Guild tG;
                                if (Kernel.Guilds.TryGetValue(Target.Entity.GuildID, out tG))
                                {
                                    if (Target.AsMember.Rank != Game.Enums.GuildMemberRank.Member)
                                    {
                                        if (Guild_Advertise.PassJoinRequirements(client, tG))
                                        {
                                            client.Entity.GuildRequest = Time32.Now;
                                            command.dwParam = client.Entity.UID;
                                            RequestInformation request = new RequestInformation();
                                            request.Level = (uint)client.Entity.Level;
                                            request.BattlePower = (uint)client.Entity.BattlePower;
                                            request.Receiver = Target.Entity.UID;
                                            request.Requester = client.Entity.UID;
                                            if (client.Entity.Spouse == Target.Entity.Name)//Spouse
                                            {
                                                request.Spouse = RequestInformation.Request;
                                            }
                                            if (client.Friends.ContainsKey(Target.Entity.UID))//Friends
                                            {
                                                request.Friends = RequestInformation.Request;
                                            }
                                            if (client.Partners.ContainsKey(Target.Entity.UID))//TradePartner
                                            {
                                                request.TradePartner = RequestInformation.Request;
                                            }
                                            if (client.Apprentices.ContainsKey(Target.Entity.UID))//Mentor
                                            {
                                                request.Mentor = RequestInformation.Request;
                                            }
                                            if (client.Mentor != null)//Apprentice
                                            {
                                                if (client.Mentor.ID == Target.Entity.UID)
                                                    request.Apprentice = RequestInformation.Request;
                                            }
                                            if (Target.Mentor != null)
                                                if (Target.Mentor.ID == client.Entity.UID)
                                                    request.Apprentice = RequestInformation.Request;
                                            if (client.Team != null && Target.Team != null)//Team
                                            {
                                                if (client.Team.Contain(Target.Entity.UID))
                                                    if (Target.Team.Contain(client.Entity.UID))
                                                        request.Team = RequestInformation.Request;
                                            }
                                            if (client.Entity.GuildID == Target.Entity.GuildID)//Guild
                                            {
                                                request.GuildRank = RequestInformation.Request;
                                            }
                                            if (Target.Enemy.ContainsKey(client.Entity.UID))//Enemy
                                            {
                                                request.Enemy = RequestInformation.Request;
                                            }
                                            Target.Send(request.ToArray());
                                            Target.Send(command);
                                        }
                                    }
                                }
                            }
                        }
                        break;
                    }
                case GuildCommand.InviteRequest:
                    {
                        Client.GameState Target;
                        if (Revora.Game.Features.GuildLeadership.Leader(client))
                        {
                            
                            if (Kernel.GamePool.TryGetValue(command.dwParam, out Target))
                            {
                                if (!Revora.Game.Features.QuestInventory.Player(Target, false)) break;
                                if (client.Guild.BlackList.Contains(Target.Entity.UID))
                                {
                                    command.Type = GuildCommand.SmsBlackList;
                                    client.Send(command);
                                    return;
                                }
                                client.GuildJoinTarget = Target.Entity.UID;
                                if (client.GuildJoinTarget == Target.Entity.UID && Target.GuildJoinTarget == client.Entity.UID)
                                {
                                    client.GuildJoinTarget = 0;
                                    Target.GuildJoinTarget = 0;
                                    Game.ConquerStructures.Society.Guild g;
                                    if (Kernel.Guilds.TryGetValue(client.Entity.GuildID, out g))
                                    {
                                        if (client.AsMember.Rank != Game.Enums.GuildMemberRank.Member
                                            && !g.BlackList.Contains(Target.Entity.UID)
                                            && Guild_Advertise.PassJoinRequirements(Target, g))
                                        {
                                            g.AddMember(Target);
                                        }
                                    }
                                }
                                else
                                {
                                    if (client.AsMember.Rank != Game.Enums.GuildMemberRank.Member)
                                    {
                                        client.Entity.GuildRequest = Time32.Now;
                                        command.dwParam = client.Entity.UID;
                                        RequestInformation request = new RequestInformation();
                                        request.Level = client.Entity.Level;
                                        request.BattlePower = (uint)client.Entity.BattlePower;
                                        request.Receiver = Target.Entity.UID;
                                        request.Requester = client.Entity.UID;
                                        if (client.Entity.Spouse == Target.Entity.Name)//Spouse
                                        {
                                            request.Spouse = RequestInformation.Request;
                                        }
                                        if (client.Friends.ContainsKey(Target.Entity.UID))//Friends
                                        {
                                            request.Friends = RequestInformation.Request;
                                        }
                                        if (client.Partners.ContainsKey(Target.Entity.UID))//TradePartner
                                        {
                                            request.TradePartner = RequestInformation.Request;
                                        }
                                        if (client.Apprentices.ContainsKey(Target.Entity.UID))//Mentor
                                        {
                                            request.Mentor = RequestInformation.Request;
                                        }
                                        if (client.Mentor != null)//Apprentice
                                        {
                                            if (client.Mentor.ID == Target.Entity.UID)
                                                request.Apprentice = RequestInformation.Request;
                                        }
                                        if (Target.Mentor != null)
                                            if (Target.Mentor.ID == client.Entity.UID)
                                                request.Apprentice = RequestInformation.Request;
                                        if (client.Team != null && Target.Team != null)//Team
                                        {
                                            if (client.Team.Contain(Target.Entity.UID))
                                                if (Target.Team.Contain(client.Entity.UID))
                                                    request.Team = RequestInformation.Request;
                                        }
                                        if (client.Entity.GuildID == Target.Entity.GuildID)//Guild
                                        {
                                            request.GuildRank = RequestInformation.Request;
                                        }
                                        if (Target.Enemy.ContainsKey(client.Entity.UID))//Enemy
                                        {
                                            request.Enemy = RequestInformation.Request;
                                        }
                                        Target.Send(request.ToArray());
                                        Target.Send(command);
                                    }
                                }
                            }
                        }
                        break;
                    }
                case GuildCommand.Unknown:
                    {
                        client.Send(command);
                        break;
                    }
                case GuildCommand.MakeBlackList:
                    {
                        if (client.Guild != null)
                        {
                            uint UID = command.dwParam;
                            if (Kernel.GamePool.ContainsKey(UID))
                            {
                                var c = Kernel.GamePool[UID];
                                if (!client.Guild.BlackList.Contains(UID))
                                    client.Guild.BlackList.Add(UID);
                                c.Send(command);
                            }
                            client.Send(new Message("Tha target is addad to the Guild Blacklist.", Message.System));
                        }
                        break;
                    }
                case GuildCommand.RemoveBlackList:
                    {
                        if (client.Guild != null)
                        {
                            uint UID = command.dwParam;
                            if (client.Guild.BlackList.Contains(UID))
                                client.Guild.BlackList.Remove(UID);
                            client.Send(command);
                        }
                        client.Send(new Message("Removed from blacklist successfully. The target is recruitable, now.", Message.System));
                        break;
                    }
                case GuildCommand.Quit:
                    {
                        if (client.Guild != null)
                        {
                            if (client.AsMember.Rank != Enums.GuildMemberRank.GuildLeader)
                            {
                                client.Guild.ExpelMember(client.Entity.Name, true);
                            }
                        }
                        break;
                    }
                case GuildCommand.Diseharge:
                    {
                        string name = System.Text.Encoding.Default.GetString(packet, 26, packet[25]);
                        if (client.Guild != null)
                        {
                            if (client.AsMember.Rank == Enums.GuildMemberRank.GuildLeader)
                            {
                                var member = client.Guild.GetMemberByName(name);
                                client.Guild.SendGuildMessage(new Message((member.Name + " was discharged from " + member.Rank + " position."), System.Drawing.Color.Yellow, Message.Guild));
                                if (member.ID != client.Entity.UID)
                                {
                                    member.Rank = Enums.GuildMemberRank.Member;
                                    if (member.IsOnline)
                                    {
                                        client.Guild.SendGuild(member.Client);
                                        member.Client.Entity.GuildRank = (ushort)member.Rank;
                                        member.Client.Screen.FullWipe();
                                        member.Client.Screen.Reload(null);
                                        member.Client.Entity.GuildSharedBp = member.Guild.Arsenal.BattlePower;
                                    }
                                    Database.EntityTable.UpdateData(member.ID, "GuildRank", (int)member.Rank);
                                }
                            }
                        }
                        break;
                    }
                default: Console.WriteLine("[GuildCommand] Unknown Action: " + command.Type + ""); break;
            }
        }
        static void AllyGuilds(string name, Client.GameState client)
        {
            foreach (var guild in Kernel.Guilds.Values)
            {
                if (guild.Name == name && client.Guild.Name != name)
                {
                    if (guild.Leader != null)
                    {
                        if (guild.Leader.IsOnline)
                        {
                            guild.Leader.Client.OnMessageBoxEventParams = new object[] { guild, client.Guild };
                            client.OnMessageBoxEventParams = new object[] { guild, client.Guild };
                            Client.GameState Leader = guild.Leader.Client;
                            Leader.MessageOK = delegate
                            {
                                Game.ConquerStructures.Society.Guild Guild1 = Leader.OnMessageBoxEventParams[0] as Game.ConquerStructures.Society.Guild;
                                Game.ConquerStructures.Society.Guild Guild2 = Leader.OnMessageBoxEventParams[1] as Game.ConquerStructures.Society.Guild;
                                if (Guild1.Ally.Count == 6 || Guild2.Ally.Count == 6) return;
                                Guild1.AddAlly(Guild2.Name);
                                Guild2.AddAlly(Guild1.Name);
                                if (Guild1.Leader.Client != null)
                                {
                                    if (Guild1.Leader.Client.Socket.Alive)
                                    {
                                        if (Guild2.Leader.Client != null && Guild2.Leader.Client.Socket.Alive)
                                        {
                                            Guild2.Leader.Client.Send(new Message(Guild1.Leader.Name + " has accepted your ally request.", System.Drawing.Color.Blue, Message.TopLeft));
                                        }
                                    }
                                }
                            };
                            guild.Leader.Client.MessageCancel = delegate
                            {
                                try
                                {
                                    if (guild.Leader.Client != null)
                                    {
                                        if (guild.Leader.Client.Socket.Alive)
                                        {
                                            if (guild.Leader.Client.OnMessageBoxEventParams != null)
                                            {
                                                Game.ConquerStructures.Society.Guild Guild2 = guild.Leader.Client.OnMessageBoxEventParams[1] as Game.ConquerStructures.Society.Guild;
                                                Game.ConquerStructures.Society.Guild Guild1 = guild.Leader.Client.OnMessageBoxEventParams[0] as Game.ConquerStructures.Society.Guild;
                                                if (Guild2.Leader.IsOnline)
                                                {
                                                    Guild2.Leader.Client.Send(new Message(Guild1.Leader.Name + " has declined your ally request.", System.Drawing.Color.Blue, Message.TopLeft));
                                                }
                                            }
                                        }
                                    }
                                }
                                catch (Exception e)
                                {
                                    Server.SaveException(e);
                                }
                            };
                            guild.Leader.Client.Send(new NpcReply(NpcReply.MessageBox, client.Entity.Name + " , GuildLeader of " + client.Guild.Name + " wants to make with you an alliance."));
                        }
                    }
                }
            }
        }
        private string CreatePromotionString(StringBuilder builder, Enums.GuildMemberRank rank, int occupants, int maxOccupants, int extraBattlePower, int conquerPoints)
        {
            builder.Remove(0, builder.Length);
            builder.Append((int)rank);
            builder.Append(" ");
            builder.Append(occupants);
            builder.Append(" ");
            builder.Append(maxOccupants);
            builder.Append(" ");
            builder.Append(extraBattlePower);
            builder.Append(" ");
            builder.Append(conquerPoints);
            builder.Append(" ");
            return builder.ToString();
        }
        public void SendPromote(Client.GameState client, ushort typ)
        {
            if (client.Guild == null || client.AsMember == null) return;
            List<string> list = new List<string>();
            StringBuilder builder = new StringBuilder();
            if (Revora.Game.Features.GuildLeadership.Leader(client))
                list.Add(this.CreatePromotionString(builder, Enums.GuildMemberRank.GuildLeader, 1, 1, client.Guild.Arsenal.BattlePower, 0));
            foreach (Enums.GuildMemberRank rank in Enum.GetValues(typeof(Enums.GuildMemberRank)))
                if (Revora.Game.Features.GuildLeadership.CanAppoint(client, rank))
                    list.Add(this.CreatePromotionString(builder, rank, (int)client.Guild.RanksCounts[(int)rank],
                        Revora.Game.Features.GuildLeadership.Capacity(rank), client.Guild.Arsenal.BattlePower,
                        (int)Revora.Game.Features.GuildLeadership.Price(rank)));
            int num = 0;
            foreach (string current in list)
            {
                num += current.Length + 1;
            }
            byte[] array = new byte[36 + num];
            Writer.WriteUInt16((ushort)(array.Length - 8), 0, array);
            Writer.WriteUInt16(1107, 2, array);
            Writer.WriteByte((byte)typ, 4, array);
            Writer.WriteStringList(list, 24, array);
            client.Send(array);
        }
        public byte[] ToArray()
        {
            return Buffer;
        }
        public void Deserialize(byte[] Data)
        {
            Buffer = Data;
        }
        public void Send(Client.GameState client)
        {
            client.Send(Buffer);
        }
    }
}
