// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;

namespace Revora
{
    public unsafe class ClanMember
    {
        public UInt32 Identifier, Donation;
        public String Name, LeaderName;
        public Clan.Ranks Rank;
        public Byte Level, Class;
        public uint UID = 0;
        public bool IsOnline
        {
            get { return Kernel.GamePool.ContainsKey(UID); }
        }
        public Client.GameState Client
        {
            get { return Kernel.GamePool[UID]; }
        }
    }
}