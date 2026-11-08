using Revora;
using Revora.Client;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Revora.Network;
using Revora.Network.GamePackets;
using Revora.Union;

namespace Revora.Network.GamePackets.Union
{
    public class Union
    {
        public unsafe class MsgLeagueOpt : Revora.Network.Writer
        {
            private byte[] Packet;
            public MsgLeagueOpt(bool Create)
            {
                if (Create)
                {
                    Packet = new byte[23 + 8];
                    fixed (byte* ptr = Packet)
                    {
                        *(ushort*)(ptr + 0) = (ushort)(Packet.Length - 8);
                        *(ushort*)(ptr + 2) = (ushort)PacketID.MsgLeagueOpt;
                    }
                }
            }

            public Union.Actions Action
            {
                get { return (Union.Actions)Revora.BitConverter.ToUInt16(Packet, 4); }
                set { fixed (byte* Pack = Packet) *((ushort*)(Pack + 4)) = (ushort)value; }
            }
            public uint dwParam2
            {
                get { return Revora.BitConverter.ToUInt32(Packet, 6); }
                set { fixed (byte* Pack = Packet) *((uint*)(Pack + 6)) = value; }
            }
            public uint UID
            {
                get { return Revora.BitConverter.ToUInt32(Packet, 10); }
                set { fixed (byte* Pack = Packet) *((uint*)(Pack + 10)) = value; }
            }
            public uint dwParam3
            {
                get { return Revora.BitConverter.ToUInt32(Packet, 14); }
                set { fixed (byte* Pack = Packet) *((uint*)(Pack + 14)) = value; }
            }
            public byte[] ToArray()
            {
                fixed (void* Pack = Packet)
                    return Packet;
            }
            public void Deserialize(byte[] buffer)
            {
                Packet = buffer;
            }
        }
        public static Counter UnionCounter = new Counter(0);
        public enum Actions : ushort
        {
            LeagueInfo = 0,
            LeagueSynList = 1,
            LeagueGuildMemList = 2,
            LeagueMemList = 3,
            LeagueAllegianceList = 4,
            KingdomIcon = 5,
            ChangeNameGui = 6,
            CreateLeague = 7,
            JoinLeagueOthers = 8,
            QuitLeague = 9,
            ExpelMember = 10,
            GuildJoinLeague = 11,
            GuildQuitLeague = 12,
            ExpelGuild = 13,
            TransferLeader = 14,
            SendLeagueRank = 15,
            SendAllLeagueRank = 16,
            KingdomTitleGui = 17,
            CreateKingdomTitle = 18,
            Declarationmsg = 20,
            LeaguePalaceGuardsList = 23,
            AppointPalaceGuard = 24,
            RemovePalaceGuard = 25,
            ReSignPalaceGuard = 26,
            Coreofficals = 27,
            AppointCoreOffical = 28,
            RemoveCoreOffical = 29,
            ReSignCoreOffical = 30,
            Officals = 34,
            SendLeagueOrderStatu = 37,
            SaveLeagueOrderStatu = 38,
            Stipend = 39,
            LeagueConcubinesList = 40,
            AppointLeagueConcubines = 41,
            RemoveHarem = 44,
            AppointCore = 47,
            AppointGuard = 48,
            AppointHarem = 49,
            Bulletionmsg = 66,
        }
        public static void Prosses(byte[] packet, GameState client)
        {
            if(packet==null || packet.Length<28 || packet.Length>512
                || Revora.BitConverter.ToUInt16(packet,0)!=packet.Length-8
                || Revora.BitConverter.ToUInt16(packet,2)!=(ushort)PacketID.MsgLeagueOpt
                || Revora.BitConverter.ToUInt64(packet,packet.Length-8)!=Revora.Network.PacketHandler.ClientSeal
                || !Revora.Game.Features.QuestInventory.Player(client,false))return;
            lock(Revora.Game.Features.UnionFormation.Sync) {
                if(!Revora.Game.Features.QuestInventory.Player(client,false))return;
                ProcessOwned(packet,client);
            }
        }
        private static void ProcessOwned(byte[] packet,GameState client) {
            if(Revora.Game.Features.UnionOffices.TryHandle(packet,client))return;
            Union.MsgLeagueOpt LeagueOpt = new Union.MsgLeagueOpt(false);
            LeagueOpt.Deserialize(packet);
            switch (LeagueOpt.Action)
            {
                case Actions.SendLeagueOrderStatu:
                    {
                        if (client.Union != null)
                        {
                            client.Send(client.Union.SendLeagueOrderStatu());
                        }
                        break;
                    }
                case Actions.SendAllLeagueRank:
                    {
                        if (client.Union != null)
                        {
                            client.Send(client.Union.SendAllLeagueRank());
                        }
                        break;
                    }
                case Union.Actions.SendLeagueRank:
                    {
                        if (client.Union != null)
                        {
                            client.Send(client.Union.SendLeagueRank());
                        }
                        break;
                    }
                case Union.Actions.ChangeNameGui:
                    {
                        if (client.Union != null)
                        {
                            if (client.Union.IsLeader(client.Entity.UID))
                            {
                                string NewName = Encoding.Default.GetString(packet, 20, packet[19]);
                                client.Union.Name = NewName;
                                client.Send(client.Union.SendOverheadLeagueInfo(client));
                                foreach (var player in Kernel.GamePool.Values)
                                {
                                    if (player.Union != null)
                                    {
                                        if (player.Union.ID == client.UnionID)
                                            client.Send(client.Union.SendOverheadLeagueInfo(player));
                                    }
                                }
                            }
                        }
                        break;
                    }
                case Actions.LeaguePalaceGuardsList:
                    {
                        if (client.Union != null)
                        {
                            client.Send(client.Union.ShowLeaguePalaceGuardsList((byte)LeagueOpt.dwParam3));
                        }
                        break;
                    }
                case Actions.Declarationmsg:
                    {
                        if (client.Union != null)
                        {
                            if (client.Union.IsLeader(client.Entity.UID))
                            {
                                string msg = Encoding.Default.GetString(packet, 20, packet[19]);
                                client.Union.Declaration = msg;
                            }
                        }
                        break;
                    }
                case Actions.Bulletionmsg:
                    {
                        if (client.Union != null)
                        {
                            if (client.Union.IsLeader(client.Entity.UID))
                            {
                                string msg = Encoding.Default.GetString(packet, 20, packet[19]);
                                client.Union.Bulletin = msg;
                            }
                        }
                        break;
                    }
                case Actions.CreateKingdomTitle:
                    {
                        if (client.Union != null && client.Union.IsLeader(client.Entity.UID))
                        {
                            string name = Encoding.Default.GetString(packet, 20, packet[19]);
                            if (name.Length <= 5)
                                client.Union.Title = name;
                        }
                        break;
                    }
                case Actions.CreateLeague:
                    {
                        Revora.Game.Features.UnionFormation.Create(client,packet);
                        break;
                    }
                case Actions.Coreofficals:
                    {
                        if (client.Union != null)
                        {
                            client.Send(client.Union.ShowLeagueImperialCourtList());
                        }
                        break;
                    }
                case Actions.Officals:
                    {
                        if (client.Union != null)
                        {
                            client.Send(client.Union.ShowLeagueImperialCourt());
                        }
                        break;
                    }
                case Actions.LeagueConcubinesList:
                    {
                        client.Send(client.Union.ShowLeagueConcubinesList());
                        break;
                    }
                case Actions.LeagueInfo:
                    {
                        if (client.Union != null)
                        {
                            client.Send(client.Union.SendLeagueInfo());
                        }
                        break;
                    }
                case Actions.LeagueSynList:
                    {
                        if (client.Union != null)
                        {
                            client.Send(client.Union.SendLeagueSynList((byte)LeagueOpt.dwParam3));
                        }
                        break;
                    }
                case Actions.LeagueGuildMemList:
                    {
                        if (client.Union != null)
                        {
                            client.Send(client.Union.ShowLeagueGuildMemList((ushort)LeagueOpt.dwParam2, (ushort)LeagueOpt.dwParam3));
                        }
                        break;
                    }
                case Actions.LeagueMemList:
                    {
                        if (client.Union != null)
                        {
                            client.Send(client.Union.ShowLeagueMemList((ushort)LeagueOpt.dwParam2, (ushort)LeagueOpt.dwParam3));
                        }
                        break;
                    }
                case Actions.LeagueAllegianceList:
                    {
                        if (client.Union == null)
                        {
                            client.Send(UnionClass.SendLeagueAllegianceList((byte)LeagueOpt.dwParam3));
                        }
                        break;
                    }
                case Actions.GuildJoinLeague:
                    Revora.Game.Features.UnionGuildMembership.Change(client,client.Guild==null?0:client.Guild.ID,LeagueOpt.dwParam3,true,false);
                    break;
                case Actions.GuildQuitLeague:
                    Revora.Game.Features.UnionGuildMembership.Change(client,client.Guild==null?0:client.Guild.ID,client.UnionID,false,false);
                    break;
                case Actions.JoinLeagueOthers:
                    Revora.Game.Features.UnionMembership.Change(client,LeagueOpt.dwParam3,true);
                    break;
                case Actions.QuitLeague:
                    Revora.Game.Features.UnionMembership.Change(client,client.UnionID,false);
                    break;
                case Actions.ExpelMember:
                    Revora.Game.Features.UnionExpulsion.Remove(client,LeagueOpt.dwParam3);
                    break;
                case Actions.ExpelGuild:
                    Revora.Game.Features.UnionGuildMembership.Change(client,LeagueOpt.dwParam2,client.UnionID,false,true);
                    break;
                case Actions.TransferLeader:
                    {
                        Revora.Game.Features.UnionSuccession.Transfer(client,LeagueOpt.UID);
                        break;
                    }
            }
        }
        [ProtoContract]
        public class OverheadLeagueInfo
        {
            [ProtoMember(1, IsRequired = true)]
            public uint Type;
            [ProtoMember(2, IsRequired = true)]
            public uint UID;
            [ProtoMember(3, IsRequired = true)]
            public uint UnionID;
            [ProtoMember(4, IsRequired = true)]
            public uint PlayerRank;
            [ProtoMember(5, IsRequired = true)]
            public uint UnionRank;
            [ProtoMember(6, IsRequired = true)]
            public uint ServerID;
            [ProtoMember(7, IsRequired = true)]
            public string Name;
        }
        [ProtoContract]
        public class MsgImperialHaremStartPacket
        {
            [ProtoMember(1, IsRequired = true)]
            public uint count;
            [ProtoMember(2, IsRequired = true)]
            public List<MsgImperialHarem> Accounts { get; set; }
        }
        [ProtoContract]
        public class LeagueOrderStatuProto
        {
            [ProtoMember(1, IsRequired = true)]
            public uint Unknown;
            [ProtoMember(4, IsRequired = true)]
            public uint Unknown2;
            [ProtoMember(7, IsRequired = true)]
            public uint Unknown3;
            [ProtoMember(10, IsRequired = true)]
            public uint Unknown4;
            [ProtoMember(13, IsRequired = true)]
            public uint Unknown5;
        }
        [ProtoContract]
        public class MsgImperialHarem
        {
            [ProtoMember(1, IsRequired = true)]
            public uint UID;
            [ProtoMember(2, IsRequired = true)]
            public uint PositionRank;
            [ProtoMember(3, IsRequired = true)]
            public string Name;
            [ProtoMember(4, IsRequired = true)]
            public uint Class;
            [ProtoMember(5, IsRequired = true)]
            public uint Level;
            [ProtoMember(6, IsRequired = true)]
            public uint BattlePower;
            [ProtoMember(7, IsRequired = true)]
            public uint Exploits;
            [ProtoMember(8, IsRequired = true)]
            public uint Nobility;
            [ProtoMember(9, IsRequired = true)]
            public uint Online_Offline;
            [ProtoMember(10, IsRequired = true)]
            public uint Mesh;
        }
    }
}
