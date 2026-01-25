//------------------------------------------------------------
//        File:  BlackboardInspector.cs
//       Brief:  BlackboardInspector
//
//      Author:  Saroce, Saroce233@163.com
//
//    Modified:  2023-10-09
//============================================================

using System;
using System.Collections;
using System.Collections.Generic;
using BTCore.Runtime.Blackboards;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace BTCore.Editor.Inspectors
{
    public class BlackboardInspector : InspectorBase, IDataSerializable<Blackboard>
    {
        [ShowInInspector]
        [LabelText("Name:")]
        [LabelWidth(100)]
        private string _valueName;
        
        [ShowInInspector]
        [LabelText("Type:")]
        [LabelWidth(100)]
        [ValueDropdown("GetValueTypeNames")]
        [InlineButton("AddBlackboardValue", "Add")]
        private string _valueTypeName = "Int";

        [ShowInInspector]
        [TableList(AlwaysExpanded = true, HideToolbar = true, ShowIndexLabels = true, ShowPaging = true)]
        [ShowIf("_showValueInspectors")]
        [OnValueChanged("OnFieldValueChanged")]
        private List<BlackboardValueInspector> _valueInspectors = new List<BlackboardValueInspector>();

        private Blackboard _blackboard;
        private readonly Dictionary<string, Type> _typeName2ValueTypes = new Dictionary<string, Type>();
        private bool _showValueInspectors => _valueInspectors.Count > 0;

        [HideInInspector]
        public Action OnValueListChanged;

        public void ImportData(Blackboard data) {
            _blackboard = data;
            _valueInspectors = data.Values.ConvertAll(keyData => {
                var inspector = BlackboardValueInspector.Create(keyData.Type);
                inspector.ImportData(keyData);
                return inspector;
            });
        }

        public Blackboard ExportData() {
            return _blackboard;
        }
        
        private void AddBlackboardValue() {
            if (string.IsNullOrEmpty(_valueName)) {
                BTEditorWindow.Instance.ShowNotification("变量名不能为空!");
                return;
            }
            
            var foundKey = _valueInspectors.Find(key => key.ValueName == _valueName);
            if (foundKey != null) {
                BTEditorWindow.Instance.ShowNotification("已存在同名变量!");
                return;
            }

            if (!_typeName2ValueTypes.TryGetValue(_valueTypeName, out var keyType)) {
                return;
            }

            var keyData = BlackboardValue.Create(keyType, _valueName);
            if (keyData != null) {
                _blackboard.Values.Add(keyData);
                var inspector = BlackboardValueInspector.Create(keyData.Type);
                inspector.ImportData(keyData);
                _valueInspectors.Add(inspector);
            }
        }

        private IEnumerable GetValueTypeNames() { 
            if (_typeName2ValueTypes.Count > 0) {
                return _typeName2ValueTypes.Keys;
            }
            
            foreach (var type in TypeCache.GetTypesDerivedFrom<BlackboardValue>()) {
                if (type.IsGenericType) {
                    continue;
                }
                _typeName2ValueTypes.Add(type.Name.Replace("Value", ""), type);
            }
            
            return _typeName2ValueTypes.Keys;;
        }
        
        protected override void OnFieldValueChanged() {
            _blackboard.Values = _valueInspectors.ConvertAll(inspector => inspector.ExportData());
            OnValueListChanged?.Invoke();
        }

        public override void Reset() {
            
        }
    }
}