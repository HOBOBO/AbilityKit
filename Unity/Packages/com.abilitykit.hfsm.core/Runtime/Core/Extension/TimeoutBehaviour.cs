using System;
using System.Collections.Generic;


namespace AbilityKit.HFSM.Extension
{

    public sealed class TimeoutBehaviour : IRollbackActionBehaviour, IInterruptibleActionBehaviour
    {
        private readonly IActionBehaviour _child;
        private readonly long _durationRaw;
        private readonly bool _useUnscaled;
        // Q32.32 raw 累计；快照 FloatValue 是边界单次换算视图。
        private long _elapsedRaw;

        public TimeoutBehaviour(IActionBehaviour child, float duration, bool useUnscaled = false)
        {
            _child = child;
            _durationRaw = AbilityKit.Core.Mathematics.DeterministicMathBridge.ToFixed(Math.Max(0f, duration)).RawValue;
            _useUnscaled = useUnscaled;
        }

        public void Reset()
        {
            _elapsedRaw = 0L;
            _child?.Reset();
        }

        public ActionBehaviourStatus Tick(in ActionBehaviourContext ctx)
        {
            if (_child == null) return ActionBehaviourStatus.Failure;
            var status = _child.Tick(in ctx);
            if (status != ActionBehaviourStatus.Running) return status;

            _elapsedRaw += AbilityKit.Core.Mathematics.DeterministicMathBridge.ToFixed(ctx.GetScaledDelta(_useUnscaled)).RawValue;
            if (_elapsedRaw < _durationRaw) return ActionBehaviourStatus.Running;

            SequenceBehaviour.AbortChild(_child, in ctx);
            return ActionBehaviourStatus.Failure;
        }

        public void Abort(in ActionBehaviourContext ctx)
        {
            SequenceBehaviour.AbortChild(_child, in ctx);
        }

        public ActionBehaviourSnapshot CaptureSnapshot()
        {
            return new ActionBehaviourSnapshot(
                nameof(TimeoutBehaviour),
                floatValue: AbilityKit.Deterministic.Fixed64.FromRaw(_elapsedRaw).ToSingle(),
                children: SequenceBehaviour.CaptureChildren(new[] { _child }, nameof(TimeoutBehaviour)));
        }

        public void RestoreSnapshot(ActionBehaviourSnapshot snapshot)
        {
            CallbackBehaviour.ValidateSnapshot(snapshot, nameof(TimeoutBehaviour));
            _elapsedRaw = AbilityKit.Core.Mathematics.DeterministicMathBridge.ToFixed(snapshot.FloatValue).RawValue;
            SequenceBehaviour.RestoreChildren(new[] { _child }, snapshot.Children, nameof(TimeoutBehaviour));
        }
    }
}
