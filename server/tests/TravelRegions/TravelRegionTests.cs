using System;
using System.Linq;
using System.Reflection;
using Revora;

internal static class TravelRegionTests
{
    static int checks;
    static readonly MethodInfo Match=typeof(Server).Assembly.GetType("Revora.Game.Features.TravelRegions").GetMethod("At",BindingFlags.NonPublic|BindingFlags.Static);
    static void At(ushort map,ushort x,ushort y,params int[] expected) {
        var actual=(int[])Match.Invoke(null,new object[]{map,x,y});
        if(!actual.OrderBy(i=>i).SequenceEqual(expected.OrderBy(i=>i)))throw new Exception("Region mismatch at "+map+" "+x+","+y+": "+string.Join(",",actual));
        checks++;
    }
    static int Main() {
        try {
            At(1002,348,209,10612);At(1002,347,209);At(1002,348,208);
            At(1002,380,209,10612);At(1002,381,209,10612,10613);
            At(1002,507,433,10612,10613);At(1002,508,433,10613);At(1002,500,434,10613);
            At(1002,237,340,10613);At(1002,236,340);At(1002,237,339);
            At(1002,500,680,10613,10614);At(1002,350,720,10613,10614);
            At(1002,349,720,10613);At(1002,389,759,10613,10614);At(1002,390,759,10613);At(1002,350,760,10613);
            At(1011,119,159,10617);At(1011,118,159);At(1011,268,308,10617);At(1011,269,308);At(1011,268,309);
            At(1011,273,0,10618);At(1011,760,386,10618,10619);At(1011,760,507,10618);
            At(1013,0,0,10620);At(1013,998,998,10620);At(1013,999,998);At(1013,998,999);
            At(1014,10,10,10622);At(1016,10,10,10621);At(1001,10,10,10703);
            At(1020,515,477,10625);At(1020,593,614,10625);At(1020,594,614,10626);
            At(1015,682,494,10629);At(1015,783,617,10629);At(1015,784,617,10631);
            At(1000,445,528,10701);At(1000,545,697,10701);At(1000,546,697,10702);
            At(1205,10,10,10708);At(1202,10,10,10709);At(1201,10,10,10710);At(1204,10,10,10711);
            At(1216,10,10,10712);At(1207,10,10,10713);At(1208,10,10,10714);At(1210,10,10,10715);
            At(1211,10,10,10716);At(1212,10,10,10717);At(1219,10,10,10718);At(1217,10,10,10719);
            At(1215,10,10,10720);At(1214,10,10,10721);
            At(1360,10,10,10723);At(1361,10,10,10724);At(1362,10,10,10725);At(1363,10,10,10726);At(1364,10,10,10727);
            At(1926,10,10,10801);At(1927,10,10,10802);At(1999,10,10,10803);At(2054,10,10,10804);At(2055,10,10,10805);At(2056,10,10,10806);
            At(1039,10,10,10807);At(1806,10,10,10808);At(1038,10,10,10810);At(1036,10,10,10812);
            for(ushort house=1100;house<=1109;house++){At(house,73,35,10809);At(house,102,64,10809);At(house,103,64);At(house,102,65);}
            foreach(ushort obsolete in new ushort[]{1003,1028,1076,1025,1075,1027,1078,1077,1026,1218,1351,1352,1353,1354,1766})At(obsolete,10,10);
            At(0,0,0);At(65535,65535,65535);At(1002,65535,65535);At(1110,73,35);
            System.Console.WriteLine("Travel region checks passed: "+checks);return 0;
        } catch(Exception ex) {System.Console.Error.WriteLine(ex);return 1;}
    }
}
