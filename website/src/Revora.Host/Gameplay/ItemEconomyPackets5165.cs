using System.Buffers.Binary;
using Revora.Gameplay.Contracts;
using Revora.Gameplay.Models;

namespace Revora.Host.Gameplay;

public readonly record struct ItemAction5165(uint Subject, uint Command, uint Action, uint Timestamp, uint Argument);
public readonly record struct WarehouseAction5165(uint NpcUid, byte Action, byte WarehouseSelector, uint ItemUid);

public static class ItemEconomyPackets5165
{
    public const ushort ItemInfoPacketId = 1008;
    public const ushort ItemActionPacketId = 1009;
    public const ushort WarehousePacketId = 1102;
    public const ushort StatusPacketId = 10017;
    public const ushort ItemInfoLength = 48;
    public const ushort ItemActionLength = 28;
    public const ushort WarehouseActionLength = 48;

    public const uint ShopBuy = 1;
    public const uint ShopSell = 2;
    public const uint InventoryRemove = 3;
    public const uint Equip = 4;
    public const uint Unequip = 6;
    public const uint Ping = 27;
    public const uint WarehouseOpen = 9;
    public const uint WarehouseMoneyDeposit = 10;
    public const uint WarehouseMoneyWithdraw = 11;
    public const byte TwinCityWarehouseSelector = 10;

    public static ItemAction5165 ParseItemAction(ReadOnlySpan<byte> packet)
    {
        if (packet.Length != ItemActionLength ||
            BinaryPrimitives.ReadUInt16LittleEndian(packet) != ItemActionLength ||
            BinaryPrimitives.ReadUInt16LittleEndian(packet[2..]) != ItemActionPacketId)
            throw new InvalidDataException("The patch-5165 item action frame is invalid.");
        uint action = BinaryPrimitives.ReadUInt32LittleEndian(packet[12..]);
        if (BinaryPrimitives.ReadUInt32LittleEndian(packet[24..]) != 0)
            throw new InvalidDataException("The item action reserved field is invalid.");
        return new(BinaryPrimitives.ReadUInt32LittleEndian(packet[4..]),
            BinaryPrimitives.ReadUInt32LittleEndian(packet[8..]), action,
            BinaryPrimitives.ReadUInt32LittleEndian(packet[16..]),
            BinaryPrimitives.ReadUInt32LittleEndian(packet[20..]));
    }

    public static WarehouseAction5165 ParseWarehouseAction(ReadOnlySpan<byte> packet)
    {
        if (packet.Length != WarehouseActionLength ||
            BinaryPrimitives.ReadUInt16LittleEndian(packet) != WarehouseActionLength ||
            BinaryPrimitives.ReadUInt16LittleEndian(packet[2..]) != WarehousePacketId ||
            packet[9] != TwinCityWarehouseSelector || BinaryPrimitives.ReadUInt16LittleEndian(packet[10..]) != 0 ||
            packet[16..].IndexOfAnyExcept((byte)0) >= 0)
            throw new InvalidDataException("The patch-5165 warehouse action frame is invalid.");
        byte action = packet[8];
        uint itemUid = BinaryPrimitives.ReadUInt32LittleEndian(packet[12..]);
        if (action is 0 or 1 or 2 && (action == 0 ? itemUid != 0 : itemUid == 0))
            throw new InvalidDataException("The warehouse action payload is invalid.");
        return new(BinaryPrimitives.ReadUInt32LittleEndian(packet[4..]), action, packet[9], itemUid);
    }

    public static byte[] EncodeItemInfo(ItemInstance item, ItemDefinition definition, ushort action = 1)
    {
        if (action is not (1 or 3)) throw new ArgumentOutOfRangeException(nameof(action));
        byte[] packet = new byte[ItemInfoLength];
        BinaryPrimitives.WriteUInt16LittleEndian(packet, ItemInfoLength);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2), ItemInfoPacketId);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4), item.LegacyUid.Value);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(8), item.DefinitionId.Value);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(12), item.Durability);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(14), definition.MaximumDurability);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(16), action);
        // Patch 5165 does not use the server's inventory-grid index as ItemInfo.Position.
        // Position zero means inventory; positions 1..12 are equipment locations.
        ushort clientPosition = item.Location.Container == ItemContainer.Equipment
            ? item.Location.Slot
            : (ushort)0;
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(18), clientPosition);
        packet[24] = item.SocketOne;
        packet[25] = item.SocketTwo;
        packet[28] = item.Plus;
        packet[29] = item.Bless;
        packet[30] = item.Bound ? (byte)1 : (byte)0;
        packet[31] = item.Enchant;
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(40), item.Color);
        return packet;
    }

    public static byte[] EncodeItemAction(uint itemUid, uint position, uint action, uint timestamp = 0)
    {
        byte[] packet = new byte[ItemActionLength];
        BinaryPrimitives.WriteUInt16LittleEndian(packet, ItemActionLength);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2), ItemActionPacketId);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4), itemUid);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(8), position);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(12), action);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(16), timestamp);
        return packet;
    }

    public static byte[] EncodeWarehouseOpen(uint npcUid, long silver)
    {
        if (silver is < 0 or > uint.MaxValue) throw new ArgumentOutOfRangeException(nameof(silver));
        byte[] packet = new byte[20];
        BinaryPrimitives.WriteUInt16LittleEndian(packet, 20);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2), ItemActionPacketId);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4), npcUid);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(8), checked((uint)silver));
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(12), WarehouseOpen);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(16), unchecked((uint)Environment.TickCount));
        return packet;
    }

    public static byte[] EncodeWarehouseList(uint npcUid, IReadOnlyList<ItemInstance> items,
        IItemDefinitionCatalog definitions)
    {
        if (items.Count > 20) throw new ArgumentOutOfRangeException(nameof(items));
        byte[] packet = new byte[16 + 32 * items.Count];
        BinaryPrimitives.WriteUInt16LittleEndian(packet, checked((ushort)packet.Length));
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2), WarehousePacketId);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4), npcUid);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(12), checked((uint)items.Count));
        for (int index = 0; index < items.Count; index++)
        {
            ItemInstance item = items[index];
            _ = definitions.Find(item.DefinitionId) ?? throw new InvalidDataException("A warehouse item definition is missing.");
            Span<byte> row = packet.AsSpan(16 + 32 * index, 32);
            BinaryPrimitives.WriteUInt32LittleEndian(row, item.LegacyUid.Value);
            BinaryPrimitives.WriteUInt32LittleEndian(row[4..], item.DefinitionId.Value);
            row[9] = item.SocketOne;
            row[10] = item.SocketTwo;
            row[13] = item.Plus;
            row[14] = item.Bless;
            row[15] = item.Bound ? (byte)1 : (byte)0;
            row[16] = item.Enchant;
            row[23] = item.Color;
        }
        return packet;
    }

    public static byte[] EncodeWarehouseLiveAdd(uint npcUid, ItemInstance item,
        IItemDefinitionCatalog definitions)
    {
        // The exact 5165 receive handler treats action 0 as one or more 32-byte item rows.
        // The reviewed owner donor uses that same one-row list representation for a live deposit.
        // Action 1 is a different client-side update layout (item UID/type at 16/20) and must not
        // be populated with the client request layout (item UID at 12).
        return EncodeWarehouseList(npcUid, [item], definitions);
    }

    public static byte[] EncodeWarehouseLiveRemove(uint npcUid, ItemInstance item)
    {
        byte[] packet = new byte[48];
        BinaryPrimitives.WriteUInt16LittleEndian(packet, 48);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2), WarehousePacketId);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4), npcUid);
        packet[8] = 2;
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(12), item.LegacyUid.Value);
        return packet;
    }

    public static byte[] EncodeStatus(uint characterUid, uint status, ulong value)
    {
        if (characterUid == 0 || status > byte.MaxValue) throw new ArgumentOutOfRangeException(nameof(characterUid));
        byte[] packet = new byte[36];
        BinaryPrimitives.WriteUInt16LittleEndian(packet, checked((ushort)packet.Length));
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2), StatusPacketId);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4), characterUid);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(12), status);
        BinaryPrimitives.WriteUInt64LittleEndian(packet.AsSpan(16), value);
        return packet;
    }
}
