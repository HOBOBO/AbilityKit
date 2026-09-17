using System;
using System.Collections.Generic;
using System.Linq;

namespace AbilityKit.Context
{
    /// <summary>
    /// 快照存储管理器
    /// 保存实体最新视图及有界受管快照历史，不负责落盘或自动采集。
    /// </summary>
    public sealed partial class SnapshotStorage : IContextSnapshotReader
    {
        private sealed class SnapshotRecord
        {
            public IContextSnapshot Snapshot;
            public long Version;
            public int Frame;
            public long SavedAtMs;
            public long SourceEntityId;
            public long OwnerEntityId;
            public long SnapshotId;
        }

        private readonly Dictionary<long, SnapshotRecord> _snapshots = new Dictionary<long, SnapshotRecord>();
        private readonly Dictionary<long, HashSet<long>> _bySource = new Dictionary<long, HashSet<long>>();
        private readonly Dictionary<long, HashSet<long>> _byOwner = new Dictionary<long, HashSet<long>>();
        private readonly Dictionary<long, int> _latestFrameByEntity = new Dictionary<long, int>();
        private readonly Dictionary<long, long> _latestVersionByEntity = new Dictionary<long, long>();
        private readonly object _lock = new object();

        public void Save(IContextSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (snapshot is IImmutableContextSnapshot)
                throw new InvalidOperationException("Immutable snapshots must use SaveManaged with explicit capture metadata.");

            lock (_lock)
            {
                if (_historyByEntity.ContainsKey(snapshot.EntityId))
                    throw new InvalidOperationException("Cannot replace managed history with a legacy snapshot. Remove the entity snapshots first.");
                RemoveIndexes(snapshot.EntityId);

                var record = new SnapshotRecord
                {
                    Snapshot = snapshot,
                    Version = snapshot is IVersionedContextSnapshot versioned ? versioned.Version : GetNextVersion(snapshot.EntityId),
                    Frame = snapshot is IVersionedContextSnapshot versionedSnapshot ? versionedSnapshot.Frame : 0,
                    SavedAtMs = TimeUtil.CurrentTimeMs,
                    SourceEntityId = snapshot is ISourceContext sourceContext ? sourceContext.SourceEntityId : 0,
                    OwnerEntityId = snapshot is IOwnerContext ownerContext ? ownerContext.OwnerEntityId : 0
                };

                _snapshots[snapshot.EntityId] = record;
                _latestFrameByEntity[snapshot.EntityId] = record.Frame;
                _latestVersionByEntity[snapshot.EntityId] = record.Version;

                if (record.SourceEntityId > 0)
                    AddIndex(_bySource, record.SourceEntityId, snapshot.EntityId);

                if (record.OwnerEntityId > 0)
                    AddIndex(_byOwner, record.OwnerEntityId, snapshot.EntityId);
            }
        }

        public IContextSnapshot Get(long entityId)
        {
            lock (_lock)
            {
                return _snapshots.TryGetValue(entityId, out var record) ? record.Snapshot : null;
            }
        }

        public bool TryGetRecord(long entityId, out ContextSnapshotRecord record)
        {
            lock (_lock)
            {
                if (_snapshots.TryGetValue(entityId, out var snapshotRecord))
                {
                    record = snapshotRecord.SnapshotId != 0
                        ? _history[snapshotRecord.SnapshotId]
                        : new ContextSnapshotRecord(snapshotRecord.Snapshot, snapshotRecord.Version, snapshotRecord.Frame, snapshotRecord.SavedAtMs);
                    return true;
                }
            }

            record = default;
            return false;
        }

        public IEnumerable<IContextSnapshot> GetBySource(long sourceEntityId)
        {
            lock (_lock)
            {
                if (!_bySource.TryGetValue(sourceEntityId, out var ids))
                    return Enumerable.Empty<IContextSnapshot>();

                return ids.Where(id => _snapshots.ContainsKey(id)).Select(id => _snapshots[id].Snapshot).ToList();
            }
        }

        public IEnumerable<IContextSnapshot> GetByOwner(long ownerEntityId)
        {
            lock (_lock)
            {
                if (!_byOwner.TryGetValue(ownerEntityId, out var ids))
                    return Enumerable.Empty<IContextSnapshot>();

                return ids.Where(id => _snapshots.ContainsKey(id)).Select(id => _snapshots[id].Snapshot).ToList();
            }
        }

        public void MarkDestroyed(long entityId)
        {
            lock (_lock)
            {
                if (_snapshots.TryGetValue(entityId, out var record) && record.Snapshot is IDestroyableSnapshot destroyable)
                    destroyable.MarkDestroyed();
            }
        }

        public bool Remove(long entityId)
        {
            lock (_lock)
            {
                var existed = _snapshots.ContainsKey(entityId);
                RemoveHistoryForEntity(entityId);
                if (!_snapshots.TryGetValue(entityId, out var record))
                    return existed;

                RemoveIndexes(entityId);
                _snapshots.Remove(entityId);
                _latestFrameByEntity.Remove(entityId);
                _latestVersionByEntity.Remove(entityId);
                return true;
            }
        }

        public void RemoveFromEntityId(long firstEntityId)
        {
            lock (_lock)
            {
                var ids = _snapshots.Keys.Where(id => id >= firstEntityId).ToArray();
                foreach (var id in ids) Remove(id);
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _snapshots.Clear();
                _bySource.Clear();
                _byOwner.Clear();
                _latestFrameByEntity.Clear();
                _latestVersionByEntity.Clear();
                ClearHistory();
            }
        }

        public int Count
        {
            get
            {
                lock (_lock)
                    return _snapshots.Count;
            }
        }

        public long GetLatestVersion(long entityId)
        {
            lock (_lock)
                return _latestVersionByEntity.TryGetValue(entityId, out var version) ? version : 0;
        }

        public int GetLatestFrame(long entityId)
        {
            lock (_lock)
                return _latestFrameByEntity.TryGetValue(entityId, out var frame) ? frame : 0;
        }

        private long GetNextVersion(long entityId)
        {
            return _latestVersionByEntity.TryGetValue(entityId, out var current) ? current + 1 : 1;
        }

        private static void AddIndex(Dictionary<long, HashSet<long>> index, long key, long entityId)
        {
            if (!index.TryGetValue(key, out var ids))
            {
                ids = new HashSet<long>();
                index[key] = ids;
            }

            ids.Add(entityId);
        }

        private void RemoveIndexes(long entityId)
        {
            if (_snapshots.TryGetValue(entityId, out var existing))
            {
                if (existing.SourceEntityId > 0 && _bySource.TryGetValue(existing.SourceEntityId, out var sourceIds))
                {
                    sourceIds.Remove(entityId);
                    if (sourceIds.Count == 0)
                        _bySource.Remove(existing.SourceEntityId);
                }

                if (existing.OwnerEntityId > 0 && _byOwner.TryGetValue(existing.OwnerEntityId, out var ownerIds))
                {
                    ownerIds.Remove(entityId);
                    if (ownerIds.Count == 0)
                        _byOwner.Remove(existing.OwnerEntityId);
                }
            }
        }
    }

    public interface ISourceContext
    {
        long SourceEntityId { get; }
    }

    public interface IOwnerContext
    {
        long OwnerEntityId { get; }
    }

    public interface IDestroyableSnapshot
    {
        bool IsDestroyed { get; }
        void MarkDestroyed();
    }
}
