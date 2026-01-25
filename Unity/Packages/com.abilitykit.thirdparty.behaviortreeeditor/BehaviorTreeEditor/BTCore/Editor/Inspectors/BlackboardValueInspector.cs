//------------------------------------------------------------
//        File:  BlackboardValueInspector.cs
//       Brief:  
//
//      Author:  Saroce, Saroce233@163.com
//
//    Modified:  2023-10-14
//============================================================

using System;
using System.Reflection;
using BTCore.Runtime.Blackboards;
using Sirenix.OdinInspector;

namespace BTCore.Editor.Inspectors
{
    public abstract class BlackboardValueInspector : IDataSerializable<BlackboardValue>
    {
        [HorizontalGroup("Variable List")]
        [HideLabel]
        [DisplayAsString]
        public string ValueName;
        
        public abstract void ImportData(BlackboardValue data);
        
        public abstract BlackboardValue ExportData();

        public static BlackboardValueInspector Create(Type keyType) {
            var genericType = typeof(BlackboardValueInspector<>).MakeGenericType(keyType);
            return Activator.CreateInstance(genericType) as BlackboardValueInspector;
        }
    }
    
    public class BlackboardValueInspector<T> : BlackboardValueInspector
    {
        [ShowInInspector]
        [HorizontalGroup("Variable List")]
        [HideLabel]
        [OnValueChanged("OnFieldValueChanged")]
        private  T _value;

        private BlackboardValue _blackboardValue;
        private PropertyInfo _propInfo;

        public override void ImportData(BlackboardValue data) {
            if (data == null) {
                return;
            }
            
            _blackboardValue = data;
            ValueName = _blackboardValue.Name;
            _propInfo = data.GetType().GetProperty("Value");
            _value = (T) _propInfo.GetValue(data);
        }

        public override BlackboardValue ExportData() {
            return _blackboardValue;
        }

        protected void OnFieldValueChanged() {
            if (_blackboardValue == null || _propInfo == null) {
                return;
            }
            
            _propInfo.SetValue(_blackboardValue, _value);
        }

        public void Clear() {
            ValueName = null;
            _value = default;
            _blackboardValue = null;
        }
    }
}