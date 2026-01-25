//------------------------------------------------------------
//        File:  ProbabilityInspector.cs
//       Brief:  ProbabilityInspector
//
//      Author:  Saroce, Saroce233@163.com
//
//    Modified:  2023-10-16
//============================================================

using BTCore.Runtime;
using BTCore.Runtime.Attributes;
using BTCore.Runtime.Conditions;
using Sirenix.OdinInspector;

namespace BTCore.Editor.Inspectors.Conditions
{
    [NodeInspector(typeof(RandomProbability))]
    public class RandomProbabilityInspector : BTNodeInspector<RandomProbability>
    {
        [ShowInInspector]
        [LabelText("Probability(?)")]
        [LabelWidth(100)]
        [OnValueChanged("OnFieldValueChanged")]
        [PropertyTooltip("概率范围0 ~ 100")]
        private SharedValueInspector<int> _probability = new SharedValueInspector<int>();

        private RandomProbability _probabilityData;
        
        protected override void OnImportData(RandomProbability data) {
            _probabilityData = data;
            _probability.ImportData(data.Probability);
        }

        public override BTNode ExportData() {
            return _probabilityData;
        }
        
        protected override void OnFieldValueChanged() {
            _probabilityData.Probability = _probability.ExportData();
            base.OnFieldValueChanged();
        }

        public override void Reset() {
            _probability = new SharedValueInspector<int>();
            _probabilityData = null;
        }
    }
}