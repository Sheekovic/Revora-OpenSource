using System;
using System.Linq;
using System.Text;
using Revora.Client;
using System.Threading.Generic;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace Revora
{
    public static class ClassExtensions
    {
        public static string Remove(this string haystack, string needle)
        {
            string ret = haystack.remove(needle);
            string aux = ret.remove(needle);
            while (aux != ret)
            {
                ret = aux;
                aux = aux.remove(needle);
            }
            return ret;
        }
        private static string remove(this string haystack, string needle)
        {
            int index = haystack.IndexOf(needle, StringComparison.CurrentCultureIgnoreCase);
            if (index == -1) return haystack;
            return haystack.Substring(0, index) +
                haystack.Substring(index + needle.Length, haystack.Length - index - needle.Length);
        }
        public static bool Remove<T, T2>(this ConcurrentDictionary<T, T2> dict, T key)
        {
            T2 val;
            return dict.TryRemove(key, out val);
        }
        public static void Add<T, T2>(this ConcurrentDictionary<T, T2> dict, T key, T2 value)
        {
            dict[key] = value;
        }
        public static IDisposable Add<T>(this TimerRule<T> rule, T param)
        {
            return Thread.Subscribe(rule, param);
        }
        public static T Cast<T>(this object obj)
        {
            if (obj is T) return (T)obj;
            return (T)Convert.ChangeType(obj, typeof(T));
        }
        public static ulong ConvertUL(this object obj)
        {
            if (obj is string) return ulong.Parse(obj.ToString());
            return Convert.ToUInt64(obj);
        }
        public const ulong KnuthConstant = 3074457345618258791ul;
        public static ulong Get64HashCode(this string obj)
        {
            return CalculateHash(obj);
        }
        public static ulong Get64HashCode(this object obj)
        {
            return CalculateHash(obj.GetHashCode() + obj.ToString());
        }
        static UInt64 CalculateHash(string read)
        {
            UInt64 hashedValue = KnuthConstant;
            for (int i = 0; i < read.Length; i++)
            {
                hashedValue += read[i];
                hashedValue *= KnuthConstant;
            }
            return hashedValue;
        }
        static int Min(int a, int b, int c)
        {
            if (a > b) return b > c ? c : b;
            else return a > c ? c : a;
        }
        public static int LevenshteinDistance(this string s, string t)
        {
            // degenerate cases
            if (s == t) return 0;
            if (s.Length == 0) return t.Length;
            if (t.Length == 0) return s.Length;

            // create two work vectors of integer distances
            int[] v0 = new int[t.Length + 1];
            int[] v1 = new int[t.Length + 1];

            // initialize v0 (the previous row of distances)
            // this row is A[0][i]: edit distance for an empty s
            // the distance is just the number of characters to delete from t
            for (int i = 0; i < v0.Length; i++)
                v0[i] = i;

            for (int i = 0; i < s.Length; i++)
            {
                // calculate v1 (current row distances) from the previous row v0

                // first element of v1 is A[i+1][0]
                //   edit distance is delete (i+1) chars from s to match empty t
                v1[0] = i + 1;

                // use formula to fill in the rest of the row
                for (int j = 0; j < t.Length; j++)
                {
                    var cost = (s[i] == t[j]) ? 0 : 1;
                    v1[j + 1] = Min(v1[j] + 1, v0[j + 1] + 1, v0[j] + cost);
                }

                // copy v1 (current row) to v0 (previous row) for next interation
                for (int j = 0; j < v0.Length; j++)
                    v0[j] = v1[j];
            }

            return v1[t.Length];
        }
    }


    public class SortEntry<Key, Value>
    {
        public Dictionary<Key, Value> Values;
    }
}