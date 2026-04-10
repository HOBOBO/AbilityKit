using System;
using System.Runtime.CompilerServices;

namespace AbilityKit.Modifiers
{
    // ============================================================================
    // 修饰器缓存
    // ============================================================================

    /// <summary>
    /// 修饰器缓存。
    /// 缓存修饰器计算结果，避免重复计算。
    ///
    /// 设计原则：
    /// - 无 GC：所有字段均为值类型
    /// - 自动失效：当检测到修改器数据变化时自动失效
    /// - 支持时变检测：检测是否有时变修饰器，必要时禁用缓存
    /// </summary>
    public struct ModifierCache
    {
        #region 缓存字段

        private readonly int _lastCount;
        private readonly int _lastHash;
        private readonly float _lastBaseValue;
        private readonly float _lastContextTime;
        private readonly ModifierResult _cachedResult;
        private readonly bool _isTimeVarying;

        #endregion

        #region 构造函数

        /// <summary>创建空缓存</summary>
        public static ModifierCache Empty => default;

        /// <summary>是否为空</summary>
        public bool IsEmpty => _lastCount == -1;

        private ModifierCache(
            int lastCount,
            int lastHash,
            float lastBaseValue,
            float lastContextTime,
            ModifierResult cachedResult,
            bool isTimeVarying)
        {
            _lastCount = lastCount;
            _lastHash = lastHash;
            _lastBaseValue = lastBaseValue;
            _lastContextTime = lastContextTime;
            _cachedResult = cachedResult;
            _isTimeVarying = isTimeVarying;
        }

        #endregion

        #region 缓存查询

        /// <summary>
        /// 尝试获取缓存的结果
        /// </summary>
        /// <param name="modifiers">修改器数组</param>
        /// <param name="baseValue">基础值</param>
        /// <param name="context">上下文</param>
        /// <param name="result">输出结果</param>
        /// <returns>是否有有效缓存</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGet(
            ReadOnlySpan<ModifierData> modifiers,
            float baseValue,
            IModifierContext context,
            out ModifierResult result)
        {
            result = default;

            // 快速检查：修改器数量和基础值
            if (modifiers.Length != _lastCount || _lastBaseValue != baseValue)
                return false;

            // 时变修饰器：检查时间
            if (_isTimeVarying)
            {
                if (context != null && context.CurrentTime != _lastContextTime)
                    return false;
            }
            else
            {
                // 非时变：检查哈希
                int hash = ComputeHash(modifiers);
                if (hash != _lastHash)
                    return false;
            }

            result = _cachedResult;
            return true;
        }

        /// <summary>
        /// 存储缓存结果
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ModifierCache Store(
            ReadOnlySpan<ModifierData> modifiers,
            float baseValue,
            IModifierContext context,
            ModifierResult result)
        {
            return new ModifierCache(
                modifiers.Length,
                ComputeHash(modifiers),
                baseValue,
                context?.CurrentTime ?? 0f,
                result,
                ContainsTimeVarying(modifiers)
            );
        }

        /// <summary>
        /// 清除缓存
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ModifierCache Clear()
        {
            return default;
        }

        #endregion

        #region 工具方法

        private static int ComputeHash(ReadOnlySpan<ModifierData> modifiers)
        {
            // 简单的哈希计算：基于 Key, Op, Priority, SourceId
            int hash = 17;
            for (int i = 0; i < modifiers.Length; i++)
            {
                var mod = modifiers[i];
                hash = hash * 31 + mod.Key.GetHashCode();
                hash = hash * 31 + (int)mod.Op;
                hash = hash * 31 + mod.Priority;
                hash = hash * 31 + mod.SourceId;
            }
            return hash;
        }

        private static bool ContainsTimeVarying(ReadOnlySpan<ModifierData> modifiers)
        {
            for (int i = 0; i < modifiers.Length; i++)
            {
                if (modifiers[i].Magnitude.IsTimeVarying)
                    return true;
            }
            return false;
        }

        #endregion
    }

    // ============================================================================
    // 修饰器计算核心
    // ============================================================================

    /// <summary>
    /// 修饰器计算核心。
    /// 包含修饰器计算的核心逻辑，不包含缓存。
    ///
    /// 设计原则：
    /// - 单一职责：只负责计算，不负责缓存
    /// - 无 GC：所有方法返回值均为值类型
    /// - 可测试：核心逻辑可以独立测试
    /// </summary>
    public struct ModifierComputeCore
    {
        #region 核心计算

        /// <summary>
        /// 计算修改器对基础值的影响
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ModifierResult Compute(
            ReadOnlySpan<ModifierData> modifiers,
            float baseValue,
            float level,
            IModifierContext context,
            IModifierRecorder recorder = null)
        {
            int count = modifiers.Length;

            if (count == 0)
                return ModifierResult.Empty(baseValue);

            // 使用 OperatorComposer 进行组合计算
            var result = OperatorComposer.Compose(modifiers, baseValue, level, context);

            // 来源追踪：计算每个修改器对最终值的贡献
            if (recorder != null)
            {
                // 预计算用于贡献计算的基准值
                float addBase = baseValue;  // Add 操作的基准值就是 baseValue
                float percentBase = baseValue + result.AddSum;  // PercentAdd 操作的基准值包含 Add

                for (int i = 0; i < count; i++)
                {
                    var mod = modifiers[i];
                    float modValue = mod.GetMagnitude(level, context);

                    // 计算贡献量
                    float contribution = mod.Op switch
                    {
                        ModifierOp.Add => modValue,
                        ModifierOp.Mul => addBase * (modValue - 1f),
                        ModifierOp.PercentAdd => percentBase * modValue,
                        ModifierOp.Override => modValue - baseValue,
                        _ => modValue
                    };

                    recorder.Record(new ModifierSourceEntry
                    {
                        Op = mod.Op,
                        Value = modValue,
                        Contribution = contribution,
                        SourceId = mod.SourceId,
                        SourceNameIndex = mod.SourceNameIndex
                    });

                    // 更新 PercentAdd 的基准值（用于后续 PercentAdd 操作）
                    if (mod.Op == ModifierOp.PercentAdd)
                    {
                        percentBase += contribution;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// 计算单个标签的最终值
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float ComputeFinal(
            ReadOnlySpan<ModifierData> modifiers,
            float baseValue,
            float level = 1f,
            IModifierContext context = null)
        {
            return Compute(modifiers, baseValue, level, context).FinalValue;
        }

        #endregion

        #region 批量计算

        /// <summary>
        /// 批量计算多个基础值的修改结果
        /// </summary>
        public void ComputeBatch(
            ReadOnlySpan<ModifierData> modifiers,
            ReadOnlySpan<float> bases,
            float level,
            IModifierContext context,
            Span<ModifierResult> results)
        {
            for (int i = 0; i < bases.Length; i++)
            {
                results[i] = Compute(modifiers, bases[i], level, context);
            }
        }

        #endregion
    }
}