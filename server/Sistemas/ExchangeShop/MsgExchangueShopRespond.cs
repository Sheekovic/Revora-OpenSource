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
    public class MsgExchangeShopRespond
    {
        private byte[] _packet;
        public MsgExchangeShopRespond(byte[] incoming)
        {
            int newLegnth = incoming.Length - 12;
            byte[] Incoming = new byte[newLegnth];
            Array.Copy(incoming, 4, Incoming, 0, Incoming.Length);
            _packet = Incoming;
        }
        public ExchangeShopRespondProto Data = null;
        public void Handle(GameState client)
        {
            uint[] ptr = Read7BitEncodedInt(_packet);
            int index = 0;
            Data = new ExchangeShopRespondProto();
            Data.ShopID = ptr[index];
            index++;
            Data.Action = ptr[index];
            index++;
            Data.Amount = ptr[index];
            index++;
            Data.ItemID = ptr[index];
            if (Data != null)
            {
                if (ExchangueShopFile.Items.ContainsKey(Data.ItemID))
                {
                    ExchangeItems Item = null;
                    ExchangueShopFile.Items.TryGetValue(Data.ItemID, out Item);
                    if (Item == null)
                        return;
                    if (Data.Amount > Item.Remaining)
                        return;
                    uint Count = Item.NeddedItemCount * Data.Amount;
                    uint PriceItem = Item.NeededItemID;
                    if (client.Inventory.Contains(PriceItem, Count))
                    {
                        if (client.Inventory.Remove(PriceItem, Count))
                        {
                            client.Inventory.Add(Item.ItemID, 0, (byte)Data.Amount, Item.Bound);
                        }
                    }
                }
            }
        }
        public static uint[] Read7BitEncodedInt(byte[] buffer)
        {
            List<uint> ptr2 = new List<uint>();

            for (int i = 0; i < buffer.Length;)
            {
                if (i + 2 <= buffer.Length)
                {
                    int tmp = buffer[i++];

                    if (tmp % 8 == 0)
                        while (true)
                        {
                            if (i + 1 > buffer.Length) break;
                            tmp = buffer[i++];
                            if (tmp < 128)
                            {
                                ptr2.Add((uint)tmp);
                                break;
                            }
                            else
                            {
                                int result = tmp & 0x7f;
                                if ((tmp = buffer[i++]) < 128)
                                {
                                    result |= tmp << 7;
                                    ptr2.Add((uint)result);
                                    break;
                                }
                                else
                                {
                                    result |= (tmp & 0x7f) << 7;
                                    if ((tmp = buffer[i++]) < 128)
                                    {
                                        result |= tmp << 14;
                                        ptr2.Add((uint)result);
                                        break;
                                    }
                                    else
                                    {
                                        result |= (tmp & 0x7f) << 14;
                                        if ((tmp = buffer[i++]) < 128)
                                        {
                                            result |= tmp << 21;
                                            ptr2.Add((uint)result);
                                            break;
                                        }
                                        else
                                        {
                                            result |= (tmp & 0x7f) << 21;
                                            result |= (tmp = buffer[i++]) << 28;
                                            ptr2.Add((uint)result);
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                }
                else break;
            }
            return ptr2.ToArray();
        }
    }
    public class ExchangeShopRespondProto
    {
        public uint ShopID;
        public uint Action;
        public uint Amount;
        public uint ItemID;
        public uint U1;
        public uint U2;
    }
}
