using System;
using System.Runtime.CompilerServices;

namespace AbilityKit.Modifiers
{
    // ============================================================================
    // 修饰器计算器
    // ============================================================================

    /// <summary>
    /// 通用修改器计算器。
    /// 支持操作注册表扩展。
    ///
    /// 核心职责：
    /// - 将修改器数组应用到基础值上，产生 ModifierResult
    /// - 支持来源追踪（零堆分配）
    /// - 支持缓存（基于修改器数量检测变化）
    ///
    /// 设计原则：
    /// - 无 GC：所有方法返回值均为值类型
    /// - 缓存本地化：计算器本身无状态
    /// - 可扩展：通过操作注册表支持自定义操作
    /// - 职责分离：使用 ModifierCacheAndCore 进行计算
    /// </summary>
    public sealed class ModifierCalculator
    {
        #region 字段

        private ModifierCache _cache;
        private readonly ModifierComputeCore _computeCore;
        private bool _enableCache = true;

        #endregion

        #region 属性

        /// <summary>是否启用缓存</summary>
        public bool EnableCache
        {
            get => _enableCache;
            set => _enableCache = value;
        }

        #endregion

        #region 构造函数

        /// <summary>创建计算器</summary>
        public ModifierCalculator() { }

        #endregion

        #region 公共 API - 经典版本

        /// <summary>
        /// 计算修改器对基础值的影响（不追踪来源）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ModifierResult Calculate(ReadOnlySpan<ModifierData> modifiers, float baseValue)
        {
            return Calculate(modifiers, baseValue, null, level: 1f, null);
        }

        /// <summary>
        /// 计算修改器对基础值的影响（不追踪来源，指定等级）
        /// </summary>
        public ModifierResult Calculate(
            ReadOnlySpan<ModifierData> modifiers,
            float baseValue,
            float level)
        {
            return Calculate(modifiers, baseValue, null, level, null);
        }

        /// <summary>
        /// 计算修改器对基础值的影响（追踪来源）
        /// </summary>
        public ModifierResult Calculate(
            ReadOnlySpan<ModifierData> modifiers,
            float baseValue,
            IModifierRecorder recorder)
        {
            return Calculate(modifiers, baseValue, recorder, level: 1f, null);
        }

        /// <summary>
        /// 计算单个标签的最终值（简化版本）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float CalculateFinal(ReadOnlySpan<ModifierData> modifiers, float baseValue)
            => Calculate(modifiers, baseValue).FinalValue;

        #endregion

        #region 公共 API - 完整版本

        /// <summary>
        /// 计算修改器对基础值的影响（完整版本）
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

            // 创建上下文
            var context = new SimpleModifierContext(level, captureDelegate);

            // 尝试从缓存获取
            if (_enableCache && (recorder == null || recorder is NullRecorder))
            {
                if (_cache.TryGet(modifiers, baseValue, context, out var cachedResult))
                {
                    return cachedResult;
                }
            }

            // 执行计算
            var result = _computeCore.Compute(modifiers, baseValue, level, context, recorder);

            // 更新缓存
            if (_enableCache && (recorder == null || recorder is NullRecorder))
            {
                _cache = _cache.Store(modifiers, baseValue, context, result);
            }

            return result;
        }

        /// <summary>
        /// 批量计算多个基础值的修改结果
        /// </summary>
        public void CalculateBatch(
            ReadOnlySpan<ModifierData> modifiers,
            ReadOnlySpan<float> bases,
            float level,
            IModifierContext context,
            Span<ModifierResult> results)
        {
            for (int i = 0; i < bases.Length; i++)
            {
                results[i] = Calculate(modifiers, bases[i], null, level,
                    context != null ? key => context.GetAttribute(key) : null);
            }
        }

        #endregion

        #region 缓存控制

        /// <summary>
        /// 手动清空缓存
        /// </summary>
        public void ClearCache()
        {
            _cache = default;
        }

        /// <summary>
        /// 使缓存失效（强制重新计算）
        /// </summary>
        public void Invalidate()
        {
            ClearCache();
        }

        #endregion
    }

    // ============================================================================
    // 简单上下文
    // ============================================================================

    /// <summary>
    /// 简单修改器上下文。
    /// 用于不需要完整上下文的场景。
    /// </summary>
    internal readonly struct SimpleModifierContext : IModifierContext
    {
        private readonly Func<ModifierKey, float> _captureDelegate;
        private readonly float _level;

        public float Level => _level;
        public float CurrentTime => 0f;
        public float DeltaTime => 0f;
        public float ElapsedTime => 0f;
        public int CurrentFrame => 0;
        public int ElapsedFrames => 0;
        public int CurrentTimeMs => 0;
        public int DeltaTimeMs => 0;
        public int ElapsedTimeMs => 0;
        public ModifierMetadata Metadata => default;

        public SimpleModifierContext(float level, Func<ModifierKey, float> captureDelegate)
        {
            _level = level;
            _captureDelegate = captureDelegate;
        }

        public float GetAttribute(ModifierKey key)
            => _captureDelegate?.Invoke(key) ?? 0f;

        public T GetData<T>(string key) where T : class => null;
        public bool TryGetData<T>(string key, out T value) where T : class
        {
            value = null;
            return false;
        }

        public float GetFloat(string key) => 0f;
        public bool TryGetFloat(string key, out float value)
        {
            value = 0f;
            return false;
        }

        public int GetInt(string key) => 0;
        public bool TryGetInt(string key, out int value)
        {
            value = 0;
            return false;
        }

        public float[] GetCurveData(int index) => null;
    }
}