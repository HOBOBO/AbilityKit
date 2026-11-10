using System;
using System.Runtime.CompilerServices;

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

        public bool IsEnabled
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ProbeToken Begin(string name) => default;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ProbeToken Begin(string name, long contextId) => default;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Complete(ProbeToken token) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Record(string name, long nanoseconds) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Increment(string counter) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(string counter, long value) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetGauge(string name, long value) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Sample(string name, double value) { }
    }
}
