using System;
using Emilia.Reference;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Emilia.Variables
{
    [Serializable, LabelText("Vector4(四维向量)")]
    public sealed class VariableVector4 : Variable<Vector4>
    {
        public static implicit operator VariableVector4(Vector4 value)
        {
            VariableVector4 varValue = ReferencePool.Acquire<VariableVector4>();
            varValue.value = value;
            return varValue;
        }

        public static implicit operator Vector4(VariableVector4 value)
        {
            return value.value;
        }

        public static explicit operator VariableVector4(VariableVector2 value)
        {
            VariableVector4 varValue = ReferencePool.Acquire<VariableVector4>();
            varValue.value = new Vector4(value.value.x, value.value.y, 0, 0);
            return varValue;
        }

        public static explicit operator VariableVector4(VariableVector3 value)
        {
            VariableVector4 varValue = ReferencePool.Acquire<VariableVector4>();
            varValue.value = new Vector4(value.value.x, value.value.y, value.value.z, 0);
            return varValue;
        }

        public bool Equals(VariableVector4 other)
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
            return Equals((VariableVector4) obj);
        }

        public override int GetHashCode()
        {
            return value.GetHashCode();
        }

        public static bool operator ==(VariableVector4 a, VariableVector4 b)
        {
            if (ReferenceEquals(a, null) == false) return a.Equals(b);
            if (ReferenceEquals(b, null)) return true;
            return false;
        }

        public static bool operator !=(VariableVector4 a, VariableVector4 b)
        {
            return ! (a == b);
        }

        public static VariableVector4 operator +(VariableVector4 a, VariableVector4 b)
        {
            return a.value += b.value;
        }

        public static VariableVector4 operator -(VariableVector4 a, VariableVector4 b)
        {
            return a.value -= b.value;
        }
    }
}