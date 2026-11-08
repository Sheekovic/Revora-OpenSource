using System;
using System.Collections.Generic;
using Revora.Network.GamePackets;
using System.Text;

namespace Revora
{
    public unsafe class Constants
    {
        public static readonly Message
       ArrowsReloaded = new Message("arrows changed.", Message.System),
        TradeRequest = new Message("Trade Request sent.", Message.System),
        Teleportout = new Message("Unable to Transport You", Message.System),
        Dropitemout = new Message("Cannot drop article here!", Message.System),
        PKForbidden = new Message("PK Prohibited on this map.", Message.System),
        vipteleport = new Message("you cannot teleport on this map.", Message.System),
        JailItemUnusable = new Message("You cannot use this article here!", Message.System),
        FullInventory = new Message("Your inventory is Full.", Message.System),
        SpellLeveled = new Message("Congratulations, you just uploaded a Skill.", Message.System),
        ExpBallsUsed = new Message("You can use only ten exp balls a day. Try tomorrow.", Message.System),
        TradeInProgress = new Message("A trade that is already in progress. Try again later.", Message.System),
        TradeInventoryFull = new Message("There is not enough space in your even inventory", Message.System),
        ProficiencyLeveled = new Message("Congratulations, you have uploaded the weapon requirement.", Message.System),
        VIPExpired = new Message("Your type of VIP has expired. Please reactivate your service, so you can enjoy its benefits.", System.Drawing.Color.Red, Message.World),
        VIPLifetime = new Message("Your VIP is not Limited.", System.Drawing.Color.Red, Message.World),
        FloorItemNotAvailable = new Message("You can pick up other loot players at the moment. Please wait.", Message.System);
        public static Message VIPRemaining(string days, string hours)
        {
            return new Message("Your VIP has a time of" + days + "Days (s) and" + hours + "hours (s)", System.Drawing.Color.Red, Message.World);
        }
        public static Message NoDrugs(int time)
        {
            return new Message("Star Poison activated. You will not be able to use medicine for" + time + "seconds.", Message.System);
        }
        public static Message Accuracy(int time)
        {
            return new Message("Accuracy activated. Your agility will be increased a bit for" + time + ".", Message.System);
        }
        public static Message DummyLevelTooHigh()
        {
            return new Message("You cannot attack this dummy because its level is not high enough.", Message.System);
        }
        public static Message NoArrows(string name)
        {
            return new Message("Cannot reload arrows, you are out of" + name + "s!", Message.System);
        }
        public static Message Invisibility(int time)
        {
            return new Message("Invisibility activated. You will be invisible to monsters, as long as you don't attack for" + time + ".", Message.System);
        }
        public static Message PickupGold(uint amount)
        {
            return new Message("You picked up " + amount + " silver.", Message.System);
        }
        public static Message PickupItem(string name)
        {
            return new Message("You picked up " + name + ".", Message.System);
        }
        public static Message Dodge(float percent, int time)
        {
            return new Message("Evasion activated. Your dodge will be increased with" + percent + "by" + time + ".", Message.System);
        }
        public static Message Shield(float percent, int time)
        {
            return new Message("Shield activate. Your defense will be increased with" + percent + "by" + time + ".", Message.System);
        }
        public static Message TeamExperience(uint experience)
        {
            return new Message("One of his teammates killed a monster for what you gained" + experience + "experience.", Message.System);
        }
        public static Message Stigma(float percent, int time)
        {
            return new Message("Stigma active. Your attack will be increased with" + percent + "for" + time + ".", Message.System);
        }
        public static Message ExtraExperience(uint experience)
        {
            return new Message("Get additional EXP" + experience + ".", Message.System);
        }
        public static Message PickupConquerPoints(uint amount)
        {
            return new Message("You picked up " + amount + " CPs.", Message.System);
        }
        public static Message PickupBoundCps(uint amount)
        {
            return new Message("Collect" + amount + "Free Cps.", Message.System);
        }
        public static Message Enchant(int origEnch, int newEnch)
        {
            if (newEnch <= origEnch)
            {
                return new Message("You had bad luck. You don't gain more charm in your article. Your enchantment generated is" + newEnch + ".", Message.System);
            }
            else
            {
                return new Message("You have been lucky. You gained more charm in your article. Your generated enchantment is" + newEnch + ".", Message.System);
            }
        }
        public static Message NoobTeamExperience(uint experience)
        {
            return new Message("One of his teammates killed a monster and because you have a rookie inside his team, he gained" + experience + "experience.", Message.System);
        }
        public static Message BoothItemSell(string buyername, string itemname, bool conquerpoints, ulong cost)
        {
            return new Message("Congratulations. You have just sold" + itemname + "to" + buyername + "for" + cost + (conquerpoints ? "CPs." : "Gold."), Message.System);
        }


        public const string
        ServerKey = "TQServer",
        DMapsPath = "database\\",
        DataHolderPath = "database\\",
        PortalsPath = "database\\Portals.ini",
        ShopsPath = "database\\shops\\Shop.dat",
        ExchangeShopsPath = "database\\shops\\exchange_shop_goods.txt",
        MonstersPath = "database\\Monsters.txt",
        rune_levexp = "database\\rune_levexp.txt",
        hairface_storage_type = "database\\hairface_storage_type.txt",
        ItemBaseInfosPath = "database\\itemtype-new.txt",
        ItemBaseInfosPath2 = "database\\ItemtypeSub.txt",
        RevivePoints = "database\\RevivePoints.ini",
        ItemPlusInfosPath = "database\\ItemsPlus.ini",
        QuizShow = "database\\Slayer\\QuizShow.txt",
        EShopsPath = "database\\shops\\emoneyshop.ini",
        HonorShopPath = "database\\shops\\HonorShop.ini",
        WelcomeMessages = "database\\WelcomeMessages.txt",
        EShopsV2Path = "database\\shops\\emoneyshopV2.ini",
        UnhandledExceptionsPath = "database\\exceptions\\",
        RaceShopPath = "database\\shops\\SlayerPointshop.ini",
        RewardPath = "database\\Slayer\\operating_prize.txt",
        ChampionShopPath = "database\\shops\\GoldenLeagueShop.ini";

        static byte[] Key = new byte[] { 67, 50, 51, 56, 120, 115, 54, 53, 112, 106, 121, 55, 72, 85, 57, 81 }; // 6711 C238xs65pjy7HU9Q
        //static byte[] Key = new byte[] { 66, 50, 50, 97, 113, 110, 122, 98, 52, 49, 99, 117, 77, 101, 109, 97 }; //Jheisson
        //static byte[] Key = new byte[] { 66, 50, 50, 98, 113, 110, 102, 98, 52, 49, 99, 117, 77, 101, 109, 97 }; //Luis


        public static string GameCryptographyKey = Encoding.Default.GetString(Key);

        public static string ServerName = "Revora";
        public const int MaxBroadcasts = 50;
        public static string[] ItemDropQualityRates;
        public const sbyte
        nScreenDistance = 19,
        pScreenDistance = 19,
        remScreenDistance = 19,
        BombScreenDistanc = 19;
        public const ushort
        FloorItemSeconds = 20,
        SocketOneProgress = 100,
        SocketTwoProgress = 300,
        FloorItemAvailableAfter = 15,
        DisconnectTimeOutSeconds = 10;
        public static uint MoneyDropRate
        {
            get { return Config.MoneyDropRate; }
            set { Config.MoneyDropRate = value; }
        }
        public static uint ExtraSpellRate
        {
            get { return Config.EffectiveSpellExperienceRate; }
            set { Config.SpellExperienceRate = value; }
        }
        public static uint MoneyDropMultiple
        {
            get { return Config.MoneyDropMultiple; }
            set { Config.MoneyDropMultiple = value; }
        }
        public static uint ExtraExperienceRate
        {
            get { return Config.EffectiveExperienceRate; }
            set { Config.ExperienceRate = value; }
        }
        public static uint ExtraProficiencyRate
        {
            get { return Config.EffectiveProficiencyExperienceRate; }
            set { Config.ProficiencyExperienceRate = value; }
        }

        public static readonly List<ushort> PKForbiddenMaps = new List<ushort>()
        {
            // jheisson no-PK map
            601,700,1036,1002,1039,1004,1006,8880,10332,4021,4022,40204,4020,
            8881,1950,8800,8801,8802,8803,3055,1858,1037,2072,10363
        };
        public static readonly System.Collections.Generic.List<ulong> ZonaTroyanos = new System.Collections.Generic.List<ulong>() 
        { 
            4573
        };
        public static readonly System.Collections.Generic.List<int> MaxItems = new System.Collections.Generic.List<int>()
        {
            410439,
            420439, 
            480439,
            610439,
            601439, 
            421439,
            823052,
            824001,
            823043,
            822052,
            800014,
            800017,
            800513,
            822053,
            820056,
            800110,
            800320
        };
        public static readonly System.Collections.Generic.List<ulong> steedguard = new System.Collections.Generic.List<ulong>()
        {
            3333,
            3334,
            3335,
            1004,
            50,
            60,
            2072,
            2071

        };
        public static readonly System.Collections.Generic.List<ulong> ZonaNinjas = new System.Collections.Generic.List<ulong>() 
        { 
            4574
        };
        public static readonly System.Collections.Generic.List<ulong> ZonaMonjes = new System.Collections.Generic.List<ulong>() 
        { 
            4575
        };
        public static readonly List<ushort> PKFreeMaps = new List<ushort>()
        {
            // jheisson arena maps
            4025,1763,3071,1812,1038,1005,6000,6001,6002,6003,6004,8883,1801,50,60,128,4444,132,8877,146,147,148,151,150,152,149,1354,
            1508,1518,8877,3333,3334,3335,1090,5000,2071,1730,1731,1732,1733,1734,1735,
            1736,2057,1559,1509,6412,14785,1707,12345,12346,1234,3846,10360,9972,1037,2072,16414,
            700
        };
        public static readonly List<int> AvaibleSpells = new List<int>()
        {
            1380,1385,1390,1395,1360,1400,1405,1410,1046,
            1045,5030,7001,7002,7003,7020,1415,1418,1416,1417,1419
        };
    }
}