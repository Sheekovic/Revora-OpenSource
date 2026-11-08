// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Revora.Game.Features.Reincarnation
{
    public unsafe class ReincarnateInfo
    {
        public uint UID;
        public byte Level;
        public ulong Experience;
        // Internal transaction metadata; never part of the legacy packet or file format.
        internal bool FirstRebirth, SecondRebirth, PaidReincarnation, Recovered;
        internal Guid PaidReceipt;
        internal byte RebirthClass;
    }
}
