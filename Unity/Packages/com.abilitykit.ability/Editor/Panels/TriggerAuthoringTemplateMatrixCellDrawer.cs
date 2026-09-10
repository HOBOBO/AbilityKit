#if UNITY_EDITOR
using System.Collections.Generic;
using AbilityKit.Ability.Config.Authoring;
using AbilityKit.Ability.Editor.Utilities;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Editor.Panels
{
    internal static class TriggerAuthoringTemplateMatrixCellDrawer
    {
        public static void DrawBinding(
            Rect rect,
            TriggerDefinitionData trigger,
            TriggerAuthoringTemplateParameterData parameter,
            TriggerArgumentData binding)
        {
            if (binding?.Value == null)
            {
                DrawMissingBinding(rect, trigger, parameter);
                return;
            }
            var value = binding.Value;
            if (value.Source != TriggerValueSource.Constant || !CanEditConstant(parameter.Type))
            {
                var text = TriggerAuthoringTemplateValueTextCodec.Format(value);
                EditorGUI.LabelField(
                    rect,
                    new GUIContent(text, "在规则编辑中可使用完整绑定编辑器"),
                    EditorStyles.miniLabel);
                return;
            }
            DrawConstant(rect, value, parameter.Type);
        }

        public static void DrawDiagnostics(
            Rect rect,
            TriggerAuthoringTriggerIndex.DiagnosticSummary diagnostics)
        {
            var previous = GUI.color;
            string text;
            if (diagnostics.Errors > 0)
            {
                GUI.color = new Color(1f, 0.48f, 0.44f);
                text = "错误 " + diagnostics.Errors;
            }
            else if (diagnostics.Warnings > 0)
            {
                GUI.color = new Color(1f, 0.75f, 0.3f);
                text = "警告 " + diagnostics.Warnings;
            }
            else
            {
                GUI.color = new Color(0.48f, 0.78f, 0.52f);
                text = "正常";
            }
            EditorGUI.LabelField(rect, text, EditorStyles.miniBoldLabel);
            GUI.color = previous;
        }

        private static void DrawConstant(Rect rect, TriggerValueRefData value, TriggerValueType type)
        {
            switch (type)
            {
                case TriggerValueType.Integer:
                case TriggerValueType.Entity:
                case TriggerValueType.ObjectId:
                    value.IntegerValue = EditorGUI.LongField(rect, value.IntegerValue);
                    break;
                case TriggerValueType.Number:
                    value.NumberValue = EditorGUI.DoubleField(rect, value.NumberValue);
                    break;
                case TriggerValueType.Boolean:
                    value.BooleanValue = EditorGUI.Toggle(rect, value.BooleanValue);
                    break;
                case TriggerValueType.String:
                    value.StringValue = EditorGUI.TextField(rect, value.StringValue ?? string.Empty);
                    break;
            }
        }

        private static void DrawMissingBinding(
            Rect rect,
            TriggerDefinitionData trigger,
            TriggerAuthoringTemplateParameterData parameter)
        {
            if (parameter.HasDefault)
            {
                var value = TriggerAuthoringTemplateValueTextCodec.Format(parameter.DefaultValue);
                var canOverride = (parameter.AllowedSources & TriggerTemplateValueSourceMask.Constant) != 0 &&
                                  CanEditConstant(parameter.Type);
                if (canOverride && GUI.Button(
                        rect,
                        new GUIContent("默认: " + value, "点击创建常量覆盖"),
                        EditorStyles.miniButton))
                    AddConstantBinding(trigger, parameter);
                else
                    EditorGUI.LabelField(
                        rect,
                        new GUIContent("默认: " + value, value),
                        EditorStyles.centeredGreyMiniLabel);
                return;
            }
            var constantAllowed = (parameter.AllowedSources & TriggerTemplateValueSourceMask.Constant) != 0;
            if (constantAllowed && CanEditConstant(parameter.Type))
            {
                if (GUI.Button(rect, new GUIContent("+ 绑定", "创建常量绑定"), EditorStyles.miniButton))
                    AddConstantBinding(trigger, parameter);
                return;
            }
            EditorGUI.LabelField(rect, "未绑定", EditorStyles.centeredGreyMiniLabel);
        }

        private static bool CanEditConstant(TriggerValueType type)
        {
            return type == TriggerValueType.Integer || type == TriggerValueType.Number ||
                   type == TriggerValueType.Boolean || type == TriggerValueType.String ||
                   type == TriggerValueType.Entity || type == TriggerValueType.ObjectId;
        }

        private static void AddConstantBinding(
            TriggerDefinitionData trigger,
            TriggerAuthoringTemplateParameterData parameter)
        {
            var bindings = trigger.Template.Bindings ??
                           (trigger.Template.Bindings = new List<TriggerArgumentData>());
            bindings.Add(new TriggerArgumentData
            {
                Name = parameter.Name,
                Value = new TriggerValueRefData
                {
                    Source = TriggerValueSource.Constant,
                    Type = parameter.Type
                }
            });
        }
    }
}
#endif
