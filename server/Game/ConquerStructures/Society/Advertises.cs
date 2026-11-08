using Revora.Game.ConquerStructures.Society;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Revora.Game.ConquerStructures.Society
{
    public class Advertises
    {
        public static System.Collections.Concurrent.ConcurrentDictionary<uint, Guild> AGuilds = new System.Collections.Concurrent.ConcurrentDictionary<uint, Guild>();

        public static Guild[] AdvertiseRanks = new Guild[0];
        public static void Add(Guild obj)
        {
            if (!AGuilds.ContainsKey(obj.ID))
                AGuilds.TryAdd(obj.ID, obj);
            CalculateRanks();
        }

        public static void CalculateRanks()
        {
            lock (AdvertiseRanks)
            {
                Guild[] array = AGuilds.Values.ToArray();
                array = (from guil in array orderby new Recruitment().Donations descending select guil).ToArray();
                List<Guild> listarray = new List<Guild>();
                for (ushort x = 0; x < array.Length; x++)
                {
                    listarray.Add(array[x]);
                    if (x == 40) break;
                }
                AdvertiseRanks = listarray.ToArray();
            }
        }
    }
}

