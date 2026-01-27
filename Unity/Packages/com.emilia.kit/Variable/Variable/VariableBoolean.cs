using System;
using Emilia.Reference;
using Sirenix.OdinInspector;

namespace Emilia.Variables
{
    [Serializable, LabelText("Bool(布尔值)")]
    public class VariableBoolean : Variable<bool>
    {
        public static implicit operator VariableBoolean(bool value)
        {
            VariableBoolean varValue = ReferencePool.Acquire<VariableBoolean>();
            varValue.value = value;
            return varValue;
        }

        public static implicit operator bool(VariableBoolean value)
        {
            return value.value;
        }

        public static explicit operator VariableBoolean(VariableString value)
        {
            VariableBoolean varValue = ReferencePool.Acquire<VariableBoolean>();
            varValue.value = bool.Parse(value.value);
            return varValue;
        }

        public static explicit operator VariableBoolean(VariableSByte value)
        {
            VariableBoolean varValue = ReferencePool.Acquire<VariableBoolean>();
            varValue.value = value.value != 0;
            return varValue;
        }

        public static explicit operator VariableBoolean(VariableUInt16 value)
        {
            VariableBoolean varValue = ReferencePool.Acquire<VariableBoolean>();
            varValue.value = value.value != 0;
            return varValue;
        }

        public static explicit operator VariableBoolean(VariableUInt32 value)
        {
            VariableBoolean varValue = ReferencePool.Acquire<VariableBoolean>();
            varValue.value = value.value != 0;
            return varValue;
        }

        public static explicit operator VariableBoolean(VariableUInt64 value)
        {
            VariableBoolean varValue = ReferencePool.Acquire<VariableBoolean>();
            varValue.value = value.value != 0;
            return varValue;
        }

        public static explicit operator VariableBoolean(VariableByte value)
        {
            VariableBoolean varValue = ReferencePool.Acquire<VariableBoolean>();
            varValue.value = value.value != 0;
            return varValue;
        }

        public static explicit operator VariableBoolean(VariableInt16 value)
        {
            VariableBoolean varValue = ReferencePool.Acquire<VariableBoolean>();
            varValue.value = value.value != 0;
            return varValue;
        }

        public static explicit operator VariableBoolean(VariableInt32 value)
        {
            VariableBoolean varValue = ReferencePool.Acquire<VariableBoolean>();
            varValue.value = value.value != 0;
            return varValue;
        }

        public static explicit operator VariableBoolean(VariableInt64 value)
        {
            VariableBoolean varValue = ReferencePool.Acquire<VariableBoolean>();
            varValue.value = value.value != 0;
            return varValue;
        }

        public static explicit operator VariableBoolean(VariableSingle value)
        {
            VariableBoolean varValue = ReferencePool.Acquire<VariableBoolean>();
            varValue.value = value.value != 0;
            return varValue;
        }

        public static explicit operator VariableBoolean(VariableDouble value)
        {
            VariableBoolean varValue = ReferencePool.Acquire<VariableBoolean>();
            varValue.value = value.value != 0;
            return varValue;
        }

        public static explicit operator VariableBoolean(VariableDecimal value)
        {
            VariableBoolean varValue = ReferencePool.Acquire<VariableBoolean>();
            varValue.value = value.value != 0;
            return varValue;
        }

        public bool Equals(VariableBoolean other)
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
            return Equals((VariableBoolean) obj);
        }

        public override int GetHashCode()
        {
            return value.GetHashCode();
        }

        public static bool operator ==(VariableBoolean a, VariableBoolean b)
        {
            if (ReferenceEquals(a, null) == false) return a.Equals(b);
            if (ReferenceEquals(b, null)) return true;
            return false;
        }

        public static bool operator !=(VariableBoolean a, VariableBoolean b)
        {
            return ! (a == b);
        }
    }
}