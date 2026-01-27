using System;
using Emilia.Reference;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Emilia.Variables
{
    [Serializable, LabelText("GameObject(游戏对象)")]
    public class VariableGameObject : Variable<GameObject>
    {
        public static implicit operator VariableGameObject(GameObject value)
        {
            VariableGameObject varValue = ReferencePool.Acquire<VariableGameObject>();
            varValue.value = value;
            return varValue;
        }

        public static implicit operator GameObject(VariableGameObject value)
        {
            return value.value;
        }

        public bool Equals(VariableGameObject other)
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
            return Equals((VariableGameObject) obj);
        }

        public override int GetHashCode()
        {
            return value.GetHashCode();
        }

        public static bool operator ==(VariableGameObject a, VariableGameObject b)
        {
            if (ReferenceEquals(a, null) == false) return a.Equals(b);
            if (ReferenceEquals(b, null)) return true;
            return false;
        }

        public static bool operator !=(VariableGameObject a, VariableGameObject b)
        {
            return ! (a == b);
        }
    }
}