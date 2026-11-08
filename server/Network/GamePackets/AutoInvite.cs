// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using Revora.Game;

namespace Revora.Network.GamePackets
{
    public unsafe class AutoInvite
    {
        public static bool Enabled = false;

        public enum Mode : uint
        {
            CaptureFlag = 10535,
            SkillTeam = 10541,
            PowerArena = 10531,
            ElitePk = 10533,
            TeamPk = 10543,
            HorseRace = 10525,
            MonthlyPk = 10523,
            ClassPKWar = 10519,
            GuildWar = 10515,
            WeeklyPk = 10521,
            Champions = 10549,
            Lastman = 10550,
            DeathWar = 10551,
            FastScent = 10552,
            ExtremePK = 10553,
            Terrorist = 10554,
            Couples = 10555,
            Treasure = 10556,
            DemonBox = 10557,
            DisCity = 10558,
            SuperGuildWar = 10559,
            SkyFight = 10560,
            TwinCityWar = 10561,
            TeamQualifier = 10562,
             PlunderWar = 10573,
            HeroOfGame = 10563,
            Devastated = 10564,
            Quarantine = 10565
        }
        byte[] Packet;
        public AutoInvite()
        {
            Packet = new byte[28];
            fixed (byte* ptr = Packet)
            {
                *(ushort*)(ptr + (Packet.Length - 8)) = 0;
                *((ushort*)(ptr + 2)) = PacketID.AutoInvite;
            }
        }
        public uint StrResID
        {
            get { return BitConverter.ToUInt32(Packet, 8); }
            set { fixed (byte* Pack = Packet) *((uint*)(Pack + 8)) = value; }
        }
        public uint InviteID
        {
            get { return BitConverter.ToUInt32(Packet, 8); }
            set { fixed (byte* Pack = Packet) *((uint*)(Pack + 8)) = value; }
        }
        public byte Action
        {
            get { return (byte)Packet[12]; }
            set { fixed (byte* Pack = Packet) *((byte*)(Pack + 12)) = value; }
        }
        public uint Countdown
        {
            get { return BitConverter.ToUInt32(Packet, 16); }
            set { fixed (byte* Pack = Packet) *((uint*)(Pack + 16)) = value; }
        }
        public byte[] ToArray()
        {
            fixed (void* buff = Packet)
                return Packet;
        }
        public void Deserialize(byte[] Pack)
        {
            Packet = Pack;
        }
        public static void Process(byte[] Packet, Client.GameState client)
        {
            if (!Enabled)
                return;
            AutoInvite Auto = new AutoInvite();
            Auto.Deserialize(Packet);
            switch (client.Entity.StrResID)
            {
                case (uint)AutoInvite.Mode.HeroOfGame:
                    {
                        if (client.Entity.Dead) return;
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
                        client.Entity.PKMode = Game.Enums.PKMode.PK;
                        client.Send(new Data(true)
                        {
                            UID = client.Entity.UID,
                            ID = Data.ChangePKMode,
                            dwParam = (uint)client.Entity.PKMode
                        });
                        client.Entity.Teleport(1507, 96, 113);
                        client.Entity.StrResID = 0;
                        break;
                    }
                case (uint)AutoInvite.Mode.TeamQualifier:
                    {
                        if (client.Entity.Dead) return;
                        Game.TeamQualifier.QualifyEngine.DoSignup(client);
                        client.Entity.StrResID = 0;
                        break;
                    }
                //case(uint)AutoInvite.Mode.PlunderWar:
                //    {
                //        if (client.Union == null) return;
                //        if (client.Player.Dead) return;
                //        client.Player.PKMode = Game.Enums.PKMode.Union;
                //        client.Send(new Data(true)
                //        {
                //            UID = client.Player.UID,
                //            ID = Data.ChangePKMode,
                //            dwParam = (uint)client.Player.PKMode
                //        });
                //        var Map = client.Map.RandomCoordinates();
                //        client.Player.Teleport(3998, 388, 392);
                //        client.Player.InviteID = 0;
                //        client.Player.PuntosUnion = 0;
                //        client.Player.OnUnionWar = true;
                //        break;
                //    }
                case (uint)AutoInvite.Mode.HorseRace:
                    {
                        if (client.Entity.Dead) return;
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
                        if (!SteedRace.isOn)
                        {
                            client.Send(new Message("The tournament has ended.", Message.Center));
                        }
                        else if (SteedRace.isOn && SteedRace.InvitationsExpired)
                        {
                            client.Send(new Message("You have missed your chance to join the horse race.", Message.Center));
                        }
                        else if (SteedRace.InvitationsOut && !SteedRace.InvitationsExpired)
                        {
                            if (!client.Spells.ContainsKey(7001))
                            {
                                client.Send("You need to learn the riding skill!");
                            }
                            else
                            {
                                if (!client.Equipment.Free(ConquerItem.Steed))
                                    Server.Thread.SteedRace.Join(client);
                                else  client.Send("You need to equip a steed!");
                            }
                        }
                        client.Entity.StrResID = 0;
                        break;
                    }
                case (uint)AutoInvite.Mode.CaptureFlag:
                    {
                        if (client.Entity.Dead) return;
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
                        client.Entity.Teleport(1002, 225, 237);
                        client.Entity.StrResID = 0;
                        break;
                    }
                case (uint)AutoInvite.Mode.TwinCityWar:
                    {
                        if (client.Entity.Dead) return;
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
                        Random random = new Random();
                        int r = random.Next(1, 10);
                        if (r == 1) client.Entity.Teleport(1002, 309, 344);
                        if (r == 2) client.Entity.Teleport(1002, 322, 341);
                        if (r == 3) client.Entity.Teleport(1002, 321, 352);
                        if (r == 4) client.Entity.Teleport(1002, 313, 347);
                        if (r == 5) client.Entity.Teleport(1002, 307, 353);
                        if (r == 6) client.Entity.Teleport(1002, 383, 256);
                        if (r == 7) client.Entity.Teleport(1002, 385, 263);
                        if (r == 8) client.Entity.Teleport(1002, 388, 251);
                        if (r == 9) client.Entity.Teleport(1002, 394, 266);
                        if (r == 10) client.Entity.Teleport(1002, 397, 251);
                        client.Entity.StrResID = 0;
                        break;
                    }
                case (uint)AutoInvite.Mode.SkillTeam:
                    {
                        if (client.Entity.Dead) return;
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
                        client.Entity.Teleport(1002, 313, 143);
                        client.Entity.StrResID = 0;
                        break;
                    }
                case (uint)AutoInvite.Mode.PowerArena:
                    {
                        if (client.Entity.Dead) return;
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
                        client.Entity.Teleport(1002, 336, 137);
                        client.Entity.StrResID = 0;
                        break;
                    }
                case (uint)AutoInvite.Mode.ElitePk:
                    {
                        if (client.Entity.Dead) return;
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
                        client.Entity.Teleport(1002, 301, 143);
                        client.Entity.StrResID = 0;
                        break;
                    }
                case (uint)AutoInvite.Mode.TeamPk:
                    {
                        if (client.Entity.Dead) return;
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
                        client.Entity.Teleport(1002, 292, 146);
                        client.Entity.StrResID = 0;
                        break;
                    }
                case (uint)AutoInvite.Mode.MonthlyPk:
                    {
                        if (client.Entity.Dead) return;
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
                        client.Entity.Teleport(1002, 323, 194);
                        client.Entity.StrResID = 0;
                        break;
                    }
                case (uint)AutoInvite.Mode.GuildWar:
                    {
                        if (client.Entity.Dead) return;
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
                        Random R = new Random();
                        int G = R.Next(1, 10);
                        if (G == 1) client.Entity.Teleport(1038, 351, 341);
                        if (G == 2) client.Entity.Teleport(1038, 335, 345);
                        if (G == 3) client.Entity.Teleport(1038, 309, 369);
                        if (G == 4) client.Entity.Teleport(1038, 283, 340);
                        if (G == 5) client.Entity.Teleport(1038, 310, 327);
                        if (G == 6) client.Entity.Teleport(1038, 318, 297);
                        if (G == 7) client.Entity.Teleport(1038, 347, 309);
                        if (G == 8) client.Entity.Teleport(1038, 337, 320);
                        if (G == 9) client.Entity.Teleport(1038, 309, 293);
                        if (G == 10) client.Entity.Teleport(1038, 371, 300);
                        client.Entity.StrResID = 0;
                        break;
                    }
                case (uint)AutoInvite.Mode.SuperGuildWar:
                    {
                        if (client.Entity.Dead) return;
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
                        client.Entity.Teleport(1234, 224, 237);
                        client.Entity.StrResID = 0;
                        break;
                    }
                case (uint)AutoInvite.Mode.WeeklyPk:
                    {
                        if (client.Entity.Dead) return;
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
                        client.Entity.Teleport(1002, 323, 194);
                        client.Entity.StrResID = 0;
                        break;
                    }
                case (uint)AutoInvite.Mode.Champions:
                    {
                        if (client.Entity.Dead) return;
                        Game.Champion.ChampionKernel.SignUp();
                        client.Send(new Message("You have joined Champion Arena!", Message.System));
                        client.Entity.StrResID = 0;
                        break;
                    }
                case (uint)AutoInvite.Mode.Lastman:
                    {
                        if (client.Entity.Dead) return;
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
                            client.Send(new Message("A PK event has just begun. Join in Dragon City (310, 279)!", Message.System));
                        }
                        client.Entity.Teleport(1002, 308, 262);
                        client.Entity.StrResID = 0;
                        break;
                    }
                case (uint)AutoInvite.Mode.DeathWar:
                    {
                        if (client.Entity.Dead) return;
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
                        client.Entity.Teleport(1002, 299, 243);
                        client.Entity.StrResID = 0;
                        break;
                    }
                case (uint)AutoInvite.Mode.FastScent:
                    {
                        if (client.Entity.Dead) return;
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
                        client.Entity.Teleport(1002, 299, 243);
                        client.Entity.StrResID = 0;
                        break;
                    }
                case (uint)AutoInvite.Mode.ExtremePK:
                    {
                        if (client.Entity.Dead) return;
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
                        client.Entity.Teleport(1002, 315, 148);
                        client.Entity.StrResID = 0;
                        break;
                    }
                case (uint)AutoInvite.Mode.Terrorist:
                    {
                        if (client.Entity.Dead) return;
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
                        client.Entity.Teleport(1002, 299, 243);
                        client.Entity.StrResID = 0;
                        break;
                    }
                case (uint)AutoInvite.Mode.Couples:
                    {
                        if (client.Entity.Dead) return;
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
                        client.Entity.Teleport(1002, 292, 191);
                        client.Entity.StrResID = 0;
                        break;
                    }
                case (uint)AutoInvite.Mode.Treasure:
                    {
                        if (client.Entity.Dead) return;
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
                        client.Entity.Teleport(1002, 299, 243);
                        client.Entity.StrResID = 0;
                        break;
                    }
                case (uint)AutoInvite.Mode.DemonBox:
                    {
                        if (client.Entity.Dead) return;
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
                        client.Entity.Teleport(1002, 274, 194);
                        client.Entity.StrResID = 0;
                        break;
                    }
                //case (uint)AutoInvite.Mode.DisCity:
                //    {
                //        if (client.Player.Dead) return;
                //        if (client.Player.MapID == 3846)
                //        {
                //            Data data = new Data(true);
                //            data.UID = client.Player.UID;
                //            data.ID = 104;
                //            data.TimeStamp = Time32.Now;
                //            data.dwParam = 0;
                //            client.Send(data);
                //            client.SendScreen(data, true);
                //            client.Screen.Reload(null);
                //        }
                //        client.Player.Teleport(1002, 297, 149);
                //        client.Player.StrResID = 0;
                //        break;
                //    }
                case (uint)AutoInvite.Mode.SkyFight:
                    {
                        if (client.Entity.Dead) return;
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
                        client.Entity.Teleport(1002, 299, 243);
                        client.Entity.StrResID = 0;
                        break;
                    }
                case (uint)AutoInvite.Mode.Devastated:
                    {
                        if (client.Entity.Dead) return;
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
                        client.Entity.Teleport(1002, 299, 243);
                        client.Entity.StrResID = 0;
                        break;
                    }
                
            }
        }
    }
}