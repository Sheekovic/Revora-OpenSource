using System;
using System.IO;
using System.Text;

namespace Revora.Network.AuthPackets
{
    public class Authentication : Interfaces.IPacket
    {
        public string Username;
        public string Password;
        public string Server;
        public string PasswordMethod;
        public Authentication()
        {
        }
        #region Desearialize Bigboss Loader
        public void Deserialize(byte[] buffer)
        {
            Deserialize(buffer, 0);
        }

        public void Deserialize(byte[] buffer, int passwordSeed)
        {
            if (buffer.Length == 312)
            {
                ushort length = BitConverter.ToUInt16(buffer, 0);

                if (length == 312)
                {

                    ushort type = BitConverter.ToUInt16(buffer, 2);
                    byte[] temp = new byte[16];
                    if (type == 1636)
                    {
                        MemoryStream MS = new MemoryStream(buffer);
                        BinaryReader BR = new BinaryReader(MS);
                        BR.ReadUInt16();
                        BR.ReadUInt16();
                        Username = Encoding.Default.GetString(BR.ReadBytes(32));
                        Username = Username.Replace("\0", "");
                        BR.ReadBytes(37);
                        byte[] PasswordArray = BR.ReadBytes(32);
                        Password = RecoverPassword(buffer, PasswordArray, passwordSeed, Username,
                            out PasswordMethod);
                        BR.ReadBytes(31);
                        Server = Encoding.Default.GetString(BR.ReadBytes(16));
                        Server = Server.Replace("\0", "");
                        BR.Close();
                        MS.Close();
                    }
                }
            }
        }
        #endregion
        #region Desearilze normal csv3
        /*public void Deserialize(byte[] buffer)
        {
            if (buffer.Length == 312)
            {
                ushort length = BitConverter.ToUInt16(buffer, 0);

                if (length == 312)
                {

                    ushort type = BitConverter.ToUInt16(buffer, 2);
                    byte[] temp = new byte[16];
                    if (type == 1542)
                    {
                        MemoryStream MS = new MemoryStream(buffer);
                        BinaryReader BR = new BinaryReader(MS);
                        BR.ReadUInt16();
                        BR.ReadUInt16();
                        Username = Encoding.Default.GetString(BR.ReadBytes(32));
                        Username = Username.Replace("\0", "");
                        BR.ReadBytes(36);
                        Password = Encoding.Default.GetString(BR.ReadBytes(32));
                        Password = Password.Replace("\0", "");
                        BR.ReadBytes(32);
                        Server = Encoding.Default.GetString(BR.ReadBytes(32));
                        Server = Server.Replace("\0", "");
                        BR.Close();
                        MS.Close();
                    }
                }
            }
        }*/
        #endregion
        static readonly byte[] ScanMap =
        {
            0x00, 0x00, 0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39, 0x30,
            0x2D, 0x00, 0x00, 0x00, 0x71, 0x77, 0x65, 0x72, 0x74, 0x79, 0x75, 0x69,
            0x6F, 0x70, 0x00, 0x00, 0x00, 0x00, 0x61, 0x73, 0x64, 0x66, 0x67, 0x68,
            0x6A, 0x6B, 0x6C, 0x00, 0x00, 0x00, 0x00, 0x00, 0x7A, 0x78, 0x63, 0x76,
            0x62, 0x6E, 0x6D, 0x00, 0x2E, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x37,
            0x38, 0x39, 0x00, 0x34, 0x35, 0x36, 0x00, 0x31, 0x32, 0x33, 0x30, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x21, 0x40,
            0x23, 0x24, 0x25, 0x5E, 0x26, 0x2A, 0x28, 0x29, 0x5F, 0x00, 0x00, 0x00,
            0x51, 0x57, 0x45, 0x52, 0x54, 0x59, 0x55, 0x49, 0x4F, 0x50, 0x00, 0x00,
            0x00, 0x00, 0x41, 0x53, 0x44, 0x46, 0x47, 0x48, 0x4A, 0x4B, 0x4C, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x5A, 0x58, 0x43, 0x56, 0x42, 0x4E, 0x4D, 0x00,
            0x3E
        };

        public static bool MatchesStored(byte[] buffer, int seed, string username, string stored, out string method)
        {
            method = "none";
            if (buffer == null || buffer.Length < 105 || string.IsNullOrEmpty(stored)) return false;
            byte[] field73 = new byte[32];
            Buffer.BlockCopy(buffer, 73, field73, 0, 32);
            if (CandidateEquals(field73, seed, username, stored, out method)) return true;
            if (buffer.Length >= 148)
            {
                byte[] field132 = new byte[16];
                Buffer.BlockCopy(buffer, 132, field132, 0, 16);
                string found;
                if (CandidateEquals(field132, seed, username, stored, out found))
                {
                    method = found.Replace("73-", "132-");
                    return true;
                }
            }
            method = "none";
            return false;
        }

        static bool CandidateEquals(byte[] field, int seed, string username, string stored, out string method)
        {
            string text;
            if (TryPrintable(field, out text) && text == stored) { method = "73-plain"; return true; }
            if (TryLoader(field, out text) && text == stored) { method = "73-loader"; return true; }
            if (TrySeededRc5(field, seed, out text) && text == stored) { method = "73-rc5"; return true; }
            if (TrySeededRc5Scan(field, seed, username, out text) && text == stored) { method = "73-rc5scan"; return true; }
            if (TryFixedRc5(field, out text) && text == stored) { method = "73-fixed-rc5"; return true; }
            method = "none";
            return false;
        }

        static string RecoverPassword(byte[] buffer, byte[] field73, int seed, string username,
            out string method)
        {
            string text;
            if (TryPrintable(field73, out text)) { method = "73-plain"; return text; }
            if (TryLoader(field73, out text)) { method = "73-loader"; return text; }
            if (TrySeededRc5(field73, seed, out text)) { method = "73-rc5"; return text; }
            if (TrySeededRc5Scan(field73, seed, username, out text)) { method = "73-rc5scan"; return text; }
            if (TryFixedRc5(field73, out text)) { method = "73-fixed-rc5"; return text; }
            byte[] field132 = new byte[16];
            if (buffer != null && buffer.Length >= 148)
            {
                Buffer.BlockCopy(buffer, 132, field132, 0, 16);
                if (TryPrintable(field132, out text)) { method = "132-plain"; return text; }
                if (TrySeededRc5(field132, seed, out text)) { method = "132-rc5"; return text; }
                if (TrySeededRc5Scan(field132, seed, username, out text)) { method = "132-rc5scan"; return text; }
                if (TryFixedRc5(field132, out text)) { method = "132-fixed-rc5"; return text; }
            }
            method = "none";
            return "";
        }

        static bool TryPrintable(byte[] data, out string text)
        {
            text = "";
            if (data == null || data.Length == 0) return false;
            int end = 0;
            while (end < data.Length && data[end] >= 0x21 && data[end] <= 0x7E) end++;
            if (end < 1 || end > 16) return false;
            for (int i = end; i < data.Length; i++)
                if (data[i] != 0) return false;
            text = Encoding.ASCII.GetString(data, 0, end);
            return true;
        }

        static bool TryFixedRc5(byte[] field, out string text)
        {
            return TryRc5(field, new Network.RivestCipher5(), null, out text);
        }

        static bool TrySeededRc5(byte[] field, int seed, out string text)
        {
            return TryRc5(field, new Network.RivestCipher5(seed), null, out text);
        }

        static bool TrySeededRc5Scan(byte[] field, int seed, string username, out string text)
        {
            return TryRc5(field, new Network.RivestCipher5(seed), username, out text);
        }

        static bool TryRc5(byte[] field, Network.RivestCipher5 cipher, string username, out string text)
        {
            text = "";
            byte[] block = First16(field);
            if (!cipher.Decrypt(block)) return false;
            if (username != null)
                DecodeScanCodes(username, block);
            return TryPrintable(block, out text);
        }

        static bool TryLoader(byte[] field, out string text)
        {
            byte[] work = (byte[])field.Clone();
            int len = 0;
            while (len < work.Length && work[len] != 0) len++;
            if (len == 0) len = work.Length;
            byte[] key =
            {
                4, 5, 1, 3, 66, 7, 77, 44, 100, 228, 21, 254, 234, 212, 114, 141,
                214, 12, 55, 99, 100, 7, 98, 187, 190, 77, 65, 55, 44, 43, 21, 99
            };
            byte[] key2 =
            {
                6, 4, 1, 7, 2, 33, 77, 66, 65, 44, 21, 254, 43, 212, 90, 44,
                214, 12, 56, 99, 67, 7, 87, 99, 0, 77, 43, 11, 44, 22, 21, 99
            };
            for (int i = 0; i < len && i < work.Length; i++)
            {
                work[i] ^= key2[88 * i & 0x1F];
                work[i] ^= key[32 * i & 0x1C];
            }
            return TryPrintable(work, out text);
        }

        static byte[] First16(byte[] field)
        {
            byte[] block = new byte[16];
            int n = field == null ? 0 : Math.Min(16, field.Length);
            if (n > 0) Buffer.BlockCopy(field, 0, block, 0, n);
            return block;
        }

        static void DecodeScanCodes(string username, byte[] password)
        {
            if (string.IsNullOrEmpty(username) || password == null || password.Length == 0)
                return;
            int seed = 0;
            for (int n = 0; n < username.Length; n++)
                seed += (byte)username[n];
            byte[] seeds = new byte[16];
            for (int i = 0; i < seeds.Length; i++)
            {
                seed *= 0x343fd;
                seed += 0x269ec3;
                seeds[i] = (byte)((seed >> 16) & 0x7fff);
            }
            byte[] sub = new byte[0x200];
            for (int i = 1; i < 0x100; i++)
            {
                sub[i * 2] = (byte)i;
                sub[(i * 2) + 1] = (byte)(i ^ seeds[i & 15]);
            }
            for (int k = 1; k < 0x100; k++)
            {
                for (int m = k + 1; m < 0x100; m++)
                {
                    int a = m * 2, b = k * 2;
                    if (sub[b + 1] < sub[a + 1])
                    {
                        byte t0 = sub[b]; sub[b] = sub[a]; sub[a] = t0;
                        byte t1 = sub[b + 1]; sub[b + 1] = sub[a + 1]; sub[a + 1] = t1;
                    }
                }
            }
            for (int i = 0; i < password.Length; i++)
            {
                if (password[i] == 0)
                {
                    for (int z = i; z < password.Length; z++)
                        password[z] = 0;
                    return;
                }
                int position = sub[password[i] * 2];
                password[i] = ScanMap[position % ScanMap.Length];
            }
        }

        public byte[] ToArray()
        {
            throw new NotImplementedException();
        }
        public void Send(Client.GameState client)
        {
            throw new NotImplementedException();
        }
    }
}
