using Sirenix.OdinInspector;

namespace Emilia.Variables
{
    public enum VariableCompareOperator
    {
        [LabelText("等于")]
        Equal,

        [LabelText("不等于")]
        NotEqual,

        [LabelText("大于等于")]
        GreaterOrEqual,

        [LabelText("大于")]
        Greater,

        [LabelText("小于等于")]
        SmallerOrEqual,

        [LabelText("小于")]
        Smaller,

        [LabelText("总是为真")]
        AlwaysTrue,

        [LabelText("总是为假")]
        AlwaysFalse,
    }
}