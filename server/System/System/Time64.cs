using System;
using System.Runtime.InteropServices;

namespace System
{
    public struct Time64
    {
        private const string nowDebug = "The last value of Time64.Now was greater than the current value generated during this call. This is likely due to a reset in the 49.71 days period. See http://msdn.microsoft.com/en-us/library/dd757629(VS.85).aspx for more information.";
        private uint value;
        private static uint lastValue;
        public int Value
        {
            get
            {
                return (int)this.value;
            }
        }
        public static Time64 Now
        {
            get
            {
                Time64 current = Time64.timeGetTime();
                Time64.lastValue = current.value;
                return current;
            }
        }
        public int AllMilliseconds
        {
            get
            {
                return this.GetHashCode();
            }
        }
        public int AllSeconds
        {
            get
            {
                return this.AllMilliseconds / 1000;
            }
        }
        public int AllMinutes
        {
            get
            {
                return this.AllSeconds / 60;
            }
        }
        public int AllHours
        {
            get
            {
                return this.AllMinutes / 60;
            }
        }
        public int AllDays
        {
            get
            {
                return this.AllHours / 24;
            }
        }
        public Time64(int Value)
        {
            this.value = (uint)Value;
        }
        public Time64(uint Value)
        {
            this.value = Value;
        }
        public Time64(long Value)
        {
            this.value = (uint)Value;
        }
        public Time64 AddMilliseconds(int Amount)
        {
            return new Time64((long)((ulong)this.value + (ulong)((long)Amount)));
        }
        public Time64 AddSeconds(int Amount)
        {
            return this.AddMilliseconds(Amount * 1000);
        }
        public Time64 AddMinutes(int Amount)
        {
            return this.AddSeconds(Amount * 60);
        }
        public Time64 AddHours(int Amount)
        {
            return this.AddMinutes(Amount * 60);
        }
        public Time64 AddDays(int Amount)
        {
            return this.AddHours(Amount * 24);
        }
        public override bool Equals(object obj)
        {
            if (obj is Time64)
            {
                return (Time64)obj == this;
            }
            return base.Equals(obj);
        }
        public override string ToString()
        {
            return this.value.ToString();
        }
        public override int GetHashCode()
        {
            return (int)this.value;
        }
        public static bool operator ==(Time64 t1, Time64 t2)
        {
            return t1.value == t2.value;
        }
        public static bool operator !=(Time64 t1, Time64 t2)
        {
            return t1.value != t2.value;
        }
        public static bool operator >(Time64 t1, Time64 t2)
        {
            return t1.value > t2.value;
        }
        public static bool operator <(Time64 t1, Time64 t2)
        {
            return t1.value < t2.value;
        }
        public static bool operator >=(Time64 t1, Time64 t2)
        {
            return t1.value >= t2.value;
        }
        public static bool operator <=(Time64 t1, Time64 t2)
        {
            return t1.value <= t2.value;
        }
        public static Time64 operator -(Time64 t1, Time64 t2)
        {
            return new Time64(t1.value - t2.value);
        }
        public bool Next(int due = 0, int time = 0)
        {
            if (time == 0)
            {
                time = (int)Time64.timeGetTime().Value;
            }
            return (ulong)this.Value + (ulong)((long)due) <= (ulong)((long)time);
        }
        [DllImport("winmm.dll")]
        public static extern Time64 timeGetTime();
    }
}