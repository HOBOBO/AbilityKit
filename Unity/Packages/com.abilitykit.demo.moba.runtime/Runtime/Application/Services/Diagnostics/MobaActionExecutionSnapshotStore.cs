using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Context;
using AbilityKit.Core.Eventing;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Trace;

namespace AbilityKit.Demo.Moba.Services
{
    public interface IMobaActionExecutionSnapshotReader
    {
        long Revision { get; }
        BattleDiagnosticActionExecutionFacts Read(in ContextSnapshotReference reference);
    }

    [WorldService(typeof(IMobaActionExecutionSnapshotHook), WorldLifetime.Scoped)]
    [WorldService(typeof(IMobaActionExecutionSnapshotReader), WorldLifetime.Scoped)]
    public sealed class MobaActionExecutionSnapshotStore : IMobaActionExecutionSnapshotHook, IMobaActionExecutionSnapshotReader, IService
    {
        private const int CommitLimit = 32;
        private readonly MobaTraceRegistry _trace;
        private readonly MobaActorRegistry _actors;
        private readonly MobaBattleDiagnosticEventCollector _collector;
        private readonly Func<bool> _enabledOverride;
        private readonly SnapshotStorage _snapshots;
        private readonly Dictionary<long, Pending> _pending = new Dictionary<long, Pending>();
        private readonly int _capacity;
        private readonly IDisposable _subscription;
        private readonly bool _hasImmediateDelivery;
        private long _generation = 1;
        private bool _disposed;
        private readonly ConditionalWeakTable<global::ActorEntity, ActorBinding> _bindings = new ConditionalWeakTable<global::ActorEntity, ActorBinding>();
        private long _nextBindingId;
        private sealed class ActorBinding { public int CreationIndex; public long Id; }
        private sealed class Pending
        {
            public ContextSnapshotReference Reference;
            public BattleDiagnosticActionExecutionFacts Before;
            public long CaptureRevision;
            public bool Interrupted;
            public bool Truncated;
            public bool DamageEnabledAtStart;
            public long CollectFailureCount;
            public bool DamageTruncated;
            public readonly List<BattleDiagnosticActionHealthCommit> Commits = new List<BattleDiagnosticActionHealthCommit>();
            public readonly List<BattleDiagnosticActionDamageResult> DamageResults = new List<BattleDiagnosticActionDamageResult>();
        }

        public MobaActionExecutionSnapshotStore(MobaTraceRegistry trace, MobaActorRegistry actors = null,
            MobaBattleDiagnosticEventCollector collector = null, AbilityKit.Triggering.Eventing.IEventBus eventBus = null)
            : this(trace, actors, collector, eventBus, 4096, null) { }

        // WorldActivator requires every constructor dependency, even C# optional parameters.
        public MobaActionExecutionSnapshotStore(MobaTraceRegistry trace, MobaActorRegistry actors,
            MobaBattleDiagnosticEventCollector collector) : this(trace, actors, collector, null, 4096, null) { }

        public MobaActionExecutionSnapshotStore(MobaTraceRegistry trace) : this(trace, null, null, null, 4096, null) { }

        internal MobaActionExecutionSnapshotStore(MobaTraceRegistry trace, MobaActorRegistry actors,
            MobaBattleDiagnosticEventCollector collector, AbilityKit.Triggering.Eventing.IEventBus eventBus,
            int capacity, Func<bool> enabledOverride)
        {
            _trace = trace ?? throw new ArgumentNullException(nameof(trace));
            _actors = actors; _collector = collector; _enabledOverride = enabledOverride; _capacity = capacity;
            _hasImmediateDelivery = eventBus is AbilityKit.Triggering.Eventing.EventBus bus &&
                bus.DispatchMode == AbilityKit.Triggering.Eventing.EEventDispatchMode.Immediate;
            _snapshots = new SnapshotStorage(capacity, 2);
            _snapshots.RegisterType<MobaActionExecutionSnapshot>("moba.action.execution", 2);
            _trace.RegistryEvent += OnTraceEvent;
            if (_collector != null) _collector.EventCollected += OnEventCollected;
            try
            {
                if (eventBus != null)
                    _subscription = eventBus.Subscribe(new EventKey<MobaHealthChangeResult>(
                        TriggeringIdUtil.GetEventEid(DamagePipelineEvents.HealthCommitted)), OnHealthCommitted);
            }
            catch (Exception) { }
        }

        private bool IsEnabled => !_disposed && (_enabledOverride != null ? _enabledOverride() :
            _collector != null && !_collector.IsFrozen && _collector.IsEnabled(BattleDiagnosticEventChannel.Skill));
        public long Revision { get; private set; }

        public void OnActionStarted(long contextId, int actionIndex, long actionId, long sourceActorId, long targetActorId, int frame)
        {
            try
            {
                if (!IsEnabled || frame < 0 || actionIndex < 0 || actionId == 0 ||
                    !_trace.TryGetNodeSnapshot(contextId, out var node) || node.IsEnded ||
                    node.Kind != (int)MobaTraceKind.EffectAction || !(node.Metadata is MobaTraceMetadata metadata) ||
                    metadata.ConfigId != actionId || metadata.ActionSnapshot.IsValid) return;
                // Pending work is bounded too; an evicted entry is never reconstructed from live state.
                PrunePending();
                var facts = new BattleDiagnosticActionExecutionFacts(BattleDiagnosticDataAvailability.Available,
                    frame: frame, actionIndex: actionIndex, actionId: actionId,
                    sourceBefore: CaptureActor(sourceActorId), targetBefore: CaptureActor(targetActorId));
                if (!_snapshots.TrySaveManaged(new MobaActionExecutionSnapshot(contextId, facts),
                    new ContextSnapshotCapture(_generation, frame, "action-entry"), out var reference)) return;
                if (_pending.Count >= _capacity)
                {
                    var oldest = long.MaxValue;
                    foreach (var id in _pending.Keys) if (id < oldest) oldest = id;
                    _pending.Remove(oldest);
                }
                metadata.ActionSnapshot = reference;
                _pending.Add(contextId, new Pending
                {
                    Reference = reference, Before = facts, CaptureRevision = _collector?.CaptureRevision ?? 0,
                    DamageEnabledAtStart = _collector != null && _collector.IsEnabled(BattleDiagnosticEventChannel.DamageAndHeal),
                    CollectFailureCount = _collector?.CollectFailureCount ?? 0
                });
                Revision++;
            }
            catch (Exception) { }
        }

        public void OnActionEnded(long contextId, int actionIndex, long actionId, bool succeeded, bool aborted, int frame)
        {
            try
            {
                if (!_pending.TryGetValue(contextId, out var pending) || pending.Before.ActionIndex != actionIndex ||
                    pending.Before.ActionId != actionId) return;
                _pending.Remove(contextId);
                if (!IsEnabled || frame < pending.Before.Frame ||
                    _snapshots.Query(pending.Reference, out _) != ContextSnapshotQueryStatus.Found ||
                    !_trace.TryGetNodeSnapshot(contextId, out var node) || !(node.Metadata is MobaTraceMetadata metadata) ||
                    !metadata.ActionSnapshot.Equals(pending.Reference)) return;
                var before = pending.Before;
                var facts = new BattleDiagnosticActionExecutionFacts(BattleDiagnosticDataAvailability.Available,
                    frame: before.Frame, actionIndex: actionIndex, actionId: actionId,
                    outcome: aborted ? BattleDiagnosticActionOutcome.Aborted : succeeded ? BattleDiagnosticActionOutcome.Completed : BattleDiagnosticActionOutcome.Failed,
                    hasAfter: true, endFrame: frame,
                    commitsComplete: _subscription != null && _hasImmediateDelivery && !pending.Interrupted &&
                        pending.CaptureRevision == (_collector?.CaptureRevision ?? 0),
                    commitsTruncated: pending.Truncated, sourceBefore: before.SourceBefore, targetBefore: before.TargetBefore,
                    sourceAfter: CaptureActor(before.SourceBefore.ActorId), targetAfter: CaptureActor(before.TargetBefore.ActorId), commits: pending.Commits,
                    damageAvailability: pending.DamageEnabledAtStart || pending.DamageResults.Count > 0 ?
                        BattleDiagnosticDataAvailability.Available : BattleDiagnosticDataAvailability.NotCaptured,
                    damageCoverageContinuous: pending.DamageEnabledAtStart && _collector != null &&
                        _collector.IsEnabled(BattleDiagnosticEventChannel.DamageAndHeal) && !pending.Interrupted &&
                        pending.CaptureRevision == _collector.CaptureRevision && pending.CollectFailureCount == _collector.CollectFailureCount,
                    damageResultsTruncated: pending.DamageTruncated, damageResults: pending.DamageResults);
                if (!_snapshots.TrySaveManaged(new MobaActionExecutionSnapshot(contextId, facts),
                    new ContextSnapshotCapture(_generation, frame, "action-end"), out var reference)) return;
                metadata.ActionSnapshot = reference;
                Revision++;
            }
            catch (Exception) { _pending.Remove(contextId); }
        }

        internal void OnHealthCommitted(MobaHealthChangeResult result)
        {
            try
            {
                if (_disposed || _pending.Count == 0 || !result.Succeeded) return;
                var origin = result.Origin;
                var current = origin.ImmediateContextId != 0 ? origin.ImmediateContextId : origin.ParentContextId;
                if (!TryFindPending(current, origin.RootContextId, out var pending)) return;
                if (!IsEnabled) { pending.Interrupted = true; return; }
                if (pending.Commits.Count >= CommitLimit) { pending.Truncated = true; return; }
                pending.Commits.Add(new BattleDiagnosticActionHealthCommit((int)result.Kind,
                    result.SourceActorId, result.TargetActorId, result.ValueType, result.ReasonKind, result.ReasonParam,
                    result.RequestedValue, result.AppliedValue, result.OldHp, result.TargetHp, result.TargetMaxHp, current));
            }
            catch (Exception) { }
        }

        private void OnEventCollected(BattleDiagnosticEvent evt)
        {
            try
            {
                if (_disposed || _pending.Count == 0 || _collector == null || !evt.Scope.Equals(_collector.Scope) ||
                    evt.Kind != BattleDiagnosticEventKind.Damage || !evt.Payload.TryGetDamageCalculation(out var calculation) ||
                    !TryFindPending(evt.ContextId, evt.RootContextId, out var pending)) return;
                if (!IsEnabled) { pending.Interrupted = true; return; }
                if (pending.DamageResults.Count >= CommitLimit) { pending.DamageTruncated = true; return; }
                pending.DamageResults.Add(new BattleDiagnosticActionDamageResult(evt.Sequence, evt.Frame,
                    evt.SourceActorId, evt.TargetActorId, evt.ContextId, calculation, evt.Summary));
            }
            catch (Exception) { }
        }

        private bool TryFindPending(long current, long rootId, out Pending pending)
        {
            pending = null;
            // Never attribute an unsampled nested action to its sampled outer action.
            for (var depth = 0; current != 0 && depth < 256; depth++)
            {
                if (!_trace.TryGetNodeSnapshot(current, out var node) || (rootId != 0 && node.RootId != rootId)) return false;
                if (node.Kind == (int)MobaTraceKind.EffectAction) return _pending.TryGetValue(current, out pending);
                current = node.ParentId;
            }
            return false;
        }

        private BattleDiagnosticActionActorValues CaptureActor(long actorId)
        {
            if (actorId <= 0 || actorId > int.MaxValue || _actors == null || !_actors.TryGet((int)actorId, out var entity))
                return new BattleDiagnosticActionActorValues(actorId, 0, false);
            var hasHp = false; var hasMana = false; var hp = 0f; var mana = 0f;
            var map = entity.hasResourceContainer ? entity.resourceContainer.Value?.Map : null;
            if (map != null)
            {
                if (map.TryGetValue(ResourceType.Hp, out var hpState) && hpState != null)
                { hasHp = true; hp = MobaResourceFixedConvert.ToSingle(hpState.Current); }
                if (map.TryGetValue(ResourceType.Mana, out var manaState) && manaState != null)
                { hasMana = true; mana = MobaResourceFixedConvert.ToSingle(manaState.Current); }
            }
            // Weak keys do not keep actors alive; creationIndex also detects Entitas object recycling.
            var binding = _bindings.GetValue(entity, _ => new ActorBinding());
            if (binding.Id == 0 || binding.CreationIndex != entity.creationIndex)
            { binding.CreationIndex = entity.creationIndex; binding.Id = ++_nextBindingId; }
            return new BattleDiagnosticActionActorValues(actorId, binding.Id, true, hasHp, hp, hasMana, mana);
        }

        public BattleDiagnosticActionExecutionFacts Read(in ContextSnapshotReference reference)
        {
            if (!reference.IsValid) return default;
            var status = _snapshots.Query(reference, out var record);
            if (status != ContextSnapshotQueryStatus.Found)
                return new BattleDiagnosticActionExecutionFacts(status == ContextSnapshotQueryStatus.Unavailable ?
                    BattleDiagnosticDataAvailability.Evicted : BattleDiagnosticDataAvailability.Error,
                    snapshotId: reference.SnapshotId, generation: reference.Generation);
            var x = ((MobaActionExecutionSnapshot)record.Snapshot).Facts;
            return new BattleDiagnosticActionExecutionFacts(BattleDiagnosticDataAvailability.Available,
                reference.SnapshotId, reference.Generation, x.Frame, record.TypeId, record.SchemaVersion,
                x.ActionIndex, x.ActionId, x.Outcome, x.HasAfter, x.EndFrame, x.CommitsComplete, x.CommitsTruncated,
                x.SourceBefore, x.TargetBefore, x.SourceAfter, x.TargetAfter, x.Commits,
                x.DamageAvailability, x.DamageCoverageContinuous, x.DamageResultsTruncated, x.DamageResults);
        }

        private void PrunePending()
        {
            var removed = new List<long>();
            foreach (var pair in _pending)
                if (_snapshots.Query(pair.Value.Reference, out _) != ContextSnapshotQueryStatus.Found) removed.Add(pair.Key);
            foreach (var id in removed) _pending.Remove(id);
        }
        private void OnTraceEvent(TraceRegistryEvent evt)
        {
            try
            {
                switch (evt.Kind)
                {
                    case TraceRegistryEventKind.RootPurged:
                        _pending.Remove(evt.ContextId);
                        if (_snapshots.Remove(evt.ContextId)) Revision++;
                        break;
                    case TraceRegistryEventKind.PredictionRetracted:
                        _snapshots.RemoveFromEntityId(evt.ContextId); PrunePending(); Revision++; break;
                    case TraceRegistryEventKind.RegistryCleared:
                        _snapshots.Clear(); _pending.Clear(); _generation++; Revision++; break;
                    case TraceRegistryEventKind.NodeEnded:
                        _pending.Remove(evt.ContextId); break;
                }
            }
            catch (Exception) { }
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true; _trace.RegistryEvent -= OnTraceEvent;
            if (_collector != null) _collector.EventCollected -= OnEventCollected;
            try { _subscription?.Dispose(); }
            finally { _pending.Clear(); _snapshots.Clear(); Revision++; }
        }
    }
}
