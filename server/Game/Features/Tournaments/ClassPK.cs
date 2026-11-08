// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Revora.Network.GamePackets;

namespace Revora.Game
{
    public unsafe class ClassPk
    {
        public static bool ClassPks = false;
        public static ushort Map = 7001;
        public static bool signup = false;
        public static int howmanyinmap = 0;
        public static int howmanyinmap1 = 0;
        public static int howmanyinmap16 = 0;
        public static int howmanyinmap17 = 0;
        public static int howmanyinmap19 = 0;
        public static int TopDlClaim = 0;
        public static int TopGlClaim = 0;
        public static int ClanClaim = 0;
        public static void AddDl()
        {
            TopDlClaim++;
            //return;
        }
        public static void AddGl()
        {
            TopGlClaim++;
            //return;
        }
        public static void CheackAlive16()
        {
            howmanyinmap16 = 0;
            foreach (Client.GameState client in Server.GamePool)
            {
                if (client.Entity.MapID == 1365 && client.Entity.Hitpoints >= 1)
                {
                    howmanyinmap16 += 1;
                    Revora.Kernel.SendWorldMessage(new Revora.Network.GamePackets.Message("Players Alive in Ms.ConquerHost: " + howmanyinmap16 + " ", System.Drawing.Color.Black, Revora.Network.GamePackets.Message.FirstRightCorner), Server.GamePool);
                }

            }
        }
        public static void CheackAlive17()
        {
            howmanyinmap17 = 0;
            foreach (Client.GameState client in Server.GamePool)
            {
                if (client.Entity.MapID == 1366 && client.Entity.Hitpoints >= 1)
                {
                    howmanyinmap17 += 1;
                    Revora.Kernel.SendWorldMessage(new Revora.Network.GamePackets.Message("Players Alive in Mr.ConquerHost: " + howmanyinmap17 + " ", System.Drawing.Color.Black, Revora.Network.GamePackets.Message.FirstRightCorner), Server.GamePool);
                }

            }
        }
        public static void CheackAlive19()
        {
            howmanyinmap19 = 0;
            foreach (Client.GameState client in Server.GamePool)
            {
                if (client.Entity.MapID == 1368 && client.Entity.Hitpoints >= 1)
                {
                    howmanyinmap19 += 1;
                    Revora.Kernel.SendWorldMessage(new Revora.Network.GamePackets.Message("Players Alive in Mr.ChinaHost: " + howmanyinmap19 + " ", System.Drawing.Color.Black, Revora.Network.GamePackets.Message.FirstRightCorner), Server.GamePool);
                }

            }
        }
        public static void CheackAlive1()
        {
            howmanyinmap1 = 0;
            foreach (Client.GameState client in Server.GamePool)
            {
                if (client.Entity.MapID == 1367 && client.Entity.Hitpoints >= 1)
                {
                    howmanyinmap1 += 1;
                    Revora.Kernel.SendWorldMessage(new Revora.Network.GamePackets.Message("Players Alive in Ms.ChinaHost: " + howmanyinmap1 + " ", System.Drawing.Color.Black, Revora.Network.GamePackets.Message.FirstRightCorner), Server.GamePool);
                }

            }
        }  
        public static void CheackAlive()
        {
            howmanyinmap = 0;
            foreach (Client.GameState client in Server.GamePool)
            {
                if (client.Entity.MapID == 7001 && client.Entity.Hitpoints >= 1)
                {
                    howmanyinmap += 1;
                    Revora.Kernel.SendWorldMessage(new Revora.Network.GamePackets.Message("Players Alive in ClassPk Now: " + howmanyinmap + " ", System.Drawing.Color.Black, Revora.Network.GamePackets.Message.FirstRightCorner), Server.GamePool);
                }
            }
        }
        public static void SignUp()
        {
            signup = true;
            ClassPks = true;
            var date = DateTime.Now;
            foreach (Client.GameState client in Server.GamePool)
                if (date.Minute == 00 && client.Entity.Class >= 10 && client.Entity.Class <= 15)
                    client.Entity.RemoveTopStatus((ulong)Network.GamePackets.FlagsUpdate.Flags.TopTrojan);
        }
        public static void SignUp1()
        {
            signup = true;
            ClassPks = true;
            var date = DateTime.Now;
            foreach (Client.GameState client in Server.GamePool)
                if (date.Minute == 00 && client.Entity.Class >= 20 && client.Entity.Class <= 25)
                    client.Entity.RemoveTopStatus((ulong)Network.GamePackets.FlagsUpdate.Flags.TopWarrior);
        }
        public static void SignUp2()
        {
            signup = true;
            ClassPks = true;
            var date = DateTime.Now;
            foreach (Client.GameState client in Server.GamePool)
                if (date.Minute == 00 && client.Entity.Class >= 40 && client.Entity.Class <= 45)
                    client.Entity.RemoveTopStatus((ulong)Network.GamePackets.FlagsUpdate.Flags.TopArcher);
        }
        public static void SignUp3()
        {
            signup = true;
            ClassPks = true;
            var date = DateTime.Now;
            foreach (Client.GameState client in Server.GamePool)
                if (date.Minute == 00 && client.Entity.Class >= 50 && client.Entity.Class <= 55)
                    client.Entity.RemoveTopStatus((ulong)Network.GamePackets.FlagsUpdate.Flags.TopNinja);
        }
        public static void SignUp4()
        {
            signup = true;
            ClassPks = true;
            var date = DateTime.Now;
            foreach (Client.GameState client in Server.GamePool)
                if (date.Minute == 00 && client.Entity.Class >= 60 && client.Entity.Class <= 65)
                    client.Entity.RemoveTopStatus((ulong)Network.GamePackets.FlagsUpdate.Flags.TopMonk);
        }
        public static void SignUp5()
        {
            signup = true;
            ClassPks = true;
            var date = DateTime.Now;
            foreach (Client.GameState client in Server.GamePool)
                if (date.Minute == 00 && client.Entity.Class >= 130 && client.Entity.Class <= 135)
                    client.Entity.RemoveTopStatus((ulong)Network.GamePackets.FlagsUpdate.Flags.TopWaterTaoist);
        }
        public static void SignUp6()
        {
            signup = true;
            ClassPks = true;
            var date = DateTime.Now;
            foreach (Client.GameState client in Server.GamePool)
                if (date.Minute == 00 && client.Entity.Class >= 140 && client.Entity.Class <= 145)
                    client.Entity.RemoveTopStatus((ulong)Network.GamePackets.FlagsUpdate.Flags.TopFireTaoist);
        }
        public static void SignUp8()
        {
            signup = true;
            ClassPks = true;
            var date = DateTime.Now;
            foreach (Client.GameState client in Server.GamePool)
                if (date.Minute == 00 && client.Entity.Class >= 70 && client.Entity.Class <= 75)
                    client.Entity.RemoveTopStatus((ulong)Network.GamePackets.FlagsUpdate.Flags.TopPirate);
        }
        public static void SignUp9()
        {
            signup = true;
            ClassPks = true;
            var date = DateTime.Now;
            foreach (Client.GameState client in Server.GamePool)
                if (date.Minute == 00 && client.Entity.Class >= 80 && client.Entity.Class <= 85)
                    client.Entity.RemoveTopStatus((ulong)Network.GamePackets.FlagsUpdate.Flags.DragonWarriorTop);
        }
        public static void End()
        {
            if (DateTime.Now.Minute == 59)
            {
                signup = false;
                ClassPks = false;
                foreach (Client.GameState client in Server.GamePool)
                {
                    if (DateTime.Now.Minute == 59)
                    {
                        client.Entity.AddConquerPoints(150);
                        Revora.Kernel.SendWorldMessage(new Network.GamePackets.Message(" ClassPk Has Ended Come Next Week ", System.Drawing.Color.Red, Revora.Network.GamePackets.Message.TopLeft), Server.GamePool);
                    }
                    if (client.Entity.MapID == 7001)
                    {
                        client.Entity.Teleport(1002, 408, 330);
                    }
                    client.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.Flashy);
                }
            }
        }
    }
}