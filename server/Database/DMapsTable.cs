// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace Revora.Database
{
    public unsafe class DMaps
    {
        public static SafeDictionary<ushort, string> MapPaths = new SafeDictionary<ushort, string>(280);
        public static void Load()
        {
            if (File.Exists(Constants.DataHolderPath + "GameMap.dat"))
            {
                Console.WriteLine("Loading GameMap information!");
                Time32 start = Time32.Now;
                FileStream FS = new FileStream(Constants.DataHolderPath + "GameMap.dat", FileMode.Open);
                BinaryReader BR = new BinaryReader(FS);
                uint MapCount = BR.ReadUInt32();
                for (uint i = 0; i < MapCount; i++)
                {
                    //System.Console.Write("\b{0}", Cargando.NextChar());
                    ushort MapID = (ushort)BR.ReadUInt32();
                    string Path = Encoding.Default.GetString(BR.ReadBytes(BR.ReadInt32()));
                    if (MapID >= 1712 && MapID <= 1720)
                    {
                        BR.ReadUInt32();
                        continue;
                    }
                    if (Path.EndsWith(".7z"))
                    {
                        Path = Path.Remove(Path.Length - 3, 3);
                        Path += ".dmap";
                    }
                    MapsTable.MapInformation info = new MapsTable.MapInformation();
                    if (MapsTable.MapInformations.ContainsKey(MapID)) info = MapsTable.MapInformations[MapID];
                    {
                        if (info.ID == info.BaseID) new Game.Map(MapID, Path);
                        {
                            MapPaths.Add(MapID, Path);
                            BR.ReadInt32();
                        }
                    }
                }
                foreach (var kval in MapsTable.MapInformations.Values)
                {
                    if (kval.BaseID != kval.ID)
                    {
                        new Game.Map(kval.ID, kval.BaseID, MapPaths[kval.BaseID]);
                    }
                }
                BR.Close();
                FS.Close();
                //Console.WriteLine("Successfully!");
                //Server.MCompressor.Optimize(); Server.MCompressor.Optimize();
                //Server.MCompressor.Optimize(); Server.MCompressor.Optimize();
                //Server.MCompressor.Optimize(); Server.MCompressor.Optimize();
            }
            else Console.WriteLine("The specified Conquer Online folder doesn't exist. Game map couldn't be loaded.");
        }
    }
}