using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Emilia.Variables
{
    [Serializable]
    public abstract class Variable<T> : Variable
    {
        [SerializeField, HideLabel]
        private T _value = default;

        public override Type type => typeof(T);

        public T value
        {
            get => this._value;
            set => this._value = value;
        }

        public override object GetValue()
        {
            return this._value;
        }

        public override void SetValue(object value)
        {
            this._value = (T) value;
        }

        public override void Clear()
        {
            this._value = default;
        }

        public override string ToString()
        {
            return this._value != null ? this._value.ToString() : "Null";
        }
    }
}