//------------------------------------------------------------
//        File:  RandomSequenceInspector.cs
//       Brief:  RandomSequenceInspector
//
//      Author:  Saroce, Saroce233@163.com
//
//    Modified:  2024-07-04
//============================================================

using BTCore.Runtime;
using BTCore.Runtime.Attributes;
using BTCore.Runtime.Composites;
using Sirenix.OdinInspector;

namespace BTCore.Editor.Inspectors.Composites
{
    [NodeInspector(typeof(RandomSequence))]
    public class RandomSequenceInspector : CompositeInspector<RandomSequence>
    {
        [ShowInInspector]
        [HorizontalGroup]
        [LabelText("Use Seed:")]
        [LabelWidth(100)]
        [OnValueChanged("OnFieldValueChanged")]
        private bool _useSeed;
        
        [ShowInInspector]
        [HorizontalGroup]
        [LabelText("Seed:")]
        [LabelWidth(40)]
        [ShowIf("_useSeed")]
        [OnValueChanged("OnFieldValueChanged")]
        private int _seed;
        
        private RandomSequence _sequence;
        

        public override BTNode ExportData() {
            return _sequence;
        }

        protected override void OnFieldValueChanged() {
            _sequence.Seed = _seed;
            _sequence.UseSeed = _useSeed;
            base.OnFieldValueChanged();
        }

        protected override void OnImportData(RandomSequence data) {
            _sequence = data;
            _seed = data.Seed;
            _useSeed = data.UseSeed;
        }
        
        public override void Reset() {
            _sequence = null;
            AbortType = AbortType.None;
        }
    }
}