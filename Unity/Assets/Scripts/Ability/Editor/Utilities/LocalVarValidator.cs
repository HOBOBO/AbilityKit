using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Editor
{
    internal static class LocalVarValidator
    {
        public static bool HasValidationError(List<LocalVarEntry> localVars)
        {
            if (localVars == null || localVars.Count == 0) return false;

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < localVars.Count; i++)
            {
                var e = localVars[i];
                if (e == null) continue;
                if (string.IsNullOrEmpty(e.Key)) return true;
                if (!seen.Add(e.Key)) return true;
            }

            return false;
        }

        public static string BuildValidationMessage(List<LocalVarEntry> localVars)
        {
            if (localVars == null || localVars.Count == 0) return string.Empty;

            var empty = 0;
            var duplicates = new HashSet<string>(StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < localVars.Count; i++)
            {
                var e = localVars[i];
                if (e == null) continue;
                if (string.IsNullOrEmpty(e.Key))
                {
                    empty++;
                    continue;
                }
                if (!seen.Add(e.Key))
                {
                    duplicates.Add(e.Key);
                }
            }

            var parts = new List<string>();
            if (empty > 0) parts.Add("存在空 Key: " + empty);
            if (duplicates.Count > 0) parts.Add("Key 重复: " + string.Join(", ", duplicates));
            return string.Join("\n", parts);
        }
    }
}
