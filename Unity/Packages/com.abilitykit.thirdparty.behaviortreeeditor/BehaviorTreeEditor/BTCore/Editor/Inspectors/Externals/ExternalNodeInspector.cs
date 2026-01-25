//------------------------------------------------------------
//        File:  ExternalNodeInspector.cs
//       Brief:  ExternalNodeInspector
//
//      Author:  Saroce, Saroce233@163.com
//
//    Modified:  2023-10-18
//============================================================

using System.Collections.Generic;
using BTCore.Runtime;
using BTCore.Runtime.Externals;
using Sirenix.OdinInspector;

namespace BTCore.Editor.Inspectors.Externals
{
    public abstract class ExternalNodeInspector<T> : BTNodeInspector<T> where T : BTNode
    {
        [TitleGroup("$GetTitleName")]
        [ShowInInspector]
        [LabelText("TypeName")]
        [LabelWidth(100)]
        [OnValueChanged("OnFieldValueChanged")]
        [ValidateInput("StringValidator", "$GetInfoMessage", ContinuousValidationCheck = true)]
        protected string TypeName;

        [TitleGroup("$GetTitleName")]
        [ShowInInspector]
        [OnValueChanged("OnFieldValueChanged")]
        protected Dictionary<string, string> Properties = new Dictionary<string, string>();

        protected IExternalNode ExternalNode;
        
        private bool StringValidator(string value)
        {
            return !string.IsNullOrEmpty(value);
        }

        protected abstract string GetInfoMessage();

        protected abstract string GetTitleName();

        protected override void OnFieldValueChanged() {
            if (ExternalNode == null) {
                return;
            }

            ExternalNode.TypeName = TypeName;
            ExternalNode.Properties = Properties;
            // 更新Name字段，同步更新对应NodeView的title显示
            if (ExternalNode is BTNode node) {
                node.Name = TypeName;
                node.Comment = Comment;
                base.OnFieldValueChanged();
            }
        }

        public override void Reset() {
            TypeName = null;
            Properties = new Dictionary<string, string>();
            ExternalNode = null;
        }
    }
}