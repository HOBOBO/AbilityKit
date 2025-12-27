using System;
using System.Collections.Generic;
using AbilityKit.Ability.Configs;
using AbilityKit.Ability.Triggering;
using Sirenix.OdinInspector;

namespace AbilityKit.Ability.Editor
{
    internal static class VarKeyDropdownUtil
    {
        public static IEnumerable<ValueDropdownItem<ScopedVarKey>> BuildScopedKeyOptions(
            VarKeyUsage usage,
            ArgValueKind expectedKind
        )
        {
            var list = new List<ValueDropdownItem<ScopedVarKey>>();

            var recent = VarKeyRecentUtil.GetRecent(usage, expectedKind);
            if (recent != null)
            {
                for (int i = 0; i < recent.Count; i++)
                {
                    var svk = recent[i];
                    if (string.IsNullOrEmpty(svk.Key)) continue;
                    if (!IsAssignable(usage, svk)) continue;
                    list.Add(new ValueDropdownItem<ScopedVarKey>("最近" + BuildScopedLabel(svk), svk));
                }
            }

            var localKeys = VarKeyProviderRegistry.GetKeys(
                new VarKeyQuery(includeLocal: true, includeGlobal: false, VarScope.Local, usage, expectedKind)
            );
            if (localKeys != null)
            {
                foreach (var k in localKeys)
                {
                    if (string.IsNullOrEmpty(k)) continue;
                    var path = "局部/" + GetKindGroup(VarScope.Local, k) + "/" + k;
                    list.Add(new ValueDropdownItem<ScopedVarKey>(path, new ScopedVarKey(VarScope.Local, k)));
                }
            }

            var globalKeys = VarKeyProviderRegistry.GetKeys(
                new VarKeyQuery(includeLocal: false, includeGlobal: true, VarScope.Global, usage, expectedKind)
            );
            if (globalKeys != null)
            {
                foreach (var k in globalKeys)
                {
                    if (string.IsNullOrEmpty(k)) continue;
                    var path = "全局/" + GetKindGroup(VarScope.Global, k) + "/" + k;
                    list.Add(new ValueDropdownItem<ScopedVarKey>(path, new ScopedVarKey(VarScope.Global, k)));
                }
            }

            return list;
        }

        private static bool IsAssignable(VarKeyUsage usage, ScopedVarKey svk)
        {
            if (usage != VarKeyUsage.Assign) return true;

            // Ensure recent entries respect assignability (e.g. read-only filtered).
            var includeLocal = svk.Scope == VarScope.Local;
            var includeGlobal = svk.Scope == VarScope.Global;
            var keys = VarKeyProviderRegistry.GetKeys(new VarKeyQuery(includeLocal, includeGlobal, svk.Scope, VarKeyUsage.Assign, ArgValueKind.None));
            if (keys == null) return false;
            using (var e = keys.GetEnumerator())
            {
                while (e.MoveNext())
                {
                    if (string.Equals(e.Current, svk.Key, StringComparison.Ordinal)) return true;
                }
            }
            return false;
        }

        private static string BuildScopedLabel(ScopedVarKey svk)
        {
            var scopePrefix = svk.Scope == VarScope.Global ? "全局" : "局部";
            return scopePrefix + "/" + svk.Key;
        }

        private static string GetKindGroup(VarScope scope, string key)
        {
            if (string.IsNullOrEmpty(key)) return "其他";
            if (!VarKeyTypeResolver.TryGetKind(scope, key, out var kind)) return "其他";
            switch (kind)
            {
                case ArgValueKind.Int: return "整数";
                case ArgValueKind.Float: return "浮点";
                case ArgValueKind.Bool: return "布尔";
                case ArgValueKind.String: return "字符串";
                case ArgValueKind.Object: return "对象";
                default: return "其他";
            }
        }

        public static IEnumerable<string> BuildKeys(
            VarScope scope,
            VarKeyUsage usage,
            ArgValueKind expectedKind
        )
        {
            var includeLocal = scope == VarScope.Local;
            var includeGlobal = scope == VarScope.Global;
            return VarKeyProviderRegistry.GetKeys(new VarKeyQuery(includeLocal, includeGlobal, scope, usage, expectedKind));
        }
    }
}
