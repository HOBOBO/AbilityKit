using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Triggering
{
    public static class GlobalVarStore
    {
        private static Dictionary<string, object> Vars = new Dictionary<string, object>(StringComparer.Ordinal);

        public static bool TryGet(string key, out object value)
        {
            if (key != null && Vars.TryGetValue(key, out var obj))
            {
                value = obj;
                return true;
            }

            value = null;
            return false;
        }

        public static bool TryGet<T>(string key, out T value)
        {
            if (key != null && Vars.TryGetValue(key, out var obj) && obj is T t)
            {
                value = t;
                return true;
            }

            value = default;
            return false;
        }

        public static void Set(string key, object value)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            Vars[key] = value;
        }

        public static void Clear()
        {
            Vars.Clear();
        }

        public static void EnsureCapacity(int capacity)
        {
            if (capacity <= 0) return;
            if (Vars.Count >= capacity) return;

            var comparer = Vars.Comparer;
            var next = new Dictionary<string, object>(capacity, comparer);
            foreach (var kv in Vars)
            {
                next[kv.Key] = kv.Value;
            }

            Vars = next;
        }

        public static void TrimExcess()
        {
            var comparer = Vars.Comparer;
            var next = new Dictionary<string, object>(Vars.Count, comparer);
            foreach (var kv in Vars)
            {
                next[kv.Key] = kv.Value;
            }

            Vars = next;
        }
    }
}
