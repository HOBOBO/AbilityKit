using AbilityKit.Triggering.Runtime.Behavior;
using AbilityKit.Triggering.Runtime.Config.Plans;
using AbilityKit.Triggering.Runtime.Instance;

namespace AbilityKit.Triggering.Runtime.Sync
{
    /// <summary>
    /// 同步触发器执行器接口
    /// </summary>
    public interface ISyncedTriggerExecutor
    {
        int ExecutorId { get; }
        ITriggerInstance Execute(int triggerId, ITriggerPlanConfig config, IBehaviorContext context, ITriggerSyncService syncService, long serverTime = 0);
        bool TryGetBehavior(int triggerId, int executorId, out ISchedulableBehavior behavior);
        void Update(float deltaTimeMs, IBehaviorContext context, ITriggerSyncService syncService);
        void Interrupt(int triggerId, string reason, ITriggerSyncService syncService);
        void CaptureState(System.Collections.Generic.IEnumerable<(int, int)> activeTriggerIds, ITriggerSyncService syncService);
    }
}