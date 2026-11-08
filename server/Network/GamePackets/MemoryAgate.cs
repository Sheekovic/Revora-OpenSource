// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

namespace Revora.Network.GamePackets
{
    public unsafe class MemoryAgate
    {
        public static void Process(byte[] packet, Client.GameState client)
        {
            uint ItemUID = BitConverter.ToUInt32(packet, 8);
            switch (packet[4])
            {
                case 1:
                    {
                        if (client.Map.IsDynamic()) return;
                        ConquerItem Item = null;
                        if (client.Inventory.TryGetItem(ItemUID, out Item))
                        {
                            if (Item.Agate_map.ContainsKey(packet[12]))
                            {
                                Item.Agate_map[(uint)packet[12]] = client.Entity.MapID + "~" + client.Entity.X + "~" + client.Entity.Y;
                                Database.ConquerItemTable.UpdateItemAgate(Item);
                                Item.SendAgate(client);
                                break;
                            }
                            if (packet[12] > Item.Agate_map.Count)
                            {
                                Item.Agate_map.Add((byte)(Item.Agate_map.Count), client.Entity.MapID + "~" + client.Entity.X + "~" + client.Entity.Y);
                                Database.ConquerItemTable.UpdateItemAgate(Item);
                                Item.SendAgate(client);
                                break;
                            }
                            else
                            {
                                if (!Item.Agate_map.ContainsKey(packet[12]))
                                {
                                    Item.Agate_map.Add(packet[12], client.Entity.MapID + "~" + client.Entity.X + "~" + client.Entity.Y);
                                    Database.ConquerItemTable.UpdateItemAgate(Item);
                                    Item.SendAgate(client);
                                }
                                break;
                            }
                        }
                        break;
                    }
                case 3:
                    {
                        if (client.Map.IsDynamic()) return;
                        ConquerItem Item = null;
                        if (client.Inventory.TryGetItem(ItemUID, out Item))
                        {
                            if (Item.Agate_map.ContainsKey(packet[12]))
                            {
                                if (client.Entity.MapID != 3825 && client.Entity.MapID != 1508 && client.Entity.MapID != 1518 && client.Entity.MapID != 1992 && client.Entity.MapID != 7001 && client.Entity.MapID != 1098 && client.Entity.MapID != 6000 && client.Entity.MapID != 6001 && client.Entity.MapID != 6002 && client.Entity.MapID != 6003 && client.Entity.MapID != 6004)
                                {
                                    if (ushort.Parse(Item.Agate_map[packet[12]].Split('~')[0].ToString()) == 1038)
                                        return;
                                    if (ushort.Parse(Item.Agate_map[packet[12]].Split('~')[0].ToString()) == 6001)
                                        return;
                                    if (ushort.Parse(Item.Agate_map[packet[12]].Split('~')[0].ToString()) == 6002)
                                        return;
                                    if (ushort.Parse(Item.Agate_map[packet[12]].Split('~')[0].ToString()) == 1098)
                                        return;
                                    if (ushort.Parse(Item.Agate_map[packet[12]].Split('~')[0].ToString()) == 1508)
                                        return;
                                    if (ushort.Parse(Item.Agate_map[packet[12]].Split('~')[0].ToString()) == 1518)
                                        return;
                                    if (ushort.Parse(Item.Agate_map[packet[12]].Split('~')[0].ToString()) == 3825)
                                        return;
                                    if (ushort.Parse(Item.Agate_map[packet[12]].Split('~')[0].ToString()) == 6003)
                                        return;
                                    if (ushort.Parse(Item.Agate_map[packet[12]].Split('~')[0].ToString()) == 6004)
                                        return;
                                    client.Entity.Teleport(ushort.Parse(Item.Agate_map[packet[12]].Split('~')[0].ToString())
                                        , ushort.Parse(Item.Agate_map[packet[12]].Split('~')[1].ToString())
                                        , ushort.Parse(Item.Agate_map[packet[12]].Split('~')[2].ToString()));
                                    Item.Durability--;
                                    Item.SendAgate(client);
                                    Database.ConquerItemTable.UpdateItemAgate(Item);
                                }
                                else
                                {
                                    client.Send(Constants.Teleportout);
                                }
                            }
                        }
                        break;
                    }
                case 4:
                    {
                        ConquerItem Item = null;
                        if (client.Inventory.TryGetItem(ItemUID, out Item))
                        {
                            int cost = (int)(Item.MaximDurability - Item.Durability) / 2;
                            if (cost == 0) cost = 1;
                            if (client.Entity.ConquerPoints > cost)
                            {
                                client.Entity.TrySpendConquerPoints((uint)((uint)cost));
                                Item.Durability = Item.MaximDurability;
                                Item.SendAgate(client);
                                Database.ConquerItemTable.UpdateItemAgate(Item);
                            }
                        }
                        break;
                    }
            }
        }
    }
}