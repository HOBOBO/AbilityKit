using System.Collections.Generic;

namespace AbilityKit.GameplayTags.Editor
{
    internal static class GameplayTagValidator
    {
        public static string Validate(IReadOnlyList<string> tags)
        {
            if (tags == null || tags.Count == 0) return "No tags.";

            var set = new HashSet<string>(System.StringComparer.Ordinal);
            for (int i = 0; i < tags.Count; i++)
            {
                var t = tags[i];
                if (string.IsNullOrWhiteSpace(t)) return $"Invalid empty tag at index {i}";
                if (!TryNormalize(t, out var n)) return $"Invalid tag: '{t}'";
                if (!set.Add(n)) return $"Duplicate tag: '{n}'";
            }

            return string.Empty;
        }

        public static bool TryNormalize(string name, out string normalized)
        {
            normalized = null;
            if (string.IsNullOrWhiteSpace(name)) return false;

            var s = name.Trim();
            if (s.Length == 0) return false;
            if (s[0] == '.' || s[s.Length - 1] == '.') return false;

            for (int i = 0; i < s.Length; i++)
            {
                var c = s[i];
                if (c == '.')
                {
                    if (i > 0 && s[i - 1] == '.') return false;
                    continue;
                }

                if (char.IsWhiteSpace(c)) return false;
            }

            normalized = s;
            return true;
        }

        public static bool IsValidSegment(string segment)
        {
            if (string.IsNullOrWhiteSpace(segment)) return false;
            var s = segment.Trim();
            if (s.Length == 0) return false;
            if (s.Contains(".")) return false;
            for (int i = 0; i < s.Length; i++)
            {
                if (char.IsWhiteSpace(s[i])) return false;
            }
            return true;
        }
    }
}