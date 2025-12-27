//------------------------------------------------------------
//        File:  NodeSearchWindow.cs
//       Brief:  NodeSearchWindow
//
//      Author:  Saroce, Saroce233@163.com
//
//    Modified:  2023-10-05
//============================================================

using System;
using System.Collections.Generic;
using BTCore.Runtime.Composites;
using BTCore.Runtime.Conditions;
using BTCore.Runtime.Decorators;
using BTCore.Runtime.Externals;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using Action = BTCore.Runtime.Actions.Action;

namespace BTCore.Editor
{
    public class NodeSearchWindow : ScriptableObject, ISearchWindowProvider
    {
        private Texture2D _indentIcon;
        private BTNodeView _sourceNode;
        private bool _isAsParent;

        private void Init(BTNodeView sourceNode, bool isAsParent) {
            _sourceNode = sourceNode;
            _isAsParent = isAsParent;
            
            _indentIcon = new Texture2D(1, 1);
            _indentIcon.SetPixel(0, 0, new Color(0, 0, 0, 0));
            _indentIcon.Apply();
        }
        
        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context) {
            var tree = new List<SearchTreeEntry>() {
                new SearchTreeGroupEntry(new GUIContent("Create Node"))
            };

            // Add Composites
            {
                tree.Add(new SearchTreeGroupEntry(new GUIContent("Composites"), 1));
                foreach (var type in TypeCache.GetTypesDerivedFrom<Composite>()) {
                    tree.Add(new SearchTreeEntry(new GUIContent($"{type.Name}", _indentIcon)) { level = 2, userData = type});
                }
            }
            
            // Add Decorators
            {
                tree.Add(new SearchTreeGroupEntry(new GUIContent("Decorators"), 1));
                foreach (var type in TypeCache.GetTypesDerivedFrom<Decorator>()) {
                    tree.Add(new SearchTreeEntry(new GUIContent($"{type.Name}", _indentIcon)) { level = 2, userData = type});
                }
            }
            
            // Add Conditions
            {
                tree.Add(new SearchTreeGroupEntry(new GUIContent("Conditions"), 1));
                foreach (var type in TypeCache.GetTypesDerivedFrom<Condition>()) {
                    if (type.IsSubclassOf(typeof(ExternalCondition))) {
                        continue;
                    }
                    
                    tree.Add(new SearchTreeEntry(new GUIContent($"{type.Name}", _indentIcon)) { level = 2, userData = type});
                }
            }

            // Add Actions (注意：Action节点只能作为叶子节点，因此从Action的输入端口拖出的连线在构建时需要过滤掉Actions)
            if (_sourceNode == null || _isAsParent) {
                tree.Add(new SearchTreeGroupEntry(new GUIContent("Actions"), 1));
                foreach (var type in TypeCache.GetTypesDerivedFrom<Action>()) {
                    if (type.IsSubclassOf(typeof(ExternalAction))) {
                        continue;
                    }
                    
                    tree.Add(new SearchTreeEntry(new GUIContent($"{type.Name}", _indentIcon)) { level = 2, userData = type});
                }
            }
            
            // Add Others
            {
                tree.Add(new SearchTreeGroupEntry(new GUIContent("Others"), 1));
                var stickNodeType = typeof(StickyNote);
                var groupType = typeof(ColorGroup);
                
                var otherTypes = new List<Type>() { stickNodeType, groupType };
                foreach (var type in otherTypes) {
                    tree.Add(new SearchTreeEntry(new GUIContent($"{type.Name}", _indentIcon)) { level = 2, userData = type});   
                }
            }
            
            return tree;
        }
        
        public bool OnSelectEntry(SearchTreeEntry searchTreeEntry, SearchWindowContext context) {
            var type = searchTreeEntry.userData as Type;
            CreateNode(type, context);
            
            return true;
        }

        private void CreateNode(Type type, SearchWindowContext context) {
            var btWindow = BTEditorWindow.Instance;
            var worldMousePos = btWindow.rootVisualElement.ChangeCoordinatesTo(
                btWindow.rootVisualElement.parent, context.screenMousePosition - btWindow.position.position);
            var localMousePos = btWindow.BTView.contentViewContainer.WorldToLocal(worldMousePos);
            btWindow.BTView.CreteNode(type, localMousePos, _sourceNode, _isAsParent);
        }
        
        public static void Show(Vector2 position, BTNodeView sourceNode, bool isAsParent = false) {
            var screenPos = GUIUtility.GUIToScreenPoint(position);
            var createWindow = CreateInstance<NodeSearchWindow>();
            createWindow.Init(sourceNode, isAsParent);
            var windowContext = new SearchWindowContext(screenPos);
            SearchWindow.Open(windowContext, createWindow);
        }
    }
}
