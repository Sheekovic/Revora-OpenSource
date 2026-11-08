// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.Collections.Generic;

namespace Revora.Game.ConquerStructures
{
    public unsafe class Broadcast
    {
        public struct BroadcastStr
        {
            public uint ID;
            public uint EntityID;
            public string EntityName;
            public uint SpentCPs;
            public string Message;
        }
        public static DateTime LastBroadcast = DateTime.Now;
        public static Counter BroadcastCounter = new Revora.Counter(1);
        public static List<BroadcastStr> Broadcasts = new List<BroadcastStr>();
        public static BroadcastStr CurrentBroadcast = new BroadcastStr() { EntityID = 1 };
    }
}