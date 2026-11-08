// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.Text;

namespace Revora.Network.GamePackets
{
    public unsafe class VIPTeleport : Writer, Interfaces.IPacket
    {
        private Byte[] Buffer;
        public enum VIPTeleportLocations
        {
            TwinCity = 1,
            PhoenixCastle = 2,
            ApeCity = 3,
            DesertCity = 4,
            BirdIland = 5,
            TCSquare = 6,
            WPFarm = 7,
            WPBridge = 8,
            WPAltar = 9,
            WPApparation = 10,
            WPPoltergiest = 11,
            WPTurtledove = 12,
            PCSqaure = 13,
            MFWaterCave = 14,
            MFVillage = 15,
            MFLake = 16,
            MFMineCave = 17,
            MFBridge = 18,
            MFToApeCity = 19,
            ACSquare = 20,
            ACSouth = 21,
            ACEast = 22,
            ACNorth = 23,
            ACWest = 24,
            BISquare = 25,
            BICenter = 26,
            BISouthWest = 27,
            BINorthWest = 28,
            BINorthEast = 29,
            DCSquare = 30,
            DCSouth = 31,
            DCVillage = 32,
            DCMoonSpring = 33,
            DCAncientMaze = 34
        }
        public enum VIPTeleportTypes
        {
            SelfTeleport = 0,
            TeamTeleport = 1,
            TeammateConfirmation = 2,
            TeammateTeleport = 3,
        }
        public VIPTeleport()
        {
            this.Buffer = new Byte[32 + 8];
            WriteUInt16(32, 0, Buffer);
            WriteUInt16(1128, 2, Buffer);
        }
        public VIPTeleportTypes TeleportType
        {
            get { return (VIPTeleportTypes)BitConverter.ToUInt32(this.Buffer, 4); }
            set { WriteUInt32((UInt32)value, 4, Buffer); }
        }
        public VIPTeleportLocations Location
        {
            get { return (VIPTeleportLocations)BitConverter.ToUInt32(this.Buffer, 8); }
            set { WriteUInt32((UInt32)value, 8, Buffer); }
        }
        public Byte Countdown
        {
            get { return Buffer[12]; }
            set { Buffer[12] = value; }
        }
        public String Name
        {
            get { return Encoding.ASCII.GetString(Buffer, 17, Buffer[16]); }
            set { WriteString(value, 16, Buffer); }
        }
        public void Send(Client.GameState client)
        {
            client.Send(Buffer);
        }
        public void Deserialize(byte[] buffer)
        {
            Buffer = buffer;
        }
        public byte[] ToArray()
        {
            return Buffer;
        }
        public static void Process(byte[] packet, Client.GameState client)
        {
            if (client.Entity.MapID == 601 || (client.Map.BaseID == 1038 && Game.GuildWar.IsWar)) return;
            if (client.Map.BaseID == 3846 || client.Map.BaseID == 6000 || client.Map.BaseID == 6001 ||
                client.Map.BaseID == 6002 || client.Map.BaseID == 6003 || client.Map.BaseID == 6004 ||
                client.Map.BaseID == 1801 || client.Map.BaseID == 8883 || client.Map.BaseID == 1005 &&
                client.Entity.MapID == 1005 || client.Map.BaseID == 700 || client.Map.BaseID == 4573 ||
                client.Map.BaseID == 4574 || client.Map.BaseID == 4575 || client.Map.BaseID == 4576 ||
                client.Map.BaseID == 4577 || client.Map.BaseID == 4578)
            {
                client.Send(Constants.vipteleport); return;
            }
            VIPTeleport tele = new VIPTeleport();
            tele.Deserialize(packet);
            switch (tele.TeleportType)
            {
                case VIPTeleportTypes.SelfTeleport:
                    {
                        if (client.Account.State == Database.AccountTable.AccountState.Administrador)
                        {
                            Teleport(client, tele.Location);
                        }
                        else if (Time32.Now > client.LastVIPTeleport.AddSeconds(1))
                        {
                            client.LastVIPTeleport = Time32.Now;
                            Teleport(client, tele.Location);
                        }
                        else client.Send(new Message(string.Format("You have to wait {0} more seconds to use the VIP Teleport.", (client.LastVIPTeleport.AddMinutes(1) - Time32.Now).AllSeconds()), System.Drawing.Color.Red, Message.Talk));
                        break;
                    }
                case VIPTeleportTypes.TeamTeleport:
                    {
                        if (client.Account.State == Database.AccountTable.AccountState.Administrador)
                        {
                            if (client.Team != null)
                            {
                                foreach (var teammate in client.Team.Teammates)
                                {
                                    if (teammate.Entity.UID != client.Entity.UID)
                                    {
                                        teammate.Send(new VIPTeleport()
                                        {
                                            Countdown = 10,
                                            Location = tele.Location,
                                            Name = client.Entity.Name,
                                            TeleportType = VIPTeleportTypes.TeammateConfirmation
                                        }.ToArray());
                                    }
                                }
                            }
                            Teleport(client, tele.Location);
                        }
                        else if (Time32.Now > client.LastVIPTeamTeleport.AddMinutes(1))
                        {
                            client.LastVIPTeamTeleport = Time32.Now;
                            if (client.Team != null)
                            {
                                foreach (var teammate in client.Team.Teammates)
                                {
                                    if (teammate.Entity.UID != client.Entity.UID)
                                    {
                                        teammate.Send(new VIPTeleport()
                                        {
                                            Countdown = 10,
                                            Location = tele.Location,
                                            Name = client.Entity.Name,
                                            TeleportType = VIPTeleportTypes.TeammateConfirmation
                                        }.ToArray());
                                    }
                                }
                            }
                            Teleport(client, tele.Location);
                        }
                        else client.Send(new Message(string.Format("You have to wait {0} more seconds to use the VIP Team Teleport.", (client.LastVIPTeamTeleport.AddMinutes(1) - Time32.Now).AllSeconds()), System.Drawing.Color.Red, Message.Talk));
                        break;
                    }
                case VIPTeleportTypes.TeammateTeleport:
                    {
                        Teleport(client, tele.Location);
                        break;
                    }
                default: Console.WriteLine("[VIPTeleport] Unknown Action: " + tele.TeleportType + " "); break;
            }
        }
        public static void Teleport(Client.GameState client, VIPTeleportLocations Location)
        {
            switch (Location)
            {
                case VIPTeleportLocations.TwinCity:
                case VIPTeleportLocations.TCSquare: client.Entity.Teleport((ushort)Game.Enums.Maps.TwinCity, 408, 330); break;
                case VIPTeleportLocations.WPFarm: client.Entity.Teleport((ushort)Game.Enums.Maps.TwinCity, 266, 174); break;
                case VIPTeleportLocations.WPBridge: client.Entity.Teleport((ushort)Game.Enums.Maps.TwinCity, 470, 578); break;
                case VIPTeleportLocations.WPAltar: client.Entity.Teleport((ushort)Game.Enums.Maps.TwinCity, 436, 693); break;
                case VIPTeleportLocations.WPApparation: client.Entity.Teleport((ushort)Game.Enums.Maps.TwinCity, 212, 450); break;
                case VIPTeleportLocations.WPPoltergiest: client.Entity.Teleport((ushort)Game.Enums.Maps.TwinCity, 90, 285); break;
                case VIPTeleportLocations.WPTurtledove: client.Entity.Teleport((ushort)Game.Enums.Maps.TwinCity, 627, 374); break;

                case VIPTeleportLocations.PhoenixCastle:
                case VIPTeleportLocations.PCSqaure: client.Entity.Teleport((ushort)Game.Enums.Maps.PhoenixCastle, 188, 264); break;
                case VIPTeleportLocations.MFWaterCave: client.Entity.Teleport((ushort)Game.Enums.Maps.PhoenixCastle, 380, 31); break;
                case VIPTeleportLocations.MFVillage: client.Entity.Teleport((ushort)Game.Enums.Maps.PhoenixCastle, 785, 472); break;
                case VIPTeleportLocations.MFLake: client.Entity.Teleport((ushort)Game.Enums.Maps.PhoenixCastle, 369, 568); break;
                case VIPTeleportLocations.MFMineCave: client.Entity.Teleport((ushort)Game.Enums.Maps.PhoenixCastle, 924, 560); break;
                case VIPTeleportLocations.MFBridge: client.Entity.Teleport((ushort)Game.Enums.Maps.PhoenixCastle, 648, 567); break;
                case VIPTeleportLocations.MFToApeCity: client.Entity.Teleport((ushort)Game.Enums.Maps.PhoenixCastle, 475, 841); break;

                case VIPTeleportLocations.ApeCity:
                case VIPTeleportLocations.ACSquare: client.Entity.Teleport((ushort)Game.Enums.Maps.ApeMoutain, 565, 562); break;
                case VIPTeleportLocations.ACSouth: client.Entity.Teleport((ushort)Game.Enums.Maps.ApeMoutain, 699, 640); break;
                case VIPTeleportLocations.ACEast: client.Entity.Teleport((ushort)Game.Enums.Maps.ApeMoutain, 624, 337); break;
                case VIPTeleportLocations.ACNorth: client.Entity.Teleport((ushort)Game.Enums.Maps.ApeMoutain, 200, 224); break;
                case VIPTeleportLocations.ACWest: client.Entity.Teleport((ushort)Game.Enums.Maps.ApeMoutain, 322, 621); break;

                case VIPTeleportLocations.DesertCity:
                case VIPTeleportLocations.DCSquare: client.Entity.Teleport((ushort)Game.Enums.Maps.DesertCity, 500, 650); break;
                case VIPTeleportLocations.DCSouth: client.Entity.Teleport((ushort)Game.Enums.Maps.DesertCity, 758, 750); break;
                case VIPTeleportLocations.DCVillage: client.Entity.Teleport((ushort)Game.Enums.Maps.DesertCity, 480, 271); break;
                case VIPTeleportLocations.DCMoonSpring: client.Entity.Teleport((ushort)Game.Enums.Maps.DesertCity, 291, 450); break;
                case VIPTeleportLocations.DCAncientMaze: client.Entity.Teleport((ushort)Game.Enums.Maps.DesertCity, 87, 321); break;

                case VIPTeleportLocations.BirdIland:
                case VIPTeleportLocations.BISquare: client.Entity.Teleport((ushort)Game.Enums.Maps.BirdIsland, 717, 571); break;
                case VIPTeleportLocations.BICenter: client.Entity.Teleport((ushort)Game.Enums.Maps.BirdIsland, 585, 593); break;
                case VIPTeleportLocations.BISouthWest: client.Entity.Teleport((ushort)Game.Enums.Maps.BirdIsland, 562, 786); break;
                case VIPTeleportLocations.BINorthWest: client.Entity.Teleport((ushort)Game.Enums.Maps.BirdIsland, 125, 323); break;
                case VIPTeleportLocations.BINorthEast: client.Entity.Teleport((ushort)Game.Enums.Maps.BirdIsland, 125, 323); break;
            }
        }
    }
}