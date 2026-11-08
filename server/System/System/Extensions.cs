// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace System
{
    public static class Extensions
    {
        public static T[] Transform<T>(this object[] strs)
        {
            T[] objArray = new T[strs.Length];
            for (int index = 0; index < strs.Length; ++index)
                objArray[index] = (T)Convert.ChangeType(strs[index], typeof(T));
            return objArray;
        }
        public static void Add<T, T2>(this IDictionary<T, T2> dict, T key, T2 value)
        {
            dict[key] = value;
        }
        public static void Remove<T, T2>(this IDictionary<T, T2> dict, T key)
        {
            dict.Remove(key);
        }
    }
}