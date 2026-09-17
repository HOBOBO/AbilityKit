using System;
using System.Collections.Generic;
using System.Linq;

namespace AbilityKit.Context
{
    public sealed partial class SnapshotStorage
    {
        private readonly Dictionary<Type, ContextSnapshotType> _types = new Dictionary<Type, ContextSnapshotType>();
        private readonly Dictionary<long, ContextSnapshotRecord> _history = new Dictionary<long, ContextSnapshotRecord>();
        private readonly Dictionary<long, List<long>> _historyByEntity = new Dictionary<long, List<long>>();
        private readonly LinkedList<long> _saveOrder = new LinkedList<long>();
        private readonly Dictionary<long, LinkedListNode<long>> _orderNodes = new Dictionary<long, LinkedListNode<long>>();
        private readonly Dictionary<long, int> _retained = new Dictionary<long, int>();
        private long _nextSnapshotId = 1;
        private readonly int _maxRecords;
        private readonly int _maxRecordsPerEntity;

        public SnapshotStorage(int maxRecords = 4096, int maxRecordsPerEntity = 16)
        {
            if (maxRecords <= 0) throw new ArgumentOutOfRangeException(nameof(maxRecords));
            if (maxRecordsPerEntity <= 0) throw new ArgumentOutOfRangeException(nameof(maxRecordsPerEntity));
            _maxRecords = maxRecords;
            _maxRecordsPerEntity = maxRecordsPerEntity;
        }

        public Guid StorageId { get; } = Guid.NewGuid();
        public int HistoryCount { get { lock (_lock) return _history.Count; } }

        public ContextSnapshotType RegisterType<T>(string typeId, int schemaVersion,
            ContextSnapshotPurpose purpose = ContextSnapshotPurpose.Observation) where T : IImmutableContextSnapshot
        {
            if (string.IsNullOrWhiteSpace(typeId)) throw new ArgumentException("Stable type ID is required.", nameof(typeId));
            if (schemaVersion <= 0) throw new ArgumentOutOfRangeException(nameof(schemaVersion));
            if (!Enum.IsDefined(typeof(ContextSnapshotPurpose), purpose)) throw new ArgumentOutOfRangeException(nameof(purpose));
            lock (_lock)
            {
                if (_types.TryGetValue(typeof(T), out var existing))
                {
                    if (existing.TypeId == typeId && existing.SchemaVersion == schemaVersion && existing.Purpose == purpose)
                        return existing;
                    throw new InvalidOperationException("Snapshot type already has a different registration.");
                }
                if (_types.Values.Any(t => t.TypeId == typeId))
                    throw new InvalidOperationException("Snapshot type ID is already registered.");
                var descriptor = new ContextSnapshotType(typeof(T), typeId, schemaVersion, purpose);
                _types.Add(typeof(T), descriptor);
                return descriptor;
            }
        }

        public IReadOnlyList<ContextSnapshotType> GetSnapshotTypes()
        {
            lock (_lock) return _types.Values.OrderBy(t => t.TypeId, StringComparer.Ordinal).ToArray();
        }

        public ContextSnapshotReference SaveManaged<T>(T snapshot, in ContextSnapshotCapture capture)
            where T : IImmutableContextSnapshot
        {
            if (!TrySaveManaged(snapshot, capture, out var reference))
                throw new InvalidOperationException("Snapshot capacity is retained.");
            return reference;
        }

        public bool TrySaveManaged<T>(T snapshot, in ContextSnapshotCapture capture, out ContextSnapshotReference reference)
            where T : IImmutableContextSnapshot
        {
            reference = default;
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (snapshot.EntityId <= 0) throw new ArgumentOutOfRangeException(nameof(snapshot));
            if (capture.Generation <= 0 || capture.Frame < 0 || string.IsNullOrWhiteSpace(capture.Kind))
                throw new ArgumentException("Explicit capture metadata is required.", nameof(capture));
            if (snapshot.Frame != capture.Frame) throw new ArgumentException("Payload and capture frames must match.", nameof(capture));
            if (snapshot is IDestroyableSnapshot)
                throw new ArgumentException("Managed payloads cannot implement mutable destruction hooks.", nameof(snapshot));
            lock (_lock)
            {
                if (!_types.TryGetValue(snapshot.GetType(), out var type))
                    throw new InvalidOperationException("Register the concrete snapshot payload type before saving.");
                if (_nextSnapshotId == long.MaxValue) throw new InvalidOperationException("Snapshot ID space exhausted.");
                _historyByEntity.TryGetValue(snapshot.EntityId, out var entityHistory);
                // Retention is bounded: reject before changing anything if capacity is entirely retained.
                if (entityHistory != null && entityHistory.Count >= _maxRecordsPerEntity && entityHistory.All(IsRetained))
                    return false;
                if (_history.Count >= _maxRecords && _saveOrder.All(IsRetained))
                    return false;

                reference = new ContextSnapshotReference(StorageId, _nextSnapshotId++, snapshot.EntityId, capture.Generation);
                var record = new ContextSnapshotRecord(snapshot, snapshot.Version, capture.Frame, TimeUtil.CurrentTimeMs,
                    reference, type.TypeId, type.SchemaVersion, type.Purpose, capture.Kind, capture.IsDestroyed);
                _history.Add(reference.SnapshotId, record);
                if (entityHistory == null)
                {
                    entityHistory = new List<long>();
                    _historyByEntity.Add(snapshot.EntityId, entityHistory);
                }
                entityHistory.Add(reference.SnapshotId);
                _orderNodes.Add(reference.SnapshotId, _saveOrder.AddLast(reference.SnapshotId));
                SetLatest(record);
                var newId = reference.SnapshotId;
                while (entityHistory.Count > _maxRecordsPerEntity)
                    RemoveHistory(entityHistory.First(id => id != newId && !IsRetained(id)));
                while (_history.Count > _maxRecords)
                    RemoveHistory(_saveOrder.First(id => id != newId && !IsRetained(id)));
                return true;
            }
        }

        public ContextSnapshotQueryStatus Query(in ContextSnapshotReference reference, out ContextSnapshotRecord record)
        {
            lock (_lock)
            {
                record = default;
                if (!reference.IsValid) return ContextSnapshotQueryStatus.InvalidReference;
                if (reference.StorageId != StorageId) return ContextSnapshotQueryStatus.IdentityMismatch;
                if (!_history.TryGetValue(reference.SnapshotId, out var found))
                    return reference.SnapshotId < _nextSnapshotId ? ContextSnapshotQueryStatus.Unavailable : ContextSnapshotQueryStatus.NotCaptured;
                if (found.Reference.EntityId != reference.EntityId || found.Reference.Generation != reference.Generation)
                    return ContextSnapshotQueryStatus.IdentityMismatch;
                record = found;
                return ContextSnapshotQueryStatus.Found;
            }
        }

        public ContextSnapshotValue<TValue> ReadSnapshot<TSnapshot, TValue>(in ContextSnapshotReference reference, string key)
            where TSnapshot : IImmutableContextSnapshot
        {
            var status = Query(reference, out var record);
            if (status != ContextSnapshotQueryStatus.Found) return new ContextSnapshotValue<TValue>(status, record, default);
            if (!(record.Snapshot is TSnapshot snapshot))
                return new ContextSnapshotValue<TValue>(ContextSnapshotQueryStatus.TypeMismatch, record, default);
            if (string.IsNullOrEmpty(key) || !snapshot.TryGetValue(key, out TValue value))
                return new ContextSnapshotValue<TValue>(ContextSnapshotQueryStatus.FieldMissing, record, default);
            return new ContextSnapshotValue<TValue>(ContextSnapshotQueryStatus.Found, record, value);
        }

        public IReadOnlyList<ContextSnapshotRecord> GetHistory(long entityId, long generation)
        {
            lock (_lock)
                return _historyByEntity.TryGetValue(entityId, out var ids)
                    ? ids.Select(id => _history[id]).Where(r => r.Reference.Generation == generation).ToArray()
                    : Array.Empty<ContextSnapshotRecord>();
        }

        public bool TryGetLatest<T>(long entityId, long generation, string kind, out ContextSnapshotRecord record)
            where T : IImmutableContextSnapshot
        {
            lock (_lock)
            {
                record = default;
                if (!_historyByEntity.TryGetValue(entityId, out var ids)) return false;
                for (var i = ids.Count - 1; i >= 0; i--)
                {
                    var candidate = _history[ids[i]];
                    if (candidate.Reference.Generation != generation || !(candidate.Snapshot is T) ||
                        (kind != null && candidate.Kind != kind)) continue;
                    record = candidate;
                    return true;
                }
                return false;
            }
        }

        public IDisposable Retain(in ContextSnapshotReference reference)
        {
            lock (_lock)
            {
                if (Query(reference, out _) != ContextSnapshotQueryStatus.Found)
                    throw new InvalidOperationException("Cannot retain an unavailable snapshot.");
                _retained.TryGetValue(reference.SnapshotId, out var count);
                _retained[reference.SnapshotId] = checked(count + 1);
                return new Retention(this, reference.SnapshotId);
            }
        }

        public int PruneBeforeFrame(int firstRetainedFrame)
        {
            if (firstRetainedFrame < 0) throw new ArgumentOutOfRangeException(nameof(firstRetainedFrame));
            lock (_lock)
            {
                var ids = _saveOrder.Where(id => _history[id].Frame < firstRetainedFrame && !IsRetained(id)).ToArray();
                foreach (var id in ids) RemoveHistory(id);
                return ids.Length;
            }
        }

        public bool RemoveSnapshot(in ContextSnapshotReference reference)
        {
            lock (_lock)
            {
                if (Query(reference, out _) != ContextSnapshotQueryStatus.Found) return false;
                RemoveHistory(reference.SnapshotId);
                return true;
            }
        }

        private bool IsRetained(long id) => _retained.ContainsKey(id);

        private void SetLatest(in ContextSnapshotRecord record)
        {
            RemoveIndexes(record.EntityId);
            var latest = new SnapshotRecord
            {
                Snapshot = record.Snapshot, Version = record.Version, Frame = record.Frame, SavedAtMs = record.SavedAtMs,
                SnapshotId = record.Reference.SnapshotId,
                SourceEntityId = record.Snapshot is ISourceContext source ? source.SourceEntityId : 0,
                OwnerEntityId = record.Snapshot is IOwnerContext owner ? owner.OwnerEntityId : 0
            };
            _snapshots[record.EntityId] = latest;
            _latestFrameByEntity[record.EntityId] = latest.Frame;
            _latestVersionByEntity[record.EntityId] = latest.Version;
            if (latest.SourceEntityId > 0) AddIndex(_bySource, latest.SourceEntityId, record.EntityId);
            if (latest.OwnerEntityId > 0) AddIndex(_byOwner, latest.OwnerEntityId, record.EntityId);
        }

        private void RemoveHistory(long id)
        {
            var record = _history[id];
            _history.Remove(id);
            _saveOrder.Remove(_orderNodes[id]);
            _orderNodes.Remove(id);
            _retained.Remove(id);
            var ids = _historyByEntity[record.EntityId];
            ids.Remove(id);
            if (ids.Count == 0) _historyByEntity.Remove(record.EntityId);
            if (_snapshots.TryGetValue(record.EntityId, out var latest) && latest.SnapshotId == id)
            {
                if (ids.Count > 0) SetLatest(_history[ids[ids.Count - 1]]);
                else
                {
                    RemoveIndexes(record.EntityId);
                    _snapshots.Remove(record.EntityId);
                    _latestFrameByEntity.Remove(record.EntityId);
                    _latestVersionByEntity.Remove(record.EntityId);
                }
            }
        }

        private void RemoveHistoryForEntity(long entityId)
        {
            if (_historyByEntity.TryGetValue(entityId, out var ids))
                foreach (var id in ids.ToArray()) RemoveHistory(id);
        }

        private void ClearHistory()
        {
            _history.Clear();
            _historyByEntity.Clear();
            _saveOrder.Clear();
            _orderNodes.Clear();
            _retained.Clear();
            // IDs never rewind, so outstanding references and leases cannot alias new records.
        }

        private sealed class Retention : IDisposable
        {
            private readonly SnapshotStorage _storage;
            private readonly long _id;
            private bool _disposed;
            public Retention(SnapshotStorage storage, long id) { _storage = storage; _id = id; }
            public void Dispose()
            {
                lock (_storage._lock)
                {
                    if (_disposed) return;
                    _disposed = true;
                    if (!_storage._retained.TryGetValue(_id, out var count)) return;
                    if (count == 1) _storage._retained.Remove(_id);
                    else _storage._retained[_id] = count - 1;
                }
            }
        }
    }
}
