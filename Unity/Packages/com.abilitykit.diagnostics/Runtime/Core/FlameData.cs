using System;
using System.Collections.Generic;

namespace AbilityKit.Diagnostics
{
    /// <summary>
    /// 聚合后的火焰图节点。
    /// </summary>
    public sealed class FlameNode
    {
        public FlameNode(string name, string category = null, FlameNode parent = null)
        {
            Name = name ?? string.Empty;
            Category = category;
            Parent = parent;
        }

        public string Name { get; set; }
        public string Category { get; set; }
        public long TotalNanoseconds { get; set; }
        public long SelfNanoseconds { get; set; }
        public int HitCount { get; set; }
        public FlameNode Parent { get; set; }
        public Dictionary<string, FlameNode> Children { get; } = new Dictionary<string, FlameNode>();
        public long Depth => Parent == null ? 0 : Parent.Depth + 1;

        public FlameNode GetOrCreateChild(string name, string category)
        {
            name = string.IsNullOrEmpty(name) ? "unnamed" : name;
            if (!Children.TryGetValue(name, out var child))
            {
                child = new FlameNode(name, category, this);
                Children[name] = child;
            }

            return child;
        }

        public void AddMeasurement(long elapsedNanoseconds)
        {
            if (elapsedNanoseconds < 0) return;
            TotalNanoseconds += elapsedNanoseconds;
            HitCount++;
        }

        public FlameNode Clone(FlameNode parent = null)
        {
            var clone = new FlameNode(Name, Category, parent)
            {
                TotalNanoseconds = TotalNanoseconds,
                SelfNanoseconds = SelfNanoseconds,
                HitCount = HitCount
            };

            foreach (var kvp in Children)
            {
                clone.Children[kvp.Key] = kvp.Value.Clone(clone);
            }

            return clone;
        }
    }

    /// <summary>
    /// 聚合后的 Profiler 会话根。
    /// </summary>
    public sealed class FlameRoot
    {
        public string SessionId { get; set; }
        public long StartTimestamp { get; set; }
        public long EndTimestamp { get; set; }
        public Dictionary<string, FlameNode> Roots { get; } = new Dictionary<string, FlameNode>();

        public FlameNode GetOrCreateRoot(string category)
        {
            category = string.IsNullOrEmpty(category) ? "default" : category;
            if (!Roots.TryGetValue(category, out var root))
            {
                root = new FlameNode(category, category);
                Roots[category] = root;
            }

            return root;
        }

        public FlameRoot CloneSnapshot(long endTimestamp)
        {
            var clone = new FlameRoot
            {
                SessionId = SessionId,
                StartTimestamp = StartTimestamp,
                EndTimestamp = endTimestamp
            };

            foreach (var kvp in Roots)
            {
                clone.Roots[kvp.Key] = kvp.Value.Clone();
            }

            clone.FinalizeSelfTime();
            return clone;
        }

        public void FinalizeSelfTime()
        {
            foreach (var root in Roots.Values)
            {
                CalculateSelfTime(root);
            }
        }

        private static long CalculateSelfTime(FlameNode node)
        {
            long childrenTotal = 0;
            foreach (var child in node.Children.Values)
            {
                childrenTotal += CalculateSelfTime(child);
            }

            node.SelfNanoseconds = Math.Max(0L, node.TotalNanoseconds - childrenTotal);
            return node.TotalNanoseconds;
        }
    }

    /// <summary>
    /// 计数器聚合记录。
    /// </summary>
    public struct CounterRecord
    {
        public string Name;
        public long Value;
        public long Delta;
        public long MinValue;
        public long MaxValue;
        public double MeanValue;
        public long SampleCount;
    }

    /// <summary>
    /// Gauge 指标的时间点值。
    /// </summary>
    public struct GaugeRecord
    {
        public string Name;
        public long Value;
        public long Timestamp;
    }

    /// <summary>
    /// 耗时样本阈值规则，单位为毫秒。
    /// </summary>
    public struct DurationThresholdRule
    {
        /// <summary>
        /// 该规则匹配的指标名称。
        /// </summary>
        public string Name;

        /// <summary>
        /// 毫秒级警告阈值。小于等于 0 表示禁用警告检查。
        /// </summary>
        public double WarningMilliseconds;

        /// <summary>
        /// 毫秒级错误阈值。小于等于 0 表示禁用错误检查。
        /// </summary>
        public double ErrorMilliseconds;
    }

    /// <summary>
    /// 滚动计数频率阈值规则。
    /// </summary>
    public struct RateThresholdRule
    {
        /// <summary>
        /// 该规则匹配的指标名称。
        /// </summary>
        public string Name;

        /// <summary>
        /// 最近一秒窗口内观测事件数的警告阈值。
        /// </summary>
        public long WarningPerSecond;

        /// <summary>
        /// 最近一秒窗口内观测事件数的错误阈值。
        /// </summary>
        public long ErrorPerSecond;
    }

    /// <summary>
    /// 指标的滚动频率聚合记录。
    /// </summary>
    public struct RateRecord
    {
        /// <summary>
        /// 指标名称。
        /// </summary>
        public string Name;

        /// <summary>
        /// 累计计数器总值。
        /// </summary>
        public long TotalCount;

        /// <summary>
        /// 最近一秒窗口内观测到的计数器值。
        /// </summary>
        public long Count1Second;

        /// <summary>
        /// 最近五秒窗口内观测到的计数器值。
        /// </summary>
        public long Count5Seconds;

        /// <summary>
        /// 最近六十秒窗口内观测到的计数器值。
        /// </summary>
        public long Count60Seconds;

        /// <summary>
        /// 本会话内观测到的最高一秒计数器值。
        /// </summary>
        public long PeakPerSecond;

        /// <summary>
        /// 最近一次刷新的 Unix 毫秒时间戳。
        /// </summary>
        public long LastTimestamp;
    }

    /// <summary>
    /// 耗时样本摘要。
    /// </summary>
    public struct DurationSummaryRecord
    {
        /// <summary>
        /// 指标名称。
        /// </summary>
        public string Name;

        /// <summary>
        /// 耗时样本数量。
        /// </summary>
        public long Count;

        /// <summary>
        /// 耗时样本总和，单位为毫秒。
        /// </summary>
        public double SumMilliseconds;

        /// <summary>
        /// 平均耗时，单位为毫秒。
        /// </summary>
        public double MeanMilliseconds;

        /// <summary>
        /// 最小耗时，单位为毫秒。
        /// </summary>
        public double MinMilliseconds;

        /// <summary>
        /// 最大耗时，单位为毫秒。
        /// </summary>
        public double MaxMilliseconds;
    }

    /// <summary>
    /// 由阈值和突发检测器产生的通用诊断事件。
    /// </summary>
    public struct DiagnosticEventRecord
    {
        /// <summary>
        /// 事件产生时的 Unix 毫秒时间戳。
        /// </summary>
        public long Timestamp;

        /// <summary>
        /// 事件严重级别。
        /// </summary>
        public DiagnosticSeverity Severity;

        /// <summary>
        /// 逻辑指标分类。
        /// </summary>
        public string Category;

        /// <summary>
        /// 指标或事件名称。
        /// </summary>
        public string Name;

        /// <summary>
        /// 便于阅读的事件消息。
        /// </summary>
        public string Message;

        /// <summary>
        /// 触发事件的观测值。
        /// </summary>
        public double Value;

        /// <summary>
        /// 被超过的配置阈值。
        /// </summary>
        public double Threshold;
    }

    /// <summary>
    /// 诊断事件严重级别。
    /// </summary>
    public enum DiagnosticSeverity
    {
        /// <summary>
        /// 信息级事件。
        /// </summary>
        Info = 0,

        /// <summary>
        /// 警告级事件。
        /// </summary>
        Warning = 1,

        /// <summary>
        /// 错误级事件。
        /// </summary>
        Error = 2
    }

    /// <summary>
    /// 用于治理、展示和采样策略的指标类型。
    /// </summary>
    public enum MetricKind
    {
        /// <summary>
        /// 以时间单位衡量的耗时指标。
        /// </summary>
        Duration = 0,

        /// <summary>
        /// 累加型计数器指标。
        /// </summary>
        Counter = 1,

        /// <summary>
        /// 时间点 Gauge 指标。
        /// </summary>
        Gauge = 2,

        /// <summary>
        /// 数值样本指标。
        /// </summary>
        Sample = 3
    }

    /// <summary>
    /// 稳定指标定义，用于在项目之间保持名称、分类、单位和标签一致。
    /// </summary>
    public struct MetricDefinition
    {
        /// <summary>
        /// 稳定指标名称。
        /// </summary>
        public string Name;

        /// <summary>
        /// 逻辑分类，通常是指标名称的第一段。
        /// </summary>
        public string Category;

        /// <summary>
        /// 指标类型。
        /// </summary>
        public MetricKind Kind;

        /// <summary>
        /// 展示和导出的单位。
        /// </summary>
        public string Unit;

        /// <summary>
        /// 便于阅读的描述。
        /// </summary>
        public string Description;

        /// <summary>
        /// 可选的低基数标签。
        /// </summary>
        public string[] Tags;
    }

    /// <summary>
    /// 控制开销和保留策略的 Profiler 运行时选项。
    /// </summary>
    public sealed class ProfilerOptions
    {
        /// <summary>
        /// 是否允许采集诊断数据。
        /// </summary>
        public bool Enabled = true;

        /// <summary>
        /// 默认采样率，范围为 0..1。
        /// </summary>
        public double DefaultSampleRate = 1d;

        /// <summary>
        /// 每个指标最多保留的原始样本数。
        /// </summary>
        public int MaxSamplesPerMetric = 512;

        /// <summary>
        /// 最多保留的诊断事件数。
        /// </summary>
        public int MaxDiagnosticEvents = 256;

        /// <summary>
        /// 时间线 span 环形缓冲容量（按完成顺序保留最近 N 条）。0 表示禁用时间线记录。
        /// </summary>
        public int MaxSpanEvents = 16384;

        /// <summary>
        /// 已禁用的指标分类。
        /// </summary>
        public HashSet<string> DisabledCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 分类级采样率。
        /// </summary>
        public Dictionary<string, double> CategorySampleRates = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 指标级采样率。
        /// </summary>
        public Dictionary<string, double> MetricSampleRates = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 创建默认 Profiler 选项。
        /// </summary>
        public static ProfilerOptions CreateDefault()
        {
            return new ProfilerOptions();
        }

        /// <summary>
        /// 返回选项的独立副本。
        /// </summary>
        public ProfilerOptions Clone()
        {
            var clone = new ProfilerOptions
            {
                Enabled = Enabled,
                DefaultSampleRate = DefaultSampleRate,
                MaxSamplesPerMetric = MaxSamplesPerMetric,
                MaxDiagnosticEvents = MaxDiagnosticEvents,
                MaxSpanEvents = MaxSpanEvents,
                DisabledCategories = new HashSet<string>(DisabledCategories ?? new HashSet<string>(), StringComparer.OrdinalIgnoreCase),
                CategorySampleRates = new Dictionary<string, double>(CategorySampleRates ?? new Dictionary<string, double>(), StringComparer.OrdinalIgnoreCase),
                MetricSampleRates = new Dictionary<string, double>(MetricSampleRates ?? new Dictionary<string, double>(), StringComparer.OrdinalIgnoreCase)
            };

            return clone;
        }

        /// <summary>
        /// 禁用一个分类。
        /// </summary>
        public void DisableCategory(string category)
        {
            if (!string.IsNullOrEmpty(category))
            {
                DisabledCategories.Add(category);
            }
        }

        /// <summary>
        /// 启用一个分类。
        /// </summary>
        public void EnableCategory(string category)
        {
            if (!string.IsNullOrEmpty(category))
            {
                DisabledCategories.Remove(category);
            }
        }

        /// <summary>
        /// 设置分类级采样率。
        /// </summary>
        public void SetCategorySampleRate(string category, double sampleRate)
        {
            if (!string.IsNullOrEmpty(category))
            {
                CategorySampleRates[category] = ClampRate(sampleRate);
            }
        }

        /// <summary>
        /// 设置指标级采样率。
        /// </summary>
        public void SetMetricSampleRate(string metric, double sampleRate)
        {
            if (!string.IsNullOrEmpty(metric))
            {
                MetricSampleRates[metric] = ClampRate(sampleRate);
            }
        }

        /// <summary>
        /// 返回分类是否已禁用。
        /// </summary>
        public bool IsCategoryDisabled(string category)
        {
            return !string.IsNullOrEmpty(category) && DisabledCategories != null && DisabledCategories.Contains(category);
        }

        /// <summary>
        /// 解析指标的实际采样率。
        /// </summary>
        public double GetSampleRate(string category, string metric)
        {
            if (!string.IsNullOrEmpty(metric) && MetricSampleRates != null && MetricSampleRates.TryGetValue(metric, out var metricRate))
            {
                return ClampRate(metricRate);
            }

            if (!string.IsNullOrEmpty(category) && CategorySampleRates != null && CategorySampleRates.TryGetValue(category, out var categoryRate))
            {
                return ClampRate(categoryRate);
            }

            return ClampRate(DefaultSampleRate);
        }

        private static double ClampRate(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return 0d;
            }

            if (value <= 0d) return 0d;
            return value >= 1d ? 1d : value;
        }
    }

    /// <summary>
    /// Profiler 会话的轻量持久化摘要。
    /// </summary>
    public struct DiagnosticsSessionRecord
    {
        /// <summary>
        /// 会话标识。
        /// </summary>
        public string SessionId;

        /// <summary>
        /// 可选用户标签。
        /// </summary>
        public string Label;

        /// <summary>
        /// 保存时的毫秒时间戳。
        /// </summary>
        public long SavedTimestamp;

        /// <summary>
        /// 会话持续时间，单位为毫秒。
        /// </summary>
        public double DurationMilliseconds;

        /// <summary>
        /// 计数器数量。
        /// </summary>
        public int CounterCount;

        /// <summary>
        /// Gauge 数量。
        /// </summary>
        public int GaugeCount;

        /// <summary>
        /// 样本指标数量。
        /// </summary>
        public int SampleCount;

        /// <summary>
        /// 诊断事件数量。
        /// </summary>
        public int EventCount;

        /// <summary>
        /// 已注册指标数量。
        /// </summary>
        public int MetricCount;

        /// <summary>
        /// 根据快照创建会话摘要。
        /// </summary>
        public static DiagnosticsSessionRecord FromSnapshot(ProfilerSnapshot snapshot, string label)
        {
            return new DiagnosticsSessionRecord
            {
                SessionId = snapshot.SessionId,
                Label = label ?? string.Empty,
                SavedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                DurationMilliseconds = snapshot.Root == null ? 0d : Math.Max(0L, snapshot.Root.EndTimestamp - snapshot.Root.StartTimestamp),
                CounterCount = snapshot.Counters == null ? 0 : snapshot.Counters.Count,
                GaugeCount = snapshot.Gauges == null ? 0 : snapshot.Gauges.Count,
                SampleCount = snapshot.Samples == null ? 0 : snapshot.Samples.Count,
                EventCount = snapshot.Events == null ? 0 : snapshot.Events.Count,
                MetricCount = snapshot.Metrics == null ? 0 : snapshot.Metrics.Count
            };
        }
    }
}
