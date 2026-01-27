using System;
using UnityEngine;

namespace Emilia.Variables
{
    public static partial class VariableUtility
    {
        public static Variable Calculate(Variable left, Variable right, VariableCalculateOperator calculateOperator)
        {
            Type leftType = left.GetType();
            Type rightType = right.GetType();

            if (leftType != rightType) right = Convert(right, left);
            if (right == null) return null;

            switch (calculateOperator)
            {
                case VariableCalculateOperator.Set:
                    left.SetValue(right.GetValue());
                    return left;
                case VariableCalculateOperator.Add:
                    if (variableAdd.TryGetValue(leftType, out var add)) return add(left, right);
                    Debug.LogError($"{leftType} 运算符 + 未注册");
                    return null;
                case VariableCalculateOperator.Subtract:
                    if (variableSubtract.TryGetValue(leftType, out var subtract)) return subtract(left, right);
                    Debug.LogError($"{leftType} 运算符 - 未注册");
                    return null;
                case VariableCalculateOperator.Multiply:
                    if (variableMultiply.TryGetValue(leftType, out var multiply)) return multiply(left, right);
                    Debug.LogError($"{leftType} 运算符 * 未注册");
                    return null;
                case VariableCalculateOperator.Divide:
                    if (variableDivide.TryGetValue(leftType, out var divide)) return divide(left, right);
                    Debug.LogError($"{leftType} 运算符 / 未注册");
                    return null;
                default:
                    throw new ArgumentOutOfRangeException(nameof(calculateOperator), calculateOperator, null);
            }
        }
    }
}