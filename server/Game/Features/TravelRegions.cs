using System;
using System.Collections.Generic;

namespace Revora.Game.Features
{
    // Numeric interoperability facts from hash-reviewed 6711 region.ini.
    // Matching a rectangle alone is not proof of an accepted player movement.
    internal static class TravelRegions
    {
        private sealed class Region
        {
            internal readonly ushort Map, X, Y, Width, Height;
            internal readonly int Achievement;
            internal Region(ushort map, ushort x, ushort y, ushort width, ushort height, int achievement)
            {
                if (width == 0 || height == 0 || (uint)x + width > ushort.MaxValue || (uint)y + height > ushort.MaxValue
                    || !AchievementCatalog.Known(achievement)) throw new InvalidOperationException("Invalid reviewed travel region.");
                Map=map;X=x;Y=y;Width=width;Height=height;Achievement=achievement;
            }
            internal bool Contains(ushort map,ushort x,ushort y)
            {return Map==map && x>=X && x<(uint)X+Width && y>=Y && y<(uint)Y+Height;}
        }
        private static readonly Region[] regions = {
            new Region(1002,348,209,160,225,10612),
            new Region(1002,381,209,999,999,10613),
            new Region(1002,237,340,999,999,10613),
            new Region(1002,500,680,20,20,10614),
            new Region(1002,404,667,198,147,10614),
            new Region(1002,429,610,52,66,10614),
            new Region(1002,450,581,30,30,10614),
            new Region(1002,350,720,40,40,10614),
            new Region(1011,119,159,150,150,10617),
            new Region(1011,0,0,115,155,10618),
            new Region(1011,273,0,999,999,10618),
            new Region(1011,0,313,999,999,10618),
            new Region(1011,119,0,150,155,10618),
            new Region(1011,0,159,115,150,10618),
            new Region(1011,760,386,999,121,10619),
            new Region(1013,0,0,999,999,10620),
            new Region(1016,0,0,999,999,10621),
            new Region(1014,0,0,999,999,10622),
            new Region(1020,515,477,79,138,10625),
            new Region(1020,0,0,510,475,10626),
            new Region(1020,594,0,999,999,10626),
            new Region(1020,0,615,999,999,10626),
            new Region(1020,515,0,79,475,10626),
            new Region(1020,0,477,513,138,10626),
            new Region(1015,682,494,102,124,10629),
            new Region(1015,0,0,680,490,10631),
            new Region(1015,784,0,999,999,10631),
            new Region(1015,0,620,999,999,10631),
            new Region(1015,682,0,102,490,10631),
            new Region(1015,0,494,680,124,10631),
            new Region(1000,445,528,101,170,10701),
            new Region(1000,0,0,443,525,10702),
            new Region(1000,546,0,999,999,10702),
            new Region(1000,0,698,999,999,10702),
            new Region(1000,445,0,101,525,10702),
            new Region(1000,0,528,443,170,10702),
            new Region(1001,0,0,999,999,10703),
            new Region(1205,0,0,999,999,10708),
            new Region(1202,0,0,999,999,10709),
            new Region(1201,0,0,999,999,10710),
            new Region(1204,0,0,999,999,10711),
            new Region(1216,0,0,999,999,10712),
            new Region(1207,0,0,999,999,10713),
            new Region(1208,0,0,999,999,10714),
            new Region(1210,0,0,999,999,10715),
            new Region(1211,0,0,999,999,10716),
            new Region(1212,0,0,999,999,10717),
            new Region(1219,0,0,999,999,10718),
            new Region(1217,0,0,999,999,10719),
            new Region(1215,0,0,999,999,10720),
            new Region(1214,0,0,999,999,10721),
            new Region(1360,0,0,999,999,10723),
            new Region(1361,0,0,999,999,10724),
            new Region(1362,0,0,999,999,10725),
            new Region(1363,0,0,999,999,10726),
            new Region(1364,0,0,999,999,10727),
            new Region(1926,0,0,999,999,10801),
            new Region(1927,0,0,999,999,10802),
            new Region(1999,0,0,999,999,10803),
            new Region(2054,0,0,999,999,10804),
            new Region(2055,0,0,999,999,10805),
            new Region(2056,0,0,999,999,10806),
            new Region(1039,0,0,999,999,10807),
            new Region(1806,0,0,999,999,10808),
            new Region(1100,73,35,30,30,10809),
            new Region(1101,73,35,30,30,10809),
            new Region(1102,73,35,30,30,10809),
            new Region(1103,73,35,30,30,10809),
            new Region(1104,73,35,30,30,10809),
            new Region(1105,73,35,30,30,10809),
            new Region(1106,73,35,30,30,10809),
            new Region(1107,73,35,30,30,10809),
            new Region(1108,73,35,30,30,10809),
            new Region(1109,73,35,30,30,10809),
            new Region(1038,0,0,999,999,10810),
            new Region(1036,0,0,999,999,10812),
        };
        internal static int[] At(ushort map,ushort x,ushort y)
        {
            var matches=new List<int>();
            foreach(var region in regions)
                if(region.Contains(map,x,y) && !matches.Contains(region.Achievement))matches.Add(region.Achievement);
            return matches.ToArray();
        }
    }
}
