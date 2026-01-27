using System;
using Emilia.Reference;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Emilia.Variables
{
    [Serializable, LabelText("Color(颜色)")]
    public class VariableColor : Variable<Color>
    {
        public static implicit operator VariableColor(Color value)
        {
            VariableColor variableValue = ReferencePool.Acquire<VariableColor>();
            variableValue.value = value;
            return variableValue;
        }

        public static implicit operator Color(VariableColor value)
        {
            return value.value;
        }

        public bool Equals(VariableColor other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            if (GetType() != other.GetType()) return false;
            return value == other.value;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((VariableColor) obj);
        }

        public override int GetHashCode()
        {
            return value.GetHashCode();
        }

        public static bool operator ==(VariableColor a, VariableColor b)
        {
            if (ReferenceEquals(a, null) == false) return a.Equals(b);
            if (ReferenceEquals(b, null)) return true;
            return false;
        }

        public static bool operator !=(VariableColor a, VariableColor b)
        {
            return ! (a == b);
        }
    }
}