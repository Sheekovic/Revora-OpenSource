
using System;
using System.Linq;

namespace Revora.Game.Features.Tournaments
{
    public unsafe class HeroOfGame
    {
        public HeroOfGame()
        {
            open = false;
        }
       public void CheakUp()
        {
            if (DateTime.Now.Hour == 00 && DateTime.Now.Minute == 00 && !open)
            {
                Start();
            }
        }
        uint Secounds = 0;
        private IDisposable Subscribe;
        bool open = false;
        bool SendInvitation = false;
        public void Start()
        {
            if (!open)
            {
                Secounds = 0;
                SendInvitation = false;
                open = true;
                Subscribe = Thread.Subscribe(Work, 1000);
            }
        }
        public void SendMapMessage(string packet)
        {
            var data = new Network.GamePackets.Message(packet, System.Drawing.Color.Yellow, Network.GamePackets.Message.Center);
            var MapDictionary = Kernel.GamePool.Values.Where(p => p.Entity.MapID == 1507).ToArray();
            foreach (var client in MapDictionary)
            {
                client.Send(data);
            }
        }
        public int CkeckUPAlive()
        {
            int Count = 0;
            var MapDictionary = Kernel.GamePool.Values.Where(p => p.Entity.MapID == 1507).ToArray();
            foreach (var client in MapDictionary)
            {
                if (client.Entity.Hitpoints > 0)
                    Count++;
            }
            return Count;
        }
        public void Close()
        {
            open = false;
            Subscribe.Dispose();
        }
        public void Work(int time)
        {
            if (!SendInvitation)
            {
                foreach (Client.GameState client in Kernel.GamePool.Values)
                {
                    if (!client.InQualifier() && client.Map.BaseID != 6001 && client.Map.BaseID != 6000 && client.Map.BaseID != 6004 && !client.Entity.Dead)
                    {
                        Network.GamePackets.AutoInvite alert = new Network.GamePackets.AutoInvite
                        {
                            StrResID = 10563,
                            Countdown = 60,
                            Action = 1
                        };
                        client.Entity.StrResID = 10563;
                        client.Send(alert.ToArray());
                    }
                }
                SendInvitation = true;
            }
            else
            {
                if (open)
                {
                    if (Secounds == 30)
                        SendMapMessage("Hero OF Game will start in 30 Secounds");
                    else if (Secounds == 40)
                        SendMapMessage("Hero OF Game will start in 20 Secounds");
                    else if (Secounds == 50)
                        SendMapMessage("Hero OF Game will start in 10 Secounds");
                    else if (Secounds == 55)
                        SendMapMessage("Hero OF Game will start in 5 Secounds");
                    else if (Secounds == 58)
                        SendMapMessage("Hero OF Game will start in 3 Secounds");
                    else if (Secounds == 60)
                    {
                        var MapDictionary = Kernel.GamePool.Values.Where(p => p.Entity.MapID == 1507).ToArray();
                        foreach (var client in MapDictionary)
                        {
                            client.Entity.AllowToAttack = true;
                        }
                        SendMapMessage("Fight Now!");
                    }
                    else if (Secounds > 60)
                    {
                        byte[] Messaje = new Network.GamePackets.Message("Alive Players : [ " + CkeckUPAlive() + " ]", System.Drawing.Color.Yellow, Network.GamePackets.Message.FirstRightCorner).ToArray();
                        var MapDictionar = Kernel.GamePool.Values.Where(p => p.Entity.MapID == 1507).ToArray();
                        foreach (var client in MapDictionar)
                            client.Send(Messaje);
                        if (CkeckUPAlive() == 1)
                        {
                            var client = Kernel.GamePool.Values.SingleOrDefault(p => p.Entity.MapID == 1507 && p.Entity.Hitpoints > 0);
                            if (client != null)
                            {
                                Game.Statue statue = new Statue(client.Entity.SpawnPacket);
                                client.Entity.AddConquerPoints(10000);
                                Game.Entity.SendWorldMessage("Congratulation ! " + client.Entity.Name + " win the Hero OF Game, he receice 10000 Conquest Points");
                                Close();
                            }
                            foreach (var player in Kernel.GamePool.Values)
                            {
                                if (player.Entity.MapID == 1507)
                                    player.Entity.Teleport(1002, 239, 245);
                                player.Entity.AllowToAttack = false;
                            }
                        }
                        else if (CkeckUPAlive() == 0)
                        {
                            Close();
                        }
                    }
                    Secounds++;
                }
            }
        }
    }
}