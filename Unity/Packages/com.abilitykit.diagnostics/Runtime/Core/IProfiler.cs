using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace AbilityKit.Diagnostics
{
    /// <summary>
    /// 诊断探针令牌
    /// </summary>
    public readonly struct ProbeToken
    {
        private readonly IProfiler _profiler;
        private readonly string _name;
        private readonly long _startTimestamp;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ProbeToken(IProfiler profiler, string name, long startTimestamp)
        {
            _profiler = profiler;
            _name = name;
            _startTimestamp = startTimestamp;
        }

        /// <summary>
        /// 完成探针采样
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Complete()
        {
            if (_profiler != null && _profiler.IsEnabled && _startTimestamp != 0)
            {
                var elapsed = (Stopwatch.GetTimestamp() - _startTimestamp) * 1_000_000_000 / Stopwatch.Frequency;
                _profiler.Record(_name, elapsed);
            }
        }

        /// <summary>
        /// 是否有效
        /// </summary>
        public bool IsValid => _profiler != null && _profiler.IsEnabled && _startTimestamp != 0;

        /// <summary>
        /// 转换为作用域
        /// </summary>
        public ProbeScope ToScope() => new ProbeScope(this);
    }

    /// <summary>
    /// 性能探针接口
    /// </summary>
    public interface IProfiler
    {
        /// <summary>
        /// 是否启用
        /// </summary>
        bool IsEnabled { get; }

        /// <summary>
        /// 开始一个探针采样
        /// </summary>
        /// <param name="name">探针名称，使用点分隔如 "pipeline.execute"</param>
        /// <returns>探针令牌，需在 using 块结束时调用 Complete</returns>
        ProbeToken Begin(string name);

        /// <summary>
        /// 记录耗时（纳秒）
        /// </summary>
        void Record(string name, long nanoseconds);

        /// <summary>
        /// 递增计数器
        /// </summary>
        void Increment(string counter);

        /// <summary>
        /// 递增计数器（指定增量）
        /// </summary>
        void Add(string counter, long value);

        /// <summary>
        /// 设置 gauge 值
        /// </summary>
        void SetGauge(string name, long value);

        /// <summary>
        /// 记录样本值（用于采样统计）
        /// </summary>
        void Sample(string name, double value);
    }

    /// <summary>
    /// 探针扩展方法
    /// </summary>
    public static class ProfilerExtensions
    {
        /// <summary>
        /// 开始采样并自动完成
        /// </summary>
        public static ProbeScope Sample(this IProfiler profiler, string name)
        {
            return new ProbeScope(profiler.Begin(name));
        }
    }

    /// <summary>
    /// 采样作用域
    /// </summary>
    public readonly struct ProbeScope : IDisposable
    {
        private readonly ProbeToken _token;

        public ProbeScope(ProbeToken token)
        {
            _token = token;
        }

        public void Dispose()
        {
            _token.Complete();
        }
    }
}
