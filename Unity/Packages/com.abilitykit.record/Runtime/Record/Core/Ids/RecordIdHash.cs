using System;

namespace AbilityKit.Core.Recording.Core
{
    public static class RecordIdHash
    {
        private const uint OffsetBasis = 2166136261u;
        private const uint Prime = 16777619u;
        private const int ReplacementCodePoint = 0xFFFD;

        // Deterministic FNV-1a 32-bit over UTF8 bytes without an intermediate byte array.
        public static int Fnv1a32(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;

            unchecked
            {
                uint hash = OffsetBasis;
                for (int index = 0; index < s.Length; index++)
                {
                    int codePoint = s[index];
                    if (char.IsHighSurrogate(s[index]))
                    {
                        if (index + 1 < s.Length && char.IsLowSurrogate(s[index + 1]))
                        {
                            codePoint = char.ConvertToUtf32(s[index], s[++index]);
                        }
                        else
                        {
                            codePoint = ReplacementCodePoint;
                        }
                    }
                    else if (char.IsLowSurrogate(s[index]))
                    {
                        codePoint = ReplacementCodePoint;
                    }

                    hash = AppendUtf8(hash, codePoint);
                }

                return (int)hash;
            }
        }

        private static uint AppendUtf8(uint hash, int codePoint)
        {
            if (codePoint <= 0x7F)
            {
                return Mix(hash, (byte)codePoint);
            }

            if (codePoint <= 0x7FF)
            {
                hash = Mix(hash, (byte)(0xC0 | codePoint >> 6));
                return Mix(hash, (byte)(0x80 | codePoint & 0x3F));
            }

            if (codePoint <= 0xFFFF)
            {
                hash = Mix(hash, (byte)(0xE0 | codePoint >> 12));
                hash = Mix(hash, (byte)(0x80 | codePoint >> 6 & 0x3F));
                return Mix(hash, (byte)(0x80 | codePoint & 0x3F));
            }

            hash = Mix(hash, (byte)(0xF0 | codePoint >> 18));
            hash = Mix(hash, (byte)(0x80 | codePoint >> 12 & 0x3F));
            hash = Mix(hash, (byte)(0x80 | codePoint >> 6 & 0x3F));
            return Mix(hash, (byte)(0x80 | codePoint & 0x3F));
        }

        private static uint Mix(uint hash, byte value)
        {
            unchecked
            {
                return (hash ^ value) * Prime;
            }
        }
    }
}
