using System;
using Emilia.Reference;
using Sirenix.OdinInspector;

namespace Emilia.Variables
{
    [Serializable, LabelText("Int32(整数)")]
    public class VariableInt32 : Variable<int>
    {
        public static implicit operator VariableInt32(int value)
        {
            VariableInt32 varValue = ReferencePool.Acquire<VariableInt32>();
            varValue.value = value;
            return varValue;
        }

        public static implicit operator int(VariableInt32 value)
        {
            return value.value;
        }

        public static explicit operator VariableInt32(VariableString value)
        {
            VariableInt32 varValue = ReferencePool.Acquire<VariableInt32>();
            varValue.value = int.Parse(value.value);
            return varValue;
        }

        public static explicit operator VariableInt32(VariableSByte value)
        {
            VariableInt32 varValue = ReferencePool.Acquire<VariableInt32>();
            varValue.value = value.value;
            return varValue;
        }

        public static explicit operator VariableInt32(VariableUInt16 value)
        {
            VariableInt32 varValue = ReferencePool.Acquire<VariableInt32>();
            varValue.value = value.value;
            return varValue;
        }

        public static explicit operator VariableInt32(VariableUInt32 value)
        {
            VariableInt32 varValue = ReferencePool.Acquire<VariableInt32>();
            varValue.value = (int) value.value;
            return varValue;
        }

        public static explicit operator VariableInt32(VariableUInt64 value)
        {
            VariableInt32 varValue = ReferencePool.Acquire<VariableInt32>();
            varValue.value = (int) value.value;
            return varValue;
        }

        public static explicit operator VariableInt32(VariableByte value)
        {
            VariableInt32 varValue = ReferencePool.Acquire<VariableInt32>();
            varValue.value = value.value;
            return varValue;
        }

        public static explicit operator VariableInt32(VariableInt16 value)
        {
            VariableInt32 varValue = ReferencePool.Acquire<VariableInt32>();
            varValue.value = value.value;
            return varValue;
        }

        public static explicit operator VariableInt32(VariableInt64 value)
        {
            VariableInt32 varValue = ReferencePool.Acquire<VariableInt32>();
            varValue.value = (int) value.value;
            return varValue;
        }

        public static explicit operator VariableInt32(VariableSingle value)
        {
            VariableInt32 varValue = ReferencePool.Acquire<VariableInt32>();
            varValue.value = (int) value.value;
            return varValue;
        }

        public static explicit operator VariableInt32(VariableDouble value)
        {
            VariableInt32 varValue = ReferencePool.Acquire<VariableInt32>();
            varValue.value = (int) value.value;
            return varValue;
        }

        public static explicit operator VariableInt32(VariableDecimal value)
        {
            VariableInt32 varValue = ReferencePool.Acquire<VariableInt32>();
            varValue.value = (int) value.value;
            return varValue;
        }

        public bool Equals(VariableInt32 other)
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
            return Equals((VariableInt32) obj);
        }

        public override int GetHashCode()
        {
            return value.GetHashCode();
        }

        public static bool operator ==(VariableInt32 a, VariableInt32 b)
        {
            if (ReferenceEquals(a, null) == false) return a.Equals(b);
            if (ReferenceEquals(b, null)) return true;
            return false;
        }

        public static bool operator !=(VariableInt32 a, VariableInt32 b)
        {
            return ! (a == b);
        }

        public static bool operator >(VariableInt32 a, VariableInt32 b)
        {
            return a.value > b.value;
        }

        public static bool operator <(VariableInt32 a, VariableInt32 b)
        {
            return a.value < b.value;
        }

        public static bool operator >=(VariableInt32 a, VariableInt32 b)
        {
            return a.value >= b.value;
        }

        public static bool operator <=(VariableInt32 a, VariableInt32 b)
        {
            return a.value <= b.value;
        }

        public static VariableInt32 operator +(VariableInt32 a, VariableInt32 b)
        {
            return a.value += b.value;
        }

        public static VariableInt32 operator -(VariableInt32 a, VariableInt32 b)
        {
            return a.value -= b.value;
        }

        public static VariableInt32 operator *(VariableInt32 a, VariableInt32 b)
        {
            return a.value *= b.value;
        }

        public static VariableInt32 operator /(VariableInt32 a, VariableInt32 b)
        {
            return a.value /= b.value;
        }
    }
}