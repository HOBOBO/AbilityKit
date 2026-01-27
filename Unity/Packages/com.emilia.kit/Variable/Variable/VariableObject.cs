using System;
using Sirenix.OdinInspector;

namespace Emilia.Variables
{
    [Serializable, LabelText("Object(任意对象)")]
    public class VariableObject : Variable<object>
    {
        public bool Equals(VariableObject other)
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
            return Equals((VariableObject) obj);
        }

        public override int GetHashCode()
        {
            return value.GetHashCode();
        }

        public static bool operator ==(VariableObject a, VariableObject b)
        {
            if (ReferenceEquals(a, null) == false) return a.Equals(b);
            if (ReferenceEquals(b, null)) return true;
            return false;
        }

        public static bool operator !=(VariableObject a, VariableObject b)
        {
            return ! (a == b);
        }
    }
}