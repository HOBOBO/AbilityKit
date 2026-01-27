using System;
using System.Globalization;
using Emilia.Reference;
using Sirenix.OdinInspector;

namespace Emilia.Variables
{
    [Serializable, LabelText("String(字符串)")]
    public class VariableString : Variable<string>
    {
        public static implicit operator VariableString(string value)
        {
            VariableString varValue = ReferencePool.Acquire<VariableString>();
            varValue.value = value;
            return varValue;
        }

        public static implicit operator string(VariableString value)
        {
            return value.value;
        }

        public static explicit operator VariableString(VariableChar value)
        {
            VariableString varValue = ReferencePool.Acquire<VariableString>();
            varValue.value = value.value.ToString(CultureInfo.InvariantCulture);
            return varValue;
        }

        public static explicit operator VariableString(VariableSByte value)
        {
            VariableString varValue = ReferencePool.Acquire<VariableString>();
            varValue.value = value.value.ToString(CultureInfo.InvariantCulture);
            return varValue;
        }

        public static explicit operator VariableString(VariableUInt16 value)
        {
            VariableString varValue = ReferencePool.Acquire<VariableString>();
            varValue.value = value.value.ToString(CultureInfo.InvariantCulture);
            return varValue;
        }

        public static explicit operator VariableString(VariableUInt32 value)
        {
            VariableString varValue = ReferencePool.Acquire<VariableString>();
            varValue.value = value.value.ToString(CultureInfo.InvariantCulture);
            return varValue;
        }

        public static explicit operator VariableString(VariableUInt64 value)
        {
            VariableString varValue = ReferencePool.Acquire<VariableString>();
            varValue.value = value.value.ToString(CultureInfo.InvariantCulture);
            return varValue;
        }

        public static explicit operator VariableString(VariableByte value)
        {
            VariableString varValue = ReferencePool.Acquire<VariableString>();
            varValue.value = value.value.ToString(CultureInfo.InvariantCulture);
            return varValue;
        }

        public static explicit operator VariableString(VariableInt32 value)
        {
            VariableString varValue = ReferencePool.Acquire<VariableString>();
            varValue.value = value.value.ToString(CultureInfo.InvariantCulture);
            return varValue;
        }

        public static explicit operator VariableString(VariableInt64 value)
        {
            VariableString varValue = ReferencePool.Acquire<VariableString>();
            varValue.value = value.value.ToString(CultureInfo.InvariantCulture);
            return varValue;
        }

        public static explicit operator VariableString(VariableSingle value)
        {
            VariableString varValue = ReferencePool.Acquire<VariableString>();
            varValue.value = value.value.ToString(CultureInfo.InvariantCulture);
            return varValue;
        }

        public static explicit operator VariableString(VariableDouble value)
        {
            VariableString varValue = ReferencePool.Acquire<VariableString>();
            varValue.value = value.value.ToString(CultureInfo.InvariantCulture);
            return varValue;
        }

        public static explicit operator VariableString(VariableDecimal value)
        {
            VariableString varValue = ReferencePool.Acquire<VariableString>();
            varValue.value = value.value.ToString(CultureInfo.InvariantCulture);
            return varValue;
        }

        public bool Equals(VariableString other)
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
            return Equals((VariableString) obj);
        }

        public override int GetHashCode()
        {
            return value.GetHashCode();
        }

        public static bool operator ==(VariableString a, VariableString b)
        {
            if (ReferenceEquals(a, null) == false) return a.Equals(b);
            if (ReferenceEquals(b, null)) return true;
            return false;
        }

        public static bool operator !=(VariableString a, VariableString b)
        {
            return ! (a == b);
        }
    }
}