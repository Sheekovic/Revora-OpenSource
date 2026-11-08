// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Revora.Network.GamePackets;

namespace Revora.Game.ConquerStructures
{
    public unsafe class Trade
    {
        internal readonly Guid ReceiptId=Guid.NewGuid();
        internal readonly Dictionary<uint,string> OfferSnapshots=new Dictionary<uint,string>();
        public ulong Money;
        public uint RevoraPoint, ConquerPoints, TraderUID;
        public List<ConquerItem> Items;
        public bool Accepted, InTrade;
        public Trade()
        {
            InTrade = Accepted = false;
            RevoraPoint = TraderUID = 0;
            Money = 0;
            ConquerPoints = 0;
            Items = new List<ConquerItem>();
        }

    }
}
