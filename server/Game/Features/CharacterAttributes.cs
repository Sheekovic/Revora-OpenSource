using System;
using Revora.Client;
using Revora.Network.GamePackets;

namespace Revora.Game.Features
{
    internal static class CharacterAttributes
    {
        internal static void Allocate(GameState player, byte[] packet)
        {
            // Existing 1024 fields are UInt32 deltas at 12/16/20/24. The transport
            // suffix is outside the declared payload, so it cannot supply a field.
            if (packet == null || packet.Length < 36 || packet.Length > ushort.MaxValue + 8
                || BitConverter.ToUInt16(packet, 0) + 8 != packet.Length
                || BitConverter.ToUInt16(packet, 2) != 1024
                || BitConverter.ToUInt64(packet, packet.Length - 8) != Network.PacketHandler.ClientSeal
                || !QuestInventory.Player(player)) return;
            lock (player.ItemSyncRoot)
            {
                if (!QuestInventory.Player(player)) return;
                uint strength = BitConverter.ToUInt32(packet, 12), agility = BitConverter.ToUInt32(packet, 16);
                uint vitality = BitConverter.ToUInt32(packet, 20), spirit = BitConverter.ToUInt32(packet, 24);
                ulong total = (ulong)strength + agility + vitality + spirit;
                var entity = player.Entity;
                if (total == 0 || total > entity.Atributes
                    || (ulong)entity.Strength + strength > ushort.MaxValue
                    || (ulong)entity.Agility + agility > ushort.MaxValue
                    || (ulong)entity.Vitality + vitality > ushort.MaxValue
                    || (ulong)entity.Spirit + spirit > ushort.MaxValue) return;
                entity.Strength += (ushort)strength;
                entity.Agility += (ushort)agility;
                entity.Vitality += (ushort)vitality;
                entity.Spirit += (ushort)spirit;
                entity.Atributes -= (ushort)total;
                player.CalculateStatBonus();
                player.CalculateHPBonus();
                player.GemAlgorithm();
                player.Send(packet);
            }
        }
    }
}
