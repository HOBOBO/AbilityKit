//------------------------------------------------------------
//        File:  RandomSelectorInspector.cs
//       Brief:  RandomSelectorInspector
//
//      Author:  Saroce, Saroce233@163.com
//
//    Modified:  2024-07-05
//============================================================

using BTCore.Runtime;
using BTCore.Runtime.Attributes;
using BTCore.Runtime.Composites;
using Sirenix.OdinInspector;

namespace BTCore.Editor.Inspectors.Composites
{
    [NodeInspector(typeof(RandomSelector))]
    public class RandomSelectorInspector : CompositeInspector<RandomSelector>
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
        
        private RandomSelector _selector;
        

        public override BTNode ExportData() {
            return _selector;
        }

        protected override void OnFieldValueChanged() {
            _selector.Seed = _seed;
            _selector.UseSeed = _useSeed;
            base.OnFieldValueChanged();
        }

        protected override void OnImportData(RandomSelector data) {
            _selector = data;
            _seed = data.Seed;
            _useSeed = data.UseSeed;
        }
        
        public override void Reset() {
            _selector = null;
            AbortType = AbortType.None;
        }
    }
}