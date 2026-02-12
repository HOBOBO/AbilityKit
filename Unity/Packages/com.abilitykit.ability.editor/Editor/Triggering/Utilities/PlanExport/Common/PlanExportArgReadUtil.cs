#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Editor.Utilities
{
    internal static class PlanExportArgReadUtil
    {
        private static bool TryGetIgnoreCase(Dictionary<string, object> args, string key, out object obj)
        {
            obj = null;
            if (args == null || string.IsNullOrEmpty(key)) return false;

            if (args.TryGetValue(key, out obj) && obj != null) return true;

            foreach (var kv in args)
            {
                if (kv.Key == null) continue;
                if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    obj = kv.Value;
                    return obj != null;
                }
            }

            obj = null;
            return false;
        }

        public static bool TryReadDouble(Dictionary<string, object> args, string key, out double value)
        {
            value = 0d;
            if (args == null || string.IsNullOrEmpty(key)) return false;

            if (!TryGetIgnoreCase(args, key, out var obj)) return false;

            try
            {
                value = Convert.ToDouble(obj);
                return true;
            }
            catch
            {
                if (obj is string s && double.TryParse(s, out var parsed))
                {
                    value = parsed;
                    return true;
                }
                return false;
            }
        }

        public static bool TryReadInt(Dictionary<string, object> args, string key, out int value)
        {
            value = 0;
            if (!TryReadDouble(args, key, out var raw)) return false;

            if (double.IsNaN(raw) || double.IsInfinity(raw)) return false;
            if (raw <= int.MinValue || raw >= int.MaxValue) return false;

            value = (int)Math.Round(raw);
            return true;
        }

        public static bool TryReadBool(Dictionary<string, object> args, string key, out bool value)
        {
            value = false;
            if (args == null || string.IsNullOrEmpty(key)) return false;
            if (!TryGetIgnoreCase(args, key, out var obj)) return false;

            try
            {
                if (obj is bool b)
                {
                    value = b;
                    return true;
                }

                value = Convert.ToBoolean(obj);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool TryReadString(Dictionary<string, object> args, string key, out string value)
        {
            value = null;
            if (args == null || string.IsNullOrEmpty(key)) return false;
            if (!TryGetIgnoreCase(args, key, out var obj)) return false;

            value = obj as string ?? obj?.ToString();
            return !string.IsNullOrEmpty(value);
        }

        public static string DumpArgs(Dictionary<string, object> args)
        {
            if (args == null || args.Count == 0) return string.Empty;
            var dump = string.Empty;
            foreach (var kv in args)
            {
                if (!string.IsNullOrEmpty(dump)) dump += ", ";
                dump += kv.Key + "=" + (kv.Value != null ? kv.Value.ToString() : "<null>");
            }
            return dump;
        }
    }
}
#endif
