using System;
using System.Collections.Generic;
using AbilityKit.Triggering.Runtime.Instance;

namespace AbilityKit.Triggering.Runtime.Sync
{
    /// <summary>
    /// 触发器同步服务实现
    /// </summary>
    public class TriggerSyncService : ITriggerSyncService
    {
        private readonly Dictionary<(int, int), TriggerSnapshot> _snapshots = new Dictionary<(int, int), TriggerSnapshot>();
        private readonly List<TriggerSnapshot> _snapshotHistory = new List<TriggerSnapshot>();

        public event Action<int, int> OnStarted;
        public event Action<int, int, long> OnProgress;
        public event Action<int, int> OnCompleted;
        public event Action<int, int, string> OnInterrupted;

        public void OnTriggerStarted(int triggerId, int executorId)
        {
            OnStarted?.Invoke(triggerId, executorId);
        }

        public void OnTriggerProgress(int triggerId, int executorId, long elapsedMs)
        {
            OnProgress?.Invoke(triggerId, executorId, elapsedMs);
        }

        public void OnTriggerCompleted(int triggerId, int executorId)
        {
            OnCompleted?.Invoke(triggerId, executorId);
            RemoveSnapshot(triggerId, executorId);
        }

        public void OnTriggerInterrupted(int triggerId, int executorId, string reason)
        {
            OnInterrupted?.Invoke(triggerId, executorId, reason);
            RemoveSnapshot(triggerId, executorId);
        }

        public void CaptureSnapshot(int triggerId, int executorId, TriggerSnapshot snapshot)
        {
            var key = (triggerId, executorId);
            _snapshots[key] = snapshot;
            _snapshotHistory.Add(snapshot);
        }

        public bool TryGetSnapshot(int triggerId, int executorId, out TriggerSnapshot snapshot)
        {
            return _snapshots.TryGetValue((triggerId, executorId), out snapshot);
        }

        public IReadOnlyList<TriggerSnapshot> GetSnapshotHistory()
        {
            return _snapshotHistory;
        }

        public void ClearHistory()
        {
            _snapshotHistory.Clear();
        }

        private void RemoveSnapshot(int triggerId, int executorId)
        {
            _snapshots.Remove((triggerId, executorId));
        }
    }
}