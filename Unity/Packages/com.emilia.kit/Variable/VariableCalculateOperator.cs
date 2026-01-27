using Sirenix.OdinInspector;

namespace Emilia.Variables
{
    public enum VariableCalculateOperator
    {
        [LabelText("赋值=")]
        Set,

        [LabelText("加+")]
        Add,

        [LabelText("减-")]
        Subtract,

        [LabelText("乘*")]
        Multiply,

        [LabelText("除/")]
        Divide,
    }
}