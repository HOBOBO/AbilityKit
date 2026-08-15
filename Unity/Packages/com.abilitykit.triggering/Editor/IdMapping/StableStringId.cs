using System;
using System.Collections.Generic;
using AbilityKit.Core.Identifiers;

namespace AbilityKit.Triggering.Editor.IdMapping
{
    internal static class StableStringId
    {
        private static readonly Dictionary<int, string> _reverse = new Dictionary<int, string>();

        public static int Get(string value)
        {
            if (string.IsNullOrEmpty(value)) throw new ArgumentException("Id string is null or empty", nameof(value));

            var id = StableHashV1.Fnv1a32Utf16NonNegative(value);
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

    }
}
