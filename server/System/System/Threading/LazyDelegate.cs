// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

namespace System.Threading
{
    public class LazyDelegate : TimerRule
    {
        public LazyDelegate(Action<int> action, int dueTime, ThreadPriority priority = ThreadPriority.Normal)
            : base(action, dueTime, priority)
        {
            this.bool_0 = false;
        }
    }
}