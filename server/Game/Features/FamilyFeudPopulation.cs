using System;
using System.IO;
using System.Security.Cryptography;
using Revora.Database;
namespace Revora.Game.Features
{
    internal static class FamilyFeudPopulation
    {
        // Correct only the two audited legacy Banditti rectangles. Preserve row
        // counts, respawn timing and every unrelated/custom spawn definition.
        internal static bool Correct(Map map,uint family,ref ushort x,ref ushort y,ref ushort width,ref ushort height){
            if(map==null || map.ID!=1015 || map.BaseID!=1015 || family!=84)return false;
            bool first=x==114 && y==312 && width==95 && height==45;
            bool second=x==293 && y==559 && width==56 && height==37;
            if(!first && !second)return false;
            MonsterInformation info;if(!MonsterInformation.MonsterInformations.TryGetValue(84,out info) || info.Name!="Banditti" || info.Level!=98 || info.Mesh!=201 || info.Boss || info.Guard || info.Reviver)
                throw new InvalidOperationException("Family Feud Banditti catalog changed.");
            using(var sha=SHA256.Create())using(var file=File.OpenRead(Path.Combine(Constants.DMapsPath,"maps","1015.map")))
                if(System.BitConverter.ToString(sha.ComputeHash(file)).Replace("-","").ToLowerInvariant()!="e47df70501200965040755a424873395172c03e7dad920d483414f85c0359ba7")
                    throw new InvalidOperationException("Family Feud population terrain changed.");
            x=(ushort)(first?518:540);y=(ushort)(first?339:338);width=height=10;return true;
        }
    }
}
