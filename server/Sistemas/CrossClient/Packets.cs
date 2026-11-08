//using System.Text;
//using Revora.Network;
//using Revora.Network.GamePackets;
//using Revora.Client;
////namespace Revora
////{
////    public class Packets2
////    {

////        public static void HandelPacket(GameState client, byte[] packet)
////        {
////            ushort ID = BitConverter.ToUInt16(packet, 2);
////            if (client != null && packet != null)
////            {
////                switch (ID)
////                {
////                    case 9002:
////                        {
////                            ushort offset = 4;
////                            Network.GamePackets.ConquerItem item = new Network.GamePackets.ConquerItem(true);
////                            item.Mode = Game.Enums.ItemMode.Default;
////                            item.UID = Network.GamePackets.ConquerItem.ItemUID.Next;
////                            item.CrossID = BitConverter.ToUInt32(packet, offset); offset += 4;
////                            item.ID = BitConverter.ToUInt32(packet, offset); offset += 4;
////                            item.Durability = BitConverter.ToUInt16(packet, offset); offset += 4;
////                            item.MaximDurability = BitConverter.ToUInt16(packet, offset); offset += 4;
////                            item.Position = BitConverter.ToUInt16(packet, offset); offset += 4;
////                            item.SocketProgress = BitConverter.ToUInt32(packet, offset); offset += 4;
////                            item.SocketOne = (Game.Enums.Gem)BitConverter.ToUInt32(packet, offset); offset += 4;
////                            item.SocketTwo = (Game.Enums.Gem)BitConverter.ToUInt32(packet, offset); offset += 4;
////                            item.Effect = (Game.Enums.ItemEffect)BitConverter.ToUInt32(packet, offset); offset += 4;
////                            item.Plus = (byte)BitConverter.ToUInt32(packet, offset); offset += 4;
////                            item.Bless = (byte)BitConverter.ToUInt32(packet, offset); offset += 4;
////                            item.Bound = BitConverter.ToBoolean(packet, offset); offset += 4;
////                            item.Enchant = (byte)BitConverter.ToUInt32(packet, offset); offset += 4;
////                            item.NextRed = (byte)BitConverter.ToUInt32(packet, offset); offset += 4;
////                            item.NextBlue = (byte)BitConverter.ToUInt32(packet, offset); offset += 4;
////                            item.NextGreen = (byte)BitConverter.ToUInt32(packet, offset); offset += 4;
////                            item.Lock = (byte)BitConverter.ToUInt32(packet, offset); offset += 4;
////                            item.Color = (Game.Enums.Color)BitConverter.ToUInt32(packet, offset); offset += 4;
////                            item.PlusProgress = BitConverter.ToUInt32(packet, offset); offset += 4;
////                            item.StackSize = (ushort)BitConverter.ToUInt32(packet, offset); offset += 4;
////                            item.Stars = BitConverter.ToUInt32(packet, offset); offset += 4;
////                            item.PerfectionProgress = BitConverter.ToUInt32(packet, offset); offset += 4;
////                            item.PerfectionOwnerGuid = BitConverter.ToUInt32(packet, offset); offset += 4;
////                            item.PerfectionOwner = ASCIIEncoding.Default.GetString(packet, offset, 16).TrimEnd('\0'); offset += 16;
////                            item.Signature = ASCIIEncoding.Default.GetString(packet, offset, 16).TrimEnd('\0'); offset += 16;
////                            client.Inventory.Add(item, Game.Enums.ItemUse.CreateAndAdd);
////                            client.Cross.Send(new CrossSync()
////                            {
////                                Type = CrossSync.SyncTypes.UpdateItemUID,
////                                Data = item.CrossID,
////                                Data2 = item.UID,
////                            }.ToArray());
////                            break;
////                        }
////                    case 9000:
////                        {
////                            CrossSync res = new CrossSync();
////                            res.Deserialization(packet);

////                            switch (res.Type)
////                            {
////                                case CrossSync.SyncTypes.StrickPoint:
////                                    {
////                                        client.Player.StrikePoints = res.Data;
////                                        break;
////                                    }
////                                case CrossSync.SyncTypes.DeleteItem:
////                                    {
////                                        ConquerItem item = null;
////                                        if (client.Inventory.TryGetItem(res.Data, out item))
////                                        {
////                                            client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
////                                        }
////                                        break;
////                                    }
////                                case CrossSync.SyncTypes.UpdateItemStack:
////                                    {
////                                        ConquerItem item = null;
////                                        if (client.Inventory.TryGetItem(res.Data, out item))
////                                        {
////                                            item.StackSize = (ushort)res.Data2;
////                                            Database.ConquerItemTable.UpdateStack(item);
////                                        }
////                                        break;
////                                    }
////                                case CrossSync.SyncTypes.EquipItem:
////                                    {
////                                        ItemHandler.EquipItem(client, res.Data, (byte)res.Data2);
////                                        break;
////                                    }
////                                case CrossSync.SyncTypes.UnequipItem:
////                                    {
////                                        ItemHandler.UnEquipItem(client, res.Data);
////                                        break;
////                                    }
////                                case CrossSync.SyncTypes.ConnectionAccept:
////                                    {
////                                        client.Cross.Send(SendEntity(client));
////                                        foreach (var item in client.Inventory.Objects)
////                                        {
////                                            if (item != null)
////                                            {
////                                                if (!item.IsWrap && item.Warehouse == 0)
////                                                {
////                                                    client.Cross.Send(SendItems(item));
////                                                }
////                                            }
////                                        }
////                                        if (client.ChiPowers != null)
////                                        {
////                                            client.Cross.Send(SendChi(client));
////                                        }
////                                        if (client.Player.MyJiang != null)
////                                        {
////                                            client.Cross.Send(SendJiung(client));
////                                        }
////                                        foreach (var item in client.Equipment.Objects)
////                                        {
////                                            if (item != null)
////                                            {
////                                                client.Cross.Send(SendItems(item));
////                                            }
////                                        }
////                                        foreach (var spell in client.Spells.Values)
////                                        {
////                                            client.Cross.Send(spell.Buffer);
////                                        }
////                                        foreach (var prof in client.Proficiencies.Values)
////                                        {
////                                            client.Cross.Send(prof.Buffer);
////                                        }
////                                        client.Cross.Send(new CrossSync()
////                                        {
////                                            Type = CrossSync.SyncTypes.DoneSpawn,
////                                        }.ToArray());
////                                        break;
////                                    }
////                                case CrossSync.SyncTypes.DoneSpawn:
////                                    {
////                                        client.Player.Invisable = true;
////                                        foreach (var xxc in Kernel.GamePool.Values)
////                                        {
////                                            if (Kernel.GetDistance(client.Player.X, client.Player.Y, xxc.Player.X, xxc.Player.Y) > 19)
////                                            {
////                                                Data data = new Data(true);
////                                                data.UID = client.Player.UID;
////                                                data.ID = Data.RemoveEntity;
////                                                xxc.Send(data);
////                                            }
////                                        }
////                                        client.RemoveScreenSpawn(client.Player, true);
////                                        client.Pipe = true;
////                                        break;
////                                    }
////                            }
////                            break;
////                        }
////                    case 10017:
////                        {
////                            Network.GamePackets.FlagsUpdate x = new FlagsUpdate(true);
////                            x.Deserialize(packet);
////                            switch (x.Type)
////                            {
////                                case FlagsUpdate.DataType.ConquerPoints:
////                                    {
////                                        client.Player.ConquerPoints = (uint)x.Value;
////                                        client.Send(packet);
////                                        break;
////                                    }
////                                case FlagsUpdate.DataType.Money:
////                                    {
////                                        client.Player.Money = (uint)x.Value;
////                                        client.Send(packet);
////                                        break;
////                                    }
////                                case FlagsUpdate.DataType.BoundConquerPoints:
////                                    {
////                                        client.Player.BoundCps = (uint)x.Value;
////                                        client.Send(packet);
////                                        break;
////                                    }
////                            }
////                            client.Send(packet);
////                            break;
////                        }
////                    default:
////                        {
////                            client.Send(packet);
////                            break;
////                        }
////                }
////            }
////        }
////        public static byte[] SendMap(ushort MapID, uint X, uint Y)
////        {
////            CrossSync packet = new CrossSync()
////            {
////                Type = CrossSync.SyncTypes.Map,
////                Data = MapID,
////                Data2 = X,
////                Data3 = Y
////            };
////            return packet.ToArray();
////        }
////        public static byte[] SendChi(Client.GameState client)
////        {
////            var packet = new byte[300];
////            Writer.WriteUInt16((ushort)(packet.Length - 8), 0, packet);
////            Writer.WriteUInt16((ushort)9005, 2, packet);
////            int offset = 4;
////            if (client.ChiData != null)
////            {
////                foreach (var power in client.ChiData.Powers)
////                {
////                    if (power != null)
////                    {
////                        Writer.WriteUInt32((uint)power.Power, offset, packet); offset += 4;
////                        foreach (var att in power.Attributes)
////                        {
////                            Writer.WriteUInt32((uint)att.Type, offset, packet); offset += 4;
////                            Writer.WriteUInt32((uint)att.Value, offset, packet); offset += 4;
////                        }
////                    }
////                    else
////                    {
////                        offset += 32;
////                    }
////                }
////            }
////            return packet;
////        }
////        public static byte[] SendItems(Network.GamePackets.ConquerItem item)
////        {
////            if (item == null)
////                return null;
////            byte[] packet = new byte[300];
////            Writer.WriteUInt16((ushort)(packet.Length - 8), 0, packet);
////            Writer.WriteUInt16((ushort)9002, 2, packet);
////            var Offset = 4;
////            Writer.WriteUInt32((uint)item.UID, Offset, packet); Offset += 4;
////            Writer.WriteUInt32((uint)item.ID, Offset, packet); Offset += 4;
////            Writer.WriteUInt32((uint)item.Durability, Offset, packet); Offset += 4;
////            Writer.WriteUInt32((uint)item.MaximDurability, Offset, packet); Offset += 4;
////            Writer.WriteUInt32((uint)item.Position, Offset, packet); Offset += 4;
////            Writer.WriteUInt32((uint)item.SocketProgress, Offset, packet); Offset += 4;
////            Writer.WriteUInt32((uint)item.SocketOne, Offset, packet); Offset += 4;
////            Writer.WriteUInt32((uint)item.SocketTwo, Offset, packet); Offset += 4;
////            Writer.WriteUInt32((uint)item.Effect, Offset, packet); Offset += 4;
////            Writer.WriteUInt32((uint)item.Plus, Offset, packet); Offset += 4;
////            Writer.WriteUInt32((uint)item.Bless, Offset, packet); Offset += 4;
////            Writer.WriteBoolean(item.Bound, Offset, packet); Offset += 4;
////            Writer.WriteUInt32((uint)item.Enchant, Offset, packet); Offset += 4;
////            Writer.WriteUInt32((uint)item.NextRed, Offset, packet); Offset += 4;
////            Writer.WriteUInt32((uint)item.NextBlue, Offset, packet); Offset += 4;
////            Writer.WriteUInt32((uint)item.NextGreen, Offset, packet); Offset += 4;
////            Writer.WriteUInt32((uint)item.Lock, Offset, packet); Offset += 4;
////            Writer.WriteUInt32((uint)item.Color, Offset, packet); Offset += 4;
////            Writer.WriteUInt32((uint)item.PlusProgress, Offset, packet); Offset += 4;
////            Writer.WriteUInt32((uint)item.StackSize, Offset, packet); Offset += 4;
////            Writer.WriteUInt32((uint)item.Stars, Offset, packet); Offset += 4;
////            Writer.WriteUInt32((uint)item.PerfectionProgress, Offset, packet); Offset += 4;
////            Writer.WriteUInt32((uint)item.PerfectionOwnerGuid, Offset, packet); Offset += 4;
////            Writer.WriteString((string)item.PerfectionOwner, Offset, packet); Offset += 16;
////            Writer.WriteString((string)item.Signature, Offset, packet); Offset += 16;
////            if (item.Purification.Available)
////            {
////                Writer.WriteUInt64((ulong)item.Purification.AddedOn.Ticks, Offset, packet); Offset += 8;
////                Writer.WriteUInt32((uint)item.Purification.ItemUID, Offset, packet); Offset += 4;
////                Writer.WriteUInt32((uint)item.Purification.PurificationDuration, Offset, packet); Offset += 4;
////                Writer.WriteUInt32((uint)item.Purification.PurificationItemID, Offset, packet); Offset += 4;
////                Writer.WriteUInt32((uint)item.Purification.PurificationLevel, Offset, packet); Offset += 4;
////            }
////            else
////            {
////                Offset += 24;
////            }
////            if (item.ExtraEffect.Available)
////            {
////                Writer.WriteUInt64((ulong)item.ExtraEffect.AddedOn.Ticks, Offset, packet); Offset += 8;
////                Writer.WriteUInt32((uint)item.ExtraEffect.EffectDuration, Offset, packet); Offset += 4;
////                Writer.WriteUInt32((uint)item.ExtraEffect.EffectID, Offset, packet); Offset += 4;
////                Writer.WriteUInt32((uint)item.ExtraEffect.EffectLevel, Offset, packet); Offset += 4;
////                Writer.WriteUInt32((uint)item.ExtraEffect.EffectPercent, Offset, packet); Offset += 4;
////                Writer.WriteUInt32((uint)item.ExtraEffect.ItemUID, Offset, packet); Offset += 4;
////            }
////            else
////            {
////                Offset += 28;
////            }
////            return packet;
////        }
////        public static byte[] SendEntity(GameState client)
////        {
////            byte[] packet = new byte[400 + 8];
////            Writer.WriteUInt16((ushort)(packet.Length - 8), 0, packet);
////            Writer.WriteUInt16((ushort)9001, 2, packet);
////            Writer.WriteUInt16((ushort)client.Player.Body, 8, packet);
////            Writer.WriteUInt16((ushort)client.Player.Face, 10, packet);
////            Writer.WriteUint((uint)client.Player.UID, 12, packet);
////            Writer.WriteUint((uint)client.Player.GuildID, 16, packet);
////            Writer.WriteUint((uint)client.Player.GuildRank, 20, packet);
////            Writer.WriteUInt64((ulong)client.Player.StatusFlag, 24, packet);
////            Writer.WriteUInt64((ulong)client.Player.StatusFlag2, 32, packet);
////            Writer.WriteUInt64((ulong)client.Player.StatusFlag3, 40, packet);
////            Writer.WriteUInt64((ulong)client.Player.StatusFlag4, 48, packet);
////            Writer.WriteUInt32((uint)client.Player.Appearance, 56, packet);
////            Writer.WriteUInt32((uint)client.Player.Hitpoints, 60, packet);
////            Writer.WriteUInt32((uint)client.Player.Mana, 62, packet);
////            Writer.WriteUInt32((uint)client.Player.X, 64, packet);
////            Writer.WriteUInt32((uint)client.Player.Y, 68, packet);
////            Writer.WriteUInt32((uint)client.Player.Reborn, 72, packet);
////            Writer.WriteUInt32((uint)client.Player.Level, 76, packet);
////            Writer.WriteUInt32((uint)client.Player.ExtraBattlePower, 80, packet);
////            Writer.WriteUInt32((uint)client.Player.NobilityRank, 84, packet);
////            Writer.WriteUInt32((uint)client.Player.ClanId, 88, packet);
////            Writer.WriteUInt32((uint)client.Player.ClanRank, 92, packet);
////            Writer.WriteUInt32((uint)client.Player.MyTitle, 96, packet);
////            Writer.WriteUInt32((uint)client.Player.FirstRebornClass, 100, packet);
////            Writer.WriteUInt32((uint)client.Player.SecondRebornClass, 104, packet);
////            Writer.WriteUInt32((uint)client.Player.Class, 108, packet);
////            Writer.WriteUInt32((uint)client.Player.CountryID, 112, packet);
////            Writer.WriteUInt32((uint)client.Player.HeavenBlessing, 114, packet);
////            Writer.WriteUInt32((uint)client.Player.AssassinBP, 116, packet);

////            Writer.WriteInt32((int)client.ChiData.DragonPoints, 118, packet);
////            Writer.WriteInt32((int)client.ChiData.PhoenixPoints, 122, packet);
////            Writer.WriteInt32((int)client.ChiData.TigerPoints, 126, packet);
////            Writer.WriteInt32((int)client.ChiData.PhoenixPoints, 130, packet);

////            Writer.WriteInt32((int)client.Player.BattlePower, 131, packet);

////            //Writer.WriteUInt32((uint)client.UnionID, 120, packet);
////            //Writer.WriteUInt32((uint)client.UnionExploits, 124, packet);
////            //Writer.WriteUInt32((uint)client.UnionTitle, 128, packet);
////            //Writer.WriteUInt32((uint)client.UnionRank, 132, packet);

////            Writer.WriteUInt32((uint)client.Player.EquippedTitle, 136, packet);
////            Writer.WriteUInt32((uint)client.Player.EquippedWing, 140, packet);
////            Writer.WriteStringWithLength((string)client.Player.Name, 144, packet);
////            Writer.WriteStringWithLength((string)client.Player.ClanName, 161, packet);
////            //Writer.WriteStringWithLength((string)client.UnionName, 177, packet);
////            int offset = 200;
////            Writer.WriteUInt32((uint)client.Player.Strength, offset, packet); offset += 4;
////            Writer.WriteUInt32((uint)client.Player.Agility, offset, packet); offset += 4;
////            Writer.WriteUInt32((uint)client.Player.Spirit, offset, packet); offset += 4;
////            Writer.WriteUInt32((uint)client.Player.Vitality, offset, packet); offset += 4;
////            Writer.WriteUInt32((uint)client.Player.HairColor, offset, packet); offset += 4;
////            Writer.WriteUInt32((uint)client.Player.HairStyle, offset, packet); offset += 4;
////            Writer.WriteUInt32((uint)CrossClient.ServerID, offset, packet); offset += 4;
////            Writer.WriteStringWithLength((string)client.Player.Spouse, offset, packet);
////            offset += 17;
////            Writer.WriteUInt64((ulong)client.Player.Money, offset, packet); offset += 8;
////            Writer.WriteUInt32((uint)client.Player.BoundCps, offset, packet); offset += 4;
////            Writer.WriteUInt32((uint)client.Player.ConquerPoints, offset, packet); offset += 4;
////            if (client.Guild != null)
////            {
////                Writer.WriteStringWithLength(client.Guild.Name, offset, packet);
////                offset += 17;
////                Writer.WriteStringWithLength(client.Guild.LeaderName, offset, packet);
////                offset += 17;

////            }
////            else
////                offset += 34;
////            return packet;
////        }
////        public static byte[] SendJiung(GameState client)
////        {
////            byte[] packet = new byte[800 + 8];
////            Writer.WriteUInt16((ushort)(packet.Length - 8), 0, packet);
////            Writer.WriteUInt16((ushort)9010, 2, packet);
////            Writer.WriteStringWithLengthX(client.Player.MyJiang.ToString(), 4, packet);
////            return packet;
////        }
////        public unsafe class CrossSync : Writer
////        {
////            private byte[] Buffer;
////            public enum SyncTypes : int
////            {
////                Teleport = 1,
////                Map = 2,
////                ConnectionAccept = 3,
////                EquipItem = 5,
////                UnequipItem = 6,
////                UpdateItemStack = 7,
////                StrickPoint = 50,
////                DeleteItem = 9,
////                UpdateItemUID = 8,
////                DoneSpawn = 4,
////            }
////            public CrossSync()
////            {
////                Buffer = new byte[28];
////                WriteUInt16(20, 0, Buffer);
////                WriteUInt16(9000, 2, Buffer);
////            }
////            public SyncTypes Type
////            {
////                get { return (SyncTypes)BitConverter.ToUInt32(Buffer, 4); }
////                set { WriteUInt32((uint)value, 4, Buffer); }
////            }
////            public uint Data
////            {
////                get { return BitConverter.ToUInt32(Buffer, 8); }
////                set { WriteUInt32(value, 8, Buffer); }
////            }
////            public uint Data2
////            {
////                get { return BitConverter.ToUInt32(Buffer, 12); }
////                set { WriteUInt32(value, 12, Buffer); }
////            }
////            public uint Data3
////            {
////                get { return BitConverter.ToUInt32(Buffer, 16); }
////                set { WriteUInt32(value, 16, Buffer); }
////            }
////            public byte[] ToArray()
////            {
////                return Buffer;
////            }
////            public void Deserialization(byte[] p)
////            {
////                Buffer = p;
////            }
////        }
////    }
////}

//namespace Revora
//{
//    public class Packets2
//    {

//        public static void HandelPacket(GameState client, byte[] packet)
//        {
//            ushort ID = BitConverter.ToUInt16(packet, 2);
//            if (client != null && packet != null)
//            {
//                switch (ID)
//                {
//                    case 9002:
//                        {
//                            ushort offset = 4;
//                            Network.GamePackets.ConquerItem item = new Network.GamePackets.ConquerItem(true);
//                            item.Mode = Game.Enums.ItemMode.Default;
//                            item.UID = Network.GamePackets.ConquerItem.ItemUID.Next;
//                            item.CrossID = BitConverter.ToUInt32(packet, offset); offset += 4;
//                            item.ID = BitConverter.ToUInt32(packet, offset); offset += 4;
//                            item.Durability = BitConverter.ToUInt16(packet, offset); offset += 4;
//                            item.MaximDurability = BitConverter.ToUInt16(packet, offset); offset += 4;
//                            item.Position = BitConverter.ToUInt16(packet, offset); offset += 4;
//                            item.SocketProgress = BitConverter.ToUInt32(packet, offset); offset += 4;
//                            item.SocketOne = (Game.Enums.Gem)BitConverter.ToUInt32(packet, offset); offset += 4;
//                            item.SocketTwo = (Game.Enums.Gem)BitConverter.ToUInt32(packet, offset); offset += 4;
//                            item.Effect = (Game.Enums.ItemEffect)BitConverter.ToUInt32(packet, offset); offset += 4;
//                            item.Plus = (byte)BitConverter.ToUInt32(packet, offset); offset += 4;
//                            item.Bless = (byte)BitConverter.ToUInt32(packet, offset); offset += 4;
//                            item.Bound = BitConverter.ToBoolean(packet, offset); offset += 4;
//                            item.Enchant = (byte)BitConverter.ToUInt32(packet, offset); offset += 4;
//                            item.NextRed = (byte)BitConverter.ToUInt32(packet, offset); offset += 4;
//                            item.NextBlue = (byte)BitConverter.ToUInt32(packet, offset); offset += 4;
//                            item.NextGreen = (byte)BitConverter.ToUInt32(packet, offset); offset += 4;
//                            item.Lock = (byte)BitConverter.ToUInt32(packet, offset); offset += 4;
//                            item.Color = (Game.Enums.Color)BitConverter.ToUInt32(packet, offset); offset += 4;
//                            item.PlusProgress = BitConverter.ToUInt32(packet, offset); offset += 4;
//                            item.StackSize = (ushort)BitConverter.ToUInt32(packet, offset); offset += 4;
//                            item.Stars = BitConverter.ToUInt32(packet, offset); offset += 4;
//                            item.PerfectionProgress = BitConverter.ToUInt32(packet, offset); offset += 4;
//                            item.PerfectionOwnerGuid = BitConverter.ToUInt32(packet, offset); offset += 4;
//                            item.PerfectionOwner = ASCIIEncoding.Default.GetString(packet, offset, 16).TrimEnd('\0'); offset += 16;
//                            item.Signature = ASCIIEncoding.Default.GetString(packet, offset, 16).TrimEnd('\0'); offset += 16;
//                            client.Inventory.Add(item, Game.Enums.ItemUse.CreateAndAdd);
//                            client.Cross.Send(new CrossSync()
//                            {
//                                Type = CrossSync.SyncTypes.UpdateItemUID,
//                                Data = item.CrossID,
//                                Data2 = item.UID,
//                            }.ToArray());
//                            break;
//                        }
//                    case 9000:
//                        {
//                            CrossSync res = new CrossSync();
//                            res.Deserialization(packet);

//                            switch (res.Type)
//                            {
//                                case CrossSync.SyncTypes.StrickPoint:
//                                    {
//                                        client.Player.StrikePoints = res.Data;
//                                        break;
//                                    }
//                                case CrossSync.SyncTypes.DeleteItem:
//                                    {
//                                        ConquerItem item = null;
//                                        if (client.Inventory.TryGetItem(res.Data, out item))
//                                        {
//                                            client.Inventory.Remove(item, Game.Enums.ItemUse.Remove);
//                                        }
//                                        break;
//                                    }
//                                case CrossSync.SyncTypes.UpdateItemStack:
//                                    {
//                                        ConquerItem item = null;
//                                        if (client.Inventory.TryGetItem(res.Data, out item))
//                                        {
//                                            item.StackSize = (ushort)res.Data2;
//                                            Database.ConquerItemTable.UpdateStack(item);
//                                        }
//                                        break;
//                                    }
//                                case CrossSync.SyncTypes.EquipItem:
//                                    {
//                                        ItemHandler.EquipItem(client, res.Data, (byte)res.Data2);
//                                        break;
//                                    }
//                                case CrossSync.SyncTypes.UnequipItem:
//                                    {
//                                        ItemHandler.UnEquipItem(client, res.Data);
//                                        break;
//                                    }
//                                case CrossSync.SyncTypes.ConnectionAccept:
//                                    {
//                                        client.Cross.Send(SendEntity(client));
//                                        foreach (var item in client.Inventory.Objects)
//                                        {
//                                            if (item != null)
//                                            {
//                                                if (!item.IsWrap && item.Warehouse == 0)
//                                                {
//                                                    client.Cross.Send(SendItems(item));
//                                                }
//                                            }
//                                        }
//                                        if (client.ChiPowers != null)
//                                        {
//                                            client.Cross.Send(SendChi(client));
//                                        }
//                                        if (client.Player.MyJiang != null)
//                                        {
//                                            client.Cross.Send(SendJiung(client));
//                                        }
//                                        foreach (var item in client.Equipment.Objects)
//                                        {
//                                            if (item != null)
//                                            {
//                                                client.Cross.Send(SendItems(item));
//                                            }
//                                        }
//                                        foreach (var spell in client.Spells.Values)
//                                        {
//                                            client.Cross.Send(spell.Buffer);
//                                        }
//                                        foreach (var prof in client.Proficiencies.Values)
//                                        {
//                                            client.Cross.Send(prof.Buffer);
//                                        }
//                                        client.Cross.Send(new CrossSync()
//                                        {
//                                            Type = CrossSync.SyncTypes.DoneSpawn,
//                                        }.ToArray());
//                                        break;
//                                    }
//                                case CrossSync.SyncTypes.DoneSpawn:
//                                    {
//                                        client.Player.Invisable = true;
//                                        foreach (var xxc in Kernel.GamePool.Values)
//                                        {
//                                            if (Kernel.GetDistance(client.Player.X, client.Player.Y, xxc.Player.X, xxc.Player.Y) > 19)
//                                            {
//                                                Data data = new Data(true);
//                                                data.UID = client.Player.UID;
//                                                data.ID = Data.RemoveEntity;
//                                                xxc.Send(data);
//                                            }
//                                        }
//                                        client.RemoveScreenSpawn(client.Player, true);
//                                        client.Pipe = true;
//                                        break;
//                                    }
//                            }
//                            break;
//                        }
//                    case 10017:
//                        {
//                            Network.GamePackets.FlagsUpdate x = new FlagsUpdate(true);
//                            x.Deserialize(packet);
//                            switch (x.Type)
//                            {
//                                case FlagsUpdate.DataType.ConquerPoints:
//                                    {
//                                        client.Player.ConquerPoints = (uint)x.Value;
//                                        client.Send(packet);
//                                        break;
//                                    }
//                                case FlagsUpdate.DataType.Money:
//                                    {
//                                        client.Player.Money = (uint)x.Value;
//                                        client.Send(packet);
//                                        break;
//                                    }
//                                case FlagsUpdate.DataType.BoundConquerPoints:
//                                    {
//                                        client.Player.BoundCps = (uint)x.Value;
//                                        client.Send(packet);
//                                        break;
//                                    }
//                            }
//                            client.Send(packet);
//                            break;
//                        }
//                    default:
//                        {
//                            client.Send(packet);
//                            break;
//                        }
//                }
//            }
//        }
//        public static byte[] SendMap(ushort MapID, uint X, uint Y)
//        {
//            CrossSync packet = new CrossSync()
//            {
//                Type = CrossSync.SyncTypes.Map,
//                Data = MapID,
//                Data2 = X,
//                Data3 = Y
//            };
//            return packet.ToArray();
//        }
//        public static byte[] SendChi(Client.GameState client)
//        {
//            var packet = new byte[300];
//            Writer.WriteUInt16((ushort)(packet.Length - 8), 0, packet);
//            Writer.WriteUInt16((ushort)9005, 2, packet);
//            int offset = 4;
//            if (client.ChiData != null)
//            {
//                foreach (var power in client.ChiData.Powers)
//                {
//                    if (power != null)
//                    {
//                        Writer.WriteUInt32((uint)power.Power, offset, packet); offset += 4;
//                        foreach (var att in power.Attributes)
//                        {
//                            Writer.WriteUInt32((uint)att.Type, offset, packet); offset += 4;
//                            Writer.WriteUInt32((uint)att.Value, offset, packet); offset += 4;
//                        }
//                    }
//                    else
//                    {
//                        offset += 32;
//                    }
//                }
//            }
//            return packet;
//        }
//        public static byte[] SendItems(Network.GamePackets.ConquerItem item)
//        {
//            if (item == null)
//                return null;
//            byte[] packet = new byte[300];
//            Writer.WriteUInt16((ushort)(packet.Length - 8), 0, packet);
//            Writer.WriteUInt16((ushort)9002, 2, packet);
//            var Offset = 4;
//            Writer.WriteUInt32((uint)item.UID, Offset, packet); Offset += 4;
//            Writer.WriteUInt32((uint)item.ID, Offset, packet); Offset += 4;
//            Writer.WriteUInt32((uint)item.Durability, Offset, packet); Offset += 4;
//            Writer.WriteUInt32((uint)item.MaximDurability, Offset, packet); Offset += 4;
//            Writer.WriteUInt32((uint)item.Position, Offset, packet); Offset += 4;
//            Writer.WriteUInt32((uint)item.SocketProgress, Offset, packet); Offset += 4;
//            Writer.WriteUInt32((uint)item.SocketOne, Offset, packet); Offset += 4;
//            Writer.WriteUInt32((uint)item.SocketTwo, Offset, packet); Offset += 4;
//            Writer.WriteUInt32((uint)item.Effect, Offset, packet); Offset += 4;
//            Writer.WriteUInt32((uint)item.Plus, Offset, packet); Offset += 4;
//            Writer.WriteUInt32((uint)item.Bless, Offset, packet); Offset += 4;
//            Writer.WriteBoolean(item.Bound, Offset, packet); Offset += 4;
//            Writer.WriteUInt32((uint)item.Enchant, Offset, packet); Offset += 4;
//            Writer.WriteUInt32((uint)item.NextRed, Offset, packet); Offset += 4;
//            Writer.WriteUInt32((uint)item.NextBlue, Offset, packet); Offset += 4;
//            Writer.WriteUInt32((uint)item.NextGreen, Offset, packet); Offset += 4;
//            Writer.WriteUInt32((uint)item.Lock, Offset, packet); Offset += 4;
//            Writer.WriteUInt32((uint)item.Color, Offset, packet); Offset += 4;
//            Writer.WriteUInt32((uint)item.PlusProgress, Offset, packet); Offset += 4;
//            Writer.WriteUInt32((uint)item.StackSize, Offset, packet); Offset += 4;
//            Writer.WriteUInt32((uint)item.Stars, Offset, packet); Offset += 4;
//            Writer.WriteUInt32((uint)item.PerfectionProgress, Offset, packet); Offset += 4;
//            Writer.WriteUInt32((uint)item.PerfectionOwnerGuid, Offset, packet); Offset += 4;
//            Writer.WriteString((string)item.PerfectionOwner, Offset, packet); Offset += 16;
//            Writer.WriteString((string)item.Signature, Offset, packet); Offset += 16;
//            if (item.Purification.Available)
//            {
//                Writer.WriteUInt64((ulong)item.Purification.AddedOn.Ticks, Offset, packet); Offset += 8;
//                Writer.WriteUInt32((uint)item.Purification.ItemUID, Offset, packet); Offset += 4;
//                Writer.WriteUInt32((uint)item.Purification.PurificationDuration, Offset, packet); Offset += 4;
//                Writer.WriteUInt32((uint)item.Purification.PurificationItemID, Offset, packet); Offset += 4;
//                Writer.WriteUInt32((uint)item.Purification.PurificationLevel, Offset, packet); Offset += 4;
//            }
//            else
//            {
//                Offset += 24;
//            }
//            if (item.ExtraEffect.Available)
//            {
//                Writer.WriteUInt64((ulong)item.ExtraEffect.AddedOn.Ticks, Offset, packet); Offset += 8;
//                Writer.WriteUInt32((uint)item.ExtraEffect.EffectDuration, Offset, packet); Offset += 4;
//                Writer.WriteUInt32((uint)item.ExtraEffect.EffectID, Offset, packet); Offset += 4;
//                Writer.WriteUInt32((uint)item.ExtraEffect.EffectLevel, Offset, packet); Offset += 4;
//                Writer.WriteUInt32((uint)item.ExtraEffect.EffectPercent, Offset, packet); Offset += 4;
//                Writer.WriteUInt32((uint)item.ExtraEffect.ItemUID, Offset, packet); Offset += 4;
//            }
//            else
//            {
//                Offset += 28;
//            }
//            return packet;
//        }
//        public static byte[] SendEntity(GameState client)
//        {
//            byte[] packet = new byte[400 + 8];
//            Writer.WriteUInt16((ushort)(packet.Length - 8), 0, packet);
//            Writer.WriteUInt16((ushort)9001, 2, packet);
//            Writer.WriteUInt16((ushort)client.Player.Body, 8, packet);
//            Writer.WriteUInt16((ushort)client.Player.Face, 10, packet);
//            Writer.WriteUint((uint)client.Player.UID, 12, packet);
//            Writer.WriteUint((uint)client.Player.GuildID, 16, packet);
//            Writer.WriteUint((uint)client.Player.GuildRank, 20, packet);
//            Writer.WriteUInt64((ulong)client.Player.StatusFlag, 24, packet);
//            Writer.WriteUInt64((ulong)client.Player.StatusFlag2, 32, packet);
//            Writer.WriteUInt64((ulong)client.Player.StatusFlag3, 40, packet);
//            Writer.WriteUInt64((ulong)client.Player.StatusFlag4, 48, packet);
//            Writer.WriteUInt32((uint)client.Player.Appearance, 56, packet);
//            Writer.WriteUInt32((uint)client.Player.Hitpoints, 60, packet);
//            Writer.WriteUInt32((uint)client.Player.Mana, 62, packet);
//            Writer.WriteUInt32((uint)client.Player.X, 64, packet);
//            Writer.WriteUInt32((uint)client.Player.Y, 68, packet);
//            Writer.WriteUInt32((uint)client.Player.Reborn, 72, packet);
//            Writer.WriteUInt32((uint)client.Player.Level, 76, packet);
//            Writer.WriteUInt32((uint)client.Player.ExtraBattlePower, 80, packet);
//            Writer.WriteUInt32((uint)client.Player.NobilityRank, 84, packet);
//            Writer.WriteUInt32((uint)client.Player.ClanId, 88, packet);
//            Writer.WriteUInt32((uint)client.Player.ClanRank, 92, packet);
//            Writer.WriteUInt32((uint)client.Player.MyTitle, 96, packet);
//            Writer.WriteUInt32((uint)client.Player.FirstRebornClass, 100, packet);
//            Writer.WriteUInt32((uint)client.Player.SecondRebornClass, 104, packet);
//            Writer.WriteUInt32((uint)client.Player.Class, 108, packet);
//            Writer.WriteUInt32((uint)client.Player.CountryID, 112, packet);
//            Writer.WriteUInt32((uint)client.Player.HeavenBlessing, 114, packet);
//            Writer.WriteUInt32((uint)client.Player.AssassinBP, 116, packet);

//            Writer.WriteInt32((int)client.ChiData.DragonPoints, 118, packet);
//            Writer.WriteInt32((int)client.ChiData.PhoenixPoints, 122, packet);
//            Writer.WriteInt32((int)client.ChiData.TigerPoints, 126, packet);
//            Writer.WriteInt32((int)client.ChiData.PhoenixPoints, 130, packet);

//            Writer.WriteUInt32((uint)client.Player.GuildSharedBp, 131, packet);

//            Writer.WriteUInt32((uint)client.UnionID, 120, packet);
//            Writer.WriteUInt32((uint)client.UnionExploits, 124, packet);
//            Writer.WriteUInt32((uint)client.UnionTitle, 128, packet);
//            //Writer.WriteUInt32((uint)client.UnionRank, 132, packet);

//            Writer.WriteUInt32((uint)client.Player.EquippedTitle, 136, packet);
//            Writer.WriteUInt32((uint)client.Player.EquippedWing, 140, packet);
//            Writer.WriteStringWithLength((string)client.Player.Name, 144, packet);
//            Writer.WriteStringWithLength((string)client.Player.ClanName, 161, packet);
//            Writer.WriteStringWithLength((string)client.Player.UnionName, 177, packet);
//            int offset = 200;
//            Writer.WriteUInt32((uint)client.Player.Strength, offset, packet); offset += 4;
//            Writer.WriteUInt32((uint)client.Player.Agility, offset, packet); offset += 4;
//            Writer.WriteUInt32((uint)client.Player.Spirit, offset, packet); offset += 4;
//            Writer.WriteUInt32((uint)client.Player.Vitality, offset, packet); offset += 4;
//            Writer.WriteUInt32((uint)client.Player.HairColor, offset, packet); offset += 4;
//            Writer.WriteUInt32((uint)client.Player.HairStyle, offset, packet); offset += 4;
//            Writer.WriteUInt32((uint)CrossClient.ServerID, offset, packet); offset += 4;
//            Writer.WriteStringWithLength((string)client.Player.Spouse, offset, packet);
//            offset += 17;
//            Writer.WriteUInt64((ulong)client.Player.Money, offset, packet); offset += 8;
//            Writer.WriteUInt32((uint)client.Player.BoundCps, offset, packet); offset += 4;
//            Writer.WriteUInt32((uint)client.Player.ConquerPoints, offset, packet); offset += 4;
//            if (client.Guild != null)
//            {
//                Writer.WriteStringWithLength(client.Guild.Name, offset, packet);
//                offset += 17;
//                Writer.WriteStringWithLength(client.Guild.LeaderName, offset, packet);
//                offset += 17;

//            }
//            else
//                offset += 34;
//            return packet;
//        }
//        public static byte[] SendJiung(GameState client)
//        {
//            byte[] packet = new byte[800 + 8];
//            Writer.WriteUInt16((ushort)(packet.Length - 8), 0, packet);
//            Writer.WriteUInt16((ushort)9010, 2, packet);
//            Writer.WriteStringWithLengthX(client.Player.MyJiang.ToString(), 4, packet);
//            return packet;
//        }
//        public unsafe class CrossSync : Writer
//        {
//            private byte[] Buffer;
//            public enum SyncTypes : int
//            {
//                Teleport = 1,
//                Map = 2,
//                ConnectionAccept = 3,
//                EquipItem = 5,
//                UnequipItem = 6,
//                UpdateItemStack = 7,
//                StrickPoint = 50,
//                DeleteItem = 9,
//                UpdateItemUID = 8,
//                DoneSpawn = 4,
//            }
//            public CrossSync()
//            {
//                Buffer = new byte[28];
//                WriteUInt16(20, 0, Buffer);
//                WriteUInt16(9000, 2, Buffer);
//            }
//            public SyncTypes Type
//            {
//                get { return (SyncTypes)BitConverter.ToUInt32(Buffer, 4); }
//                set { WriteUInt32((uint)value, 4, Buffer); }
//            }
//            public uint Data
//            {
//                get { return BitConverter.ToUInt32(Buffer, 8); }
//                set { WriteUInt32(value, 8, Buffer); }
//            }
//            public uint Data2
//            {
//                get { return BitConverter.ToUInt32(Buffer, 12); }
//                set { WriteUInt32(value, 12, Buffer); }
//            }
//            public uint Data3
//            {
//                get { return BitConverter.ToUInt32(Buffer, 16); }
//                set { WriteUInt32(value, 16, Buffer); }
//            }
//            public byte[] ToArray()
//            {
//                return Buffer;
//            }
//            public void Deserialization(byte[] p)
//            {
//                Buffer = p;
//            }
//        }
//    }
//}

