using System;
using System.Collections.Generic;

namespace AbilityKit.Diagnostics
{
    /// <summary>
    /// 探针管理器
    /// 提供全局探针访问，支持运行时切换实现
    /// </summary>
    public static class ProfilerHub
    {
        private static IProfiler _instance;
        private static readonly object _lock = new();

        /// <summary>
        /// 当前探针实例
        /// </summary>
        public static IProfiler Current => _instance ?? NullProfiler.Instance;

        /// <summary>
        /// 是否启用
        /// </summary>
        public static bool IsEnabled => Current.IsEnabled;

        static ProfilerHub()
        {
            // 默认使用空探针
            _instance = NullProfiler.Instance;
        }

        /// <summary>
        /// 设置探针实现
        /// </summary>
        public static void SetProfiler(IProfiler profiler)
        {
            lock (_lock)
            {
                _instance = profiler ?? NullProfiler.Instance;
            }
        }

        /// <summary>
        /// 获取编辑器探针（如果需要）
        /// </summary>
        public static EditorProfiler GetEditorProfiler()
        {
            return Current as EditorProfiler;
        }

        /// <summary>
        /// 当当前 Profiler 支持开发期诊断选项时应用配置。
        /// </summary>
        public static void Configure(ProfilerOptions options)
        {
            GetEditorProfiler()?.Configure(options);
        }

        /// <summary>
        /// 注册用于治理和展示的稳定指标定义。
        /// </summary>
        public static void RegisterMetric(MetricDefinition metric)
        {
            GetEditorProfiler()?.RegisterMetric(metric);
        }

        /// <summary>
        /// 配置耗时阈值规则。
        /// </summary>
        public static void ConfigureDurationThreshold(string name, double warningMilliseconds, double errorMilliseconds = 0d)
        {
            GetEditorProfiler()?.ConfigureDurationThreshold(name, warningMilliseconds, errorMilliseconds);
        }

        /// <summary>
        /// 配置滚动频率阈值规则。
        /// </summary>
        public static void ConfigureRateThreshold(string name, long warningPerSecond, long errorPerSecond = 0L)
        {
            GetEditorProfiler()?.ConfigureRateThreshold(name, warningPerSecond, errorPerSecond);
        }

        /// <summary>
        /// 写入一条通用诊断事件。
        /// </summary>
        public static void EmitEvent(DiagnosticSeverity severity, string category, string name, string message, double value = 0d, double threshold = 0d)
        {
            GetEditorProfiler()?.EmitEvent(severity, category, name, message, value, threshold);
        }

        /// <summary>
        /// 保存当前会话的轻量摘要。
        /// </summary>
        public static DiagnosticsSessionRecord SaveSession(string label = null)
        {
            var profiler = GetEditorProfiler();
            return profiler == null ? default : profiler.SaveSession(label);
        }

        /// <summary>
        /// 开始采样
        /// </summary>
        public static ProbeToken Begin(string name) => Current.Begin(name);

        /// <summary>
        /// 开始携带关联上下文 ID 的采样（用于与 Trace / 分析工件关联）。
        /// </summary>
        public static ProbeToken Begin(string name, long contextId) => Current.Begin(name, contextId);

        /// <summary>
        /// 声明当前逻辑帧号（供时间线导出标注帧轴；仅 EditorProfiler 支持）。
        /// </summary>
        public static void SetFrame(long frame) => GetEditorProfiler()?.SetFrame(frame);

        /// <summary>
        /// 记录耗时
        /// </summary>
        public static void Record(string name, long nanoseconds) => Current.Record(name, nanoseconds);

        /// <summary>
        /// 递增计数器
        /// </summary>
        public static void Increment(string counter) => Current.Increment(counter);

        /// <summary>
        /// 添加计数器
        /// </summary>
        public static void Add(string counter, long value) => Current.Add(counter, value);

        /// <summary>
        /// 设置 Gauge 指标
        /// </summary>
        public static void SetGauge(string name, long value) => Current.SetGauge(name, value);

        /// <summary>
        /// 记录样本
        /// </summary>
        public static void Sample(string name, double value) => Current.Sample(name, value);
    }

    /// <summary>
    /// 静态采样扩展
    /// </summary>
    public static class StaticSampling
    {
        /// <summary>
        /// 使用 using 块进行采样
        /// </summary>
        public static IDisposable Sample(string name) => ProfilerHub.Begin(name).ToScope();
    }
#if UNITY_5_3_OR_NEWER
    /// <summary>
    /// 用于帧耗时和托管内存跟踪的 Unity 运行时诊断辅助工具。
    /// </summary>
    public static class UnityRuntimeDiagnostics
    {
        private static long _frameIndex;
        private static bool _metricsRegistered;

        /// <summary>
        /// 注册标准 Unity 运行时指标。
        /// </summary>
        public static void RegisterMetrics()
        {
            if (_metricsRegistered)
            {
                return;
            }

            _metricsRegistered = true;
            ProfilerHub.RegisterMetric(new MetricDefinition { Name = "unity.frame.delta_ms", Category = "unity", Kind = MetricKind.Sample, Unit = "ms", Description = "Scaled frame delta time", Tags = new[] { "unity", "frame" } });
            ProfilerHub.RegisterMetric(new MetricDefinition { Name = "unity.frame.unscaled_delta_ms", Category = "unity", Kind = MetricKind.Sample, Unit = "ms", Description = "Unscaled frame delta time", Tags = new[] { "unity", "frame" } });
            ProfilerHub.RegisterMetric(new MetricDefinition { Name = "unity.frame.index", Category = "unity", Kind = MetricKind.Gauge, Unit = "frame", Description = "Observed frame index", Tags = new[] { "unity", "frame" } });
            ProfilerHub.RegisterMetric(new MetricDefinition { Name = "unity.memory.gc_allocated_bytes", Category = "unity", Kind = MetricKind.Gauge, Unit = "bytes", Description = "Total managed memory reported by Unity profiler", Tags = new[] { "unity", "memory" } });
        }

        /// <summary>
        /// 使用 UnityEngine.Time 和 UnityEngine.Profiling.Profiler 记录标准 Unity 运行时指标。
        /// </summary>
        public static void CollectFrame()
        {
            CollectFrame(UnityEngine.Time.deltaTime, UnityEngine.Time.unscaledDeltaTime, UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong());
        }

        /// <summary>
        /// 根据外部传入的值记录标准 Unity 运行时指标。
        /// </summary>
        public static void CollectFrame(float deltaTime, float unscaledDeltaTime, long allocatedBytes)
        {
            if (!ProfilerHub.IsEnabled)
            {
                return;
            }

            RegisterMetrics();
            _frameIndex++;
            ProfilerHub.Sample("unity.frame.delta_ms", deltaTime * 1000d);
            ProfilerHub.Sample("unity.frame.unscaled_delta_ms", unscaledDeltaTime * 1000d);
            ProfilerHub.SetGauge("unity.frame.index", _frameIndex);
            if (allocatedBytes >= 0L)
            {
                ProfilerHub.SetGauge("unity.memory.gc_allocated_bytes", allocatedBytes);
            }
        }
    }
#endif
}
