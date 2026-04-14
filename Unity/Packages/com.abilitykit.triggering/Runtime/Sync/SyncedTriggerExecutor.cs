using System.Collections.Generic;
using AbilityKit.Triggering.Runtime.Behavior;
using AbilityKit.Triggering.Runtime.Config.Plans;
using AbilityKit.Triggering.Runtime.Factory;
using AbilityKit.Triggering.Runtime.Instance;

namespace AbilityKit.Triggering.Runtime.Sync
{
    /// <summary>
    /// 同步触发器执行器（支持网络同步）
    /// </summary>
    public class SyncedTriggerExecutor : ISyncedTriggerExecutor
    {
        private readonly IBehaviorFactory _behaviorFactory;
        private readonly Dictionary<(int, int), ISchedulableBehavior> _activeBehaviors = new Dictionary<(int, int), ISchedulableBehavior>();
        private readonly Dictionary<(int, int), TriggerState> _activeStates = new Dictionary<(int, int), TriggerState>();

        public int ExecutorId { get; }

        public SyncedTriggerExecutor(int executorId, IBehaviorFactory behaviorFactory)
        {
            ExecutorId = executorId;
            _behaviorFactory = behaviorFactory ?? throw new System.ArgumentNullException(nameof(behaviorFactory));
        }

        public ITriggerInstance Execute(
            int triggerId,
            ITriggerPlanConfig config,
            IBehaviorContext context,
            ITriggerSyncService syncService,
            long serverTime = 0)
        {
            if (syncService == null)
                throw new System.ArgumentNullException(nameof(syncService));

            var behavior = _behaviorFactory.Create(config);
            var key = (triggerId, ExecutorId);
            var state = TriggerState.Create(triggerId, ExecutorId, serverTime);

            syncService.OnTriggerStarted(triggerId, ExecutorId);

            if (behavior is ISchedulableBehavior schedulable)
            {
                _activeBehaviors[key] = schedulable;
                _activeStates[key] = state;
                schedulable.Begin(context);
                state.CurrentState = ETriggerState.Running;

                if (schedulable.State == EBehaviorState.Completed)
                {
                    syncService.OnTriggerCompleted(triggerId, ExecutorId);
                    state.CurrentState = ETriggerState.Completed;
                    _activeBehaviors.Remove(key);
                    _activeStates.Remove(key);
                    return CreateInstance(config, state, behavior);
                }

                syncService.OnTriggerProgress(triggerId, ExecutorId, schedulable.ElapsedMs);
            }
            else if (behavior is ISimpleTriggerBehavior simple)
            {
                if (!simple.Evaluate(context))
                {
                    syncService.OnTriggerInterrupted(triggerId, ExecutorId, "Condition failed");
                    state.CurrentState = ETriggerState.Interrupted;
                    return CreateInstance(config, state, behavior);
                }

                var result = simple.Execute(context);
                if (result.IsInterrupted)
                {
                    syncService.OnTriggerInterrupted(triggerId, ExecutorId, result.FailureReason);
                    state.CurrentState = ETriggerState.Interrupted;
                }
                else
                {
                    syncService.OnTriggerCompleted(triggerId, ExecutorId);
                    state.CurrentState = ETriggerState.Completed;
                }
                return CreateInstance(config, state, behavior);
            }

            return CreateInstance(config, state, behavior);
        }

        private ITriggerInstance CreateInstance(ITriggerPlanConfig config, TriggerState state, ITriggerBehavior behavior)
        {
            return new TriggerInstance(config, state.ExecutorId, state.StartServerTime)
            {
                Behavior = behavior
            };
        }

        public bool TryGetBehavior(int triggerId, int executorId, out ISchedulableBehavior behavior)
        {
            return _activeBehaviors.TryGetValue((triggerId, executorId), out behavior);
        }

        public bool TryGetState(int triggerId, int executorId, out TriggerState state)
        {
            return _activeStates.TryGetValue((triggerId, executorId), out state);
        }

        public void Update(float deltaTimeMs, IBehaviorContext context, ITriggerSyncService syncService)
        {
            var keysToRemove = new System.Collections.Generic.List<(int, int)>();

            foreach (var kvp in _activeBehaviors)
            {
                var behavior = kvp.Value;
                behavior.Update(deltaTimeMs, context);

                if (_activeStates.TryGetValue(kvp.Key, out var state))
                {
                    state.ElapsedMs = behavior.ElapsedMs;
                }

                syncService.OnTriggerProgress(kvp.Key.Item1, kvp.Key.Item2, behavior.ElapsedMs);

                if (behavior.State == EBehaviorState.Completed)
                {
                    syncService.OnTriggerCompleted(kvp.Key.Item1, kvp.Key.Item2);
                    if (_activeStates.TryGetValue(kvp.Key, out var st))
                        st.CurrentState = ETriggerState.Completed;
                    keysToRemove.Add(kvp.Key);
                }
            }

            foreach (var key in keysToRemove)
            {
                _activeBehaviors.Remove(key);
                _activeStates.Remove(key);
            }
        }

        public void Interrupt(int triggerId, string reason, ITriggerSyncService syncService)
        {
            var key = (triggerId, ExecutorId);
            if (_activeBehaviors.TryGetValue(key, out var behavior))
            {
                behavior.Interrupt(reason);
                syncService.OnTriggerInterrupted(triggerId, ExecutorId, reason);
                
                if (_activeStates.TryGetValue(key, out var state))
                    state.CurrentState = ETriggerState.Interrupted;
                
                _activeBehaviors.Remove(key);
                _activeStates.Remove(key);
            }
        }

        public void CaptureState(System.Collections.Generic.IEnumerable<(int, int)> activeTriggerIds, ITriggerSyncService syncService)
        {
            foreach (var key in activeTriggerIds)
            {
                if (_activeBehaviors.TryGetValue(key, out var behavior) && 
                    _activeStates.TryGetValue(key, out var state))
                {
                    var snapshot = TriggerSnapshot.FromState(state, behavior.GetType().GetHashCode());
                    syncService.CaptureSnapshot(key.Item1, key.Item2, snapshot);
                }
            }
        }
    }
}