// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Utilities.Encoders;
using CO2_CORE_DLL.Security.Cryptography;

namespace Revora.Network.GamePackets
{
    public unsafe static class DHKeyExchange
    {
        public class ServerKeyExchange
        {
            DiffieHellman _keyExchange;
            byte[] _serverIv;
            byte[] _clientIv;

            public byte[] CreateServerKeyPacket()
            {
                _clientIv = new byte[8];
                _serverIv = new byte[8];
                string P = "E7A69EBDF105F2A6BBDEAD7E798F76A209AD73FB466431E2E7352ED262F8C558F10BEFEA977DE9E21DCEE9B04D245F300ECCBBA03E72630556D011023F9E857F";
                string G = "05";
                _keyExchange = new DiffieHellman(P, G);
                return GeneratePacket(_serverIv, _clientIv, P, G, _keyExchange.GenerateRequest());
            }
            public Cryptography.GameCryptography HandleClientKeyPacket(string PublicKey, Cryptography.GameCryptography cryptographer)
            {
                _keyExchange.HandleResponse(PublicKey);
                byte[] data = _keyExchange.ToBytes();
                var md5 = new MD5Digest();
                var firstRun = new byte[md5.GetDigestSize() * 2];
                md5.BlockUpdate(data, 0, data.TakeWhile(x => x != 0).Count());
                md5.DoFinal(firstRun, 0);
                Array.Copy(firstRun, 0, firstRun, md5.GetDigestSize(), md5.GetDigestSize());
                var n = Hex.Encode(firstRun);
                md5.BlockUpdate(n, 0, 32);
                md5.DoFinal(firstRun, md5.GetDigestSize());
                byte[] key = Hex.Encode(firstRun);
                var fresh = new Cryptography.GameCryptography(key);
                fresh.SetIvs(_clientIv, _serverIv);
                return fresh;
            }
            public byte[] GeneratePacket(byte[] ServerIV1, byte[] ServerIV2, string P, string G, string ServerPublicKey)
            {
                int PAD_LEN = 11;
                int _junk_len = 12;
                string tqs = "TQServer";
                MemoryStream ms = new MemoryStream();
                byte[] pad = new byte[PAD_LEN];
                Kernel.Random.NextBytes(pad);
                byte[] junk = new byte[_junk_len];
                Kernel.Random.NextBytes(junk);
                if (ServerPublicKey == null) ServerPublicKey = "";
                ServerPublicKey = ServerPublicKey.TrimStart('0');
                if (ServerPublicKey.Length == 0) ServerPublicKey = "0";
                if (ServerPublicKey.Length < 128)
                    ServerPublicKey = ServerPublicKey.PadLeft(128, '0');
                ServerPublicKey = ServerPublicKey.ToUpperInvariant();
                int size = 47 + P.Length + G.Length + ServerPublicKey.Length + 12 + 8 + 8;
                BinaryWriter bw = new BinaryWriter(ms);
                bw.Write(pad);
                bw.Write(size - PAD_LEN);
                bw.Write((UInt32)_junk_len);
                bw.Write(junk);
                bw.Write((UInt32)ServerIV2.Length);
                bw.Write(ServerIV2);
                bw.Write((UInt32)ServerIV1.Length);
                bw.Write(ServerIV1);
                bw.Write((UInt32)P.ToCharArray().Length);
                foreach (char fP in P.ToCharArray())
                {
                    bw.BaseStream.WriteByte((byte)fP);
                }
                bw.Write((UInt32)G.ToCharArray().Length);
                foreach (char fG in G.ToCharArray())
                {
                    bw.BaseStream.WriteByte((byte)fG);
                }
                bw.Write((UInt32)ServerPublicKey.ToCharArray().Length);
                foreach (char SPK in ServerPublicKey.ToCharArray())
                {
                    bw.BaseStream.WriteByte((byte)SPK);
                }
                foreach (char tq in tqs.ToCharArray())
                {
                    bw.BaseStream.WriteByte((byte)tq);
                }
                byte[] Packet = new byte[ms.Length];
                Packet = ms.ToArray();
                ms.Close();
                return Packet;
            }
        }
    }
}