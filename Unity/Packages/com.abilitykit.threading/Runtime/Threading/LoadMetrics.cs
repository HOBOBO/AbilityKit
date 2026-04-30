using System;
using System.Collections.Generic;
using System.Threading;

namespace AbilityKit.Threading
{
    /// <summary>
    /// 负载指标采样点
    /// </summary>
    public sealed class LoadSample
    {
        public long Timestamp { get; }
        public int ThreadCount { get; }
        public int ActiveThreadCount { get; }
        public int PendingTaskCount { get; }
        public double AverageLatencyMs { get; }
        public int HighPriorityQueueLength { get; }
        public int NormalPriorityQueueLength { get; }
        public int LowPriorityQueueLength { get; }

        public LoadSample(
            int threadCount,
            int activeThreadCount,
            int pendingTaskCount,
            double averageLatencyMs,
            int highPriorityQueueLength,
            int normalPriorityQueueLength,
            int lowPriorityQueueLength)
        {
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            ThreadCount = threadCount;
            ActiveThreadCount = activeThreadCount;
            PendingTaskCount = pendingTaskCount;
            AverageLatencyMs = averageLatencyMs;
            HighPriorityQueueLength = highPriorityQueueLength;
            NormalPriorityQueueLength = normalPriorityQueueLength;
            LowPriorityQueueLength = lowPriorityQueueLength;
        }

        public static LoadSample FromMetrics(LoadMetrics metrics, int activeThreadCount)
        {
            var queueLengths = metrics.QueueLengthByPriority;
            return new LoadSample(
                metrics.CurrentThreadCount,
                activeThreadCount,
                (int)metrics.PendingTaskCount,
                metrics.AverageLatencyMs,
                queueLengths?.Length > 2 ? queueLengths[2] : 0,
                queueLengths?.Length > 1 ? queueLengths[1] : 0,
                queueLengths?.Length > 0 ? queueLengths[0] : 0
            );
        }
    }

    /// <summary>
    /// 负载历史记录
    /// </summary>
    public sealed class LoadHistory
    {
        private readonly Queue<LoadSample> _samples;
        private readonly int _maxSamples;

        public int Count => _samples.Count;

        public LoadHistory(int maxSamples = 60)
        {
            _maxSamples = maxSamples;
            _samples = new Queue<LoadSample>(maxSamples);
        }

        public void Add(LoadSample sample)
        {
            lock (_samples)
            {
                if (_samples.Count >= _maxSamples)
                {
                    _samples.Dequeue();
                }
                _samples.Enqueue(sample);
            }
        }

        public LoadSample[] GetRecentSamples()
        {
            lock (_samples)
            {
                return _samples.ToArray();
            }
        }

        public LoadStatistics CalculateStatistics()
        {
            lock (_samples)
            {
                if (_samples.Count == 0)
                    return default;

                double totalLatency = 0;
                double maxLatency = double.MinValue;
                double minLatency = double.MaxValue;
                int totalThreads = 0;
                int maxThreads = 0;
                int totalPending = 0;
                int maxPending = 0;

                foreach (var sample in _samples)
                {
                    totalLatency += sample.AverageLatencyMs;
                    maxLatency = Math.Max(maxLatency, sample.AverageLatencyMs);
                    minLatency = Math.Min(minLatency, sample.AverageLatencyMs);
                    totalThreads += sample.ThreadCount;
                    maxThreads = Math.Max(maxThreads, sample.ThreadCount);
                    totalPending += sample.PendingTaskCount;
                    maxPending = Math.Max(maxPending, sample.PendingTaskCount);
                }

                var count = _samples.Count;
                return new LoadStatistics
                {
                    SampleCount = count,
                    AverageLatencyMs = totalLatency / count,
                    MaxLatencyMs = maxLatency,
                    MinLatencyMs = minLatency,
                    AverageThreadCount = (double)totalThreads / count,
                    MaxThreadCount = maxThreads,
                    AveragePendingTasks = (double)totalPending / count,
                    MaxPendingTasks = maxPending
                };
            }
        }
    }

    /// <summary>
    /// 负载统计数据
    /// </summary>
    public struct LoadStatistics
    {
        public int SampleCount { get; set; }
        public double AverageLatencyMs { get; set; }
        public double MaxLatencyMs { get; set; }
        public double MinLatencyMs { get; set; }
        public double AverageThreadCount { get; set; }
        public int MaxThreadCount { get; set; }
        public double AveragePendingTasks { get; set; }
        public int MaxPendingTasks { get; set; }

        public double LatencyStdDev()
        {
            return 0;
        }

        public override string ToString()
        {
            return $"Samples={SampleCount}, AvgLatency={AverageLatencyMs:F2}ms, " +
                   $"MaxLatency={MaxLatencyMs:F2}ms, AvgThreads={AverageThreadCount:F1}, " +
                   $"MaxPending={MaxPendingTasks}";
        }
    }

    /// <summary>
    /// 负载监控器
    /// 用于监控和记录线程池的负载状态
    /// </summary>
    public sealed class LoadMonitor : IDisposable
    {
        private readonly DynamicThreadPool _pool;
        private readonly LoadHistory _history;
        private readonly Timer _samplingTimer;
        private readonly int _samplingIntervalMs;
        private volatile bool _isRunning;

        /// <summary>
        /// 历史记录
        /// </summary>
        public LoadHistory History => _history;

        public LoadMonitor(DynamicThreadPool pool, int samplingIntervalMs = 1000, int historySize = 60)
        {
            _pool = pool ?? throw new ArgumentNullException(nameof(pool));
            _history = new LoadHistory(historySize);
            _samplingIntervalMs = samplingIntervalMs;
            _isRunning = true;

            _samplingTimer = new Timer(Sample, null, 0, samplingIntervalMs);
        }

        private void Sample(object state)
        {
            if (!_isRunning)
                return;

            var metrics = _pool.GetMetrics();
            var activeThreads = (int)metrics.ActiveTaskCount;
            var sample = LoadSample.FromMetrics(metrics, activeThreads);
            _history.Add(sample);
        }

        /// <summary>
        /// 获取当前状态描述
        /// </summary>
        public string GetStatusDescription()
        {
            var metrics = _pool.GetMetrics();
            var stats = _history.CalculateStatistics();

            var loadLevel = DetermineLoadLevel(metrics);

            return $"[{loadLevel}] Threads={metrics.CurrentThreadCount}, " +
                   $"Pending={metrics.PendingTaskCount}, Active={metrics.ActiveTaskCount}, " +
                   $"AvgLatency={stats.AverageLatencyMs:F2}ms";
        }

        /// <summary>
        /// 确定负载等级
        /// </summary>
        public LoadLevel DetermineLoadLevel(LoadMetrics metrics)
        {
            if (metrics.AverageLatencyMs > 500 || metrics.PendingTaskCount > metrics.CurrentThreadCount * 10)
                return LoadLevel.Overloaded;
            if (metrics.AverageLatencyMs > 100 || metrics.PendingTaskCount > metrics.CurrentThreadCount * 2)
                return LoadLevel.High;
            if (metrics.AverageLatencyMs > 50 || metrics.PendingTaskCount > metrics.CurrentThreadCount)
                return LoadLevel.Medium;
            return LoadLevel.Low;
        }

        public void Dispose()
        {
            _isRunning = false;
            _samplingTimer?.Dispose();
        }
    }

    /// <summary>
    /// 负载等级
    /// </summary>
    public enum LoadLevel
    {
        /// <summary>
        /// 低负载
        /// </summary>
        Low,

        /// <summary>
        /// 中等负载
        /// </summary>
        Medium,

        /// <summary>
        /// 高负载
        /// </summary>
        High,

        /// <summary>
        /// 过载
        /// </summary>
        Overloaded
    }

    /// <summary>
    /// 负载均衡策略
    /// </summary>
    public interface ILoadBalancingStrategy
    {
        /// <summary>
        /// 根据负载指标决定是否需要调整
        /// </summary>
        LoadAdjustment DecideAdjustment(LoadMetrics metrics, LoadHistory history);

        /// <summary>
        /// 策略名称
        /// </summary>
        string Name { get; }
    }

    /// <summary>
    /// 负载调整建议
    /// </summary>
    public struct LoadAdjustment
    {
        /// <summary>
        /// 是否需要调整
        /// </summary>
        public bool ShouldAdjust { get; }

        /// <summary>
        /// 调整的线程数量（正数增加，负数减少）
        /// </summary>
        public int ThreadDelta { get; }

        /// <summary>
        /// 调整原因
        /// </summary>
        public string Reason { get; }

        public static LoadAdjustment None => new(false, 0, null);

        public static LoadAdjustment Increase(int delta, string reason) => new(true, delta, reason);

        public static LoadAdjustment Decrease(int delta, string reason) => new(true, -delta, reason);

        private LoadAdjustment(bool shouldAdjust, int threadDelta, string reason)
        {
            ShouldAdjust = shouldAdjust;
            ThreadDelta = threadDelta;
            Reason = reason;
        }
    }

    /// <summary>
    /// 基于延迟的负载均衡策略
    /// </summary>
    public sealed class LatencyBasedStrategy : ILoadBalancingStrategy
    {
        public string Name => "LatencyBased";

        public int TargetLatencyMs { get; set; } = 100;
        public int MaxThreads { get; set; } = Environment.ProcessorCount * 2;
        public int MinThreads { get; set; } = 1;
        public int AdjustmentStep { get; set; } = 1;

        public LoadAdjustment DecideAdjustment(LoadMetrics metrics, LoadHistory history)
        {
            var avgLatency = metrics.AverageLatencyMs;
            var pending = metrics.PendingTaskCount;
            var currentThreads = metrics.CurrentThreadCount;

            // 过载 - 延迟过高且队列积压
            if (avgLatency > TargetLatencyMs * 5 && pending > currentThreads * 5)
            {
                if (currentThreads < MaxThreads)
                {
                    return LoadAdjustment.Increase(AdjustmentStep,
                        $"Severe overload: latency={avgLatency:F2}ms, pending={pending}");
                }
            }

            // 高负载 - 延迟超过目标
            if (avgLatency > TargetLatencyMs && pending > currentThreads)
            {
                if (currentThreads < MaxThreads)
                {
                    return LoadAdjustment.Increase(AdjustmentStep,
                        $"High latency: {avgLatency:F2}ms > {TargetLatencyMs}ms");
                }
            }

            // 低负载 - 延迟远低于目标且无积压
            if (avgLatency < TargetLatencyMs * 0.3 && pending == 0 && currentThreads > MinThreads)
            {
                return LoadAdjustment.Decrease(AdjustmentStep,
                    $"Low load: latency={avgLatency:F2}ms, no pending tasks");
            }

            return LoadAdjustment.None;
        }
    }

    /// <summary>
    /// 基于吞吐量的负载均衡策略
    /// </summary>
    public sealed class ThroughputBasedStrategy : ILoadBalancingStrategy
    {
        public string Name => "ThroughputBased";

        public int TargetThroughputPerSecond { get; set; } = 10000;
        public int MaxThreads { get; set; } = Environment.ProcessorCount * 2;
        public int MinThreads { get; set; } = 1;

        public LoadAdjustment DecideAdjustment(LoadMetrics metrics, LoadHistory history)
        {
            var stats = history.CalculateStatistics();
            if (stats.SampleCount == 0)
                return LoadAdjustment.None;

            var avgThroughput = stats.SampleCount / (stats.SampleCount * 1.0);
            var currentThreads = metrics.CurrentThreadCount;

            // 需要根据完成的任务数计算实际吞吐量
            // 这里简化处理

            return LoadAdjustment.None;
        }
    }
}
