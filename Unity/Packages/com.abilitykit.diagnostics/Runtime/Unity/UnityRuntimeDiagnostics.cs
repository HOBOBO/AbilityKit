using UnityEngine;
using UnityEngine.Profiling;

namespace AbilityKit.Diagnostics
{
    /// <summary>
    /// 将帧耗时和托管内存采样写入当前诊断探针，由 Unity 主线程的宿主显式调用。
    /// </summary>
    public static class UnityRuntimeDiagnostics
    {
        private static long _frameIndex;
        private static bool _metricsRegistered;

        /// <summary>
        /// 向当前探针注册标准 Unity 指标定义；同一域内只尝试一次，不随探针更换自动重注册。
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
        /// 读取 Unity 的帧时间和内存统计，并在探针启用时记录到当前会话。
        /// </summary>
        public static void CollectFrame()
        {
            CollectFrame(Time.deltaTime, Time.unscaledDeltaTime, Profiler.GetTotalAllocatedMemoryLong());
        }

        /// <summary>
        /// 记录宿主提供的一帧数据；探针未启用时不写入，也不递增采样序号。
        /// </summary>
        /// <param name="deltaTime">缩放后的帧间隔，单位为秒。</param>
        /// <param name="unscaledDeltaTime">未缩放的帧间隔，单位为秒。</param>
        /// <param name="allocatedBytes">已分配内存的字节数；负值不写入内存指标。</param>
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
}
