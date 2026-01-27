using System;
using Emilia.Reference;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Emilia.Variables
{
    [Serializable, LabelText("Vector2(二维向量)")]
    public sealed class VariableVector2 : Variable<Vector2>
    {
        public static implicit operator VariableVector2(Vector2 value)
        {
            VariableVector2 varValue = ReferencePool.Acquire<VariableVector2>();
            varValue.value = value;
            return varValue;
        }

        public static implicit operator Vector2(VariableVector2 value)
        {
            return value.value;
        }

        public bool Equals(VariableVector2 other)
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
            return Equals((VariableVector2) obj);
        }

        public override int GetHashCode()
        {
            return value.GetHashCode();
        }

        public static bool operator ==(VariableVector2 a, VariableVector2 b)
        {
            if (ReferenceEquals(a, null) == false) return a.Equals(b);
            if (ReferenceEquals(b, null)) return true;
            return false;
        }

        public static bool operator !=(VariableVector2 a, VariableVector2 b)
        {
            return ! (a == b);
        }

        public static VariableVector2 operator +(VariableVector2 a, VariableVector2 b)
        {
            return a.value += b.value;
        }

        public static VariableVector2 operator -(VariableVector2 a, VariableVector2 b)
        {
            return a.value -= b.value;
        }

        public static VariableVector2 operator *(VariableVector2 a, VariableVector2 b)
        {
            return a.value *= b.value;
        }

        public static VariableVector2 operator /(VariableVector2 a, VariableVector2 b)
        {
            return a.value /= b.value;
        }
    }
}