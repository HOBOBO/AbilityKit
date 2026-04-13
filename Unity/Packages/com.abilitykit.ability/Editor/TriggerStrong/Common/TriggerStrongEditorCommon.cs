using System;
using System.Collections.Generic;
using AbilityKit.Ability.Config;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Triggering.Definitions;
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

        public abstract ConditionConfigBase ToRuntimeConfig();

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

        public virtual ConditionDef ToConditionDef()
        {
            return null;
        }
    }

    internal static class StrongEditorTitleUtil
    {
        public static string FormatArg(ArgRuntimeEntryCore e)
        {
            if (e == null) return "null";

            switch (e.Kind)
            {
                case ArgValueKind.Int:
                    return Convert.ToInt64(e.Value).ToString();
                case ArgValueKind.Float:
                    return Convert.ToSingle(e.Value).ToString("0.###");
                case ArgValueKind.Bool:
                    return Convert.ToBoolean(e.Value) ? "true" : "false";
                case ArgValueKind.String:
                    var strVal = e.Value as string;
                    return QuoteAndTruncate(strVal, 32);
                case ArgValueKind.Object:
                    if (e.Value == null) return "null";
                    if (e.Value is UnityEngine.Object uo) return uo != null ? uo.name : "null";
                    return e.Value.ToString();
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

        public abstract ActionConfigBase ToRuntimeConfig();

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

        public virtual ActionDef ToActionDef()
        {
            return null;
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
