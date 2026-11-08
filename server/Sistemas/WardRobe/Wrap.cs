using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Revora.Database
{
    public class WrapTypeS
    {
        public static Dictionary<StorageType, StorageItems> Storage = new Dictionary<StorageType, StorageItems>();
        public static Dictionary<string, Wrap> Items = new Dictionary<string, Wrap>();
        public static void Load()
        {
            string File = "database\\WrapTypeData.ini";
            IniFile IniFile = new IniFile(File);
            var Count = IniFile.ReadUInt32("Wrap", "WrapCount");
            for (uint i = 0; i < Count; i++)
            {
                Wrap Wrap = new Wrap();
                Wrap.ID = IniFile.ReadUInt16(i.ToString(), "WrapID");
                Wrap.ItemID = IniFile.ReadUInt32(i.ToString(), "ItemType");
                Wrap.Type = (WrapType)IniFile.ReadByte(i.ToString(), "WrapType", 0);
                var item = Database.ConquerItemInformation.BaseInformations.Values.Where(p => p.ID == Wrap.ItemID).FirstOrDefault();
                if (item != null)
                {
                    if (!Items.ContainsKey(item.Name.ToLower()))
                    {
                        Items.Add(item.Name.ToLower(), Wrap);
                    }
                }
                else
                {
                    Console.WriteLine("[Wrap] Couldn't load item:" + Wrap.ItemID);
                }
            }
            LoadStorage();
        }
        public static void LoadStorage()
        {
            string File = "database\\Storage.ini";
            IniFile IniFile = new IniFile(File);
            var Count = IniFile.ReadUInt32("Storage", "StorageTypeCount");
            for (uint i = 1; i < Count + 1; i++)
            {

                StorageItems item = new StorageItems();
                item.Type = (StorageType)IniFile.ReadUInt16(i.ToString(), "Type");
                item.ID = IniFile.ReadUInt32(i.ToString() + "-1", "ID");
                Storage.Add(item.Type, item);
            }
        }
        public struct Wrap
        {
            public ushort ID;
            public WrapType Type;
            public uint ItemID;
        }
        public enum WrapType : byte
        {
            Garment = 1,
            Mount = 2
        }
        public struct StorageItems
        {
            public StorageType Type;
            public uint ID;
            public ulong Time;
        }
        public enum StorageType : uint
        {
            OverLord = 1,
            GrandMaster = 2018,
            RisingStar = 2001,
            Victor = 2002,
            Conqueror = 2003,
            Talent = 2004,
            Fashionist = 2005,
            SwiftChaser = 2006,
            Fairy = 2020,
            Goddess = 2021,
            Beauty = 2022,
            Scholar = 2023,
            Handsome = 2024,
            Wise = 2025,
            EarthKnight = 2028,
            Dubujianghu = 2027,
            Ouzhoubeicha = 2033,
            SkyKnight = 2029,
            GloryKnight = 2030,
            Paladin = 2031,
            SolarDragon = 4001,
            SuperMan = 2026,
            Ouzhoubeich = 2032,
            InfernalBeast = 6001,
            RadiantWings = 6002,
            StarlightWings = 6003,
            MoonlightWings = 6004,
            RareWings = 6009,
            SunWing = 6007,
            MoonWing = 6008,
            FairyWings = 6005
        }
    }

} 
