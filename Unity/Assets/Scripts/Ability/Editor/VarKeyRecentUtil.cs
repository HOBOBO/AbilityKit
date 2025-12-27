using System;
using System.Collections.Generic;
using AbilityKit.Configs;
using AbilityKit.Triggering;
using UnityEditor;

namespace AbilityKit.Ability.Editor
{
    internal static class VarKeyRecentUtil
    {
        private const string PrefKey = "AbilityKit.VarKey.Recent";
        private const int MaxCount = 10;

        public static void Record(VarScope scope, string key)
        {
            if (string.IsNullOrEmpty(key)) return;

            var token = Encode(scope, key);
            var list = Load();
            list.Remove(token);
            list.Insert(0, token);
            if (list.Count > MaxCount) list.RemoveRange(MaxCount, list.Count - MaxCount);
            Save(list);
        }

        public static List<ScopedVarKey> GetRecent(VarKeyUsage usage, ArgValueKind expectedKind)
        {
            var raw = Load();
            var list = new List<ScopedVarKey>(raw.Count);

            for (int i = 0; i < raw.Count; i++)
            {
                if (!TryDecode(raw[i], out var scope, out var key)) continue;

                if (expectedKind != ArgValueKind.None)
                {
                    if (!VarKeyTypeResolver.TryGetKind(scope, key, out var kind) || kind != expectedKind) continue;
                }

                if (usage == VarKeyUsage.Assign)
                {
                    // Assign filtering is already done by providers; keep recent list lenient and let dropdown builder filter.
                }

                list.Add(new ScopedVarKey(scope, key));
            }

            return list;
        }

        private static string Encode(VarScope scope, string key)
        {
            return (scope == VarScope.Global ? "G:" : "L:") + key;
        }

        private static bool TryDecode(string token, out VarScope scope, out string key)
        {
            scope = VarScope.Local;
            key = null;
            if (string.IsNullOrEmpty(token) || token.Length < 3) return false;

            if (token.StartsWith("G:", StringComparison.Ordinal))
            {
                scope = VarScope.Global;
                key = token.Substring(2);
                return !string.IsNullOrEmpty(key);
            }

            if (token.StartsWith("L:", StringComparison.Ordinal))
            {
                scope = VarScope.Local;
                key = token.Substring(2);
                return !string.IsNullOrEmpty(key);
            }

            return false;
        }

        private static List<string> Load()
        {
            var s = EditorPrefs.GetString(PrefKey, string.Empty);
            if (string.IsNullOrEmpty(s)) return new List<string>();

            var parts = s.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            return new List<string>(parts);
        }

        private static void Save(List<string> tokens)
        {
            if (tokens == null || tokens.Count == 0)
            {
                EditorPrefs.SetString(PrefKey, string.Empty);
                return;
            }

            var s = string.Join(";", tokens);
            EditorPrefs.SetString(PrefKey, s);
        }
    }
}
