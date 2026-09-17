namespace AbilityKit.Context
{
    /// <summary>
    /// 快照接口
    /// 实体瞬时状态的基础身份；受管不可变载荷使用 IImmutableContextSnapshot。
    /// </summary>
    public interface IContextSnapshot
    {
        long EntityId { get; }
        long CreatedAtMs { get; }
    }

    public interface IVersionedContextSnapshot : IContextSnapshot
    {
        long Version { get; }
        int Frame { get; }
    }

    /// <summary>
    /// Detached snapshot payload. Implementations must never expose mutable runtime
    /// references or change after construction; collections must also be immutable.
    /// </summary>
    public interface IImmutableContextSnapshot : IVersionedContextSnapshot, IContextValueProvider
    {
    }

    public readonly struct ContextSnapshotRecord
    {
        public readonly IContextSnapshot Snapshot;
        public readonly long Version;
        public readonly int Frame;
        public readonly long SavedAtMs;
        public readonly ContextSnapshotReference Reference;
        public readonly string TypeId;
        public readonly int SchemaVersion;
        public readonly ContextSnapshotPurpose Purpose;
        public readonly string Kind;
        public readonly bool IsDestroyed;

        public ContextSnapshotRecord(IContextSnapshot snapshot, long version, int frame, long savedAtMs)
            : this(snapshot, version, frame, savedAtMs, default, null, 0, ContextSnapshotPurpose.Observation, null,
                snapshot is IDestroyableSnapshot destroyable && destroyable.IsDestroyed)
        {
        }

        internal ContextSnapshotRecord(IContextSnapshot snapshot, long version, int frame, long savedAtMs,
            ContextSnapshotReference reference, string typeId, int schemaVersion,
            ContextSnapshotPurpose purpose, string kind, bool isDestroyed)
        {
            Snapshot = snapshot;
            Version = version;
            Frame = frame;
            SavedAtMs = savedAtMs;
            Reference = reference;
            TypeId = typeId;
            SchemaVersion = schemaVersion;
            Purpose = purpose;
            Kind = kind;
            IsDestroyed = isDestroyed;
        }

        public long EntityId => Snapshot?.EntityId ?? 0;
        public bool IsValid => Snapshot != null;
        public bool IsManaged => Reference.IsValid;
    }
}
