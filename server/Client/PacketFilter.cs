using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace Revora
{
    public unsafe class PacketFilter : IEnumerable<int>
    {
        private struct PacketEntry
        {
            public int Time;
            public int Count;
        }
        public const int Timelimit = 500;
        public const int PacketLimit = 8;
        private ConcurrentDictionary<int, int> Limits;
        private ConcurrentDictionary<int, PacketEntry> Entries;
        private object SyncRoot;
        public PacketFilter()
        {
            Limits = new ConcurrentDictionary<int, int>();
            Entries = new ConcurrentDictionary<int, PacketEntry>();
            SyncRoot = new object();
        }
        public void Add(int id, int value)
        {
            Limits[id] = value;
        }
        public bool Filter(int id)
        {
            lock (SyncRoot)
            {
                int time = Time32.Now.GetHashCode();
                PacketEntry filter;
                if (!Entries.TryGetValue(id, out filter))
                {
                    Entries.TryAdd(id, filter = new PacketEntry()
                    {
                        Count = 1,
                        Time = time
                    });
                    return false;
                }
                if (time - filter.Time > Timelimit)
                {
                    filter.Time = time;
                    filter.Count = 0;
                }
                filter.Count++;
                Entries[id] = filter;
                return (filter.Count > Limit(id));
            }
        }
        private int Limit(int id)
        {
            if (Limits.ContainsKey(id)) return Limits[id];
            return PacketLimit;
        }
        IEnumerator<int> IEnumerable<int>.GetEnumerator()
        {
            throw new NotImplementedException();
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException();
        }
    }
}