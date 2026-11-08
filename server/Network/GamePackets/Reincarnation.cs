using System;

namespace Revora.Network.GamePackets
{
    public static class Reincarnation
    {
        public static void Process(byte[] packet, Client.GameState client)
        {
            if (packet == null || packet.Length < 21 || packet.Length > ushort.MaxValue + 8
                || BitConverter.ToUInt16(packet, 0) + 8 != packet.Length
                || BitConverter.ToUInt16(packet, 2) != 1066
                || BitConverter.ToUInt64(packet, packet.Length - 8) != PacketHandler.ClientSeal
                || !Game.Features.QuestInventory.Player(client)) return;
            lock (client.ItemSyncRoot)
            {
                if (!Game.Features.QuestInventory.Player(client) || client.Trade == null || client.Trade.InTrade || client.Booth != null) return;
                Game.Features.PaidReincarnation.Process(packet, client);
            }
        }
    }
}
