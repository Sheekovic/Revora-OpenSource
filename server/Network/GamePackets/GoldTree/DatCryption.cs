using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Revora.Database
{
    public class DatCryption
    {
        private byte[] key = new byte[128];
        public static int seed = 9527;

        public DatCryption()
        {
            MSRandom random = new MSRandom(seed);
            for (int i = 0; i < this.key.Length; i++)
            {
                this.key[i] = (byte)(random.Next() % 256);
            }
        }
        public static string[] Decrypt(string Path)
        {
            byte[] b = new DatCryption().Decrypt(File.ReadAllBytes(Path));
            File.WriteAllBytes("DatCryptor.txt", b);
            string[] text = File.ReadAllLines("DatCryptor.txt");
            File.Delete("DatCryptor.txt");
            return text;
        }
        public byte[] Decrypt(byte[] b)
        {
            for (int i = 0; i < b.Length; i++)
            {
                int num = (int)(b[i] ^ this.key[i % 128]);
                int num2 = i % 8;
                b[i] = (byte)((num << 8 - num2) + (num >> num2));
            }
            return b;
        }
        public class MSRandom
        {
            public long Seed;

            public MSRandom(int seed)
            {
                this.Seed = (long)seed;
            }

            public int Next()
            {
                return (int)((this.Seed = this.Seed * 214013L + 2531011L) >> 16 & 32767L);
            }
        }
    }
}
