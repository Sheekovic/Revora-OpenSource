using Revora.Client;
using Revora.Database;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Revora.Network.GamePackets
{
    public class CMsgEquipRefineRank
    {
        private byte[] _packet;
        public CMsgEquipRefineRank(byte[] incoming)
        {
            _packet = incoming;
        }
        public bool Read()
        {
            var dePacket = new byte[_packet.Length - 8];

            for (int i = 0; i < dePacket.Length; i++)
            {
                dePacket[i] = _packet[i];
            }

            Data = Serializer.DeserializeWithLengthPrefix<EquipRefineRankProto>(new MemoryStream(dePacket), PrefixStyle.Fixed32);

            return true;
        }
        public void Handle(GameState client)
        {
            switch (Data.Type)
            {
                case EquipRefineRankProto.Action.RequestRanking:
                    var myPacket = new EquipRefineRankProto();
                    #region Top items
                    var allItems = FindAllPerfectedItems();
                    var myDic = new Dictionary<EquipRefineProto.Positions, ConquerItem>();
                    myDic.Add(EquipRefineProto.Positions.Headgear, allItems.Where(item => ItemHandler.GetPositionFromID(item.ID) == ItemHandler.Positions.Head).FindMax(item => item.Prestige));
                    myDic.Add(EquipRefineProto.Positions.Necklace, allItems.Where(item => ItemHandler.GetPositionFromID(item.ID) == ItemHandler.Positions.Necklace).FindMax(item => item.Prestige));
                    myDic.Add(EquipRefineProto.Positions.Armor, allItems.Where(item => ItemHandler.GetPositionFromID(item.ID) == ItemHandler.Positions.Armor).FindMax(item => item.Prestige));
                    myDic.Add(EquipRefineProto.Positions.Weapon, allItems.Where(item => ItemHandler.GetPositionFromID(item.ID) == ItemHandler.Positions.Right).FindMax(item => item.Prestige));
                    myDic.Add(EquipRefineProto.Positions.Wing, allItems.Where(item => ItemHandler.GetPositionFromID(item.ID) == ItemHandler.Positions.Wing).FindMax(item => item.Prestige));
                    myDic.Add(EquipRefineProto.Positions.Ring, allItems.Where(item => ItemHandler.GetPositionFromID(item.ID) == ItemHandler.Positions.Ring).FindMax(item => item.Prestige));
                    myDic.Add(EquipRefineProto.Positions.Crop, allItems.Where(item => ItemHandler.GetPositionFromID(item.ID) == ItemHandler.Positions.SteedTalisman).FindMax(item => item.Prestige));
                    myDic.Add(EquipRefineProto.Positions.Boot, allItems.Where(item => ItemHandler.GetPositionFromID(item.ID) == ItemHandler.Positions.Boots).FindMax(item => item.Prestige));
                    myDic.Add(EquipRefineProto.Positions.Steed, allItems.Where(item => ItemHandler.GetPositionFromID(item.ID) == ItemHandler.Positions.Steed).FindMax(item => item.Prestige));
                    myDic.Add(EquipRefineProto.Positions.Fan, allItems.Where(item => ItemHandler.GetPositionFromID(item.ID) == ItemHandler.Positions.AttackTalisman).FindMax(item => item.Prestige));
                    myDic.Add(EquipRefineProto.Positions.Tower, allItems.Where(item => ItemHandler.GetPositionFromID(item.ID) == ItemHandler.Positions.DefenceTalisman).FindMax(item => item.Prestige));
                    #endregion
                    foreach (var kVPair in myDic.ToDictionary(obj => obj.Key, obj => obj.Value))
                    {
                        if (kVPair.Value == null)
                            myDic.Remove(kVPair.Key);
                    }
                    myPacket.Data = new EquipRefineProto[myDic.Count];
                    var index = 0;
                    foreach (var kVPair in myDic)
                    {
                        var item = kVPair.Value;
                        myPacket.Data[index] = new EquipRefineProto();
                        myPacket.Data[index].ItemCraftLevel = item.Plus;
                        myPacket.Data[index].ItemGuid = item.UID;
                        myPacket.Data[index].ItemId = (int)item.ID;
                        myPacket.Data[index].ItemPerfectionLevel = (int)item.PerfectionLevel;
                        myPacket.Data[index].ItemRank = 1;
                        myPacket.Data[index].Name = item.PerfectionOwner;
                        myPacket.Data[index].Position = kVPair.Key;
                        myPacket.Data[index].PrestigePoints = item.Prestige;
                        myPacket.Data[index].SoulId = (int)(item.Purification.PurificationItemID > 0 ? item.Purification.PurificationItemID : 0);
                        index++;
                    }
                    _packet = FinalizeProtoBuf(myPacket);
                    client.Send(this);
                    break;
                case EquipRefineRankProto.Action.RequestItemInfo:
                    var itemRecord = ConquerItemTable.LoadItem(Data.ItemGOP);
                    if (itemRecord != null)
                    {
                        itemRecord.Mode = Game.Enums.ItemMode.PerfectionView;
                        client.Send(itemRecord);
                    }
                    else client.Send(this);
                    break;
                case EquipRefineRankProto.Action.RequestItemRanking:
                    var pos = (EquipRefineProto.Positions)Data.ItemGOP;
                    myPacket = new EquipRefineRankProto();
                    myPacket.Type = Data.Type;
                    myPacket.Data = new EquipRefineProto[10];
                    allItems = FindAllPerfectedItems();

                    index = 0;

                    switch (pos)
                    {
                        case EquipRefineProto.Positions.Headgear:
                            var rankItems = allItems.Where(i => ItemHandler.GetPositionFromID(i.ID) == ItemHandler.Positions.Head).FindMax(i => i.Prestige, 10, Data.Page * 10).ToList();
                            var Owners = new List<string>();
                            foreach (var item in rankItems.ToList())
                            {
                                if (Owners.Contains(item.PerfectionOwner))
                                    rankItems.Remove(item);
                                else Owners.Add(item.PerfectionOwner);
                            }
                            foreach (var rankItem in rankItems)
                            {
                                myPacket.Data[index] = new EquipRefineProto();
                                myPacket.Data[index].ItemCraftLevel = rankItem.Plus;
                                myPacket.Data[index].ItemGuid = rankItem.UID;
                                myPacket.Data[index].ItemId = (int)rankItem.ID;
                                myPacket.Data[index].ItemPerfectionLevel = (int)rankItem.PerfectionLevel;
                                myPacket.Data[index].ItemRank = index + 1;
                                myPacket.Data[index].Name = rankItem.PerfectionOwner;
                                myPacket.Data[index].Position = EquipRefineProto.Positions.Headgear;
                                myPacket.Data[index].PrestigePoints = rankItem.Prestige;
                                myPacket.Data[index].SoulId = (int)(rankItem.Purification.PurificationItemID > 0 ? rankItem.Purification.PurificationItemID : 0);
                                index++;
                            }
                            break;
                        case EquipRefineProto.Positions.Necklace:
                            rankItems = allItems.Where(i => ItemHandler.GetPositionFromID(i.ID) == ItemHandler.Positions.Necklace).FindMax(i => i.Prestige, 10, Data.Page * 10).ToList();
                            Owners = new List<string>();
                            foreach (var item in rankItems.ToList())
                            {
                                if (Owners.Contains(item.PerfectionOwner))
                                    rankItems.Remove(item);
                                else Owners.Add(item.PerfectionOwner);
                            }
                            foreach (var rankItem in rankItems)
                            {
                                myPacket.Data[index] = new EquipRefineProto();
                                myPacket.Data[index].ItemCraftLevel = rankItem.Plus;
                                myPacket.Data[index].ItemGuid = rankItem.UID;
                                myPacket.Data[index].ItemId = (int)rankItem.ID;
                                myPacket.Data[index].ItemPerfectionLevel = (int)rankItem.PerfectionLevel;
                                myPacket.Data[index].ItemRank = index + 1;
                                myPacket.Data[index].Name = rankItem.PerfectionOwner;
                                myPacket.Data[index].Position = EquipRefineProto.Positions.Headgear;
                                myPacket.Data[index].PrestigePoints = rankItem.Prestige;
                                myPacket.Data[index].SoulId = (int)(rankItem.Purification.PurificationItemID > 0 ? rankItem.Purification.PurificationItemID : 0);
                                index++;
                            }
                            break;
                        case EquipRefineProto.Positions.Armor:
                            rankItems = allItems.Where(i => ItemHandler.GetPositionFromID(i.ID) == ItemHandler.Positions.Armor).FindMax(i => i.Prestige, 10, Data.Page * 10).ToList();
                            Owners = new List<string>();
                            foreach (var item in rankItems.ToList())
                            {
                                if (Owners.Contains(item.PerfectionOwner))
                                    rankItems.Remove(item);
                                else Owners.Add(item.PerfectionOwner);
                            }
                            foreach (var rankItem in rankItems)
                            {
                                myPacket.Data[index] = new EquipRefineProto();
                                myPacket.Data[index].ItemCraftLevel = rankItem.Plus;
                                myPacket.Data[index].ItemGuid = rankItem.UID;
                                myPacket.Data[index].ItemId = (int)rankItem.ID;
                                myPacket.Data[index].ItemPerfectionLevel = (int)rankItem.PerfectionLevel;
                                myPacket.Data[index].ItemRank = index + 1;
                                myPacket.Data[index].Name = rankItem.PerfectionOwner;
                                myPacket.Data[index].Position = EquipRefineProto.Positions.Headgear;
                                myPacket.Data[index].PrestigePoints = rankItem.Prestige;
                                myPacket.Data[index].SoulId = (int)(rankItem.Purification.PurificationItemID > 0 ? rankItem.Purification.PurificationItemID : 0);
                                index++;
                            }
                            break;
                        case EquipRefineProto.Positions.Weapon:
                            rankItems = allItems.Where(i => ItemHandler.GetPositionFromID(i.ID) == ItemHandler.Positions.Right).FindMax(i => i.Prestige, 10, Data.Page * 10).ToList();
                            Owners = new List<string>();
                            foreach (var item in rankItems.ToList())
                            {
                                if (Owners.Contains(item.PerfectionOwner))
                                    rankItems.Remove(item);
                                else Owners.Add(item.PerfectionOwner);
                            }
                            foreach (var rankItem in rankItems)
                            {
                                myPacket.Data[index] = new EquipRefineProto();
                                myPacket.Data[index].ItemCraftLevel = rankItem.Plus;
                                myPacket.Data[index].ItemGuid = rankItem.UID;
                                myPacket.Data[index].ItemId = (int)rankItem.ID;
                                myPacket.Data[index].ItemPerfectionLevel = (int)rankItem.PerfectionLevel;
                                myPacket.Data[index].ItemRank = index + 1;
                                myPacket.Data[index].Name = rankItem.PerfectionOwner;
                                myPacket.Data[index].Position = EquipRefineProto.Positions.Headgear;
                                myPacket.Data[index].PrestigePoints = rankItem.Prestige;
                                myPacket.Data[index].SoulId = (int)(rankItem.Purification.PurificationItemID > 0 ? rankItem.Purification.PurificationItemID : 0);
                                index++;
                            }
                            break;
                        case EquipRefineProto.Positions.Wing:
                            rankItems = allItems.Where(i => ItemHandler.GetPositionFromID(i.ID) == ItemHandler.Positions.Wing).FindMax(i => i.Prestige, 10, Data.Page * 10).ToList();
                            Owners = new List<string>();
                            foreach (var item in rankItems.ToList())
                            {
                                if (Owners.Contains(item.PerfectionOwner))
                                    rankItems.Remove(item);
                                else Owners.Add(item.PerfectionOwner);
                            }
                            foreach (var rankItem in rankItems)
                            {
                                myPacket.Data[index] = new EquipRefineProto();
                                myPacket.Data[index].ItemCraftLevel = rankItem.Plus;
                                myPacket.Data[index].ItemGuid = rankItem.UID;
                                myPacket.Data[index].ItemId = (int)rankItem.ID;
                                myPacket.Data[index].ItemPerfectionLevel = (int)rankItem.PerfectionLevel;
                                myPacket.Data[index].ItemRank = index + 1;
                                myPacket.Data[index].Name = rankItem.PerfectionOwner;
                                myPacket.Data[index].Position = EquipRefineProto.Positions.Headgear;
                                myPacket.Data[index].PrestigePoints = rankItem.Prestige;
                                myPacket.Data[index].SoulId = (int)(rankItem.Purification.PurificationItemID > 0 ? rankItem.Purification.PurificationItemID : 0);
                                index++;
                            }
                            break;
                        case EquipRefineProto.Positions.Ring:
                            rankItems = allItems.Where(i => ItemHandler.GetPositionFromID(i.ID) == ItemHandler.Positions.Ring).FindMax(i => i.Prestige, 10, Data.Page * 10).ToList();
                            Owners = new List<string>();
                            foreach (var item in rankItems.ToList())
                            {
                                if (Owners.Contains(item.PerfectionOwner))
                                    rankItems.Remove(item);
                                else Owners.Add(item.PerfectionOwner);
                            }
                            foreach (var rankItem in rankItems)
                            {
                                myPacket.Data[index] = new EquipRefineProto();
                                myPacket.Data[index].ItemCraftLevel = rankItem.Plus;
                                myPacket.Data[index].ItemGuid = rankItem.UID;
                                myPacket.Data[index].ItemId = (int)rankItem.ID;
                                myPacket.Data[index].ItemPerfectionLevel = (int)rankItem.PerfectionLevel;
                                myPacket.Data[index].ItemRank = index + 1;
                                myPacket.Data[index].Name = rankItem.PerfectionOwner;
                                myPacket.Data[index].Position = EquipRefineProto.Positions.Headgear;
                                myPacket.Data[index].PrestigePoints = rankItem.Prestige;
                                myPacket.Data[index].SoulId = (int)(rankItem.Purification.PurificationItemID > 0 ? rankItem.Purification.PurificationItemID : 0);
                                index++;
                            }
                            break;
                        case EquipRefineProto.Positions.Crop:
                            rankItems = allItems.Where(i => ItemHandler.GetPositionFromID(i.ID) == ItemHandler.Positions.SteedTalisman).FindMax(i => i.Prestige, 10, Data.Page * 10).ToList();
                            Owners = new List<string>();
                            foreach (var item in rankItems.ToList())
                            {
                                if (Owners.Contains(item.PerfectionOwner))
                                    rankItems.Remove(item);
                                else Owners.Add(item.PerfectionOwner);
                            }
                            foreach (var rankItem in rankItems)
                            {
                                myPacket.Data[index] = new EquipRefineProto();
                                myPacket.Data[index].ItemCraftLevel = rankItem.Plus;
                                myPacket.Data[index].ItemGuid = rankItem.UID;
                                myPacket.Data[index].ItemId = (int)rankItem.ID;
                                myPacket.Data[index].ItemPerfectionLevel = (int)rankItem.PerfectionLevel;
                                myPacket.Data[index].ItemRank = index + 1;
                                myPacket.Data[index].Name = rankItem.PerfectionOwner;
                                myPacket.Data[index].Position = EquipRefineProto.Positions.Headgear;
                                myPacket.Data[index].PrestigePoints = rankItem.Prestige;
                                myPacket.Data[index].SoulId = (int)(rankItem.Purification.PurificationItemID > 0 ? rankItem.Purification.PurificationItemID : 0);
                                index++;
                            }
                            break;
                        case EquipRefineProto.Positions.Boot:
                            rankItems = allItems.Where(i => ItemHandler.GetPositionFromID(i.ID) == ItemHandler.Positions.Boots).FindMax(i => i.Prestige, 10, Data.Page * 10).ToList();
                            Owners = new List<string>();
                            foreach (var item in rankItems.ToList())
                            {
                                if (Owners.Contains(item.PerfectionOwner))
                                    rankItems.Remove(item);
                                else Owners.Add(item.PerfectionOwner);
                            }
                            foreach (var rankItem in rankItems)
                            {
                                myPacket.Data[index] = new EquipRefineProto();
                                myPacket.Data[index].ItemCraftLevel = rankItem.Plus;
                                myPacket.Data[index].ItemGuid = rankItem.UID;
                                myPacket.Data[index].ItemId = (int)rankItem.ID;
                                myPacket.Data[index].ItemPerfectionLevel = (int)rankItem.PerfectionLevel;
                                myPacket.Data[index].ItemRank = index + 1;
                                myPacket.Data[index].Name = rankItem.PerfectionOwner;
                                myPacket.Data[index].Position = EquipRefineProto.Positions.Headgear;
                                myPacket.Data[index].PrestigePoints = rankItem.Prestige;
                                myPacket.Data[index].SoulId = (int)(rankItem.Purification.PurificationItemID > 0 ? rankItem.Purification.PurificationItemID : 0);
                                index++;
                            }
                            break;
                        case EquipRefineProto.Positions.Steed:
                            rankItems = allItems.Where(i => ItemHandler.GetPositionFromID(i.ID) == ItemHandler.Positions.Steed).FindMax(i => i.Prestige, 10, Data.Page * 10).ToList();
                            Owners = new List<string>();
                            foreach (var item in rankItems.ToList())
                            {
                                if (Owners.Contains(item.PerfectionOwner))
                                    rankItems.Remove(item);
                                else Owners.Add(item.PerfectionOwner);
                            }
                            foreach (var rankItem in rankItems)
                            {
                                myPacket.Data[index] = new EquipRefineProto();
                                myPacket.Data[index].ItemCraftLevel = rankItem.Plus;
                                myPacket.Data[index].ItemGuid = rankItem.UID;
                                myPacket.Data[index].ItemId = (int)rankItem.ID;
                                myPacket.Data[index].ItemPerfectionLevel = (int)rankItem.PerfectionLevel;
                                myPacket.Data[index].ItemRank = index + 1;
                                myPacket.Data[index].Name = rankItem.PerfectionOwner;
                                myPacket.Data[index].Position = EquipRefineProto.Positions.Headgear;
                                myPacket.Data[index].PrestigePoints = rankItem.Prestige;
                                myPacket.Data[index].SoulId = (int)(rankItem.Purification.PurificationItemID > 0 ? rankItem.Purification.PurificationItemID : 0);
                                index++;
                            }
                            break;
                        case EquipRefineProto.Positions.Fan:
                            rankItems = allItems.Where(i => ItemHandler.GetPositionFromID(i.ID) == ItemHandler.Positions.AttackTalisman).FindMax(i => i.Prestige, 10, Data.Page * 10).ToList();
                            Owners = new List<string>();
                            foreach (var item in rankItems.ToList())
                            {
                                if (Owners.Contains(item.PerfectionOwner))
                                    rankItems.Remove(item);
                                else Owners.Add(item.PerfectionOwner);
                            }
                            foreach (var rankItem in rankItems)
                            {
                                myPacket.Data[index] = new EquipRefineProto();
                                myPacket.Data[index].ItemCraftLevel = rankItem.Plus;
                                myPacket.Data[index].ItemGuid = rankItem.UID;
                                myPacket.Data[index].ItemId = (int)rankItem.ID;
                                myPacket.Data[index].ItemPerfectionLevel = (int)rankItem.PerfectionLevel;
                                myPacket.Data[index].ItemRank = index + 1;
                                myPacket.Data[index].Name = rankItem.PerfectionOwner;
                                myPacket.Data[index].Position = EquipRefineProto.Positions.Headgear;
                                myPacket.Data[index].PrestigePoints = rankItem.Prestige;
                                myPacket.Data[index].SoulId = (int)(rankItem.Purification.PurificationItemID > 0 ? rankItem.Purification.PurificationItemID : 0);
                                index++;
                            }
                            break;
                        case EquipRefineProto.Positions.Tower:
                            rankItems = allItems.Where(i => ItemHandler.GetPositionFromID(i.ID) == ItemHandler.Positions.DefenceTalisman).FindMax(i => i.Prestige, 10, Data.Page * 10).ToList();
                            Owners = new List<string>();
                            foreach (var item in rankItems.ToList())
                            {
                                if (Owners.Contains(item.PerfectionOwner))
                                    rankItems.Remove(item);
                                else Owners.Add(item.PerfectionOwner);
                            }
                            foreach (var rankItem in rankItems)
                            {
                                myPacket.Data[index] = new EquipRefineProto();
                                myPacket.Data[index].ItemCraftLevel = rankItem.Plus;
                                myPacket.Data[index].ItemGuid = rankItem.UID;
                                myPacket.Data[index].ItemId = (int)rankItem.ID;
                                myPacket.Data[index].ItemPerfectionLevel = (int)rankItem.PerfectionLevel;
                                myPacket.Data[index].ItemRank = index + 1;
                                myPacket.Data[index].Name = rankItem.PerfectionOwner;
                                myPacket.Data[index].Position = EquipRefineProto.Positions.Headgear;
                                myPacket.Data[index].PrestigePoints = rankItem.Prestige;
                                myPacket.Data[index].SoulId = (int)(rankItem.Purification.PurificationItemID > 0 ? rankItem.Purification.PurificationItemID : 0);
                                index++;
                            }
                            break;
                    }
                    _packet = FinalizeProtoBuf(myPacket);
                    client.Send(this);
                    break;
                case EquipRefineRankProto.Action.UserItemRanking:
                    myPacket = new EquipRefineRankProto();
                    myPacket.Type = Data.Type;
                    var currentPos = (EquipRefineProto.Positions)Data.ItemGOP;
                    var slot = currentPos == EquipRefineProto.Positions.Armor ? ItemHandler.Positions.Armor : currentPos == EquipRefineProto.Positions.Boot ? ItemHandler.Positions.Boots : currentPos == EquipRefineProto.Positions.Crop ? ItemHandler.Positions.SteedTalisman : currentPos == EquipRefineProto.Positions.Fan ? ItemHandler.Positions.AttackTalisman : currentPos == EquipRefineProto.Positions.Headgear ? ItemHandler.Positions.Head : currentPos == EquipRefineProto.Positions.Necklace ? ItemHandler.Positions.Necklace : currentPos == EquipRefineProto.Positions.Ring ? ItemHandler.Positions.Ring : currentPos == EquipRefineProto.Positions.Steed ? ItemHandler.Positions.Steed : currentPos == EquipRefineProto.Positions.Tower ? ItemHandler.Positions.DefenceTalisman : currentPos == EquipRefineProto.Positions.Weapon ? ItemHandler.Positions.Right : ItemHandler.Positions.Wing;
                    var rankedItem = client.Equipment.TryGetItem((byte)slot);

                    allItems = FindAllPerfectedItems()
                        .Where(i => ItemHandler.GetPositionFromID(i.ID) == slot)
                        .FindMax(i => i.Prestige, 50).ToList();

                    myPacket.Data = new EquipRefineProto[1];

                    if (allItems.Exists(i => i.ID == rankedItem.ID))
                    {
                        myPacket.Data[0] = new EquipRefineProto();
                        myPacket.Data[0].ItemCraftLevel = rankedItem.Plus;
                        myPacket.Data[0].ItemGuid = rankedItem.UID;
                        myPacket.Data[0].ItemId = (int)rankedItem.ID;
                        myPacket.Data[0].ItemPerfectionLevel = (int)rankedItem.PerfectionLevel;
                        myPacket.Data[0].ItemRank = Array.FindIndex(allItems.ToArray(), item => item.ID == rankedItem.ID) + 1;
                        myPacket.Data[0].Name = rankedItem.PerfectionOwner;
                        myPacket.Data[0].Position = currentPos;
                        myPacket.Data[0].PrestigePoints = rankedItem.Prestige;
                        myPacket.Data[0].SoulId = (int)(rankedItem.Purification.PurificationItemID > 0 ? rankedItem.Purification.PurificationItemID : 0);

                        _packet = FinalizeProtoBuf(myPacket);
                        client.Send(this);
                    }
                    break;
            }
        }

        private List<ConquerItem> FindAllPerfectedItems()
        {
            var results = new List<ConquerItem>();
            using (var cmd = new MySqlCommand(MySqlCommandType.SELECT))
            {
                cmd.Select("items");
                var reader = cmd.CreateReader();
                while (reader.Read())
                {
                    if (reader.ReadInt16("PerfectionLevel") > 0)
                    {
                        var item = ConquerItemTable.deserialzeItem(reader);
                        results.Add(item);
                    }
                }
            }
            return results;
        }

        private static byte[] FinalizeProtoBuf(EquipRefineRankProto coatStorageArgs)
        {
            using (var memoryStream = new MemoryStream())
            {
                Serializer.SerializeWithLengthPrefix(memoryStream, coatStorageArgs, PrefixStyle.Fixed32);
                var pkt = new byte[8 + memoryStream.Length];
                memoryStream.ToArray().CopyTo(pkt, 0);
                Writer.WriteUshort((ushort)memoryStream.Length, 0, pkt);
                Writer.WriteUshort(3256, 2, pkt);
                return pkt;
            }
        }
        public static implicit operator byte[] (CMsgEquipRefineRank effect)
        {
            return effect._packet;
        }
        public EquipRefineRankProto Data;
    }
    [ProtoContract]
    public class EquipRefineRankProto
    {
        public enum Action
        {
            RequestRanking = 0,
            RequestItemRanking = 1,
            UserItemRanking = 2,
            RequestItemInfo = 4
        }
        [ProtoMember(1, IsRequired = true)]
        public Action Type;
        [ProtoMember(2, IsRequired = true)]
        public int Count;
        [ProtoMember(3, IsRequired = true)]
        public int Page;
        /// <summary>
        /// Item guid or position
        /// </summary>
        [ProtoMember(4, IsRequired = true)]
        public uint ItemGOP;
        [ProtoMember(5, IsRequired = true)]
        public EquipRefineProto[] Data;
    }
    [ProtoContract]
    public class EquipRefineProto
    {
        public enum Positions
        {
            Headgear = 1,
            Necklace = 2,
            Armor = 3,
            Weapon = 4,
            Wing = 5,
            Ring = 6,
            Crop = 7,
            Boot = 8,
            Steed = 9,
            Fan = 10,
            Tower = 11,
        }
        [ProtoMember(1, IsRequired = true)]
        public int ItemRank;
        [ProtoMember(2, IsRequired = true)]
        public int dwParam;
        [ProtoMember(3, IsRequired = true)]
        public Positions Position;
        [ProtoMember(4, IsRequired = true)]
        public int PrestigePoints;
        [ProtoMember(5, IsRequired = true)]
        public uint ItemGuid;
        [ProtoMember(6, IsRequired = true)]
        public int ItemId;
        [ProtoMember(7, IsRequired = true)]
        public int SoulId;
        [ProtoMember(8, IsRequired = true)]
        public int ItemCraftLevel;
        [ProtoMember(9, IsRequired = true)]
        public int ItemPerfectionLevel;
        [ProtoMember(10, IsRequired = true)]
        public string Name;
    }
}
