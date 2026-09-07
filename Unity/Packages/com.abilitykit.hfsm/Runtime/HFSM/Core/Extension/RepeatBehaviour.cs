using System;
using System.Collections.Generic;


namespace AbilityKit.HFSM.Extension
{

    public sealed class RepeatBehaviour : IRollbackActionBehaviour, IInterruptibleActionBehaviour
    {
        private readonly IActionBehaviour _child;
        private readonly int _count;
        private int _completedCount;

        public RepeatBehaviour(IActionBehaviour child, int count = -1)
        {
            _child = child;
            _count = count;
        }

        public void Reset()
        {
            _completedCount = 0;
            _child?.Reset();
        }

        public ActionBehaviourStatus Tick(in ActionBehaviourContext ctx)
        {
            if (_child == null) return ActionBehaviourStatus.Failure;
            if (_count == 0) return ActionBehaviourStatus.Success;

            var status = _child.Tick(in ctx);
            if (status != ActionBehaviourStatus.Success) return status;

            _completedCount++;
            if (_count > 0 && _completedCount >= _count) return ActionBehaviourStatus.Success;

            _child.Reset();
            return ActionBehaviourStatus.Running;
        }

        public void Abort(in ActionBehaviourContext ctx)
        {
            SequenceBehaviour.AbortChild(_child, in ctx);
        }

        public ActionBehaviourSnapshot CaptureSnapshot()
        {
            return new ActionBehaviourSnapshot(
                nameof(RepeatBehaviour),
                integerValue: _completedCount,
                children: SequenceBehaviour.CaptureChildren(new[] { _child }, nameof(RepeatBehaviour)));
        }

        public void RestoreSnapshot(ActionBehaviourSnapshot snapshot)
        {
            CallbackBehaviour.ValidateSnapshot(snapshot, nameof(RepeatBehaviour));
            if (snapshot.IntegerValue < 0 || (_count >= 0 && snapshot.IntegerValue > _count))
            {
                throw new InvalidOperationException("Repeat action snapshot has an invalid completed count.");
            }

            _completedCount = snapshot.IntegerValue;
            SequenceBehaviour.RestoreChildren(new[] { _child }, snapshot.Children, nameof(RepeatBehaviour));
        }
    }
}
