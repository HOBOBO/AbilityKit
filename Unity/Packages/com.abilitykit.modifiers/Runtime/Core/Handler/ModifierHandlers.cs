using System;
using System.Runtime.CompilerServices;

namespace AbilityKit.Modifiers
{
    // ============================================================================
    // 数值型修改器处理器
    // ============================================================================

    /// <summary>
    /// 数值型修改器处理器。
    /// 处理 float/int 类型的数值修改。
    ///
    /// 内置操作：
    /// - Add: Base + Value
    /// - Mul: Base × Value
    /// - PercentAdd: Base × (1 + Value)
    /// - Override: 直接替换
    ///
    /// 业务层可通过继承扩展自定义操作。
    /// </summary>
    public class NumericModifierHandler : ModifierHandlerBase<float>
    {
        /// <summary>
        /// 使用 IModifierOperator 应用修改
        /// </summary>
        protected override float ApplyOperator(float currentValue, IModifierOperator op, float modifierValue)
        {
            return op.Apply(currentValue, modifierValue);
        }

        /// <summary>
        /// 比较两个值，用于 Override 优先级判断。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int Compare(float a, float b)
        {
            return a.CompareTo(b);
        }

        /// <summary>
        /// 合并多个修改器的值。对于数值类型，默认求和。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override float Combine(in Span<float> values)
        {
            if (values.Length == 0) return 0f;

            float sum = 0f;
            for (int i = 0; i < values.Length; i++)
            {
                sum += values[i];
            }
            return sum;
        }
    }

    // ============================================================================
    // 整数型修改器处理器
    // ============================================================================

    /// <summary>
    /// 整数型修改器处理器。
    /// 处理 int 类型的数值修改。
    /// </summary>
    public class IntModifierHandler : ModifierHandlerBase<int>
    {
        /// <summary>
        /// 使用 IModifierOperator 应用修改
        /// </summary>
        protected override int ApplyOperator(int currentValue, IModifierOperator op, float modifierValue)
        {
            // IModifierOperator 返回 float，需要转换
            float result = op.Apply(currentValue, modifierValue);
            return (int)result;
        }

        /// <summary>
        /// 比较两个值
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int Compare(int a, int b)
        {
            return a.CompareTo(b);
        }

        /// <summary>
        /// 合并多个值
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int Combine(in Span<int> values)
        {
            if (values.Length == 0) return 0;

            int sum = 0;
            for (int i = 0; i < values.Length; i++)
            {
                sum += values[i];
            }
            return sum;
        }
    }

    // ============================================================================
    // 布尔型修改器处理器
    // ============================================================================

    /// <summary>
    /// 布尔型修改器处理器。
    /// 处理 bool 类型的状态修改。
    ///
    /// 操作：
    /// - Override: 直接设置为目标值
    /// - Add: 切换状态 (XOR)
    /// </summary>
    public class BooleanModifierHandler : IModifierHandler<bool>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual bool Apply(bool currentValue, in ModifierData modifier, IModifierContext context)
        {
            bool targetValue = modifier.CustomData.IntValue != 0;

            return modifier.Op switch
            {
                ModifierOp.Override => targetValue,
                ModifierOp.Add => currentValue ^ targetValue,
                _ => currentValue
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual int Compare(bool a, bool b)
        {
            return (a ? 1 : 0).CompareTo(b ? 1 : 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual bool Combine(in Span<bool> values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i]) return true;
            }
            return false;
        }
    }

    // ============================================================================
    // 枚举型修改器处理器
    // ============================================================================

    /// <summary>
    /// 枚举型修改器处理器。
    /// 处理枚举类型的修改（通常使用 Override）。
    ///
    /// 示例：技能阶段、Buff 类型等
    /// </summary>
    public class EnumModifierHandler<TEnum> : IModifierHandler<TEnum> where TEnum : struct, Enum
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual TEnum Apply(TEnum currentValue, in ModifierData modifier, IModifierContext context)
        {
            if (modifier.Op == ModifierOp.Override)
            {
                if (Enum.TryParse<TEnum>(modifier.CustomData.StringValue, out var result))
                {
                    return result;
                }
            }
            return currentValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual int Compare(TEnum a, TEnum b)
        {
            return ((int)(object)a).CompareTo((int)(object)b);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual TEnum Combine(in Span<TEnum> values)
        {
            return values.Length > 0 ? values[0] : default;
        }
    }
}
