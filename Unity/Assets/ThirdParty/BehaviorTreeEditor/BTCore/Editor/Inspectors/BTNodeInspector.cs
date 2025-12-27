//------------------------------------------------------------
//        File:  BTNodeInspector.cs
//       Brief:  BTNodeInspector
//
//      Author:  Saroce, Saroce233@163.com
//
//    Modified:  2023-10-08
//============================================================

using System;
using BTCore.Runtime;
using Sirenix.OdinInspector;
using UnityEngine;

namespace BTCore.Editor.Inspectors
{
    public abstract class BTNodeInspector : InspectorBase, IDataSerializable<BTNode>
    {
        [HideInInspector]
        public Action OnNodeViewUpdate;

        [TextArea]
        [OnValueChanged("OnFieldValueChanged")]
        public string Comment;
        
        public abstract void ImportData(BTNode data);
        public abstract BTNode ExportData();
        
        public void OnBlackboardValueChanged() {
            foreach (var fieldInfo in GetType().GetFields(BTEditorDef.BindValueFlags)) {
                if (fieldInfo.FieldType.IsSubclassOf(typeof(SharedValueInspector))) {
                    var svInspector = fieldInfo.GetValue(this) as SharedValueInspector;
                    svInspector?.RebindValueName();
                }
            }
        }
    }
    
    public abstract class BTNodeInspector<T> : BTNodeInspector where T : BTNode
    {
        private BTNode _btNode;
        
        public override void ImportData(BTNode data) {
            _btNode = data;
            Comment = data.Comment;
            OnImportData(data as T);
        }

        protected abstract void OnImportData(T data);

        // 字段数值变化后，可能需要刷新下对应NodeView的显示，这个应放到最后调用
        protected override void OnFieldValueChanged() {
            _btNode.Comment = Comment;
            OnNodeViewUpdate?.Invoke();
        }
    }
}