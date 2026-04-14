using AbilityKit.Triggering.Runtime.Instance;

namespace AbilityKit.Triggering.Runtime.Sync
{
    /// <summary>
    /// 触发器同步服务接口
    /// </summary>
    public interface ITriggerSyncService
    {
        void OnTriggerStarted(int triggerId, int executorId);
        void OnTriggerProgress(int triggerId, int executorId, long elapsedMs);
        void OnTriggerCompleted(int triggerId, int executorId);
        void OnTriggerInterrupted(int triggerId, int executorId, string reason);
        void CaptureSnapshot(int triggerId, int executorId, TriggerSnapshot snapshot);
        bool TryGetSnapshot(int triggerId, int executorId, out TriggerSnapshot snapshot);
    }
}