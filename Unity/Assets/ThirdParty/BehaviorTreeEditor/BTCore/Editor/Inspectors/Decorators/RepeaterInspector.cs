//------------------------------------------------------------
//        File:  RepeaterInspector.cs
//       Brief:  RepeaterInspector
//
//      Author:  Saroce, Saroce233@163.com
//
//    Modified:  2023-10-16
//============================================================

using BTCore.Runtime;
using BTCore.Runtime.Attributes;
using BTCore.Runtime.Composites;
using BTCore.Runtime.Decorators;
using Sirenix.OdinInspector;

namespace BTCore.Editor.Inspectors.Decorators
{
    [NodeInspector(typeof(Repeater))]
    public class RepeaterInspector : BTNodeInspector<Repeater>
    {
        [ShowInInspector]
        [LabelText("Repeat Count(?)")]
        [LabelWidth(100)]
        [OnValueChanged("OnFieldValueChanged")]
        [PropertyTooltip("循环次数，设定为负数一直循环执行")]
        private int _count;

        private Repeater _repeater;
        
        protected override void OnImportData(Repeater data) {
            _repeater = data;
            _count = data.RepeatCount;
        }

        public override BTNode ExportData() {
            return _repeater;
        }
        
        protected override void OnFieldValueChanged() {
            _repeater.RepeatCount = _count;
            base.OnFieldValueChanged();
        }

        public override void Reset() {
            _count = 0;
            _repeater = null;
        }
    }
}