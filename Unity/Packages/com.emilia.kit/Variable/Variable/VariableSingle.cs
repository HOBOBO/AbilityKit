using System;
using Emilia.Reference;
using Sirenix.OdinInspector;

namespace Emilia.Variables
{
    [Serializable, LabelText("float(小数)")]
    public class VariableSingle : Variable<float>
    {
        public static implicit operator VariableSingle(float value)
        {
            VariableSingle varValue = ReferencePool.Acquire<VariableSingle>();
            varValue.value = value;
            return varValue;
        }

        public static implicit operator float(VariableSingle value)
        {
            return value.value;
        }

        public static explicit operator VariableSingle(VariableString value)
        {
            VariableSingle varValue = ReferencePool.Acquire<VariableSingle>();
            varValue.value = float.Parse(value.value);
            return varValue;
        }

        public static explicit operator VariableSingle(VariableSByte value)
        {
            VariableSingle varValue = ReferencePool.Acquire<VariableSingle>();
            varValue.value = value.value;
            return varValue;
        }

        public static explicit operator VariableSingle(VariableUInt16 value)
        {
            VariableSingle varValue = ReferencePool.Acquire<VariableSingle>();
            varValue.value = value.value;
            return varValue;
        }

        public static explicit operator VariableSingle(VariableUInt32 value)
        {
            VariableSingle varValue = ReferencePool.Acquire<VariableSingle>();
            varValue.value = value.value;
            return varValue;
        }

        public static explicit operator VariableSingle(VariableUInt64 value)
        {
            VariableSingle varValue = ReferencePool.Acquire<VariableSingle>();
            varValue.value = value.value;
            return varValue;
        }

        public static explicit operator VariableSingle(VariableByte value)
        {
            VariableSingle varValue = ReferencePool.Acquire<VariableSingle>();
            varValue.value = value.value;
            return varValue;
        }

        public static explicit operator VariableSingle(VariableInt16 value)
        {
            VariableSingle varValue = ReferencePool.Acquire<VariableSingle>();
            varValue.value = value.value;
            return varValue;
        }

        public static explicit operator VariableSingle(VariableInt32 value)
        {
            VariableSingle varValue = ReferencePool.Acquire<VariableSingle>();
            varValue.value = value.value;
            return varValue;
        }

        public static explicit operator VariableSingle(VariableInt64 value)
        {
            VariableSingle varValue = ReferencePool.Acquire<VariableSingle>();
            varValue.value = value.value;
            return varValue;
        }

        public static explicit operator VariableSingle(VariableDouble value)
        {
            VariableSingle varValue = ReferencePool.Acquire<VariableSingle>();
            varValue.value = (float) value.value;
            return varValue;
        }

        public static explicit operator VariableSingle(VariableDecimal value)
        {
            VariableSingle varValue = ReferencePool.Acquire<VariableSingle>();
            varValue.value = (float) value.value;
            return varValue;
        }

        public bool Equals(VariableSingle other)
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
            return Equals((VariableSingle) obj);
        }

        public override int GetHashCode()
        {
            return value.GetHashCode();
        }

        public static bool operator ==(VariableSingle a, VariableSingle b)
        {
            if (ReferenceEquals(a, null) == false) return a.Equals(b);
            if (ReferenceEquals(b, null)) return true;
            return false;
        }

        public static bool operator !=(VariableSingle a, VariableSingle b)
        {
            return ! (a == b);
        }

        public static bool operator >(VariableSingle a, VariableSingle b)
        {
            return a.value > b.value;
        }

        public static bool operator <(VariableSingle a, VariableSingle b)
        {
            return a.value < b.value;
        }

        public static bool operator >=(VariableSingle a, VariableSingle b)
        {
            return a.value >= b.value;
        }

        public static bool operator <=(VariableSingle a, VariableSingle b)
        {
            return a.value <= b.value;
        }

        public static VariableSingle operator +(VariableSingle a, VariableSingle b)
        {
            return a.value += b.value;
        }

        public static VariableSingle operator -(VariableSingle a, VariableSingle b)
        {
            return a.value -= b.value;
        }

        public static VariableSingle operator *(VariableSingle a, VariableSingle b)
        {
            return a.value *= b.value;
        }

        public static VariableSingle operator /(VariableSingle a, VariableSingle b)
        {
            return a.value /= b.value;
        }
    }
}