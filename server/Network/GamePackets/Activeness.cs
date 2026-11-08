// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora
// * Open Source

namespace Revora.Network.GamePackets
{
    public unsafe class Activeness
    {
        byte[] Packet;
        public Activeness(Client.GameState client)
        {
            Packet = new byte[120];
            fixed (byte* Pack = Packet)
            {
                *(ushort*)(Pack + (Packet.Length - 8)) = 0;
                *((ushort*)(Pack + 2)) = PacketID.Activeness;
                *((byte*)(Pack + 4)) = 3;//Unknown
                *((byte*)(Pack + 5)) = 15;//Num

                *((byte*)(Pack + 6)) = 25;//Login the Game
                *((byte*)(Pack + 10)) = 1;//Completed
                *((byte*)(Pack + 11)) = 1;//MaxTime 1

                *((byte*)(Pack + 12)) = 26;//VIP Activeness
                if (client.Entity.VIPLevel >= 1)
                {
                    *((byte*)(Pack + 16)) = 1;//Completed
                    *((byte*)(Pack + 17)) = 1;//MaxTime 1
                }
                else if (client.Entity.VIPLevel == 0)
                {
                    *((byte*)(Pack + 17)) = 0;//MaxTime 1
                }
              
                *((byte*)(Pack + 18)) = 27;//0.5-Hour Online
                if (client.Entity.HourTime >= 5)
                {
                    *((byte*)(Pack + 22)) = 1;//Completed
                }
                *((byte*)(Pack + 23)) = client.Entity.HourTime;//MaxTime 5

                *((byte*)(Pack + 24)) = 28;//Lottery
                if (client.Entity.LotteryTime >= 10)
                {
                    *((byte*)(Pack + 28)) = 1;//Completed
                }
                *((byte*)(Pack + 29)) = client.Entity.LotteryTime;//MaxTime 10

                *((byte*)(Pack + 30)) = 29;//Daily Quest
                if (client.Entity.DailyTime >= 8)
                {
                    *((byte*)(Pack + 34)) = 1;//Completed
                }
                *((byte*)(Pack + 35)) = client.Entity.DailyTime;//MaxTime 8

                *((byte*)(Pack + 36)) = 30;//Flowers/Gifts
                *((byte*)(Pack + 40)) = client.Entity.FlowersGiftsTime;//Completed
                *((byte*)(Pack + 41)) = client.Entity.FlowersGiftsTime;//MaxTime 1

                *((byte*)(Pack + 42)) = 31;//Qualifier
                if (client.Entity.QualifierTime >= 5)
                {
                    *((byte*)(Pack + 46)) = 1;//Completed
                }
                *((byte*)(Pack + 47)) = client.Entity.QualifierTime;//MaxTime 5

                *((byte*)(Pack + 48)) = 32;//Champion`s Arena
                *((byte*)(Pack + 52)) = client.Entity.ChampionTime;//Completed
                *((byte*)(Pack + 53)) = client.Entity.ChampionTime;//MaxTime 1

                *((byte*)(Pack + 54)) = 33;//HorseRacing
                *((byte*)(Pack + 58)) = client.Entity.HorseRacingTime;//Completed
                *((byte*)(Pack + 59)) = client.Entity.HorseRacingTime;//MaxTime 1

                *((byte*)(Pack + 60)) = 34;//Treasure in the Blue
                *((byte*)(Pack + 64)) = client.Entity.TreasureTime;//Completed
                *((byte*)(Pack + 65)) = client.Entity.TreasureTime;//MaxTime 1

                *((byte*)(Pack + 66)) = 35;//Enlightenment
                if (client.Entity.EnlightenmentTime >= 3)
                {
                    *((byte*)(Pack + 70)) = 1;//Completed
                }
                *((byte*)(Pack + 71)) = client.Entity.EnlightenmentTime;//MaxTime 3

                *((byte*)(Pack + 72)) = 36;//TeamQualifier
                if (client.Entity.TeamQualifierTime >= 2)
                {
                    *((byte*)(Pack + 76)) = 1;//Completed
                }
                *((byte*)(Pack + 77)) = client.Entity.TeamQualifierTime;//MaxTime 2

                *((byte*)(Pack + 78)) = 37;//ChiStudy
                if (client.Entity.ChiStudyTime >= 12)
                {
                    *((byte*)(Pack + 82)) = 1;//Completed
                }
                *((byte*)(Pack + 83)) = client.Entity.ChiStudyTime;//MaxTime 12

                *((byte*)(Pack + 84)) = 38;//JiangHu
                if (client.Entity.JiangHuTime >= 12)
                {
                    *((byte*)(Pack + 88)) = 1;//Completed
                }
                *((byte*)(Pack + 89)) = client.Entity.JiangHuTime;//MaxTime 12
            }
        }
        public byte[] ToArray()
        {
            fixed (void* Pack = Packet)
                return Packet;
        }
    }
    public unsafe class ActivenessGift
    {
        byte[] Packet;
        public ActivenessGift()
        {
            Packet = new byte[30];
            fixed (byte* Pack = Packet)
            {
                *(ushort*)(Pack + (Packet.Length - 8)) = 0;
                *((ushort*)(Pack + 2)) = PacketID.ActivenessGift;
            }
        }
        public byte Action
        {
            get { return (byte)Packet[4]; }
            set { fixed (byte* Pack = Packet) *((byte*)(Pack + 4)) = value; }
        }
        public byte[] ToArray()
        {
            fixed (void* Pack = Packet)
                return Packet;
        }
        public static void Process(byte[] Packet, Client.GameState client)
        {
            ActivenessGift Gift = new ActivenessGift();
            client.Send(Gift.ToArray());
            switch (Gift.Action)
            {
                case 0:
                    {
                        if (client.Entity.ActivenessPoint >= 1001000000)
                        {
                            if (client.Entity.ActivenessPoint >= 3901000000)
                            {
                                client.Inventory.Add(3004247, 0, 1);
                                client.Inventory.Add(3004248, 0, 1);
                            }
                            else if (client.Entity.ActivenessPoint >= 3401000000)
                            {
                                client.Inventory.Add(3004247, 0, 1);
                            }
                            else if (client.Entity.ActivenessPoint >= 2101000000)
                            {
                                client.Inventory.Add(3004248, 0, 1);
                            }
                            else if (client.Entity.ActivenessPoint >= 1000000)
                            {
                                client.Inventory.Add(3004249, 0, 1);
                            }
                            ActivenessPoint Point = new ActivenessPoint();
                            Point.Action = 2;
                            Point.Point = client.Entity.ActivenessPoint;
                            client.Send(Point.ToArray());
                            client.Entity.ActivenessDone = true;
                        }
                        break;
                    }
                default: System.Console.WriteLine("[ActivenessGift] Unknown Action: " + Gift.Action + " "); break;
            }
        }
    }
    public unsafe class ActivenessPoint
    {
        byte[] Packet;
        public ActivenessPoint()
        {
            Packet = new byte[58];
            fixed (byte* Pack = Packet)
            {
                *(ushort*)(Pack + (Packet.Length - 8)) = 0;
                *((ushort*)(Pack + 2)) = PacketID.ActivenessPoint;
                *((byte*)(Pack + 4)) = 1;
                *((byte*)(Pack + 5)) = 2;
            }
        }
        public byte Action
        {
            set { fixed (byte* Pack = Packet) *((byte*)(Pack + 9)) = value; }
        }
        public uint Point
        {
            get { return BitConverter.ToUInt32(Packet, 13); }
            set { fixed (byte* Pack = Packet) *((uint*)(Pack + 13)) = value; }
        }
        public byte[] ToArray()
        {
            fixed (void* Pack = Packet)
                return Packet;
        }
    }
}