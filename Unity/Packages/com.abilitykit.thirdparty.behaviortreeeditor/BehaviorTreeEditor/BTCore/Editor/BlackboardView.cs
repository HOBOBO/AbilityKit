//------------------------------------------------------------
//        File:  BlackboardView.cs
//       Brief:  BlackboardView
//
//      Author:  Saroce, Saroce233@163.com
//
//    Modified:  2023-10-08
//============================================================

using System;
using BTCore.Editor.Inspectors;
using BTCore.Runtime.Blackboards;
using UnityEngine;
using UnityEngine.UIElements;

namespace BTCore.Editor
{
    public class BlackboardView : VisualElement, IDataSerializable<Blackboard>
    {
        public new class UxmlFactory : UxmlFactory<BlackboardView, UxmlTraits> { }

        private readonly IMGUIContainer _container;
        private BlackboardInspector _blackboardInspector;

        public Action OnValueListChanged;
        
        public BlackboardView() {
            // 打开BTEditorWindow的uxml文件，会初始化BlackboardView，导致Odin部分序列化报错，这里直接判空返回处理
            if (BTEditorWindow.Instance == null) {
                return;
            }
            
            _container = new IMGUIContainer();
            _container.style.flexGrow = 1;
            Add(_container);
            
            CreateBlackboardInspector();
        }

        private void CreateBlackboardInspector() {
            if (_blackboardInspector == null) {
                _blackboardInspector = ScriptableObject.CreateInstance<BlackboardInspector>();
                var editor = UnityEditor.Editor.CreateEditor(_blackboardInspector);
                if (_container != null)
                    _container.onGUIHandler = () => {
                        if (editor.target) {
                            editor.OnInspectorGUI();
                        }
                    };
            }
        }
        
        public void ImportData(Blackboard data) {
            // 从Runtime状态退回到Editor状态，_blackboardInspector可能为空，这里重新创建添加一遍
            if (_blackboardInspector == null) {
                CreateBlackboardInspector();
            }
            
            if (_blackboardInspector != null) {
                _blackboardInspector.ImportData(data);
            }

            _blackboardInspector.OnValueListChanged ??= OnValueListChanged;
        }

        public Blackboard ExportData() {
            return _blackboardInspector != null ? _blackboardInspector.ExportData() : null;
        }
    }
}
