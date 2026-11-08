using System;
using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace Revora.Network.AuthPackets
{
    public sealed class Srp6711Exchange
    {
        public const int PublicValueSize = 32;
        public const int ProofSize = 20;
        public const int SaltSize = 32;
        public const int ChallengeSize = 653;
        public const int ProofPacketSize = 262;

        static readonly string ModulusHex =
            "894B645E89E1535BBDAD5B8B290650530801B18EBFBF5E8FAB3C82872A3E9BB7";
        static readonly byte[] PasswordSuffix =
        {
            0xA3, 0xAC, 0xA1, 0xA3, 0x66, 0x64, 0x6A, 0x66,
            0x2C, 0x6A, 0x6B, 0x67, 0x66, 0x6B, 0x6C
        };

        static readonly BigInteger Modulus = BigInteger.Parse("0" + ModulusHex, NumberStyles.AllowHexSpecifier);
        static readonly BigInteger Generator = new BigInteger(9);

        readonly string username;
        readonly byte[] salt;
        readonly BigInteger verifier;
        readonly BigInteger privateB;
        bool consumed;

        public byte[] PublicB;
        public uint Confirmation;

        Srp6711Exchange(string username, byte[] salt, BigInteger verifier, BigInteger privateB, byte[] publicB)
        {
            this.username = username;
            this.salt = salt;
            this.verifier = verifier;
            this.privateB = privateB;
            PublicB = publicB;
        }

        public static Srp6711Exchange Begin(string username, string password)
        {
            byte[] salt, storedVerifier;
            CreateVerifier(password, out salt, out storedVerifier);
            try { return BeginWithVerifier(username, salt, storedVerifier); }
            finally { Array.Clear(storedVerifier, 0, storedVerifier.Length); }
        }

        // Store these two values, never the supplied password. This preserves
        // the existing 6711 algorithm; it is not a general password-hash API.
        public static void CreateVerifier(string password, out byte[] salt, out byte[] storedVerifier)
        {
            salt = RandomBytes(SaltSize);
            byte[] passwordBytes = Encoding.UTF8.GetBytes(password ?? "");
            try
            {
                BigInteger x = DerivePrivateKey(passwordBytes, salt);
                storedVerifier = ToFixedLittleEndian(BigInteger.ModPow(Generator, x, Modulus), PublicValueSize);
            }
            finally { Array.Clear(passwordBytes, 0, passwordBytes.Length); }
        }

        internal static bool IsValidStoredVerifier(byte[] value)
        {
            if (value == null || value.Length != PublicValueSize) return false;
            BigInteger parsed = FromLittleEndian(value);
            return parsed > BigInteger.One && parsed < Modulus;
        }

        public static Srp6711Exchange BeginWithVerifier(string username, byte[] storedSalt, byte[] storedVerifier)
        {
            if (storedSalt == null || storedSalt.Length != SaltSize)
                throw new ArgumentException("Invalid 6711 salt size.", "storedSalt");
            if (!IsValidStoredVerifier(storedVerifier))
                throw new ArgumentException("Invalid 6711 verifier size.", "storedVerifier");
            BigInteger verifier = FromLittleEndian(storedVerifier);
            if (verifier <= BigInteger.One || verifier >= Modulus)
                throw new ArgumentException("Invalid 6711 verifier value.", "storedVerifier");
            byte[] salt = (byte[])storedSalt.Clone();
            byte[] privateBytes;
            BigInteger b;
            do
            {
                privateBytes = RandomBytes(PublicValueSize);
                b = FromLittleEndian(privateBytes);
            }
            while (b <= BigInteger.Zero || b >= Modulus);
            Array.Clear(privateBytes, 0, privateBytes.Length);
            BigInteger multiplier = DeriveMultiplier();
            BigInteger publicB = PositiveMod(multiplier * verifier + BigInteger.ModPow(Generator, b, Modulus), Modulus);
            if (publicB.IsZero)
                throw new CryptographicException("invalid SRP public B");
            return new Srp6711Exchange(username, salt, verifier, b, ToMinimalLittleEndian(publicB));
        }

        public byte[] CreateChallenge()
        {
            byte[] result = new byte[ChallengeSize];
            Writer.WriteUInt16((ushort)ChallengeSize, 0, result);
            Writer.WriteUInt16(1213, 2, result);
            result[4] = (byte)PublicB.Length;
            Buffer.BlockCopy(PublicB, 0, result, 5, PublicB.Length);
            result[391] = (byte)salt.Length;
            Buffer.BlockCopy(salt, 0, result, 392, salt.Length);
            return result;
        }

        public static bool TryReadProof(byte[] frame, out byte[] publicA, out byte[] proof)
        {
            publicA = null;
            proof = null;
            if (frame == null || frame.Length != ProofPacketSize) return false;
            if (BitConverter.ToUInt16(frame, 0) != ProofPacketSize) return false;
            if (BitConverter.ToUInt16(frame, 2) != 1214) return false;
            int publicLength = frame[4];
            int proofLength = frame[133];
            if (publicLength < 1 || publicLength > PublicValueSize) return false;
            if (proofLength < 1 || proofLength > ProofSize) return false;
            for (int i = 5 + publicLength; i < 133; i++)
                if (frame[i] != 0) return false;
            for (int i = 134 + proofLength; i < frame.Length; i++)
                if (frame[i] != 0) return false;
            publicA = new byte[publicLength];
            Buffer.BlockCopy(frame, 5, publicA, 0, publicLength);
            proof = new byte[ProofSize];
            Buffer.BlockCopy(frame, 134, proof, 0, proofLength);
            return true;
        }

        public bool TryVerify(byte[] publicA, byte[] suppliedProof)
        {
            Confirmation = 0;
            if (consumed) return false;
            consumed = true;
            if (publicA == null || suppliedProof == null) return false;
            if (publicA.Length < 1 || publicA.Length > PublicValueSize) return false;
            if (suppliedProof.Length != ProofSize) return false;
            BigInteger a = FromLittleEndian(publicA);
            if (a <= BigInteger.Zero || a >= Modulus) return false;
            BigInteger u = FromLittleEndian(Sha1(Combine(MinimalWire(publicA), MinimalWire(PublicB))));
            if (u.IsZero) return false;
            BigInteger shared = BigInteger.ModPow(
                (a * BigInteger.ModPow(verifier, u, Modulus)) % Modulus,
                privateB, Modulus);
            byte[] sessionKey = ComputeSessionKey(shared);
            byte[] expected = ComputeClientProof(username, salt, publicA, PublicB, sessionKey);
            bool valid = BytesEqual(expected, suppliedProof);
            if (valid)
            {
                byte[] serverProof = Sha1(Combine(MinimalWire(publicA), MinimalDigest(expected), MinimalWire(sessionKey)));
                Confirmation = ComputeClientConfirmationCode(serverProof);
            }
            return valid;
        }

        static BigInteger DerivePrivateKey(byte[] passwordUtf8, byte[] salt)
        {
            byte[] input = Combine(passwordUtf8, PasswordSuffix);
            byte[] digest = Md5(input);
            byte[] passwordIntegerPadded = new byte[20];
            Buffer.BlockCopy(digest, 0, passwordIntegerPadded, 4, digest.Length);
            byte[] xInput = Combine(salt, passwordIntegerPadded);
            byte[] result = null;
            try
            {
                result = Sha1(xInput);
                return FromLittleEndian(result);
            }
            finally
            {
                Array.Clear(input, 0, input.Length);
                Array.Clear(digest, 0, digest.Length);
                Array.Clear(passwordIntegerPadded, 0, passwordIntegerPadded.Length);
                Array.Clear(xInput, 0, xInput.Length);
                if (result != null) Array.Clear(result, 0, result.Length);
            }
        }

        static BigInteger DeriveMultiplier()
        {
            byte[] generatorPadded = new byte[ProofSize];
            generatorPadded[ProofSize - 1] = 9;
            return FromLittleEndian(Sha1(Combine(ToMinimalLittleEndian(Modulus), generatorPadded)));
        }

        static byte[] ComputeSessionKey(BigInteger sharedSecret)
        {
            byte[] value = ToFixedLittleEndian(sharedSecret, PublicValueSize);
            byte[] even = new byte[PublicValueSize / 2];
            byte[] odd = new byte[PublicValueSize / 2];
            for (int i = 0; i < even.Length; i++)
            {
                even[i] = value[i * 2];
                odd[i] = value[i * 2 + 1];
            }
            byte[] left = Sha1(even);
            byte[] right = Sha1(odd);
            byte[] result = new byte[40];
            for (int i = 0; i < left.Length; i++)
            {
                result[i * 2] = left[i];
                result[i * 2 + 1] = right[i];
            }
            return result;
        }

        static byte[] ComputeClientProof(string username, byte[] salt, byte[] publicA, byte[] publicB, byte[] sessionKey)
        {
            byte[] modulusHash = Sha1(ToMinimalLittleEndian(Modulus));
            byte[] generatorHash = Sha1(ToMinimalLittleEndian(Generator));
            for (int i = 0; i < ProofSize; i++)
                modulusHash[i] ^= generatorHash[i];
            byte[] userHash = Sha1(Encoding.UTF8.GetBytes(username));
            return Sha1(Combine(
                MinimalDigest(modulusHash),
                MinimalDigest(userHash),
                salt,
                MinimalWire(publicA),
                MinimalWire(publicB),
                sessionKey));
        }

        static uint ComputeClientConfirmationCode(byte[] serverProof)
        {
            string hexadecimal = FromLittleEndian(serverProof).ToString("X").TrimStart('0');
            if (hexadecimal.Length == 0) hexadecimal = "0";
            byte[] bytes = Encoding.ASCII.GetBytes(hexadecimal);
            uint result = 0;
            uint sequence = 0x100;
            for (int i = 0; i < bytes.Length; i++)
            {
                byte value = bytes[i];
                if (value == 0) break;
                uint item = unchecked((uint)(int)(sbyte)value) | sequence;
                int shift = (int)(((item >> 2) ^ value) & 0x0F);
                sequence += 0x100;
                result = RotateLeft(result, shift) ^ unchecked(item * item);
            }
            return result ^ (result >> 16);
        }

        static uint RotateLeft(uint value, int count)
        {
            count &= 31;
            if (count == 0) return value;
            return (value << count) | (value >> (32 - count));
        }

        static BigInteger FromLittleEndian(byte[] value)
        {
            byte[] padded = new byte[value.Length + 1];
            Buffer.BlockCopy(value, 0, padded, 0, value.Length);
            return new BigInteger(padded);
        }

        static byte[] ToMinimalLittleEndian(BigInteger value)
        {
            byte[] raw = value.ToByteArray();
            int len = raw.Length;
            while (len > 1 && raw[len - 1] == 0) len--;
            if (len == raw.Length) return raw;
            byte[] trimmed = new byte[len];
            Buffer.BlockCopy(raw, 0, trimmed, 0, len);
            return trimmed;
        }

        static byte[] ToFixedLittleEndian(BigInteger value, int length)
        {
            byte[] minimal = ToMinimalLittleEndian(value);
            byte[] output = new byte[length];
            Buffer.BlockCopy(minimal, 0, output, 0, Math.Min(minimal.Length, length));
            return output;
        }

        static byte[] MinimalDigest(byte[] value)
        {
            return ToMinimalLittleEndian(FromLittleEndian(value));
        }

        static byte[] MinimalWire(byte[] value)
        {
            return ToMinimalLittleEndian(FromLittleEndian(value));
        }

        static BigInteger PositiveMod(BigInteger value, BigInteger modulus)
        {
            BigInteger result = value % modulus;
            if (result.Sign < 0) result += modulus;
            return result;
        }

        static byte[] Combine(params byte[][] values)
        {
            int size = 0;
            for (int i = 0; i < values.Length; i++)
                size += values[i].Length;
            byte[] result = new byte[size];
            int offset = 0;
            for (int i = 0; i < values.Length; i++)
            {
                Buffer.BlockCopy(values[i], 0, result, offset, values[i].Length);
                offset += values[i].Length;
            }
            return result;
        }

        static byte[] Md5(byte[] value)
        {
            using (MD5 hash = MD5.Create())
                return hash.ComputeHash(value);
        }

        static byte[] Sha1(byte[] value)
        {
            using (SHA1 hash = SHA1.Create())
                return hash.ComputeHash(value);
        }

        static byte[] RandomBytes(int length)
        {
            byte[] value = new byte[length];
            using (var rng = new RNGCryptoServiceProvider())
                rng.GetBytes(value);
            return value;
        }

        static bool BytesEqual(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length) return false;
            int diff = 0;
            for (int i = 0; i < left.Length; i++)
                diff |= left[i] ^ right[i];
            return diff == 0;
        }
    }
}
