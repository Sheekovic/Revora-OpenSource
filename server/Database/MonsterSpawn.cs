using System;
using Revora.Game;
using System.Drawing;
using Revora.Client;
using Revora.Network.GamePackets;

namespace Revora.Database
{
    public unsafe class MonsterSpawn
    {
        public static void StartSnowBanshee(GameState client)
        {
            Kernel.SpawnBanshee = true;
            Kernel.SpawnBanshee2 = true;
            NpcSpawn npc = new NpcSpawn();
            npc.UID = 2999;
            npc.Mesh = 16537;
            npc.X = 496;
            npc.Y = 422;
            npc.MapID = 1927;
            npc.Type = Enums.NpcType.Talker;
            Kernel.Maps[1927].Npcs.Add(2999, npc);
            npc.SendSpawn(client);
            Kernel.SendWorldMessage(new Message("The Ice Queen has appeared in Scratch Grotto 2 (496, 422)! Kill her!", Color.White, Message.System));
        }
        //public static void StartNemesisTyrant(GameState client)
        //{
        //    Random Random = new Random();
        //    int random = Random.Next(1, 2);
        //    if (random == 1)
        //    {

        //        Database.MonsterInformation mob;
        //        Database.MonsterInformation.MonsterInformations.TryGetValue(2447, out mob);
        //        client.Map.Spawnthis(mob, client, 1022, 421, 428);
        //        Kernel.SpawnNemesis = true;
        //        Kernel.SendWorldMessage(new Message("El SlayerSoldier ha aparecido en la ciudad escondida 421, 428  derramando mucha sangre, Matalo y Gana Fabulosos Premios", Color.White, Message.System));
        //    }
        //    else if (random == 2)
        //    {


        //        Database.MonsterInformation mob;
        //        Database.MonsterInformation.MonsterInformations.TryGetValue(2447, out mob);
        //        client.Map.Spawnthis(mob, client, 1022, 421, 428);
        //        Kernel.SpawnNemesis = true;
        //        Kernel.SendWorldMessage(new Message("El SlayerSoldier ha aparecido en la ciudad escondida 421, 428  derramando mucha sangre, Matalo y Gana Fabulosos Premios", Color.White, Message.System));
        //    }
        //    else if (random == 3)
        //    {


        //        Database.MonsterInformation mob;
        //        Database.MonsterInformation.MonsterInformations.TryGetValue(2447, out mob);
        //        client.Map.Spawnthis(mob, client, 1022, 224, 172);
        //        Kernel.SpawnNemesis = true;
        //        Kernel.SendWorldMessage(new Message("El SlayerSoldier ha aparecido en la ciudad escondida  derramando mucha sangre, Matalo y Gana Fabulosos Premios", Color.White, Message.System));
        //    }
        //    else if (random == 4)
        //    {


        //        Database.MonsterInformation mob;
        //        Database.MonsterInformation.MonsterInformations.TryGetValue(2447, out mob);
        //        client.Map.Spawnthis(mob, client, 1022, 159, 134);
        //        Kernel.SpawnNemesis = true;
        //        Kernel.SendWorldMessage(new Message("El SlayerSoldier ha aparecido en la ciudad escondida  derramando mucha sangre, Matalo y Gana Fabulosos Premios", Color.White, Message.System));
        //    }
        //    else if (random == 5)
        //    {

        //        Database.MonsterInformation mob;
        //        Database.MonsterInformation.MonsterInformations.TryGetValue(2447, out mob);
        //        client.Map.Spawnthis(mob, client, 1022, 104, 133);
        //        Kernel.SpawnNemesis = true;
        //        Kernel.SendWorldMessage(new Message("El SlayerSoldier ha aparecido en la ciudad escondida  derramando mucha sangre, Matalo y Gana Fabulosos Premios", Color.White, Message.System));
        //    }
        //    else if (random == 6)
        //    {

        //        Database.MonsterInformation mob;
        //        Database.MonsterInformation.MonsterInformations.TryGetValue(2447, out mob);
        //        client.Map.Spawnthis(mob, client, 1022, 420, 427);
        //        Kernel.SpawnNemesis = true;
        //        Kernel.SendWorldMessage(new Message("El SlayerSoldier ha aparecido en la ciudad escondida  derramando mucha sangre, Matalo y Gana Fabulosos Premios", Color.White, Message.System));
        //    }
        //    else if (random == 7)
        //    {

        //        Database.MonsterInformation mob;
        //        Database.MonsterInformation.MonsterInformations.TryGetValue(2447, out mob);
        //        client.Map.Spawnthis(mob, client, 1022, 476, 547);
        //        Kernel.SpawnNemesis = true;
        //        Kernel.SendWorldMessage(new Message("El SlayerSoldier ha aparecido en la ciudad escondida  derramando mucha sangre, Matalo y Gana Fabulosos Premios", Color.White, Message.System));
        //    }
        //    else if (random == 8)
        //    {


        //        Database.MonsterInformation mob;
        //        Database.MonsterInformation.MonsterInformations.TryGetValue(2447, out mob);
        //        client.Map.Spawnthis(mob, client, 1022, 357, 367);
        //        Kernel.SpawnNemesis = true;
        //        Kernel.SendWorldMessage(new Message("El SlayerSoldier ha aparecido en la ciudad escondida  derramando mucha sangre, Matalo y Gana Fabulosos Premios", Color.White, Message.System));
        //    }
        //    else if (random == 9)
        //    {

        //        Database.MonsterInformation mob;
        //        Database.MonsterInformation.MonsterInformations.TryGetValue(2447, out mob);
        //        client.Map.Spawnthis(mob, client, 1022, 374, 437);
        //        Kernel.SpawnNemesis = true;
        //        Kernel.SendWorldMessage(new Message("El SlayerSoldier ha aparecido en la ciudad escondida  derramando mucha sangre, Matalo y Gana Fabulosos Premios", Color.White, Message.System));
        //    }
        //    else if (random == 10)
        //    {

        //        Database.MonsterInformation mob;
        //        Database.MonsterInformation.MonsterInformations.TryGetValue(2447, out mob);
        //        client.Map.Spawnthis(mob, client, 1022, 426, 485);
        //        Kernel.SpawnNemesis = true;
        //        Kernel.SendWorldMessage(new Message("El SlayerSoldier ha aparecido en la ciudad escondida  derramando mucha sangre, Matalo y Gana Fabulosos Premios", Color.White, Message.System));
        //    }
        //    else if (random == 11)
        //    {

        //        Database.MonsterInformation mob;
        //        Database.MonsterInformation.MonsterInformations.TryGetValue(2447, out mob);
        //        client.Map.Spawnthis(mob, client, 1022, 484, 478);
        //        Kernel.SpawnNemesis = true;
        //        Kernel.SendWorldMessage(new Message("El SlayerSoldier ha aparecido en la ciudad escondida  derramando mucha sangre, Matalo y Gana Fabulosos Premios", Color.White, Message.System));
        //    }
        //    else if (random == 12)
        //    {

        //        Database.MonsterInformation mob;
        //        Database.MonsterInformation.MonsterInformations.TryGetValue(2447, out mob);
        //        client.Map.Spawnthis(mob, client, 1022, 494, 444);
        //        Kernel.SpawnNemesis = true;
        //        Kernel.SendWorldMessage(new Message("El SlayerSoldier ha aparecido en la ciudad escondida  derramando mucha sangre, Matalo y Gana Fabulosos Premios", Color.White, Message.System));
        //    }

        //}
    }
}