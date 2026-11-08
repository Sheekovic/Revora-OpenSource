using System;
using Revora.Client;
using Revora.Network.GamePackets;

namespace Revora.Game.Features
{
    public static class CpShopClerkSystem
    {
        public const uint NpcId = 900000015;
        public const string NpcName = "PurseClerk";
        public const ushort TwinCityMap = 1002;
        public const ushort X = 418;
        public const ushort Y = 330;
        public const ushort Mesh = 50870;

        public static void Create()
        {
            CityGuardSystem.SpawnTalker(NpcId, TwinCityMap, X, Y, Mesh, NpcName, (byte)Enums.ConquerAngle.South);
            if (Kernel.Maps.ContainsKey(TwinCityMap) && Kernel.Maps[TwinCityMap].Npcs.ContainsKey(NpcId))
                Console.WriteLine("PurseClerk at Twin City " + X + "," + Y);
            else
                Console.WriteLine("PurseClerk spawn skipped: map 1002 not ready");
        }

        public static bool HandleNpc(GameState client, NpcRequest request)
        {
            if (client == null || request == null || client.Entity == null || client.ActiveNpc != NpcId)
                return false;
            Npcs dialog = new Npcs(client);
            switch (request.OptionID)
            {
                case 0:
                    dialog.Text("I am PurseClerk. Conquer Points are sold on playrevora.com. Log in with this account, open Shop, and pick this character. I do not take coins here.");
                    if (CourierLoopSystem.AwaitingClerk(client))
                        CourierLoopSystem.OfferClerk(dialog);
                    MarketErrandSystem.Offer(client, dialog);
                    dialog.Option("How it works", 1);
                    dialog.Option("Packs", 2);
                    dialog.Option("Custom amount", 3);
                    dialog.Option("Goodbye", 255);
                    dialog.Send();
                    return true;
                case CourierLoopSystem.ClerkOption:
                    return CourierLoopSystem.HandleClerk(client, dialog);
                case MarketErrandSystem.StartOption:
                case MarketErrandSystem.TurnOption:
                    return MarketErrandSystem.Handle(client, dialog, request.OptionID);
                case 1:
                    dialog.Text("Pay with crypto through NOWPayments. Every $1 is 10,000 CP, then a 5% bonus. The smallest payment is $15. Points arrive here after the payment confirms. If you are online they appear within a minute. If you are offline they wait for your next login.");
                    dialog.Option("Packs", 2);
                    dialog.Option("Custom amount", 3);
                    dialog.Option("I see.", 255);
                    dialog.Send();
                    return true;
                case 2:
                    dialog.Text("Starter $15 is 157,500 CP. $30 is 315,000. $50 is 525,000. $100 is 1,050,000. $200 is 2,100,000. All of those already include the 5% bonus.");
                    dialog.Option("Custom amount", 3);
                    dialog.Option("How it works", 1);
                    dialog.Option("I see.", 255);
                    dialog.Send();
                    return true;
                case 3:
                    dialog.Text("On playrevora.com you may type any whole dollar from 15 to 1,000. Example: $374 is 3,927,000 CP. Weekly ceiling is $2,000 per account.");
                    dialog.Option("Packs", 2);
                    dialog.Option("How it works", 1);
                    dialog.Option("I see.", 255);
                    dialog.Send();
                    return true;
            }
            return true;
        }
    }
}
