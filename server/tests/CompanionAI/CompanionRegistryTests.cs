using System;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Revora.Game;

internal static class CompanionRegistryTests
{
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static Entity Actor(uint uid, ushort x, ushort y) {
        return new Entity(EntityFlag.Player, false) { UID=uid, X=x, Y=y, MapID=1002 };
    }
    static int Main() {
        var map=(Map)FormatterServices.GetUninitializedObject(typeof(Map));
        map.ID=map.BaseID=1002;map.Floor=new Floor(80,80,1002);
        map.Entities=new SafeConcurrentDictionary<uint,Entity>();
        map.Companions=new SafeConcurrentDictionary<uint,Entity>();
        var actors=Enumerable.Range(0,2048).Select(i=>Actor((uint)(1100000+i),(ushort)(1+i%64),(ushort)(1+i/64))).ToArray();
        Parallel.For(0,actors.Length,i=>{map.AddEntity(actors[i]);foreach(var pair in map.Companions) Check(pair.Value!=null,"Null actor published");});
        Check(map.Companions.Count==actors.Length,"Concurrent registration lost actors");
        foreach(var actor in actors) Check(ReferenceEquals(map.Companions[actor.UID],actor),"Registration replaced owner");
        Parallel.For(0,actors.Length,i=>{map.RemoveEntity(actors[i]);foreach(var pair in map.Companions.Values) Check(pair!=null,"Null actor during removal");});
        Check(map.Companions.Count==0,"Concurrent removal retained actors");
        var rivals=Enumerable.Range(0,64).Select(i=>Actor(1200000,40,40)).ToArray();
        Parallel.For(0,rivals.Length,i=>map.AddEntity(rivals[i]));
        var winner=map.Companions[1200000];Check(winner!=null && map.Companions.Count==1,"Duplicate UID must have one owner");
        Parallel.ForEach(rivals.Where(e=>!ReferenceEquals(e,winner)),e=>map.RemoveEntity(e));
        Check(ReferenceEquals(map.Companions[1200000],winner),"Stale cleanup removed current owner");
        map.RemoveEntity(winner);var replacement=Actor(1200000,41,41);map.AddEntity(replacement);map.RemoveEntity(winner);
        Check(ReferenceEquals(map.Companions[1200000],replacement),"Old lifetime removed replacement");
        map.RemoveEntity(replacement);Check(map.Companions.Count==0,"Current owner removal failed");
        System.Console.WriteLine("Companion registry: parallel add/read/remove, UID contention and stale-owner removal passed.");
        return 0;
    }
}
