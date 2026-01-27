using System;
using Emilia.Reference;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Emilia.Variables
{
    [Serializable, LabelText("Quaternion(四元数)")]
    public class VariableQuaternion : Variable<Quaternion>
    {
        public static implicit operator VariableQuaternion(Quaternion value)
        {
            VariableQuaternion varValue = ReferencePool.Acquire<VariableQuaternion>();
            varValue.value = value;
            return varValue;
        }

        public static implicit operator Quaternion(VariableQuaternion value)
        {
            return value.value;
        }

        public bool Equals(VariableQuaternion other)
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
            return Equals((VariableQuaternion) obj);
        }

        public override int GetHashCode()
        {
            return value.GetHashCode();
        }

        public static bool operator ==(VariableQuaternion a, VariableQuaternion b)
        {
            if (ReferenceEquals(a, null) == false) return a.Equals(b);
            if (ReferenceEquals(b, null)) return true;
            return false;
        }

        public static bool operator !=(VariableQuaternion a, VariableQuaternion b)
        {
            return ! (a == b);
        }

        public static VariableQuaternion operator *(VariableQuaternion a, VariableQuaternion b)
        {
            return a.value *= b.value;
        }
    }
}