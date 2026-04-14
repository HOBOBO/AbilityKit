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
        // 直接存储触发器实例（包含所有状态）
        private readonly Dictionary<(int, int), TriggerInstance> _activeInstances = new Dictionary<(int, int), TriggerInstance>();
        private readonly Dictionary<(int, int), ISchedulableBehavior> _activeBehaviors = new Dictionary<(int, int), ISchedulableBehavior>();

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

            // 创建触发器实例（包含所有状态）
            var instance = new TriggerInstance(config, ExecutorId, serverTime)
            {
                Behavior = behavior
            };

            syncService.OnTriggerStarted(triggerId, ExecutorId);

            if (behavior is ISchedulableBehavior schedulable)
            {
                _activeInstances[key] = instance;
                _activeBehaviors[key] = schedulable;
                schedulable.Begin(context);
                instance.CurrentState = ETriggerState.Running;

                if (schedulable.State == EBehaviorState.Completed)
                {
                    syncService.OnTriggerCompleted(triggerId, ExecutorId);
                    instance.CurrentState = ETriggerState.Completed;
                    _activeInstances.Remove(key);
                    _activeBehaviors.Remove(key);
                    return instance;
                }

                syncService.OnTriggerProgress(triggerId, ExecutorId, schedulable.ElapsedMs);
            }
            else if (behavior is ISimpleTriggerBehavior simple)
            {
                if (!simple.Evaluate(context))
                {
                    syncService.OnTriggerInterrupted(triggerId, ExecutorId, "Condition failed");
                    instance.CurrentState = ETriggerState.Interrupted;
                    return instance;
                }

                var result = simple.Execute(context);
                if (result.IsInterrupted)
                {
                    syncService.OnTriggerInterrupted(triggerId, ExecutorId, result.FailureReason);
                    instance.CurrentState = ETriggerState.Interrupted;
                }
                else
                {
                    syncService.OnTriggerCompleted(triggerId, ExecutorId);
                    instance.CurrentState = ETriggerState.Completed;
                }
                return instance;
            }

            return instance;
        }

        public bool TryGetBehavior(int triggerId, int executorId, out ISchedulableBehavior behavior)
        {
            return _activeBehaviors.TryGetValue((triggerId, executorId), out behavior);
        }

        public bool TryGetInstance(int triggerId, int executorId, out TriggerInstance instance)
        {
            return _activeInstances.TryGetValue((triggerId, executorId), out instance);
        }

        public void Update(float deltaTimeMs, IBehaviorContext context, ITriggerSyncService syncService)
        {
            var keysToRemove = new System.Collections.Generic.List<(int, int)>();

            foreach (var kvp in _activeBehaviors)
            {
                var behavior = kvp.Value;
                behavior.Update(deltaTimeMs, context);

                if (_activeInstances.TryGetValue(kvp.Key, out var instance))
                {
                    instance.ElapsedMs = behavior.ElapsedMs;
                }

                syncService.OnTriggerProgress(kvp.Key.Item1, kvp.Key.Item2, behavior.ElapsedMs);

                if (behavior.State == EBehaviorState.Completed)
                {
                    syncService.OnTriggerCompleted(kvp.Key.Item1, kvp.Key.Item2);
                    if (_activeInstances.TryGetValue(kvp.Key, out var inst))
                        inst.CurrentState = ETriggerState.Completed;
                    keysToRemove.Add(kvp.Key);
                }
            }

            foreach (var key in keysToRemove)
            {
                _activeBehaviors.Remove(key);
                _activeInstances.Remove(key);
            }
        }

        public void Interrupt(int triggerId, string reason, ITriggerSyncService syncService)
        {
            var key = (triggerId, ExecutorId);
            if (_activeBehaviors.TryGetValue(key, out var behavior))
            {
                behavior.Interrupt(reason);
                syncService.OnTriggerInterrupted(triggerId, ExecutorId, reason);

                if (_activeInstances.TryGetValue(key, out var instance))
                    instance.CurrentState = ETriggerState.Interrupted;

                _activeBehaviors.Remove(key);
                _activeInstances.Remove(key);
            }
        }

        public void CaptureState(System.Collections.Generic.IEnumerable<(int, int)> activeTriggerIds, ITriggerSyncService syncService)
        {
            foreach (var key in activeTriggerIds)
            {
                if (_activeBehaviors.TryGetValue(key, out var behavior) &&
                    _activeInstances.TryGetValue(key, out var instance))
                {
                    var snapshot = TriggerSnapshot.FromInstance(instance);
                    syncService.CaptureSnapshot(key.Item1, key.Item2, snapshot);
                }
            }
        }
    }
}