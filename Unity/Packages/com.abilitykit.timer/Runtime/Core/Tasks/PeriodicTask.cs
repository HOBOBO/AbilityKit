using System;
using AbilityKit.Core.Mathematics;
using AbilityKit.Deterministic;

namespace AbilityKit.Timer
{
    /// <summary>
    /// 周期任务。
    /// 以固定间隔重复执行回调。
    ///
    /// 确定性说明：内部用 Fixed64 raw 累加（对外 float 无感）。
    /// duration 语义：duration 是总时长截止（-1 表示无限）；触发时刻为 period 的整数倍且
    /// ≤ duration，到 duration 后完成。ElapsedTime 是自启动以来的总耗时（只增不减）。
    /// </summary>
    public sealed class PeriodicTask : ScheduledTaskBase
    {
        private readonly Action _callback;
        private readonly float _period;
        private readonly float _duration;
        private readonly int _maxExecutions;
        private readonly Fixed64 _periodRaw;
        private readonly Fixed64 _durationRaw;
        private Fixed64 _elapsedRaw;     // 总耗时，只增不减
        private Fixed64 _nextFireRaw;    // 下一次触发的绝对时刻
        private int _executionCount;

        public override float ElapsedTime => DeterministicMathBridge.ToSingle(_elapsedRaw);
        public override float Duration => _duration < 0 ? float.MaxValue : _duration;

        public override TaskState State
        {
            get
            {
                if (_canceled) return TaskState.Canceled;
                if (_completed) return TaskState.Completed;
                if (_maxExecutions > 0 && _executionCount >= _maxExecutions) return TaskState.Completed;
                if (_duration > 0 && _elapsedRaw >= _durationRaw) return TaskState.Completed;
                return TaskState.Running;
            }
        }

        public override bool IsCompleted
        {
            get
            {
                if (_canceled) return true;
                if (_completed) return true;
                if (_maxExecutions > 0 && _executionCount >= _maxExecutions) return true;
                if (_duration > 0 && _elapsedRaw >= _durationRaw) return true;
                return false;
            }
        }

        /// <summary>当前执行次数</summary>
        public int ExecutionCount => _executionCount;

        /// <summary>
        /// 构造周期任务
        /// </summary>
        /// <param name="callback">周期执行的回调</param>
        /// <param name="periodSeconds">周期间隔（秒）</param>
        /// <param name="durationSeconds">持续时间（秒），-1 表示无限</param>
        /// <param name="maxExecutions">最大执行次数，-1 表示无限</param>
        public PeriodicTask(Action callback, float periodSeconds, float durationSeconds = -1, int maxExecutions = -1)
        {
            if (periodSeconds <= 0f)
                throw new ArgumentOutOfRangeException(nameof(periodSeconds), periodSeconds, "Period must be positive; non-positive periods would loop forever in Update.");

            _callback = callback;
            _period = periodSeconds;
            _duration = durationSeconds;
            _maxExecutions = maxExecutions;
            _periodRaw = DeterministicMathBridge.ToFixed(periodSeconds);
            _durationRaw = DeterministicMathBridge.ToFixed(durationSeconds);
            _elapsedRaw = Fixed64.Zero;
            _nextFireRaw = _periodRaw;
            _executionCount = 0;
        }

        public override void Update(float deltaTime)
        {
            if (IsCompleted || _canceled) return;

            _elapsedRaw += DeterministicMathBridge.ToFixed(deltaTime);

            while (_nextFireRaw <= _elapsedRaw)
            {
                if (_maxExecutions > 0 && _executionCount >= _maxExecutions) break;
                if (_duration > 0 && _nextFireRaw > _durationRaw) break;

                _callback?.Invoke();
                _executionCount++;
                _nextFireRaw += _periodRaw;
            }
        }
    }
}
