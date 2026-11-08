using System;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using Revora.Network.AuthPackets;

internal static class SrpVerifierTests
{
    static readonly BigInteger N = BigInteger.Parse("0894B645E89E1535BBDAD5B8B290650530801B18EBFBF5E8FAB3C82872A3E9BB7", NumberStyles.HexNumber);
    static int checks;
    static void Check(bool condition, string label) { if (!condition) throw new Exception(label); checks++; }
    static byte[] Join(params byte[][] arrays) { return arrays.SelectMany(x => x).ToArray(); }
    static byte[] Hash(byte[] bytes) { using (var hash = SHA1.Create()) return hash.ComputeHash(bytes); }
    static BigInteger Number(byte[] bytes) { return new BigInteger(Join(bytes, new byte[] { 0 })); }
    static byte[] Minimal(BigInteger n) { var b = n.ToByteArray(); int size = b.Length; while (size > 1 && b[size - 1] == 0) size--; return b.Take(size).ToArray(); }
    static byte[] Trim(byte[] b) { return Minimal(Number(b)); }
    static BigInteger Positive(BigInteger n) { return (n % N + N) % N; }

    // Independent client side of the existing custom 6711 exchange. It receives
    // only the public challenge, username and synthetic password, never server b.
    internal static byte[] Proof(byte[] challenge, string user, string password, out byte[] publicA)
    {
        byte[] salt = challenge.Skip(392).Take(challenge[391]).ToArray();
        byte[] publicB = challenge.Skip(5).Take(challenge[4]).ToArray();
        byte[] suffix = { 163,172,161,163,102,100,106,102,44,106,107,103,102,107,108 };
        byte[] digest;
        using (var md5 = MD5.Create()) digest = md5.ComputeHash(Join(Encoding.UTF8.GetBytes(password), suffix));
        BigInteger x = Number(Hash(Join(salt, new byte[4], digest)));
        byte[] gPad = new byte[20]; gPad[19] = 9;
        BigInteger k = Number(Hash(Join(Minimal(N), gPad)));
        BigInteger a = BigInteger.Parse("12345678901234567890123456789");
        publicA = Minimal(BigInteger.ModPow(9, a, N));
        BigInteger u = Number(Hash(Join(publicA, Trim(publicB))));
        BigInteger shared = BigInteger.ModPow(Positive(Number(publicB) - k * BigInteger.ModPow(9, x, N)), a + u * x, N);
        byte[] fixedShared = new byte[32]; byte[] minimal = Minimal(shared); Array.Copy(minimal, fixedShared, minimal.Length);
        byte[] even = new byte[16], odd = new byte[16];
        for (int i = 0; i < 16; i++) { even[i] = fixedShared[i * 2]; odd[i] = fixedShared[i * 2 + 1]; }
        byte[] hEven = Hash(even), hOdd = Hash(odd), key = new byte[40];
        for (int i = 0; i < 20; i++) { key[i * 2] = hEven[i]; key[i * 2 + 1] = hOdd[i]; }
        byte[] xor = Hash(Minimal(N)), gh = Hash(new byte[] { 9 });
        for (int i = 0; i < 20; i++) xor[i] ^= gh[i];
        return Hash(Join(Trim(xor), Trim(Hash(Encoding.UTF8.GetBytes(user))), salt, publicA, Trim(publicB), key));
    }
    static void Reject(byte[] salt, byte[] verifier)
    {
        bool rejected = false;
        try { Srp6711Exchange.BeginWithVerifier("ordinary", salt, verifier); }
        catch (ArgumentException) { rejected = true; }
        Check(rejected, "malformed stored credential rejected");
    }
    static int Main()
    {
        foreach (string password in new[] { "synthetic-one", "semi;quote'\"", "UTF8-\u00e9-\u4e2d" })
        {
            byte[] salt, verifier, otherSalt, otherVerifier;
            Srp6711Exchange.CreateVerifier(password, out salt, out verifier);
            Srp6711Exchange.CreateVerifier(password, out otherSalt, out otherVerifier);
            Check(salt.Length == 32 && verifier.Length == 32, "fixed persisted representation");
            Check(!salt.SequenceEqual(otherSalt) && !verifier.SequenceEqual(otherVerifier), "independent credential salts");
            var exchange = Srp6711Exchange.BeginWithVerifier("ordinary", salt, verifier);
            byte[] challenge = exchange.CreateChallenge();
            Check(challenge.Length == 653 && BitConverter.ToUInt16(challenge, 2) == 1213 && challenge[391] == 32, "unchanged challenge layout");
            var reloaded = Srp6711Exchange.BeginWithVerifier("ordinary", (byte[])salt.Clone(), (byte[])verifier.Clone());
            Check(!exchange.PublicB.SequenceEqual(reloaded.PublicB), "fresh exchange with persisted credential");
            byte[] a; byte[] proof = Proof(challenge, "ordinary", password, out a);
            Array.Clear(salt, 0, salt.Length); Array.Clear(verifier, 0, verifier.Length);
            Check(exchange.TryVerify(a, proof), "independent client authenticates despite caller-buffer mutation");
            Check(!exchange.TryVerify(a, proof), "successful proof cannot replay");
            Check(!reloaded.TryVerify(a, proof), "proof cannot cross exchanges");
            byte[] reloadProof = Proof(reloaded.CreateChallenge(), "ordinary", password, out a);
            Check(!reloaded.TryVerify(a, reloadProof), "failed attempt consumes exchange");
            var fresh = Srp6711Exchange.BeginWithVerifier("ordinary", otherSalt, otherVerifier);
            proof = Proof(fresh.CreateChallenge(), "ordinary", password, out a);
            Check(fresh.TryVerify(a, proof), "reconstructed credential authenticates on new exchange");
            var wrong = Srp6711Exchange.BeginWithVerifier("ordinary", otherSalt, otherVerifier);
            proof = Proof(wrong.CreateChallenge(), "ordinary", "wrong", out a);
            Check(!wrong.TryVerify(a, proof), "wrong password rejected");
            var wrongName = Srp6711Exchange.BeginWithVerifier("ordinary", otherSalt, otherVerifier);
            proof = Proof(wrongName.CreateChallenge(), "another", password, out a);
            Check(!wrongName.TryVerify(a, proof), "proof bound to username");
            var legacy = Srp6711Exchange.Begin("ordinary", password);
            proof = Proof(legacy.CreateChallenge(), "ordinary", password, out a);
            Check(legacy.TryVerify(a, proof), "existing Begin API remains compatible");
        }
        byte[] goodSalt, goodVerifier;
        Srp6711Exchange.CreateVerifier("synthetic", out goodSalt, out goodVerifier);
        Reject(null, goodVerifier); Reject(new byte[31], goodVerifier); Reject(new byte[33], goodVerifier);
        Reject(goodSalt, null); Reject(goodSalt, new byte[31]); Reject(goodSalt, new byte[33]);
        Reject(goodSalt, new byte[32]);
        byte[] one = new byte[32]; one[0] = 1; Reject(goodSalt, one);
        Reject(goodSalt, Minimal(N)); Reject(goodSalt, Enumerable.Repeat((byte)255, 32).ToArray());
        Console.WriteLine("SRP verifier checks passed: " + checks);
        return 0;
    }
}
