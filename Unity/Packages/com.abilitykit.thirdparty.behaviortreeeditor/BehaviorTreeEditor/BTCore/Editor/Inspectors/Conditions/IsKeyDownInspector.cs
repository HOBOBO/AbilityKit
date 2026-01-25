//------------------------------------------------------------
//        File:  IsKeyDownInspector.cs
//       Brief:  IsKeyDownInspector
//
//      Author:  Saroce, Saroce233@163.com
//
//    Modified:  2024-06-25
//============================================================

using BTCore.Runtime;
using BTCore.Runtime.Attributes;
using BTCore.Runtime.Unity.Conditions;
using Sirenix.OdinInspector;
using UnityEngine;

namespace BTCore.Editor.Inspectors.Conditions
{
    [NodeInspector(typeof(IsKeyDown))]
    public class IsKeyDownInspector : BTNodeInspector<IsKeyDown>
    {
        [ShowInInspector]
        [LabelText("按键:")]
        [LabelWidth(100)]
        [OnValueChanged("OnFieldValueChanged")]
        private KeyCode _keyCode;
        
        private IsKeyDown _isKeyDown;
        
        protected override void OnFieldValueChanged() {
            _isKeyDown.KeyCode = _keyCode;
            base.OnFieldValueChanged();
        }

        public override void Reset() {
            _isKeyDown = null;
            _keyCode = KeyCode.None;
        }

        public override BTNode ExportData() {
            return _isKeyDown;
        }

        protected override void OnImportData(IsKeyDown data) {
            _isKeyDown = data;
            _keyCode = data.KeyCode;
        }
    }
}