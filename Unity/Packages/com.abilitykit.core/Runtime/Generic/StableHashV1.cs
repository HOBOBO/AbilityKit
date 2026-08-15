using System;

namespace AbilityKit.Core.Identifiers
{
    /// <summary>
    /// Versioned, allocation-free stable hash algorithms for persisted and cross-runtime identifiers.
    /// These methods must not change their output within the V1 contract.
    /// </summary>
    public static class StableHashV1
    {
        private const uint Fnv32OffsetBasis = 2166136261u;
        private const uint Fnv32Prime = 16777619u;
        private const int ReplacementCodePoint = 0xFFFD;

        /// <summary>
        /// Computes FNV-1a 32-bit by mixing each UTF-16 code unit as one value.
        /// This preserves the original AbilityKit string ID algorithm.
        /// </summary>
        /// <param name="value">The string to hash.</param>
        /// <returns>The signed 32-bit representation of the hash.</returns>
        public static int Fnv1a32Utf16(string value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));

            unchecked
            {
                uint hash = Fnv32OffsetBasis;
                for (int i = 0; i < value.Length; i++)
                {
                    hash ^= value[i];
                    hash *= Fnv32Prime;
                }

                return (int)hash;
            }
        }

        /// <summary>
        /// Computes the V1 UTF-16 hash and clears its sign bit for positive identifier domains.
        /// </summary>
        /// <param name="value">The string to hash.</param>
        /// <returns>A non-negative 31-bit identifier.</returns>
        public static int Fnv1a32Utf16NonNegative(string value)
        {
            return Fnv1a32Utf16(value) & 0x7FFFFFFF;
        }

        /// <summary>
        /// Computes FNV-1a 32-bit over the UTF-8 encoding without allocating an intermediate buffer.
        /// Invalid UTF-16 surrogate sequences are encoded as Unicode replacement characters.
        /// </summary>
        /// <param name="value">The string to hash.</param>
        /// <returns>The signed 32-bit representation of the hash.</returns>
        public static int Fnv1a32Utf8(string value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));

            unchecked
            {
                uint hash = Fnv32OffsetBasis;
                for (int index = 0; index < value.Length; index++)
                {
                    int codePoint = value[index];
                    if (char.IsHighSurrogate(value[index]))
                    {
                        if (index + 1 < value.Length && char.IsLowSurrogate(value[index + 1]))
                        {
                            codePoint = char.ConvertToUtf32(value[index], value[++index]);
                        }
                        else
                        {
                            codePoint = ReplacementCodePoint;
                        }
                    }
                    else if (char.IsLowSurrogate(value[index]))
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
                return (hash ^ value) * Fnv32Prime;
            }
        }
    }
}
