using System;
using Emilia.Reference;
using Sirenix.OdinInspector;

namespace Emilia.Variables
{
    [Serializable, LabelText("Char(字符)")]
    public class VariableChar : Variable<char>
    {
        public static implicit operator VariableChar(char value)
        {
            VariableChar varValue = ReferencePool.Acquire<VariableChar>();
            varValue.value = value;
            return varValue;
        }

        public static implicit operator char(VariableChar value)
        {
            return value.value;
        }

        public static explicit operator VariableChar(VariableString value)
        {
            VariableChar varValue = ReferencePool.Acquire<VariableChar>();
            varValue.value = char.Parse(value.value);
            return varValue;
        }

        public bool Equals(VariableChar other)
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
            return Equals((VariableChar) obj);
        }

        public override int GetHashCode()
        {
            return value.GetHashCode();
        }

        public static bool operator ==(VariableChar a, VariableChar b)
        {
            if (ReferenceEquals(a, null) == false) return a.Equals(b);
            if (ReferenceEquals(b, null)) return true;
            return false;
        }

        public static bool operator !=(VariableChar a, VariableChar b)
        {
            return ! (a == b);
        }

        public static bool operator >(VariableChar a, VariableChar b)
        {
            return a.value > b.value;
        }

        public static bool operator <(VariableChar a, VariableChar b)
        {
            return a.value < b.value;
        }

        public static bool operator >=(VariableChar a, VariableChar b)
        {
            return a.value >= b.value;
        }

        public static bool operator <=(VariableChar a, VariableChar b)
        {
            return a.value <= b.value;
        }

        public static VariableChar operator +(VariableChar a, VariableChar b)
        {
            return a.value += b.value;
        }

        public static VariableChar operator -(VariableChar a, VariableChar b)
        {
            return a.value -= b.value;
        }

        public static VariableChar operator *(VariableChar a, VariableChar b)
        {
            return a.value *= b.value;
        }

        public static VariableChar operator /(VariableChar a, VariableChar b)
        {
            return a.value /= b.value;
        }
    }
}