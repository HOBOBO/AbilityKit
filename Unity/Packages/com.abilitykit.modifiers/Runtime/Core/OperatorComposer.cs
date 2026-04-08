using System;
using System.Runtime.CompilerServices;

namespace AbilityKit.Modifiers
{
    // ============================================================================
    // 操作组合器
    // ============================================================================

    /// <summary>
    /// 操作组合器。
    /// 管理修饰器操作的组合逻辑。
    ///
    /// 职责：
    /// - 使用 IModifierOperator 执行具体操作
    /// - 管理操作顺序（Override 最先，Add/Mul/PercentAdd 按优先级）
    /// - 支持自定义操作
    ///
    /// 设计原则：
    /// - 无 GC：所有方法返回值均为值类型
    /// - 零分配：使用 Span 进行批处理
    /// </summary>
    public struct OperatorComposer
    {
        #region 核心组合逻辑

        /// <summary>
        /// 组合多个修改器到基础值上。
        /// 使用 IModifierOperator 执行操作。
        ///
        /// 操作顺序：
        /// 1. Override 操作（优先级 0）- 直接替换值
        /// 2. Add 操作（优先级 10）- 加法
        /// 3. PercentAdd 操作（优先级 15）- 百分比加成
        /// 4. Mul 操作（优先级 20）- 乘法
        /// </summary>
        /// <param name="modifiers">修改器数组</param>
        /// <param name="baseValue">基础值</param>
        /// <param name="level">等级</param>
        /// <param name="context">上下文</param>
        /// <returns>组合结果</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ModifierResult Compose(
            ReadOnlySpan<ModifierData> modifiers,
            float baseValue,
            float level,
            IModifierContext context)
        {
            int count = modifiers.Length;

            if (count == 0)
                return ModifierResult.Empty(baseValue);

            float addSum = 0f;
            float mulProduct = 1f;
            float overrideValue = 0f;
            bool hasOverride = false;
            int appliedCount = 0;

            for (int i = 0; i < count; i++)
            {
                var modifier = modifiers[i];
                float modValue = modifier.GetMagnitude(level, context);

                var op = ModifierOperatorRegistry.Get(modifier.Op);
                if (op == null) continue;

                if (op.IsTerminal)
                {
                    // Override 操作：直接替换
                    overrideValue = modValue;
                    hasOverride = true;
                    appliedCount = 1;
                    break;
                }

                if (op.IsAdditive)
                {
                    addSum += op.CalculateContribution(baseValue, modValue);
                }
                else
                {
                    mulProduct *= modValue;
                }

                appliedCount++;
            }

            // 处理 Mul 是否为 1 的情况
            bool isMulOne = MathF.Abs(mulProduct - 1f) < 0.0001f;

            return new ModifierResult
            {
                BaseValue = baseValue,
                AddSum = addSum,
                MulProduct = isMulOne ? 1f : mulProduct,
                OverrideValue = overrideValue,
                OverrideFlag = hasOverride ? (byte)1 : (byte)0,
                Count = appliedCount
            };
        }

        /// <summary>
        /// 批量组合修改器
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ComposeBatch(
            ReadOnlySpan<ModifierData> modifiers,
            ReadOnlySpan<float> baseValues,
            float level,
            IModifierContext context,
            Span<ModifierResult> results)
        {
            for (int i = 0; i < baseValues.Length; i++)
            {
                results[i] = Compose(modifiers, baseValues[i], level, context);
            }
        }

        #endregion

        #region 优先级排序

        /// <summary>
        /// 按优先级对修改器进行稳定排序
        /// </summary>
        public static void SortByPriority(Span<ModifierData> modifiers)
        {
            // 简单插入排序（对于少量元素效率高）
            for (int i = 1; i < modifiers.Length; i++)
            {
                var key = modifiers[i];
                int j = i - 1;

                while (j >= 0 && modifiers[j].Priority > key.Priority)
                {
                    modifiers[j + 1] = modifiers[j];
                    j--;
                }

                modifiers[j + 1] = key;
            }
        }

        #endregion
    }

    // ============================================================================
    // 扩展方法
    // ============================================================================

    /// <summary>
    /// OperatorComposer 的扩展方法
    /// </summary>
    public static class OperatorComposerExtensions
    {
        /// <summary>
        /// 计算修改器对单个值的影响
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ApplyModifiers(
            this ReadOnlySpan<ModifierData> modifiers,
            float baseValue,
            float level = 1f,
            IModifierContext context = null)
        {
            return OperatorComposer.Compose(modifiers, baseValue, level, context).FinalValue;
        }

        /// <summary>
        /// 计算修改器对单个值的影响（返回详细信息）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ModifierResult CalculateWithDetails(
            this ReadOnlySpan<ModifierData> modifiers,
            float baseValue,
            float level = 1f,
            IModifierContext context = null)
        {
            return OperatorComposer.Compose(modifiers, baseValue, level, context);
        }
    }
}