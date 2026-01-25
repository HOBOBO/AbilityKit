//------------------------------------------------------------
//        File:  SequenceInspector.cs
//       Brief:  SequenceInspector
//
//      Author:  Saroce, Saroce233@163.com
//
//    Modified:  2024-06-19
//============================================================

using BTCore.Runtime;
using BTCore.Runtime.Attributes;
using BTCore.Runtime.Composites;

namespace BTCore.Editor.Inspectors.Composites
{
    [NodeInspector(typeof(Sequence))]
    public class SequenceInspector : CompositeInspector<Sequence>
    {
        private Sequence _sequence;

        protected override void OnImportData(Sequence data) {
            _sequence = data;
            AbortType = data.AbortType;
        }

        public override BTNode ExportData() {
            return _sequence;
        }
        
        protected override void OnFieldValueChanged() {
            _sequence.AbortType = AbortType;
            base.OnFieldValueChanged();
        }

        public override void Reset() {
            _sequence = null;
            AbortType = AbortType.None;
        }
    }
}