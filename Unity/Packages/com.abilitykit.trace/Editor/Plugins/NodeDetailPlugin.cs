#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using AbilityKit.Editor.Framework;

namespace AbilityKit.Trace.Editor.Windows
{
    /// <summary>
    /// 节点详情插件 - 显示选中节点的详细信息
    /// </summary>
    public class NodeDetailPlugin : BaseWindowPlugin<TraceRootViewData>
    {
        private TraceTreeViewModel _viewModel;
        private Vector2 _scrollPosition;

        public NodeDetailPlugin(TraceTreeViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        public override int Priority => 5;

        public override void OnDetailHeaderGUI()
        {
            var selectedNode = _viewModel.SelectedNode;
            if (selectedNode != null)
            {
                EditorGUILayout.LabelField($"Node Details - #{selectedNode.ContextId}", EditorStyles.boldLabel);
            }
            else
            {
                EditorGUILayout.LabelField("Node Details", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("Click on a node in the tree view to see its details.", MessageType.Info);
            }
        }

        public override void OnDetailGUI(TraceRootViewData item)
        {
            var selectedNode = _viewModel.SelectedNode;
            if (selectedNode == null)
            {
                DrawTreeOverview(item);
                return;
            }

            DrawNodeDetails(selectedNode);
        }

        private void DrawTreeOverview(TraceRootViewData rootData)
        {
            if (rootData == null) return;

            EditorGUILayout.LabelField("Tree Overview", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            EditorGUILayout.LabelField($"Root ID: {rootData.RootId}");
            EditorGUILayout.LabelField($"Kind: {rootData.KindName}");
            EditorGUILayout.LabelField($"Is Active: {rootData.IsActive}");
            EditorGUILayout.LabelField($"Active Count: {rootData.ActiveCount}");
            EditorGUILayout.LabelField($"Node Count: {rootData.NodeCount}");

            EditorGUI.indentLevel--;

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Select a node in the tree view to see its details.", MessageType.Info);
        }

        private void DrawNodeDetails(TraceNodeViewData node)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField("Basic Info", EditorStyles.miniBoldLabel);

            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField($"Context ID: {node.ContextId}");
            EditorGUILayout.LabelField($"Root ID: {node.RootId}");
            EditorGUILayout.LabelField($"Parent ID: {node.ParentId}");
            EditorGUILayout.LabelField($"Kind: {node.KindName}");
            EditorGUILayout.LabelField($"Is Root: {node.IsRoot}");
            EditorGUILayout.LabelField($"Is Ended: {node.IsEnded}");
            EditorGUILayout.LabelField($"Child Count: {node.ChildCount}");
            EditorGUILayout.LabelField($"Level: {node.Level}");
            EditorGUI.indentLevel--;
            EditorGUI.indentLevel--;

            EditorGUILayout.Space();

            if (node.Metadata != null)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("Metadata", EditorStyles.miniBoldLabel);
                DrawMetadataFields(node.Metadata);
                EditorGUI.indentLevel--;
            }
        }

        private void DrawMetadataFields(object metadata)
        {
            var type = metadata.GetType();

            // 获取所有公共字段和属性
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            EditorGUI.indentLevel++;

            foreach (var field in fields)
            {
                try
                {
                    var value = field.GetValue(metadata);
                    DrawValue($"{field.Name}:", value);
                }
                catch
                {
                    // 忽略无法读取的字段
                }
            }

            foreach (var prop in properties)
            {
                try
                {
                    if (prop.GetIndexParameters().Length > 0) continue; // 跳过索引属性
                    var value = prop.GetValue(metadata);
                    DrawValue($"{prop.Name}:", value);
                }
                catch
                {
                    // 忽略无法读取的属性
                }
            }

            EditorGUI.indentLevel--;
        }

        private void DrawValue(string label, object value)
        {
            if (value == null)
            {
                EditorGUILayout.LabelField(label, "null");
                return;
            }

            // 处理常见类型
            if (value is bool boolValue)
            {
                GUI.color = boolValue ? Color.green : Color.red;
                EditorGUILayout.LabelField(label, boolValue ? "True" : "False");
                GUI.color = Color.white;
            }
            else if (value is int intValue)
            {
                EditorGUILayout.LabelField(label, intValue.ToString());
            }
            else if (value is long longValue)
            {
                EditorGUILayout.LabelField(label, longValue.ToString());
            }
            else if (value is float floatValue)
            {
                EditorGUILayout.LabelField(label, floatValue.ToString("F2"));
            }
            else if (value is double doubleValue)
            {
                EditorGUILayout.LabelField(label, doubleValue.ToString("F2"));
            }
            else if (value is string stringValue)
            {
                if (stringValue.Length > 100)
                    stringValue = stringValue.Substring(0, 97) + "...";
                EditorGUILayout.LabelField(label, stringValue);
            }
            else if (value is System.Collections.IEnumerable enumerable && !(value is string))
            {
                var list = new List<object>();
                foreach (var item in enumerable)
                {
                    list.Add(item);
                }
                EditorGUILayout.LabelField(label, $"[{list.Count} items]");
            }
            else
            {
                var str = value.ToString();
                if (str.Length > 50)
                    str = str.Substring(0, 47) + "...";
                EditorGUILayout.LabelField(label, str);
            }
        }
    }
}
#endif
