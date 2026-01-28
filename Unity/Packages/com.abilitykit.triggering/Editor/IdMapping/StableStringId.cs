using System;
using System.Collections.Generic;

namespace AbilityKit.Triggering.Editor.IdMapping
{
    internal static class StableStringId
    {
        private static readonly Dictionary<int, string> _reverse = new Dictionary<int, string>();

        public static int Get(string value)
        {
            if (string.IsNullOrEmpty(value)) throw new ArgumentException("Id string is null or empty", nameof(value));

            var id = Fnv1a32(value);
            if (_reverse.TryGetValue(id, out var existing))
            {
                if (!string.Equals(existing, value, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"StableStringId collision: '{existing}' and '{value}' => {id}");
                }

                return id;
            }

            _reverse[id] = value;
            return id;
        }

        private static int Fnv1a32(string s)
        {
            unchecked
            {
                const uint offset = 2166136261;
                const uint prime = 16777619;

                uint hash = offset;
                for (int i = 0; i < s.Length; i++)
                {
                    hash ^= s[i];
                    hash *= prime;
                }

                // Keep ids positive and avoid 0 as a special-case if you want.
                return (int)(hash & 0x7FFFFFFF);
            }
        }
    }
}
