using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace AbilityKit.Diagnostics
{
    /// <summary>
    /// 适用于编辑器和开发期诊断的内存型 Profiler 实现。
    /// </summary>
    public sealed class EditorProfiler : IProfiler
    {
        private struct RateSample
        {
            public long Timestamp;
            public long Value;
        }

        private sealed class ActiveFrame
        {
            public ActiveFrame(string name, string category, FlameNode node, long startTimestamp)
            {
                Name = name;
                Category = category;
                Node = node;
                StartTimestamp = startTimestamp;
            }

            public string Name { get; }
            public string Category { get; }
            public FlameNode Node { get; }
            public long StartTimestamp { get; }
            public long ChildNanoseconds { get; set; }
        }

        private const int DefaultMaxSamplesPerMetric = 512;
        private const int DefaultMaxDiagnosticEvents = 256;
        private const long EventCooldownMilliseconds = 1000L;

        private readonly object _lock = new object();
        private readonly FlameRoot _root = new FlameRoot();
        private readonly Dictionary<string, MetricDefinition> _metrics = new Dictionary<string, MetricDefinition>(128);
        private readonly Dictionary<string, long> _sampleDecisions = new Dictionary<string, long>(128);
        private readonly List<DiagnosticsSessionRecord> _sessionHistory = new List<DiagnosticsSessionRecord>(16);
        private readonly Dictionary<string, CounterRecord> _counters = new Dictionary<string, CounterRecord>(128);
        private readonly Dictionary<string, GaugeRecord> _gauges = new Dictionary<string, GaugeRecord>(128);
        private readonly Dictionary<string, List<double>> _samples = new Dictionary<string, List<double>>(128);
        private readonly Dictionary<string, DurationSummaryRecord> _durationSummaries = new Dictionary<string, DurationSummaryRecord>(128);
        private readonly Dictionary<string, Queue<RateSample>> _rateSamples = new Dictionary<string, Queue<RateSample>>(128);
        private readonly Dictionary<string, RateRecord> _rates = new Dictionary<string, RateRecord>(128);
        private readonly Dictionary<string, DurationThresholdRule> _durationThresholds = new Dictionary<string, DurationThresholdRule>(64);
        private readonly Dictionary<string, RateThresholdRule> _rateThresholds = new Dictionary<string, RateThresholdRule>(64);
        private readonly Dictionary<string, long> _eventCooldowns = new Dictionary<string, long>(128);
        private readonly Queue<DiagnosticEventRecord> _events = new Queue<DiagnosticEventRecord>(DefaultMaxDiagnosticEvents);
        private readonly Dictionary<int, Stack<ActiveFrame>> _threadStacks = new Dictionary<int, Stack<ActiveFrame>>();
        private ProfilerOptions _options = ProfilerOptions.CreateDefault();
        private volatile bool _isEnabled;
        private int _sessionIndex;

        public EditorProfiler()
        {
            ResetSessionIdentity();
        }

        public bool IsEnabled => _isEnabled;

        public void Start()
        {
            lock (_lock)
            {
                _isEnabled = true;
                _sessionIndex++;
                ClearLocked();
                _root.SessionId = $"{_sessionIndex}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
                _root.StartTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                _root.EndTimestamp = 0L;
            }
        }

        public void Stop()
        {
            lock (_lock)
            {
                _isEnabled = false;
                _root.EndTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                ClearLocked();
                _sessionHistory.Clear();
                ResetSessionIdentity();
            }
        }

        public void Configure(ProfilerOptions options)
        {
            lock (_lock)
            {
                _options = (options ?? ProfilerOptions.CreateDefault()).Clone();
            }
        }

        public ProfilerOptions GetOptions()
        {
            lock (_lock)
            {
                return _options.Clone();
            }
        }

        public void RegisterMetric(MetricDefinition metric)
        {
            if (string.IsNullOrEmpty(metric.Name))
            {
                return;
            }

            lock (_lock)
            {
                if (string.IsNullOrEmpty(metric.Category))
                {
                    metric.Category = GetCategory(metric.Name);
                }

                _metrics[metric.Name] = metric;
            }
        }

        public Dictionary<string, MetricDefinition> GetMetrics()
        {
            lock (_lock)
            {
                return new Dictionary<string, MetricDefinition>(_metrics);
            }
        }

        public List<DiagnosticsSessionRecord> GetSessionHistory()
        {
            lock (_lock)
            {
                return new List<DiagnosticsSessionRecord>(_sessionHistory);
            }
        }

        /// <summary>
        /// 为指标配置耗时阈值规则。传入非正阈值时会移除该规则。
        /// </summary>
        public void ConfigureDurationThreshold(string name, double warningMilliseconds, double errorMilliseconds = 0d)
        {
            if (string.IsNullOrEmpty(name))
            {
                return;
            }

            lock (_lock)
            {
                if (warningMilliseconds <= 0d && errorMilliseconds <= 0d)
                {
                    _durationThresholds.Remove(name);
                    return;
                }

                _durationThresholds[name] = new DurationThresholdRule
                {
                    Name = name,
                    WarningMilliseconds = Math.Max(0d, warningMilliseconds),
                    ErrorMilliseconds = Math.Max(0d, errorMilliseconds)
                };
            }
        }

        /// <summary>
        /// 为计数器指标配置一秒滚动频率阈值规则。传入非正阈值时会移除该规则。
        /// </summary>
        public void ConfigureRateThreshold(string name, long warningPerSecond, long errorPerSecond = 0L)
        {
            if (string.IsNullOrEmpty(name))
            {
                return;
            }

            lock (_lock)
            {
                if (warningPerSecond <= 0L && errorPerSecond <= 0L)
                {
                    _rateThresholds.Remove(name);
                    return;
                }

                _rateThresholds[name] = new RateThresholdRule
                {
                    Name = name,
                    WarningPerSecond = Math.Max(0L, warningPerSecond),
                    ErrorPerSecond = Math.Max(0L, errorPerSecond)
                };
            }
        }

        /// <summary>
        /// 向最近事件缓冲区写入一条诊断事件。
        /// </summary>
        public void EmitEvent(DiagnosticSeverity severity, string category, string name, string message, double value = 0d, double threshold = 0d)
        {
            if (string.IsNullOrEmpty(name))
            {
                return;
            }

            lock (_lock)
            {
                AddEventLocked(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), severity, category, name, message, value, threshold, respectCooldown: false);
            }
        }

        public ProbeToken Begin(string name)
        {
            if (!_isEnabled || string.IsNullOrEmpty(name))
            {
                return default;
            }

            var startTimestamp = Stopwatch.GetTimestamp();
            lock (_lock)
            {
                if (!ShouldRecordLocked(name, MetricKind.Duration))
                {
                    return default;
                }

                EnsureMetricLocked(name, MetricKind.Duration, "ms");
                var threadId = Thread.CurrentThread.ManagedThreadId;
                var stack = GetThreadStack(threadId);
                var category = GetMetricCategoryLocked(name);
                var parent = stack.Count > 0 ? stack.Peek().Node : _root.GetOrCreateRoot(category);
                var node = parent.GetOrCreateChild(name, category);
                stack.Push(new ActiveFrame(name, category, node, startTimestamp));
                return new ProbeToken(this, name, startTimestamp);
            }
        }

        public void Complete(ProbeToken token)
        {
            if (!_isEnabled || !token.IsValid)
            {
                return;
            }

            var nowTimestamp = Stopwatch.GetTimestamp();
            var elapsedNanoseconds = TicksToNanoseconds(nowTimestamp - token.StartTimestamp);
            lock (_lock)
            {
                var stack = GetThreadStack(Thread.CurrentThread.ManagedThreadId);
                if (stack.Count == 0)
                {
                    RecordDurationLocked(token.Name, elapsedNanoseconds, attachToCurrentStack: true);
                    return;
                }

                var top = stack.Peek();
                if (string.Equals(top.Name, token.Name, StringComparison.Ordinal))
                {
                    CompleteFrameLocked(stack, stack.Pop(), elapsedNanoseconds);
                    return;
                }

                // 乱序 Complete：向下弹出直到目标帧。目标帧上方的未配对帧按各自开始时间
                // 依次强制收尾（自顶向下），不丢弃任何帧的数据；目标帧最后收尾。
                var popped = new List<ActiveFrame>();
                ActiveFrame targetFrame = null;
                while (stack.Count > 0)
                {
                    var frame = stack.Pop();
                    popped.Add(frame);
                    if (string.Equals(frame.Name, token.Name, StringComparison.Ordinal))
                    {
                        targetFrame = frame;
                        break;
                    }
                }

                if (targetFrame == null)
                {
                    // 目标不在本线程栈上（已收尾过或跨线程）：恢复栈，本次 elapsed 记为
                    // 独立时长，绝不归属到栈顶的无关帧上。
                    for (int i = popped.Count - 1; i >= 0; i--)
                    {
                        stack.Push(popped[i]);
                    }

                    RecordDurationLocked(token.Name, elapsedNanoseconds, attachToCurrentStack: true);
                    return;
                }

                // popped = [顶帧, ..., 目标帧]（弹出顺序）。i 从 0（顶）到目标前一帧：
                // 各自收尾并把耗时累计到列表中的下一帧（即其父帧）。
                for (int i = 0; i < popped.Count - 1; i++)
                {
                    var frame = popped[i];
                    var frameElapsed = TicksToNanoseconds(nowTimestamp - frame.StartTimestamp);
                    RecordFrameMeasurementLocked(frame, frameElapsed);
                    popped[i + 1].ChildNanoseconds += frameElapsed;
                }

                CompleteFrameLocked(stack, targetFrame, elapsedNanoseconds);
            }
        }

        private void RecordFrameMeasurementLocked(ActiveFrame frame, long elapsedNanoseconds)
        {
            frame.Node.AddMeasurement(elapsedNanoseconds);
            var elapsedMilliseconds = elapsedNanoseconds / 1000000.0d;
            AddSampleLocked(frame.Name, elapsedMilliseconds);
            UpdateDurationSummaryLocked(frame.Name, elapsedMilliseconds);
            CheckDurationThresholdLocked(frame.Name, elapsedMilliseconds);

            var selfNanoseconds = elapsedNanoseconds - frame.ChildNanoseconds;
            if (selfNanoseconds > 0)
            {
                frame.Node.SelfNanoseconds += selfNanoseconds;
            }
        }

        private void CompleteFrameLocked(Stack<ActiveFrame> stack, ActiveFrame frame, long elapsedNanoseconds)
        {
            RecordFrameMeasurementLocked(frame, elapsedNanoseconds);
            if (stack.Count > 0)
            {
                stack.Peek().ChildNanoseconds += elapsedNanoseconds;
            }
        }

        public void Record(string name, long nanoseconds)
        {
            if (!_isEnabled || string.IsNullOrEmpty(name) || nanoseconds < 0)
            {
                return;
            }

            lock (_lock)
            {
                if (!ShouldRecordLocked(name, MetricKind.Duration))
                {
                    return;
                }

                EnsureMetricLocked(name, MetricKind.Duration, "ms");
                RecordDurationLocked(name, nanoseconds, attachToCurrentStack: true);
            }
        }

        public void Increment(string counter)
        {
            Add(counter, 1L);
        }

        public void Add(string counter, long value)
        {
            if (!_isEnabled || string.IsNullOrEmpty(counter) || value == 0L)
            {
                return;
            }

            lock (_lock)
            {
                if (!ShouldRecordLocked(counter, MetricKind.Counter))
                {
                    return;
                }

                EnsureMetricLocked(counter, MetricKind.Counter, "count");
                if (!_counters.TryGetValue(counter, out var record))
                {
                    record = new CounterRecord
                    {
                        Name = counter,
                        MinValue = value,
                        MaxValue = value
                    };
                }

                record.Value += value;
                record.Delta += value;
                record.SampleCount++;
                record.MinValue = record.SampleCount == 1 ? value : Math.Min(record.MinValue, record.Value);
                record.MaxValue = record.SampleCount == 1 ? value : Math.Max(record.MaxValue, record.Value);
                record.MeanValue = record.SampleCount <= 0 ? record.Value : (double)record.Value / record.SampleCount;
                _counters[counter] = record;
                UpdateRateLocked(counter, value);
            }
        }

        public void SetGauge(string name, long value)
        {
            if (!_isEnabled || string.IsNullOrEmpty(name))
            {
                return;
            }

            lock (_lock)
            {
                if (!ShouldRecordLocked(name, MetricKind.Gauge))
                {
                    return;
                }

                EnsureMetricLocked(name, MetricKind.Gauge, "value");
                _gauges[name] = new GaugeRecord
                {
                    Name = name,
                    Value = value,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };
            }
        }

        public void Sample(string name, double value)
        {
            if (!_isEnabled || string.IsNullOrEmpty(name) || double.IsNaN(value) || double.IsInfinity(value))
            {
                return;
            }

            lock (_lock)
            {
                if (!ShouldRecordLocked(name, MetricKind.Sample))
                {
                    return;
                }

                EnsureMetricLocked(name, MetricKind.Sample, "value");
                AddSampleLocked(name, value);
            }
        }

        public FlameRoot GetRoot()
        {
            lock (_lock)
            {
                return _root.CloneSnapshot(GetSnapshotEndTimestampLocked());
            }
        }

        public Dictionary<string, CounterRecord> GetCounters()
        {
            lock (_lock)
            {
                return new Dictionary<string, CounterRecord>(_counters);
            }
        }

        public Dictionary<string, GaugeRecord> GetGauges()
        {
            lock (_lock)
            {
                return new Dictionary<string, GaugeRecord>(_gauges);
            }
        }

        public Dictionary<string, List<double>> GetSamples()
        {
            lock (_lock)
            {
                return CloneSamplesLocked();
            }
        }

        /// <summary>
        /// 获取计数器指标的滚动频率记录。
        /// </summary>
        public Dictionary<string, RateRecord> GetRates()
        {
            lock (_lock)
            {
                RefreshRatesLocked(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                return new Dictionary<string, RateRecord>(_rates);
            }
        }

        /// <summary>
        /// 获取已记录探针的聚合耗时摘要。
        /// </summary>
        public Dictionary<string, DurationSummaryRecord> GetDurationSummaries()
        {
            lock (_lock)
            {
                return new Dictionary<string, DurationSummaryRecord>(_durationSummaries);
            }
        }

        /// <summary>
        /// 获取最近的诊断事件。
        /// </summary>
        public List<DiagnosticEventRecord> GetEvents()
        {
            lock (_lock)
            {
                return new List<DiagnosticEventRecord>(_events);
            }
        }

        public ProfilerSnapshot GetSnapshot()
        {
            lock (_lock)
            {
                return CreateSnapshotLocked(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            }
        }

        public DiagnosticsSessionRecord SaveSession(string label = null)
        {
            lock (_lock)
            {
                var snapshot = CreateSnapshotLocked(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                var record = DiagnosticsSessionRecord.FromSnapshot(snapshot, label);
                _sessionHistory.Add(record);
                return record;
            }
        }

        private ProfilerSnapshot CreateSnapshotLocked(long now)
        {
            RefreshRatesLocked(now);
            return new ProfilerSnapshot
            {
                SessionId = _root.SessionId,
                Timestamp = now,
                Root = _root.CloneSnapshot(GetSnapshotEndTimestampLocked()),
                Counters = new Dictionary<string, CounterRecord>(_counters),
                Gauges = new Dictionary<string, GaugeRecord>(_gauges),
                Samples = CloneSamplesLocked(),
                Rates = new Dictionary<string, RateRecord>(_rates),
                Durations = new Dictionary<string, DurationSummaryRecord>(_durationSummaries),
                Events = new List<DiagnosticEventRecord>(_events),
                Metrics = new Dictionary<string, MetricDefinition>(_metrics),
                Options = _options.Clone(),
                Sessions = new List<DiagnosticsSessionRecord>(_sessionHistory)
            };
        }

        private void RecordDurationLocked(string name, long nanoseconds, bool attachToCurrentStack)
        {
            var category = GetMetricCategoryLocked(name);
            FlameNode node;
            var stack = attachToCurrentStack ? GetThreadStack(Thread.CurrentThread.ManagedThreadId) : null;

            if (stack != null && stack.Count > 0)
            {
                node = stack.Peek().Node.GetOrCreateChild(name, category);
                stack.Peek().ChildNanoseconds += nanoseconds;
            }
            else
            {
                node = _root.GetOrCreateRoot(category).GetOrCreateChild(name, category);
            }

            var milliseconds = nanoseconds / 1000000.0d;
            node.AddMeasurement(nanoseconds);
            node.SelfNanoseconds += nanoseconds;
            AddSampleLocked(name, milliseconds);
            UpdateDurationSummaryLocked(name, milliseconds);
            CheckDurationThresholdLocked(name, milliseconds);
        }

        private Stack<ActiveFrame> GetThreadStack(int threadId)
        {
            if (!_threadStacks.TryGetValue(threadId, out var stack))
            {
                stack = new Stack<ActiveFrame>();
                _threadStacks[threadId] = stack;
            }

            return stack;
        }

        private void AddSampleLocked(string name, double value)
        {
            if (!_samples.TryGetValue(name, out var list))
            {
                list = new List<double>(16);
                _samples[name] = list;
            }

            list.Add(value);
            var maxSamples = _options == null || _options.MaxSamplesPerMetric <= 0 ? DefaultMaxSamplesPerMetric : _options.MaxSamplesPerMetric;
            if (list.Count > maxSamples)
            {
                list.RemoveRange(0, list.Count - maxSamples);
            }
        }

        private void UpdateDurationSummaryLocked(string name, double milliseconds)
        {
            if (!_durationSummaries.TryGetValue(name, out var summary))
            {
                summary = new DurationSummaryRecord
                {
                    Name = name,
                    MinMilliseconds = milliseconds,
                    MaxMilliseconds = milliseconds
                };
            }

            summary.Count++;
            summary.SumMilliseconds += milliseconds;
            summary.MeanMilliseconds = summary.SumMilliseconds / summary.Count;
            summary.MinMilliseconds = summary.Count == 1 ? milliseconds : Math.Min(summary.MinMilliseconds, milliseconds);
            summary.MaxMilliseconds = summary.Count == 1 ? milliseconds : Math.Max(summary.MaxMilliseconds, milliseconds);
            _durationSummaries[name] = summary;
        }

        private void UpdateRateLocked(string name, long value)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (!_rateSamples.TryGetValue(name, out var queue))
            {
                queue = new Queue<RateSample>(64);
                _rateSamples[name] = queue;
            }

            // Math.Abs(long.MinValue) 会抛 OverflowException，取 long.MaxValue 作为封顶。
            queue.Enqueue(new RateSample { Timestamp = now, Value = value == long.MinValue ? long.MaxValue : Math.Abs(value) });
            RefreshRateLocked(name, now);
            CheckRateThresholdLocked(name, now);
        }

        private void RefreshRatesLocked(long now)
        {
            foreach (var name in _rateSamples.Keys)
            {
                RefreshRateLocked(name, now);
            }
        }

        private void RefreshRateLocked(string name, long now)
        {
            if (!_rateSamples.TryGetValue(name, out var queue))
            {
                return;
            }

            while (queue.Count > 0 && now - queue.Peek().Timestamp > 60000L)
            {
                queue.Dequeue();
            }

            long count1Second = 0L;
            long count5Seconds = 0L;
            long count60Seconds = 0L;
            foreach (var sample in queue)
            {
                var age = now - sample.Timestamp;
                if (age <= 1000L) count1Second += sample.Value;
                if (age <= 5000L) count5Seconds += sample.Value;
                count60Seconds += sample.Value;
            }

            _rates.TryGetValue(name, out var record);
            record.Name = name;
            record.TotalCount = _counters.TryGetValue(name, out var counter) ? counter.Value : record.TotalCount;
            record.Count1Second = count1Second;
            record.Count5Seconds = count5Seconds;
            record.Count60Seconds = count60Seconds;
            record.PeakPerSecond = Math.Max(record.PeakPerSecond, count1Second);
            record.LastTimestamp = queue.Count == 0 ? record.LastTimestamp : now;
            _rates[name] = record;
        }

        private void CheckDurationThresholdLocked(string name, double milliseconds)
        {
            if (!_durationThresholds.TryGetValue(name, out var rule))
            {
                return;
            }

            var threshold = 0d;
            var severity = DiagnosticSeverity.Info;
            if (rule.ErrorMilliseconds > 0d && milliseconds >= rule.ErrorMilliseconds)
            {
                threshold = rule.ErrorMilliseconds;
                severity = DiagnosticSeverity.Error;
            }
            else if (rule.WarningMilliseconds > 0d && milliseconds >= rule.WarningMilliseconds)
            {
                threshold = rule.WarningMilliseconds;
                severity = DiagnosticSeverity.Warning;
            }

            if (threshold > 0d)
            {
                AddEventLocked(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), severity, GetCategory(name), name, $"Duration {milliseconds:F3}ms exceeded {threshold:F3}ms", milliseconds, threshold, respectCooldown: true);
            }
        }

        private void CheckRateThresholdLocked(string name, long now)
        {
            if (!_rateThresholds.TryGetValue(name, out var rule) || !_rates.TryGetValue(name, out var rate))
            {
                return;
            }

            var threshold = 0L;
            var severity = DiagnosticSeverity.Info;
            if (rule.ErrorPerSecond > 0L && rate.Count1Second >= rule.ErrorPerSecond)
            {
                threshold = rule.ErrorPerSecond;
                severity = DiagnosticSeverity.Error;
            }
            else if (rule.WarningPerSecond > 0L && rate.Count1Second >= rule.WarningPerSecond)
            {
                threshold = rule.WarningPerSecond;
                severity = DiagnosticSeverity.Warning;
            }

            if (threshold > 0L)
            {
                AddEventLocked(now, severity, GetCategory(name), name, $"Rate {rate.Count1Second}/s exceeded {threshold}/s", rate.Count1Second, threshold, respectCooldown: true);
            }
        }

        private void AddEventLocked(long now, DiagnosticSeverity severity, string category, string name, string message, double value, double threshold, bool respectCooldown)
        {
            var cooldownKey = severity + ":" + name;
            if (respectCooldown && _eventCooldowns.TryGetValue(cooldownKey, out var lastTimestamp) && now - lastTimestamp < EventCooldownMilliseconds)
            {
                return;
            }

            _eventCooldowns[cooldownKey] = now;
            var maxEvents = _options == null || _options.MaxDiagnosticEvents <= 0 ? DefaultMaxDiagnosticEvents : _options.MaxDiagnosticEvents;
            while (_events.Count >= maxEvents)
            {
                _events.Dequeue();
            }

            _events.Enqueue(new DiagnosticEventRecord
            {
                Timestamp = now,
                Severity = severity,
                Category = string.IsNullOrEmpty(category) ? GetCategory(name) : category,
                Name = name,
                Message = message ?? string.Empty,
                Value = value,
                Threshold = threshold
            });
        }

        private Dictionary<string, List<double>> CloneSamplesLocked()
        {
            var samples = new Dictionary<string, List<double>>(_samples.Count);
            foreach (var kvp in _samples)
            {
                samples[kvp.Key] = new List<double>(kvp.Value);
            }

            return samples;
        }

        private bool ShouldRecordLocked(string name, MetricKind kind)
        {
            if (_options == null || !_options.Enabled)
            {
                return false;
            }

            var category = GetMetricCategoryLocked(name);
            if (_options.IsCategoryDisabled(category))
            {
                return false;
            }

            var sampleRate = _options.GetSampleRate(category, name);
            if (sampleRate >= 1d)
            {
                return true;
            }

            if (sampleRate <= 0d)
            {
                return false;
            }

            var interval = Math.Max(1L, (long)Math.Round(1d / sampleRate));
            _sampleDecisions.TryGetValue(name, out var count);
            count++;
            _sampleDecisions[name] = count;
            return count % interval == 0L;
        }

        private void EnsureMetricLocked(string name, MetricKind kind, string unit)
        {
            if (_metrics.ContainsKey(name))
            {
                return;
            }

            _metrics[name] = new MetricDefinition
            {
                Name = name,
                Category = GetCategory(name),
                Kind = kind,
                Unit = unit,
                Description = string.Empty,
                Tags = Array.Empty<string>()
            };
        }

        private string GetMetricCategoryLocked(string name)
        {
            return _metrics.TryGetValue(name, out var metric) && !string.IsNullOrEmpty(metric.Category) ? metric.Category : GetCategory(name);
        }

        private void ClearLocked()
        {
            _root.Roots.Clear();
            _sampleDecisions.Clear();
            _counters.Clear();
            _gauges.Clear();
            _samples.Clear();
            _durationSummaries.Clear();
            _rateSamples.Clear();
            _rates.Clear();
            _eventCooldowns.Clear();
            _events.Clear();
            _threadStacks.Clear();
        }

        private void ResetSessionIdentity()
        {
            _root.SessionId = Guid.NewGuid().ToString("N");
            _root.StartTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _root.EndTimestamp = 0L;
        }

        private long GetSnapshotEndTimestampLocked()
        {
            return _root.EndTimestamp != 0L ? _root.EndTimestamp : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        // internal 供测试直接验证大 tick 数换算（真实探针要 >9.2s 才触发溢出，测试里不可行）。
        internal static long TicksToNanoseconds(long stopwatchTicks)
        {
            if (stopwatchTicks <= 0L)
            {
                return 0L;
            }

            // 直接乘 1e9 在 ticks > ~9.2e9 时中间溢出（Linux 上 Frequency==1e9，
            // 即超过 ~9.2 秒的探针会溢出成负纳秒）。拆成整秒 + 余数两段换算，
            // remainder < Frequency ≤ ~4e9，各段乘法都不会溢出。
            var seconds = stopwatchTicks / Stopwatch.Frequency;
            var remainder = stopwatchTicks % Stopwatch.Frequency;
            return seconds * 1000000000L + remainder * 1000000000L / Stopwatch.Frequency;
        }

        private static string GetCategory(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return "default";
            }

            var dotIndex = name.IndexOf('.');
            return dotIndex > 0 ? name.Substring(0, dotIndex) : "default";
        }
    }

    /// <summary>
    /// 可安全用于 UI 和导出器的 Profiler 快照。
    /// </summary>
    public struct ProfilerSnapshot
    {
        public string SessionId;
        public long Timestamp;
        public FlameRoot Root;
        public Dictionary<string, CounterRecord> Counters;
        public Dictionary<string, GaugeRecord> Gauges;
        public Dictionary<string, List<double>> Samples;
        public Dictionary<string, RateRecord> Rates;
        public Dictionary<string, DurationSummaryRecord> Durations;
        public List<DiagnosticEventRecord> Events;
        public Dictionary<string, MetricDefinition> Metrics;
        public ProfilerOptions Options;
        public List<DiagnosticsSessionRecord> Sessions;
    }
}
