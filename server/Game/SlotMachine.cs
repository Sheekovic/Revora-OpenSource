// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.Linq;
using System.Text;
using Revora.Client;
using System.Collections.Generic;
using Revora.Network.GamePackets;

namespace Revora.Game.ConquerStructures
{
    public unsafe class SlotMachine
    {
        public static readonly int[] Rates = new int[8] 
        {
            80, //Stancher
            920, //Meteor
            120, //Sword
            180, //TwoSwords
            210, //SwordAndShield
            180, //ExpBall
            1200, //DragonBall
            200, //3s line
        };

        public Enums.SlotMachineItems[] Wheels = new Enums.SlotMachineItems[3];
        public uint NPCID;
        public uint BetAmount;
        public bool Cps;
        public SlotMachine(uint npcid, uint betamount, bool cps = false)
        {
            NPCID = npcid;
            BetAmount = betamount;
            Cps = cps;
        }
        int GetAmount(Enums.SlotMachineItems Item)
        {
            int count = 0;
            foreach (Enums.SlotMachineItems item in Wheels)
                if (item == Item)
                    count++;
            return count;
        }
        private int GetSLCount()
        {
            return GetAmount(Enums.SlotMachineItems.Sword) + GetAmount(Enums.SlotMachineItems.SwordAndShield)
                + GetAmount(Enums.SlotMachineItems.TwoSwords) + GetAmount(Enums.SlotMachineItems.DragonBall);
        }
        private bool IsSL(Enums.SlotMachineItems item)
        {
            return item == Enums.SlotMachineItems.DragonBall || item == Enums.SlotMachineItems.Sword
                || item == Enums.SlotMachineItems.SwordAndShield || item == Enums.SlotMachineItems.TwoSwords;
        }
        public uint GetRewardAmount(GameState client)
        {
            uint win = 0;
            if (GetAmount(Enums.SlotMachineItems.DragonBall) == 3)
            {
                Kernel.SendWorldMessage(new Message("Congratulations to " + client.Entity.Name + "! He/She has won the jackpot from the 1-Arm Bandit!", Message.Center));
                client.SendScreen(new _String(true)
                {
                    UID = client.Entity.UID,
                    Type = _String.Effect,
                    Texts = new List<string>()
                    {
                        "accession5" 
                    }
                });
                if (Cps) return BetAmount * 3000;
                else return BetAmount * 1000;
            }
            if (GetAmount(Enums.SlotMachineItems.ExpBall) == 3 - GetAmount(Enums.SlotMachineItems.DragonBall))
                win = BetAmount * 60;
            else if (GetAmount(Enums.SlotMachineItems.SwordAndShield) == 3 - GetAmount(Enums.SlotMachineItems.DragonBall))
                win = BetAmount * 40;
            else if (GetAmount(Enums.SlotMachineItems.TwoSwords) == 3 - GetAmount(Enums.SlotMachineItems.DragonBall))
                win = BetAmount * 20;
            else if (GetAmount(Enums.SlotMachineItems.Sword) == 3 - GetAmount(Enums.SlotMachineItems.DragonBall)
                || GetAmount(Enums.SlotMachineItems.Meteor) == 3 - GetAmount(Enums.SlotMachineItems.DragonBall))
                win = BetAmount * 10;
            else if (GetAmount(Enums.SlotMachineItems.Meteor) == 2 - GetAmount(Enums.SlotMachineItems.DragonBall)
                || GetSLCount() == 3)
                win = BetAmount * 5;
            else if (GetAmount(Enums.SlotMachineItems.Meteor) == 1 - GetAmount(Enums.SlotMachineItems.DragonBall))
                win = BetAmount * 2;
            if (Cps)
            {
                if (GetAmount(Enums.SlotMachineItems.DragonBall) == 1) win *= 3;
                else if (GetAmount(Enums.SlotMachineItems.DragonBall) == 2) win *= 9;
            }
            return win;
        }
        public void SpinTheWheels()
        {
            int wheelPick;
            for (int i = 2; i >= 0; i--)
            {
                while (true)
                {
                    wheelPick = Kernel.Random.Next(0, 7);
                    if (Kernel.Rate(1, Rates[wheelPick]))
                    {
                        if (i == 0)
                            if (GetSLCount() == 2 && IsSL((Enums.SlotMachineItems)wheelPick) ||
                                (GetAmount((Enums.SlotMachineItems)wheelPick) == 3 - GetAmount(Enums.SlotMachineItems.DragonBall)))
                                if (!Kernel.Rate(1, Rates[7]))
                                    continue;
                        Wheels[i] = (Enums.SlotMachineItems)wheelPick;
                        break;
                    }
                }
            }
        }
        public void SendWheelsToClient(GameState client)
        {
            client.Send(new SlotMachineResponse()
            {
                Mode = Enums.SlotMachineSubType.StartSpin,
                WheelOne = (byte)Wheels[0],
                WheelTwo = (byte)Wheels[1],
                WheelThree = (byte)Wheels[2],
                NpcID = NPCID
            });
        }
    }
}