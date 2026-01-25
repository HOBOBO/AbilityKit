using System;
using System.Collections.Generic;
using AbilityKit.Ability.Configs;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Triggering.Runtime;
using Sirenix.OdinInspector;
using UnityEngine;

namespace AbilityKit.Ability.Editor
{
    [Serializable]
    public abstract class ConditionEditorConfigBase
    {
        [ShowInInspector, ReadOnly, LabelText("类型")]
        public abstract string Type { get; }

        public string DisplayTitle => ToString();

        public abstract ConditionRuntimeConfigBase ToRuntimeStrong();

        protected virtual string GetTitleSuffix()
        {
            return null;
        }

        public override string ToString()
        {
            var name = StrongEditorConfigNameCache.GetDisplayName(GetType());
            var title = string.IsNullOrEmpty(name) ? Type : name;
            var suffix = GetTitleSuffix();
            if (!string.IsNullOrEmpty(suffix))
            {
                title += ": " + suffix;
            }
            title += "  [" + Type + "]";
            return title;
        }
    }

    internal static class StrongEditorTitleUtil
    {
        public static string FormatArg(ArgRuntimeEntry e)
        {
            if (e == null) return "null";

            switch (e.Kind)
            {
                case ArgValueKind.Int:
                    return e.IntValue.ToString();
                case ArgValueKind.Float:
                    return e.FloatValue.ToString("0.###");
                case ArgValueKind.Bool:
                    return e.BoolValue ? "true" : "false";
                case ArgValueKind.String:
                    return QuoteAndTruncate(e.StringValue, 32);
                case ArgValueKind.Object:
                    if (e.ObjectValue == null) return "null";
                    if (e.ObjectValue is UnityEngine.Object uo) return uo != null ? uo.name : "null";
                    return e.ObjectValue.ToString();
                default:
                    return "null";
            }
        }

        public static string QuoteAndTruncate(string s, int maxLen)
        {
            if (string.IsNullOrEmpty(s)) return "\"\"";
            if (maxLen > 3 && s.Length > maxLen)
            {
                s = s.Substring(0, maxLen - 3) + "...";
            }
            return "\"" + s + "\"";
        }
    }

    [Serializable]
    public abstract class ActionEditorConfigBase
    {
        [ShowInInspector, ReadOnly, LabelText("类型")]
        public abstract string Type { get; }

        public string DisplayTitle => ToString();

        public abstract ActionRuntimeConfigBase ToRuntimeStrong();

        protected virtual string GetTitleSuffix()
        {
            return null;
        }

        public override string ToString()
        {
            var name = StrongEditorConfigNameCache.GetDisplayName(GetType());
            var title = string.IsNullOrEmpty(name) ? Type : name;
            var suffix = GetTitleSuffix();
            if (!string.IsNullOrEmpty(suffix))
            {
                title += ": " + suffix;
            }
            title += "  [" + Type + "]";
            return title;
        }
    }

    internal static class StrongEditorConfigNameCache
    {
        private static readonly Dictionary<Type, string> Cache = new Dictionary<Type, string>();

        public static string GetDisplayName(Type t)
        {
            if (t == null) return null;

            if (Cache.TryGetValue(t, out var v)) return v;

            var c = (TriggerConditionTypeAttribute)Attribute.GetCustomAttribute(t, typeof(TriggerConditionTypeAttribute));
            if (c != null)
            {
                v = c.DisplayName;
                Cache[t] = v;
                return v;
            }

            var a = (TriggerActionTypeAttribute)Attribute.GetCustomAttribute(t, typeof(TriggerActionTypeAttribute));
            if (a != null)
            {
                v = a.DisplayName;
                Cache[t] = v;
                return v;
            }

            Cache[t] = null;
            return null;
        }
    }
}
