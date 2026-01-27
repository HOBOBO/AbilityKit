using System;
using Emilia.Reference;
using Sirenix.OdinInspector;

namespace Emilia.Variables
{
    [Serializable, LabelText("Double(小数)")]
    public class VariableDouble : Variable<double>
    {
        public static implicit operator VariableDouble(double value)
        {
            VariableDouble varValue = ReferencePool.Acquire<VariableDouble>();
            varValue.value = value;
            return varValue;
        }

        public static implicit operator double(VariableDouble value)
        {
            return value.value;
        }

        public static explicit operator VariableDouble(VariableString value)
        {
            VariableDouble varValue = ReferencePool.Acquire<VariableDouble>();
            varValue.value = double.Parse(value.value);
            return varValue;
        }

        public static explicit operator VariableDouble(VariableSByte value)
        {
            VariableDouble varValue = ReferencePool.Acquire<VariableDouble>();
            varValue.value = value.value;
            return varValue;
        }

        public static explicit operator VariableDouble(VariableUInt16 value)
        {
            VariableDouble varValue = ReferencePool.Acquire<VariableDouble>();
            varValue.value = value.value;
            return varValue;
        }

        public static explicit operator VariableDouble(VariableUInt32 value)
        {
            VariableDouble varValue = ReferencePool.Acquire<VariableDouble>();
            varValue.value = value.value;
            return varValue;
        }

        public static explicit operator VariableDouble(VariableUInt64 value)
        {
            VariableDouble varValue = ReferencePool.Acquire<VariableDouble>();
            varValue.value = value.value;
            return varValue;
        }

        public static explicit operator VariableDouble(VariableByte value)
        {
            VariableDouble varValue = ReferencePool.Acquire<VariableDouble>();
            varValue.value = value.value;
            return varValue;
        }

        public static explicit operator VariableDouble(VariableInt16 value)
        {
            VariableDouble varValue = ReferencePool.Acquire<VariableDouble>();
            varValue.value = value.value;
            return varValue;
        }

        public static explicit operator VariableDouble(VariableInt32 value)
        {
            VariableDouble varValue = ReferencePool.Acquire<VariableDouble>();
            varValue.value = value.value;
            return varValue;
        }

        public static explicit operator VariableDouble(VariableInt64 value)
        {
            VariableDouble varValue = ReferencePool.Acquire<VariableDouble>();
            varValue.value = value.value;
            return varValue;
        }

        public static explicit operator VariableDouble(VariableSingle value)
        {
            VariableDouble varValue = ReferencePool.Acquire<VariableDouble>();
            varValue.value = value.value;
            return varValue;
        }

        public static explicit operator VariableDouble(VariableDecimal value)
        {
            VariableDouble varValue = ReferencePool.Acquire<VariableDouble>();
            varValue.value = (double) value.value;
            return varValue;
        }

        public bool Equals(VariableDouble other)
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
            return Equals((VariableDouble) obj);
        }

        public override int GetHashCode()
        {
            return value.GetHashCode();
        }

        public static bool operator ==(VariableDouble a, VariableDouble b)
        {
            if (ReferenceEquals(a, null) == false) return a.Equals(b);
            if (ReferenceEquals(b, null)) return true;
            return false;
        }

        public static bool operator !=(VariableDouble a, VariableDouble b)
        {
            return ! (a == b);
        }

        public static bool operator >(VariableDouble a, VariableDouble b)
        {
            return a.value > b.value;
        }

        public static bool operator <(VariableDouble a, VariableDouble b)
        {
            return a.value < b.value;
        }

        public static bool operator >=(VariableDouble a, VariableDouble b)
        {
            return a.value >= b.value;
        }

        public static bool operator <=(VariableDouble a, VariableDouble b)
        {
            return a.value <= b.value;
        }

        public static VariableDouble operator +(VariableDouble a, VariableDouble b)
        {
            return a.value += b.value;
        }

        public static VariableDouble operator -(VariableDouble a, VariableDouble b)
        {
            return a.value -= b.value;
        }

        public static VariableDouble operator *(VariableDouble a, VariableDouble b)
        {
            return a.value *= b.value;
        }

        public static VariableDouble operator /(VariableDouble a, VariableDouble b)
        {
            return a.value /= b.value;
        }
    }
}