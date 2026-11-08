using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace COServer.Database
{
    public class StorageItem
    {
        public uint ID;
        public uint Unknown;
        public uint Stars;
        public byte Type;
        public static Dictionary<uint, StorageItem> Garments = new Dictionary<uint, StorageItem>();
        public static Dictionary<uint, StorageItem> Mounts = new Dictionary<uint, StorageItem>();
        public static void Load()
        {
            string[] baseText = System.IO.File.ReadAllLines("database\\coat_storage_type.txt");
            foreach (var bas_line in baseText)
            {
                var line = bas_line.Split(' ');
                StorageItem item = new StorageItem();
                item.ID = uint.Parse(line[0]);
                item.Unknown = uint.Parse(line[1]);
                item.Stars = uint.Parse(line[7]);
                item.Type = byte.Parse(line[2]);
                if (item.Type == 1)
                    Garments.Add(item.ID, item);
                else
                    Mounts.Add(item.ID, item);
            }
        }
        public static uint AmountStarGarments(COServer.Client.GameState client, byte Star)
        {
            uint Count = 0;
            foreach (var Garment in client.Player.StorageItems.Values)
            {
                StorageItem item = new StorageItem();
                if (item.Type != 1)
                    continue;
                if (Garments.TryGetValue(Garment.ID, out item))
                {
                    if (item.Stars >= Star)
                        Count++;
                }
            }
            return Count;
        }
        public static uint AmountStarMounts(COServer.Client.GameState client, byte Star)
        {
            uint Count = 0;
            foreach (var Mount in client.Player.StorageItems.Values)
            {
                StorageItem item = new StorageItem();
                if (item.Type != 2)
                    continue;
                if (Garments.TryGetValue(Mount.ID, out item))
                {
                    if (item.Stars >= Star)
                        Count++;
                }
            }
            return Count;
        }
        public static uint PerfectionPoints(Client.GameState client, bool Garment)
        {
            uint Count = 0;
            foreach (var item2 in client.Player.StorageItems.Values)
            {
                StorageItem item;
                if (Garment)
                {
                    if (Garments.TryGetValue(item2.ID, out item))
                    {
                        if (item.Type != 1)
                            continue;
                        Count += 50;
                    }
                }
                else
                {
                    if (Mounts.TryGetValue(item2.ID, out item))
                    {
                        if (item.Type != 2)
                            continue;
                        Count += 50;
                    }
                }
            }
            return Count;
        }
    }
}