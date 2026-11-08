// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Revora.Database
{
    public unsafe class PoketTables
    {
        public static void LoadTables()
        {
            if (!Game.PokerPolicy.Enabled) return;
            Console.WriteLine("Loading Poker Tables!");
            string[] TDs = System.IO.File.ReadAllLines("database/Slayer/PokerTables.txt");
            foreach (string Tinfo in TDs)
            {
                //System.Console.Write("\b{0}", Cargando.NextChar());
                string[] DR = Tinfo.Split(' ');
                Game.PokerTable T = new Game.PokerTable();
                T.Id = Convert.ToUInt32(DR[0]);
                T.Nomber = (byte)Convert.ToInt16(DR[3]);
                T.BetType = (byte)Convert.ToUInt32(DR[1]);
                T.X = (ushort)Convert.ToUInt32(DR[4]);
                T.Y = (ushort)Convert.ToUInt32(DR[5]);
                bool FoundMinBet = false;
                try
                {
                    T.MinLimit = (ushort)Convert.ToUInt32(DR[6]); FoundMinBet = true;
                }
                catch { }
                T.Map = Convert.ToUInt32(DR[2]);
                if (!FoundMinBet)
                {
                    if (T.Nomber > 19)
                    {
                        T.MinLimit = 10000;
                        T.FreeBet = false;
                    }
                    if (T.Nomber > 25) T.MinLimit = 5000;
                    if (T.Nomber > 30) T.MinLimit = 2000;
                    if (T.Nomber > 35) T.MinLimit = 1000;
                }
                if (!Revora.Kernel.PokerTables.ContainsKey(T.Id))
                    Revora.Kernel.PokerTables.Add(T.Id, T);
            }
            //Console.WriteLine("Done!");
        }
    }
}
