using System;
using Emilia.Reference;
using Sirenix.OdinInspector;

namespace Emilia.Variables
{
    [Serializable, LabelText("Int16(整数)")]
    public class VariableInt16 : Variable<short>
    {
        public static implicit operator VariableInt16(short value)
        {
            VariableInt16 varValue = ReferencePool.Acquire<VariableInt16>();
            varValue.value = value;
            return varValue;
        }

        public static implicit operator short(VariableInt16 value)
        {
            return value.value;
        }

        public static explicit operator VariableInt16(VariableString value)
        {
            VariableInt16 varValue = ReferencePool.Acquire<VariableInt16>();
            varValue.value = short.Parse(value.value);
            return varValue;
        }

        public static explicit operator VariableInt16(VariableSByte value)
        {
            VariableInt16 varValue = ReferencePool.Acquire<VariableInt16>();
            varValue.value = value.value;
            return varValue;
        }

        public static explicit operator VariableInt16(VariableUInt16 value)
        {
            VariableInt16 varValue = ReferencePool.Acquire<VariableInt16>();
            varValue.value = (short) value.value;
            return varValue;
        }

        public static explicit operator VariableInt16(VariableUInt32 value)
        {
            VariableInt16 varValue = ReferencePool.Acquire<VariableInt16>();
            varValue.value = (short) value.value;
            return varValue;
        }

        public static explicit operator VariableInt16(VariableUInt64 value)
        {
            VariableInt16 varValue = ReferencePool.Acquire<VariableInt16>();
            varValue.value = (short) value.value;
            return varValue;
        }

        public static explicit operator VariableInt16(VariableByte value)
        {
            VariableInt16 varValue = ReferencePool.Acquire<VariableInt16>();
            varValue.value = value.value;
            return varValue;
        }

        public static explicit operator VariableInt16(VariableInt32 value)
        {
            VariableInt16 varValue = ReferencePool.Acquire<VariableInt16>();
            varValue.value = (short) value.value;
            return varValue;
        }

        public static explicit operator VariableInt16(VariableInt64 value)
        {
            VariableInt16 varValue = ReferencePool.Acquire<VariableInt16>();
            varValue.value = (short) value.value;
            return varValue;
        }

        public static explicit operator VariableInt16(VariableSingle value)
        {
            VariableInt16 varValue = ReferencePool.Acquire<VariableInt16>();
            varValue.value = (short) value.value;
            return varValue;
        }

        public static explicit operator VariableInt16(VariableDouble value)
        {
            VariableInt16 varValue = ReferencePool.Acquire<VariableInt16>();
            varValue.value = (short) value.value;
            return varValue;
        }

        public static explicit operator VariableInt16(VariableDecimal value)
        {
            VariableInt16 varValue = ReferencePool.Acquire<VariableInt16>();
            varValue.value = (short) value.value;
            return varValue;
        }

        public bool Equals(VariableInt16 other)
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
            return Equals((VariableInt16) obj);
        }

        public override int GetHashCode()
        {
            return value.GetHashCode();
        }

        public static bool operator ==(VariableInt16 a, VariableInt16 b)
        {
            if (ReferenceEquals(a, null) == false) return a.Equals(b);
            if (ReferenceEquals(b, null)) return true;
            return false;
        }

        public static bool operator !=(VariableInt16 a, VariableInt16 b)
        {
            return ! (a == b);
        }

        public static bool operator >(VariableInt16 a, VariableInt16 b)
        {
            return a.value > b.value;
        }

        public static bool operator <(VariableInt16 a, VariableInt16 b)
        {
            return a.value < b.value;
        }

        public static bool operator >=(VariableInt16 a, VariableInt16 b)
        {
            return a.value >= b.value;
        }

        public static bool operator <=(VariableInt16 a, VariableInt16 b)
        {
            return a.value <= b.value;
        }

        public static VariableInt16 operator +(VariableInt16 a, VariableInt16 b)
        {
            return a.value += b.value;
        }

        public static VariableInt16 operator -(VariableInt16 a, VariableInt16 b)
        {
            return a.value -= b.value;
        }

        public static VariableInt16 operator *(VariableInt16 a, VariableInt16 b)
        {
            return a.value *= b.value;
        }

        public static VariableInt16 operator /(VariableInt16 a, VariableInt16 b)
        {
            return a.value /= b.value;
        }
    }
}