using System;

namespace AbilityKit.Diagnostics
{
    /// <summary>
    /// 诊断关闭时使用的空操作 Profiler。
    /// </summary>
    public sealed class NullProfiler : IProfiler
    {
        public static NullProfiler Instance { get; } = new NullProfiler();

        private NullProfiler()
        {
        }

        public bool IsEnabled => false;

        public ProbeToken Begin(string name) => default;

        public ProbeToken Begin(string name, long contextId) => default;

        public void Complete(ProbeToken token) { }

        public void Record(string name, long nanoseconds) { }

        public void Increment(string counter) { }

        public void Add(string counter, long value) { }

        public void SetGauge(string name, long value) { }

        public void Sample(string name, double value) { }
    }
}
