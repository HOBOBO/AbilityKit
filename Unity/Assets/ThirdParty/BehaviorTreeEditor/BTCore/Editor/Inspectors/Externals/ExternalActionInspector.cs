//------------------------------------------------------------
//        File:  ExternalActionInspector.cs
//       Brief:  ExternalActionInspector
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
    [NodeInspector(typeof(ExternalAction))]
    public class ExternalActionInspector : ExternalNodeInspector<ExternalAction>
    {
        protected override string GetInfoMessage() {
            return "必须填写外部Action类型名称!";
        }

        protected override string GetTitleName() {
            return "External Action";
        }

        public override BTNode ExportData() {
            return ExternalNode as BTNode;
        }

        protected override void OnImportData(ExternalAction data) {
            ExternalNode = data;
            TypeName = data.TypeName;
            Properties = data.Properties;
        }
    }
}