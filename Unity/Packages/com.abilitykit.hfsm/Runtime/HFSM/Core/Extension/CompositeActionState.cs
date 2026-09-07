

namespace AbilityKit.HFSM.Extension
{
    public enum ActionStateCompletionPolicy
    {
        Hold = 0,
        Loop = 1,
    }

    public class CompositeActionState<TStateId, TEvent> : ActionState<TStateId, TEvent>
    {
        private IActionBehaviour _root;
        private IActionTimeSource _timeSource;
        private ActionStateCompletionPolicy _completionPolicy;
        private float _timeScale = 1f;
        private float _speed = 1f;

        private bool _exitRequested;
        private bool _completed;
        private ActionBehaviourStatus _lastStatus;

        public event System.Action<ActionBehaviourStatus> BehaviourCompleted;

        public CompositeActionState(bool needsExitTime, bool isGhostState = false)
            : base(needsExitTime: needsExitTime, isGhostState: isGhostState)
        {
        }

        public CompositeActionState<TStateId, TEvent> SetRoot(IActionBehaviour root)
        {
            _root = root;
            return this;
        }

        public CompositeActionState<TStateId, TEvent> SetLoop(bool loop)
        {
            _completionPolicy = loop ? ActionStateCompletionPolicy.Loop : ActionStateCompletionPolicy.Hold;
            return this;
        }

        public CompositeActionState<TStateId, TEvent> SetCompletionPolicy(ActionStateCompletionPolicy policy)
        {
            _completionPolicy = policy;
            return this;
        }

        public CompositeActionState<TStateId, TEvent> SetTimeScale(float timeScale)
        {
            _timeScale = timeScale;
            return this;
        }

        public CompositeActionState<TStateId, TEvent> SetSpeed(float speed)
        {
            _speed = speed;
            return this;
        }

        public CompositeActionState<TStateId, TEvent> SetTimeSource(IActionTimeSource timeSource)
        {
            _timeSource = timeSource;
            return this;
        }

        public override void OnEnter()
        {
            _exitRequested = false;
            _completed = false;
            _lastStatus = ActionBehaviourStatus.Running;
            _root?.Reset();
        }

        public override void OnLogic()
        {
            if (_root == null)
            {
                MarkCompleted(ActionBehaviourStatus.Success);
                return;
            }

            if (_completed)
            {
                if (_completionPolicy == ActionStateCompletionPolicy.Loop)
                {
                    _completed = false;
                    _lastStatus = ActionBehaviourStatus.Running;
                    _root.Reset();
                }
                else
                {
                    return;
                }
            }

            var dt = _timeSource != null ? _timeSource.DeltaTime : 0f;
            var udt = _timeSource != null ? _timeSource.UnscaledDeltaTime : 0f;
            var ctx = new ActionBehaviourContext(dt, udt, _timeScale, _speed);
            var status = _root.Tick(in ctx);

            if (status != ActionBehaviourStatus.Running)
            {
                MarkCompleted(status);
            }
        }

        public override void OnExitRequest()
        {
            if (!_completed)
            {
                _exitRequested = true;
                return;
            }

            fsm.StateCanExit();
        }

        public override void OnExit()
        {
            if (!_completed)
            {
                var dt = _timeSource != null ? _timeSource.DeltaTime : 0f;
                var udt = _timeSource != null ? _timeSource.UnscaledDeltaTime : 0f;
                var ctx = new ActionBehaviourContext(dt, udt, _timeScale, _speed);
                if (_root is IInterruptibleActionBehaviour interruptible)
                {
                    interruptible.Abort(in ctx);
                }

                MarkCompleted(ActionBehaviourStatus.Cancelled);
            }
        }

        private void MarkCompleted(ActionBehaviourStatus status)
        {
            _completed = true;
            _lastStatus = status;
            BehaviourCompleted?.Invoke(status);

            if (!needsExitTime) return;

            if (_exitRequested || fsm.HasPendingTransition)
            {
                fsm.StateCanExit();
            }
        }

        public CompositeActionStateSnapshot CaptureRuntimeSnapshot()
        {
            if (!(_root is IRollbackActionBehaviour rollbackRoot))
            {
                throw new System.InvalidOperationException(
                    $"Root action '{_root?.GetType().FullName ?? "null"}' does not support rollback.");
            }

            return new CompositeActionStateSnapshot(
                _exitRequested,
                _completed,
                _lastStatus,
                rollbackRoot.CaptureSnapshot());
        }

        public void RestoreRuntimeSnapshot(CompositeActionStateSnapshot snapshot)
        {
            if (snapshot == null) throw new System.ArgumentNullException(nameof(snapshot));
            if (!(_root is IRollbackActionBehaviour rollbackRoot))
            {
                throw new System.InvalidOperationException(
                    $"Root action '{_root?.GetType().FullName ?? "null"}' does not support rollback.");
            }

            rollbackRoot.RestoreSnapshot(snapshot.Root);
            _exitRequested = snapshot.ExitRequested;
            _completed = snapshot.Completed;
            _lastStatus = snapshot.LastStatus;
        }

        public bool IsCompleted => _completed;
        public ActionBehaviourStatus LastStatus => _lastStatus;
        public ActionStateCompletionPolicy CompletionPolicy => _completionPolicy;
    }


    public sealed class CompositeActionState<TStateId> : CompositeActionState<TStateId, string>
    {
        public CompositeActionState(bool needsExitTime, bool isGhostState = false)
            : base(needsExitTime: needsExitTime, isGhostState: isGhostState)
        {
        }
    }


    public sealed class CompositeActionState : CompositeActionState<string, string>
    {
        public CompositeActionState(bool needsExitTime, bool isGhostState = false)
            : base(needsExitTime: needsExitTime, isGhostState: isGhostState)
        {
        }
    }
}
