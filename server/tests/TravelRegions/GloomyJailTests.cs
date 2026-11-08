using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Revora.Game;

internal static class GloomyJailTests
{
    static int checks;
    static void Check(bool value,string message) {if(!value)throw new Exception(message);checks++;Console.WriteLine("PASS: "+message);}
    static string Hash(byte[] bytes) {using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
    static int Main(string[] args) {
        try {
            if(args.Length!=2)throw new ArgumentException("Pass the private 1363.map and extracted maidan-D.DMap.");
            if(new FileInfo(args[0]).Length!=313608 || new FileInfo(args[1]).Length!=1958888)throw new InvalidDataException("Unexpected artifact length.");
            byte[] output=File.ReadAllBytes(args[0]),source=File.ReadAllBytes(args[1]);
            Check(Hash(output)=="a9a555cf6b1b407f75b5ce19aaaec2109d3d1397cf8b0675b67acfdf7b5a54dd","staged server terrain has the reviewed output hash");
            Check(Hash(source)=="ddce0fec05ab9d2dd1c6509cfd1867a08ce75f3143875f57ee920b80ca65102b","input DMap has the reviewed extracted-archive hash");
            int width=BitConverter.ToInt32(output,0),height=BitConverter.ToInt32(output,4);
            Check(width==560 && height==560 && width==BitConverter.ToInt32(source,268) && height==BitConverter.ToInt32(source,272),"server and client terrain dimensions agree");
            var floor=new Floor(width,height,1363);int walkable=0,blocked=0,firstX=-1,firstY=-1;
            for(int y=0;y<height;y++)for(int x=0;x<width;x++) {
                // Exercise the existing production .map reader's assignment and
                // the actual Floor implementation, not the staging parser.
                floor[x,y,MapObjectType.InvalidCast]=output[8+y*width+x]!=1;
                bool accepted=floor[x,y,MapObjectType.Player];
                bool expected=BitConverter.ToUInt16(source,276+y*(width*6+4)+x*6)==0;
                if(accepted!=expected)throw new Exception("Terrain polarity mismatch at "+x+","+y);
                if(accepted){walkable++;if(firstX<0){firstX=x;firstY=y;}}else blocked++;
            }
            Check(walkable==134697 && blocked==178903,"all 313600 cells preserve client walkability through Revora Floor");
            Check(!floor[-1,0,MapObjectType.Player] && !floor[0,-1,MapObjectType.Player]
                && !floor[560,0,MapObjectType.Player] && !floor[0,560,MapObjectType.Player],"out-of-bounds landings remain blocked");
            var regions=typeof(Floor).Assembly.GetType("Revora.Game.Features.TravelRegions");
            var ids=(int[])regions.GetMethod("At",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{(ushort)1363,(ushort)firstX,(ushort)firstY});
            Check(ids.SequenceEqual(new[]{10726}),"reviewed region geometry recognizes a valid staged map-1363 point");
            Console.WriteLine("Gloomy Jail floor checks passed: "+checks);return 0;
        }catch(Exception ex){Console.Error.WriteLine(ex.GetType().Name+": "+ex.Message);return 1;}
    }
}
