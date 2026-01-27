using System;
using System.Collections.Generic;
using Emilia.Reference;
using Sirenix.OdinInspector;

namespace Emilia.Variables
{
    [Serializable, LabelText("Int32(整数)列表")]
    public class VariableInt32List : Variable<List<int>>
    {
        public static implicit operator VariableInt32List(List<int> value)
        {
            VariableInt32List varValue = ReferencePool.Acquire<VariableInt32List>();
            varValue.value = value;
            return varValue;
        }

        public static implicit operator List<int>(VariableInt32List value)
        {
            return value.value;
        }
    }
}