using System;

namespace AbilityKit.Context
{
    /// <summary>Local entity identity, independent of persisted IDs and business versions.</summary>
    public readonly struct ContextEntityReference
    {
        public readonly Guid RegistryId;
        public readonly long EntityId;
        public readonly long Generation;

        public ContextEntityReference(Guid registryId, long entityId, long generation)
        {
            RegistryId = registryId;
            EntityId = entityId;
            Generation = generation;
        }

        public bool IsValid => RegistryId != Guid.Empty && EntityId > 0 && Generation > 0;
    }

    public enum ContextEntityQueryStatus
    {
        Found = 0,
        InvalidReference = 1,
        Unavailable = 2,
        IdentityMismatch = 3,
        PropertyMissing = 4,
        FieldMissing = 5
    }

    public readonly struct ContextRealtimeValue<T>
    {
        public readonly ContextEntityQueryStatus Status;
        public readonly ContextEntityReference Reference;
        public readonly T Value;
        public bool Found => Status == ContextEntityQueryStatus.Found && Reference.IsValid;

        internal ContextRealtimeValue(ContextEntityQueryStatus status, in ContextEntityReference reference, T value)
        {
            Status = status;
            Reference = reference;
            Value = value;
        }
    }

    /// <summary>
    /// 上下文值来源。
    /// </summary>
    public enum ContextValueSource
    {
        None = 0,
        Realtime = 1,
        Snapshot = 2,
        DefaultValue = 3
    }

    /// <summary>
    /// 上下文值读取模式。
    /// </summary>
    public enum ContextValueReadMode
    {
        RealtimeThenSnapshot = 0,
        RealtimeOnly = 1,
        SnapshotOnly = 2,
        SnapshotThenRealtime = 3
    }

    /// <summary>
    /// 上下文值读取结果。
    /// </summary>
    public readonly struct ContextValueResult<T>
    {
        public ContextValueResult(bool found, T value, ContextValueSource source)
        {
            Found = found;
            Value = value;
            Source = source;
        }

        public bool Found { get; }
        public T Value { get; }
        public ContextValueSource Source { get; }
        public bool IsRealtime => Source == ContextValueSource.Realtime;
        public bool IsSnapshot => Source == ContextValueSource.Snapshot;

        public static ContextValueResult<T> Missing()
        {
            return new ContextValueResult<T>(false, default, ContextValueSource.None);
        }

        public static ContextValueResult<T> FromDefault(T value)
        {
            return new ContextValueResult<T>(true, value, ContextValueSource.DefaultValue);
        }
    }
}
