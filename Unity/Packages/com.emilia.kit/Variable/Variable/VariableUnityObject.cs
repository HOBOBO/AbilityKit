using System;
using Emilia.Reference;
using Sirenix.OdinInspector;
using Object = UnityEngine.Object;

namespace Emilia.Variables
{
    [Serializable, LabelText("UnityObject(Unity任意对象)")]
    public class VariableUnityObject : Variable<Object>
    {
        public static implicit operator VariableUnityObject(Object value)
        {
            VariableUnityObject varValue = ReferencePool.Acquire<VariableUnityObject>();
            varValue.value = value;
            return varValue;
        }

        public static implicit operator Object(VariableUnityObject value)
        {
            return value.value;
        }

        public bool Equals(VariableUnityObject other)
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
            return Equals((VariableUnityObject) obj);
        }

        public override int GetHashCode()
        {
            return value.GetHashCode();
        }

        public static bool operator ==(VariableUnityObject a, VariableUnityObject b)
        {
            if (ReferenceEquals(a, null) == false) return a.Equals(b);
            if (ReferenceEquals(b, null)) return true;
            return false;
        }

        public static bool operator !=(VariableUnityObject a, VariableUnityObject b)
        {
            return ! (a == b);
        }
    }
}