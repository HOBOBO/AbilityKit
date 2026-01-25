//------------------------------------------------------------
//        File:  LogInspector.cs
//       Brief:  LogInspector
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
    [NodeInspector(typeof(Log))]
    public class LogInspector : BTNodeInspector<Log>
    {
        [ShowInInspector]
        [LabelText("Log")]
        [LabelWidth(100)]
        [OnValueChanged("OnFieldValueChanged")]
        private SharedValueInspector<string> _message = new SharedValueInspector<string>();
        
        private Log _logData;

        protected override void OnImportData(Log data) {
            _logData = data;
            _message.ImportData(data.Message);
        }

        public override BTNode ExportData() {
            return _logData;
        }

        protected override void OnFieldValueChanged() {
            _logData.Message = _message.ExportData();
            base.OnFieldValueChanged();
        }

        public override void Reset() {
            _message = new SharedValueInspector<string>();
            _logData = null;
        }
    }
}