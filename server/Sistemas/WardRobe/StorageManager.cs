using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Revora.Network.GamePackets;
using Npgsql;
namespace Revora
{
    public sealed class StorageManager
    {
        // Numeric interoperability facts from the installed 6711 title_type.dat.
        private static readonly Dictionary<int,int> Points = new Dictionary<int,int> {
            {11000, 150}, // Overlord
            {90001, 500}, // Chosen One
            {20010002, 300}, // RisingStar
            {20020003, 150}, // Victor
            {20030004, 300}, // Conqueror
            {20040005, 150}, // Talent
            {20050006, 150}, // Fashionist
            {20060007, 150}, // SwiftChaser
            {20130014, 50}, // MonkeyRider
            {20140015, 150}, // SolarRider
            {20150016, 150}, // LunarRider
            {20160017, 300}, // SaintRider
            {20180001, 500}, // Grandmaster
            {20202020, 300}, // Fairy
            {20212021, 200}, // Goddess
            {20222022, 100}, // Beauty
            {20232023, 300}, // Scholar
            {20242024, 200}, // Handsome
            {20252025, 100}, // Wise
            {20262026, 500}, // Superman
            {20272027, 300}, // Scholar
            {20282028, 50}, // EarthKnight
            {20292029, 50}, // GloryKnight
            {20302030, 50}, // SkyKnight
            {20312031, 50}, // Paladin
            {20322032, 150}, // BigFan
            {20332033, 300}, // EuroCollector
            {20342034, 500}, // Invincible
            {20352035, 500}, // Legendary
            {20362036, 450}, // Peerless
            {20372037, 400}, // Outstanding
            {20382038, 300}, // Expert
            {20402040, 300}, // PeerlessBeauty
            {20412041, 300}, // SuperIdol
            {20442044, 100}, // MostRadiant
            {20452045, 100}, // MostAttractive
            {20462046, 500}, // PokerKing
            {20472047, 450}, // PokerLord
            {20482048, 400}, // PokerMaster
            {20492049, 200}, // PokerStar
            {20502050, 50}, // ImmortalFate
            {20512051, 150}, // SuccessortoLege
            {20522052, 300}, // FashionIcon
            {20532053, 200}, // FashionIdol
            {20542054, 100}, // FashionMaster
            {20562056, 150}, // SlamDunkLeader
            {20572057, 30}, // HonoredHero
            {20592059, 500}, // KingTeam
            {20602060, 450}, // DominatorTeam
            {20612061, 400}, // PowerTeam
            {20622062, 300}, // EliteTeam
            {20632063, 50}, // LoveJoker
            {20642064, 150}, // SweetFairy
            {20652065, 150}, // Love`sMadness
            {40011000, 0}, // WingsofSolarDra
            {60010020, 0}, // WingsofInfernal
            {60020021, 0}, // RadiantWings
            {60030022, 0}, // StarlightWings
            {60040023, 0}, // MoonlightWings
            {60056005, 0}, // FairyWings
            {60076007, 0}, // VioletCloudWing
            {60086008, 0}, // VioletLightning
            {60096009, 0}, // WingsofPlanet
            {60116011, 500}, // Supreme
            {60126012, 0}, // WingsofRomance
            {60136013, 0}, // EmeraldGlowWing
            {60146014, 0}, // OrangeGlowWings
            {60156015, 0}, // FlameGlowWings
            {60166016, 0}, // BrightGlowWings
            {60176017, 0}, // SirenSong
            {60186018, 0}, // WingsofHeart
            {20392039, 0} // Storage.ini unlock; title_type.dat has no point entry.
        };
        public static ConcurrentDictionary<uint,WardrobeTitles> Data = new ConcurrentDictionary<uint,WardrobeTitles>();
        public static bool Known(int type,int id) { return type>0 && type<=short.MaxValue && id>0 && id<10000 && Points.ContainsKey(type*10000+id); }
        public static T Wing<T>(int type,int id) { return Value<T>(type,id,true); }
        public static T Title<T>(int type,int id) { return Value<T>(type,id,false); }
        private static T Value<T>(int type,int id,bool wing)
        {
            bool valid=Known(type,id) && (type>=4000)==wing;
            if(typeof(T)==typeof(bool)) return (T)(object)valid;
            if(typeof(T)==typeof(int)) return (T)(object)(valid?type*10000+id:0);
            throw new ArgumentException("Unsupported wardrobe value type.");
        }
        public static int GetTitlePoints(short type,short id) { int value; return Known(type,id) && Points.TryGetValue(type*10000+id,out value)?value:0; }
        public static WardrobeTitles Find(Func<WardrobeTitles,bool> predicate) { return Data.Values.FirstOrDefault(predicate); }
        public static void Load()
        {
            Game.Features.ProgressionEvidence.Initialize();
            using(var connection=Database.DataHolder.MySqlConnection)
            {
                connection.Open();
                using(var command=new NpgsqlCommand("CREATE TABLE IF NOT EXISTS revora_title_backup_v1(id bigint PRIMARY KEY,points integer NOT NULL,data bytea NOT NULL,saved_at timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP); CREATE TABLE IF NOT EXISTS revora_title_ctf(id integer PRIMARY KEY CHECK(id=1),guild_id bigint NOT NULL);",connection)) command.ExecuteNonQuery();
                using(var command=new NpgsqlCommand("SELECT guild_id FROM revora_title_ctf WHERE id=1",connection))
                    TitleRequirements.CtfGuild=Convert.ToUInt32(command.ExecuteScalar()??0);
                Data.Clear();
                using(var command=new NpgsqlCommand("SELECT id,points,data FROM titles",connection))
                using(var reader=command.ExecuteReader())
                    while(reader.Read()) { var row=new WardrobeTitles{Id=Convert.ToUInt32(reader[0]),Points=Convert.ToInt32(reader[1]),Data=(byte[])reader[2]}; Data[row.Id]=row; }
            }
        }
    }
}
