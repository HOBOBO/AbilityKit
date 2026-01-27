using System;
using Emilia.Reference;
using Sirenix.OdinInspector;

namespace Emilia.Variables
{
    [Serializable, LabelText("Int64(整数)")]
    public class VariableInt64 : Variable<long>
    {
        public static implicit operator VariableInt64(long value)
        {
            VariableInt64 varValue = ReferencePool.Acquire<VariableInt64>();
            varValue.value = value;
            return varValue;
        }

        public static implicit operator long(VariableInt64 value)
        {
            return value.value;
        }

        public static explicit operator VariableInt64(VariableString value)
        {
            VariableInt64 varValue = ReferencePool.Acquire<VariableInt64>();
            varValue.value = long.Parse(value.value);
            return varValue;
        }

        public static explicit operator VariableInt64(VariableByte value)
        {
            VariableInt64 varValue = ReferencePool.Acquire<VariableInt64>();
            varValue.value = value.value;
            return varValue;
        }

        public static explicit operator VariableInt64(VariableInt16 value)
        {
            VariableInt64 varValue = ReferencePool.Acquire<VariableInt64>();
            varValue.value = value.value;
            return varValue;
        }

        public static explicit operator VariableInt64(VariableInt32 value)
        {
            VariableInt64 varValue = ReferencePool.Acquire<VariableInt64>();
            varValue.value = value.value;
            return varValue;
        }

        public static explicit operator VariableInt64(VariableSingle value)
        {
            VariableInt64 varValue = ReferencePool.Acquire<VariableInt64>();
            varValue.value = (long) value.value;
            return varValue;
        }

        public static explicit operator VariableInt64(VariableDouble value)
        {
            VariableInt64 varValue = ReferencePool.Acquire<VariableInt64>();
            varValue.value = (long) value.value;
            return varValue;
        }

        public static explicit operator VariableInt64(VariableDecimal value)
        {
            VariableInt64 varValue = ReferencePool.Acquire<VariableInt64>();
            varValue.value = (long) value.value;
            return varValue;
        }

        public bool Equals(VariableInt64 other)
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
            return Equals((VariableInt64) obj);
        }

        public override int GetHashCode()
        {
            return value.GetHashCode();
        }

        public static bool operator ==(VariableInt64 a, VariableInt64 b)
        {
            if (ReferenceEquals(a, null) == false) return a.Equals(b);
            if (ReferenceEquals(b, null)) return true;
            return false;
        }

        public static bool operator !=(VariableInt64 a, VariableInt64 b)
        {
            return ! (a == b);
        }

        public static bool operator >(VariableInt64 a, VariableInt64 b)
        {
            return a.value > b.value;
        }

        public static bool operator <(VariableInt64 a, VariableInt64 b)
        {
            return a.value < b.value;
        }

        public static bool operator >=(VariableInt64 a, VariableInt64 b)
        {
            return a.value >= b.value;
        }

        public static bool operator <=(VariableInt64 a, VariableInt64 b)
        {
            return a.value <= b.value;
        }

        public static VariableInt64 operator +(VariableInt64 a, VariableInt64 b)
        {
            return a.value += b.value;
        }

        public static VariableInt64 operator -(VariableInt64 a, VariableInt64 b)
        {
            return a.value -= b.value;
        }

        public static VariableInt64 operator *(VariableInt64 a, VariableInt64 b)
        {
            return a.value *= b.value;
        }

        public static VariableInt64 operator /(VariableInt64 a, VariableInt64 b)
        {
            return a.value /= b.value;
        }
    }
}