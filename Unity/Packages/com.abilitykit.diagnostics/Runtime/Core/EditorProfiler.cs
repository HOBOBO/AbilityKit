using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace AbilityKit.Diagnostics
{
    /// <summary>
    /// 编辑器探针实现 - 开发阶段使用
    /// </summary>
    public sealed class EditorProfiler : IProfiler
    {
        private readonly object _lock = new();
        private readonly FlameRoot _root;
        private readonly Dictionary<string, CounterRecord> _counters = new();
        private readonly Dictionary<string, GaugeRecord> _gauges = new();
        private readonly Dictionary<string, List<double>> _samples = new();
        private readonly Stack<(string name, long startTime)> _stack = new();
        private volatile bool _isEnabled;
        private int _sessionId;

        public bool IsEnabled => _isEnabled;

        public EditorProfiler()
        {
            _root = new FlameRoot
            {
                SessionId = Guid.NewGuid().ToString("N"),
                StartTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
        }

        public void Start()
        {
            lock (_lock)
            {
                _isEnabled = true;
                _sessionId++;
                _root.SessionId = $"{_sessionId}-{DateTimeOffset.UtcNow:HHmmss}";
                _root.StartTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                Clear();
            }
        }

        public void Stop()
        {
            lock (_lock)
            {
                _isEnabled = false;
                _root.EndTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                _root.FinalizeSelfTime();
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _root.Roots.Clear();
                _counters.Clear();
                _gauges.Clear();
                _samples.Clear();
                _stack.Clear();
                _root.CurrentNode = null;
            }
        }

        public ProbeToken Begin(string name)
        {
            if (!_isEnabled)
                return default;

            lock (_lock)
            {
                var category = GetCategory(name);
                _root.Push(name, category);
                var startTime = Stopwatch.GetTimestamp();
                _stack.Push((name, startTime));
                return new ProbeToken(this, name, startTime);
            }
        }

        public void Record(string name, long nanoseconds)
        {
            if (!_isEnabled)
                return;

            lock (_lock)
            {
                var category = GetCategory(name);
                var elapsed = nanoseconds;

                // 累加到当前栈帧
                if (_stack.Count > 0)
                {
                    var parentName = _stack.Peek().name;
                    // 记录到样本中
                }

                // 更新样本数据
                if (!_samples.TryGetValue(name, out var list))
                {
                    list = new List<double>();
                    _samples[name] = list;
                }
                list.Add(nanoseconds / 1_000_000.0); // 转换为毫秒
            }
        }

        public void Increment(string counter)
        {
            if (!_isEnabled)
                return;

            lock (_lock)
            {
                if (!_counters.TryGetValue(counter, out var record))
                {
                    record = new CounterRecord { Name = counter };
                    _counters[counter] = record;
                }
                record.Value++;
                record.Delta++;
            }
        }

        public void Add(string counter, long value)
        {
            if (!_isEnabled)
                return;

            lock (_lock)
            {
                if (!_counters.TryGetValue(counter, out var record))
                {
                    record = new CounterRecord { Name = counter };
                    _counters[counter] = record;
                }
                record.Value += value;
                record.Delta += value;
            }
        }

        public void SetGauge(string name, long value)
        {
            if (!_isEnabled)
                return;

            lock (_lock)
            {
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
            if (!_isEnabled)
                return;

            lock (_lock)
            {
                if (!_samples.TryGetValue(name, out var list))
                {
                    list = new List<double>();
                    _samples[name] = list;
                }
                list.Add(value);
            }
        }

        internal void CompleteProbe(ProbeToken token)
        {
            if (!_isEnabled)
                return;

            lock (_lock)
            {
                if (_stack.Count > 0)
                {
                    var (name, startTime) = _stack.Pop();
                    var elapsed = (Stopwatch.GetTimestamp() - startTime) * 1_000_000_000 / Stopwatch.Frequency;
                    _root.Pop(elapsed);

                    // 记录样本
                    if (!_samples.TryGetValue(name, out var list))
                    {
                        list = new List<double>();
                        _samples[name] = list;
                    }
                    list.Add(elapsed / 1_000_000.0);
                }
            }
        }

        public FlameRoot GetRoot() => _root;

        public Dictionary<string, CounterRecord> GetCounters() => new(_counters);

        public Dictionary<string, GaugeRecord> GetGauges() => new(_gauges);

        public Dictionary<string, List<double>> GetSamples() => new(_samples);

        public ProfilerSnapshot GetSnapshot()
        {
            lock (_lock)
            {
                var snapshot = new ProfilerSnapshot
                {
                    SessionId = _root.SessionId,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    Root = _root,
                    Counters = new Dictionary<string, CounterRecord>(_counters),
                    Gauges = new Dictionary<string, GaugeRecord>(_gauges),
                    Samples = new Dictionary<string, List<double>>()
                };

                foreach (var kvp in _samples)
                {
                    snapshot.Samples[kvp.Key] = new List<double>(kvp.Value);
                }

                return snapshot;
            }
        }

        private static string GetCategory(string name)
        {
            var dotIndex = name.IndexOf('.');
            return dotIndex > 0 ? name.Substring(0, dotIndex) : "default";
        }
    }

    /// <summary>
    /// 探针完成扩展方法
    /// </summary>
    internal static class ProbeTokenExtensions
    {
        public static void Complete(this ProbeToken token, EditorProfiler profiler)
        {
            profiler.CompleteProbe(token);
        }
    }

    /// <summary>
    /// 探针快照
    /// </summary>
    public struct ProfilerSnapshot
    {
        public string SessionId;
        public long Timestamp;
        public FlameRoot Root;
        public Dictionary<string, CounterRecord> Counters;
        public Dictionary<string, GaugeRecord> Gauges;
        public Dictionary<string, List<double>> Samples;
    }
}
