using System;
using Emilia.Reference;
using Sirenix.OdinInspector;

namespace Emilia.Variables
{
    [Serializable, LabelText("UInt16(正整数)")]
    public class VariableUInt16 : Variable<ushort>
    {
        public static implicit operator VariableUInt16(ushort value)
        {
            VariableUInt16 varValue = ReferencePool.Acquire<VariableUInt16>();
            varValue.value = value;
            return varValue;
        }

        public static implicit operator ushort(VariableUInt16 value)
        {
            return value.value;
        }

        public static explicit operator VariableUInt16(VariableString value)
        {
            VariableUInt16 varValue = ReferencePool.Acquire<VariableUInt16>();
            varValue.value = ushort.Parse(value.value);
            return varValue;
        }

        public static explicit operator VariableUInt16(VariableSByte value)
        {
            VariableUInt16 varValue = ReferencePool.Acquire<VariableUInt16>();
            varValue.value = (ushort) value.value;
            return varValue;
        }

        public static explicit operator VariableUInt16(VariableUInt32 value)
        {
            VariableUInt16 varValue = ReferencePool.Acquire<VariableUInt16>();
            varValue.value = (ushort) value.value;
            return varValue;
        }

        public static explicit operator VariableUInt16(VariableUInt64 value)
        {
            VariableUInt16 varValue = ReferencePool.Acquire<VariableUInt16>();
            varValue.value = (ushort) value.value;
            return varValue;
        }

        public static explicit operator VariableUInt16(VariableByte value)
        {
            VariableUInt16 varValue = ReferencePool.Acquire<VariableUInt16>();
            varValue.value = value.value;
            return varValue;
        }

        public static explicit operator VariableUInt16(VariableInt16 value)
        {
            VariableUInt16 varValue = ReferencePool.Acquire<VariableUInt16>();
            varValue.value = (ushort) value.value;
            return varValue;
        }

        public static explicit operator VariableUInt16(VariableInt32 value)
        {
            VariableUInt16 varValue = ReferencePool.Acquire<VariableUInt16>();
            varValue.value = (ushort) value.value;
            return varValue;
        }

        public static explicit operator VariableUInt16(VariableInt64 value)
        {
            VariableUInt16 varValue = ReferencePool.Acquire<VariableUInt16>();
            varValue.value = (ushort) value.value;
            return varValue;
        }

        public static explicit operator VariableUInt16(VariableSingle value)
        {
            VariableUInt16 varValue = ReferencePool.Acquire<VariableUInt16>();
            varValue.value = (ushort) value.value;
            return varValue;
        }

        public static explicit operator VariableUInt16(VariableDouble value)
        {
            VariableUInt16 varValue = ReferencePool.Acquire<VariableUInt16>();
            varValue.value = (ushort) value.value;
            return varValue;
        }

        public static explicit operator VariableUInt16(VariableDecimal value)
        {
            VariableUInt16 varValue = ReferencePool.Acquire<VariableUInt16>();
            varValue.value = (ushort) value.value;
            return varValue;
        }

        public bool Equals(VariableUInt16 other)
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
            return Equals((VariableUInt16) obj);
        }

        public override int GetHashCode()
        {
            return value.GetHashCode();
        }

        public static bool operator ==(VariableUInt16 a, VariableUInt16 b)
        {
            if (ReferenceEquals(a, null) == false) return a.Equals(b);
            if (ReferenceEquals(b, null)) return true;
            return false;
        }

        public static bool operator !=(VariableUInt16 a, VariableUInt16 b)
        {
            return ! (a == b);
        }

        public static bool operator >(VariableUInt16 a, VariableUInt16 b)
        {
            return a.value > b.value;
        }

        public static bool operator <(VariableUInt16 a, VariableUInt16 b)
        {
            return a.value < b.value;
        }

        public static bool operator >=(VariableUInt16 a, VariableUInt16 b)
        {
            return a.value >= b.value;
        }

        public static bool operator <=(VariableUInt16 a, VariableUInt16 b)
        {
            return a.value <= b.value;
        }

        public static VariableUInt16 operator +(VariableUInt16 a, VariableUInt16 b)
        {
            return a.value += b.value;
        }

        public static VariableUInt16 operator -(VariableUInt16 a, VariableUInt16 b)
        {
            return a.value -= b.value;
        }

        public static VariableUInt16 operator *(VariableUInt16 a, VariableUInt16 b)
        {
            return a.value *= b.value;
        }

        public static VariableUInt16 operator /(VariableUInt16 a, VariableUInt16 b)
        {
            return a.value /= b.value;
        }
    }
}