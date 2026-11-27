using System;
using AbilityKit.Core.Mathematics;
using AbilityKit.Deterministic;

namespace AbilityKit.Timer
{
    /// <summary>
    /// 用定点时间累计推进，以固定间隔执行回调；支持正数总时长截止和执行次数上限。
    /// </summary>
    public sealed class PeriodicTask : ScheduledTaskBase
    {
        private readonly Action _callback;
        private readonly float _duration;
        private readonly int _maxExecutions;
        private readonly Fixed64 _periodRaw;
        private readonly Fixed64 _durationRaw;
        private Fixed64 _elapsedRaw;
        private Fixed64 _nextFireRaw;
        private int _executionCount;

        /// <summary>
        /// 获取累计推进时间的浮点秒数视图。
        /// </summary>
        public override float ElapsedTime => DeterministicMathBridge.ToSingle(_elapsedRaw);

        /// <summary>
        /// 获取配置的总时长；负数配置以 <see cref="Single.MaxValue"/> 表示。
        /// </summary>
        public override float Duration => _duration < 0 ? float.MaxValue : _duration;

        /// <summary>
        /// 获取当前状态；取消优先于完成。
        /// </summary>
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

        /// <summary>
        /// 获取任务是否已取消、已完成或达到时长或次数上限。
        /// </summary>
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

        /// <summary>
        /// 获取已经结束的触发次数，不包含仍在执行中的回调。
        /// </summary>
        public int ExecutionCount => _executionCount;

        /// <summary>
        /// 创建周期任务，首次触发在一个间隔后。
        /// </summary>
        /// <param name="callback">周期执行的回调</param>
        /// <param name="periodSeconds">周期间隔（秒）</param>
        /// <param name="durationSeconds">总时长（秒），非正值不设截止。</param>
        /// <param name="maxExecutions">触发次数上限，非正值不限次。</param>
        /// <exception cref="ArgumentOutOfRangeException">间隔转换为定点时间后不是正数。</exception>
        public PeriodicTask(Action callback, float periodSeconds, float durationSeconds = -1, int maxExecutions = -1)
        {
            _periodRaw = periodSeconds > 0f ? DeterministicMathBridge.ToFixed(periodSeconds) : Fixed64.Zero;
            if (_periodRaw <= Fixed64.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(periodSeconds), periodSeconds, "Period must be positive after conversion to fixed-point seconds.");
            }

            _callback = callback;
            _duration = durationSeconds;
            _maxExecutions = maxExecutions;
            _durationRaw = DeterministicMathBridge.ToFixed(durationSeconds);
            _elapsedRaw = Fixed64.Zero;
            _nextFireRaw = _periodRaw;
            _executionCount = 0;
        }

        /// <summary>
        /// 累加秒增量并补齐本次已到期且不晚于截止的触发；回调主动停止任务后不再补触发。
        /// </summary>
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
                if (_canceled || _completed) break;
                _nextFireRaw += _periodRaw;
            }
        }
    }
}
