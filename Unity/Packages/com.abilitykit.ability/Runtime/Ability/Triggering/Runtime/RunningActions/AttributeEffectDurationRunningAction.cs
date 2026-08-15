using System;
using AbilityKit.Core.Logging;

namespace AbilityKit.Ability.Triggering.Runtime
{
    /// <summary>
    /// 持续时间运行的属性效果动作。
    /// 使用 SourceId 追踪效果，结束时自动移除。
    /// </summary>
    public sealed class AttributeEffectDurationRunningAction : IRunningAction
    {
        private readonly int _sourceId;
        private readonly Action<int> _removeEffect;
        // Q32.32 raw 倒计时（整数减法无漂移）。
        private long _remainingRaw;
        private bool _done;

        /// <summary>
        /// 构造函数（需要外部传入移除效果的回调）
        /// </summary>
        public AttributeEffectDurationRunningAction(int sourceId, float durationSeconds, Action<int> removeEffect = null)
        {
            _sourceId = sourceId;
            _removeEffect = removeEffect;
            _remainingRaw = AbilityKit.Core.Mathematics.DeterministicMathBridge.ToFixed(durationSeconds).RawValue;
            if (durationSeconds <= 0f)
            {
                _done = true;
                Remove();
            }
        }

        /// <summary>
        /// 构造函数（兼容旧 API，但已弃用）
        /// </summary>
        [System.Obsolete("请使用带 removeEffect 参数的构造函数")]
        public AttributeEffectDurationRunningAction(int sourceId, float durationSeconds)
        {
            _sourceId = sourceId;
            _removeEffect = null;
            _remainingRaw = AbilityKit.Core.Mathematics.DeterministicMathBridge.ToFixed(durationSeconds).RawValue;
            if (durationSeconds <= 0f)
            {
                _done = true;
                Remove();
            }
        }

        public bool IsDone => _done;

        public void Tick(float deltaTime)
        {
            if (_done) return;

            _remainingRaw -= AbilityKit.Core.Mathematics.DeterministicMathBridge.ToFixed(deltaTime).RawValue;
            if (_remainingRaw <= 0L)
            {
                _done = true;
                Remove();
            }
        }

        public void Cancel()
        {
            if (_done) return;
            _done = true;
            Remove();
        }

        public void Dispose()
        {
            Remove();
        }

        private void Remove()
        {
            try
            {
                _removeEffect?.Invoke(_sourceId);
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "[AttributeEffectDurationRunningAction] remove effect failed");
            }
        }
    }
}
