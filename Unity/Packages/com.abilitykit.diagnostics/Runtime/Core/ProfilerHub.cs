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
        /// 开始采样
        /// </summary>
        public static ProbeToken Begin(string name) => Current.Begin(name);

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
        /// 设置 Gauge
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
}
