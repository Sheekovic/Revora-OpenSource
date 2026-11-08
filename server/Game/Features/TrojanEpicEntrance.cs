using System;
using System.Linq;
using Revora.Client;
using Revora.Interfaces;
using Revora.Network.GamePackets;

namespace Revora.Game.Features
{
    internal static class TrojanEpicEntrance
    {
        internal const uint Pak = 10579, Mok = 10580;
        private static Map town;
        private static NpcSpawn[] actors;
        // Installed 6711 journal locations and npc.ini profiles 2919 / 2920.
        internal static void Place()
        {
            Map map;
            if (!Kernel.Maps.TryGetValue(1002, out map) || map.BaseID != 1002)
                throw new InvalidOperationException("Trojan entrance requires Twin City.");
            if (actors != null) {
                if (!ReferenceEquals(map, town) || actors.Any(a => !Registered(a)))
                    throw new InvalidOperationException("Trojan entrance ownership changed.");
                return;
            }
            var proposed = new[] {
                new NpcSpawn { UID = Pak, MapID = 1002, X = 425, Y = 298, Mesh = 29196, Type = Enums.NpcType.Talker },
                new NpcSpawn { UID = Mok, MapID = 1002, X = 466, Y = 400, Mesh = 29206, Type = Enums.NpcType.Talker }
            };
            foreach (var npc in proposed) {
                if (map.Npcs.ContainsKey(npc.UID) || Map.SecoundNpc.ContainsKey(npc.UID)
                    || map.Npcs.Values.Any(n => Kernel.GetDistance(n.X, n.Y, npc.X, npc.Y) < 4))
                    throw new InvalidOperationException("Trojan entrance NPC identity or footprint is occupied.");
                for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++)
                    if (!map.Floor[npc.X + dx, npc.Y + dy, MapObjectType.Player])
                        throw new InvalidOperationException("Trojan entrance footprint is not clear.");
                bool approach = false;
                for (int dx = -3; dx <= 3; dx++) for (int dy = -3; dy <= 3; dy++)
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) >= 2 && map.Floor[npc.X + dx, npc.Y + dy, MapObjectType.Player]) approach = true;
                if (!approach) throw new InvalidOperationException("Trojan entrance has no talking approach.");
            }
            try {
                foreach (var npc in proposed) map.AddNpc(npc);
                town = map; actors = proposed;
            } catch {
                foreach (var npc in proposed) {
                    INpc current;
                    if (map.Npcs.TryGetValue(npc.UID, out current) && ReferenceEquals(current, npc)) map.RemoveNpc(npc);
                    if (Map.SecoundNpc.TryGetValue(npc.UID, out current) && ReferenceEquals(current, npc)) Map.SecoundNpc.Remove(npc.UID);
                }
                throw;
            }
        }
        private static bool Registered(NpcSpawn npc)
        {
            INpc current;
            return town != null && town.Npcs.TryGetValue(npc.UID, out current) && ReferenceEquals(current, npc)
                && npc.MapID == 1002 && (npc.UID == Pak ? npc.X == 425 && npc.Y == 298 && npc.Mesh == 29196
                    : npc.UID == Mok && npc.X == 466 && npc.Y == 400 && npc.Mesh == 29206);
        }
        internal static bool Near(GameState p, uint npc)
        {
            Map current;
            return actors != null && Kernel.Maps.TryGetValue(1002, out current) && ReferenceEquals(current, town)
                && ReferenceEquals(p.Map, town) && actors.Any(a => a.UID == npc && Registered(a))
                && QuestInventory.Near(p, npc, 1002);
        }
    }
}
