using System;
using System.Collections.Generic;
using System.IO;
using Revora.Game.Features;

namespace Revora.Network.GamePackets
{
    // Original encoder from the documented numeric 6711 wire contract.
    // Reserved transport bytes are completed by the normal send path.
    internal static class HairFaceResponse
    {
        private static void Scalar(Stream s, uint field, uint value)
        { Varint(s, field << 3); Varint(s, value); }
        private static void Varint(Stream s, uint value)
        {
            while (value >= 128) { s.WriteByte((byte)((value & 127) | 128)); value >>= 7; }
            s.WriteByte((byte)value);
        }
        private static void Child(Stream s, uint field, byte[] bytes)
        { Varint(s, (field << 3) | 2); Varint(s, (uint)bytes.Length); s.Write(bytes, 0, bytes.Length); }
        private static byte[] Frame(byte[] payload)
        {
            if (payload.Length > 1020) throw new InvalidOperationException("Cosmetic response exceeds the native message buffer.");
            var frame = new byte[payload.Length + 12];
            Writer.WriteUInt16((ushort)(payload.Length + 4), 0, frame); Writer.WriteUInt16(3510, 2, frame);
            Buffer.BlockCopy(payload, 0, frame, 4, payload.Length); return frame;
        }
        internal static byte[] Pair(uint action, uint first, uint second)
        {
            if (action != 3 && action != 5 && action != 6) throw new ArgumentOutOfRangeException("action");
            if (action == 3 ? first > 1 || second == 0 || second > ushort.MaxValue
                : first == 0 || first >= 1000 || second > 6 || action == 5 && second == 0)
                throw new ArgumentOutOfRangeException("first");
            using (var s = new MemoryStream()) using (var child = new MemoryStream()) {
                Scalar(s, 1, action); Scalar(child, 1, first); Scalar(child, 2, second);
                Child(s, action == 3 ? 2u : 3u, child.ToArray()); return Frame(s.ToArray());
            }
        }
        internal static byte[] Rules(uint action, uint vip)
        {
            if (action != 8 && action != 9) throw new ArgumentOutOfRangeException("action");
            using (var s = new MemoryStream()) using (var child = new MemoryStream()) {
                Scalar(s, 1, action);
                for (uint i = 1; i <= 5; i++) Scalar(child, i, i == 5 ? vip : 0);
                Child(s, 4, child.ToArray()); return Frame(s.ToArray());
            }
        }
        internal static byte[][] Entries(uint action, CosmeticOwnership.Owned[] entries)
        {
            if (action != 0 && action != 1 || entries == null || entries.Length > 1024 || action == 1 && entries.Length != 1)
                throw new ArgumentException("Invalid cosmetic response entries.");
            var result = new List<byte[]>();
            for (int start = 0; start < Math.Max(1, entries.Length); start += 30) using (var s = new MemoryStream()) {
                Scalar(s, 1, action);
                for (int i = start; i < Math.Min(start + 30, entries.Length); i++) {
                    var entry = entries[i];
                    if (entry == null || entry.Kind > 1 || entry.Id == 0 || entry.Id > ushort.MaxValue || entry.Colour > 6
                        || entry.Colours > 127 || (entry.Colours & 1) == 0 || (entry.Colours & (1u << (int)entry.Colour)) == 0
                        || entry.Kind == 0 && entry.Id >= 1000 || entry.Kind == 1 && (entry.Colour != 0 || entry.Colours != 1))
                        throw new ArgumentException("Invalid owned cosmetic response.");
                    using (var child = new MemoryStream()) {
                        Scalar(child, 1, entry.Kind); Scalar(child, 2, entry.Id); Scalar(child, 3, entry.Active ? 1u : 0);
                        Scalar(child, 4, 0); // Permanent; timed expiry encoding is deliberately not inferred.
                        Scalar(child, 5, entry.Colour); Scalar(child, 5, entry.Colours);
                        Child(s, 5, child.ToArray());
                    }
                }
                result.Add(Frame(s.ToArray()));
            }
            return result.ToArray();
        }
    }
}
