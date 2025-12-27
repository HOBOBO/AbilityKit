//------------------------------------------------------------
//        File:  WaitInspector.cs
//       Brief:  WaitInspector
//
//      Author:  Saroce, Saroce233@163.com
//
//    Modified:  2023-10-08
//============================================================


using BTCore.Runtime;
using BTCore.Runtime.Actions;
using BTCore.Runtime.Attributes;
using Sirenix.OdinInspector;

namespace BTCore.Editor.Inspectors.Actions
{
    [NodeInspector(typeof(Wait))]
    public class WaitInspector : BTNodeInspector<Wait>
    {
        [ShowInInspector]
        [LabelText("Duration(ms)")]
        [LabelWidth(100)]
        [OnValueChanged("OnFieldValueChanged")]
        private int _duration;
        
        private Wait _waitData;

        protected override void OnImportData(Wait data) {
            _waitData = data;
            _duration = data.Duration;
        }

        public override BTNode ExportData() {
            return _waitData;
        }

        protected override void OnFieldValueChanged() {
            _waitData.Duration = _duration;
            base.OnFieldValueChanged();
        }

        public override void Reset() {
            _duration = 0;
            _waitData = null;
        }
    }
}