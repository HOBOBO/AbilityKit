using System;
using System.Collections.Generic;


namespace AbilityKit.HFSM.Extension
{

    public sealed class CallbackBehaviour : IRollbackActionBehaviour, IInterruptibleActionBehaviour
    {
        private readonly Action _action;
        private bool _done;

        public CallbackBehaviour(Action action)
        {
            _action = action;
        }

        public void Reset()
        {
            _done = false;
        }

        public ActionBehaviourStatus Tick(in ActionBehaviourContext ctx)
        {
            if (_done) return ActionBehaviourStatus.Success;
            _done = true;
            _action?.Invoke();
            return ActionBehaviourStatus.Success;
        }

        public void Abort(in ActionBehaviourContext ctx)
        {
            _done = true;
        }

        public ActionBehaviourSnapshot CaptureSnapshot()
        {
            return new ActionBehaviourSnapshot(nameof(CallbackBehaviour), booleanValue: _done);
        }

        public void RestoreSnapshot(ActionBehaviourSnapshot snapshot)
        {
            ValidateSnapshot(snapshot, nameof(CallbackBehaviour));
            _done = snapshot.BooleanValue;
        }

        internal static void ValidateSnapshot(ActionBehaviourSnapshot snapshot, string expectedKind)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (!string.Equals(snapshot.Kind, expectedKind, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Action snapshot kind '{snapshot.Kind}' cannot restore '{expectedKind}'.");
            }
        }
    }
}
