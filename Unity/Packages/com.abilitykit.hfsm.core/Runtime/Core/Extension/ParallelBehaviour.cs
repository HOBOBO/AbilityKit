using System;
using System.Collections.Generic;


namespace AbilityKit.HFSM.Extension
{

    public sealed class ParallelBehaviour : IRollbackActionBehaviour, IInterruptibleActionBehaviour
    {
        private const string ChildSnapshotKind = "ParallelChild";
        private readonly List<IActionBehaviour> _children = new List<IActionBehaviour>();
        private readonly ParallelSuccessPolicy _successPolicy;
        private readonly ParallelFailurePolicy _failurePolicy;
        private ActionBehaviourStatus[] _statuses = Array.Empty<ActionBehaviourStatus>();

        public ParallelBehaviour(
            ParallelSuccessPolicy successPolicy = ParallelSuccessPolicy.All,
            ParallelFailurePolicy failurePolicy = ParallelFailurePolicy.Any)
        {
            _successPolicy = successPolicy;
            _failurePolicy = failurePolicy;
        }

        public ParallelBehaviour Add(IActionBehaviour child)
        {
            if (child != null) _children.Add(child);
            return this;
        }

        public void Reset()
        {
            _statuses = new ActionBehaviourStatus[_children.Count];
            for (var i = 0; i < _children.Count; i++) _children[i].Reset();
        }

        public ActionBehaviourStatus Tick(in ActionBehaviourContext ctx)
        {
            if (_children.Count == 0) return ActionBehaviourStatus.Success;
            if (_statuses.Length != _children.Count) Reset();

            for (var i = 0; i < _children.Count; i++)
            {
                if (_statuses[i] != ActionBehaviourStatus.Running) continue;
                _statuses[i] = _children[i].Tick(in ctx);
            }

            CountStatuses(out var running, out var succeeded, out var failed);
            var failureReached = _failurePolicy == ParallelFailurePolicy.Any
                ? failed > 0
                : failed == _children.Count;
            var successReached = _successPolicy == ParallelSuccessPolicy.Any
                ? succeeded > 0
                : succeeded == _children.Count;

            if (failureReached)
            {
                AbortRunningChildren(in ctx);
                return ActionBehaviourStatus.Failure;
            }

            if (successReached)
            {
                AbortRunningChildren(in ctx);
                return ActionBehaviourStatus.Success;
            }

            if (running == 0)
            {
                return ActionBehaviourStatus.Failure;
            }

            return ActionBehaviourStatus.Running;
        }

        public void Abort(in ActionBehaviourContext ctx)
        {
            if (_statuses.Length != _children.Count) _statuses = new ActionBehaviourStatus[_children.Count];
            AbortRunningChildren(in ctx);
        }

        public ActionBehaviourSnapshot CaptureSnapshot()
        {
            if (_statuses.Length != _children.Count) _statuses = new ActionBehaviourStatus[_children.Count];
            var children = new ActionBehaviourSnapshot[_children.Count];
            for (var i = 0; i < _children.Count; i++)
            {
                if (!(_children[i] is IRollbackActionBehaviour rollbackBehaviour))
                {
                    throw new InvalidOperationException(
                        $"Action '{_children[i]?.GetType().FullName ?? "null"}' in '{nameof(ParallelBehaviour)}' does not support rollback.");
                }

                children[i] = new ActionBehaviourSnapshot(
                    ChildSnapshotKind,
                    integerValue: (int)_statuses[i],
                    children: new[] { rollbackBehaviour.CaptureSnapshot() });
            }

            return new ActionBehaviourSnapshot(nameof(ParallelBehaviour), children: children);
        }

        public void RestoreSnapshot(ActionBehaviourSnapshot snapshot)
        {
            CallbackBehaviour.ValidateSnapshot(snapshot, nameof(ParallelBehaviour));
            if (snapshot.Children.Count != _children.Count)
            {
                throw new InvalidOperationException(
                    $"Action snapshot for '{nameof(ParallelBehaviour)}' has {snapshot.Children.Count} children; expected {_children.Count}.");
            }

            _statuses = new ActionBehaviourStatus[_children.Count];
            for (var i = 0; i < _children.Count; i++)
            {
                var childSnapshot = snapshot.Children[i];
                CallbackBehaviour.ValidateSnapshot(childSnapshot, ChildSnapshotKind);
                if (childSnapshot.Children.Count != 1
                    || childSnapshot.IntegerValue < (int)ActionBehaviourStatus.Running
                    || childSnapshot.IntegerValue > (int)ActionBehaviourStatus.Cancelled)
                {
                    throw new InvalidOperationException("Parallel child snapshot is malformed.");
                }

                if (!(_children[i] is IRollbackActionBehaviour rollbackBehaviour))
                {
                    throw new InvalidOperationException(
                        $"Action '{_children[i]?.GetType().FullName ?? "null"}' in '{nameof(ParallelBehaviour)}' does not support rollback.");
                }

                _statuses[i] = (ActionBehaviourStatus)childSnapshot.IntegerValue;
                rollbackBehaviour.RestoreSnapshot(childSnapshot.Children[0]);
            }
        }

        private void CountStatuses(out int running, out int succeeded, out int failed)
        {
            running = 0;
            succeeded = 0;
            failed = 0;
            for (var i = 0; i < _statuses.Length; i++)
            {
                switch (_statuses[i])
                {
                    case ActionBehaviourStatus.Running:
                        running++;
                        break;
                    case ActionBehaviourStatus.Success:
                        succeeded++;
                        break;
                    default:
                        failed++;
                        break;
                }
            }
        }

        private void AbortRunningChildren(in ActionBehaviourContext ctx)
        {
            for (var i = 0; i < _children.Count; i++)
            {
                if (_statuses[i] != ActionBehaviourStatus.Running) continue;
                SequenceBehaviour.AbortChild(_children[i], in ctx);
                _statuses[i] = ActionBehaviourStatus.Cancelled;
            }
        }
    }
}
