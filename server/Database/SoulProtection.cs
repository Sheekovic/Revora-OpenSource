// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.IO;
using System.Collections.Generic;

namespace Revora.Database
{
    public unsafe class SoulProtection
    {
        public struct SoulInfo
        {
            public uint UID;
            public uint ItemType;
            public Network.GamePackets.ItemHandler.Positions Pos;
        }
        public static void Load()
        {
            string path = Path.Combine(Constants.DataHolderPath, "souls_protection.txt");
            if (!File.Exists(path)) path = Path.Combine(Constants.DataHolderPath, "Slayer", "souls_protection.txt");
            if (File.Exists(path))
            {
                string[] lines = File.ReadAllLines(path);
                foreach (var item in lines)
                {
                    try
                    {
                        var coloums = item.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
                        SoulInfo info = new SoulInfo();
                        info.UID = uint.Parse(coloums[0]);
                        info.ItemType = uint.Parse(coloums[1]);
                        info.Pos = (Network.GamePackets.ItemHandler.Positions)uint.Parse(coloums[2]);
                        if (!Soul_Protections.ContainsKey(info.UID))
                            Soul_Protections.Add(info.UID, info);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.ToString());
                        throw;
                    }
                }
            }
        }
        public static SafeDictionary<uint, SoulInfo> Soul_Protections = new SafeDictionary<uint, SoulInfo>();
    }
}