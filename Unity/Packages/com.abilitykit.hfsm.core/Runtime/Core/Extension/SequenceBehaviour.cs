using System;
using System.Collections.Generic;


namespace AbilityKit.HFSM.Extension
{

    public sealed class SequenceBehaviour : IRollbackActionBehaviour, IInterruptibleActionBehaviour
    {
        private readonly List<IActionBehaviour> _children = new List<IActionBehaviour>();
        private int _index;

        public SequenceBehaviour Add(IActionBehaviour child)
        {
            if (child != null) _children.Add(child);
            return this;
        }

        public void Reset()
        {
            _index = 0;
            for (var i = 0; i < _children.Count; i++) _children[i].Reset();
        }

        public ActionBehaviourStatus Tick(in ActionBehaviourContext ctx)
        {
            while (_index < _children.Count)
            {
                var status = _children[_index].Tick(in ctx);
                if (status == ActionBehaviourStatus.Running) return ActionBehaviourStatus.Running;
                if (status != ActionBehaviourStatus.Success) return status;
                _index++;
            }

            return ActionBehaviourStatus.Success;
        }

        public void Abort(in ActionBehaviourContext ctx)
        {
            if (_index < _children.Count) AbortChild(_children[_index], in ctx);
        }

        public ActionBehaviourSnapshot CaptureSnapshot()
        {
            return new ActionBehaviourSnapshot(
                nameof(SequenceBehaviour),
                integerValue: _index,
                children: CaptureChildren(_children, nameof(SequenceBehaviour)));
        }

        public void RestoreSnapshot(ActionBehaviourSnapshot snapshot)
        {
            CallbackBehaviour.ValidateSnapshot(snapshot, nameof(SequenceBehaviour));
            RestoreChildren(_children, snapshot.Children, nameof(SequenceBehaviour));
            if (snapshot.IntegerValue < 0 || snapshot.IntegerValue > _children.Count)
            {
                throw new InvalidOperationException(
                    $"Sequence action index '{snapshot.IntegerValue}' is outside [0, {_children.Count}].");
            }

            _index = snapshot.IntegerValue;
        }

        internal static ActionBehaviourSnapshot[] CaptureChildren(
            IReadOnlyList<IActionBehaviour> children,
            string ownerKind)
        {
            var snapshots = new ActionBehaviourSnapshot[children.Count];
            for (var i = 0; i < children.Count; i++)
            {
                if (!(children[i] is IRollbackActionBehaviour rollbackBehaviour))
                {
                    throw new InvalidOperationException(
                        $"Action '{children[i]?.GetType().FullName ?? "null"}' in '{ownerKind}' does not support rollback.");
                }

                snapshots[i] = rollbackBehaviour.CaptureSnapshot();
            }

            return snapshots;
        }

        internal static void RestoreChildren(
            IReadOnlyList<IActionBehaviour> children,
            IReadOnlyList<ActionBehaviourSnapshot> snapshots,
            string ownerKind)
        {
            if (snapshots == null || snapshots.Count != children.Count)
            {
                throw new InvalidOperationException(
                    $"Action snapshot for '{ownerKind}' has {snapshots?.Count ?? 0} children; expected {children.Count}.");
            }

            for (var i = 0; i < children.Count; i++)
            {
                if (!(children[i] is IRollbackActionBehaviour rollbackBehaviour))
                {
                    throw new InvalidOperationException(
                        $"Action '{children[i]?.GetType().FullName ?? "null"}' in '{ownerKind}' does not support rollback.");
                }

                rollbackBehaviour.RestoreSnapshot(snapshots[i]);
            }
        }

        internal static void AbortChild(IActionBehaviour child, in ActionBehaviourContext ctx)
        {
            if (child is IInterruptibleActionBehaviour interruptible) interruptible.Abort(in ctx);
        }
    }
}
