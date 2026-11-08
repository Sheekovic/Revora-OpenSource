// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

namespace Revora.Game.ConquerStructures.Society
{
    public unsafe class Apprentice : Interfaces.IKnownPerson
    {
        public uint ID
        {
            get;
            set;
        }
        public string Name
        {
            get;
            set;
        }
        public bool IsOnline
        {
            get { return Kernel.GamePool.ContainsKey(ID); }
        }
        public Client.GameState Client
        {
            get { return Kernel.GamePool[ID]; }
        }
        public uint EnroleDate
        {
            get;
            set;
        }
        public ulong Actual_Experience
        {
            get;
            set;
        }
        public ulong Total_Experience
        {
            get;
            set;
        }
        public ushort Actual_Plus
        {
            get;
            set;
        }
        public ushort Total_Plus
        {
            get;
            set;
        }
        public ushort Actual_HeavenBlessing
        {
            get;
            set;
        }
        public ushort Total_HeavenBlessing
        {
            get;
            set;
        }
    }
}