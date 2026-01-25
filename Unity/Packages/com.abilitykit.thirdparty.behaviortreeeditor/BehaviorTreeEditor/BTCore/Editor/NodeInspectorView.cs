//------------------------------------------------------------
//        File:  NodeInspectorView.cs
//       Brief:  NodeInspectorView
//
//      Author:  Saroce, Saroce233@163.com
//
//    Modified:  2023-10-02
//============================================================

using System;
using System.Collections.Generic;
using System.Linq;
using BTCore.Editor.Inspectors;
using BTCore.Runtime.Attributes;
using Sirenix.Utilities;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace BTCore.Editor
{
    public class NodeInspectorView : VisualElement
    {
        public new class UxmlFactory : UxmlFactory<NodeInspectorView, UxmlTraits> { }
        
        private readonly IMGUIContainer _container;

        private static readonly Dictionary<Type, BTNodeInspector> _type2Inspectors = new();
        private static readonly Dictionary<BTNodeInspector, UnityEditor.Editor> _inspector2Editors = new();

        private BTNodeInspector _selectedInspector;
        
        public NodeInspectorView() {
            _container = new IMGUIContainer();
            _container.style.flexGrow = 1;
            Add(_container);
            
            InitNodeInspectors();
        }

        private void InitNodeInspectors() {
            _type2Inspectors.Clear();
            foreach (var inspectorType in TypeCache.GetTypesDerivedFrom<BTNodeInspector>()) {
                var attr = inspectorType.GetCustomAttribute<NodeInspectorAttribute>();
                if (attr == null) {
                    continue;
                }

                var inspector = ScriptableObject.CreateInstance(inspectorType) as BTNodeInspector;
                _type2Inspectors.Add(attr.NodeType, inspector);
            }
        }

        public void UpdateSelection(BTNodeView nodeView) {
            ClearSelection();

            // 从Runtime状态退回到Editor状态，创建的inspector会全部为空，这里需要清空重新添加一遍
            if (_type2Inspectors.Values.All(v => v == null)) {
                InitNodeInspectors();
                _inspector2Editors.Clear();
            }

            if (!_type2Inspectors.TryGetValue(nodeView.Node.GetType(), out var inspector)) {
                return;
            }

            if (inspector == null) {
                return;
            }
            
            _selectedInspector = inspector;
            if (!_inspector2Editors.TryGetValue(inspector, out var editor)) {
                editor = UnityEditor.Editor.CreateEditor(inspector);
                _inspector2Editors.Add(inspector, editor);
            }
            
            // Node Inspector字段须在Reset里面重新进行初始化
            inspector.Reset();
            inspector.ImportData(nodeView.Node);
            inspector.OnNodeViewUpdate = nodeView.UpdateView;
            
            _container.onGUIHandler = () => {
                if (editor.target) {
                    editor.OnInspectorGUI();
                }
            };
        }

        public void ClearSelection() {
            _container.onGUIHandler = null;
        }
        
        public void UpdateNodeBindValues() {
            if (_selectedInspector == null) {
                return;
            }
            
            _selectedInspector.OnBlackboardValueChanged();
        }
        
    }
}