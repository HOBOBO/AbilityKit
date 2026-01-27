using System;
using Emilia.Reference;
using Sirenix.OdinInspector;

namespace Emilia.Variables
{
    [Serializable, LabelText("sbyte(字节)")]
    public class VariableSByte : Variable<sbyte>
    {
        public static implicit operator VariableSByte(sbyte value)
        {
            VariableSByte varValue = ReferencePool.Acquire<VariableSByte>();
            varValue.value = value;
            return varValue;
        }

        public static implicit operator sbyte(VariableSByte value)
        {
            return value.value;
        }

        public static explicit operator VariableSByte(VariableString value)
        {
            VariableSByte varValue = ReferencePool.Acquire<VariableSByte>();
            varValue.value = sbyte.Parse(value.value);
            return varValue;
        }

        public static explicit operator VariableSByte(VariableUInt16 value)
        {
            VariableSByte varValue = ReferencePool.Acquire<VariableSByte>();
            varValue.value = (sbyte) value.value;
            return varValue;
        }

        public static explicit operator VariableSByte(VariableUInt32 value)
        {
            VariableSByte varValue = ReferencePool.Acquire<VariableSByte>();
            varValue.value = (sbyte) value.value;
            return varValue;
        }

        public static explicit operator VariableSByte(VariableUInt64 value)
        {
            VariableSByte varValue = ReferencePool.Acquire<VariableSByte>();
            varValue.value = (sbyte) value.value;
            return varValue;
        }

        public static explicit operator VariableSByte(VariableByte value)
        {
            VariableSByte varValue = ReferencePool.Acquire<VariableSByte>();
            varValue.value = (sbyte) value.value;
            return varValue;
        }

        public static explicit operator VariableSByte(VariableInt16 value)
        {
            VariableSByte varValue = ReferencePool.Acquire<VariableSByte>();
            varValue.value = (sbyte) value.value;
            return varValue;
        }

        public static explicit operator VariableSByte(VariableInt32 value)
        {
            VariableSByte varValue = ReferencePool.Acquire<VariableSByte>();
            varValue.value = (sbyte) value.value;
            return varValue;
        }

        public static explicit operator VariableSByte(VariableInt64 value)
        {
            VariableSByte varValue = ReferencePool.Acquire<VariableSByte>();
            varValue.value = (sbyte) value.value;
            return varValue;
        }

        public static explicit operator VariableSByte(VariableSingle value)
        {
            VariableSByte varValue = ReferencePool.Acquire<VariableSByte>();
            varValue.value = (sbyte) value.value;
            return varValue;
        }

        public static explicit operator VariableSByte(VariableDouble value)
        {
            VariableSByte varValue = ReferencePool.Acquire<VariableSByte>();
            varValue.value = (sbyte) value.value;
            return varValue;
        }

        public static explicit operator VariableSByte(VariableDecimal value)
        {
            VariableSByte varValue = ReferencePool.Acquire<VariableSByte>();
            varValue.value = (sbyte) value.value;
            return varValue;
        }

        public bool Equals(VariableSByte other)
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
            return Equals((VariableSByte) obj);
        }

        public override int GetHashCode()
        {
            return value.GetHashCode();
        }

        public static bool operator ==(VariableSByte a, VariableSByte b)
        {
            if (ReferenceEquals(a, null) == false) return a.Equals(b);
            if (ReferenceEquals(b, null)) return true;
            return false;
        }

        public static bool operator !=(VariableSByte a, VariableSByte b)
        {
            return ! (a == b);
        }

        public static bool operator >(VariableSByte a, VariableSByte b)
        {
            return a.value > b.value;
        }

        public static bool operator <(VariableSByte a, VariableSByte b)
        {
            return a.value < b.value;
        }

        public static bool operator >=(VariableSByte a, VariableSByte b)
        {
            return a.value >= b.value;
        }

        public static bool operator <=(VariableSByte a, VariableSByte b)
        {
            return a.value <= b.value;
        }

        public static VariableSByte operator +(VariableSByte a, VariableSByte b)
        {
            return a.value += b.value;
        }

        public static VariableSByte operator -(VariableSByte a, VariableSByte b)
        {
            return a.value -= b.value;
        }

        public static VariableSByte operator *(VariableSByte a, VariableSByte b)
        {
            return a.value *= b.value;
        }

        public static VariableSByte operator /(VariableSByte a, VariableSByte b)
        {
            return a.value /= b.value;
        }
    }
}