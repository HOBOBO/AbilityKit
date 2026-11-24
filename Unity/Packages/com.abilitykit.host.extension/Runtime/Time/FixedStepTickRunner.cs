using System;

namespace AbilityKit.Ability.Host.Extensions.Time
{
    /// <summary>
    /// 将宿主提供的时间换算为固定步长；由单一宿主线程驱动，回调不得重入步进或重置执行器。
    /// </summary>
    public class FixedStepTickRunner
    {
        private double _accumulator;
        private double _time;
        private long _frame;

        /// <summary>
        /// 每秒的逻辑步数。
        /// </summary>
        public int TickRate { get; }

        /// <summary>
        /// 每次回调收到的固定步长，单位为秒。
        /// </summary>
        public float FixedDeltaTime { get; }

        /// <summary>
        /// 累积宿主时间的倍率，非正值暂停按时间步进，不影响显式帧推进。
        /// </summary>
        public float SpeedMultiplier { get; set; } = 1f;

        /// <summary>
        /// 最近成功推进的帧号；执行回调时尚未计入当前帧。
        /// </summary>
        public long Frame => _frame;

        /// <summary>
        /// 最近成功推进的逻辑时间，单位为秒。
        /// </summary>
        public double Time => _time;

        /// <summary>
        /// 以指定的正数逻辑帧率创建步进器。
        /// </summary>
        public FixedStepTickRunner(int tickRate)
        {
            if (tickRate <= 0) throw new ArgumentOutOfRangeException(nameof(tickRate));
            TickRate = tickRate;
            FixedDeltaTime = 1f / tickRate;
        }

        /// <summary>
        /// 累积宿主时间并执行可用的完整步数。回调异常直接传播，只扣除已成功完成的帧；执行器不回滚回调副作用。
        /// </summary>
        /// <returns>本次成功执行的步数。</returns>
        public int Step(float elapsedSeconds, Action<float> tick)
        {
            if (tick == null) throw new ArgumentNullException(nameof(tick));
            if (elapsedSeconds <= 0f) return 0;

            var speed = SpeedMultiplier;
            if (speed <= 0f) return 0;

            _accumulator += (double)elapsedSeconds * speed;

            var dt = (double)FixedDeltaTime;
            if (dt <= 0d) return 0;

            var framesToRun = (int)Math.Floor(_accumulator / dt);
            if (framesToRun <= 0) return 0;

            var completedFrames = 0;
            try
            {
                for (; completedFrames < framesToRun; completedFrames++)
                {
                    AdvanceFrame(tick, dt);
                }
            }
            finally
            {
                _accumulator = Math.Max(0d, _accumulator - completedFrames * dt);
            }

            return completedFrames;
        }

        /// <summary>
        /// 显式推进指定帧数，不消耗累积的宿主时间；回调异常直接传播。
        /// </summary>
        public void RunFrames(int frames, Action<float> tick)
        {
            if (tick == null) throw new ArgumentNullException(nameof(tick));
            if (frames <= 0) return;

            var dt = (double)FixedDeltaTime;
            for (var index = 0; index < frames; index++)
            {
                AdvanceFrame(tick, dt);
            }
        }

        private void AdvanceFrame(Action<float> tick, double stepSeconds)
        {
            tick(FixedDeltaTime);
            _frame++;
            _time += stepSeconds;
        }

        /// <summary>
        /// 清空累计的宿主时间并重置帧号和逻辑时间；负值归零。
        /// </summary>
        public void Reset(double timeSeconds = 0d, long frame = 0)
        {
            if (timeSeconds < 0d) timeSeconds = 0d;
            if (frame < 0) frame = 0;

            _accumulator = 0d;
            _time = timeSeconds;
            _frame = frame;
        }
    }
}
