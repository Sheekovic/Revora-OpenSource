using System;
using System.Collections.Generic;
using Revora.Client;
using Revora.Network.GamePackets;

namespace Revora.Game.Features
{
    internal static class GoldPickup
    {
        internal const int AutoHuntRange = 3;

        internal static bool TryCollect(GameState client, FloorItem item, bool automatic)
        {
            if (client == null || client.Fake || client.Entity == null || client.ItemSyncRoot == null
                || item == null) return false;
            // Serialize the collector's balance and the shared drop across manual/automatic requests.
            lock (client.ItemSyncRoot)
            lock (item)
            {
                Entity player = client.Entity;
                Map map = client.Map;
                Time32 now = Time32.Now;
                if (client.Action != 2 || player.Dead || client.Trade == null || client.Trade.InTrade
                    || map == null || map.Floor == null || map.FloorItems == null
                    || player.MapID != item.MapID || map.ID != item.MapID
                    || item.ValueType != FloorItem.FloorValueType.Money || item.Type != FloorItem.Drop
                    || item.PickedUpAlready || item.Value == 0
                    || now > item.OnFloor.AddSeconds(Constants.FloorItemSeconds)) return false;
                if (automatic && (!player.InAutoHunt || player.VIPLevel < 1)) return false;
                int distance = Kernel.GetDistance(player.X, player.Y, item.X, item.Y);
                if (distance > (automatic ? AutoHuntRange : 0)) return false;
                if (automatic && !ClearPath(map, player.X, player.Y, item.X, item.Y)) return false;

                uint owner = item.Owner == null || item.Owner.Entity == null ? 0 : item.Owner.Entity.UID;
                bool available = owner == 0 || owner == player.UID
                    || now >= item.OnFloor.AddSeconds(Constants.FloorItemAvailableAfter)
                    || (client.Team != null && client.Team.PickupMoney && client.Team.IsTeammate(owner));
                if (!available)
                {
                    if (!automatic) client.Send(Constants.FloorItemNotAvailable);
                    return false;
                }
                if (player.Money > Revora.Game.Entity.MaximumSilver - item.Value) return false;
                // A stale screen entry is not proof that a drop still exists. Remove this exact
                // object atomically before crediting, including when expiry races a pickup.
                var drops = (ICollection<KeyValuePair<uint, FloorItem>>)map.FloorItems;
                if (!drops.Remove(new KeyValuePair<uint, FloorItem>(item.UID, item))) return false;
                item.PickedUpAlready = true;
                item.Type = FloorItem.Remove;
                map.Floor[item.X, item.Y, MapObjectType.Item, item] = true;
                player.AdjustMoney((decimal)(item.Value));

                var animation = new FloorItem(true) { Type = FloorItem.Animation, UID = player.UID,
                    X = player.X, Y = player.Y };
                client.Send(Constants.PickupGold(item.Value));
                client.SendScreen(animation, false);
                client.RemoveScreenSpawn(item, true);
                if (item.Value > 3000)
                    client.Send(new Data(true) { UID = player.UID, dwParam = item.Value, ID = Data.GetMoney });
                return true;
            }
        }

        private static bool ClearPath(Map map, ushort x, ushort y, ushort targetX, ushort targetY)
        {
            int steps = Kernel.GetDistance(x, y, targetX, targetY);
            int previousX = x, previousY = y;
            for (int i = 1; i <= steps; i++)
            {
                int nextX = x + (targetX - x) * i / steps;
                int nextY = y + (targetY - y) * i / steps;
                if (!map.Floor[nextX, nextY, MapObjectType.Player, null]
                    || !map.Floor[previousX, nextY, MapObjectType.Player, null]
                    || !map.Floor[nextX, previousY, MapObjectType.Player, null]) return false;
                previousX = nextX; previousY = nextY;
            }
            return true;
        }
    }
}
