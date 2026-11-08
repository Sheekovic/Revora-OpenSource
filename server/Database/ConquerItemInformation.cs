using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;

namespace Revora.Database
{
    public unsafe class ConquerItemInformation
    {
        public static SafeDictionary<uint, ConquerItemBaseInformation> BaseInformations;
        public static SafeDictionary<string, SafeDictionary<uint, int>> GradeInformations2;
        public static SafeDictionary<uint, SafeDictionary<byte, ConquerItemPlusInformation>> PlusInformations;
        public static SafeDictionary<string, SafeDictionary<int, ConquerItemBaseInformation>> GradeInformations;
        public static void Load()
        {
            BaseInformations = new SafeDictionary<uint, ConquerItemBaseInformation>();
            PlusInformations = new SafeDictionary<uint, SafeDictionary<byte, ConquerItemPlusInformation>>();
            GradeInformations = new SafeDictionary<string, SafeDictionary<int, ConquerItemBaseInformation>>();
            GradeInformations2 = new SafeDictionary<string, SafeDictionary<uint, int>>();
            #region ItemType
            string[] baseText = File.ReadAllLines(Constants.ItemBaseInfosPath);
            List<string> itemRows = NormalizeItemtype(baseText);
            int count = 0;
            int gkey = 0;
            int lastlevel = 0;
            string lastgr = "";
            foreach (string line in itemRows)
            {
                count++;
                string _item_ = line.Trim();
                if (_item_.Length > 11)
                {
                    if (_item_.IndexOf("//", 0, 2) != 0)
                    {
                        ConquerItemBaseInformation CIBI = new ConquerItemBaseInformation();
                        try
                        {
                            CIBI.Parse(_item_);
                        }
                        catch (Exception)
                        {
                            Console.WriteLine("itemtype skip line " + count);
                            continue;
                        }
                        if (CIBI.Description == null)
                            CIBI.Description = "";
                        string name = CIBI.Description.ToLower();
                        var Grades = GradeInformations[CIBI.Description];
                        BaseInformations.Add(CIBI.ID, CIBI);
                        if (GradeInformations.ContainsKey(CIBI.Description) == false)
                        {
                            GradeInformations2.Add(CIBI.Description, new SafeDictionary<uint, int>());
                            GradeInformations2[CIBI.Description].Add((uint)(CIBI.ID / 10), 0);
                            lastlevel = CIBI.Level;
                            GradeInformations.Add(CIBI.Description, new SafeDictionary<int, ConquerItemBaseInformation>());
                            gkey = 0;
                        }
                        else
                        {
                            if (lastgr != CIBI.Description)
                                gkey = GradeInformations2[CIBI.Description].Count - 1;

                            if (GradeInformations2[CIBI.Description].ContainsKey(CIBI.ID / 10) && CIBI.Level == lastlevel)
                            {
                                CIBI.GradeKey = gkey;
                                continue;
                            }
                            else
                            {
                                GradeInformations2[CIBI.Description].Add((uint)(CIBI.ID / 10), 0);
                                lastlevel = CIBI.Level;
                                gkey = gkey + 1;
                            }
                        }
                        lastgr = CIBI.Description;
                        CIBI.GradeKey = gkey;
                        GradeInformations[CIBI.Description].Add(gkey, CIBI);
                    }
                }
            }
            Console.WriteLine("Item Type Loaded" + BaseInformations.Count());
            #endregion
            #region Itemtype2
            int gkey2 = 0;
            int lastlevel2 = 0;
            string lastgr2 = "";
            baseText = File.ReadAllLines(Constants.ItemBaseInfosPath2);
            foreach (string line in baseText)
            {
                count++;
                string _item_ = line.Trim();
                if (_item_.Length > 11)
                {
                    if (_item_.IndexOf("//", 0, 2) != 0)
                    {
                        ConquerItemBaseInformation CIBI = new ConquerItemBaseInformation();
                        CIBI.Parse(_item_);

                        var Grades = GradeInformations[CIBI.Description];
                        BaseInformations.Add(CIBI.ID, CIBI);
                        if (GradeInformations.ContainsKey(CIBI.Description) == false)
                        {
                            GradeInformations2.Add(CIBI.Description, new SafeDictionary<uint, int>());
                            GradeInformations2[CIBI.Description].Add((uint)(CIBI.ID / 10), 0);
                            lastlevel2 = CIBI.Level;
                            GradeInformations.Add(CIBI.Description, new SafeDictionary<int, ConquerItemBaseInformation>());
                            gkey2 = 0;
                        }
                        else
                        {
                            if (lastgr2 != CIBI.Description)
                                gkey2 = GradeInformations2[CIBI.Description].Count - 1;

                            if (GradeInformations2[CIBI.Description].ContainsKey(CIBI.ID / 10) && CIBI.Level == lastlevel2)
                            {
                                CIBI.GradeKey = gkey2;
                                continue;
                            }
                            else
                            {
                                GradeInformations2[CIBI.Description].Add((uint)(CIBI.ID / 10), 0);
                                lastlevel2 = CIBI.Level;
                                gkey2 = gkey2 + 1;
                            }
                        }
                        lastgr2 = CIBI.Description;
                        CIBI.GradeKey = gkey2;
                        GradeInformations[CIBI.Description].Add(gkey2, CIBI);
                    }
                }
            }
            GradeInformations2.Clear();
            Console.WriteLine("Item Sub Loaded" + BaseInformations.Count());

            #endregion
            #region ItemPlus
            baseText = File.ReadAllLines(Constants.ItemPlusInfosPath);
            foreach (string line in baseText)
            {
                try
                {
                    string _item_ = line.Trim();
                    ConquerItemPlusInformation CIPI = new ConquerItemPlusInformation();
                    CIPI.Parse(_item_);
                    SafeDictionary<byte, ConquerItemPlusInformation> info = null;
                    if (PlusInformations.TryGetValue(CIPI.ID, out info))
                    {
                        info.Add(CIPI.Plus, CIPI);
                    }
                    else
                    {
                        PlusInformations.Add(CIPI.ID, new SafeDictionary<byte, ConquerItemPlusInformation>());
                        if (PlusInformations.TryGetValue(CIPI.ID, out info))
                        {
                            info.Add(CIPI.Plus, CIPI);
                        }
                    }
                }
                catch (Exception ex) { Console.WriteLine(ex); }
            }
            Console.WriteLine("Item Base and Plus information loaded.");
            #endregion
        }

        private static List<string> NormalizeItemtype(string[] raw)
        {
            List<string> rows = new List<string>(raw.Length);
            string pending = null;
            for (int i = 0; i < raw.Length; i++)
            {
                string line = raw[i] == null ? "" : raw[i].Trim();
                if (pending != null)
                {
                    pending += line;
                    if (ItemtypeRowReady(pending))
                    {
                        rows.Add(CollapseItemtypeName(pending));
                        pending = null;
                    }
                    continue;
                }
                if (line.Length >= 2 && line[0] == '/' && line[1] == '/')
                    continue;
                if (StartsWithItemId(line) && !ItemtypeRowReady(line))
                {
                    pending = line;
                    continue;
                }
                if (!StartsWithItemId(line))
                    continue;
                if (line.Length <= 11)
                    continue;
                rows.Add(CollapseItemtypeName(line));
            }
            if (pending != null && ItemtypeRowReady(pending))
                rows.Add(CollapseItemtypeName(pending));
            return rows;
        }

        private static bool StartsWithItemId(string line)
        {
            int i = 0;
            while (i < line.Length && char.IsDigit(line[i]))
                i++;
            return i > 0 && i + 1 < line.Length && line[i] == '@' && line[i + 1] == '@';
        }

        private static bool ItemtypeRowReady(string line)
        {
            if (!StartsWithItemId(line))
                return false;
            string[] parts = line.Split(new string[] { "@@" }, StringSplitOptions.None);
            return parts.Length >= 20;
        }

        private static string CollapseItemtypeName(string line)
        {
            string[] parts = line.Split(new string[] { "@@" }, StringSplitOptions.None);
            if (parts.Length < 5)
                return line;
            uint id;
            if (!uint.TryParse(parts[0], out id))
                return line;
            int stats = -1;
            for (int i = 2; i < parts.Length - 1; i++)
            {
                int a;
                int b;
                if (int.TryParse(parts[i], out a) && int.TryParse(parts[i + 1], out b))
                {
                    stats = i;
                    break;
                }
            }
            if (stats <= 2)
                return line;
            StringBuilder name = new StringBuilder();
            for (int i = 1; i < stats; i++)
            {
                if (i > 1)
                    name.Append(' ');
                name.Append(parts[i]);
            }
            StringBuilder rebuilt = new StringBuilder();
            rebuilt.Append(parts[0]).Append("@@").Append(name.ToString());
            for (int i = stats; i < parts.Length; i++)
                rebuilt.Append("@@").Append(parts[i]);
            return rebuilt.ToString();
        }

        public static List<uint> unabletradeitem = new List<uint>()
        {
            750000,
            722700,
            3600025,
            3200000,
            729549,
            3008994,
            3200005,
            3600029,
            3007108,
            3007109,
            3007110,
            3200004,
            3600031,
            3600027,
            3600024,
            2100075,
            2100095
        };
        public uint CalculateUplevel()
        {
            var item = BaseInformations.Values.Where(x => x.Level > this.BaseInformation.Level && (x.ID / 1000) == (this.BaseInformation.ID / 1000) && (x.ID % 10) == (this.BaseInformation.ID % 10)).OrderBy(y => y.Level).FirstOrDefault();
            if (item == null)
                return BaseInformation.ID;
            else
                return item.ID;
        }
        public uint LowestID(byte Level)
        {
            var grades = GradeInformations[this.BaseInformation.Description];
            if (grades == null) return BaseInformation.ID;
            for (byte gr = 0; gr < grades.Count; gr++)
                if (grades[gr].Level == Level)
                    return (uint)((grades[gr + 1].ID / 10) * 10 + BaseInformation.ID % 10);
            return (uint)((grades[0].ID / 10) * 10 + BaseInformation.ID % 10);
        }
        public ConquerItemInformation(uint ID, byte Plus)
        {
            _BaseInformation = null;
            if (BaseInformations.TryGetValue(ID, out _BaseInformation))
            {
                getPlusInformation(ID, Plus);
            }
            else
            {
                return;
            }
        }
        public static ItemType GetItemType(uint type)
        {
            switch ((type % 10000000) / 100000)
            {
                case 3:
                case 4:
                case 5:
                case 6:
                    {
                        return (ItemType)((type % 100000) / 1000 * 1000);
                    }
                case 7:
                case 10:
                    {
                        return (ItemType)(((type % 100000) / 10000) * 10000);
                    }
            }
            return ItemType.Invalid;
        }
        public enum ItemSort
        {
            Invalid = -1,
            Expendable = 0,
            Helmet = 1,
            Necklace = 2,
            Armor = 3,
            Weapon1 = 4,
            Weapon2 = 5,
            Shield = 6,
            RingR = 7,
            Shoes = 8,
            Other = 9,
            RingL = 10,
            Overcoat = 11,
            IncreaseDmgArtifact = 12,
            DecreaseDmgArtifact = 13,
            MountAndAccessory = 14
        }
        public static ItemSort GetItemSort(uint type)
        {
            switch ((type % 10000000) / 100000)
            {
                case 1:
                    {
                        switch ((type % 1000000) / 10000)
                        {
                            case 11: return ItemSort.Helmet;
                            case 12: return ItemSort.Necklace;
                            case 13: return ItemSort.Armor;
                            case 14: return ItemSort.Helmet;
                            case 15: return ItemSort.RingR;
                            case 16: return ItemSort.Shoes;
                            case 17: break;
                            case 18: return ItemSort.Overcoat;
                            case 19: return ItemSort.Overcoat;
                        }
                        break;
                    }
                case 2:
                    {
                        switch ((type % 10000) / 1000)
                        {
                            case 1: return ItemSort.IncreaseDmgArtifact;
                            case 2: return ItemSort.DecreaseDmgArtifact;
                        }
                        break;
                    }
                case 3: return ItemSort.MountAndAccessory;
                case 4: return ItemSort.Weapon1;
                case 5: return ItemSort.Weapon2;
                case 6: return ItemSort.Weapon1;
                case 7: return ItemSort.Other;
                case 9: return ItemSort.Shield;
                case 10: return ItemSort.Expendable;
                default:
                    {
                        break;
                    }
            }

            var sort = ((type % 10000000) / 100000);
            if (sort >= 20 && sort < 30) return ItemSort.RingL;

            return ItemSort.Invalid;
        }
        private void getPlusInformation(uint ID, byte Plus)
        {
            var itemtype = ID;
            var type = GetItemType(ID);
            var sort = GetItemSort(ID);
            if (type == ItemType.Backsword || type == ItemType.Bow)
            {
            }
            else if (sort == ItemSort.Weapon1)
            {
                itemtype = (itemtype / 100000) * 100000 + (itemtype % 1000) + 44000;
            }
            else if (sort == ItemSort.Weapon2)
            {
                itemtype = (itemtype / 100000) * 100000 + (itemtype % 1000) + 55000;
            }
            else if (sort == ItemSort.Helmet || sort == ItemSort.Armor || sort == ItemSort.Shield)
            {
                itemtype = (itemtype / 1000) * 1000 + (itemtype % 1000);
            }

            itemtype = itemtype / 10 * 10;
            ID = (uint)(
                             ID - (ID % 10) // [5] = 0
                         );
            uint orID = ID;
            byte itemType = (byte)(ID / 10000);
            ushort itemType2 = (ushort)(ID / 1000);
            if (itemType == 14 && itemType2 != 143 && itemType2 != 142 && itemType2 != 141)//armors
            {
                ID = (uint)(
                            (((uint)(ID / 1000)) * 1000) + // [3] = 0
                            ((ID % 100))
                        );
            }
            else if (itemType2 == 141 || itemType2 == 142 || itemType2 == 143 || itemType == 13 || itemType == 11 || itemType2 == 123 || itemType == 30 || itemType == 20 || itemType == 12 || itemType == 15 || itemType == 16 || itemType == 50 || itemType2 == 421 || itemType2 == 601 || itemType2 == 610 || itemType == 90)//Necky bow bag
            {
                ID = (uint)(
                            ID - (ID % 10) // [5] = 0
                        );
            }
            else
            {
                byte head = (byte)(ID / 100000);
                ID = (uint)(
                        ((head * 100000) + (head * 10000) + (head * 1000)) + // [1] = [0], [2] = [0]
                        ((ID % 1000) - (ID % 10)) // [5] = 0
                    );
            }

            _PlusInformation = new ConquerItemPlusInformation();
            if (Plus > 0)
            {
                SafeDictionary<byte, ConquerItemPlusInformation> pInfo = null;
                if (!PlusInformations.TryGetValue(itemtype, out pInfo)) if (!PlusInformations.TryGetValue(ID, out pInfo)) PlusInformations.TryGetValue(orID, out pInfo);
                if (pInfo == null)
                {
                    return;
                }
                if (!pInfo.TryGetValue(Plus, out _PlusInformation))
                {
                    _PlusInformation = new ConquerItemPlusInformation();
                }
            }
        }
        private ConquerItemPlusInformation _PlusInformation;
        private ConquerItemBaseInformation _BaseInformation;
        public ConquerItemPlusInformation PlusInformation
        {
            get
            {
                if (_PlusInformation == null)
                    return new ConquerItemPlusInformation();
                return _PlusInformation;
            }
        }
        public enum ItemType
        {
            Invalid = -1,

            // Weapon1
            Blade = 10000,
            Sword = 20000,
            Backsword = 21000,
            Hook = 30000,
            Whip = 40000,
            Axe = 50000,
            Hammer = 60000,
            Club = 80000,
            Scepter = 81000,
            Dagger = 90000,

            // Weapon2
            Bow = 00000,
            Glaive = 10000,
            Poleaxe = 30000,
            Longhammer = 40000,
            Spear = 60000,
            Wand = 61000,
            Pickaxe = 62000,
            Halbert = 80000,

            // Other
            Gem = 00000,
            TaskItem = 10000,
            ActionItem = 20000,
            ComposeItem = 30000,
            MonsterItem = 50000,
            PointCard = 80000,
            DarkHorn = 90000,

            // Expendable
            Physic = 00000,
            PhysicMana = 01000,
            PhysicLife = 02000,
            Arrow = 50000,
            Spell = 60000,
            Ore = 70000,
            Special = 80000,
            Silver = 90000,

            // MountAndAccessory
            Mount = 00000,
            Weapon2Accessory = 50000,
            Weapon1Accessory = 60000,
            BowAccessory = 70000,
            ShieldAccessory = 80000,
        }
        public ConquerItemBaseInformation BaseInformation
        {
            get
            {
                if (_BaseInformation == null)
                    return new ConquerItemBaseInformation();
                return _BaseInformation;
            }
        }
    }
    public unsafe class ConquerItemPlusInformation
    {
        public uint
        ID,
        MinAttack,
        MaxAttack;
        public byte
        Plus,
        Dodge;
        public ushort
        ItemHP,
        Agility,
        MagicAttack,
        MagicDefence,
        PhysicalDefence;
        public void Parse(string Line)
        {
            string[] Info = Line.Split(' ');
            ID = uint.Parse(Info[0]);
            Plus = byte.Parse(Info[1]);
            ItemHP = ushort.Parse(Info[2]);
            MinAttack = uint.Parse(Info[3]);
            MaxAttack = uint.Parse(Info[4]);
            PhysicalDefence = ushort.Parse(Info[5]);
            MagicAttack = ushort.Parse(Info[6]);
            MagicDefence = ushort.Parse(Info[7]);
            Agility = ushort.Parse(Info[8]);
            Dodge = byte.Parse(Info[9]);
        }
        public ConquerItemPlusInformation()
        {
            ID = MinAttack = MaxAttack = 0;
            Plus = Dodge = 0;
            PhysicalDefence = MagicAttack = MagicDefence = Agility = 0;
        }
    }
    public unsafe class ConquerItemBaseInformation
    {
        private byte postion;
        public ushort
        ItemHP,
        ItemMP,
        Agility,
        Strength,
        defence1,
        defence2,
        StackSize,
        MinAttack,
        MaxAttack,
        Durability,
        Breaktrough,
        AgilityItem,
        AttackRange,
        MagicAttack,
        MagicDefence,
        PhysicalDefence,
        PurificationMeteorNeed;

        public byte Frequency { get; private set; }

        public uint subtype, Dodge;
        public uint Weight;
        public string
        Name,
        LoweredName,
        LowerName,
        Description,
        FinalDescription;
        public byte
        Class,
        Level,

        Gender,
        Proficiency;
        public uint
        ID,
        GoldWorth,
        PurificationLevel,
        ConquerPointsWorth;

        public uint TimeTillRemovInMinutes { get; private set; }

        public int GradeKey;
        public UInt16
        Block,
        Immunity,
        FireResist,
        WoodResist,
        WaterResist,
        EarthResist,
        MetalResist,
        Penetration,
        Detoxication,
        BreakThrough,
        Counteraction,
        CriticalStrike,
        SkillCStrike;

        public ushort CounterAction { get; private set; }
        public ushort PhysicalDamageIncrease { get; private set; }
        public ushort MagicDamageIncrease { get; private set; }
        public ushort PhysicalDamageDecrease { get; private set; }
        public ushort MagicDamageDecrease { get; private set; }

        public ItemType Type;
        public enum ItemType : byte
        {
            Dropable = 0,
            Others
        }
        public void Parse(string Line)
        {
            string[] data = Line.Split(new string[] { "@@" }, StringSplitOptions.RemoveEmptyEntries);
            ID = Convert.ToUInt32(data[0]);
            Name = data[1].Trim();
            LoweredName = Name.ToLower();
            Class = Convert.ToByte(data[2]);
            Proficiency = Convert.ToByte(data[3]);
            Level = Convert.ToByte(data[4]);
            Gender = Convert.ToByte(data[5]);
            Strength = Convert.ToUInt16(data[6]);
            Agility = Convert.ToUInt16(data[7]);
            Type = Convert.ToUInt32(data[10]) == 0 ? ItemType.Dropable : ItemType.Others;
            GoldWorth = Convert.ToUInt32(data[11]);
            MaxAttack = Convert.ToUInt16(data[13]);
            MinAttack = Convert.ToUInt16(data[14]);
            PhysicalDefence = Convert.ToUInt16(data[15]);
            Frequency = (byte)Convert.ToUInt16(data[16]);
            Dodge = Convert.ToUInt16(data[17]);
            ItemHP = Convert.ToUInt16(data[18]);
            ItemMP = Convert.ToUInt16(data[19]);
            Durability = Convert.ToUInt16(data[21]);
            subtype = Convert.ToUInt32(data[25]);
            postion = Convert.ToByte(data[26]);
            MagicAttack = Convert.ToUInt16(data[29]);
            MagicDefence = Convert.ToUInt16(data[30]);
            AttackRange = Convert.ToUInt16(data[31]);
            ConquerPointsWorth = Convert.ToUInt32(data[36]);
            TimeTillRemovInMinutes = Convert.ToUInt32(data[38]);
            CriticalStrike = Convert.ToUInt16(data[39]);
            SkillCStrike = Convert.ToUInt16(data[40]);
            Immunity = Convert.ToUInt16(data[41]);
            Penetration = Convert.ToUInt16(data[42]);
            Block = Convert.ToUInt16(data[43]);
            //Detoxication = Convert.ToUInt16(data[43]);
            BreakThrough = Convert.ToUInt16(data[44]);
            CounterAction = Convert.ToUInt16(data[45]);
            StackSize = Convert.ToUInt16(data[46]);
            WoodResist = Convert.ToUInt16(data[48]);
            WaterResist = Convert.ToUInt16(data[49]);
            FireResist = Convert.ToUInt16(data[50]);
            try
            {
                EarthResist = Convert.ToUInt16(data[51]);
            }
            catch
            {

            }
            Description = data[52].Replace("`s", "");
            if (Description == "NinjaKatana")
                Description = "NinjaWeapon";
            if (Description == "Earrings")
                Description = "Earring";
            if (Description == "Bow")
                Description = "ArcherBow";
            if (Description == "Backsword")
                Description = "TaoistBackSword";
            uint Typo = (ID / 1000);
            if (Typo == 614)
                Description = "TrojanEpic";
            if (Typo == 616)
                Description = "NinjaEpic";
            if (Typo == 622)
                Description = "MonkEpic";
            if (Typo == 670 || Typo == 671)
                Description = "PirateEpic";
            Description = Description.ToLower();
            if (data.Length >= 57)
            {
                PurificationLevel = ParseU16(data, 55);
                PurificationMeteorNeed = ParseU16(data, 56);
            }
            if (data.Length > 63)
            {
                PhysicalDamageIncrease = ParseU16(data, 60);
                MagicDamageIncrease = ParseU16(data, 61);
                PhysicalDamageDecrease = ParseU16(data, 62);
                MagicDamageDecrease = ParseU16(data, 63);
            }
        }

        private static ushort ParseU16(string[] data, int index)
        {
            if (data == null || index < 0 || index >= data.Length)
                return 0;
            ushort value;
            if (ushort.TryParse(data[index], out value))
                return value;
            return 0;
        }
    }
}
