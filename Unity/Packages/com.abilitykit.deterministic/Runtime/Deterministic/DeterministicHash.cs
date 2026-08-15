using System;

namespace AbilityKit.Deterministic
{
    /// <summary>
    /// Stable 64-bit FNV-1a hashing over fixed-point values, for simulation state hashing
    /// (rollback reconcile, replay verification). Unlike <see cref="object.GetHashCode"/>,
    /// the result is identical across processes, platforms, and runtime versions by construction.
    /// </summary>
    public static class DeterministicHash
    {
        /// <summary>FNV-1a 64-bit offset basis.</summary>
        public static long OffsetBasis => unchecked((long)0xCBF29CE484222325L);

        /// <summary>FNV-1a 64-bit prime.</summary>
        public static long Prime => unchecked((long)0x100000001B3L);

        /// <summary>Folds the 8 bytes of a raw long into the hash, lowest byte first.</summary>
        public static long Combine(long hash, long value)
        {
            unchecked
            {
                var current = hash;
                current = MixByte(current, (byte)value);
                current = MixByte(current, (byte)((long)((ulong)value >> 8)));
                current = MixByte(current, (byte)((long)((ulong)value >> 16)));
                current = MixByte(current, (byte)((long)((ulong)value >> 24)));
                current = MixByte(current, (byte)((long)((ulong)value >> 32)));
                current = MixByte(current, (byte)((long)((ulong)value >> 40)));
                current = MixByte(current, (byte)((long)((ulong)value >> 48)));
                current = MixByte(current, (byte)((long)((ulong)value >> 56)));
                return current;
            }
        }

        public static long Combine(long hash, Fixed64 value) => Combine(hash, value.RawValue);

        public static long Combine(long hash, FixedVec2 value)
        {
            return Combine(Combine(hash, value.X), value.Y);
        }

        public static long Combine(long hash, FixedVec3 value)
        {
            return Combine(Combine(Combine(hash, value.X), value.Y), value.Z);
        }

        public static long Hash(Fixed64 value) => Combine(OffsetBasis, value);

        public static long Hash(FixedVec2 value) => Combine(OffsetBasis, value);

        public static long Hash(FixedVec3 value) => Combine(OffsetBasis, value);

        private static long MixByte(long hash, byte value)
        {
            unchecked
            {
                hash ^= value;
                hash *= Prime;
                return hash;
            }
        }
    }
}
