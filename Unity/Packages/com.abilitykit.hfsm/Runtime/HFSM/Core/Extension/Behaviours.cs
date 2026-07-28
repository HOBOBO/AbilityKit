using System;
using System.Collections.Generic;

namespace UnityHFSM.Extension
{
    public sealed class CallbackBehaviour : IRollbackActionBehaviour
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

    public sealed class DelayBehaviour : IRollbackActionBehaviour
    {
        private readonly float _duration;
        private readonly bool _useUnscaled;
        private float _elapsed;

        public DelayBehaviour(float duration, bool useUnscaled = false)
        {
            _duration = duration;
            _useUnscaled = useUnscaled;
        }

        public void Reset()
        {
            _elapsed = 0f;
        }

        public ActionBehaviourStatus Tick(in ActionBehaviourContext ctx)
        {
            _elapsed += ctx.GetScaledDelta(_useUnscaled);
            return _elapsed >= _duration ? ActionBehaviourStatus.Success : ActionBehaviourStatus.Running;
        }

        public ActionBehaviourSnapshot CaptureSnapshot()
        {
            return new ActionBehaviourSnapshot(nameof(DelayBehaviour), floatValue: _elapsed);
        }

        public void RestoreSnapshot(ActionBehaviourSnapshot snapshot)
        {
            CallbackBehaviour.ValidateSnapshot(snapshot, nameof(DelayBehaviour));
            _elapsed = snapshot.FloatValue;
        }
    }

    public sealed class SequenceBehaviour : IRollbackActionBehaviour
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
            for (int i = 0; i < _children.Count; i++)
            {
                _children[i].Reset();
            }
        }

        public ActionBehaviourStatus Tick(in ActionBehaviourContext ctx)
        {
            while (_index < _children.Count)
            {
                var s = _children[_index].Tick(ctx);
                if (s == ActionBehaviourStatus.Running) return ActionBehaviourStatus.Running;
                if (s == ActionBehaviourStatus.Failure) return ActionBehaviourStatus.Failure;
                _index++;
            }

            return ActionBehaviourStatus.Success;
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
    }

    public sealed class ParallelBehaviour : IRollbackActionBehaviour
    {
        private readonly List<IActionBehaviour> _children = new List<IActionBehaviour>();

        public ParallelBehaviour Add(IActionBehaviour child)
        {
            if (child != null) _children.Add(child);
            return this;
        }

        public void Reset()
        {
            for (int i = 0; i < _children.Count; i++)
            {
                _children[i].Reset();
            }
        }

        public ActionBehaviourStatus Tick(in ActionBehaviourContext ctx)
        {
            var anyRunning = false;
            for (int i = 0; i < _children.Count; i++)
            {
                var s = _children[i].Tick(ctx);
                if (s == ActionBehaviourStatus.Failure) return ActionBehaviourStatus.Failure;
                if (s == ActionBehaviourStatus.Running) anyRunning = true;
            }

            return anyRunning ? ActionBehaviourStatus.Running : ActionBehaviourStatus.Success;
        }

        public ActionBehaviourSnapshot CaptureSnapshot()
        {
            return new ActionBehaviourSnapshot(
                nameof(ParallelBehaviour),
                children: SequenceBehaviour.CaptureChildren(_children, nameof(ParallelBehaviour)));
        }

        public void RestoreSnapshot(ActionBehaviourSnapshot snapshot)
        {
            CallbackBehaviour.ValidateSnapshot(snapshot, nameof(ParallelBehaviour));
            SequenceBehaviour.RestoreChildren(_children, snapshot.Children, nameof(ParallelBehaviour));
        }
    }
}
