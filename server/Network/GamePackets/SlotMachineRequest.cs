// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.Linq;
using System.Text;
using Revora.Game;
using Revora.Interfaces;
using System.Collections.Generic;

namespace Revora.Network.GamePackets
{
    public unsafe class SlotMachineRequest : Writer, IPacket
    {
        byte[] Buffer;
        public SlotMachineRequest(bool Create = false)
        {
        }
        public Enums.SlotMachineSubType Mode
        {
            get { return (Enums.SlotMachineSubType)Buffer[4]; }
            set { Buffer[4] = (byte)value; }
        }
        public byte BetMultiplier
        {
            get { return Buffer[5]; }
            set { Buffer[5] = value; }
        }
        public uint NpcID
        {
            get { return BitConverter.ToUInt32(Buffer, 8); }
            set { WriteUInt32(value, 8, Buffer); }
        }
        public void Send(Client.GameState client)
        {
            client.Send(Buffer);
        }
        public byte[] ToArray()
        {
            return Buffer;
        }
        public void Deserialize(byte[] buffer)
        {
            Buffer = buffer;
        }
        public static void Process(byte[] packet, Client.GameState client)
        {
            SlotMachineRequest req = new SlotMachineRequest();
            req.Deserialize(packet);
            switch (req.Mode)
            {
                case Enums.SlotMachineSubType.StartSpin:
                    {
                        INpc npc = client.Screen.SelectWhere<INpc>(MapObjectType.Npc, (o) => o.UID == req.NpcID).ElementAtOrDefault(0);
                        if (npc != null)
                        {
                            if (npc.Mesh / 10 >= 1977 && npc.Mesh / 10 <= 1980)
                            {
                                int id = npc.Mesh / 10 - 1977;
                                uint cost = 10000;
                                bool cps = id != 0;
                                if (id == 1) cost = 3;
                                if (id == 2) cost = 10;
                                if (id == 3) cost = 100;
                                cost *= req.BetMultiplier;
                                if ((cps && client.Entity.ConquerPoints >= cost) || (!cps && client.Entity.Money >= cost))
                                {
                                    if (cps) client.Entity.TrySpendConquerPoints((uint)(cost));
                                    else client.Entity.AdjustMoney(-(decimal)(cost));
                                    client.SlotMachine = new Game.ConquerStructures.SlotMachine(req.NpcID, cost, cps);
                                    client.SlotMachine.SpinTheWheels();
                                    client.SlotMachine.SendWheelsToClient(client);
                                }
                            }
                        }
                        break;
                    }
                case Enums.SlotMachineSubType.ClientFinishSpin:
                    {
                        if (client.SlotMachine != null)
                        {
                            uint reward = client.SlotMachine.GetRewardAmount(client);
                            if (client.SlotMachine.Cps) client.Entity.AddConquerPoints(reward);
                            else client.Entity.AdjustMoney((decimal)(reward));
                            client.Send(new SlotMachineResponse() { NpcID = req.NpcID, Mode = Enums.SlotMachineSubType.StopSpin });
                            client.SlotMachine = null;
                        }
                        break;
                    }
            }
        }
    }
}