using System;
using Emilia.Reference;
using Sirenix.OdinInspector;

namespace Emilia.Variables
{
    [Serializable, LabelText("Byte(字节)")]
    public class VariableByte : Variable<byte>
    {
        public static implicit operator VariableByte(byte value)
        {
            VariableByte varValue = ReferencePool.Acquire<VariableByte>();
            varValue.value = value;
            return varValue;
        }

        public static implicit operator byte(VariableByte value)
        {
            return value.value;
        }

        public static explicit operator VariableByte(VariableString value)
        {
            VariableByte varValue = ReferencePool.Acquire<VariableByte>();
            varValue.value = byte.Parse(value.value);
            return varValue;
        }

        public static explicit operator VariableByte(VariableUInt16 value)
        {
            VariableByte varValue = ReferencePool.Acquire<VariableByte>();
            varValue.value = (byte) value.value;
            return varValue;
        }

        public static explicit operator VariableByte(VariableUInt32 value)
        {
            VariableByte varValue = ReferencePool.Acquire<VariableByte>();
            varValue.value = (byte) value.value;
            return varValue;
        }

        public static explicit operator VariableByte(VariableUInt64 value)
        {
            VariableByte varValue = ReferencePool.Acquire<VariableByte>();
            varValue.value = (byte) value.value;
            return varValue;
        }

        public static explicit operator VariableByte(VariableSByte value)
        {
            VariableByte varValue = ReferencePool.Acquire<VariableByte>();
            varValue.value = (byte) value.value;
            return varValue;
        }

        public static explicit operator VariableByte(VariableInt16 value)
        {
            VariableByte varValue = ReferencePool.Acquire<VariableByte>();
            varValue.value = (byte) value.value;
            return varValue;
        }

        public static explicit operator VariableByte(VariableInt32 value)
        {
            VariableByte varValue = ReferencePool.Acquire<VariableByte>();
            varValue.value = (byte) value.value;
            return varValue;
        }

        public static explicit operator VariableByte(VariableInt64 value)
        {
            VariableByte varValue = ReferencePool.Acquire<VariableByte>();
            varValue.value = (byte) value.value;
            return varValue;
        }

        public static explicit operator VariableByte(VariableSingle value)
        {
            VariableByte varValue = ReferencePool.Acquire<VariableByte>();
            varValue.value = (byte) value.value;
            return varValue;
        }

        public static explicit operator VariableByte(VariableDouble value)
        {
            VariableByte varValue = ReferencePool.Acquire<VariableByte>();
            varValue.value = (byte) value.value;
            return varValue;
        }

        public static explicit operator VariableByte(VariableDecimal value)
        {
            VariableByte varValue = ReferencePool.Acquire<VariableByte>();
            varValue.value = (byte) value.value;
            return varValue;
        }

        public bool Equals(VariableByte other)
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
            return Equals((VariableByte) obj);
        }

        public override int GetHashCode()
        {
            return value.GetHashCode();
        }

        public static bool operator ==(VariableByte a, VariableByte b)
        {
            if (ReferenceEquals(a, null) == false) return a.Equals(b);
            if (ReferenceEquals(b, null)) return true;
            return false;
        }

        public static bool operator !=(VariableByte a, VariableByte b)
        {
            return ! (a == b);
        }

        public static bool operator >(VariableByte a, VariableByte b)
        {
            return a.value > b.value;
        }

        public static bool operator <(VariableByte a, VariableByte b)
        {
            return a.value < b.value;
        }

        public static bool operator >=(VariableByte a, VariableByte b)
        {
            return a.value >= b.value;
        }

        public static bool operator <=(VariableByte a, VariableByte b)
        {
            return a.value <= b.value;
        }

        public static VariableByte operator +(VariableByte a, VariableByte b)
        {
            return a.value += b.value;
        }

        public static VariableByte operator -(VariableByte a, VariableByte b)
        {
            return a.value -= b.value;
        }

        public static VariableByte operator *(VariableByte a, VariableByte b)
        {
            return a.value *= b.value;
        }

        public static VariableByte operator /(VariableByte a, VariableByte b)
        {
            return a.value /= b.value;
        }
    }
}