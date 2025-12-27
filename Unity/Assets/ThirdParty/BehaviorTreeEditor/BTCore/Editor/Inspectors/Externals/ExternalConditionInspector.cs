//------------------------------------------------------------
//        File:  ExternalCondition.cs
//       Brief:  ExternalCondition
//
//      Author:  Saroce, Saroce233@163.com
//
//    Modified:  2023-10-18
//============================================================

using BTCore.Runtime;
using BTCore.Runtime.Attributes;
using BTCore.Runtime.Externals;

namespace BTCore.Editor.Inspectors.Externals
{
    [NodeInspector(typeof(ExternalCondition))]
    public class ExternalConditionInspector : ExternalNodeInspector<ExternalCondition>
    {
        protected override string GetInfoMessage() {
            return "必须填写外部Condition类型名称!";
        }

        protected override string GetTitleName() {
            return "External Condition";
        }

        public override BTNode ExportData() {
            return ExternalNode as BTNode;
        }

        protected override void OnImportData(ExternalCondition data) {
            ExternalNode = data;
            TypeName = data.TypeName;
            Properties = data.Properties;
        }
    }
}