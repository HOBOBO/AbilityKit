using System;
using System.Runtime.CompilerServices;

namespace AbilityKit.Modifiers
{
    /// <summary>
    /// 数值型修改器处理器。
    /// 默认实现，处理 float 类型的修改器。
    ///
    /// 内置操作：
    /// - Add: Base + Value
    /// - Mul: Base × Value
    /// - PercentAdd: Base × (1 + Value)
    /// - Override: 直接替换
    ///
    /// 业务层可通过继承或组合扩展自定义操作。
    /// </summary>
    public class NumericModifierHandler : IModifierHandler<float>
    {
        #region IModifierHandler<float>

        /// <summary>
        /// 应用单个修改器到基础值
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual float Apply(float baseValue, in ModifierData modifier, IModifierContext context)
        {
            float value = modifier.GetMagnitude(context?.Level ?? 1f, context);

            return modifier.Op switch
            {
                ModifierOp.Add => baseValue + value,
                ModifierOp.Mul => baseValue * value,
                ModifierOp.PercentAdd => baseValue * (1f + value),
                ModifierOp.Override => value,
                ModifierOp.Custom => ApplyCustom(baseValue, modifier, context),
                _ => baseValue
            };
        }

        /// <summary>
        /// 比较两个值，用于 Override 优先级判断。
        /// 默认按数值比较，业务层可重写。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual int Compare(float a, float b)
        {
            return a.CompareTo(b);
        }

        /// <summary>
        /// 合并多个修改器的值。
        /// 对于数值类型，默认求和。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual float Combine(in Span<float> values)
        {
            if (values.Length == 0) return 0f;

            float sum = 0f;
            for (int i = 0; i < values.Length; i++)
            {
                sum += values[i];
            }
            return sum;
        }

        #endregion

        #region 扩展点

        /// <summary>
        /// 自定义操作处理。
        /// 业务层可继承 NumericModifierHandler 并重写此方法。
        /// </summary>
        protected virtual float ApplyCustom(float baseValue, in ModifierData modifier, IModifierContext context)
        {
            return baseValue;
        }

        #endregion
    }

    /// <summary>
    /// 技能 ID 修改器处理器。
    /// 示例：展示如何扩展处理非数值类型。
    /// </summary>
    public struct SkillIdModifierHandler : IModifierHandler<int>
    {
        /// <summary>
        /// 替换规则：Override 直接替换，否则保持原值
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Apply(int baseValue, in ModifierData modifier, IModifierContext context)
        {
            if (modifier.Op == ModifierOp.Override && modifier.CustomData.CustomTypeId == 1)
            {
                return modifier.CustomData.IntValue;
            }
            return baseValue;
        }

        /// <summary>
        /// 技能 ID 比较：直接比较数值
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Compare(int a, int b)
        {
            return a.CompareTo(b);
        }

        /// <summary>
        /// 合并：技能 ID 不支持合并，返回第一个
        /// </summary>
        public int Combine(in Span<int> values)
        {
            return values.Length > 0 ? values[0] : 0;
        }
    }

    /// <summary>
    /// 布尔型修改器处理器。
    /// 示例：处理无敌、免疫等布尔状态。
    /// </summary>
    public struct BooleanModifierHandler : IModifierHandler<bool>
    {
        /// <summary>
        /// Override: 直接设置为目标值
        /// Add: 切换状态
        /// Mul: 不支持
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Apply(bool baseValue, in ModifierData modifier, IModifierContext context)
        {
            return modifier.Op switch
            {
                ModifierOp.Override => modifier.Value > 0.5f,
                ModifierOp.Add => baseValue ^ (modifier.Value > 0.5f),
                _ => baseValue
            };
        }

        /// <summary>
        /// 比较：True > False
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Compare(bool a, bool b)
        {
            return (a ? 1 : 0).CompareTo(b ? 1 : 0);
        }

        /// <summary>
        /// 合并：任意 True 则为 True
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Combine(in Span<bool> values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i]) return true;
            }
            return false;
        }
    }
}
