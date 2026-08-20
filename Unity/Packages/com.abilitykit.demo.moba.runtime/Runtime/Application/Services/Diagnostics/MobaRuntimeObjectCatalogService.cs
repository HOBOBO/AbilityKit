using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Core.Observability;
using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Demo.Moba.Services.Observability;

namespace AbilityKit.Demo.Moba.Services
{
    public interface IBattleDiagnosticObjectCatalogSnapshotSource
    {
        BattleDiagnosticSessionScope Scope { get; }
        BattleDiagnosticObjectCatalogSnapshot CaptureObjectCatalogSnapshot();
    }

    public interface IMobaRuntimeObjectCatalogControl
    {
        void SetFrozen(bool frozen);
        void Clear();
    }

    public interface IMobaRuntimeObjectKeyResolver
    {
        bool TryResolve(
            MobaRuntimeObjectKind kind,
            long runtimeId,
            int frame,
            out RuntimeObjectKey key);
    }

    [WorldService(typeof(IMobaRuntimeObjectLifecycleHook), WorldLifetime.Scoped)]
    [WorldService(typeof(IBattleDiagnosticObjectCatalogSnapshotSource), WorldLifetime.Scoped)]
    [WorldService(typeof(IMobaRuntimeObjectCatalogControl), WorldLifetime.Scoped)]
    [WorldService(typeof(IMobaRuntimeObjectKeyResolver), WorldLifetime.Scoped)]
    [WorldService(typeof(IMobaRuntimeObjectBootstrapRegistry), WorldLifetime.Scoped)]
    [WorldService(typeof(IBattleDiagnosticRuntimeObjectReadStore), WorldLifetime.Scoped)]
    [WorldService(typeof(IBattleDiagnosticRuntimeObjectCatalogReadStore), WorldLifetime.Scoped)]
    [WorldService(typeof(MobaRuntimeObjectCatalogService), WorldLifetime.Scoped)]
    public sealed class MobaRuntimeObjectCatalogService :
        IMobaRuntimeObjectLifecycleHook,
        IBattleDiagnosticObjectCatalogSnapshotSource,
        IMobaRuntimeObjectCatalogControl,
        IMobaRuntimeObjectKeyResolver,
        IMobaRuntimeObjectBootstrapRegistry,
        IBattleDiagnosticRuntimeObjectCatalogReadStore,
        IMobaBattleDiagnosticCaptureModeObserver,
        IService
    {
        public const int DefaultCapacity = 4096;

        private readonly object _gate = new object();
        private readonly BattleDiagnosticSessionScope _scope;
        private readonly int _capacity;
        private readonly Func<bool> _enabledOverride;
        private readonly MobaBattleDiagnosticEventCollector _collector;
        private readonly IMobaRuntimeObjectLifecycleHook _backfillHook;
        private List<RecordState> _records;
        private Dictionary<ObjectIdentity, RecordState> _active;
        private Dictionary<ObjectIdentity, RecordState> _latest;
        private int _generationSequence;
        private List<IMobaRuntimeObjectBootstrapContributor> _contributors;
        private long _revision;
        private long _backfillAttemptCount;
        private long _backfillFailureCount;
        private int _lastBackfillFrame = BattleDiagnosticFrames.Invalid;
        private bool _truncated;
        private bool _frozen;

        public MobaRuntimeObjectCatalogService(MobaBattleDiagnosticEventCollector collector)
            : this(
                GetScope(collector),
                DefaultCapacity,
                () => CapturesRuntimeObjects(collector.CaptureMode))
        {
            _collector = collector;
            collector.AttachRuntimeObjectKeyResolver(this);
            collector.AttachCaptureModeObserver(this);
        }

        internal MobaRuntimeObjectCatalogService(
            BattleDiagnosticSessionScope scope,
            int capacity = DefaultCapacity,
            Func<bool> enabledOverride = null)
        {
            if (!scope.IsValid) throw new ArgumentException("A valid session scope is required.", nameof(scope));
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            _scope = scope;
            _capacity = capacity;
            _enabledOverride = enabledOverride;
            _backfillHook = new BackfillLifecycleHook(this);
        }

        public BattleDiagnosticSessionScope Scope => _scope;
        public long Revision
        {
            get { lock (_gate) return _revision; }
        }

        public bool IsEnabled
        {
            get
            {
                if (_frozen) return false;
                return _enabledOverride != null && _enabledOverride();
            }
        }

        public void OnObserved(in MobaRuntimeObjectLifecycleObservation observation)
        {
            Observe(in observation, BattleDiagnosticRuntimeObjectDiscoveryKind.LifecycleCreated);
        }

        private void Observe(
            in MobaRuntimeObjectLifecycleObservation observation,
            BattleDiagnosticRuntimeObjectDiscoveryKind discoveryKind)
        {
            if (!IsEnabled || observation.RuntimeId == 0L ||
                observation.Kind == MobaRuntimeObjectKind.Unknown) return;

            lock (_gate)
            {
                if (_frozen) return;
                EnsureStorage();
                var identity = new ObjectIdentity(observation.Kind, observation.RuntimeId);
                switch (observation.Stage)
                {
                    case MobaRuntimeObjectLifecycleStage.Created:
                        ObserveCreated(identity, in observation, discoveryKind);
                        break;
                    case MobaRuntimeObjectLifecycleStage.Destroyed:
                        ObserveDestroyed(identity, in observation);
                        break;
                }
            }
        }

        public bool TryResolve(
            MobaRuntimeObjectKind kind,
            long runtimeId,
            int frame,
            out RuntimeObjectKey key)
        {
            key = default;
            if (kind == MobaRuntimeObjectKind.Unknown || runtimeId == 0L) return false;

            lock (_gate)
            {
                if (_records == null) return false;
                var identity = new ObjectIdentity(kind, runtimeId);
                if (frame < 0 && _latest.TryGetValue(identity, out var latest))
                {
                    key = latest.Key;
                    return true;
                }

                for (var i = _records.Count - 1; i >= 0; i--)
                {
                    var state = _records[i];
                    if (!state.Identity.Equals(identity)) continue;
                    if (state.CreatedFrame >= 0 && frame < state.CreatedFrame) continue;
                    if (state.DestroyedFrame >= 0 && frame > state.DestroyedFrame) continue;
                    key = state.Key;
                    return true;
                }

                return false;
            }
        }

        public BattleDiagnosticObjectCatalogSnapshot CaptureObjectCatalogSnapshot()
        {
            lock (_gate)
            {
                if (_records == null || _records.Count == 0)
                {
                    return new BattleDiagnosticObjectCatalogSnapshot(
                        _scope,
                        _revision,
                        _truncated,
                        Array.Empty<BattleDiagnosticRuntimeObject>(),
                        _backfillAttemptCount,
                        _backfillFailureCount,
                        _lastBackfillFrame);
                }

                var items = new List<BattleDiagnosticRuntimeObject>(_records.Count);
                for (var i = 0; i < _records.Count; i++)
                {
                    items.Add(_records[i].ToSnapshot());
                }

                return new BattleDiagnosticObjectCatalogSnapshot(
                    _scope,
                    _revision,
                    _truncated,
                    items,
                    _backfillAttemptCount,
                    _backfillFailureCount,
                    _lastBackfillFrame);
            }
        }

        public BattleDiagnosticQueryResult<BattleDiagnosticRuntimeObject> QueryRuntimeObject(
            long requestId,
            in BattleDiagnosticRuntimeObjectReference reference,
            int frame)
        {
            if (requestId <= 0L) throw new ArgumentOutOfRangeException(nameof(requestId));
            if (!reference.HasRuntimeId) throw new ArgumentException(
                "A runtime object reference with an ID is required.",
                nameof(reference));

            lock (_gate)
            {
                if (TryResolveRecord(in reference, frame, out var state))
                {
                    return BattleDiagnosticQueryResult<BattleDiagnosticRuntimeObject>.FromItems(
                        requestId,
                        _revision,
                        new[] { state.ToSnapshot() },
                        false);
                }

                return BattleDiagnosticQueryResult<BattleDiagnosticRuntimeObject>.Unavailable(
                    requestId,
                    _revision,
                    _truncated
                        ? BattleDiagnosticDataAvailability.Truncated
                        : BattleDiagnosticDataAvailability.NotCaptured,
                    _truncated
                        ? "The runtime object may have been evicted from the bounded catalog."
                        : "The runtime object was not captured.");
            }
        }

        public BattleDiagnosticQueryResult<BattleDiagnosticRuntimeObject> QueryRuntimeObjects(
            BattleDiagnosticRuntimeObjectQuery query)
        {
            lock (_gate)
            {
                if (query.Page.StoreRevision > 0L && query.Page.StoreRevision != _revision)
                {
                    return BattleDiagnosticQueryResult<BattleDiagnosticRuntimeObject>.Unavailable(
                        query.RequestId,
                        query.Page.StoreRevision,
                        BattleDiagnosticDataAvailability.Evicted,
                        "The requested runtime object catalog revision is no longer available.");
                }

                var results = new List<BattleDiagnosticRuntimeObject>(query.Page.Limit);
                var skipped = 0;
                var hasMore = false;
                if (_records != null)
                {
                    for (var i = 0; i < _records.Count; i++)
                    {
                        var item = _records[i].ToSnapshot();
                        if (!query.Filter.Matches(in item)) continue;
                        if (skipped++ < query.Page.Offset) continue;
                        if (results.Count == query.Page.Limit)
                        {
                            hasMore = true;
                            break;
                        }
                        results.Add(item);
                    }
                }

                return BattleDiagnosticQueryResult<BattleDiagnosticRuntimeObject>.FromItems(
                    query.RequestId,
                    _revision,
                    results,
                    hasMore);
            }
        }

        public BattleDiagnosticQueryResult<BattleDiagnosticRuntimeObjectCatalogSummary>
            QueryRuntimeObjectSummary(long requestId)
        {
            if (requestId <= 0L) throw new ArgumentOutOfRangeException(nameof(requestId));
            var snapshot = CaptureObjectCatalogSnapshot();
            return BattleDiagnosticQueryResult<BattleDiagnosticRuntimeObjectCatalogSummary>.FromItems(
                requestId,
                snapshot.Revision,
                new[] { snapshot.Summary },
                false);
        }

        public void SetFrozen(bool frozen)
        {
            lock (_gate) _frozen = frozen;
        }

        public bool Register(IMobaRuntimeObjectBootstrapContributor contributor)
        {
            if (contributor == null) return false;
            var captureImmediately = false;
            lock (_gate)
            {
                if (_contributors == null)
                    _contributors = new List<IMobaRuntimeObjectBootstrapContributor>(4);
                if (_contributors.Contains(contributor)) return true;
                _contributors.Add(contributor);
                captureImmediately = IsEnabled;
            }

            if (captureImmediately) CaptureContributor(contributor, ResolveCurrentFrame());
            return true;
        }

        public void Unregister(IMobaRuntimeObjectBootstrapContributor contributor)
        {
            if (contributor == null) return;
            lock (_gate) _contributors?.Remove(contributor);
        }

        public void Clear()
        {
            lock (_gate)
            {
                _records?.Clear();
                _active?.Clear();
                _latest?.Clear();
                _generationSequence = 0;
                _truncated = false;
                _backfillAttemptCount = 0L;
                _backfillFailureCount = 0L;
                _lastBackfillFrame = BattleDiagnosticFrames.Invalid;
                _revision++;
            }
        }

        public void Dispose()
        {
            _collector?.DetachRuntimeObjectKeyResolver(this);
            _collector?.DetachCaptureModeObserver(this);
            lock (_gate) _contributors?.Clear();
            Clear();
        }

        void IMobaBattleDiagnosticCaptureModeObserver.OnCaptureModeChanged(
            BattleDiagnosticCaptureMode previous,
            BattleDiagnosticCaptureMode current,
            int frame)
        {
            if (CapturesRuntimeObjects(previous) || !CapturesRuntimeObjects(current)) return;
            IMobaRuntimeObjectBootstrapContributor[] contributors;
            lock (_gate)
            {
                if (_frozen || _contributors == null) return;
                contributors = _contributors.ToArray();
            }

            for (var i = 0; i < contributors.Length; i++)
                CaptureContributor(contributors[i], frame);
        }

        private static BattleDiagnosticSessionScope GetScope(
            MobaBattleDiagnosticEventCollector collector)
        {
            if (collector == null) throw new ArgumentNullException(nameof(collector));
            return collector.Scope;
        }

        private static bool CapturesRuntimeObjects(BattleDiagnosticCaptureMode mode)
        {
            return mode == BattleDiagnosticCaptureMode.Events ||
                   mode == BattleDiagnosticCaptureMode.Full;
        }

        private int ResolveCurrentFrame()
        {
            return _collector != null ? _collector.CurrentFrame : BattleDiagnosticFrames.Invalid;
        }

        private void CaptureContributor(
            IMobaRuntimeObjectBootstrapContributor contributor,
            int frame)
        {
            var failed = false;
            try
            {
                contributor.CaptureActiveRuntimeObjects(_backfillHook, frame);
            }
            catch
            {
                failed = true;
                // Backfill is best-effort and must not affect capture-mode or gameplay commits.
            }
            finally
            {
                lock (_gate)
                {
                    _backfillAttemptCount++;
                    if (failed) _backfillFailureCount++;
                    _lastBackfillFrame = frame;
                    _revision++;
                }
            }
        }

        private bool TryResolveRecord(
            in BattleDiagnosticRuntimeObjectReference reference,
            int frame,
            out RecordState result)
        {
            if (_records != null)
            {
                for (var i = _records.Count - 1; i >= 0; i--)
                {
                    var candidate = _records[i];
                    if ((BattleDiagnosticRuntimeObjectKind)candidate.Identity.Kind != reference.Kind ||
                        candidate.Identity.RuntimeId != reference.RuntimeId) continue;
                    if (reference.IsResolved)
                    {
                        if (candidate.Key.Generation != reference.Generation) continue;
                    }
                    else
                    {
                        if (candidate.CreatedFrame >= 0 && frame >= 0 && frame < candidate.CreatedFrame)
                            continue;
                        if (candidate.DestroyedFrame >= 0 && frame >= 0 && frame > candidate.DestroyedFrame)
                            continue;
                    }

                    result = candidate;
                    return true;
                }
            }

            result = null;
            return false;
        }

        private void ObserveCreated(
            ObjectIdentity identity,
            in MobaRuntimeObjectLifecycleObservation observation,
            BattleDiagnosticRuntimeObjectDiscoveryKind discoveryKind)
        {
            if (_active.TryGetValue(identity, out var active))
            {
                active.Merge(in observation);
                _revision++;
                return;
            }

            EnsureCapacity();
            var generation = NextGeneration();
            var state = new RecordState(
                identity,
                new RuntimeObjectKey(observation.RuntimeId, generation),
                in observation,
                BattleDiagnosticRuntimeObjectState.Active,
                discoveryKind);
            _records.Add(state);
            _active[identity] = state;
            _latest[identity] = state;
            _revision++;
        }

        private void ObserveDestroyed(
            ObjectIdentity identity,
            in MobaRuntimeObjectLifecycleObservation observation)
        {
            if (_active.TryGetValue(identity, out var active))
            {
                active.End(in observation);
                _active.Remove(identity);
                _latest[identity] = active;
                _revision++;
                return;
            }

            if (_latest.TryGetValue(identity, out var latest) &&
                latest.State == BattleDiagnosticRuntimeObjectState.Ended)
            {
                return;
            }

            EnsureCapacity();
            var generation = NextGeneration();
            var state = new RecordState(
                identity,
                new RuntimeObjectKey(observation.RuntimeId, generation),
                in observation,
                BattleDiagnosticRuntimeObjectState.Ended,
                BattleDiagnosticRuntimeObjectDiscoveryKind.LifecycleEndedOnly);
            state.CreatedFrame = -1;
            state.End(in observation);
            _records.Add(state);
            _latest[identity] = state;
            _revision++;
        }

        private void EnsureStorage()
        {
            if (_records != null) return;
            _records = new List<RecordState>();
            _active = new Dictionary<ObjectIdentity, RecordState>();
            _latest = new Dictionary<ObjectIdentity, RecordState>();
        }

        private int NextGeneration()
        {
            if (_generationSequence == int.MaxValue) _generationSequence = 0;
            return ++_generationSequence;
        }

        private void EnsureCapacity()
        {
            if (_records.Count < _capacity) return;

            var removeIndex = -1;
            for (var i = 0; i < _records.Count; i++)
            {
                if (_records[i].State == BattleDiagnosticRuntimeObjectState.Ended)
                {
                    removeIndex = i;
                    break;
                }
            }

            if (removeIndex < 0) removeIndex = 0;
            var removed = _records[removeIndex];
            _records.RemoveAt(removeIndex);
            if (_active.TryGetValue(removed.Identity, out var active) && ReferenceEquals(active, removed))
                _active.Remove(removed.Identity);

            RecordState replacement = null;
            for (var i = _records.Count - 1; i >= 0; i--)
            {
                if (_records[i].Identity.Equals(removed.Identity))
                {
                    replacement = _records[i];
                    break;
                }
            }

            if (replacement == null)
            {
                _latest.Remove(removed.Identity);
            }
            else
            {
                _latest[removed.Identity] = replacement;
            }

            _truncated = true;
        }

        private readonly struct ObjectIdentity : IEquatable<ObjectIdentity>
        {
            public ObjectIdentity(MobaRuntimeObjectKind kind, long runtimeId)
            {
                Kind = kind;
                RuntimeId = runtimeId;
            }

            public MobaRuntimeObjectKind Kind { get; }
            public long RuntimeId { get; }

            public bool Equals(ObjectIdentity other) =>
                Kind == other.Kind && RuntimeId == other.RuntimeId;

            public override bool Equals(object obj) =>
                obj is ObjectIdentity other && Equals(other);

            public override int GetHashCode() =>
                ((int)Kind * 397) ^ RuntimeId.GetHashCode();
        }

        private sealed class RecordState
        {
            public RecordState(
                ObjectIdentity identity,
                RuntimeObjectKey key,
                in MobaRuntimeObjectLifecycleObservation observation,
                BattleDiagnosticRuntimeObjectState state,
                BattleDiagnosticRuntimeObjectDiscoveryKind discoveryKind)
            {
                Identity = identity;
                Key = key;
                CreatedFrame = observation.Frame;
                DestroyedFrame = state == BattleDiagnosticRuntimeObjectState.Ended
                    ? observation.Frame
                    : -1;
                State = state;
                DiscoveryKind = discoveryKind;
                BackfilledFrame = discoveryKind ==
                    BattleDiagnosticRuntimeObjectDiscoveryKind.ActiveBackfill
                        ? observation.Frame
                        : BattleDiagnosticFrames.Invalid;
                Merge(in observation);
            }

            public ObjectIdentity Identity;
            public RuntimeObjectKey Key;
            public MobaRuntimeObjectDefinitionKind DefinitionKind;
            public int DefinitionId;
            public long RelatedActorId;
            public long OwnerActorId;
            public long SourceActorId;
            public long TargetActorId;
            public int CreatedFrame;
            public int DestroyedFrame;
            public long RootContextId;
            public long ContextId;
            public BattleDiagnosticRuntimeObjectState State;
            public int EndReason;
            public string DisplayName;
            public BattleDiagnosticRuntimeObjectDiscoveryKind DiscoveryKind;
            public int BackfilledFrame;

            public void Merge(in MobaRuntimeObjectLifecycleObservation observation)
            {
                if (observation.DefinitionKind != MobaRuntimeObjectDefinitionKind.Unknown)
                    DefinitionKind = observation.DefinitionKind;
                if (observation.DefinitionId != 0) DefinitionId = observation.DefinitionId;
                if (observation.RelatedActorId != 0L) RelatedActorId = observation.RelatedActorId;
                if (observation.OwnerActorId != 0L) OwnerActorId = observation.OwnerActorId;
                if (observation.SourceActorId != 0L) SourceActorId = observation.SourceActorId;
                if (observation.TargetActorId != 0L) TargetActorId = observation.TargetActorId;
                if (observation.RootContextId != 0L) RootContextId = observation.RootContextId;
                if (observation.ContextId != 0L) ContextId = observation.ContextId;
                if (!string.IsNullOrEmpty(observation.DisplayName)) DisplayName = observation.DisplayName;
            }

            public void End(in MobaRuntimeObjectLifecycleObservation observation)
            {
                Merge(in observation);
                DestroyedFrame = observation.Frame;
                EndReason = observation.EndReason;
                State = BattleDiagnosticRuntimeObjectState.Ended;
            }

            public BattleDiagnosticRuntimeObject ToSnapshot()
            {
                return new BattleDiagnosticRuntimeObject(
                    (BattleDiagnosticRuntimeObjectKind)Identity.Kind,
                    Key.RuntimeId,
                    Key.Generation,
                    (BattleDiagnosticDefinitionKind)DefinitionKind,
                    DefinitionId,
                    RelatedActorId,
                    OwnerActorId,
                    SourceActorId,
                    TargetActorId,
                    CreatedFrame,
                    DestroyedFrame,
                    RootContextId,
                    ContextId,
                    State,
                    EndReason,
                    DisplayName,
                    DiscoveryKind,
                    BackfilledFrame);
            }
        }

        private sealed class BackfillLifecycleHook : IMobaRuntimeObjectLifecycleHook
        {
            private readonly MobaRuntimeObjectCatalogService _owner;

            public BackfillLifecycleHook(MobaRuntimeObjectCatalogService owner)
            {
                _owner = owner;
            }

            public bool IsEnabled => _owner.IsEnabled;

            public void OnObserved(in MobaRuntimeObjectLifecycleObservation observation)
            {
                _owner.Observe(
                    in observation,
                    BattleDiagnosticRuntimeObjectDiscoveryKind.ActiveBackfill);
            }
        }
    }
}
