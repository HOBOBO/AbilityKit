using System;
using System.Collections.Generic;


namespace AbilityKit.HFSM.Extension
{

    public sealed class DelayBehaviour : IRollbackActionBehaviour, IInterruptibleActionBehaviour
    {
        private readonly long _durationRaw;
        private readonly bool _useUnscaled;
        // Q32.32 raw 累计（整数加法无漂移）；快照的 FloatValue 是边界单次换算视图。
        private long _elapsedRaw;

        public DelayBehaviour(float duration, bool useUnscaled = false)
        {
            _durationRaw = AbilityKit.Core.Mathematics.DeterministicMathBridge.ToFixed(Math.Max(0f, duration)).RawValue;
            _useUnscaled = useUnscaled;
        }

        public void Reset()
        {
            _elapsedRaw = 0L;
        }

        public ActionBehaviourStatus Tick(in ActionBehaviourContext ctx)
        {
            _elapsedRaw += AbilityKit.Core.Mathematics.DeterministicMathBridge.ToFixed(ctx.GetScaledDelta(_useUnscaled)).RawValue;
            return _elapsedRaw >= _durationRaw ? ActionBehaviourStatus.Success : ActionBehaviourStatus.Running;
        }

        public void Abort(in ActionBehaviourContext ctx)
        {
        }

        public ActionBehaviourSnapshot CaptureSnapshot()
        {
            return new ActionBehaviourSnapshot(
                nameof(DelayBehaviour),
                floatValue: AbilityKit.Deterministic.Fixed64.FromRaw(_elapsedRaw).ToSingle());
        }

        public void RestoreSnapshot(ActionBehaviourSnapshot snapshot)
        {
            CallbackBehaviour.ValidateSnapshot(snapshot, nameof(DelayBehaviour));
            _elapsedRaw = AbilityKit.Core.Mathematics.DeterministicMathBridge.ToFixed(snapshot.FloatValue).RawValue;
        }
    }
}
