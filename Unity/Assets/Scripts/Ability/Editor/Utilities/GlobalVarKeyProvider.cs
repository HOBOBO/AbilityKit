using System;
using System.Collections.Generic;
using AbilityKit.Ability.Configs;
using AbilityKit.Ability.Triggering;
using UnityEditor;

namespace AbilityKit.Ability.Editor
{
    internal sealed class GlobalVarKeyProvider : IVarKeyProvider
    {
        public int Order => 100;

        private readonly struct Entry
        {
            public Entry(string key, ArgValueKind kind, bool readOnly)
            {
                Key = key;
                Kind = kind;
                ReadOnly = readOnly;
            }

            public string Key { get; }
            public ArgValueKind Kind { get; }
            public bool ReadOnly { get; }
        }

        private static double _nextRefreshAt;
        private static List<Entry> _cache;

        public bool CanProvide(in VarKeyQuery query)
        {
            if (!query.IncludeGlobal) return false;
            if (query.Scope.HasValue && query.Scope.Value != VarScope.Global) return false;
            return true;
        }

        public void CollectKeys(in VarKeyQuery query, List<string> output)
        {
            if (output == null) return;
            RefreshCacheIfNeeded();
            if (_cache == null) return;

            var expected = query.ExpectedKind;
            var filterByKind = expected != ArgValueKind.None;
            var assign = query.Usage == VarKeyUsage.Assign;

            for (int i = 0; i < _cache.Count; i++)
            {
                var e = _cache[i];
                if (assign && e.ReadOnly) continue;
                if (filterByKind && e.Kind != expected) continue;
                output.Add(e.Key);
            }
        }

        private static void RefreshCacheIfNeeded()
        {
            var now = EditorApplication.timeSinceStartup;
            if (_cache != null && now < _nextRefreshAt) return;

            _cache = new List<Entry>();

            var guids = AssetDatabase.FindAssets("t:GlobalVarsSO");
            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var asset = AssetDatabase.LoadAssetAtPath<GlobalVarsSO>(path);
                if (asset == null || asset.Vars == null) continue;

                for (int j = 0; j < asset.Vars.Count; j++)
                {
                    var e = asset.Vars[j];
                    if (e == null || string.IsNullOrEmpty(e.Key)) continue;
                    _cache.Add(new Entry(e.Key, e.Kind, e.ReadOnly));
                }
            }

            _cache.Sort((a, b) => StringComparer.Ordinal.Compare(a.Key, b.Key));
            for (int i = _cache.Count - 1; i > 0; i--)
            {
                if (string.Equals(_cache[i].Key, _cache[i - 1].Key, StringComparison.Ordinal))
                {
                    _cache.RemoveAt(i);
                }
            }

            _nextRefreshAt = now + 0.75;
        }
    }
}
