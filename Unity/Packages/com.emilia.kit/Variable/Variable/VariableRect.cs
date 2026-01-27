using System;
using Emilia.Reference;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Emilia.Variables
{
    [Serializable, LabelText("Rect(矩形)")]
    public class VariableRect : Variable<Rect>
    {
        public static implicit operator VariableRect(Rect value)
        {
            VariableRect varValue = ReferencePool.Acquire<VariableRect>();
            varValue.value = value;
            return varValue;
        }

        public static implicit operator Rect(VariableRect value)
        {
            return value.value;
        }

        public bool Equals(VariableRect other)
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
            return Equals((VariableRect) obj);
        }

        public override int GetHashCode()
        {
            return value.GetHashCode();
        }

        public static bool operator ==(VariableRect a, VariableRect b)
        {
            if (ReferenceEquals(a, null) == false) return a.Equals(b);
            if (ReferenceEquals(b, null)) return true;
            return false;
        }

        public static bool operator !=(VariableRect a, VariableRect b)
        {
            return ! (a == b);
        }
    }
}