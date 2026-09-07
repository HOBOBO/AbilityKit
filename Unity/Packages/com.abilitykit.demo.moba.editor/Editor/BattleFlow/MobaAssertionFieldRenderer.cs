#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.BattleFlow;
using AbilityKit.BattleFlow.Editor;
using AbilityKit.Demo.Moba.EnvironmentModel;
using AbilityKit.Demo.Moba.Services;
using UnityEditor;

namespace AbilityKit.Demo.Moba.Editor.BattleFlow
{
    /// <summary>
    /// MOBA 断言积木的字段渲染器：把框架反射兜底（裸 string/int 输入）替换成下拉框 + 必填提示，
    /// 避免策划/测试手写 trace kind / comparator 拼错。
    /// </summary>
    [InitializeOnLoad]
    public sealed class MobaAssertionFieldRenderer : IBattleBlockFieldRenderer
    {
        private static readonly string[] TraceKinds = Enum.GetNames(typeof(MobaTraceKind))
            .Where(k => k != "None")
            .ToArray();

        private static readonly string[] Comparators = { "eq", "ne", "gt", "gte", "lt", "lte", "contains" };

        private static readonly string[] StateProperties =
            { "hp", "mana", "maxhp", "maxmana", "position", "teamid", "buffcount", "exists" };

        static MobaAssertionFieldRenderer()
        {
            BattleBlockFieldRendererRegistry.Renderer = new MobaAssertionFieldRenderer();
        }

        public bool TryDrawFields(BattleBlock block)
        {
            switch (block)
            {
                case AssertTraceBlock b: DrawAssertTrace(b); return true;
                case AssertNoTraceBlock b: DrawAssertNoTrace(b); return true;
                case AssertStateBlock b: DrawAssertState(b); return true;
                case AssertContextBlock b: DrawAssertContext(b); return true;
                case AssertRelationshipBlock b: DrawAssertRelationship(b); return true;
                default: return false;
            }
        }

        private static void DrawAssertTrace(AssertTraceBlock b)
        {
            b.Kind = DrawKind(b.Kind, required: true);
            b.ConfigId = EditorGUILayout.IntField("ConfigId", b.ConfigId);
            b.MinCount = EditorGUILayout.IntField("MinCount", b.MinCount);
            b.MaxCount = EditorGUILayout.IntField("MaxCount", b.MaxCount);
            b.UnderEffectId = EditorGUILayout.IntField("UnderEffectId", b.UnderEffectId);
        }

        private static void DrawAssertNoTrace(AssertNoTraceBlock b)
        {
            b.Kind = DrawKind(b.Kind, required: true);
            b.ConfigId = EditorGUILayout.IntField("ConfigId", b.ConfigId);
            b.UnderEffectId = EditorGUILayout.IntField("UnderEffectId", b.UnderEffectId);
        }

        private static void DrawAssertState(AssertStateBlock b)
        {
            b.Alias = DrawRequiredText("Alias", b.Alias);
            b.Property = DrawStringPopup("Property", b.Property, StateProperties);
            b.Comparator = DrawStringPopup("Comparator", b.Comparator, Comparators);
            b.ExpectedValue = EditorGUILayout.TextField("ExpectedValue", b.ExpectedValue);
        }

        private static void DrawAssertContext(AssertContextBlock b)
        {
            b.Alias = DrawRequiredText("Alias", b.Alias);
            b.Kind = DrawKind(b.Kind, required: false);
            b.Property = DrawStringPopup("Property", b.Property, StateProperties);
            b.Comparator = DrawStringPopup("Comparator", b.Comparator, Comparators);
            b.ExpectedValue = EditorGUILayout.TextField("ExpectedValue", b.ExpectedValue);
        }

        private static void DrawAssertRelationship(AssertRelationshipBlock b)
        {
            b.ParentKind = DrawKind(b.ParentKind, required: true);
            b.ParentConfigId = EditorGUILayout.IntField("ParentConfigId", b.ParentConfigId);
            b.ChildKind = DrawKind(b.ChildKind, required: true);
            b.ChildConfigId = EditorGUILayout.IntField("ChildConfigId", b.ChildConfigId);
        }

        /// <summary>trace kind 下拉框；required 时空值标红。</summary>
        private static string DrawKind(string current, bool required)
        {
            var value = DrawStringPopup("Kind", current, TraceKinds);
            if (required && string.IsNullOrEmpty(value))
                EditorGUILayout.HelpBox("Kind 必填", MessageType.Warning);
            return value;
        }

        private static string DrawRequiredText(string label, string current)
        {
            var value = EditorGUILayout.TextField(label, current);
            if (string.IsNullOrEmpty(value))
                EditorGUILayout.HelpBox(label + " 必填", MessageType.Warning);
            return value;
        }

        private static string DrawStringPopup(string label, string current, string[] options)
        {
            var list = new List<string>(options);
            if (!string.IsNullOrEmpty(current) && !list.Contains(current))
                list.Insert(0, current);
            var index = Math.Max(0, list.IndexOf(current ?? string.Empty));
            index = EditorGUILayout.Popup(label, index, list.ToArray());
            return list[index];
        }
    }
}
#endif
