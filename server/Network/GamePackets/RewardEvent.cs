// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;

namespace Revora.Network.GamePackets
{
    public unsafe class RewardEvent
    {
        byte[] Packet;
        public RewardEvent(Client.GameState client)
        {
            Packet = new byte[1000];
            fixed (byte* ptr = Packet)
            {
                *(ushort*)(ptr + (Packet.Length - 8)) = 0;
                *((ushort*)(ptr + 2)) = PacketID.RewardPoint;
                *((byte*)(ptr + 4)) = 1;//Type
                *((byte*)(ptr + 6)) = 17;//Num

                *((byte*)(ptr + 8)) = 1;//New-server Credit Bonus
                *((uint*)(ptr + 12)) = client.Entity.Quest_Reward_Clamied;
                *((byte*)(ptr + 14)) = 1;//New-server Credit Bonus
                *((uint*)(ptr + 16)) = client.Entity.Quest_Reward_Point;

                *((byte*)(ptr + 20)) = 2;//Credit for Training Pills
                *((uint*)(ptr + 24)) = client.Entity.Credit_Reward_Clamied;
                *((uint*)(ptr + 28)) = client.Entity.Credit_Reward_Point;

                *((byte*)(ptr + 32)) = 3;//Weekly~Credit~Bonuses
                *((uint*)(ptr + 36)) = client.Entity.Online_Reward_Clamied;
                *((uint*)(ptr + 40)) = client.Entity.Online_Reward_Point;

                *((byte*)(ptr + 44)) = 4;//Weekly~Credit~Bonuses
                *((uint*)(ptr + 48)) = client.Entity.Kill_Reward_Clamied;
                *((uint*)(ptr + 52)) = client.Entity.Kill_Reward_Point;

                *((byte*)(ptr + 56)) = 5;//Weekly~Credit~Bonuses
                *((uint*)(ptr + 60)) = client.Entity.King_Reward_Clamied;
                *((uint*)(ptr + 64)) = client.Entity.King_Reward_Point;

                *((byte*)(ptr + 68)) = 6;//Sweet~Flowers~for~Beauties
                *((uint*)(ptr + 72)) = 0;
                *((uint*)(ptr + 76)) = 0;

                *((byte*)(ptr + 80)) = 7;//Credits of the Heart
                *((uint*)(ptr + 84)) = client.Entity.Arena_Reward_Clamied;
                *((uint*)(ptr + 88)) = client.Entity.Arena_Reward_Point;

                *((byte*)(ptr + 92)) = 9;//Magic~Chi~Bonus
                *((uint*)(ptr + 96)) = client.Entity.Arena_Reward_Clamied;
                *((uint*)(ptr + 100)) = client.Entity.Arena_Reward_Point;

                *((byte*)(ptr + 104)) = 10;//Great Wall Street
                *((uint*)(ptr + 108)) = client.Entity.Arena_Reward_Clamied;
                *((uint*)(ptr + 112)) = client.Entity.Arena_Reward_Point;

                *((byte*)(ptr + 116)) = 11;//Supreme Guild Summit
                *((uint*)(ptr + 120)) = client.Entity.Arena_Reward_Clamied;
                *((uint*)(ptr + 124)) = client.Entity.Arena_Reward_Point;

                *((byte*)(ptr + 128)) = 12;//Magic Chi Bonus
                *((uint*)(ptr + 132)) = client.Entity.Arena_Reward_Clamied;
                *((uint*)(ptr + 136)) = client.Entity.Arena_Reward_Point;

                *((byte*)(ptr + 140)) = 13;//New-server Credit Bonus
                *((uint*)(ptr + 144)) = client.Entity.Arena_Reward_Clamied;
                *((uint*)(ptr + 148)) = client.Entity.Arena_Reward_Point;

                *((byte*)(ptr + 152)) = 14;//Accumulative Training Reward
                *((uint*)(ptr + 156)) = client.Entity.Arena_Reward_Clamied;
                *((uint*)(ptr + 160)) = client.Entity.Arena_Reward_Point;

                *((byte*)(ptr + 164)) = 15;//Accumulative Consumption Reward
                *((uint*)(ptr + 168)) = client.Entity.Arena_Reward_Clamied;
                *((uint*)(ptr + 172)) = client.Entity.Arena_Reward_Point;

                *((byte*)(ptr + 176)) = 16;//Accumulative Training Reward
                *((uint*)(ptr + 180)) = client.Entity.Arena_Reward_Clamied;
                *((uint*)(ptr + 184)) = client.Entity.Arena_Reward_Point;

                *((byte*)(ptr + 188)) = 17;//Accumulative Training Reward
                *((uint*)(ptr + 192)) = client.Entity.Arena_Reward_Clamied;
                *((uint*)(ptr + 196)) = client.Entity.Arena_Reward_Point;

                *((byte*)(ptr + 200)) = 18;//New-server Credit Bonus
                *((uint*)(ptr + 204)) = client.Entity.Arena_Reward_Clamied;
                *((uint*)(ptr + 208)) = client.Entity.Arena_Reward_Point;
            }
        }
        public byte[] ToArray()
        {
            fixed (void* Buf = Packet)
                return Packet;
        }
        public static void SendReward(Client.GameState client)
        {
            byte[] Packet = new byte[26];
            fixed (byte* ptr = Packet)
            {
                *(ushort*)(ptr + (Packet.Length - 8)) = 0;
                *((ushort*)(ptr + 2)) = PacketID.RewardPoint;
                *((byte*)(ptr + 6)) = 1;
                *((byte*)(ptr + 8)) = 1;
            }
            client.Send(Packet);
        }
        public static void Process(byte[] packet, Client.GameState client)
        {
            switch (packet[4])
            {
                case 0:
                    {
                        RewardEvent Event = new RewardEvent(client);
                        client.Send(Event.ToArray());

                        Activeness Act = new Activeness(client);
                        client.Send(Act.ToArray());

                        ActivenessPoint Point = new ActivenessPoint();
                        if (!client.Entity.ActivenessDone)
                            Point.Action = 1;
                        else
                            Point.Action = 2;
                        Point.Point = client.Entity.ActivenessPoint;
                        client.Send(Point.ToArray());
                        break;
                    }
                case 2:
                    {
                        foreach (Database.BaisReward.Reward re in Database.BaisReward.RewardBase.Values)
                        {
                            if (re.Type == packet[14])
                            {
                                switch (re.Type)
                                {
                                    #region New-server Credit Bonus
                                    case 1:
                                        {
                                            if (client.Entity.Quest_Reward_Point >= re.NeedPoints && re.Type == 1)
                                            {
                                                if (!client.ClamiedReward.ContainsKey(re.ID))
                                                {
                                                    if (client.Inventory.Add(re.GiveReward, 0, 1))
                                                    {
                                                        Database.RewardClameID.InsertNewClam(client, re.ID, re.GiveReward);
                                                        client.Entity.Quest_Reward_Clamied++;
                                                        client.ClamiedReward.Add(re.ID, re.GiveReward);
                                                        Database.RewardDataBase.Save(client);
                                                        RewardEvent Event = new RewardEvent(client);
                                                        client.Send(Event.ToArray());
                                                    }
                                                }
                                            }
                                            break;
                                        }
                                    #endregion
                                    #region Credit for Training Pills
                                    case 2:
                                        {
                                            if (client.Entity.Credit_Reward_Point >= re.NeedPoints && re.Type == 2)
                                            {
                                                if (!client.ClamiedReward.ContainsKey(re.ID))
                                                {
                                                    if (client.Inventory.Add(re.GiveReward, 0, 1))
                                                    {
                                                        Database.RewardClameID.InsertNewClam(client, re.ID, re.GiveReward);
                                                        client.Entity.Credit_Reward_Clamied++;
                                                        client.ClamiedReward.Add(re.ID, re.GiveReward);
                                                        Database.RewardDataBase.Save(client);
                                                        RewardEvent Event = new RewardEvent(client);
                                                        client.Send(Event.ToArray());
                                                    }
                                                }
                                            }
                                            break;
                                        }
                                    #endregion
                                    #region Weekly~Credit~Bonuses
                                    case 3:
                                        {
                                            if (client.Entity.Online_Reward_Point >= re.NeedPoints && re.Type == 3)
                                            {
                                                if (!client.ClamiedReward.ContainsKey(re.ID))
                                                {
                                                    if (client.Inventory.Add(re.GiveReward, 0, 1))
                                                    {
                                                        Database.RewardClameID.InsertNewClam(client, re.ID, re.GiveReward);
                                                        client.Entity.Online_Reward_Clamied++;
                                                        client.ClamiedReward.Add(re.ID, re.GiveReward);
                                                        Database.RewardDataBase.Save(client);
                                                        RewardEvent Event = new RewardEvent(client);
                                                        client.Send(Event.ToArray());
                                                    }
                                                }
                                            }
                                            break;
                                        }
                                    #endregion
                                    #region Sweet~Flowers~for~Beauties
                                    case 4:
                                        {
                                            if (client.Entity.Kill_Reward_Point >= re.NeedPoints && re.Type == 4)
                                            {
                                                if (!client.ClamiedReward.ContainsKey(re.ID))
                                                {
                                                    if (client.Inventory.Add(re.GiveReward, 0, 1))
                                                    {
                                                        Database.RewardClameID.InsertNewClam(client, re.ID, re.GiveReward);
                                                        client.Entity.Kill_Reward_Clamied++;
                                                        client.ClamiedReward.Add(re.ID, re.GiveReward);
                                                        Database.RewardDataBase.Save(client);
                                                        RewardEvent Event = new RewardEvent(client);
                                                        client.Send(Event.ToArray());
                                                    }
                                                }
                                            }
                                            break;
                                        }
                                    #endregion
                                    #region Anniversary Chi Points Rebate
                                    case 5:
                                        {
                                            if (client.Entity.King_Reward_Point >= re.NeedPoints && re.Type == 5)
                                            {
                                                if (!client.ClamiedReward.ContainsKey(re.ID))
                                                {
                                                    if (client.Inventory.Add(re.GiveReward, 0, 1))
                                                    {
                                                        Database.RewardClameID.InsertNewClam(client, re.ID, re.GiveReward);
                                                        client.Entity.King_Reward_Clamied++;
                                                        client.ClamiedReward.Add(re.ID, re.GiveReward);
                                                        Database.RewardDataBase.Save(client);
                                                        RewardEvent Event = new RewardEvent(client);
                                                        client.Send(Event.ToArray());
                                                    }
                                                }
                                            }
                                            break;
                                        }
                                    #endregion
                                    #region Anniversary CP Rebate
                                    case 7:
                                        {
                                            if (client.Entity.Arena_Reward_Point >= re.NeedPoints && re.Type == 7)
                                            {
                                                if (!client.ClamiedReward.ContainsKey(re.ID))
                                                {
                                                    if (client.Inventory.Add(re.GiveReward, 0, 1))
                                                    {
                                                        Database.RewardClameID.InsertNewClam(client, re.ID, re.GiveReward);
                                                        client.Entity.Arena_Reward_Clamied++;
                                                        client.ClamiedReward.Add(re.ID, re.GiveReward);
                                                        Database.RewardDataBase.Save(client);
                                                        RewardEvent Event = new RewardEvent(client);
                                                        client.Send(Event.ToArray());
                                                    }
                                                }
                                            }
                                            break;
                                        }
                                    #endregion
                                }
                            }
                        }
                        break;
                    }
            }
        }
    }
}