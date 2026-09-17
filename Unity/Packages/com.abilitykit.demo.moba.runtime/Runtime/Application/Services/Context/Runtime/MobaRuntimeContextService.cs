using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Context;
using AbilityKit.Demo.Moba.Components;
using System;

namespace AbilityKit.Demo.Moba.Services
{
    [WorldService(typeof(MobaRuntimeContextService), WorldLifetime.Scoped)]
    public sealed class MobaRuntimeContextService : IService
    {
        private readonly ContextRegistry _registry = new ContextRegistry();
        private readonly SnapshotStorage _snapshots = new SnapshotStorage();
        private readonly ContextRealtimeProviderRegistry _realtimeProviders = new ContextRealtimeProviderRegistry();
        private readonly MobaBuffRealtimeContextProvider _buffProvider = new MobaBuffRealtimeContextProvider();
        private readonly Dictionary<long, long> _generations = new Dictionary<long, long>();
        private readonly ContextValueResolver _resolver;
        private bool _disposed;

        public MobaRuntimeContextService()
        {
            _snapshots.RegisterType<MobaBuffContextSnapshot>("moba.buff.state", 1);
            _realtimeProviders.Register<MobaBuffContextProperty>(_buffProvider);
            _resolver = new ContextValueResolver(_registry, _snapshots, _realtimeProviders);
            _registry.Subscribe(OnContextEvent);
        }

        public ContextRegistry Registry => _registry;
        public SnapshotStorage Snapshots => _snapshots;
        public IContextSnapshotReader SnapshotReader => _snapshots;
        public ContextValueResolver Resolver => _resolver;

        public bool IsDisposed => _disposed;

        public bool TryGetBuffReference(BuffRuntime runtime, out MobaRuntimeContextReference reference)
        {
            reference = default;
            if (_disposed || runtime == null || !_buffProvider.IsBoundTo(runtime.RuntimeContextId, runtime) ||
                !_registry.TryGetReference(runtime.RuntimeContextId, out var identity)) return false;
            if (runtime.RuntimeContextIdentity.IsValid && _registry.Query(runtime.RuntimeContextIdentity) != ContextEntityQueryStatus.Found) return false;
            reference = new MobaRuntimeContextReference(identity, runtime.RuntimeContextVersion);
            return reference.IsValid;
        }

        internal MobaRuntimeContextValueFailure ResolveRuntimeContext<TProperty>(in MobaRuntimeContextReference reference,
            ContextValueReadMode mode, out IContextValueProvider provider, out ContextValueSource source,
            out ContextSnapshotReference snapshot)
            where TProperty : class, IProperty
        {
            provider = null;
            source = ContextValueSource.None;
            snapshot = default;
            if (_disposed) return MobaRuntimeContextValueFailure.ContextServiceDisposed;
            if (mode != ContextValueReadMode.RealtimeOnly && mode != ContextValueReadMode.SnapshotOnly &&
                mode != ContextValueReadMode.RealtimeThenSnapshot && mode != ContextValueReadMode.SnapshotThenRealtime)
                return MobaRuntimeContextValueFailure.InvalidReadMode;
            var identityStatus = reference.HasIdentity ? _registry.Query(reference.Identity) : ContextEntityQueryStatus.Found;
            if (identityStatus == ContextEntityQueryStatus.IdentityMismatch)
                return MobaRuntimeContextValueFailure.IdentityMismatch;
            if (identityStatus == ContextEntityQueryStatus.InvalidReference)
                return MobaRuntimeContextValueFailure.InvalidRuntimeContext;

            if (mode == ContextValueReadMode.SnapshotOnly || mode == ContextValueReadMode.SnapshotThenRealtime)
            {
                if (TryResolveSnapshot<TProperty>(reference, out provider, out snapshot))
                {
                    source = ContextValueSource.Snapshot;
                    return MobaRuntimeContextValueFailure.None;
                }
                if (mode == ContextValueReadMode.SnapshotOnly)
                    return reference.HasIdentity ? MobaRuntimeContextValueFailure.SnapshotUnavailable : MobaRuntimeContextValueFailure.ValueMissing;
            }

            if (identityStatus == ContextEntityQueryStatus.Found)
            {
                TProperty property;
                if (reference.HasIdentity)
                {
                    var realtime = _resolver.ReadRealtimeProperty<TProperty>(reference.Identity);
                    if (realtime.Status == ContextEntityQueryStatus.IdentityMismatch || realtime.Status == ContextEntityQueryStatus.Unavailable)
                        return MobaRuntimeContextValueFailure.IdentityMismatch;
                    property = realtime.Value;
                }
                else
                {
                    property = _resolver.GetProperty<TProperty>(reference.ContextId, ContextValueReadMode.RealtimeOnly).Value;
                }
                provider = property as IContextValueProvider;
                if (provider == null && !reference.HasIdentity)
                    _realtimeProviders.TryGetValueReader<TProperty>(reference.ContextId, out provider);
                if (provider != null)
                {
                    source = ContextValueSource.Realtime;
                    return MobaRuntimeContextValueFailure.None;
                }
                // A live identity must not borrow missing fields from a historical record.
                if (reference.HasIdentity) return MobaRuntimeContextValueFailure.ValueMissing;
            }

            if (mode == ContextValueReadMode.RealtimeThenSnapshot && TryResolveSnapshot<TProperty>(reference, out provider, out snapshot))
            {
                source = ContextValueSource.Snapshot;
                return MobaRuntimeContextValueFailure.None;
            }
            if (reference.HasIdentity && identityStatus == ContextEntityQueryStatus.Unavailable)
                return mode == ContextValueReadMode.RealtimeOnly || mode == ContextValueReadMode.SnapshotThenRealtime
                    ? MobaRuntimeContextValueFailure.ContextUnavailable : MobaRuntimeContextValueFailure.SnapshotUnavailable;
            return MobaRuntimeContextValueFailure.ValueMissing;
        }

        private bool TryResolveSnapshot<TProperty>(in MobaRuntimeContextReference reference,
            out IContextValueProvider provider, out ContextSnapshotReference snapshot)
            where TProperty : class, IProperty
        {
            provider = null;
            snapshot = default;
            ContextSnapshotRecord record;
            if (reference.HasIdentity)
            {
                if (typeof(TProperty) != typeof(MobaBuffContextProperty) ||
                    !_snapshots.TryGetLatest<MobaBuffContextSnapshot>(reference.ContextId, reference.Identity.Generation, "removed", out record))
                    return false;
            }
            else if (!_snapshots.TryGetRecord(reference.ContextId, out record)) return false;
            provider = record.Snapshot as IContextValueProvider;
            if (provider == null && !reference.HasIdentity && record.Snapshot is ISnapshotAccessor accessor)
                provider = new LegacySnapshotReader(accessor);
            snapshot = record.Reference;
            return provider != null;
        }

        private sealed class LegacySnapshotReader : IContextValueProvider
        {
            private readonly ISnapshotAccessor _accessor;
            internal LegacySnapshotReader(ISnapshotAccessor accessor) => _accessor = accessor;
            public bool TryGetValue<T>(string key, out T value)
            {
                value = _accessor.GetValue(key, default(T));
                return true;
            }
        }

        public void RestoreRollbackEntityCursor(long nextEntityId, IReadOnlyCollection<long> confirmedIds)
        {
            ThrowIfDisposed();
            var confirmed = new HashSet<long>(confirmedIds ?? throw new ArgumentNullException(nameof(confirmedIds)));
            _registry.ValidateRollbackEntityCursor(nextEntityId, confirmed);
            foreach (var id in _registry.GetRollbackEntityIds())
            {
                if (confirmed.Contains(id)) continue;
                _buffProvider.Unbind(id);
                _generations.Remove(id);
                _snapshots.Remove(id);
            }
            _snapshots.RemoveFromEntityId(nextEntityId);
            _buffProvider.PruneFromEntityId(nextEntityId);
            foreach (var id in new List<long>(_generations.Keys))
                if (id >= nextEntityId) _generations.Remove(id);
            _registry.RestoreRollbackEntityCursor(nextEntityId, confirmed);
        }

        public long EnsureBuffContext(BuffRuntime runtime, in MobaBuffRuntimeContextData data)
        {
            ThrowIfDisposed();
            if (runtime == null) return 0L;

            ValidateBuffContextOwnership(runtime);
            var contextId = runtime.RuntimeContextId;
            if (contextId == 0L || !_registry.Exists(contextId))
            {
                contextId = _registry.Create().Build();
                runtime.RuntimeContextId = contextId;
                runtime.RuntimeContextVersion = 1L;
            }
            else if (runtime.RuntimeContextVersion <= 0L)
            {
                runtime.RuntimeContextVersion = 1L;
            }

            _buffProvider.Bind(contextId, runtime, data.TargetActorId, data.LifecycleState, data.Frame);
            EnsureGeneration(contextId);
            _registry.TryGetReference(contextId, out runtime.RuntimeContextIdentity);
            return contextId;
        }

        public long RestoreBuffContext(BuffRuntime runtime, in MobaBuffRuntimeContextData data)
        {
            ThrowIfDisposed();
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            var contextId = runtime.RuntimeContextId;
            ValidateRestoredBuffReference(contextId, runtime.RuntimeContextVersion);
            // Legacy snapshots without runtime context retain their original absent identity.
            if (contextId == 0L) return 0L;

            _registry.RestoreEntity(contextId).Build();
            _snapshots.Remove(contextId);
            EnsureGeneration(contextId);
            _registry.TryGetReference(contextId, out runtime.RuntimeContextIdentity);
            _buffProvider.Bind(contextId, runtime, data.TargetActorId, data.LifecycleState, data.Frame);
            return contextId;
        }

        internal static void ValidateRestoredBuffReference(long contextId, long version)
        {
            if (contextId < 0L || contextId == long.MaxValue ||
                (contextId == 0L ? version != 0L : version <= 0L))
                throw new InvalidOperationException($"Invalid restored Buff runtime context. id={contextId}, version={version}.");
        }

        internal void ValidateBuffContextOwnership(BuffRuntime runtime)
        {
            var id = runtime.RuntimeContextId;
            if (id != 0L && _registry.Exists(id) && !_buffProvider.IsBoundTo(id, runtime))
                throw new InvalidOperationException($"Buff runtime does not own context {id}.");
            if (id != 0L && _registry.Exists(id) && runtime.RuntimeContextIdentity.IsValid &&
                _registry.Query(runtime.RuntimeContextIdentity) != ContextEntityQueryStatus.Found)
                throw new InvalidOperationException($"Buff runtime context {id} has a stale local identity.");
        }

        public bool TryGetBuffContext(long contextId, out MobaBuffContextProperty property)
        {
            if (_disposed) { property = null; return false; }
            var result = _resolver.GetProperty<MobaBuffContextProperty>(contextId, ContextValueReadMode.RealtimeThenSnapshot);
            property = result.Found ? result.Value : null;
            return property != null;
        }

        public void SnapshotAndDestroyBuffContext(BuffRuntime runtime, MobaRuntimeContextLifecycleState finalState, int frame, bool preserveReference = false)
        {
            if (_disposed || runtime == null) return;

            var contextId = runtime.RuntimeContextId;
            if (contextId == 0L) return;

            ValidateBuffContextOwnership(runtime);
            if (_buffProvider.TryCreateSnapshot(contextId, finalState, frame, out var snapshot))
                SaveFinalSnapshot(snapshot);
            else if (!_snapshots.TryGetRecord(contextId, out _) && _registry.TryGetReference(contextId, out _))
                SaveFinalSnapshot(MobaBuffContextSnapshot.FromRuntime(runtime, 0, frame, finalState));

            _buffProvider.Unbind(contextId);
            _generations.Remove(contextId);
            if (_registry.Exists(contextId))
                _registry.Destroy(contextId);

            if (!preserveReference)
            {
                runtime.RuntimeContextId = 0L;
                runtime.RuntimeContextVersion = 0L;
                runtime.RuntimeContextIdentity = default;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _registry.Unsubscribe(OnContextEvent);
            _generations.Clear();
            _buffProvider.Clear();
            _realtimeProviders.Clear();
            _registry.Clear();
            _snapshots.Clear();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(MobaRuntimeContextService));
        }

        private void OnContextEvent(ContextEvent evt)
        {
            if (evt.Type != ContextEventType.Destroyed) return;
            _buffProvider.Unbind(evt.EntityId);
            _generations.Remove(evt.EntityId);
        }

        private long EnsureGeneration(long contextId)
        {
            if (!_generations.TryGetValue(contextId, out var generation))
            {
                if (!_registry.TryGetReference(contextId, out var identity))
                    throw new InvalidOperationException($"Runtime context {contextId} has no live identity to capture.");
                generation = identity.Generation;
                _generations.Add(contextId, generation);
            }
            return generation;
        }

        private void SaveFinalSnapshot(MobaBuffContextSnapshot snapshot)
        {
            var capture = new ContextSnapshotCapture(EnsureGeneration(snapshot.EntityId), snapshot.Frame,
                "removed", isDestroyed: true);
            // Snapshot retention must never prevent the domain lifecycle from completing.
            _snapshots.TrySaveManaged(snapshot, capture, out _);
        }

        private sealed class MobaBuffRealtimeContextProvider : IContextRealtimeValueProvider
        {
            private readonly Dictionary<long, Entry> _entries = new Dictionary<long, Entry>();

            public void Bind(long contextId, BuffRuntime runtime, int targetActorId, MobaRuntimeContextLifecycleState state, int frame)
            {
                if (contextId == 0L || runtime == null) return;
                _entries[contextId] = new Entry(runtime, targetActorId, state, frame);
            }

            public void Unbind(long contextId)
            {
                _entries.Remove(contextId);
            }

            public bool IsBoundTo(long contextId, BuffRuntime runtime)
            {
                return _entries.TryGetValue(contextId, out var entry) && ReferenceEquals(entry.Runtime, runtime);
            }

            public void Clear()
            {
                _entries.Clear();
            }

            public void PruneFromEntityId(long firstEntityId)
            {
                var ids = new List<long>(_entries.Keys);
                foreach (var id in ids)
                    if (id >= firstEntityId) _entries.Remove(id);
            }

            public bool TryGetProperty(long contextId, out IProperty property)
            {
                if (_entries.TryGetValue(contextId, out var entry) && entry.Runtime != null)
                {
                    property = MobaBuffContextProperty.FromRuntime(entry.Runtime, entry.TargetActorId, entry.Frame, entry.State);
                    return true;
                }

                property = null;
                return false;
            }

            public bool TryGetValue<T>(long contextId, string key, out T value)
            {
                if (_entries.TryGetValue(contextId, out var entry) && entry.Runtime != null)
                {
                    var data = MobaBuffRuntimeContextData.FromRuntime(entry.Runtime, entry.TargetActorId, entry.Frame, entry.State);
                    return data.TryGetValue(contextId, entry.Runtime.RuntimeContextVersion, key, out value);
                }

                value = default;
                return false;
            }

            public bool TryCreateSnapshot(long contextId, MobaRuntimeContextLifecycleState state, int frame, out MobaBuffContextSnapshot snapshot)
            {
                if (_entries.TryGetValue(contextId, out var entry) && entry.Runtime != null)
                {
                    snapshot = MobaBuffContextSnapshot.FromRuntime(entry.Runtime, entry.TargetActorId, frame, state);
                    return true;
                }

                snapshot = null;
                return false;
            }

            private readonly struct Entry
            {
                public Entry(BuffRuntime runtime, int targetActorId, MobaRuntimeContextLifecycleState state, int frame)
                {
                    Runtime = runtime;
                    TargetActorId = targetActorId;
                    State = state;
                    Frame = frame;
                }

                public BuffRuntime Runtime { get; }
                public int TargetActorId { get; }
                public MobaRuntimeContextLifecycleState State { get; }
                public int Frame { get; }
            }
        }
    }
}
