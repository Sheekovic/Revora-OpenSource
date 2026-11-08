using System.Text;

namespace Revora.Cryptography
{
    public class LoaderEncryption
    {

        private static byte[] Key =
{
      4 ,
    5 ,
    1 ,
    3 ,
    66 ,
    7 ,
    77 ,
    44 ,
    100 ,
    228 ,
    21 ,
    254 ,
    234 ,
    212 ,
    114 ,
    141 ,
    214 ,
    12 ,
    55 ,
    99 ,
    100 ,
    7 ,
    98 ,
    187 ,
    190 ,
    77 ,
    65 ,
    55 ,
    44 ,
    43 ,
    21 ,
    99
}; // idb
        private static byte[] Key2 =
{
  6,
  4,
  1,
  7,
  2,
  33,
  77,
  66,
  65,
  44,
  21,
  254,
  43,
  212,
  90,
  44,
  214,
  12,
  56,
  99,
  67,
  7,
  87,
  99,
  0,
  77,
  43,
  11,
  44,
  22,
  21,
  99
}; // idb

        public static void Encrypt(byte[] arr)
        {
            int length = Key.Length;
            for (int i = 0; i < arr.Length; i++)
            {
                arr[i] ^= Key[i % length];
                arr[i] ^= Key[(i + 1) % length];
            }
        }
        public static void Decrypt(byte[] arr)
        {
            var len = Encoding.Default.GetString(arr).Replace("\0", "").Length;
            for (int i = 0; i < len; ++i)
            {
                arr[i] ^= Key2[88 * i & 0x1F];
                arr[i] ^= Key[32 * i & 0x1C];
            }
        }
    }
}
