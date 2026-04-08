using System;
using System.Text;

namespace ET
{
    public static class StringHashHelper
    {
        public static long GetLongHashCode(this string str)
        {
            const uint seed = 1313;
            ulong hash = 0;
            for (int i = 0; i < str.Length; ++i)
            {
                char c = str[i];
                byte high = (byte)(c >> 8);
                byte low = (byte)(c & byte.MaxValue);
                hash = hash * seed + high;
                hash = hash * seed + low;
            }
            return (long)hash;
        }
    }
}