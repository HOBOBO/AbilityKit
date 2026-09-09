using System;
using System.Collections.Generic;
using AbilityKit.HFSM.Definition;
using AbilityKit.HFSM.Runtime;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.HFSM.Editor
{

    [Serializable]
    public sealed class BindingCatalogEntry
    {
        [SerializeField, InspectorName("类型")] private BindingKind kind;
        [SerializeField, InspectorName("稳定键")] private string key = string.Empty;
        [SerializeField, InspectorName("显示名称")] private string displayName = string.Empty;
        [SerializeField, InspectorName("分类")] private string category = string.Empty;
        [SerializeField, InspectorName("描述")] private string description = string.Empty;

        public BindingKind Kind { get => kind; set => kind = value; }
        public string Key { get => key; set => key = value ?? string.Empty; }
        public string DisplayName { get => displayName; set => displayName = value ?? string.Empty; }
        public string Category { get => category; set => category = value ?? string.Empty; }
        public string Description { get => description; set => description = value ?? string.Empty; }
    }

    [CustomPropertyDrawer(typeof(BindingCatalogEntry))]
    internal sealed class BindingCatalogEntryDrawer : PropertyDrawer
    {
        private const int FieldCount = 5;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return property.isExpanded
                ? (FieldCount + 1) * (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing)
                : EditorGUIUtility.singleLineHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var lineHeight = EditorGUIUtility.singleLineHeight;
            var lineStep = lineHeight + EditorGUIUtility.standardVerticalSpacing;
            var line = new Rect(position.x, position.y, position.width, lineHeight);
            property.isExpanded = EditorGUI.Foldout(line, property.isExpanded, "绑定条目", true);
            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                line.y += lineStep;
                var kind = property.FindPropertyRelative("kind");
                kind.intValue = EditorGUI.Popup(line, "类型", kind.intValue, new[] { "状态", "条件", "动作" });
                line.y += lineStep;
                EditorGUI.PropertyField(line, property.FindPropertyRelative("key"), new GUIContent("稳定键"));
                line.y += lineStep;
                EditorGUI.PropertyField(line, property.FindPropertyRelative("displayName"), new GUIContent("显示名称"));
                line.y += lineStep;
                EditorGUI.PropertyField(line, property.FindPropertyRelative("category"), new GUIContent("分类"));
                line.y += lineStep;
                EditorGUI.PropertyField(line, property.FindPropertyRelative("description"), new GUIContent("描述"));
                EditorGUI.indentLevel--;
            }
            EditorGUI.EndProperty();
        }
    }
}
