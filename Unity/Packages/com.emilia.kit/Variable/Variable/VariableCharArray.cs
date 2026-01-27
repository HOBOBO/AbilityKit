using System;
using Emilia.Reference;
using Sirenix.OdinInspector;

namespace Emilia.Variables
{
    [Serializable, LabelText("Char Array(字符数组)")]
    public class VariableCharArray : Variable<char[]>
    {
        public static implicit operator VariableCharArray(char[] value)
        {
            VariableCharArray varValue = ReferencePool.Acquire<VariableCharArray>();
            varValue.value = value;
            return varValue;
        }

        public static implicit operator char[](VariableCharArray value)
        {
            return value.value;
        }
    }
}