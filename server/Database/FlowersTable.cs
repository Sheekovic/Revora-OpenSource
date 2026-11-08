using System;
using System.Linq;

namespace Revora.Database
{
    public unsafe class Flowers
    {
        //public static void SaveFlowers()
        //{
        //    using (Write write = new Write("database\\Slayer\\Flores.txt"))
        //    {
        //        Game.Features.Flowers[] array = Game.Features.Flowers.Flowers_Poll.Values.ToArray<Game.Features.Flowers>();
        //        uint count = (uint)Game.Features.Flowers.Flowers_Poll.Count;
        //        string[] array2 = new string[count];
        //        for (uint num = 0u; num < count; num += 1u)
        //        {
        //            array2[(int)((UIntPtr)num)] = array[(int)((UIntPtr)num)].ToString();
        //        }
        //        write.Add(array2, array2.Length).Execute(Mode.Open);
        //    }
        //    using (Write write2 = new Write("database\\Slayer\\Besos.txt"))
        //    {
        //        Game.Features.Flowers[] array3 = Game.Features.Flowers.BoyFlowers.Values.ToArray<Game.Features.Flowers>();
        //        uint count2 = (uint)Game.Features.Flowers.BoyFlowers.Count;
        //        string[] array4 = new string[count2];
        //        for (uint num2 = 0u; num2 < count2; num2 += 1u)
        //        {
        //            array4[(int)((UIntPtr)num2)] = array3[(int)((UIntPtr)num2)].ToString();
        //        }
        //        write2.Add(array4, array4.Length).Execute(Mode.Open);
        //    }
        //}
        //public static void LoadFlowers()
        //{
        //    using (Read read = new Read("database\\Slayer\\Flores.txt"))
        //    {
        //        if (read.Reader(true))
        //        {
        //            int count = read.Count;
        //            uint num = 0u;
        //            while ((ulong)num < (ulong)((long)count))
        //            {
        //                string text = read.ReadString("");
        //                if (text != null)
        //                {
        //                    Game.Features.Flowers flowers = new Game.Features.Flowers();
        //                    flowers.Read(text);
        //                    if (!Game.Features.Flowers.Flowers_Poll.ContainsKey(flowers.UID))
        //                    {
        //                        Game.Features.Flowers.Flowers_Poll.TryAdd(flowers.UID, flowers);
        //                    }
        //                    else
        //                    {
        //                        Game.Features.Flowers.Flowers_Poll[flowers.UID] = flowers;
        //                    }
        //                    Game.Features.Flowers.CulculateRankRouse(flowers);
        //                    Game.Features.Flowers.CulculateRankLilies(flowers);
        //                    Game.Features.Flowers.CulculateRankOrchids(flowers);
        //                    Game.Features.Flowers.CulculateRankTulips(flowers);
        //                }
        //                num += 1u;
        //            }
        //        }
        //    }
        //    using (Read read2 = new Read("database\\Slayer\\Besos.txt"))
        //    {
        //        if (read2.Reader(true))
        //        {
        //            int count2 = read2.Count;
        //            uint num2 = 0u;
        //            while ((ulong)num2 < (ulong)((long)count2))
        //            {
        //                string text2 = read2.ReadString("");
        //                if (text2 != null)
        //                {
        //                    Game.Features.Flowers flowers2 = new Game.Features.Flowers();
        //                    flowers2.Read(text2);
        //                    if (!Game.Features.Flowers.BoyFlowers.ContainsKey(flowers2.UID))
        //                    {
        //                        Game.Features.Flowers.BoyFlowers.TryAdd(flowers2.UID, flowers2);
        //                    }
        //                    else
        //                    {
        //                        Game.Features.Flowers.BoyFlowers[flowers2.UID] = flowers2;
        //                    }
        //                    Game.Features.Flowers.CulculateRankKiss(flowers2);
        //                    Game.Features.Flowers.CulculateRankLove(flowers2);
        //                    Game.Features.Flowers.CulculateRankTine(flowers2);
        //                    Game.Features.Flowers.CulculateRankJade(flowers2);
        //                }
        //                num2 += 1u;
        //            }
        //        }
        //    }
        //    GC.Collect();
        //}
    }
}