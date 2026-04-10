using System;
using System.Runtime.CompilerServices;

namespace AbilityKit.Timer
{
    /// <summary>
    /// 系统计时器。
    /// 使用 Stopwatch 实现，精度高，适用于非 Unity 环境。
    /// </summary>
    public struct SystemTimer : ITimer
    {
        private System.Diagnostics.Stopwatch _sw;

        public float Elapsed => _sw == null ? 0f : (float)_sw.Elapsed.TotalSeconds;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset()
        {
            if (_sw == null)
                _sw = new System.Diagnostics.Stopwatch();
            _sw.Restart();
        }

        public static bool operator >(SystemTimer timer, float duration) => timer.Elapsed > duration;
        public static bool operator <(SystemTimer timer, float duration) => timer.Elapsed < duration;
        public static bool operator >=(SystemTimer timer, float duration) => timer.Elapsed >= duration;
        public static bool operator <=(SystemTimer timer, float duration) => timer.Elapsed <= duration;
        public static bool operator >(SystemTimer a, SystemTimer b) => a.Elapsed > b.Elapsed;
        public static bool operator <(SystemTimer a, SystemTimer b) => a.Elapsed < b.Elapsed;
    }

    /// <summary>
    /// 计时器辅助方法
    /// </summary>
    public static class TimerUtility
    {
        /// <summary>
        /// 创建一个已开始的计时器
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SystemTimer CreateStarted()
        {
            var timer = new SystemTimer();
            timer.Reset();
            return timer;
        }
    }
}
