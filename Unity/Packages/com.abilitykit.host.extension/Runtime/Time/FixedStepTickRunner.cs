using System;

namespace AbilityKit.Ability.Host.Extensions.Time
{
    public class FixedStepTickRunner
    {
        private double _accumulator;
        private double _time;
        private long _frame;

        public int TickRate { get; }
        public float FixedDeltaTime { get; }

        public float SpeedMultiplier { get; set; } = 1f;

        public long Frame => _frame;
        public double Time => _time;

        public FixedStepTickRunner(int tickRate)
        {
            if (tickRate <= 0) throw new ArgumentOutOfRangeException(nameof(tickRate));
            TickRate = tickRate;
            FixedDeltaTime = 1f / tickRate;
        }

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

            for (int i = 0; i < framesToRun; i++)
            {
                tick(FixedDeltaTime);
                _frame++;
                _time += dt;
            }

            _accumulator -= framesToRun * dt;
            if (_accumulator < 0d) _accumulator = 0d;

            return framesToRun;
        }

        public void RunFrames(int frames, Action<float> tick)
        {
            if (tick == null) throw new ArgumentNullException(nameof(tick));
            if (frames <= 0) return;

            var dt = (double)FixedDeltaTime;
            for (int i = 0; i < frames; i++)
            {
                tick(FixedDeltaTime);
                _frame++;
                _time += dt;
            }
        }

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
