using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace AbilityKit.Diagnostics
{
    /// <summary>
    /// 由 <see cref="IProfiler.Begin"/> 返回的诊断探针令牌。
    /// </summary>
    public readonly struct ProbeToken
    {
        private readonly IProfiler _profiler;
        private readonly string _name;
        private readonly long _startTimestamp;
        private readonly long _contextId;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ProbeToken(IProfiler profiler, string name, long startTimestamp, long contextId = 0L)
        {
            _profiler = profiler;
            _name = name;
            _startTimestamp = startTimestamp;
            _contextId = contextId;
        }

        internal IProfiler Profiler => _profiler;
        internal string Name => _name;
        internal long StartTimestamp => _startTimestamp;
        internal long ContextId => _contextId;

        /// <summary>
        /// 完成本次探针采样。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Complete()
        {
            if (_profiler != null && _profiler.IsEnabled && _startTimestamp != 0)
            {
                _profiler.Complete(this);
            }
        }

        /// <summary>
        /// 获取令牌是否处于活动状态且可以完成。
        /// </summary>
        public bool IsValid => _profiler != null && _profiler.IsEnabled && _startTimestamp != 0;

        /// <summary>
        /// 将令牌转换为可释放的作用域。
        /// </summary>
        public ProbeScope ToScope() => new ProbeScope(this);
    }

    /// <summary>
    /// 运行时诊断 Profiler 抽象。
    /// </summary>
    public interface IProfiler
    {
        /// <summary>
        /// 获取当前 Profiler 是否正在记录诊断数据。
        /// </summary>
        bool IsEnabled { get; }

        /// <summary>
        /// 开始一次作用域探针采样。
        /// </summary>
        /// <param name="name">探针名称，建议使用点分隔格式，例如 "pipeline.execute"。</param>
        ProbeToken Begin(string name);

        /// <summary>
        /// 开始一次携带关联上下文 ID 的作用域探针采样。
        /// 上下文 ID 用于与 Trace / 分析工件中的 ContextId 关联（0 表示无关联）。
        /// </summary>
        /// <param name="name">探针名称。</param>
        /// <param name="contextId">可选的关联上下文 ID。</param>
        ProbeToken Begin(string name, long contextId);

        /// <summary>
        /// 完成由 <see cref="Begin"/> 返回的作用域探针采样。
        /// </summary>
        void Complete(ProbeToken token);

        /// <summary>
        /// 记录外部测量的纳秒级耗时。
        /// </summary>
        void Record(string name, long nanoseconds);

        /// <summary>
        /// 将计数器递增 1。
        /// </summary>
        void Increment(string counter);

        /// <summary>
        /// 向计数器累加指定值。
        /// </summary>
        void Add(string counter, long value);

        /// <summary>
        /// 设置 Gauge 指标值。
        /// </summary>
        void SetGauge(string name, long value);

        /// <summary>
        /// 记录一个数值样本。
        /// </summary>
        void Sample(string name, double value);
    }

    /// <summary>
    /// Profiler 扩展方法。
    /// </summary>
    public static class ProfilerExtensions
    {
        /// <summary>
        /// 开始一个在释放时自动完成的采样作用域。
        /// </summary>
        public static ProbeScope Sample(this IProfiler profiler, string name)
        {
            return profiler == null ? default : new ProbeScope(profiler.Begin(name));
        }
    }

    /// <summary>
    /// 可释放的探针作用域。
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
