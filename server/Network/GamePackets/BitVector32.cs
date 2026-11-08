// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

namespace Revora.Network.GamePackets
{
    public unsafe class BitVector32
    {
        public uint[] bits;
        public BitVector32(int BitCount)
        {
            int sections = BitCount / 32;
            if (BitCount % 32 != 0)
                sections += 1;
            bits = new uint[sections];
        }
        public int Size
        {
            get { return 32 * bits.Length; }
        }
        public void Clear()
        {
            ushort siz = (byte)(Size / 32);
            for (byte x = 0; x < siz; x++)
            {
                bits[x] = 0;
            }
        }
        public void Add(int index)
        {
            if (index < Size)
            {
                int idx = index / 32;
                uint bites = (uint)(1 << (index % 32));
                bits[idx] |= bites;
            }
        }
        public void Remove(int index)
        {
            if (index < Size)
            {
                int idx = index / 32;
                uint bites = (uint)(1 << (index % 32));
                bits[idx] &= ~bites;
            }
        }
        public bool Contain(int index)
        {
            if (index > Size) return false;
            int idx = index / 32;
            uint bites = (uint)(1 << (index % 32));
            return ((bits[idx] & bites) == bites);
        }
    }
}