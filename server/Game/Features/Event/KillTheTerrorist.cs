// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using Revora.Network.GamePackets;

namespace Revora.Game
{
    public unsafe class KillTheTerrorist
    {
        public static bool IsOn = false;
        public static bool Terrorist = false;
        public static ushort Map = 1801;
        public static void SendTimer()
        {
            System.Timers.Timer TimerA = new System.Timers.Timer(1000.0);
            TimerA.Start();
            TimerA.Elapsed += delegate { SignUp(); };

            System.Timers.Timer TimerB = new System.Timers.Timer(1000.0);
            TimerB.Start();
            TimerB.Elapsed += delegate { Send(); };

            System.Timers.Timer TimerC = new System.Timers.Timer(1000.0);
            TimerC.Start();
            TimerC.Elapsed += delegate { End(); };

            System.Timers.Timer TimerD = new System.Timers.Timer(1000.0);
            TimerD.Start();
            TimerD.Elapsed += delegate { TeleEnd(); };
        }
        public static bool signup = false;
        public static bool send = false;
        public static bool end = false;
        public static bool teleend = false;
        public static void SignUp()
        {
            if (DateTime.Now.Hour == 16 && DateTime.Now.Minute == 3 && signup == false)
            {
                send = false;
                end = false;
                teleend = false;
                signup = true;
                IsOn = true;
                Revora.Kernel.SendWorldMessage(new Revora.Network.GamePackets.Message("KillTheTerrorist have started. Sign Up in TwinCity! You have one minute", System.Drawing.Color.Red, Revora.Network.GamePackets.Message.Center), Server.GamePool);
            }
        }
        public static void Send()
        {
            if (DateTime.Now.Hour == 16 && DateTime.Now.Minute == 4 && send == false)
            {
                send = true;
                Terrorist = false;
                foreach (Client.GameState client in Server.GamePool)
                {
                    if (client.Entity.Tournament_Signed == true && client.Entity.KillTheTerrorist_IsTerrorist == false)
                    {
                        client.Entity.SpawnProtection = true;
                        client.Entity.Teleport(1801, 55, 55);
                        client.Entity.Hitpoints = 1;
                    }
                    if (client.Entity.Tournament_Signed == true && client.Entity.KillTheTerrorist_IsTerrorist == true)
                    {
                        client.Entity.Teleport(1801, 55, 50);
                        client.Entity.Hitpoints = 1;
                    }
                }
            }
        }
        public static void End()
        {
            if (DateTime.Now.Hour == 16 && DateTime.Now.Minute == 14 && end == false)
            {
                signup = false;
                end = true;
                Terrorist = false;
                IsOn = false;
                foreach (Client.GameState client in Server.GamePool)
                {
                    if (client.Entity.KillTheTerrorist_IsTerrorist == true)
                    {
                        client.Entity.AddConquerPoints(150);
                        Revora.Kernel.SendWorldMessage(new Revora.Network.GamePackets.Message(":" + client.Entity.Name + " was the terrorist in the end and have won 150 cps ", System.Drawing.Color.Red, Revora.Network.GamePackets.Message.Center), Server.GamePool);
                    }
                    if (client.Entity.MapID == 1801)
                    {
                        client.Entity.Teleport(1002, 408, 330);
                    }
                    client.Entity.KillTheTerrorist_IsTerrorist = false;
                    client.Entity.Tournament_Signed = false;
                }
            }
        }
        public static void TeleEnd()
        {
            if (DateTime.Now.Hour == 16 && DateTime.Now.Minute == 10)
            {
                Terrorist = false;
                IsOn = false;
                foreach (Client.GameState client in Server.GamePool)
                {
                    if (client.Entity.MapID == 1801)
                    {
                        client.Entity.Teleport(1002, 408, 330);
                    }
                    if (client.Entity.KillTheTerrorist_IsTerrorist == true)
                    {
                        client.Entity.AddConquerPoints(100);
                        Revora.Kernel.SendWorldMessage(new Revora.Network.GamePackets.Message(":" + client.Entity.Name + " was the terrorist in the end and have won 100 cps", System.Drawing.Color.Red, Revora.Network.GamePackets.Message.Center), Server.GamePool);
                    }
                    client.Entity.KillTheTerrorist_IsTerrorist = false;
                    client.Entity.Tournament_Signed = false;
                }
            }
        }
    }
}