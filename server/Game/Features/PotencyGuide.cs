using Revora.Client;
using Revora.Interfaces;
using Revora.Network.GamePackets;

namespace Revora.Game.Features
{
    internal static class PotencyGuide
    {
        internal const uint GuideId = 900000018, ReturnId = 900000019;
        internal const ushort HomeX = 428, HomeY = 346, ReturnX = 559, ReturnY = 547;

        internal static void Create()
        {
            Spawn(GuideId, 1002, HomeX, HomeY);
            Spawn(ReturnId, PotencyHunt.MapId, ReturnX, ReturnY);
        }

        private static void Spawn(uint id, ushort mapId, ushort x, ushort y)
        {
            Map map;
            INpc existing;
            if (!Kernel.Maps.TryGetValue(mapId, out map)) throw new System.InvalidOperationException("Potency Guide map is unavailable");
            if (map.Npcs.TryGetValue(id, out existing))
            {
                if (existing.X != x || existing.Y != y || existing.Name != "PotencyGuide")
                    throw new System.InvalidOperationException("Potency Guide NPC collision");
                return;
            }
            if (!map.Floor[x, y, MapObjectType.Player]) throw new System.InvalidOperationException("Potency Guide tile is blocked");
            CityGuardSystem.SpawnTalker(id, mapId, x, y, 50000, "PotencyGuide", (byte)Enums.ConquerAngle.South);
            System.Console.WriteLine("[potency-guide] map=" + mapId + " x=" + x + " y=" + y);
        }

        internal static bool HandleNpc(GameState client, NpcRequest request)
        {
            if (client == null || request == null || (client.ActiveNpc != GuideId && client.ActiveNpc != ReturnId)) return false;
            INpc npc;
            ushort expectedMap = client.ActiveNpc == GuideId ? (ushort)1002 : PotencyHunt.MapId;
            if (client.Fake || client.Action != 2 || client.Entity == null || client.Entity.Dead || client.Entity.PersistenceRequiresReload
                || client.Map == null || client.Map.ID != expectedMap || client.Booth != null
                || client.Trade == null || client.Trade.InTrade || !client.Map.Npcs.TryGetValue(client.ActiveNpc, out npc)
                || Kernel.GetDistance(client.Entity.X, client.Entity.Y, npc.X, npc.Y) > 6) return true;
            var dialog = new Npcs(client);
            if (request.OptionID == 1)
            {
                ushort destination = expectedMap == 1002 ? PotencyHunt.MapId : (ushort)1002;
                ushort x = destination == 1002 ? (ushort)428 : (ushort)561;
                ushort y = destination == 1002 ? (ushort)349 : (ushort)549;
                Map map;
                if (Kernel.Maps.TryGetValue(destination, out map) && FindLanding(map, ref x, ref y))
                {
                    client.Entity.Teleport(destination, x, y);
                    client.Send(destination == 1002 ? "Safe travels." : "May your hunt bear fruit.");
                }
                else client.Send("The path is crowded. Give it a moment.");
                return true;
            }
            if (request.OptionID == 0)
            {
                dialog.Text(expectedMap == 1002
                    ? "The serpents guard relics of inner power. Hunt them and open their Exploit Packs to gain Potency. Shall I take you there?"
                    : "Keep your blade steady. Ready to return to Twin City?");
                dialog.Option(expectedMap == 1002 ? "Take me there." : "Take me home.", 1);
                dialog.Option("Not yet.", 255);
                dialog.Send();
            }
            return true;
        }

        internal static bool FindLanding(Map map, ref ushort x, ref ushort y)
        {
            for (int radius = 0; radius <= 8; radius++)
                for (int dx = -radius; dx <= radius; dx++)
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        int tx = x + dx, ty = y + dy;
                        if (System.Math.Max(System.Math.Abs(dx), System.Math.Abs(dy)) != radius
                            || !map.Floor[tx, ty, MapObjectType.Player] || !map.Floor[tx, ty, MapObjectType.Monster]) continue;
                        x = (ushort)tx; y = (ushort)ty; return true;
                    }
            return false;
        }
    }
}
