//------------------------------------------------------------
//        File:  SelectorInspector.cs
//       Brief:  SelectorInspector
//
//      Author:  Saroce, Saroce233@163.com
//
//    Modified:  2024-06-24
//============================================================

using BTCore.Runtime;
using BTCore.Runtime.Attributes;
using BTCore.Runtime.Composites;

namespace BTCore.Editor.Inspectors.Composites
{
    [NodeInspector(typeof(Selector))]
    public class SelectorInspector : CompositeInspector<Selector>
    {
        private Selector _selector;
        
        protected override void OnFieldValueChanged() {
            _selector.AbortType = AbortType;
            base.OnFieldValueChanged();
        }

        public override void Reset() {
            _selector = null;
            AbortType = AbortType.None;
        }

        public override BTNode ExportData() {
            return _selector;
        }

        protected override void OnImportData(Selector data) {
            _selector = data;
            AbortType = _selector.AbortType;
        }
    }
}