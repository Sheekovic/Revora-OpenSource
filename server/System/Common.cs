using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;

namespace Emulator.Core
{
    public static class Common
    {
        public static readonly sbyte[] DeltaX, DeltaY;
        public static readonly sbyte[] DeltaMountX, DeltaMountY;
        public static readonly uint[] RebornItemLevels;
        public static readonly DateTime UnixEpoch;
        private static readonly int[] _trojanLifeBonus;
        private static readonly int[] _taoistManaBonus;
        static Common()
        {
            RebornItemLevels = new uint[] { 0, 0, 0, 0, 2, 2, 1, 0, 1, 0, 0, 0, 0, 0, 0 };
            DeltaX = new sbyte[] { 0, -1, -1, -1, 0, 1, 1, 1, 0 };
            DeltaY = new sbyte[] { 1, 1, 0, -1, -1, -1, 0, 1, 0 };
            DeltaMountX = new sbyte[] { 0, -2, -2, -2, 0, 2, 2, 2, 1, 0, -2, 0, 1, 0, 2, 0, 0, -2, 0, -1, 0, 2, 0, 1, 0 };
            DeltaMountY = new sbyte[] { 2, 2, 0, -2, -2, -2, 0, 2, 2, 0, -1, 0, -2, 0, 1, 0, 0, 1, 0, -2, 0, -1, 0, 2, 0 };
            UnixEpoch = new DateTime(1970, 1, 1);
            _trojanLifeBonus = new[] { 100, 105, 108, 110, 112, 115 };
            _taoistManaBonus = new[] { 100, 100, 300, 400, 500, 600 };
        }
        public static uint UnixTimestamp
        {
            get { return (uint)(DateTime.UtcNow - UnixEpoch).TotalSeconds; }
        }
        public static int TimeZoneOffset
        {
            get { return (int)TimeZone.CurrentTimeZone.GetUtcOffset(DateTime.Now).TotalSeconds; }
        }
        public static string TimeZoneName(DateTime dt)
        {
            var name = TimeZone.CurrentTimeZone.IsDaylightSavingTime(dt) ?
                TimeZone.CurrentTimeZone.DaylightName : TimeZone.CurrentTimeZone.StandardName;
            return name.Split(' ').Where(str => str.Length >= 1).Aggregate(string.Empty, (current, str) => current + str[0]);
        }
        public static int MulDiv(int number, int numerator, int denominator)
        {
            return number * numerator / denominator;
        }
        public static uint MulDiv(uint number, uint numerator, uint denominator)
        {
            return number * numerator / denominator;
        }
        public static long MulDiv(long number, long numerator, long denominator)
        {
            return number * numerator / denominator;
        }
        public static int GetTrojanLifeBonus(int index)
        {
            return index < 0 || index > _trojanLifeBonus.Length ? _trojanLifeBonus[0] : _trojanLifeBonus[index];
        }
        public static int GetTaoistManaBonus(int index)
        {
            return index < 0 || index > _taoistManaBonus.Length ? _taoistManaBonus[0] : _taoistManaBonus[index];
        }
        public static byte ExchangeBits(byte data, int bits)
        {
            return (byte)((data << bits) | (data >> bits));
        }
        public static uint ExchangeShortBits(uint data, int bits)
        {
            data &= 0xffff;
            return ((data >> bits) | (data << (16 - bits))) & 0xffff;
        }
        public static uint ExchangeLongBits(uint data, int bits)
        {
            return (data >> bits) | (data << (32 - bits));
        }
        public static uint TimeGet(TimeType type = TimeType.Millisecond)
        {
            var time = 0u;
            switch (type)
            {
                case TimeType.Second:
                    {
                        time = (uint)(DateTime.UtcNow - UnixEpoch).TotalSeconds;
                        break;
                    }
                case TimeType.Minute:
                    {
                        var now = DateTime.Now;
                        time = (uint)(now.Year % 100 * 100000000 + (now.Month + 1) * 1000000
                            + now.Day * 10000 + now.Hour * 100 + now.Minute);
                        break;
                    }
                case TimeType.Hour:
                    {
                        var now = DateTime.Now;
                        time = (uint)(now.Year % 100 * 1000000 + (now.Month + 1) * 10000
                            + now.Day * 100 + now.Hour);
                        break;
                    }
                case TimeType.Day:
                    {
                        var now = DateTime.Now;
                        time = (uint)(now.Year * 10000 + now.Month * 100 + now.Day);
                        break;
                    }
                case TimeType.DayTime:
                    {
                        var now = DateTime.Now;
                        time = (uint)(now.Hour * 10000 + now.Minute * 100 + now.Second);
                        break;
                    }
                case TimeType.Stamp:
                    {
                        var now = DateTime.Now;
                        time = (uint)((now.Month + 1) * 100000000 + now.Day * 1000000 +
                                       now.Hour * 10000 + now.Minute * 100 + now.Second);
                        break;
                    }
                default:
                    {
                        time = (uint)Environment.TickCount;
                        break;
                    }
            }
            return time;
        }
    }
    public enum TimeType
    {
        Millisecond = 0,
        Second,
        Minute,
        Hour,
        Day,
        DayTime,
        Stamp
    }
}