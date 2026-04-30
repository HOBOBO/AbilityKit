using System;

namespace AbilityKit.Diagnostics
{
    /// <summary>
    /// 空探针实现 - 发布版本使用，完全零开销
    /// </summary>
    public sealed class NullProfiler : IProfiler
    {
        public static NullProfiler Instance { get; } = new();

        public bool IsEnabled => false;

        public ProbeToken Begin(string name) => default;

        public void Record(string name, long nanoseconds) { }

        public void Increment(string counter) { }

        public void Add(string counter, long value) { }

        public void SetGauge(string name, long value) { }

        public void Sample(string name, double value) { }
    }
}
