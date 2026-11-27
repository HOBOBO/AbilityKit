using System;
using AbilityKit.Core.Mathematics;
using AbilityKit.Deterministic;

namespace AbilityKit.Timer
{
    /// <summary>
    /// 使用定点时间判断延时是否到期；自然到期最多调用一次回调。
    /// </summary>
    public sealed class DelayTask : ScheduledTaskBase
    {
        private readonly Action _callback;
        private readonly float _delay;
        private readonly Fixed64 _delayRaw;
        private Fixed64 _elapsedRaw;

        /// <summary>
        /// 获取累计推进时间的浮点秒数视图。
        /// </summary>
        public override float ElapsedTime => DeterministicMathBridge.ToSingle(_elapsedRaw);

        /// <summary>
        /// 获取配置的延迟秒数。
        /// </summary>
        public override float Duration => _delay;

        /// <summary>
        /// 获取当前状态；取消优先于完成，达到时限即视为完成。
        /// </summary>
        public override TaskState State
        {
            get
            {
                if (_canceled) return TaskState.Canceled;
                if (_completed) return TaskState.Completed;
                if (_elapsedRaw >= _delayRaw) return TaskState.Completed;
                return TaskState.Running;
            }
        }

        /// <summary>
        /// 获取任务是否达到时限或被显式完成；此属性不表示到期回调已执行。
        /// </summary>
        public override bool IsCompleted => _elapsedRaw >= _delayRaw || _completed;

        /// <summary>
        /// 构造延时任务
        /// </summary>
        /// <param name="callback">到期执行的回调</param>
        /// <param name="delaySeconds">延迟时间（秒）</param>
        public DelayTask(Action callback, float delaySeconds)
        {
            _callback = callback;
            _delay = delaySeconds;
            _delayRaw = DeterministicMathBridge.ToFixed(delaySeconds);
            _elapsedRaw = Fixed64.Zero;
        }

        /// <summary>
        /// 推进秒增量；自然到期时先标记完成，再调用回调，回调抛错也不会重试。
        /// </summary>
        public override void Update(float deltaTime)
        {
            /*
             * 不使用 IsCompleted 作守卫，否则零延迟任务会跳过首次到期回调。
             */
            if (_completed || _canceled) return;

            _elapsedRaw += DeterministicMathBridge.ToFixed(deltaTime);

            if (_elapsedRaw >= _delayRaw)
            {
                _completed = true;
                _callback?.Invoke();
            }
        }
    }
}
