using System;
using AbilityKit.Core.Mathematics;
using AbilityKit.Deterministic;

namespace AbilityKit.Timer
{
    /// <summary>
    /// 每次推进执行回调，直至被主动停止或达到正数时长上限；到期使用定点时间判定。
    /// </summary>
    public sealed class ContinuousTask : ScheduledTaskBase
    {
        private readonly Action<float> _onTick;
        private readonly Action _onComplete;
        private readonly float _duration;
        private readonly Fixed64 _durationRaw;
        private Fixed64 _elapsedRaw;

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
                if (_duration > 0 && _elapsedRaw >= _durationRaw) return TaskState.Completed;
                return TaskState.Running;
            }
        }

        /// <summary>
        /// 获取任务是否已取消、已完成或达到时长上限。
        /// </summary>
        public override bool IsCompleted
        {
            get
            {
                if (_canceled) return true;
                if (_completed) return true;
                if (_duration > 0 && _elapsedRaw >= _durationRaw) return true;
                return false;
            }
        }

        /// <summary>
        /// 创建持续任务，外部完成和取消均不触发自然完成回调。
        /// </summary>
        /// <param name="onTick">每次有效推进的回调，接收调用方原始秒增量。</param>
        /// <param name="onComplete">自然到期时的可选回调。</param>
        /// <param name="durationSeconds">最大时长（秒），非正值不设截止。</param>
        public ContinuousTask(Action<float> onTick, Action onComplete, float durationSeconds = -1)
        {
            _onTick = onTick;
            _onComplete = onComplete;
            _duration = durationSeconds;
            _durationRaw = DeterministicMathBridge.ToFixed(durationSeconds);
            _elapsedRaw = Fixed64.Zero;
        }

        /// <summary>
        /// 推进时间并执行本次回调；自然到期且未在回调中停止时才执行完成回调。
        /// </summary>
        public override void Update(float deltaTime)
        {
            if (IsCompleted || _canceled) return;

            _elapsedRaw += DeterministicMathBridge.ToFixed(deltaTime);
            _onTick?.Invoke(deltaTime);

            if (!_canceled && !_completed && _duration > 0 && _elapsedRaw >= _durationRaw)
            {
                _completed = true;
                _onComplete?.Invoke();
            }
        }
    }
}
