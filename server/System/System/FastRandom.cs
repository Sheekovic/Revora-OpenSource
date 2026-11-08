// * Created by AliMOssaed
// * Copyright © 2015
// * Epic-Fight - Project
namespace System
{
    public class FastRandom
    {
        private object object_0;
        private uint uint_0;
        private uint uint_1;
        private uint uint_2;
        private uint uint_3;

        public FastRandom()
            : this(Time32.Now.TotalMilliseconds)
        {
            // Class1.Class0.smethod_0();
        }

        public FastRandom(int seed)
        {
            this.object_0 = new object();
            this.Reinitialise(seed);
        }

        public int Next()
        {
            lock (this.object_0)
            {
                uint num = this.uint_0 ^ (this.uint_0 << 11);
                this.uint_0 = this.uint_1;
                this.uint_1 = this.uint_2;
                this.uint_2 = this.uint_3;
                this.uint_3 = (this.uint_3 ^ (this.uint_3 >> 0x13)) ^ (num ^ (num >> 8));
                uint num2 = this.uint_3 & 0x7fffffff;
                if (num2 == 0x7fffffff)
                {
                    return this.Next();
                }
                return (int)num2;
            }
        }

        public int Next(int upperBound)
        {
            lock (this.object_0)
            {
                if (upperBound < 0)
                {
                    upperBound = 0;
                }
                uint num = this.uint_0 ^ (this.uint_0 << 11);
                this.uint_0 = this.uint_1;
                this.uint_1 = this.uint_2;
                this.uint_2 = this.uint_3;
                return (int)((4.6566128730773926E-10 * (0x7fffffff & (this.uint_3 = (this.uint_3 ^ (this.uint_3 >> 0x13)) ^ (num ^ (num >> 8))))) * upperBound);
            }
        }

        public int Next(int lowerBound, int upperBound)
        {
            lock (this.object_0)
            {
                if (lowerBound > upperBound)
                {
                    int num = lowerBound;
                    lowerBound = upperBound;
                    upperBound = num;
                }
                uint num2 = this.uint_0 ^ (this.uint_0 << 11);
                this.uint_0 = this.uint_1;
                this.uint_1 = this.uint_2;
                this.uint_2 = this.uint_3;
                int num3 = upperBound - lowerBound;
                if (num3 < 0)
                {
                    return (lowerBound + ((int)((2.3283064365386963E-10 * (this.uint_3 = (this.uint_3 ^ (this.uint_3 >> 0x13)) ^ (num2 ^ (num2 >> 8)))) * (upperBound - lowerBound))));
                }
                return (lowerBound + ((int)((4.6566128730773926E-10 * (0x7fffffff & (this.uint_3 = (this.uint_3 ^ (this.uint_3 >> 0x13)) ^ (num2 ^ (num2 >> 8))))) * num3)));
            }
        }

        public unsafe void NextBytes(byte[] buffer)
        {
            lock (this.object_0)
            {
                if ((buffer.Length % 8) != 0)
                {
                    new Random().NextBytes(buffer);
                }
                else
                {
                    uint num = this.uint_0;
                    uint num2 = this.uint_1;
                    uint num3 = this.uint_2;
                    uint num4 = this.uint_3;
                    fixed (byte* numRef = buffer)
                    {
                        uint* numPtr = (uint*)numRef;
                        int index = 0;
                        int num6 = buffer.Length >> 2;
                        while (index < num6)
                        {
                            uint num7 = num ^ (num << 11);
                            num = num2;
                            num2 = num3;
                            num3 = num4;
                            numPtr[index] = num4 = (num4 ^ (num4 >> 0x13)) ^ (num7 ^ (num7 >> 8));
                            num7 = num ^ (num << 11);
                            num = num2;
                            num2 = num3;
                            num3 = num4;
                            numPtr[index + 1] = num4 = (num4 ^ (num4 >> 0x13)) ^ (num7 ^ (num7 >> 8));
                            index += 2;
                        }
                    }
                }
            }
        }

        public double NextDouble()
        {
            lock (this.object_0)
            {
                uint num = this.uint_0 ^ (this.uint_0 << 11);
                this.uint_0 = this.uint_1;
                this.uint_1 = this.uint_2;
                this.uint_2 = this.uint_3;
                return (4.6566128730773926E-10 * (0x7fffffff & (this.uint_3 = (this.uint_3 ^ (this.uint_3 >> 0x13)) ^ (num ^ (num >> 8)))));
            }
        }

        public int NextInt()
        {
            lock (this.object_0)
            {
                uint num = this.uint_0 ^ (this.uint_0 << 11);
                this.uint_0 = this.uint_1;
                this.uint_1 = this.uint_2;
                this.uint_2 = this.uint_3;
                return (0x7fffffff & ((int)(this.uint_3 = (this.uint_3 ^ (this.uint_3 >> 0x13)) ^ (num ^ (num >> 8)))));
            }
        }

        public uint NextUInt()
        {
            lock (this.object_0)
            {
                uint num = this.uint_0 ^ (this.uint_0 << 11);
                this.uint_0 = this.uint_1;
                this.uint_1 = this.uint_2;
                this.uint_2 = this.uint_3;
                return (this.uint_3 = (this.uint_3 ^ (this.uint_3 >> 0x13)) ^ (num ^ (num >> 8)));
            }
        }

        public void Reinitialise(int seed)
        {
            lock (this.object_0)
            {
                this.uint_0 = (uint)seed;
                this.uint_1 = 0x32378fc7;
                this.uint_2 = 0xd55f8767;
                this.uint_3 = 0x104aa1ad;
            }
        }

        public int Sign()
        {
            if (this.Next(0, 2) == 0)
            {
                return -1;
            }
            return 1;
        }
    }
}

