// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.Collections.Generic;

namespace Revora.Database
{
    public unsafe class MapsTable
    {
        public struct MapInformation
        {
            public ushort ID;
            public ushort BaseID;
            public uint Status;
            public uint Weather;
            public uint RaceRecord;
            public uint Owner;
            public uint HouseLevel;
        }
        public static CareDictionary<ushort, MapInformation> MapInformations = new CareDictionary<ushort, MapInformation>(280);
        private static IniFile RaceRecords;
        public static void Load()
        {
            Console.WriteLine("Loading Map information!");
            RaceRecords = new IniFile("database\\racerecords.ini", "record");
            using(var cmd = new MySqlCommand(MySqlCommandType.SELECT).Select("maps"))
            using (var reader = cmd.CreateReader())
            {
                while (reader.Read())
                {
                    //System.Console.Write("\b{0}", Cargando.NextChar());
                    MapInformation info = new MapInformation();
                    info.ID = reader.ReadUInt16("Id");
                    info.BaseID = reader.ReadUInt16("Mapdoc");
                    info.Status = reader.ReadUInt32("Type");
                    info.Weather = reader.ReadUInt32("Weather");
                    info.RaceRecord = RaceRecords[info.ID, 0].Cast<uint>();
                    MapInformations.Add(info.ID, info);
                }
            }
            //Server.MCompressor.Optimize(); Server.MCompressor.Optimize();
            //Server.MCompressor.Optimize(); Server.MCompressor.Optimize();
            //Server.MCompressor.Optimize(); Server.MCompressor.Optimize();
            //Server.MCompressor.Optimize(); Server.MCompressor.Optimize();
            //Server.MCompressor.Optimize(); Server.MCompressor.Optimize();
            //Server.MCompressor.Optimize(); Server.MCompressor.Optimize();
            //Server.MCompressor.Optimize(); Server.MCompressor.Optimize();
            //Server.MCompressor.Optimize(); Server.MCompressor.Optimize();
            //Console.WriteLine("Done!");
        }
        internal static void SaveRecord(MapInformation key)
        {
            RaceRecords[key.ID] = key.RaceRecord;
        }
    }
}
