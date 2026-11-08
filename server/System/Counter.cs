// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

namespace Revora
{
    public class Counter
    {
        private readonly object gate = new object();
        private uint now;
        uint Start = 0;
        uint finish = uint.MaxValue;
        public uint Finish
        {
            get { lock (gate) return finish; }
            set { lock (gate) finish = value; }
        }
        public uint Now
        {
            get { lock (gate) return now; }
            set { lock (gate) now = value; }
        }
        public uint Next
        {
            get
            {
                lock (gate)
                {
                    now++;
                    if (now == finish) now = Start;
                    return now;
                }
            }
        }
        public Counter()
        {
            Now = Start;
        }
        // Reserved identities must never wrap back to an earlier value.
        internal uint NextStrict
        {
            get
            {
                lock (gate)
                {
                    if (finish == 0 || now >= finish - 1 || now == uint.MaxValue) throw new System.OverflowException("Identity counter exhausted.");
                    return ++now;
                }
            }
        }
        internal void EnsureAtLeast(uint minimum)
        {
            lock (gate)
            {
                if (finish == 0 || minimum >= finish - 1) throw new System.OverflowException("Identity counter exhausted.");
                if (now < minimum) now = minimum;
            }
        }
        public Counter(uint startFrom)
        {
            Start = startFrom;
            Now = startFrom;
        }
    }
}
