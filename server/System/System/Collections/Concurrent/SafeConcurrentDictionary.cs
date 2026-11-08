// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace System.Collections.Concurrent
{
    public class SafeConcurrentDictionary<T, T2> : ConcurrentDictionary<T, T2>
    {
        public new T2 this[T key]
        {
            get
            {
                T2 value;
                return TryGetValue(key,out value)?value:default(T2);
            }
            set
            {
                base[key] = value;
            }
        }
    }
}
