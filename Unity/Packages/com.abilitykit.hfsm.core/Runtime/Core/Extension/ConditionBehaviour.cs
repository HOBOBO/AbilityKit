using System;
using System.Collections.Generic;


namespace AbilityKit.HFSM.Extension
{

    public sealed class ConditionBehaviour : IRollbackActionBehaviour
    {
        private readonly ActionBehaviourPredicate _predicate;

        public ConditionBehaviour(ActionBehaviourPredicate predicate)
        {
            _predicate = predicate;
        }

        public void Reset()
        {
        }

        public ActionBehaviourStatus Tick(in ActionBehaviourContext ctx)
        {
            return _predicate != null && _predicate(in ctx)
                ? ActionBehaviourStatus.Success
                : ActionBehaviourStatus.Failure;
        }

        public ActionBehaviourSnapshot CaptureSnapshot()
        {
            return new ActionBehaviourSnapshot(nameof(ConditionBehaviour));
        }

        public void RestoreSnapshot(ActionBehaviourSnapshot snapshot)
        {
            CallbackBehaviour.ValidateSnapshot(snapshot, nameof(ConditionBehaviour));
        }
    }
}
