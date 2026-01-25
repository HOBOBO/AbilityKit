using System;
using System.Collections.Generic;
using System.Text;

namespace AbilityKit.Ability.Editor
{
    internal static class AbilityEditorFilterSyntax
    {
        public static bool MatchesFilter(AbilityModuleSO asset, string path, string filter)
        {
            if (string.IsNullOrEmpty(filter)) return true;
            if (asset == null) return false;

            var tokens = TokenizeFilter(filter);
            if (tokens.Count == 0) return true;

            for (int i = 0; i < tokens.Count; i++)
            {
                var token = tokens[i];
                if (string.IsNullOrEmpty(token)) continue;

                var isExclude = token[0] == '-';
                var t = isExclude ? token.Substring(1) : token;
                if (string.IsNullOrEmpty(t)) continue;

                var matched = MatchToken(asset, path, t);
                if (isExclude)
                {
                    if (matched) return false;
                }
                else
                {
                    if (!matched) return false;
                }
            }

            return true;
        }

        private static bool MatchToken(AbilityModuleSO asset, string path, string token)
        {
            var skillId = asset != null ? asset.AbilityId : null;

            string key = null;
            string value = token;
            var idx = token.IndexOf(':');
            if (idx > 0 && idx < token.Length - 1)
            {
                key = token.Substring(0, idx);
                value = token.Substring(idx + 1);
            }

            if (string.IsNullOrEmpty(value)) return true;

            if (string.Equals(key, "id", StringComparison.OrdinalIgnoreCase))
            {
                return !string.IsNullOrEmpty(skillId) && skillId.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
            }

            if (string.Equals(key, "path", StringComparison.OrdinalIgnoreCase))
            {
                return !string.IsNullOrEmpty(path) && path.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
            }

            return (!string.IsNullOrEmpty(path) && path.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0)
                   || (!string.IsNullOrEmpty(skillId) && skillId.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static List<string> TokenizeFilter(string filter)
        {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(filter)) return list;

            var sb = new StringBuilder();
            var inQuote = false;
            for (int i = 0; i < filter.Length; i++)
            {
                var ch = filter[i];
                if (ch == '"')
                {
                    inQuote = !inQuote;
                    continue;
                }

                if (!inQuote && char.IsWhiteSpace(ch))
                {
                    if (sb.Length > 0)
                    {
                        list.Add(sb.ToString());
                        sb.Length = 0;
                    }
                    continue;
                }

                sb.Append(ch);
            }

            if (sb.Length > 0) list.Add(sb.ToString());
            return list;
        }
    }
}
