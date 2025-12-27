using System;
using System.Collections.Generic;
using System.Reflection;

namespace AbilityKit.Configs
{
    internal static class TemplateParamResolver
    {
        public static void ApplyToObject(object target, IReadOnlyDictionary<string, ArgRuntimeEntry> bindings)
        {
            if (target == null || bindings == null || bindings.Count == 0) return;

            var type = target.GetType();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var fields = type.GetFields(flags);

            for (int i = 0; i < fields.Length; i++)
            {
                var f = fields[i];
                if (f.IsInitOnly) continue;

                var ft = f.FieldType;
                if (ft == typeof(string))
                {
                    var s = (string)f.GetValue(target);
                    if (!TryParseParamRef(s, out var name)) continue;
                    if (!bindings.TryGetValue(name, out var bound) || bound == null) continue;
                    var value = bound.GetBoxedValue();
                    f.SetValue(target, value != null ? value.ToString() : null);
                    continue;
                }

                if (ft == typeof(ArgRuntimeEntry))
                {
                    var e = (ArgRuntimeEntry)f.GetValue(target);
                    if (e == null) continue;
                    if (e.Kind != ArgValueKind.String) continue;
                    if (!TryParseParamRef(e.StringValue, out var name)) continue;
                    if (!bindings.TryGetValue(name, out var bound) || bound == null) continue;
                    f.SetValue(target, bound.Clone());
                    continue;
                }

                if (typeof(System.Collections.IList).IsAssignableFrom(ft))
                {
                    var list = f.GetValue(target) as System.Collections.IList;
                    if (list == null) continue;
                    for (int j = 0; j < list.Count; j++)
                    {
                        var item = list[j];
                        if (item == null) continue;
                        ApplyToObject(item, bindings);
                    }
                    continue;
                }

                if (ft.IsClass && ft != typeof(UnityEngine.Object))
                {
                    var child = f.GetValue(target);
                    if (child == null) continue;
                    ApplyToObject(child, bindings);
                }
            }
        }

        public static bool TryParseParamRef(string s, out string name)
        {
            name = null;
            if (string.IsNullOrEmpty(s)) return false;
            if (s.Length < 2) return false;
            if (s[0] != '$') return false;
            name = s.Substring(1);
            return !string.IsNullOrEmpty(name);
        }
    }
}
