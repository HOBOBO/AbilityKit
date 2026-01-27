using System;
using Emilia.Reference;
using Sirenix.OdinInspector;

namespace Emilia.Variables
{
    [Serializable, LabelText("Decimal(小数)")]
    public class VariableDecimal : Variable<decimal>
    {
        public static implicit operator VariableDecimal(decimal value)
        {
            VariableDecimal varValue = ReferencePool.Acquire<VariableDecimal>();
            varValue.value = value;
            return varValue;
        }

        public static implicit operator decimal(VariableDecimal value)
        {
            return value.value;
        }

        public static explicit operator VariableDecimal(VariableString value)
        {
            VariableDecimal varValue = ReferencePool.Acquire<VariableDecimal>();
            varValue.value = decimal.Parse(value.value);
            return varValue;
        }

        public static explicit operator VariableDecimal(VariableSByte value)
        {
            VariableDecimal varValue = ReferencePool.Acquire<VariableDecimal>();
            varValue.value = value.value;
            return varValue;
        }

        public static explicit operator VariableDecimal(VariableUInt16 value)
        {
            VariableDecimal varValue = ReferencePool.Acquire<VariableDecimal>();
            varValue.value = value.value;
            return varValue;
        }

        public static explicit operator VariableDecimal(VariableUInt32 value)
        {
            VariableDecimal varValue = ReferencePool.Acquire<VariableDecimal>();
            varValue.value = value.value;
            return varValue;
        }

        public static explicit operator VariableDecimal(VariableUInt64 value)
        {
            VariableDecimal varValue = ReferencePool.Acquire<VariableDecimal>();
            varValue.value = value.value;
            return varValue;
        }

        public static explicit operator VariableDecimal(VariableByte value)
        {
            VariableDecimal varValue = ReferencePool.Acquire<VariableDecimal>();
            varValue.value = value.value;
            return varValue;
        }

        public static explicit operator VariableDecimal(VariableInt16 value)
        {
            VariableDecimal varValue = ReferencePool.Acquire<VariableDecimal>();
            varValue.value = value.value;
            return varValue;
        }

        public static explicit operator VariableDecimal(VariableInt32 value)
        {
            VariableDecimal varValue = ReferencePool.Acquire<VariableDecimal>();
            varValue.value = value.value;
            return varValue;
        }

        public static explicit operator VariableDecimal(VariableInt64 value)
        {
            VariableDecimal varValue = ReferencePool.Acquire<VariableDecimal>();
            varValue.value = value.value;
            return varValue;
        }

        public static explicit operator VariableDecimal(VariableSingle value)
        {
            VariableDecimal varValue = ReferencePool.Acquire<VariableDecimal>();
            varValue.value = (decimal) value.value;
            return varValue;
        }

        public static explicit operator VariableDecimal(VariableDouble value)
        {
            VariableDecimal varValue = ReferencePool.Acquire<VariableDecimal>();
            varValue.value = (decimal) value.value;
            return varValue;
        }

        public bool Equals(VariableDecimal other)
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
            return Equals((VariableDecimal) obj);
        }

        public override int GetHashCode()
        {
            return value.GetHashCode();
        }

        public static bool operator ==(VariableDecimal a, VariableDecimal b)
        {
            if (ReferenceEquals(a, null) == false) return a.Equals(b);
            if (ReferenceEquals(b, null)) return true;
            return false;
        }

        public static bool operator !=(VariableDecimal a, VariableDecimal b)
        {
            return ! (a == b);
        }

        public static bool operator >(VariableDecimal a, VariableDecimal b)
        {
            return a.value > b.value;
        }

        public static bool operator <(VariableDecimal a, VariableDecimal b)
        {
            return a.value < b.value;
        }

        public static bool operator >=(VariableDecimal a, VariableDecimal b)
        {
            return a.value >= b.value;
        }

        public static bool operator <=(VariableDecimal a, VariableDecimal b)
        {
            return a.value <= b.value;
        }

        public static VariableDecimal operator +(VariableDecimal a, VariableDecimal b)
        {
            return a.value += b.value;
        }

        public static VariableDecimal operator -(VariableDecimal a, VariableDecimal b)
        {
            return a.value -= b.value;
        }

        public static VariableDecimal operator *(VariableDecimal a, VariableDecimal b)
        {
            return a.value *= b.value;
        }

        public static VariableDecimal operator /(VariableDecimal a, VariableDecimal b)
        {
            return a.value /= b.value;
        }
    }
}