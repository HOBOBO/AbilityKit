using AbilityKit.Context;

namespace AbilityKit.Demo.Moba.Services
{
    /// <summary>
    /// 在当前上下文服务中定位指定代际与业务版本。
    /// </summary>
    public readonly struct MobaRuntimeContextReference
    {
        /// <summary>
        /// 保存本地实体身份及正数业务版本；不补全缺失的身份。
        /// </summary>
        public MobaRuntimeContextReference(in ContextEntityReference identity, long version)
        {
            Version = version;
            Identity = identity;
        }

        /// <summary>
        /// 实体身份中的本地上下文编号。
        /// </summary>
        public long ContextId => Identity.EntityId;
        /// <summary>
        /// 读取时必须匹配的业务版本。
        /// </summary>
        public long Version { get; }
        /// <summary>
        /// 包含注册表、实体编号和代际的本地身份。
        /// </summary>
        public ContextEntityReference Identity { get; }
        /// <summary>
        /// 引用具有完整身份与正数业务版本；不代表实体仍可读。
        /// </summary>
        public bool IsValid => Identity.IsValid && Version > 0L;
    }

    public enum MobaRuntimeContextValueFailure
    {
        None = 0,
        MissingContextService = 1,
        MissingPayload = 2,
        MissingRuntimeContext = 3,
        InvalidRuntimeContext = 4,
        VersionUnavailable = 5,
        VersionMismatch = 6,
        ValueMissing = 7,
        IdentityMismatch = 8,
        ContextServiceDisposed = 9,
        ContextUnavailable = 11,
        SnapshotUnavailable = 12
    }

    public readonly struct MobaRuntimeContextValueResult<TValue>
    {
        public MobaRuntimeContextValueResult(
            bool found,
            TValue value,
            ContextValueSource source,
            MobaRuntimeContextValueFailure failure,
            MobaRuntimeContextReference reference,
            long expectedVersion,
            long actualVersion,
            ContextSnapshotReference resolvedSnapshot = default)
        {
            Found = found;
            Value = value;
            Source = source;
            Failure = failure;
            Reference = reference;
            ExpectedVersion = expectedVersion;
            ActualVersion = actualVersion;
            ResolvedSnapshot = resolvedSnapshot;
        }

        public bool Found { get; }
        public TValue Value { get; }
        public ContextValueSource Source { get; }
        public MobaRuntimeContextValueFailure Failure { get; }
        public MobaRuntimeContextReference Reference { get; }
        public long ExpectedVersion { get; }
        public long ActualVersion { get; }
        public ContextSnapshotReference ResolvedSnapshot { get; }
        public bool IsVersionChecked => ExpectedVersion > 0L;

        public static MobaRuntimeContextValueResult<TValue> Success(TValue value, ContextValueSource source, in MobaRuntimeContextReference reference, long actualVersion,
            ContextSnapshotReference resolvedSnapshot = default)
        {
            return new MobaRuntimeContextValueResult<TValue>(true, value, source, MobaRuntimeContextValueFailure.None, reference, reference.Version, actualVersion, resolvedSnapshot);
        }

        public static MobaRuntimeContextValueResult<TValue> Fail(
            MobaRuntimeContextValueFailure failure,
            in MobaRuntimeContextReference reference = default,
            long expectedVersion = 0L,
            long actualVersion = 0L)
        {
            return new MobaRuntimeContextValueResult<TValue>(false, default, ContextValueSource.None, failure, reference, expectedVersion, actualVersion);
        }
    }

    public interface IMobaRuntimeContextPayload
    {
        bool TryGetRuntimeContext(out MobaRuntimeContextReference reference);
    }

    public static class MobaRuntimeContextAccessExtensions
    {
        public static bool TryResolveRuntimeContext(this object payload, out MobaRuntimeContextReference reference)
        {
            reference = default;
            if (payload == null) return false;

            if (payload is MobaRuntimeContextReference direct && direct.IsValid)
            {
                reference = direct;
                return true;
            }

            if (payload is MobaCombatExecutionContext executionContext)
            {
                return executionContext.TryGetRuntimeContext(out reference);
            }

            if (payload is IMobaRuntimeContextPayload provider && provider.TryGetRuntimeContext(out reference) && reference.IsValid)
                return true;

            return false;
        }

        /// <summary>
        /// 读取指定身份和业务版本的实时属性，不读取退场记录。
        /// </summary>
        public static MobaRuntimeContextValueResult<TValue> ReadRealtimeContextValue<TValue, TProperty>(
            this object payload, MobaRuntimeContextService contexts, string key)
            where TProperty : class, IProperty => ReadValue<TValue, TProperty>(payload, contexts, key, false);

        /// <summary>
        /// 读取同代 Buff 的退场最终值，不读取实时属性或任意事件快照。
        /// </summary>
        public static MobaRuntimeContextValueResult<TValue> ReadFinalBuffContextValue<TValue>(
            this object payload, MobaRuntimeContextService contexts, string key) =>
            ReadValue<TValue, MobaBuffContextProperty>(payload, contexts, key, true);

        private static MobaRuntimeContextValueResult<TValue> ReadValue<TValue, TProperty>(
            object payload,
            MobaRuntimeContextService contexts,
            string key,
            bool finalBuff)
            where TProperty : class, IProperty
        {
            if (contexts == null)
                return MobaRuntimeContextValueResult<TValue>.Fail(MobaRuntimeContextValueFailure.MissingContextService);

            if (payload == null)
                return MobaRuntimeContextValueResult<TValue>.Fail(MobaRuntimeContextValueFailure.MissingPayload);

            var reference = payload is MobaRuntimeContextReference direct ? direct : default;
            if (!(payload is MobaRuntimeContextReference) && !payload.TryResolveRuntimeContext(out reference))
                return MobaRuntimeContextValueResult<TValue>.Fail(MobaRuntimeContextValueFailure.MissingRuntimeContext);

            if (!reference.IsValid)
                return MobaRuntimeContextValueResult<TValue>.Fail(MobaRuntimeContextValueFailure.InvalidRuntimeContext, in reference);

            ContextSnapshotReference snapshot = default;
            var source = finalBuff ? ContextValueSource.Snapshot : ContextValueSource.Realtime;
            var failure = finalBuff
                ? contexts.ResolveFinalBuffContext(reference, out var provider, out snapshot)
                : contexts.ResolveRealtimeContext<TProperty>(reference, out provider);
            if (failure != MobaRuntimeContextValueFailure.None)
                return MobaRuntimeContextValueResult<TValue>.Fail(failure, reference, reference.Version);
            if (!provider.TryGetValue(MobaRuntimeContextKeys.Version, out long actualVersion))
                return MobaRuntimeContextValueResult<TValue>.Fail(MobaRuntimeContextValueFailure.VersionUnavailable, in reference, reference.Version);

            if (actualVersion != reference.Version)
                return MobaRuntimeContextValueResult<TValue>.Fail(MobaRuntimeContextValueFailure.VersionMismatch, in reference, reference.Version, actualVersion);

            var found = provider.TryGetValue(key, out TValue value);
            if (contexts.IsDisposed)
                return MobaRuntimeContextValueResult<TValue>.Fail(MobaRuntimeContextValueFailure.ContextServiceDisposed, reference, reference.Version, actualVersion);
            if (source == ContextValueSource.Realtime && contexts.Registry.Query(reference.Identity) != ContextEntityQueryStatus.Found)
                return MobaRuntimeContextValueResult<TValue>.Fail(MobaRuntimeContextValueFailure.IdentityMismatch, reference, reference.Version, actualVersion);
            if (source == ContextValueSource.Snapshot && contexts.SnapshotReader.Query(snapshot, out _) != ContextSnapshotQueryStatus.Found)
                return MobaRuntimeContextValueResult<TValue>.Fail(MobaRuntimeContextValueFailure.SnapshotUnavailable, reference, reference.Version, actualVersion);
            if (found)
                return MobaRuntimeContextValueResult<TValue>.Success(value, source, in reference, actualVersion, snapshot);

            return MobaRuntimeContextValueResult<TValue>.Fail(MobaRuntimeContextValueFailure.ValueMissing, in reference, reference.Version, actualVersion);
        }

    }
}
