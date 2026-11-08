using System.Collections.Generic;

namespace Revora.Game
{
    public unsafe class SubClass
    {
        public byte ID;
        public byte Phase;
        public byte Level;
    }
    public unsafe class Subclasses
    {
        public uint GetHashPoint()
        {
            uint val = 0;
           
            return val;
        }
        public Dictionary<byte, SubClass> Classes;
        public ushort StudyPoints;
        public byte Active;
        public Entity Owner;
        public Subclasses()
        {
            Classes = new Dictionary<byte, SubClass>();
            StudyPoints = 0;
            Active = 0;
        }
    }
    public enum ClassID : byte
    {
        MartialArtist = 1,
        Warlock = 2,
        ChiMaster = 3,
        Sage = 4,
        Apothecary = 5,
        Performer = 6,
        Wrangler = 9
    }
}