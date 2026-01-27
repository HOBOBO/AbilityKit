using System;
using Emilia.Reference;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Emilia.Variables
{
    [Serializable, LabelText("Vector3(三维向量)")]
    public class VariableVector3 : Variable<Vector3>
    {
        public static implicit operator VariableVector3(Vector3 value)
        {
            VariableVector3 varValue = ReferencePool.Acquire<VariableVector3>();
            varValue.value = value;
            return varValue;
        }

        public static implicit operator Vector3(VariableVector3 value)
        {
            return value.value;
        }

        public static explicit operator VariableVector3(VariableVector2 value)
        {
            VariableVector3 varValue = ReferencePool.Acquire<VariableVector3>();
            varValue.value = value.value;
            return varValue;
        }

        public bool Equals(VariableVector3 other)
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
            return Equals((VariableVector3) obj);
        }

        public override int GetHashCode()
        {
            return value.GetHashCode();
        }

        public static bool operator ==(VariableVector3 a, VariableVector3 b)
        {
            if (ReferenceEquals(a, null) == false) return a.Equals(b);
            if (ReferenceEquals(b, null)) return true;
            return false;
        }

        public static bool operator !=(VariableVector3 a, VariableVector3 b)
        {
            return ! (a == b);
        }

        public static VariableVector3 operator +(VariableVector3 a, VariableVector3 b)
        {
            return a.value += b.value;
        }

        public static VariableVector3 operator -(VariableVector3 a, VariableVector3 b)
        {
            return a.value -= b.value;
        }
    }
}