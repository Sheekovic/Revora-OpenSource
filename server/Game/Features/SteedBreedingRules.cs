using System;

namespace Revora.Game.Features
{
    // Clean-room numeric contract from the approved 6711 client. See
    // docs/STEED_BREEDING_RESEARCH.md. Persistence and player dispatch are separate.
    internal static class SteedBreedingRules
    {
        private static readonly uint[] thresholds = {20,20,90,240,720,2160,6480,19440,58320,2700,5500,9000};
        private static readonly uint[] minorCredits = {1,10,30,120,360,1080,3240,9720,29160};

        internal static bool TryThreshold(byte level, out uint points)
        {
            points = 0;
            if (level >= thresholds.Length) return false;
            points = thresholds[level];
            return true;
        }

        internal static bool TryMinorCredit(byte level, uint progress, out uint points)
        {
            points = 0;
            // The native selection predicate rejects minor levels above eight.
            if (level >= minorCredits.Length || progress >= thresholds[level]) return false;
            points = minorCredits[level] + progress;
            return true;
        }

        internal static bool TryBlend(uint major, uint minor, out uint result)
        {
            result = 0;
            if (major > 0xffffff || minor > 0xffffff) return false;
            for (int shift = 0; shift < 24; shift += 8)
            {
                uint a = (major >> shift) & 255, b = (minor >> shift) & 255;
                // Separate integer truncation is significant; there is no +1.
                result |= ((a * 90 / 100) + (b * 10 / 100)) << shift;
            }
            return true;
        }

        internal sealed class Request
        {
            internal readonly byte Mode;
            internal readonly uint[] Items;
            internal Request(byte mode, uint[] items) { Mode = mode; Items = items; }
        }

        internal static bool TryRequest(byte[] frame, ulong clientSeal, out Request request)
        {
            request = null;
            if (frame == null || frame.Length < 24 || frame.Length > 180) return false;
            int count = frame[5];
            // Forty bag slots plus an equipped main mount. UID validation and
            // ownership must still be enforced by the eventual player service.
            if (frame[4] != 2 && frame[4] != 3 || count < 2 || count > 41) return false;
            int length = 8 + 4 * count;
            if (frame.Length != length + 8 || BitConverter.ToUInt16(frame,0) != length
                || BitConverter.ToUInt16(frame,2) != 2036
                || BitConverter.ToUInt64(frame,length) != clientSeal) return false;
            var items = new uint[count];
            for (int i = 0; i < count; i++)
            {
                items[i] = BitConverter.ToUInt32(frame,8 + 4 * i);
                if (items[i] == 0) return false;
                for (int j = 0; j < i; j++) if (items[i] == items[j]) return false;
            }
            request = new Request(frame[4],items);
            return true;
        }
    }
}
