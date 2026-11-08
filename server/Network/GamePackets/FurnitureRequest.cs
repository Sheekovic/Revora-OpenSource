using System;

namespace Revora.Network.GamePackets
{
    // Separate from outbound NPC spawns: the native preview emits no actor UID.
    // Decoding does not establish house ownership or authorize an item operation.
    internal static class FurnitureRequest
    {
        internal struct Placement { internal ushort X, Y, Look; }

        private static bool Fields(byte[] frame, ushort packet, int mask, out uint[] values)
        {
            values = null;
            if (frame == null || frame.Length < 12 || frame.Length > 40
                || System.BitConverter.ToUInt16(frame, 0) != frame.Length - 8
                || System.BitConverter.ToUInt16(frame, 2) != packet
                || System.BitConverter.ToUInt64(frame, frame.Length - 8) != PacketHandler.ClientSeal) return false;
            var result = new uint[7];
            int seen = 0, position = 4, end = frame.Length - 8;
            while (position < end)
            {
                // Confirmed fields have one-byte tags and unsigned varint values.
                int tag = frame[position++], field = tag >> 3;
                if ((tag & 7) != 0 || field < 1 || field > 6 || (mask & (1 << field)) == 0
                    || (seen & (1 << field)) != 0) return false;
                uint value = 0;
                bool complete = false;
                for (int shift = 0; shift <= 28 && position < end; shift += 7)
                {
                    byte part = frame[position++];
                    if (shift == 28 && part > 15) return false;
                    value |= (uint)(part & 127) << shift;
                    if ((part & 128) == 0)
                    {
                        if (shift != 0 && part == 0) return false; // Noncanonical overlong integer.
                        complete = true;
                        break;
                    }
                }
                if (!complete) return false;
                result[field] = value;
                seen |= 1 << field;
            }
            if (seen != mask) return false;
            values = result;
            return true;
        }

        internal static bool TryPlacement(byte[] frame, out Placement request)
        {
            request = default(Placement);
            uint[] fields;
            if (!Fields(frame, 2030, (1 << 4) | (1 << 5) | (1 << 6), out fields)
                || fields[4] > ushort.MaxValue || fields[5] > ushort.MaxValue
                || fields[6] == 0 || fields[6] > ushort.MaxValue) return false;
            request = new Placement { X = (ushort)fields[4], Y = (ushort)fields[5], Look = (ushort)fields[6] };
            return true;
        }

        internal static bool TryRemoval(byte[] frame, out uint actor)
        {
            return TryActorAction(frame, 6, out actor);
        }

        // Native ordinary NPC clicks use action zero and preserve the full actor UID.
        // The box service must separately establish ownership, map and proximity.
        internal static bool TryInteraction(byte[] frame, out uint actor)
        {
            return TryActorAction(frame, 0, out actor);
        }

        private static bool TryActorAction(byte[] frame, uint action, out uint actor)
        {
            actor = 0;
            uint[] fields;
            if (!Fields(frame, 2031, (1 << 1) | (1 << 2) | (1 << 3) | (1 << 4), out fields)
                || fields[1] != action || fields[2] == 0 || fields[3] != 0 || fields[4] != 0) return false;
            actor = fields[2];
            return true;
        }
    }
}
