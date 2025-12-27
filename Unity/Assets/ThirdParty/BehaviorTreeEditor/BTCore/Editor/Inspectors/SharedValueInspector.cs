//------------------------------------------------------------
//        File:  SharedValueInspector.cs
//       Brief:  
//
//      Author:  Saroce, Saroce233@163.com
//
//    Modified:  2023-10-11
//============================================================

using System.Collections;
using System.Collections.Generic;
using BTCore.Runtime;
using Sirenix.OdinInspector;

namespace BTCore.Editor.Inspectors
{
    public abstract class SharedValueInspector
    {
        public abstract void RebindValueName();
    }
    
    [HideReferenceObjectPicker]
    [InlineProperty]
    public class SharedValueInspector<T> : SharedValueInspector, IDataSerializable<SharedValue<T>>
    {
        [ShowInInspector]
        [HideLabel]
        [HorizontalGroup]
        [ShowIf("_showRawValue")]
        [InlineButton("SelectBlackboardValue", "▼")]
        [OnValueChanged("OnFieldValueChanged")]
        private T _rawValue;

        [ShowInInspector]
        [HideLabel]
        [HorizontalGroup]
        [ValueDropdown("GetValueNames")]
        [ShowIf("_showValueName")]
        [InlineButton("EditRawValue", "↺")]
        [OnValueChanged("OnFieldValueChanged")]
        private string _bindValueName = NONE_VALUE;
        
        private const string NONE_VALUE = "(None)";
        
        private void SelectBlackboardValue() {
            _showRawValue = false;
        }
        
        private void EditRawValue() {
            _showRawValue = true;
            _bindValueName = NONE_VALUE;
        }

        private SharedValue<T> _sharedValue;
        private bool _showRawValue = true;
        private bool _showValueName => !_showRawValue;

        private IEnumerable GetValueNames() {
            var blackboard = BTEditorWindow.Instance.Blackboard;
            if (blackboard == null) {
                return new List<string>() { NONE_VALUE };
            }

            var valueNames = new List<string>();
            foreach (var blackboardValue in blackboard.Values) {
                if (blackboardValue.Type == typeof(T)) {
                    valueNames.Add(blackboardValue.Name);
                }
            }

            return valueNames;
        }

        public void ImportData(SharedValue<T> data) {
            _sharedValue = data;
            _showRawValue = string.IsNullOrEmpty(data.ValueName);
            
            if (_showRawValue) {
                _rawValue = data.RawValue;
            }
            else {
                _bindValueName = data.ValueName;
            }
        }

        public SharedValue<T> ExportData() {
            return _sharedValue;
        }

        private void OnFieldValueChanged() {
            _sharedValue.RawValue = _rawValue;
            _sharedValue.ValueName = _bindValueName == NONE_VALUE ? string.Empty : _bindValueName;
        }

        public override void RebindValueName() {
            if (_bindValueName == NONE_VALUE) {
                return;
            }
            
            var blackboard = BTEditorWindow.Instance.Blackboard;
            if (blackboard == null) {
                return;
            }

            var isFind = false;
            foreach (var key in blackboard.Values) {
                if (key.Type == typeof(T) && key.Name == _bindValueName) {
                    isFind = true;
                }
            }

            _bindValueName = isFind ? _bindValueName : NONE_VALUE;
        }
    }
}
