// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using Revora.Client;
using Revora.Interfaces;

namespace Revora.Game
{
    public unsafe class NpcEffect
    {
        private static string
        Effect1 = "attach_continuance05",
        Effect2 = "curse",
        Effect3 = "antaseptmem",
        Effect4 = "audience_eyeable",
        Effect5 = "god_kiuj6",
        Effect6 = "god_pj1",
        Effect7 = "god_pj2",
        Effect8 = "LuckDiffuse";

        public static string GetEffect()
        {
            Random random = new Random();
            int r = random.Next(1, 7);
            string result = "";
            switch (r)
            {
                case 1:
                    {
                        result = NpcEffect.Effect1;
                        break;
                    }
                case 2:
                    {
                        result = NpcEffect.Effect2;
                        break;
                    }
                case 3:
                    {
                        result = NpcEffect.Effect3;
                        break;
                    }
                case 4:
                    {
                        result = NpcEffect.Effect4;
                        break;
                    }
                case 5:
                    {
                        result = NpcEffect.Effect5;
                        break;
                    }
                case 6:
                    {
                        result = NpcEffect.Effect6;
                        break;
                    }
                case 7:
                    {
                        result = NpcEffect.Effect7;
                        break;
                    }
            }
            return result;
        }
        public static void AddEffect(uint type)
        {
            if (Kernel.Maps[1002].Npcs.ContainsKey(type))
            {
                INpc npc = Kernel.Maps[1002].Npcs[type];
                npc.effect = NpcEffect.GetEffect();
                Kernel.Maps[1002].Npcs.Remove(npc.UID);
                Kernel.Maps[1002].AddNpc(npc);
                GameState[] GamePool = Server.GamePool;
                for (int i = 0; i < GamePool.Length; i++)
                {
                    GameState Client = GamePool[i];
                    if (Client.Entity.MapID == 1002)
                    {
                        npc.SendSpawn(Client);
                    }
                }
            }
        }
        public static void AddEffect2(uint type)
        {
            if (Kernel.Maps[1002].Npcs.ContainsKey(type))
            {
                INpc npc = Kernel.Maps[1002].Npcs[type];
                npc.effect = NpcEffect.Effect8;
                Kernel.Maps[1002].Npcs.Remove(npc.UID);
                Kernel.Maps[1002].AddNpc(npc);
                GameState[] GamePool = Server.GamePool;
                for (int i = 0; i < GamePool.Length; i++)
                {
                    GameState Client = GamePool[i];
                    if (Client.Entity.MapID == 1002)
                    {
                        npc.SendSpawn(Client);
                    }
                }
            }
        }
        public static void RemoveEffect(uint type)
        {
            if (Kernel.Maps[1002].Npcs.ContainsKey(type))
            {
                INpc npc = Kernel.Maps[1002].Npcs[type];
                npc.effect = "";
                Kernel.Maps[1002].Npcs.Remove(npc.UID);
                Kernel.Maps[1002].AddNpc(npc);
                GameState[] GamePool = Server.GamePool;
                for (int i = 0; i < GamePool.Length; i++)
                {
                    GameState Client = GamePool[i];
                    if (Client.Entity.MapID == 1002)
                    {
                        npc.SendSpawn(Client);
                    }
                }
            }
        }
    }
}