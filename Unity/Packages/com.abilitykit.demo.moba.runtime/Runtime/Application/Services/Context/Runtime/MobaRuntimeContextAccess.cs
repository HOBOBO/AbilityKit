using AbilityKit.Context;

namespace AbilityKit.Demo.Moba.Services
{
    public readonly struct MobaRuntimeContextReference
    {
        public MobaRuntimeContextReference(long contextId, long version)
        {
            ContextId = contextId;
            Version = version;
            Identity = default;
        }

        public MobaRuntimeContextReference(in ContextEntityReference identity, long version)
            : this(identity.EntityId, version, identity)
        {
        }

        private MobaRuntimeContextReference(long contextId, long version, in ContextEntityReference identity)
        {
            ContextId = contextId;
            Version = version;
            Identity = identity;
        }

        public long ContextId { get; }
        public long Version { get; }
        public ContextEntityReference Identity { get; }
        public bool HasIdentity => Identity.RegistryId != System.Guid.Empty || Identity.EntityId != 0L || Identity.Generation != 0L;
        public bool IsValid => ContextId > 0L && (!HasIdentity || (Identity.IsValid && Identity.EntityId == ContextId && Version > 0L));

        public static MobaRuntimeContextReference FromIdentityOrLegacy(long contextId, long version, in ContextEntityReference identity)
        {
            return new MobaRuntimeContextReference(contextId, version, identity);
        }
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
        InvalidReadMode = 10,
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
        public bool IsIdentityChecked => Found && Reference.HasIdentity;
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

        public static MobaRuntimeContextValueResult<TValue> GetRuntimeContextValue<TValue, TProperty>(
            this object payload,
            MobaRuntimeContextService contexts,
            string key,
            ContextValueReadMode mode = ContextValueReadMode.RealtimeThenSnapshot)
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

            var failure = contexts.ResolveRuntimeContext<TProperty>(reference, mode, out var provider, out var source, out var snapshot);
            if (failure != MobaRuntimeContextValueFailure.None)
                return MobaRuntimeContextValueResult<TValue>.Fail(failure, reference, reference.Version);
            var actualVersion = 0L;
            if (reference.Version > 0L)
            {
                if (!provider.TryGetValue(MobaRuntimeContextKeys.Version, out actualVersion))
                    return MobaRuntimeContextValueResult<TValue>.Fail(MobaRuntimeContextValueFailure.VersionUnavailable, in reference, reference.Version);

                if (actualVersion != reference.Version)
                    return MobaRuntimeContextValueResult<TValue>.Fail(MobaRuntimeContextValueFailure.VersionMismatch, in reference, reference.Version, actualVersion);
            }

            var found = provider.TryGetValue(key, out TValue value);
            if (contexts.IsDisposed)
                return MobaRuntimeContextValueResult<TValue>.Fail(MobaRuntimeContextValueFailure.ContextServiceDisposed, reference, reference.Version, actualVersion);
            if (reference.HasIdentity && source == ContextValueSource.Realtime && contexts.Registry.Query(reference.Identity) != ContextEntityQueryStatus.Found)
                return MobaRuntimeContextValueResult<TValue>.Fail(MobaRuntimeContextValueFailure.IdentityMismatch, reference, reference.Version, actualVersion);
            if (reference.HasIdentity && source == ContextValueSource.Snapshot && contexts.SnapshotReader.Query(snapshot, out _) != ContextSnapshotQueryStatus.Found)
                return MobaRuntimeContextValueResult<TValue>.Fail(MobaRuntimeContextValueFailure.SnapshotUnavailable, reference, reference.Version, actualVersion);
            if (found)
                return MobaRuntimeContextValueResult<TValue>.Success(value, source, in reference, actualVersion, snapshot);

            return MobaRuntimeContextValueResult<TValue>.Fail(MobaRuntimeContextValueFailure.ValueMissing, in reference, reference.Version, actualVersion);
        }

        public static bool TryGetRuntimeContextValue<TValue, TProperty>(
            this object payload,
            MobaRuntimeContextService contexts,
            string key,
            out TValue value,
            ContextValueReadMode mode = ContextValueReadMode.RealtimeThenSnapshot)
            where TProperty : class, IProperty
        {
            var result = payload.GetRuntimeContextValue<TValue, TProperty>(contexts, key, mode);
            value = result.Value;
            return result.Found;
        }
    }
}
