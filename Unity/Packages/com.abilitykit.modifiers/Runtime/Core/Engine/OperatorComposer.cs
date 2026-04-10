using System;
using System.Runtime.CompilerServices;

namespace AbilityKit.Modifiers
{
    // ============================================================================
    // 操作组合策略接口
    // ============================================================================

    /// <summary>
    /// 操作组合策略接口。
    /// 定义如何将多个修改器的效果合并到基础值上。
    ///
    /// 使用场景：
    /// - 默认策略：按 Priority 分组计算
    /// - 自定义策略：业务层可实现不同的组合顺序或公式
    ///
    /// 示例：
    /// ```csharp
    /// // 默认策略：Override > Add > PercentAdd > Mul
    /// public class DefaultComposerStrategy : IComposerStrategy { ... }
    ///
    /// // 自定义策略：业务层可按需实现
    /// public class MyStrategy : IComposerStrategy { ... }
    /// ```
    /// </summary>
    public interface IComposerStrategy
    {
        /// <summary>
        /// 组合多个修改器到基础值上
        /// </summary>
        ModifierResult Compose(
            ReadOnlySpan<ModifierData> modifiers,
            float baseValue,
            float level,
            IModifierContext context);

        /// <summary>
        /// 策略名称
        /// </summary>
        string Name { get; }
    }

    // ============================================================================
    // 操作组合器
    // ============================================================================

    /// <summary>
    /// 操作组合器。
    /// 管理修饰器操作的组合逻辑。
    ///
    /// 职责：
    /// - 使用 IModifierOperator 执行具体操作
    /// - 支持策略注入：可自定义组合顺序和公式
    /// - 默认策略：按操作优先级分组计算（Override > Add > PercentAdd > Mul）
    ///
    /// 设计原则：
    /// - 无 GC：所有方法返回值均为值类型
    /// - 零分配：使用 Span 进行批处理
    /// - 可扩展：业务层可实现 IComposerStrategy 自定义组合逻辑
    /// </summary>
    public struct OperatorComposer
    {
        #region 默认策略

        /// <summary>
        /// 默认组合策略。
        /// 按操作优先级分组计算：
        /// - Priority 0: Override（终止操作，直接替换值）
        /// - Priority 10: Add（加法）
        /// - Priority 15: PercentAdd（百分比加成）
        /// - Priority 20: Mul（乘法）
        ///
        /// 计算公式：
        /// FinalValue = OverrideFlag ? OverrideValue
        ///                        : (BaseValue + AddSum) × PercentProduct × MulProduct
        /// </summary>
        public static readonly IComposerStrategy DefaultStrategy = new DefaultComposerStrategy();

        private class DefaultComposerStrategy : IComposerStrategy
        {
            public string Name => "Default";

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ModifierResult Compose(
                ReadOnlySpan<ModifierData> modifiers,
                float baseValue,
                float level,
                IModifierContext context)
            {
                int count = modifiers.Length;

                if (count == 0)
                    return ModifierResult.Empty(baseValue);

                // 按 Priority 分组累积
                float addSum = 0f;
                float percentProduct = 1f;
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

                    // Override 操作（Priority 0）：终止操作，直接替换
                    if (op.IsTerminal)
                    {
                        overrideValue = modValue;
                        hasOverride = true;
                        appliedCount = 1;
                        break;
                    }

                    // 按 Priority 累积到对应分组
                    switch (op.Priority)
                    {
                        case 10: // Add
                            addSum += modValue;
                            appliedCount++;
                            break;

                        case 15: // PercentAdd
                            percentProduct *= (1f + modValue);
                            appliedCount++;
                            break;

                        case 20: // Mul
                            mulProduct *= modValue;
                            appliedCount++;
                            break;

                        default:
                            // 自定义 Priority：按 IsAdditive 决定分组
                            if (op.IsAdditive)
                                addSum += modValue;
                            else
                                mulProduct *= modValue;
                            appliedCount++;
                            break;
                    }
                }

                return new ModifierResult
                {
                    BaseValue = baseValue,
                    AddSum = addSum,
                    PercentProduct = percentProduct,
                    MulProduct = mulProduct,
                    OverrideValue = overrideValue,
                    OverrideFlag = hasOverride ? (byte)1 : (byte)0,
                    Count = appliedCount
                };
            }
        }

        #endregion

        #region 核心组合逻辑

        /// <summary>
        /// 组合多个修改器到基础值上。
        /// 使用默认策略（按操作优先级分组计算）。
        ///
        /// 操作顺序由 IModifierOperator.Priority 决定：
        /// - Priority 0: Override（最先，检查到就终止）
        /// - Priority 10: Add（加法）
        /// - Priority 15: PercentAdd（百分比加成）
        /// - Priority 20: Mul（乘法）
        ///
        /// 注意：此方法会对修改器进行稳定排序，修改器数量较多时会有一定开销。
        /// 如需高性能，可预先排序后使用 ComposeSorted 或注入自定义策略。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ModifierResult Compose(
            ReadOnlySpan<ModifierData> modifiers,
            float baseValue,
            float level,
            IModifierContext context)
        {
            return Compose(modifiers, baseValue, level, context, DefaultStrategy);
        }

        /// <summary>
        /// 组合多个修改器到基础值上（使用指定策略）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ModifierResult Compose(
            ReadOnlySpan<ModifierData> modifiers,
            float baseValue,
            float level,
            IModifierContext context,
            IComposerStrategy strategy)
        {
            if (strategy == null)
                return DefaultStrategy.Compose(modifiers, baseValue, level, context);

            int count = modifiers.Length;
            if (count == 0)
                return ModifierResult.Empty(baseValue);

            // 按 Priority 排序（稳定排序）
            if (count > 1)
            {
                var sorted = new ModifierData[count];
                modifiers.CopyTo(sorted);
                SortByPriority(sorted);
                return strategy.Compose(sorted, baseValue, level, context);
            }

            return strategy.Compose(modifiers, baseValue, level, context);
        }

        /// <summary>
        /// 组合已排序的修改器（高性能路径，避免重复排序）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ModifierResult ComposeSorted(
            ReadOnlySpan<ModifierData> sortedModifiers,
            float baseValue,
            float level,
            IModifierContext context)
        {
            return Compose(sortedModifiers, baseValue, level, context, DefaultStrategy);
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

        /// <summary>
        /// 批量组合修改器（使用指定策略）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ComposeBatch(
            ReadOnlySpan<ModifierData> modifiers,
            ReadOnlySpan<float> baseValues,
            float level,
            IModifierContext context,
            IComposerStrategy strategy,
            Span<ModifierResult> results)
        {
            for (int i = 0; i < baseValues.Length; i++)
            {
                results[i] = Compose(modifiers, baseValues[i], level, context, strategy);
            }
        }

        #endregion

        #region 优先级排序

        /// <summary>
        /// 按修改器优先级和操作优先级进行稳定排序
        /// </summary>
        public static void SortByPriority(Span<ModifierData> modifiers)
        {
            int count = modifiers.Length;
            if (count <= 1) return;

            // 插入排序（对小数据量高效，且稳定）
            for (int i = 1; i < count; i++)
            {
                var key = modifiers[i];
                int keyOpPriority = GetOperatorPriority(key.Op);
                int keyPriority = key.Priority;

                int j = i - 1;
                while (j >= 0)
                {
                    int prevOpPriority = GetOperatorPriority(modifiers[j].Op);
                    int prevPriority = modifiers[j].Priority;

                    // 首先按操作优先级排序，然后按修改器优先级排序
                    if (prevOpPriority > keyOpPriority ||
                        (prevOpPriority == keyOpPriority && prevPriority > keyPriority))
                    {
                        modifiers[j + 1] = modifiers[j];
                        j--;
                    }
                    else
                    {
                        break;
                    }
                }

                modifiers[j + 1] = key;
            }
        }

        /// <summary>
        /// 获取操作的计算优先级
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetOperatorPriority(ModifierOp op)
        {
            var opHandler = ModifierOperatorRegistry.Get(op);
            return opHandler?.Priority ?? int.MaxValue;
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

        /// <summary>
        /// 计算修改器对单个值的影响（使用指定策略）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ApplyModifiers(
            this ReadOnlySpan<ModifierData> modifiers,
            float baseValue,
            float level,
            IModifierContext context,
            IComposerStrategy strategy)
        {
            return OperatorComposer.Compose(modifiers, baseValue, level, context, strategy).FinalValue;
        }
    }
}