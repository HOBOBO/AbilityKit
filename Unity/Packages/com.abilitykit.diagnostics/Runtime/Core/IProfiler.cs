#nullable enable

using System;
using System.Runtime.CompilerServices;

namespace AbilityKit.Diagnostics
{
    /// <summary>
    /// 由 <see cref="IProfiler.Begin"/> 返回的诊断探针令牌。
    /// </summary>
    public readonly struct ProbeToken
    {
        private readonly EditorProfiler? _profiler;
        private readonly ProfilerScopeTracker.Capture? _capture;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ProbeToken(EditorProfiler profiler, ProfilerScopeTracker.Capture capture)
        {
            _profiler = profiler;
            _capture = capture;
        }

        internal ProfilerScopeTracker.Capture? GetCapture(EditorProfiler owner) =>
            ReferenceEquals(_profiler, owner) ? _capture : null;

        /// <summary>
        /// 完成本次探针采样。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Complete() => _profiler?.Complete(this);

        /// <summary>
        /// 获取采样是否尚未结束，且所属会话当前允许完成采样。
        /// 这是瞬时检查，不会预留完成权。
        /// </summary>
        public bool IsValid => _profiler != null && _capture != null && _capture.IsPending
            && _profiler.CanCompleteProbe(_capture.Generation);

        /// <summary>
        /// 将令牌转换为可释放的作用域；释放任一副本都会结束同一次采样。
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
        /// 完成由本实例开始的探针采样；默认、已完成或过期的令牌不产生数据。
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

        /// <summary>
        /// 包装现有令牌，不创建新的采样。
        /// </summary>
        public ProbeScope(ProbeToken token)
        {
            _token = token;
        }

        /// <summary>
        /// 结束采样。重复释放同一令牌或其副本不会产生额外采样。
        /// </summary>
        public void Dispose()
        {
            _token.Complete();
        }
    }
}
