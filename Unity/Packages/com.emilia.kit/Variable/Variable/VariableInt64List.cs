using System;
using System.Collections.Generic;
using Emilia.Reference;
using Sirenix.OdinInspector;

namespace Emilia.Variables
{
    [Serializable, LabelText("Int64(整数)列表")]
    public class VariableInt64List : Variable<List<long>>
    {
        public static implicit operator VariableInt64List(List<long> value)
        {
            VariableInt64List varValue = ReferencePool.Acquire<VariableInt64List>();
            varValue.value = value;
            return varValue;
        }

        public static implicit operator List<long>(VariableInt64List value)
        {
            return value.value;
        }
    }
}