using System;
using UnityEngine;

namespace Emilia.Variables
{
    public static partial class VariableUtility
    {
        public static bool Compare(Variable left, Variable right, VariableCompareOperator @operator)
        {
            Type leftType = left.GetType();
            Type rightType = right.GetType();

            switch (@operator)
            {
                case VariableCompareOperator.AlwaysTrue:
                    return true;
                case VariableCompareOperator.AlwaysFalse:
                    return false;
                case VariableCompareOperator.Equal:
                    if (leftType != rightType) return false;
                    return Equal(leftType, left, right);
                case VariableCompareOperator.NotEqual:
                    if (leftType != rightType) return true;
                    return NotEqual(leftType, left, right);
                case VariableCompareOperator.GreaterOrEqual:
                    if (leftType != rightType) return false;
                    return GreaterOrEqual(leftType, left, right);
                case VariableCompareOperator.Greater:
                    if (leftType != rightType) return false;
                    return Greater(leftType, left, right);
                case VariableCompareOperator.SmallerOrEqual:
                    if (leftType != rightType) return false;
                    return SmallerOrEqual(leftType, left, right);
                case VariableCompareOperator.Smaller:
                    if (leftType != rightType) return false;
                    return Smaller(leftType, left, right);
                default:
                    throw new ArgumentOutOfRangeException(nameof(@operator), @operator, null);
            }
        }

        private static bool Equal(Type type, Variable left, Variable right)
        {
            if (variableEqual.TryGetValue(type, out var equal)) return equal(left, right);
            Debug.LogError($"{type} 运算符 == 未注册");
            return false;
        }

        private static bool NotEqual(Type type, Variable left, Variable right)
        {
            if (variableNotEqual.TryGetValue(type, out var notEqual)) return notEqual(left, right);
            Debug.LogError($"{type} 运算符 != 未注册");
            return false;
        }

        private static bool GreaterOrEqual(Type type, Variable left, Variable right)
        {
            if (variableGreaterOrEqual.TryGetValue(type, out var greaterOrEqual)) return greaterOrEqual(left, right);
            Debug.LogError($"{type} 运算符 >= 未注册");
            return false;
        }

        private static bool Greater(Type type, Variable left, Variable right)
        {
            if (variableGreater.TryGetValue(type, out var greater)) return greater(left, right);
            Debug.LogError($"{type} 运算符 > 未注册");
            return false;
        }

        private static bool SmallerOrEqual(Type type, Variable left, Variable right)
        {
            if (variableSmallerOrEqual.TryGetValue(type, out var smallerOrEqual)) return smallerOrEqual(left, right);
            Debug.LogError($"{type} 运算符 <= 未注册");
            return false;
        }

        private static bool Smaller(Type type, Variable left, Variable right)
        {
            if (variableSmaller.TryGetValue(type, out var smaller)) return smaller(left, right);
            Debug.LogError($"{type} 运算符 < 未注册");
            return false;
        }
    }
}