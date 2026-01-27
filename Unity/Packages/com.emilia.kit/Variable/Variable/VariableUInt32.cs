using System;
using Emilia.Reference;
using Sirenix.OdinInspector;

namespace Emilia.Variables
{
    [Serializable, LabelText("UInt32(正整数)")]
    public class VariableUInt32 : Variable<uint>
    {
        public static implicit operator VariableUInt32(uint value)
        {
            VariableUInt32 varValue = ReferencePool.Acquire<VariableUInt32>();
            varValue.value = value;
            return varValue;
        }

        public static implicit operator uint(VariableUInt32 value)
        {
            return value.value;
        }

        public static explicit operator VariableUInt32(VariableString value)
        {
            VariableUInt32 varValue = ReferencePool.Acquire<VariableUInt32>();
            varValue.value = uint.Parse(value.value);
            return varValue;
        }

        public static explicit operator VariableUInt32(VariableSByte value)
        {
            VariableUInt32 varValue = ReferencePool.Acquire<VariableUInt32>();
            varValue.value = (uint) value.value;
            return varValue;
        }

        public static explicit operator VariableUInt32(VariableUInt16 value)
        {
            VariableUInt32 varValue = ReferencePool.Acquire<VariableUInt32>();
            varValue.value = value.value;
            return varValue;
        }

        public static explicit operator VariableUInt32(VariableUInt64 value)
        {
            VariableUInt32 varValue = ReferencePool.Acquire<VariableUInt32>();
            varValue.value = (uint) value.value;
            return varValue;
        }

        public static explicit operator VariableUInt32(VariableByte value)
        {
            VariableUInt32 varValue = ReferencePool.Acquire<VariableUInt32>();
            varValue.value = value.value;
            return varValue;
        }

        public static explicit operator VariableUInt32(VariableInt16 value)
        {
            VariableUInt32 varValue = ReferencePool.Acquire<VariableUInt32>();
            varValue.value = (uint) value.value;
            return varValue;
        }

        public static explicit operator VariableUInt32(VariableInt32 value)
        {
            VariableUInt32 varValue = ReferencePool.Acquire<VariableUInt32>();
            varValue.value = (uint) value.value;
            return varValue;
        }

        public static explicit operator VariableUInt32(VariableInt64 value)
        {
            VariableUInt32 varValue = ReferencePool.Acquire<VariableUInt32>();
            varValue.value = (uint) value.value;
            return varValue;
        }

        public static explicit operator VariableUInt32(VariableSingle value)
        {
            VariableUInt32 varValue = ReferencePool.Acquire<VariableUInt32>();
            varValue.value = (uint) value.value;
            return varValue;
        }

        public static explicit operator VariableUInt32(VariableDouble value)
        {
            VariableUInt32 varValue = ReferencePool.Acquire<VariableUInt32>();
            varValue.value = (uint) value.value;
            return varValue;
        }

        public static explicit operator VariableUInt32(VariableDecimal value)
        {
            VariableUInt32 varValue = ReferencePool.Acquire<VariableUInt32>();
            varValue.value = (uint) value.value;
            return varValue;
        }

        public bool Equals(VariableUInt32 other)
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
            return Equals((VariableUInt32) obj);
        }

        public override int GetHashCode()
        {
            return value.GetHashCode();
        }

        public static bool operator ==(VariableUInt32 a, VariableUInt32 b)
        {
            if (ReferenceEquals(a, null) == false) return a.Equals(b);
            if (ReferenceEquals(b, null)) return true;
            return false;
        }

        public static bool operator !=(VariableUInt32 a, VariableUInt32 b)
        {
            return ! (a == b);
        }

        public static bool operator >(VariableUInt32 a, VariableUInt32 b)
        {
            return a.value > b.value;
        }

        public static bool operator <(VariableUInt32 a, VariableUInt32 b)
        {
            return a.value < b.value;
        }

        public static bool operator >=(VariableUInt32 a, VariableUInt32 b)
        {
            return a.value >= b.value;
        }

        public static bool operator <=(VariableUInt32 a, VariableUInt32 b)
        {
            return a.value <= b.value;
        }

        public static VariableUInt32 operator +(VariableUInt32 a, VariableUInt32 b)
        {
            return a.value += b.value;
        }

        public static VariableUInt32 operator -(VariableUInt32 a, VariableUInt32 b)
        {
            return a.value -= b.value;
        }

        public static VariableUInt32 operator *(VariableUInt32 a, VariableUInt32 b)
        {
            return a.value *= b.value;
        }

        public static VariableUInt32 operator /(VariableUInt32 a, VariableUInt32 b)
        {
            return a.value /= b.value;
        }
    }
}