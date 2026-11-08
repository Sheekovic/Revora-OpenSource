using System;
using System.Linq;
using Revora.Database;
using Revora.Network.GamePackets;

namespace Revora.Game.Features
{
    internal static class P6SoulShop
    {
        internal const uint NpcUid = 91010;
        internal const uint Price = 2500;
        internal static readonly uint[] Items = {
            800000,800017,800110,800254,800320,800421,800513,800616,800722,
            800804,800809,800916,801003,801103,801212,801214,801306,
            820071,820072,821031,821032,822053,822055,823055,823056,823057,824017
        };
        internal static void Create()
        {
            Map map;
            if (!Kernel.Maps.TryGetValue(1002, out map))
                throw new InvalidOperationException("P6 soul shop requires Twin City.");
            foreach (uint id in Items)
            {
                ConquerItemBaseInformation item;
                if (!ConquerItemInformation.BaseInformations.TryGetValue(id, out item) || item.PurificationLevel != 6)
                    throw new InvalidOperationException("P6 soul shop catalog mismatch.");
            }
            Interfaces.INpc existing;
            if (map.Npcs.TryGetValue(NpcUid, out existing) &&
                (existing.Type != Enums.NpcType.Shop || existing.X != 401 || existing.Y != 339 || existing.Mesh != 46090))
                throw new InvalidOperationException("P6 soul shop NPC identity collision.");
            foreach (uint id in Items) ConquerItemInformation.BaseInformations[id].ConquerPointsWorth = Price;
            ShopFile.Shops[NpcUid] = new ShopFile.Shop {
                UID = NpcUid, MoneyType = ShopFile.MoneyType.ConquerPoints, Items = Items.ToList()
            };
            if (existing == null) map.AddNpc(new NpcSpawn {
                UID = NpcUid, MapID = 1002, X = 401, Y = 339, Mesh = 46090,
                Type = Enums.NpcType.Shop, Name = "P6SoulMerchant"
            });
        }
    }
}
