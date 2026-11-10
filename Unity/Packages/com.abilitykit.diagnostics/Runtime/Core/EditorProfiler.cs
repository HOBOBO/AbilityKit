using System;
using System.Collections.Concurrent;
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
        private enum MetricOperationKind
        {
            CounterAdd,
            GaugeSet,
            DurationSummary,
            SampleValue
        }

        private struct PendingMetricOperation
        {
            public MetricOperationKind Kind;
            public string Name;
            public long Value;
            public double DoubleValue;
            public long Timestamp;
            public long Generation;
        }

        /// <summary>
        /// Immutable read-only gate used by the high-frequency counter and gauge paths.
        /// Replacing one reference keeps configuration changes atomic without taking the
        /// profiler aggregation lock for every metric write.
        /// </summary>
        private sealed class MetricGateSnapshot
        {
            public long Generation;
            public bool IsCollectionEnabled;
            public bool CollectsDetailedData;
            public HashSet<string> DisabledCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, double> CategorySampleRates = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, double> MetricSampleRates = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, string> MetricCategories = new Dictionary<string, string>(StringComparer.Ordinal);
            public double DefaultSampleRate;

            public string GetCategory(string name)
            {
                if (!string.IsNullOrEmpty(name) && MetricCategories.TryGetValue(name, out var category) && !string.IsNullOrEmpty(category))
                {
                    return category;
                }

                return EditorProfiler.GetCategory(name);
            }

            public double GetSampleRate(string category, string metric)
            {
                if (!string.IsNullOrEmpty(metric) && MetricSampleRates.TryGetValue(metric, out var metricRate))
                {
                    return metricRate;
                }

                if (!string.IsNullOrEmpty(category) && CategorySampleRates.TryGetValue(category, out var categoryRate))
                {
                    return categoryRate;
                }

                return DefaultSampleRate;
            }
        }

        private sealed class ThreadMetricBuffer
        {
            private readonly object _sync = new object();
            private PendingMetricOperation[] _operations;
            private int _head;
            private int _count;
            private long _dropped;

            public ThreadMetricBuffer(int capacity)
            {
                _operations = new PendingMetricOperation[Math.Max(1, capacity)];
            }

            public long Resize(int capacity)
            {
                capacity = Math.Max(1, capacity);
                lock (_sync)
                {
                    if (capacity == _operations.Length)
                    {
                        return 0L;
                    }

                    var keep = Math.Min(_count, capacity);
                    var skipped = _count - keep;
                    var next = new PendingMetricOperation[capacity];
                    var oldest = (_head - _count + _operations.Length) % _operations.Length;
                    for (var i = 0; i < keep; i++)
                    {
                        next[i] = _operations[(oldest + skipped + i) % _operations.Length];
                    }

                    _operations = next;
                    _count = keep;
                    _head = keep == capacity ? 0 : keep;
                    return skipped;
                }
            }

            public void Add(in PendingMetricOperation operation)
            {
                lock (_sync)
                {
                    if (_count == _operations.Length)
                    {
                        _dropped++;
                    }
                    else
                    {
                        _count++;
                    }

                    _operations[_head] = operation;
                    _head = (_head + 1) % _operations.Length;
                }
            }

            public void Reset()
            {
                lock (_sync)
                {
                    _head = 0;
                    _count = 0;
                    _dropped = 0L;
                }
            }

            public void DrainTo(Action<PendingMetricOperation> apply, ref long dropped)
            {
                lock (_sync)
                {
                    dropped += _dropped;
                    _dropped = 0L;
                    var oldest = (_head - _count + _operations.Length) % _operations.Length;
                    for (var i = 0; i < _count; i++)
                    {
                        apply(_operations[(oldest + i) % _operations.Length]);
                    }

                    _head = 0;
                    _count = 0;
                }
            }
        }

        private sealed class ThreadSpanBuffer
        {
            private readonly object _sync = new object();
            private ProfilerSpanRecord[] _records;
            private int _head;
            private int _count;
            private long _dropped;

            public ThreadSpanBuffer(int capacity)
            {
                _records = new ProfilerSpanRecord[Math.Max(1, capacity)];
            }

            public void Add(in ProfilerSpanRecord record)
            {
                lock (_sync)
                {
                    if (_count == _records.Length)
                    {
                        _dropped++;
                    }
                    else
                    {
                        _count++;
                    }

                    _records[_head] = record;
                    _head = (_head + 1) % _records.Length;
                }
            }

            public void Reset(int capacity)
            {
                lock (_sync)
                {
                    if (_records.Length != Math.Max(1, capacity))
                    {
                        _records = new ProfilerSpanRecord[Math.Max(1, capacity)];
                    }

                    _head = 0;
                    _count = 0;
                    _dropped = 0L;
                }
            }

            public void DrainTo(ProfilerSpanRecord[] destination, ref int head, ref int count, ref long dropped)
            {
                lock (_sync)
                {
                    dropped += _dropped;
                    _dropped = 0L;

                    var oldest = (_head - _count + _records.Length) % _records.Length;
                    for (var i = 0; i < _count; i++)
                    {
                        var record = _records[(oldest + i) % _records.Length];
                        if (destination.Length == 0)
                        {
                            dropped++;
                            continue;
                        }

                        if (count == destination.Length)
                        {
                            dropped++;
                        }
                        else
                        {
                            count++;
                        }

                        destination[head] = record;
                        head = (head + 1) % destination.Length;
                    }

                    _head = 0;
                    _count = 0;
                }
            }
        }

        private sealed class SampleRingBuffer
        {
            private double[] _values;
            private int _start;
            private int _count;

            public SampleRingBuffer(int capacity)
            {
                _values = new double[Math.Max(1, capacity)];
            }

            public void Add(double value)
            {
                if (_count < _values.Length)
                {
                    _values[(_start + _count) % _values.Length] = value;
                    _count++;
                    return;
                }

                _values[_start] = value;
                _start = (_start + 1) % _values.Length;
            }

            public void Resize(int capacity)
            {
                capacity = Math.Max(1, capacity);
                if (capacity == _values.Length)
                {
                    return;
                }

                var existing = ToList();
                var next = new double[capacity];
                var copyStart = Math.Max(0, existing.Count - capacity);
                var copyCount = existing.Count - copyStart;
                for (var i = 0; i < copyCount; i++)
                {
                    next[i] = existing[copyStart + i];
                }

                _values = next;
                _start = 0;
                _count = copyCount;
            }

            public List<double> ToList()
            {
                var result = new List<double>(_count);
                for (var i = 0; i < _count; i++)
                {
                    result.Add(_values[(_start + i) % _values.Length]);
                }

                return result;
            }
        }

        private struct RateSample
        {
            public long Timestamp;
            public long Value;
        }

        /// <summary>
        /// Incremental rolling-window state for a counter rate. Each queue contains
        /// samples for one window, so refreshes evict only samples that crossed a
        /// boundary instead of rescanning the complete 60-second queue.
        /// </summary>
        private sealed class RateWindowState
        {
            public readonly Queue<RateSample> Samples = new Queue<RateSample>(64);
            public readonly Queue<RateSample> FiveSecondSamples = new Queue<RateSample>(64);
            public readonly Queue<RateSample> OneSecondSamples = new Queue<RateSample>(64);
            public long Count60Seconds;
            public long Count5Seconds;
            public long Count1Second;
            public long LastTimestamp;
            public long LastRefreshTimestamp;
            public bool RequiresFullRefresh;
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
        private const int DefaultMetricBufferCapacity = 256;
        private const long EventCooldownMilliseconds = 1000L;

        private readonly object _lock = new object();
        private readonly List<ThreadMetricBuffer> _threadMetricBuffers = new List<ThreadMetricBuffer>();
        private readonly ThreadLocal<ThreadMetricBuffer> _threadMetricBuffer;
        private readonly List<ThreadSpanBuffer> _threadSpanBuffers = new List<ThreadSpanBuffer>();
        private readonly ThreadLocal<ThreadSpanBuffer> _threadSpanBuffer;
        private readonly FlameRoot _root = new FlameRoot();
        private readonly Dictionary<string, MetricDefinition> _metrics = new Dictionary<string, MetricDefinition>(128);
        private readonly ConcurrentDictionary<string, long> _sampleDecisions = new ConcurrentDictionary<string, long>(StringComparer.Ordinal);
        private readonly List<DiagnosticsSessionRecord> _sessionHistory = new List<DiagnosticsSessionRecord>(16);
        private readonly Dictionary<string, CounterRecord> _counters = new Dictionary<string, CounterRecord>(128);
        private readonly Dictionary<string, GaugeRecord> _gauges = new Dictionary<string, GaugeRecord>(128);
        private readonly Dictionary<string, SampleRingBuffer> _samples = new Dictionary<string, SampleRingBuffer>(128);
        private readonly Dictionary<string, DurationSummaryRecord> _durationSummaries = new Dictionary<string, DurationSummaryRecord>(128);
        private readonly Dictionary<string, RateWindowState> _rateSamples = new Dictionary<string, RateWindowState>(128);
        private readonly Dictionary<string, RateRecord> _rates = new Dictionary<string, RateRecord>(128);
        private readonly Dictionary<string, DurationThresholdRule> _durationThresholds = new Dictionary<string, DurationThresholdRule>(64);
        private readonly Dictionary<string, RateThresholdRule> _rateThresholds = new Dictionary<string, RateThresholdRule>(64);
        private readonly Dictionary<string, long> _eventCooldowns = new Dictionary<string, long>(128);
        private readonly Queue<DiagnosticEventRecord> _events = new Queue<DiagnosticEventRecord>(DefaultMaxDiagnosticEvents);
        private readonly Dictionary<int, Stack<ActiveFrame>> _threadStacks = new Dictionary<int, Stack<ActiveFrame>>();
        private ProfilerOptions _options = ProfilerOptions.CreateDefault();
        private volatile bool _isEnabled;
        private int _sessionIndex;
        private long _metricGeneration;
        private MetricGateSnapshot _metricGate;

        // 时间线 span 环形缓冲：按完成顺序记录 (名称, 起点, 时长, 关联上下文, 线程, 帧号)，
        // 供 chrome/speedscope 导出真实时间轴。容量由 ProfilerOptions.MaxSpanEvents 控制。
        private ProfilerSpanRecord[] _spans = Array.Empty<ProfilerSpanRecord>();
        private int _spanHead;
        private int _spanCount;
        private long _spansDropped;
        private long _currentFrame;
        private int _spanBufferCapacity;
        private long _metricOperationsDropped;

        public EditorProfiler()
        {
            _threadMetricBuffer = new ThreadLocal<ThreadMetricBuffer>(CreateThreadMetricBuffer);
            _threadSpanBuffer = new ThreadLocal<ThreadSpanBuffer>(CreateThreadSpanBuffer);
            _metricGeneration = 0L;
            _metricGate = BuildMetricGateSnapshotLocked();
            ResetSessionIdentity();
        }

        public bool IsEnabled => _isEnabled;

        public void Start()
        {
            lock (_lock)
            {
                _isEnabled = false;
                _metricGeneration++;
                _sessionIndex++;
                ClearLocked();
                var capacity = _options == null ? 0 : _options.MaxSpanEvents;
                _spans = capacity > 0 ? new ProfilerSpanRecord[capacity] : Array.Empty<ProfilerSpanRecord>();
                _spanHead = 0;
                _spanCount = 0;
                _spansDropped = 0;
                Volatile.Write(ref _currentFrame, 0L);
                _spanBufferCapacity = capacity > 0 ? Math.Max(1, Math.Min(capacity, 256)) : 0;
                for (var i = 0; i < _threadSpanBuffers.Count; i++)
                {
                    _threadSpanBuffers[i].Reset(_spanBufferCapacity);
                }
                _root.SessionId = $"{_sessionIndex}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
                _root.StartTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                _root.EndTimestamp = 0L;
                PublishMetricGateLocked();
                _isEnabled = true;
            }
        }

        /// <summary>
        /// 声明当前逻辑帧号。时间线导出按此标注 span 所属帧。
        /// </summary>
        public void SetFrame(long frame)
        {
            // Frame association is an atomic value and does not need the aggregation lock.
            Volatile.Write(ref _currentFrame, frame);
        }

        public void Stop()
        {
            lock (_lock)
            {
                // Atomically close the current metric generation before flushing. Producers
                // that raced with Stop retain the old generation and are counted as dropped.
                PublishMetricGateLocked(forceCollectionDisabled: true);
                _isEnabled = false;
                FlushMetricBuffersLocked();
                _metricGeneration++;
                PublishMetricGateLocked(forceCollectionDisabled: true);
                FlushMetricBuffersLocked();
                FlushSpanBuffersLocked();
                _root.EndTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                var wasEnabled = _isEnabled;
                PublishMetricGateLocked(forceCollectionDisabled: true);
                _metricGeneration++;
                ClearLocked();
                _sessionHistory.Clear();
                ResetSessionIdentity();
                PublishMetricGateLocked(forceCollectionDisabled: !wasEnabled);
            }
        }

        public void Configure(ProfilerOptions options)
        {
            lock (_lock)
            {
                var next = (options ?? ProfilerOptions.CreateDefault()).Clone();
                _options = next;

                // A detailed scope owns an entry in the per-thread flame stack. If collection is
                // downgraded while scopes are active, discard those entries rather than allowing
                // a later Standard session to inherit stale nesting state.
                if (!next.CollectsDetailedData)
                {
                    _threadStacks.Clear();
                }

                var maxSamples = next.MaxSamplesPerMetric <= 0 ? DefaultMaxSamplesPerMetric : next.MaxSamplesPerMetric;
                foreach (var buffer in _samples.Values)
                {
                    buffer.Resize(maxSamples);
                }

                var maxMetricOperations = next.MaxMetricOperationsPerThread <= 0 ? DefaultMetricBufferCapacity : next.MaxMetricOperationsPerThread;
                for (var i = 0; i < _threadMetricBuffers.Count; i++)
                {
                    _metricOperationsDropped += _threadMetricBuffers[i].Resize(maxMetricOperations);
                }

                PublishMetricGateLocked();
            }
        }

        /// <summary>
        /// Returns whether the current session is allowed to collect data.
        /// IsEnabled intentionally continues to represent the session lifecycle for backwards
        /// compatibility; this property represents the effective collection switch.
        /// </summary>
        public bool IsCollecting
        {
            get
            {
                if (!_isEnabled)
                {
                    return false;
                }

                lock (_lock)
                {
                    return _options != null && _options.IsCollectionEnabled;
                }
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
                PublishMetricGateLocked();
            }
        }

        public Dictionary<string, MetricDefinition> GetMetrics()
        {
            lock (_lock)
            {
                FlushMetricBuffersLocked();
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
            if (!_isEnabled || string.IsNullOrEmpty(name))
            {
                return;
            }

            var gate = Volatile.Read(ref _metricGate);
            if (gate == null || !gate.IsCollectionEnabled)
            {
                return;
            }

            lock (_lock)
            {
                if (_options == null || !_options.CollectsDetailedData)
                {
                    return;
                }

                AddEventLocked(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), severity, category, name, message, value, threshold, respectCooldown: false);
            }
        }

        public ProbeToken Begin(string name)
        {
            return Begin(name, 0L);
        }

        public ProbeToken Begin(string name, long contextId)
        {
            if (!_isEnabled || string.IsNullOrEmpty(name))
            {
                return default;
            }

            var startTimestamp = Stopwatch.GetTimestamp();
            var gate = Volatile.Read(ref _metricGate);
            if (gate == null || !gate.IsCollectionEnabled)
            {
                return default;
            }

            if (!gate.CollectsDetailedData)
            {
                if (!ShouldRecord(name, MetricKind.Duration, gate))
                {
                    return default;
                }

                return new ProbeToken(this, name, startTimestamp, contextId, hasDetailedState: false, generation: gate.Generation);
            }

            lock (_lock)
            {
                if (!ShouldRecordLocked(name, MetricKind.Duration))
                {
                    return default;
                }

                EnsureMetricLocked(name, MetricKind.Duration, "ms");
                if (!_options.CollectsDetailedData)
                {
                    return new ProbeToken(this, name, startTimestamp, contextId, hasDetailedState: false, generation: _metricGeneration);
                }

                var threadId = Thread.CurrentThread.ManagedThreadId;
                var stack = GetThreadStack(threadId);
                var category = GetMetricCategoryLocked(name);
                var parent = stack.Count > 0 ? stack.Peek().Node : _root.GetOrCreateRoot(category);
                var node = parent.GetOrCreateChild(name, category);
                stack.Push(new ActiveFrame(name, category, node, startTimestamp));
                return new ProbeToken(this, name, startTimestamp, contextId, hasDetailedState: true, generation: _metricGeneration);
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
            var appendSpan = false;
            var threadId = Thread.CurrentThread.ManagedThreadId;
            var frame = 0L;

            var gate = Volatile.Read(ref _metricGate);
            if (gate == null || !gate.IsCollectionEnabled)
            {
                return;
            }

            if (token.Generation != gate.Generation)
            {
                return;
            }

            if (!token.HasDetailedState)
            {
                EnqueueDurationSummary(token.Name, elapsedNanoseconds, token.Generation);
                return;
            }

            lock (_lock)
            {
                if (_options == null || !_options.IsCollectionEnabled || _metricGeneration != token.Generation)
                {
                    return;
                }

                if (!_options.CollectsDetailedData)
                {
                    EnqueueDurationSummary(token.Name, elapsedNanoseconds, token.Generation);
                    return;
                }

                var stack = GetThreadStack(threadId);
                if (stack.Count == 0)
                {
                    RecordDurationLocked(token.Name, elapsedNanoseconds, attachToCurrentStack: true);
                    appendSpan = true;
                    frame = Volatile.Read(ref _currentFrame);
                }
                else
                {
                    var top = stack.Peek();
                    if (string.Equals(top.Name, token.Name, StringComparison.Ordinal))
                    {
                        CompleteFrameLocked(stack, stack.Pop(), elapsedNanoseconds);
                        appendSpan = true;
                        frame = Volatile.Read(ref _currentFrame);
                    }
                    else
                    {
                        var popped = new List<ActiveFrame>();
                        ActiveFrame targetFrame = null;
                        while (stack.Count > 0)
                        {
                            var activeFrame = stack.Pop();
                            popped.Add(activeFrame);
                            if (string.Equals(activeFrame.Name, token.Name, StringComparison.Ordinal))
                            {
                                targetFrame = activeFrame;
                                break;
                            }
                        }

                        if (targetFrame == null)
                        {
                            for (var i = popped.Count - 1; i >= 0; i--)
                            {
                                stack.Push(popped[i]);
                            }

                            RecordDurationLocked(token.Name, elapsedNanoseconds, attachToCurrentStack: true);
                        }
                        else
                        {
                            for (var i = 0; i < popped.Count - 1; i++)
                            {
                                var activeFrame = popped[i];
                                var frameElapsed = TicksToNanoseconds(nowTimestamp - activeFrame.StartTimestamp);
                                RecordFrameMeasurementLocked(activeFrame, frameElapsed);
                                popped[i + 1].ChildNanoseconds += frameElapsed;
                            }

                            CompleteFrameLocked(stack, targetFrame, elapsedNanoseconds);
                        }

                        appendSpan = true;
                        frame = Volatile.Read(ref _currentFrame);
                    }
                }
            }

            if (appendSpan)
            {
                AppendSpan(token.Name, token.StartTimestamp, elapsedNanoseconds, token.ContextId, threadId, frame);
            }
        }

        private void AppendSpan(string name, long startTimestamp, long elapsedNanoseconds, long contextId, int threadId, long frame)
        {
            if (_spanBufferCapacity <= 0 || elapsedNanoseconds < 0L)
            {
                return;
            }

            var buffer = _threadSpanBuffer.Value;
            buffer.Add(new ProfilerSpanRecord
            {
                Name = name,
                StartTimestamp = startTimestamp,
                DurationNanoseconds = elapsedNanoseconds,
                ContextId = contextId,
                ThreadId = threadId,
                Frame = frame
            });
        }

        private ThreadMetricBuffer CreateThreadMetricBuffer()
        {
            lock (_lock)
            {
                var capacity = _options == null || _options.MaxMetricOperationsPerThread <= 0
                    ? DefaultMetricBufferCapacity
                    : _options.MaxMetricOperationsPerThread;
                var buffer = new ThreadMetricBuffer(capacity);
                _threadMetricBuffers.Add(buffer);
                return buffer;
            }
        }

        private void FlushMetricBuffersLocked()
        {
            for (var i = 0; i < _threadMetricBuffers.Count; i++)
            {
                _threadMetricBuffers[i].DrainTo(ApplyMetricOperationLocked, ref _metricOperationsDropped);
            }
        }

        private void ApplyMetricOperationLocked(PendingMetricOperation operation)
        {
            if (operation.Generation != _metricGeneration)
            {
                _metricOperationsDropped++;
                return;
            }

            if (operation.Kind == MetricOperationKind.CounterAdd)
            {
                ApplyCounterAddLocked(operation.Name, operation.Value, operation.Timestamp);
                return;
            }

            if (operation.Kind == MetricOperationKind.DurationSummary)
            {
                RecordDurationSummaryOnlyLocked(operation.Name, operation.Value);
                return;
            }

            if (operation.Kind == MetricOperationKind.SampleValue)
            {
                ApplySampleLocked(operation.Name, operation.DoubleValue);
                return;
            }

            ApplyGaugeSetLocked(operation.Name, operation.Value, operation.Timestamp);
        }

        private void EnqueueDurationSummary(string name, long nanoseconds, long generation)
        {
            if (nanoseconds < 0L)
            {
                return;
            }

            _threadMetricBuffer.Value.Add(new PendingMetricOperation
            {
                Kind = MetricOperationKind.DurationSummary,
                Name = name,
                Value = nanoseconds,
                Timestamp = 0L,
                Generation = generation
            });
        }

        private void ApplyCounterAddLocked(string counter, long value, long timestamp)
        {
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
            UpdateRateLocked(counter, value, timestamp);
        }

        private void ApplyGaugeSetLocked(string name, long value, long timestamp)
        {
            EnsureMetricLocked(name, MetricKind.Gauge, "value");
            _gauges[name] = new GaugeRecord
            {
                Name = name,
                Value = value,
                Timestamp = timestamp
            };
        }

        private ThreadSpanBuffer CreateThreadSpanBuffer()
        {
            var buffer = new ThreadSpanBuffer(Math.Max(1, _spanBufferCapacity));
            lock (_lock)
            {
                _threadSpanBuffers.Add(buffer);
            }

            return buffer;
        }

        private void FlushSpanBuffersLocked()
        {
            for (var i = 0; i < _threadSpanBuffers.Count; i++)
            {
                _threadSpanBuffers[i].DrainTo(_spans, ref _spanHead, ref _spanCount, ref _spansDropped);
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

            var gate = Volatile.Read(ref _metricGate);
            if (gate == null || !gate.IsCollectionEnabled)
            {
                return;
            }

            if (!gate.CollectsDetailedData)
            {
                if (ShouldRecord(name, MetricKind.Duration, gate))
                {
                    EnqueueDurationSummary(name, nanoseconds, gate.Generation);
                }

                return;
            }

            var appendSpan = false;
            var startTimestamp = 0L;
            var frame = 0L;
            var threadId = Thread.CurrentThread.ManagedThreadId;
            lock (_lock)
            {
                if (!ShouldRecordLocked(name, MetricKind.Duration))
                {
                    return;
                }

                EnsureMetricLocked(name, MetricKind.Duration, "ms");
                if (!_options.CollectsDetailedData)
                {
                    RecordDurationSummaryOnlyLocked(name, nanoseconds);
                    return;
                }

                RecordDurationLocked(name, nanoseconds, attachToCurrentStack: true);

                // ??????????????? - ????????????????
                var now = Stopwatch.GetTimestamp();
                startTimestamp = now - NanosecondsToTicks(nanoseconds);
                frame = Volatile.Read(ref _currentFrame);
                appendSpan = true;
            }

            if (appendSpan)
            {
                AppendSpan(name, startTimestamp, nanoseconds, 0L, threadId, frame);
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

            var gate = Volatile.Read(ref _metricGate);
            if (!ShouldRecord(counter, MetricKind.Counter, gate))
            {
                return;
            }

            _threadMetricBuffer.Value.Add(new PendingMetricOperation
            {
                Kind = MetricOperationKind.CounterAdd,
                Name = counter,
                Value = value,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Generation = gate.Generation
            });
        }

        public void SetGauge(string name, long value)
        {
            if (!_isEnabled || string.IsNullOrEmpty(name))
            {
                return;
            }

            var gate = Volatile.Read(ref _metricGate);
            if (!ShouldRecord(name, MetricKind.Gauge, gate))
            {
                return;
            }

            _threadMetricBuffer.Value.Add(new PendingMetricOperation
            {
                Kind = MetricOperationKind.GaugeSet,
                Name = name,
                Value = value,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Generation = gate.Generation
            });
        }

        public void Sample(string name, double value)
        {
            if (!_isEnabled || string.IsNullOrEmpty(name) || double.IsNaN(value) || double.IsInfinity(value))
            {
                return;
            }

            var gate = Volatile.Read(ref _metricGate);
            if (!ShouldRecord(name, MetricKind.Sample, gate))
            {
                return;
            }

            _threadMetricBuffer.Value.Add(new PendingMetricOperation
            {
                Kind = MetricOperationKind.SampleValue,
                Name = name,
                DoubleValue = value,
                Generation = gate.Generation
            });
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
                FlushMetricBuffersLocked();
                return new Dictionary<string, CounterRecord>(_counters);
            }
        }

        public Dictionary<string, GaugeRecord> GetGauges()
        {
            lock (_lock)
            {
                FlushMetricBuffersLocked();
                return new Dictionary<string, GaugeRecord>(_gauges);
            }
        }

        public Dictionary<string, List<double>> GetSamples()
        {
            lock (_lock)
            {
                FlushMetricBuffersLocked();
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
                FlushMetricBuffersLocked();
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
                FlushMetricBuffersLocked();
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
                FlushMetricBuffersLocked();
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
                // A saved session only stores aggregate metadata. Avoid cloning the flame
                // tree, samples and span timeline just to calculate these counts; callers
                // that need the complete payload can request GetSnapshot explicitly.
                FlushMetricBuffersLocked();
                var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                RefreshRatesLocked(now);
                var endTimestamp = GetSnapshotEndTimestampLocked();
                var record = new DiagnosticsSessionRecord
                {
                    SessionId = _root.SessionId,
                    Label = label ?? string.Empty,
                    SavedTimestamp = now,
                    DurationMilliseconds = Math.Max(0L, endTimestamp - _root.StartTimestamp),
                    CounterCount = _counters.Count,
                    GaugeCount = _gauges.Count,
                    SampleCount = _samples.Count,
                    EventCount = _events.Count,
                    MetricCount = _metrics.Count
                };
                _sessionHistory.Add(record);
                return record;
            }
        }

        private ProfilerSnapshot CreateSnapshotLocked(long now)
        {
            FlushMetricBuffersLocked();
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
                Sessions = new List<DiagnosticsSessionRecord>(_sessionHistory),
                Spans = CloneSpansLocked(),
                SpansDropped = _spansDropped,
                MetricOperationsDropped = _metricOperationsDropped
            };
        }

        private List<ProfilerSpanRecord> CloneSpansLocked()
        {
            FlushSpanBuffersLocked();
            if (_spanCount == 0)
            {
                return new List<ProfilerSpanRecord>();
            }

            var result = new List<ProfilerSpanRecord>(_spanCount);
            var oldest = (_spanHead - _spanCount + _spans.Length) % _spans.Length;
            for (var i = 0; i < _spanCount; i++)
            {
                result.Add(_spans[(oldest + i) % _spans.Length]);
            }

            return result;
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

        private void RecordDurationSummaryOnlyLocked(string name, long nanoseconds)
        {
            if (nanoseconds < 0L)
            {
                return;
            }

            EnsureMetricLocked(name, MetricKind.Duration, "ms");
            UpdateDurationSummaryLocked(name, nanoseconds / 1000000.0d);
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
            if (!_samples.TryGetValue(name, out var buffer))
            {
                var maxSamples = _options == null || _options.MaxSamplesPerMetric <= 0 ? DefaultMaxSamplesPerMetric : _options.MaxSamplesPerMetric;
                buffer = new SampleRingBuffer(maxSamples);
                _samples[name] = buffer;
            }

            buffer.Add(value);
        }

        private void ApplySampleLocked(string name, double value)
        {
            EnsureMetricLocked(name, MetricKind.Sample, "value");
            AddSampleLocked(name, value);
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
            UpdateRateLocked(name, value, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }

        private void UpdateRateLocked(string name, long value, long timestamp)
        {
            if (!_rateSamples.TryGetValue(name, out var state))
            {
                state = new RateWindowState();
                _rateSamples[name] = state;
            }

            // Math.Abs(long.MinValue) 会抛 OverflowException，取 long.MaxValue 作为封顶。
            var sample = new RateSample { Timestamp = timestamp, Value = value == long.MinValue ? long.MaxValue : Math.Abs(value) };
            if (state.Samples.Count > 0 && timestamp < state.LastTimestamp)
            {
                // Different thread-local buffers can be drained in an order that does
                // not match their wall-clock timestamps. Defer to a one-time rebuild
                // so the incremental queues never hide an expired sample behind a newer one.
                state.RequiresFullRefresh = true;
            }

            state.LastTimestamp = Math.Max(state.LastTimestamp, timestamp);
            state.Samples.Enqueue(sample);
            state.FiveSecondSamples.Enqueue(sample);
            state.OneSecondSamples.Enqueue(sample);
            state.Count60Seconds += sample.Value;
            state.Count5Seconds += sample.Value;
            state.Count1Second += sample.Value;
            var now = timestamp;
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
            if (!_rateSamples.TryGetValue(name, out var state))
            {
                return;
            }

            if (now < state.LastRefreshTimestamp)
            {
                state.RequiresFullRefresh = true;
            }

            if (state.RequiresFullRefresh)
            {
                RebuildRateWindowsLocked(state, now);
            }
            else
            {
                ExpireRateWindowsLocked(state, now);
            }

            state.LastRefreshTimestamp = now;

            _rates.TryGetValue(name, out var record);
            record.Name = name;
            record.TotalCount = _counters.TryGetValue(name, out var counter) ? counter.Value : record.TotalCount;
            record.Count1Second = state.Count1Second;
            record.Count5Seconds = state.Count5Seconds;
            record.Count60Seconds = state.Count60Seconds;
            record.PeakPerSecond = Math.Max(record.PeakPerSecond, state.Count1Second);
            record.LastTimestamp = state.Samples.Count == 0 ? record.LastTimestamp : now;
            _rates[name] = record;
        }

        private static void ExpireRateWindowsLocked(RateWindowState state, long now)
        {

            while (state.Samples.Count > 0 && now - state.Samples.Peek().Timestamp > 60000L)
            {
                state.Count60Seconds -= state.Samples.Dequeue().Value;
            }

            while (state.FiveSecondSamples.Count > 0 && now - state.FiveSecondSamples.Peek().Timestamp > 5000L)
            {
                state.Count5Seconds -= state.FiveSecondSamples.Dequeue().Value;
            }

            while (state.OneSecondSamples.Count > 0 && now - state.OneSecondSamples.Peek().Timestamp > 1000L)
            {
                state.Count1Second -= state.OneSecondSamples.Dequeue().Value;
            }
        }

        private static void RebuildRateWindowsLocked(RateWindowState state, long now)
        {
            var retained = new List<RateSample>(state.Samples.Count);
            foreach (var sample in state.Samples)
            {
                if (now - sample.Timestamp <= 60000L)
                {
                    retained.Add(sample);
                }
            }

            retained.Sort((left, right) => left.Timestamp.CompareTo(right.Timestamp));
            state.Samples.Clear();
            state.FiveSecondSamples.Clear();
            state.OneSecondSamples.Clear();
            state.Count60Seconds = 0L;
            state.Count5Seconds = 0L;
            state.Count1Second = 0L;
            state.LastTimestamp = 0L;

            foreach (var sample in retained)
            {
                state.Samples.Enqueue(sample);
                state.Count60Seconds += sample.Value;
                state.LastTimestamp = Math.Max(state.LastTimestamp, sample.Timestamp);

                if (now - sample.Timestamp <= 5000L)
                {
                    state.FiveSecondSamples.Enqueue(sample);
                    state.Count5Seconds += sample.Value;
                }

                if (now - sample.Timestamp <= 1000L)
                {
                    state.OneSecondSamples.Enqueue(sample);
                    state.Count1Second += sample.Value;
                }
            }

            state.RequiresFullRefresh = false;
        }

        private void CheckDurationThresholdLocked(string name, double milliseconds)
        {
            if (_options == null || !_options.CollectsDetailedData)
            {
                return;
            }

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
            if (_options == null || !_options.CollectsDetailedData)
            {
                return;
            }

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
                samples[kvp.Key] = kvp.Value.ToList();
            }

            return samples;
        }

        private bool ShouldRecord(string name, MetricKind kind, MetricGateSnapshot gate)
        {
            if (gate == null || !gate.IsCollectionEnabled)
            {
                return false;
            }

            if (!gate.CollectsDetailedData && kind == MetricKind.Sample)
            {
                return false;
            }

            var category = gate.GetCategory(name);
            if (gate.DisabledCategories.Contains(category))
            {
                return false;
            }

            var sampleRate = gate.GetSampleRate(category, name);
            if (sampleRate >= 1d)
            {
                return true;
            }

            if (sampleRate <= 0d)
            {
                return false;
            }

            var interval = Math.Max(1L, (long)Math.Round(1d / sampleRate));
            var count = _sampleDecisions.AddOrUpdate(
                name,
                1L,
                (_, previous) => previous == long.MaxValue ? 1L : previous + 1L);
            return count % interval == 0L;
        }

        private bool ShouldRecordLocked(string name, MetricKind kind)
        {
            if (_options == null || !_options.IsCollectionEnabled)
            {
                return false;
            }

            // Summary mode intentionally excludes raw samples. This keeps the low-cost
            // production path bounded while duration summaries remain available through
            // Begin/Complete and Record.
            if (_options.CollectionLevel == ProfilerCollectionLevel.Summary && kind == MetricKind.Sample)
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

        private MetricGateSnapshot BuildMetricGateSnapshotLocked(bool forceCollectionDisabled = false)
        {
            var options = _options ?? ProfilerOptions.CreateDefault();
            var disabledCategories = new HashSet<string>(options.DisabledCategories ?? new HashSet<string>(), StringComparer.OrdinalIgnoreCase);
            var categoryRates = new Dictionary<string, double>(options.CategorySampleRates ?? new Dictionary<string, double>(), StringComparer.OrdinalIgnoreCase);
            var metricRates = new Dictionary<string, double>(options.MetricSampleRates ?? new Dictionary<string, double>(), StringComparer.OrdinalIgnoreCase);
            var metricCategories = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var metric in _metrics)
            {
                metricCategories[metric.Key] = string.IsNullOrEmpty(metric.Value.Category) ? GetCategory(metric.Key) : metric.Value.Category;
            }

            return new MetricGateSnapshot
            {
                Generation = _metricGeneration,
                IsCollectionEnabled = !forceCollectionDisabled && options.IsCollectionEnabled,
                CollectsDetailedData = !forceCollectionDisabled && options.CollectsDetailedData,
                DisabledCategories = disabledCategories,
                CategorySampleRates = categoryRates,
                MetricSampleRates = metricRates,
                MetricCategories = metricCategories,
                DefaultSampleRate = ClampSampleRate(options.DefaultSampleRate)
            };
        }

        private void PublishMetricGateLocked(bool forceCollectionDisabled = false)
        {
            Volatile.Write(ref _metricGate, BuildMetricGateSnapshotLocked(forceCollectionDisabled));
        }

        private static double ClampSampleRate(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0d)
            {
                return 0d;
            }

            return value >= 1d ? 1d : value;
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
            _metricOperationsDropped = 0L;
            for (var i = 0; i < _threadMetricBuffers.Count; i++)
            {
                _threadMetricBuffers[i].Reset();
            }
            for (var i = 0; i < _threadSpanBuffers.Count; i++)
            {
                _threadSpanBuffers[i].Reset(_spanBufferCapacity);
            }
            _spanHead = 0;
            _spanCount = 0;
            _spansDropped = 0;
            Volatile.Write(ref _currentFrame, 0L);
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

        private static long NanosecondsToTicks(long nanoseconds)
        {
            if (nanoseconds <= 0L)
            {
                return 0L;
            }

            var seconds = nanoseconds / 1000000000L;
            var remainder = nanoseconds % 1000000000L;
            return seconds * Stopwatch.Frequency + remainder * Stopwatch.Frequency / 1000000000L;
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

        /// <summary>时间线 span 记录（按发生顺序，最旧在前）。空列表表示无时间线数据。</summary>
        public List<ProfilerSpanRecord> Spans;

        /// <summary>环形缓冲覆盖（丢弃）的 span 数量。非零表示时间线不完整。</summary>
        public long SpansDropped;

        /// <summary>线程本地指标缓冲区因容量不足而丢弃的操作数量。</summary>
        public long MetricOperationsDropped;
    }

    /// <summary>
    /// 一次探针完成事件的时间线记录：名称、Stopwatch 起点、时长、关联上下文、线程与逻辑帧。
    /// </summary>
    public struct ProfilerSpanRecord
    {
        public string Name;
        public long StartTimestamp;
        public long DurationNanoseconds;
        public long ContextId;
        public int ThreadId;
        public long Frame;
    }
}
