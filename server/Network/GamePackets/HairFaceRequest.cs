using System;

namespace Revora.Network.GamePackets
{
    // Confirmed 6711 request envelope. Paid mutations remain independently
    // authorized by the canonical character ownership service.
    internal sealed class HairFaceRequest
    {
        internal uint Action, First, Second;
        internal byte ParameterField;
        internal static bool TryRead(byte[] frame, out HairFaceRequest request)
        {
            request = null;
            if (frame == null || frame.Length < 14 || frame.Length > 128
                || BitConverter.ToUInt16(frame, 0) != frame.Length - 8
                || BitConverter.ToUInt16(frame, 2) != 3510
                || BitConverter.ToUInt64(frame, frame.Length - 8) != PacketHandler.ClientSeal) return false;
            int offset = 4, end = frame.Length - 8;
            var value = new HairFaceRequest(); bool action = false, parameter = false;
            while (offset < end) {
                uint tag, scalar;
                if (!Varint(frame, ref offset, end, out tag)) return false;
                if (tag == 8) {
                    if (action || !Varint(frame, ref offset, end, out scalar)) return false;
                    value.Action = scalar; action = true;
                } else if (tag == 18 || tag == 26) {
                    if (parameter || !Varint(frame, ref offset, end, out scalar) || scalar > end - offset) return false;
                    int stop = offset + (int)scalar;
                    bool first = false, second = false;
                    while (offset < stop) {
                        uint field, number;
                        if (!Varint(frame, ref offset, stop, out field) || !Varint(frame, ref offset, stop, out number)) return false;
                        if (field == 8 && !first) { value.First = number; first = true; }
                        else if (field == 16 && !second) { value.Second = number; second = true; }
                        else return false;
                    }
                    if (!first || !second) return false;
                    value.ParameterField = (byte)(tag >> 3); parameter = true;
                } else return false; // Client-supplied rules and owned lists are not requests.
            }
            if (!action) return false;
            // Business authorization and catalog bounds belong to the owner service.
            // Both known selection/acquisition pairs require their distinct message.
            if (value.Action == 3 || value.Action == 4) {
                if (value.ParameterField != 2 || value.First > 1 || value.Second == 0 || value.Second > ushort.MaxValue) return false;
            } else if (value.Action == 6 || value.Action == 7) {
                if (value.ParameterField != 3 || value.First == 0 || value.First > ushort.MaxValue || value.Second > 6
                    || value.Action == 7 && value.Second == 0) return false;
            } else if (value.Action == 0 || value.Action == 8 || value.Action == 9) {
                if (parameter) return false;
            } else return false;
            request = value; return true;
        }
        private static bool Varint(byte[] bytes, ref int offset, int end, out uint value)
        {
            value = 0;
            for (int shift = 0; shift <= 28; shift += 7) {
                if (offset >= end) return false;
                byte part = bytes[offset++];
                if (shift == 28 && (part & 0xf0) != 0) return false;
                value |= (uint)(part & 127) << shift;
                if ((part & 128) == 0) return shift == 0 || part != 0;
            }
            return false;
        }
    }
}
