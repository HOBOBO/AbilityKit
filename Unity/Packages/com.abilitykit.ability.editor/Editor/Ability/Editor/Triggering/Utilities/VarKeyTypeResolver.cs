using System;
using System.Collections.Generic;
using AbilityKit.Ability.Configs;
using AbilityKit.Ability.Triggering;
using UnityEditor;

namespace AbilityKit.Ability.Editor
{
    internal static class VarKeyTypeResolver
    {
        private static double _nextGlobalRefreshAt;
        private static Dictionary<string, (ArgValueKind Kind, bool ReadOnly)> _globalKeyToMeta;

        public static bool TryGetKind(VarScope scope, string key, out ArgValueKind kind)
        {
            kind = ArgValueKind.None;
            if (string.IsNullOrEmpty(key)) return false;

            switch (scope)
            {
                case VarScope.Local:
                    return TryGetLocalKind(key, out kind);
                case VarScope.Global:
                    return TryGetGlobalKind(key, out kind);
                default:
                    return false;
            }
        }

        private static bool TryGetLocalKind(string key, out ArgValueKind kind)
        {
            kind = ArgValueKind.None;

            var currentTrigger = AbilityEditorVarKeyContext.CurrentTrigger;
            if (currentTrigger != null && currentTrigger.LocalVars != null)
            {
                for (int i = 0; i < currentTrigger.LocalVars.Count; i++)
                {
                    var e = currentTrigger.LocalVars[i];
                    if (e == null) continue;
                    if (!string.Equals(e.Key, key, StringComparison.Ordinal)) continue;
                    kind = e.Kind;
                    return kind != ArgValueKind.None;
                }

                return false;
            }

            var skill = AbilityEditorVarKeyContext.CurrentModule;
            var triggers = skill != null ? skill.Triggers : null;
            if (triggers == null) return false;

            for (int i = 0; i < triggers.Count; i++)
            {
                var t = triggers[i];
                if (t == null || t.LocalVars == null) continue;

                for (int j = 0; j < t.LocalVars.Count; j++)
                {
                    var e = t.LocalVars[j];
                    if (e == null) continue;
                    if (!string.Equals(e.Key, key, StringComparison.Ordinal)) continue;
                    kind = e.Kind;
                    return kind != ArgValueKind.None;
                }
            }

            return false;
        }

        private static bool TryGetGlobalKind(string key, out ArgValueKind kind)
        {
            kind = ArgValueKind.None;
            RefreshGlobalCacheIfNeeded();
            if (_globalKeyToMeta == null) return false;

            if (_globalKeyToMeta.TryGetValue(key, out var meta) && meta.Kind != ArgValueKind.None)
            {
                kind = meta.Kind;
                return true;
            }

            return false;
        }

        private static void RefreshGlobalCacheIfNeeded()
        {
            var now = EditorApplication.timeSinceStartup;
            if (_globalKeyToMeta != null && now < _nextGlobalRefreshAt) return;

            _globalKeyToMeta = new Dictionary<string, (ArgValueKind Kind, bool ReadOnly)>(StringComparer.Ordinal);

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

                    // If multiple assets have same key, keep the first one encountered.
                    if (_globalKeyToMeta.ContainsKey(e.Key)) continue;
                    _globalKeyToMeta.Add(e.Key, (e.Kind, e.ReadOnly));
                }
            }

            _nextGlobalRefreshAt = now + 0.75;
        }
    }
}
