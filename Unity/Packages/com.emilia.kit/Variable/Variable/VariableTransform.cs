using System;
using Emilia.Reference;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Emilia.Variables
{
    [Serializable, LabelText("Transform(变换)")]
    public class VariableTransform : Variable<Transform>
    {
        public static implicit operator VariableTransform(Transform value)
        {
            VariableTransform varValue = ReferencePool.Acquire<VariableTransform>();
            varValue.value = value;
            return varValue;
        }

        public static implicit operator Transform(VariableTransform value)
        {
            return value.value;
        }

        public bool Equals(VariableTransform other)
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
            return Equals((VariableTransform) obj);
        }

        public override int GetHashCode()
        {
            return value.GetHashCode();
        }

        public static bool operator ==(VariableTransform a, VariableTransform b)
        {
            if (ReferenceEquals(a, null) == false) return a.Equals(b);
            if (ReferenceEquals(b, null)) return true;
            return false;
        }

        public static bool operator !=(VariableTransform a, VariableTransform b)
        {
            return ! (a == b);
        }
    }
}