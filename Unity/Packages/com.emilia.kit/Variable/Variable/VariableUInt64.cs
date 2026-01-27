using System;
using Emilia.Reference;
using Sirenix.OdinInspector;

namespace Emilia.Variables
{
    [Serializable, LabelText("UInt64(正整数)")]
    public sealed class VariableUInt64 : Variable<ulong>
    {
        public static implicit operator VariableUInt64(ulong value)
        {
            VariableUInt64 varValue = ReferencePool.Acquire<VariableUInt64>();
            varValue.value = value;
            return varValue;
        }

        public static implicit operator ulong(VariableUInt64 value)
        {
            return value.value;
        }

        public static explicit operator VariableUInt64(VariableString value)
        {
            VariableUInt64 varValue = ReferencePool.Acquire<VariableUInt64>();
            varValue.value = ulong.Parse(value.value);
            return varValue;
        }

        public static explicit operator VariableUInt64(VariableSByte value)
        {
            VariableUInt64 varValue = ReferencePool.Acquire<VariableUInt64>();
            varValue.value = (ulong) value.value;
            return varValue;
        }

        public static explicit operator VariableUInt64(VariableUInt16 value)
        {
            VariableUInt64 varValue = ReferencePool.Acquire<VariableUInt64>();
            varValue.value = value.value;
            return varValue;
        }

        public static explicit operator VariableUInt64(VariableUInt32 value)
        {
            VariableUInt64 varValue = ReferencePool.Acquire<VariableUInt64>();
            varValue.value = value.value;
            return varValue;
        }

        public static explicit operator VariableUInt64(VariableByte value)
        {
            VariableUInt64 varValue = ReferencePool.Acquire<VariableUInt64>();
            varValue.value = value.value;
            return varValue;
        }

        public static explicit operator VariableUInt64(VariableInt16 value)
        {
            VariableUInt64 varValue = ReferencePool.Acquire<VariableUInt64>();
            varValue.value = (ulong) value.value;
            return varValue;
        }

        public static explicit operator VariableUInt64(VariableInt32 value)
        {
            VariableUInt64 varValue = ReferencePool.Acquire<VariableUInt64>();
            varValue.value = (ulong) value.value;
            return varValue;
        }

        public static explicit operator VariableUInt64(VariableInt64 value)
        {
            VariableUInt64 varValue = ReferencePool.Acquire<VariableUInt64>();
            varValue.value = (ulong) value.value;
            return varValue;
        }

        public static explicit operator VariableUInt64(VariableSingle value)
        {
            VariableUInt64 varValue = ReferencePool.Acquire<VariableUInt64>();
            varValue.value = (ulong) value.value;
            return varValue;
        }

        public static explicit operator VariableUInt64(VariableDouble value)
        {
            VariableUInt64 varValue = ReferencePool.Acquire<VariableUInt64>();
            varValue.value = (ulong) value.value;
            return varValue;
        }

        public static explicit operator VariableUInt64(VariableDecimal value)
        {
            VariableUInt64 varValue = ReferencePool.Acquire<VariableUInt64>();
            varValue.value = (ulong) value.value;
            return varValue;
        }

        public bool Equals(VariableUInt64 other)
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
            return Equals((VariableUInt64) obj);
        }

        public override int GetHashCode()
        {
            return value.GetHashCode();
        }

        public static bool operator ==(VariableUInt64 a, VariableUInt64 b)
        {
            if (ReferenceEquals(a, null) == false) return a.Equals(b);
            if (ReferenceEquals(b, null)) return true;
            return false;
        }

        public static bool operator !=(VariableUInt64 a, VariableUInt64 b)
        {
            return ! (a == b);
        }

        public static bool operator >(VariableUInt64 a, VariableUInt64 b)
        {
            return a.value > b.value;
        }

        public static bool operator <(VariableUInt64 a, VariableUInt64 b)
        {
            return a.value < b.value;
        }

        public static bool operator >=(VariableUInt64 a, VariableUInt64 b)
        {
            return a.value >= b.value;
        }

        public static bool operator <=(VariableUInt64 a, VariableUInt64 b)
        {
            return a.value <= b.value;
        }

        public static VariableUInt64 operator +(VariableUInt64 a, VariableUInt64 b)
        {
            return a.value += b.value;
        }

        public static VariableUInt64 operator -(VariableUInt64 a, VariableUInt64 b)
        {
            return a.value -= b.value;
        }

        public static VariableUInt64 operator *(VariableUInt64 a, VariableUInt64 b)
        {
            return a.value *= b.value;
        }

        public static VariableUInt64 operator /(VariableUInt64 a, VariableUInt64 b)
        {
            return a.value /= b.value;
        }
    }
}