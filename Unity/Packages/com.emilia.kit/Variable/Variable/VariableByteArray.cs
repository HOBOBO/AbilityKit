using System;
using Emilia.Reference;
using Sirenix.OdinInspector;

namespace Emilia.Variables
{
    [Serializable, LabelText("Byte Array(字节数组)")]
    public class VariableByteArray : Variable<byte[]>
    {
        public static implicit operator VariableByteArray(byte[] value)
        {
            VariableByteArray varValue = ReferencePool.Acquire<VariableByteArray>();
            varValue.value = value;
            return varValue;
        }

        public static implicit operator byte[](VariableByteArray value)
        {
            return value.value;
        }
    }
}