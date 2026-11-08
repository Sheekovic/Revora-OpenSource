using System;
using System.Collections.Generic;
using Throne.World.Database.Information.Files;

namespace COServer
{
    public sealed class StorageManager
    {
        private static StorageInfo Info;
        public static Dictionary<uint, COServer.Network.GamePackets.WardrobeTitles> Data;

        public static void Load()
        {
            Storage.Read(out Info);
            Data = new Dictionary<uint, Network.GamePackets.WardrobeTitles>();
            using (var cmd = new Database.MySqlCommand(Database.MySqlCommandType.SELECT))
            {
                cmd.Select("Titles");
                var reader = cmd.CreateReader();
                while (reader.Read())
                {
                    var title = new COServer.Network.GamePackets.WardrobeTitles();
                    title.Id = reader.ReadUInt32("Id");
                    title.Points = reader.ReadInt32("Points");
                    title.Data = reader.ReadBlob("Data");
                    Data.Add(title.Id, title);
                }
            }
        }

        public static T Wing<T>(int _type, int _id)
        {
            object value = null;
            int trash = 0;
            if (typeof(T) == typeof(bool))
            {
                value = int.TryParse(Info.GetUnitByID(_id, Info.GetStorageByType(_type)).Param, out trash);
            }
            else if (typeof(T) == typeof(int))
            {
                var myType = _type.ToString();
                var myID = _id.ToString();


                while (myID.Length < 4)
                    myID = "0" + myID;
                value = int.Parse(myType + myID);
            }
            else
                throw new Exception("Unknow type : " + typeof(T).Name);
            return (T)Convert.ChangeType(value, typeof(T));
        }

        public static int GetTitlePoints(short _type, short _id)
        {
            if (_type == 1 && _id == 1000)
                return 150;
            else if (_type == 2018 && _id == 1)
                return 500;
            else if (_type == 2001 && _id == 2)
                return 300;
            else if (_type == 2002 && _id == 3)
                return 150;
            else if (_type == 2003 && _id == 4)
                return 300;
            else if (_type == 2004 && _id == 5)
                return 150;
            else if (_type == 2005 && _id == 6)
                return 150;
            else if (_type == 2006 && _id == 7)
                return 150;
            else if (_type == 2020 && _id == 2020)
                return 300;
            else if (_type == 2021 && _id == 2021)
                return 200;
            else if (_type == 2022 && _id == 2022)
                return 100;
            else if (_type == 2023 && _id == 2023)
                return 300;
            else if (_type == 2024 && _id == 2024)
                return 200;
            else if (_type == 2025 && _id == 2025)
                return 100;
            else if (_type == 2028 && _id == 2028)
                return 150;
            else if (_type == 2029 && _id == 2029)
                return 300;
            else if (_type == 2030 && _id == 2030)
                return 100;
            else if (_type == 2031 && _id == 2031)
                return 200;
            else if (_type == 2032 && _id == 2032)
                return 200;
            else if (_type == 2033 && _id == 2033)
                return 200;
            return 0;
        }  
        public static T Title<T>(int _type, int _id)
        {
            object value = null;
            int trash = 0;
            if (typeof(T) == typeof(bool))
            {
                value = !int.TryParse(Info.GetUnitByID(_id, Info.GetStorageByType(_type)).Param, out trash);
            }
            else if (typeof(T) == typeof(int))
            {
                var myType = _type.ToString();
                var myID = _id.ToString();

                while (myID.Length < 4)
                    myID = "0" + myID;
                value = int.Parse(myType + myID);
            }
            else
                throw new Exception("Unknow type : " + typeof(T).Name);
            return (T)Convert.ChangeType(value, typeof(T));
        }

        public static Network.GamePackets.WardrobeTitles Find(Func<Network.GamePackets.WardrobeTitles, bool> predicate)
        {
            foreach (var title in Data.Values)
            {
                if (predicate(title))
                    return title;
            }
            return null;
        }
    }
}