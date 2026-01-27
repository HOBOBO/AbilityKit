namespace Emilia.Variables
{
    public static partial class VariableUtility
    {
        public static string ToDisplayString(VariableCalculateOperator calculateOperator)
        {
            switch (calculateOperator)
            {
                case VariableCalculateOperator.Set:
                    return "=";
                case VariableCalculateOperator.Add:
                    return "+";
                case VariableCalculateOperator.Subtract:
                    return "-";
                case VariableCalculateOperator.Multiply:
                    return "*";
                case VariableCalculateOperator.Divide:
                    return "/";
            }

            return string.Empty;
        }

        public static string ToDisplayString(VariableCompareOperator compareOperator)
        {
            switch (compareOperator)
            {
                case VariableCompareOperator.Equal:
                    return "==";
                case VariableCompareOperator.NotEqual:
                    return "!=";
                case VariableCompareOperator.GreaterOrEqual:
                    return ">=";
                case VariableCompareOperator.Greater:
                    return ">";
                case VariableCompareOperator.SmallerOrEqual:
                    return "<=";
                case VariableCompareOperator.Smaller:
                    return "<";
                case VariableCompareOperator.AlwaysTrue:
                    return "[True]";
                case VariableCompareOperator.AlwaysFalse:
                    return "[False]";
            }

            return string.Empty;
        }
    }
}