using System;
using AbilityKit.Core.Identifiers;

namespace AbilityKit.Core.Recording.Core
{
    public static class RecordIdHash
    {
        // Deterministic FNV-1a 32-bit over UTF8 bytes without an intermediate byte array.
        public static int Fnv1a32(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            return StableHashV1.Fnv1a32Utf8(s);
        }
    }
}
