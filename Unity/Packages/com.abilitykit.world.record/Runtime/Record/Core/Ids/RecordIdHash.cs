using System;
using System.Text;

namespace AbilityKit.Core.Common.Record.Core
{
    public static class RecordIdHash
    {
        // Deterministic FNV-1a 32-bit over UTF8 bytes.
        public static int Fnv1a32(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;

            var bytes = Encoding.UTF8.GetBytes(s);
            unchecked
            {
                uint h = 2166136261u;
                for (int i = 0; i < bytes.Length; i++)
                {
                    h ^= bytes[i];
                    h *= 16777619u;
                }
                return (int)h;
            }
        }
    }
}
