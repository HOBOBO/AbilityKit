using System;
using System.Collections.Generic;

namespace AbilityKit.Context
{
    public interface IContextSnapshotReader
    {
        int HistoryCount { get; }
        IReadOnlyList<ContextSnapshotType> GetSnapshotTypes();
        ContextSnapshotQueryStatus Query(in ContextSnapshotReference reference, out ContextSnapshotRecord record);
        ContextSnapshotValue<TValue> ReadSnapshot<TSnapshot, TValue>(in ContextSnapshotReference reference, string key)
            where TSnapshot : IImmutableContextSnapshot;
        IReadOnlyList<ContextSnapshotRecord> GetHistory(long entityId, long generation);
        bool TryGetLatest<T>(long entityId, long generation, string kind, out ContextSnapshotRecord record)
            where T : IImmutableContextSnapshot;
        bool TryGetRecord(long entityId, out ContextSnapshotRecord record);
    }

    public enum ContextSnapshotPurpose
    {
        Observation = 0,
        Recovery = 1
    }

    public enum ContextSnapshotQueryStatus
    {
        Found = 0,
        InvalidReference = 1,
        NotCaptured = 2,
        Unavailable = 3,
        IdentityMismatch = 4,
        TypeMismatch = 5,
        FieldMissing = 6
    }

    /// <summary>Store-scoped identity; generation is supplied by the logical owner, not inferred from Version.</summary>
    public readonly struct ContextSnapshotReference
    {
        public readonly Guid StorageId;
        public readonly long SnapshotId;
        public readonly long EntityId;
        public readonly long Generation;

        public ContextSnapshotReference(Guid storageId, long snapshotId, long entityId, long generation)
        {
            StorageId = storageId;
            SnapshotId = snapshotId;
            EntityId = entityId;
            Generation = generation;
        }

        public bool IsValid => StorageId != Guid.Empty && SnapshotId > 0 && EntityId > 0 && Generation > 0;
    }

    public readonly struct ContextSnapshotCapture
    {
        public readonly long Generation;
        public readonly int Frame;
        public readonly string Kind;
        public readonly bool IsDestroyed;

        public ContextSnapshotCapture(long generation, int frame, string kind, bool isDestroyed = false)
        {
            if (generation <= 0) throw new ArgumentOutOfRangeException(nameof(generation));
            if (frame < 0) throw new ArgumentOutOfRangeException(nameof(frame));
            if (string.IsNullOrWhiteSpace(kind)) throw new ArgumentException("Capture kind is required.", nameof(kind));
            Generation = generation;
            Frame = frame;
            Kind = kind;
            IsDestroyed = isDestroyed;
        }
    }

    public sealed class ContextSnapshotType
    {
        public Type PayloadType { get; }
        public string TypeId { get; }
        public int SchemaVersion { get; }
        public ContextSnapshotPurpose Purpose { get; }

        internal ContextSnapshotType(Type payloadType, string typeId, int schemaVersion, ContextSnapshotPurpose purpose)
        {
            PayloadType = payloadType;
            TypeId = typeId;
            SchemaVersion = schemaVersion;
            Purpose = purpose;
        }
    }

    public readonly struct ContextSnapshotValue<T>
    {
        public readonly ContextSnapshotQueryStatus Status;
        public readonly ContextSnapshotRecord Record;
        public readonly T Value;
        public bool Found => Status == ContextSnapshotQueryStatus.Found;

        internal ContextSnapshotValue(ContextSnapshotQueryStatus status, ContextSnapshotRecord record, T value)
        {
            Status = status;
            Record = record;
            Value = value;
        }
    }
}
