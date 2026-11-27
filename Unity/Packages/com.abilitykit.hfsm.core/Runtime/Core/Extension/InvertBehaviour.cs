using System;
using System.Collections.Generic;


namespace AbilityKit.HFSM.Extension
{

    public sealed class InvertBehaviour : IRollbackActionBehaviour, IInterruptibleActionBehaviour
    {
        private readonly IActionBehaviour _child;

        public InvertBehaviour(IActionBehaviour child)
        {
            _child = child;
        }

        public void Reset()
        {
            _child?.Reset();
        }

        public ActionBehaviourStatus Tick(in ActionBehaviourContext ctx)
        {
            if (_child == null) return ActionBehaviourStatus.Failure;
            var status = _child.Tick(in ctx);
            return status switch
            {
                ActionBehaviourStatus.Success => ActionBehaviourStatus.Failure,
                ActionBehaviourStatus.Failure => ActionBehaviourStatus.Success,
                _ => status,
            };
        }

        public void Abort(in ActionBehaviourContext ctx)
        {
            SequenceBehaviour.AbortChild(_child, in ctx);
        }

        public ActionBehaviourSnapshot CaptureSnapshot()
        {
            return new ActionBehaviourSnapshot(
                nameof(InvertBehaviour),
                children: SequenceBehaviour.CaptureChildren(new[] { _child }, nameof(InvertBehaviour)));
        }

        public void RestoreSnapshot(ActionBehaviourSnapshot snapshot)
        {
            CallbackBehaviour.ValidateSnapshot(snapshot, nameof(InvertBehaviour));
            SequenceBehaviour.RestoreChildren(new[] { _child }, snapshot.Children, nameof(InvertBehaviour));
        }
    }
}
