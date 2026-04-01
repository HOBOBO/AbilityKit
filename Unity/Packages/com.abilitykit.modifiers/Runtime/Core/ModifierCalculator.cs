using System;
using System.Runtime.CompilerServices;

namespace AbilityKit.Modifiers
{
    /// <summary>
    /// 通用修改器计算器。
    /// 支持任意类型的值，通过 IModifierHandler 接口扩展。
    ///
    /// 核心职责：
    /// - 将修改器数组应用到基础值上，产生 ModifierResult
    /// - 支持来源追踪（零堆分配）
    /// - 支持多种数值来源（固定值 / ScalableFloat / AttributeBased）
    /// - 支持缓存（基于修改器数量检测变化）
    /// - 支持自定义 Handler 扩展
    ///
    /// 设计原则：
    /// - 无 GC：所有方法返回值均为值类型，无 List/Array 分配
    /// - 缓存本地化：计算器本身无状态
    /// - 可扩展：通过 IModifierHandler 支持任意类型
    /// </summary>
    public sealed class ModifierCalculator
    {
        #region 缓存字段

        private int _lastCount;
        private int _lastHash;
        private float _lastBaseValue;
        private ModifierResult _cachedResult;
        private int _cachedHash;

        #endregion

        #region 属性

        /// <summary>是否启用缓存</summary>
        public bool EnableCache { get; set; } = true;

        #endregion

        #region 公共 API - float 版本

        /// <summary>
        /// 计算修改器对基础值的影响（不追踪来源）。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ModifierResult Calculate(ReadOnlySpan<ModifierData> modifiers, float baseValue)
        {
            return Calculate(modifiers, baseValue, null, level: 1f, null);
        }

        /// <summary>
        /// 计算修改器对基础值的影响（不追踪来源，指定等级）。
        /// </summary>
        public ModifierResult Calculate(
            ReadOnlySpan<ModifierData> modifiers,
            float baseValue,
            float level)
        {
            return Calculate(modifiers, baseValue, null, level, null);
        }

        /// <summary>
        /// 计算修改器对基础值的影响（追踪来源）。
        /// recorder 由调用方预分配，零堆分配。
        /// </summary>
        public ModifierResult Calculate(
            ReadOnlySpan<ModifierData> modifiers,
            float baseValue,
            IModifierRecorder recorder)
        {
            return Calculate(modifiers, baseValue, recorder, level: 1f, null);
        }

        /// <summary>
        /// 计算修改器对基础值的影响（追踪来源，指定等级）。
        /// recorder 由调用方预分配，零堆分配。
        /// </summary>
        public ModifierResult Calculate(
            ReadOnlySpan<ModifierData> modifiers,
            float baseValue,
            IModifierRecorder recorder,
            float level,
            Func<ModifierKey, float> captureDelegate)
        {
            int count = modifiers.Length;

            if (count == 0)
                return ModifierResult.Empty(baseValue);

            if (EnableCache && count == _lastCount && _lastBaseValue == baseValue)
            {
                int hash = ComputeHash(modifiers);
                if (hash == _cachedHash)
                    return _cachedResult;
            }

            var result = CalculateCore(modifiers, baseValue, recorder, level, captureDelegate);

            if (EnableCache && (recorder == null || recorder is NullRecorder))
            {
                _lastCount = count;
                _lastBaseValue = baseValue;
                _cachedHash = ComputeHash(modifiers);
                _cachedResult = result;
            }

            return result;
        }

        /// <summary>
        /// 计算单个标签的最终值（简化版本）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float CalculateFinal(ReadOnlySpan<ModifierData> modifiers, float baseValue)
            => Calculate(modifiers, baseValue).FinalValue;

        /// <summary>
        /// 批量计算多个基础值的修改结果。
        /// </summary>
        public void CalculateBatch(
            ReadOnlySpan<ModifierData> modifiers,
            ReadOnlySpan<float> bases,
            Span<ModifierResult> results)
        {
            for (int i = 0; i < bases.Length; i++)
            {
                results[i] = Calculate(modifiers, bases[i]);
            }
        }

        /// <summary>
        /// 手动清空缓存
        /// </summary>
        public void ClearCache()
        {
            _lastCount = -1;
            _cachedHash = 0;
            _lastBaseValue = float.NaN;
        }

        #endregion

        #region 公共 API - 泛型版本

        /// <summary>
        /// 计算修改器对基础值的影响（使用自定义 Handler）。
        /// </summary>
        /// <typeparam name="T">值类型</typeparam>
        /// <param name="modifiers">修改器数组</param>
        /// <param name="baseValue">基础值</param>
        /// <param name="handler">修改器处理器</param>
        /// <param name="context">上下文</param>
        /// <param name="sources">预分配的来源数组（传入 Span）</param>
        /// <returns>计算结果</returns>
        public ModifierResult<T> Calculate<T>(
            ReadOnlySpan<ModifierData> modifiers,
            T baseValue,
            IModifierHandler<T> handler,
            IModifierContext context = null,
            Span<ModifierSourceEntry> sources = default)
        {
            context ??= EmptyModifierContext.Default;

            int count = modifiers.Length;
            if (count == 0)
                return ModifierResult<T>.Empty(baseValue);

            T current = baseValue;
            int sourceIndex = 0;
            float overrideValue = float.NaN;
            bool hasOverride = false;

            // 第一遍：处理 Override（最高优先级）
            for (int i = 0; i < count; i++)
            {
                var mod = modifiers[i];
                if (mod.Op == ModifierOp.Override)
                {
                    hasOverride = true;
                    overrideValue = mod.GetMagnitude(context.Level, context);

                    if (!sources.IsEmpty && sourceIndex < sources.Length)
                    {
                        sources[sourceIndex++] = new ModifierSourceEntry
                        {
                            Op = mod.Op,
                            Value = overrideValue,
                            SourceId = mod.SourceId,
                            SourceNameIndex = mod.SourceNameIndex
                        };
                    }

                    // Override 直接返回
                    var result = new ModifierResult<T>
                    {
                        BaseValue = baseValue,
                        FinalValue = handler.Apply(baseValue, mod, context),
                        Count = 1
                    };
                    return result;
                }
            }

            // 第二遍：处理 Mul 和 Add/PercentAdd
            for (int i = 0; i < count; i++)
            {
                var mod = modifiers[i];
                current = handler.Apply(current, mod, context);

                if (!sources.IsEmpty && sourceIndex < sources.Length)
                {
                    float mag = mod.GetMagnitude(context.Level, context);
                    sources[sourceIndex++] = new ModifierSourceEntry
                    {
                        Op = mod.Op,
                        Value = mag,
                        SourceId = mod.SourceId,
                        SourceNameIndex = mod.SourceNameIndex
                    };
                }
            }

            return new ModifierResult<T>
            {
                BaseValue = baseValue,
                FinalValue = current,
                Count = count
            };
        }

        #endregion

        #region 核心计算逻辑

        /// <summary>
        /// 单遍遍历计算，无 GC。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ModifierResult CalculateCore(
            ReadOnlySpan<ModifierData> modifiers,
            float baseValue,
            IModifierRecorder recorder,
            float level,
            Func<ModifierKey, float> captureDelegate)
        {
            int count = modifiers.Length;
            float addSum = 0f;
            float mulProduct = 1f;
            float? overrideValue = null;
            float currentValue = baseValue;

            // 单遍遍历，检测最高优先级的 Override
            for (int i = 0; i < count; i++)
            {
                var mod = modifiers[i];
                float modValue = mod.GetMagnitude(level, new SimpleContext(level, captureDelegate));

                switch (mod.Op)
                {
                    case ModifierOp.Override:
                        overrideValue = modValue;

                        if (recorder != null)
                        {
                            recorder.Record(new ModifierSourceEntry
                            {
                                Op = mod.Op,
                                Value = modValue,
                                Contribution = modValue - baseValue,
                                SourceId = mod.SourceId,
                                SourceNameIndex = mod.SourceNameIndex
                            });
                        }

                        return new ModifierResult
                        {
                            BaseValue = baseValue,
                            OverrideValue = overrideValue,
                            Count = 1
                        };

                    case ModifierOp.Mul:
                        mulProduct *= modValue;
                        break;

                    case ModifierOp.Add:
                    case ModifierOp.PercentAdd:
                        float contrib = mod.Op == ModifierOp.Add
                            ? modValue
                            : baseValue * modValue;
                        addSum += contrib;
                        break;
                }
            }

            var result = new ModifierResult
            {
                BaseValue = baseValue,
                AddSum = addSum,
                MulProduct = mulProduct,
                Count = count
            };

            if (recorder != null)
            {
                for (int i = 0; i < count; i++)
                {
                    var mod = modifiers[i];
                    float modValue = mod.GetMagnitude(level, new SimpleContext(level, captureDelegate));

                    switch (mod.Op)
                    {
                        case ModifierOp.Mul:
                            recorder.Record(new ModifierSourceEntry
                            {
                                Op = mod.Op,
                                Value = modValue,
                                Contribution = currentValue * (modValue - 1f),
                                SourceId = mod.SourceId,
                                SourceNameIndex = mod.SourceNameIndex
                            });
                            currentValue *= modValue;
                            break;

                        case ModifierOp.Add:
                            recorder.Record(new ModifierSourceEntry
                            {
                                Op = mod.Op,
                                Value = modValue,
                                Contribution = modValue,
                                SourceId = mod.SourceId,
                                SourceNameIndex = mod.SourceNameIndex
                            });
                            currentValue += modValue;
                            break;

                        case ModifierOp.PercentAdd:
                            float pctContrib = baseValue * modValue;
                            recorder.Record(new ModifierSourceEntry
                            {
                                Op = mod.Op,
                                Value = modValue,
                                Contribution = pctContrib,
                                SourceId = mod.SourceId,
                                SourceNameIndex = mod.SourceNameIndex
                            });
                            currentValue += pctContrib;
                            break;
                    }
                }
            }

            return result;
        }

        #endregion

        #region 缓存哈希

        private static int ComputeHash(ReadOnlySpan<ModifierData> modifiers)
        {
            int len = modifiers.Length;
            if (len == 0) return 0;
            if (len == 1) return modifiers[0].GetHashCode();

            int hash = len;
            hash ^= modifiers[0].GetHashCode() << 2;
            hash ^= modifiers[len - 1].GetHashCode() << 4;
            hash ^= modifiers[len / 2].GetHashCode() << 6;
            return hash;
        }

        #endregion

        #region 调试辅助

        /// <summary>
        /// 生成调试字符串（使用默认记录器，零 GC）
        /// </summary>
        public string ToDebugString(
            ReadOnlySpan<ModifierData> modifiers,
            float baseValue,
            float level = 1f,
            Func<ModifierKey, float> captureDelegate = null)
        {
            var recorder = new DefaultRecorder(modifiers.Length);
            var result = Calculate(modifiers, baseValue, recorder, level, captureDelegate);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Base: {baseValue}, Level: {level}");
            sb.AppendLine($"Modifiers: {result.Count}");

            for (int i = 0; i < recorder.Count; i++)
            {
                ref readonly var entry = ref recorder.GetEntry(i);
                string delta = entry.Op switch
                {
                    ModifierOp.Add => $"+{entry.Value}",
                    ModifierOp.Mul => $"×{entry.Value}",
                    ModifierOp.Override => $"={entry.Value}",
                    ModifierOp.PercentAdd => $"+{entry.Value * 100}%",
                    _ => $"?{entry.Value}"
                };
                sb.AppendLine($"  {delta} (Src#{entry.SourceId})");
            }

            sb.AppendLine($"Final: {result.FinalValue}");
            return sb.ToString();
        }

        #endregion

        #region 内部辅助

        /// <summary>
        /// 简化上下文（用于不需要完整 IModifierContext 的场景）
        /// </summary>
        private readonly struct SimpleContext : IModifierContext
        {
            private readonly Func<ModifierKey, float> _captureDelegate;

            public SimpleContext(float level, Func<ModifierKey, float> captureDelegate)
            {
                Level = level;
                _captureDelegate = captureDelegate;
            }

            public float Level { get; }

            public float GetAttribute(ModifierKey key) => _captureDelegate?.Invoke(key) ?? 0f;
        }

        #endregion
    }
}
