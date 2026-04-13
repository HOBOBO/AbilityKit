#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace AbilityKit.Ability.Editor.Utilities
{
    internal static class AbilityTriggerExportUtils
    {
        public static string FormatCounterMap(Dictionary<string, int> map)
        {
            if (map == null || map.Count == 0) return "(empty)";
            var s = "";
            foreach (var kv in map)
            {
                if (!string.IsNullOrEmpty(s)) s += ", ";
                s += kv.Key + "=" + kv.Value;
            }
            return s;
        }

        public static string FormatIntList(List<int> list)
        {
            if (list == null || list.Count == 0) return "(empty)";
            var s = "";
            for (int i = 0; i < list.Count && i < 32; i++)
            {
                if (!string.IsNullOrEmpty(s)) s += ", ";
                s += list[i];
            }
            if (list.Count > 32) s += $" ... (+{list.Count - 32})";
            return s;
        }

        public static Dictionary<string, object> CopyArgs(IReadOnlyDictionary<string, object> args)
        {
            if (args == null) return null;
            var dict = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var kv in args)
            {
                if (kv.Key == null) continue;
                dict[kv.Key] = kv.Value;
            }
            return dict.Count > 0 ? dict : null;
        }

        public static string TryGetSelectedFolderPath()
        {
            var obj = Selection.activeObject;
            if (obj == null) return "Assets";

            var path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path)) return "Assets";

            if (AssetDatabase.IsValidFolder(path)) return path;

            var dir = Path.GetDirectoryName(path);
            return string.IsNullOrEmpty(dir) ? "Assets" : dir.Replace('\\', '/');
        }
    }
}
#endif
