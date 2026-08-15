using System;
using System.Collections.Generic;

namespace UnityHFSM.Extension
{
    public delegate bool ActionBehaviourPredicate(in ActionBehaviourContext context);

    public enum ParallelSuccessPolicy
    {
        All = 0,
        Any = 1,
    }

    public enum ParallelFailurePolicy
    {
        Any = 0,
        All = 1,
    }

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

    public sealed class SelectorBehaviour : IRollbackActionBehaviour, IInterruptibleActionBehaviour
    {
        private readonly List<IActionBehaviour> _children = new List<IActionBehaviour>();
        private int _index;

        public SelectorBehaviour Add(IActionBehaviour child)
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
                if (status == ActionBehaviourStatus.Success) return ActionBehaviourStatus.Success;
                if (status == ActionBehaviourStatus.Cancelled) return ActionBehaviourStatus.Cancelled;
                _index++;
            }

            return ActionBehaviourStatus.Failure;
        }

        public void Abort(in ActionBehaviourContext ctx)
        {
            if (_index < _children.Count) SequenceBehaviour.AbortChild(_children[_index], in ctx);
        }

        public ActionBehaviourSnapshot CaptureSnapshot()
        {
            return new ActionBehaviourSnapshot(
                nameof(SelectorBehaviour),
                integerValue: _index,
                children: SequenceBehaviour.CaptureChildren(_children, nameof(SelectorBehaviour)));
        }

        public void RestoreSnapshot(ActionBehaviourSnapshot snapshot)
        {
            CallbackBehaviour.ValidateSnapshot(snapshot, nameof(SelectorBehaviour));
            SequenceBehaviour.RestoreChildren(_children, snapshot.Children, nameof(SelectorBehaviour));
            if (snapshot.IntegerValue < 0 || snapshot.IntegerValue > _children.Count)
            {
                throw new InvalidOperationException(
                    $"Selector action index '{snapshot.IntegerValue}' is outside [0, {_children.Count}].");
            }

            _index = snapshot.IntegerValue;
        }
    }

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
