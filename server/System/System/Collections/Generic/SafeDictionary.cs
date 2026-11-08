// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

namespace System.Collections.Generic
{
    public class SafeDictionary<T, T2> : Dictionary<T, T2>
    {
        public new T2 this[T key]
        {
            get
            {
                if (this.ContainsKey(key))
                    return base[key];
                else
                    return default(T2);
            }
            set { base[key] = value; }
        }
        public SafeDictionary()
        {
        }
        public SafeDictionary(int nulledNumber)
        {
        }
        public new void Add(T key, T2 value)
        {
            base[key] = value;
        }
    }
}