using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Context;
using AbilityKit.Demo.Moba.Components;
using System;
using System.Collections.Generic;

namespace AbilityKit.Demo.Moba.Services
{
    [WorldService(typeof(MobaRuntimeContextService), WorldLifetime.Scoped)]
    public sealed class MobaRuntimeContextService : IService
    {
        private readonly ContextRegistry _registry = new ContextRegistry();
        private readonly SnapshotStorage _snapshots = new SnapshotStorage();
        private readonly ContextRealtimeProviderRegistry _realtimeProviders = new ContextRealtimeProviderRegistry();
        private readonly MobaBuffRealtimeContextProvider _buffProvider = new MobaBuffRealtimeContextProvider();
        private readonly ContextValueResolver _resolver;

        public MobaRuntimeContextService()
        {
            _realtimeProviders.Register<MobaBuffContextProperty>(_buffProvider);
            _resolver = new ContextValueResolver(_registry, _snapshots, _realtimeProviders);
        }

        public ContextRegistry Registry => _registry;
        public SnapshotStorage Snapshots => _snapshots;
        public ContextValueResolver Resolver => _resolver;

        public void RestoreRollbackEntityCursor(long nextEntityId, IReadOnlyCollection<long> confirmedIds)
        {
            var confirmed = new HashSet<long>(confirmedIds ?? throw new ArgumentNullException(nameof(confirmedIds)));
            foreach (var id in confirmed)
                if (!_registry.Exists(id)) throw new InvalidOperationException($"Confirmed context entity {id} was destroyed before rollback.");
            foreach (var id in _registry.GetRollbackEntityIds())
            {
                if (confirmed.Contains(id)) continue;
                _buffProvider.Unbind(id);
                _snapshots.Remove(id);
            }
            _snapshots.RemoveFromEntityId(nextEntityId);
            _buffProvider.PruneFromEntityId(nextEntityId);
            _registry.RestoreRollbackEntityCursor(nextEntityId, confirmedIds);
        }

        public long EnsureBuffContext(BuffRuntime runtime, in MobaBuffRuntimeContextData data)
        {
            if (runtime == null) return 0L;

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
            return contextId;
        }

        public bool TryGetBuffContext(long contextId, out MobaBuffContextProperty property)
        {
            var result = _resolver.GetProperty<MobaBuffContextProperty>(contextId, ContextValueReadMode.RealtimeThenSnapshot);
            property = result.Found ? result.Value : null;
            return property != null;
        }

        public void SnapshotAndDestroyBuffContext(BuffRuntime runtime, MobaRuntimeContextLifecycleState finalState, int frame)
        {
            if (runtime == null) return;

            var contextId = runtime.RuntimeContextId;
            if (contextId == 0L) return;

            if (_buffProvider.TryCreateSnapshot(contextId, finalState, frame, out var snapshot))
                _snapshots.Save(snapshot);
            else if (!_snapshots.TryGetRecord(contextId, out _))
                _snapshots.Save(MobaBuffContextSnapshot.FromRuntime(runtime, 0, frame, finalState));

            _buffProvider.Unbind(contextId);
            if (_registry.Exists(contextId))
                _registry.Destroy(contextId);

            _snapshots.MarkDestroyed(contextId);
            runtime.RuntimeContextId = 0L;
            runtime.RuntimeContextVersion = 0L;
        }

        public void Dispose()
        {
            _buffProvider.Clear();
            _realtimeProviders.Clear();
            _registry.Clear();
            _snapshots.Clear();
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
