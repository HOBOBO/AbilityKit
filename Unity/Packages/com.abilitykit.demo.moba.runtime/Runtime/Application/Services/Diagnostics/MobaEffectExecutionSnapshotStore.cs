using System;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Context;
using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Trace;

namespace AbilityKit.Demo.Moba.Services
{
    public interface IMobaEffectExecutionSnapshotReader
    {
        long Revision { get; }
        BattleDiagnosticEffectExecutionFacts Read(in ContextSnapshotReference reference);
    }

    [WorldService(typeof(IMobaEffectExecutionSnapshotHook), WorldLifetime.Scoped)]
    [WorldService(typeof(IMobaEffectExecutionSnapshotReader), WorldLifetime.Scoped)]
    public sealed class MobaEffectExecutionSnapshotStore : IMobaEffectExecutionSnapshotHook,
        IMobaEffectExecutionSnapshotReader, IService
    {
        private readonly MobaTraceRegistry _trace;
        private readonly SnapshotStorage _snapshots;
        private long _generation = 1;
        private readonly Func<bool> _enabledOverride;
        private bool _disposed;
        private readonly MobaBattleDiagnosticEventCollector _collector;

        public MobaEffectExecutionSnapshotStore(MobaTraceRegistry trace, MobaBattleDiagnosticEventCollector collector = null)
            : this(trace, 4096) { _collector = collector; }

        internal MobaEffectExecutionSnapshotStore(MobaTraceRegistry trace, int capacity, Func<bool> enabledOverride = null)
        {
            _trace = trace ?? throw new ArgumentNullException(nameof(trace));
            _enabledOverride = enabledOverride;
            _snapshots = new SnapshotStorage(capacity, 1);
            _snapshots.RegisterType<MobaEffectExecutionEntrySnapshot>("moba.effect.execution-entry", 1);
            _trace.RegistryEvent += OnTraceEvent;
        }

        public bool IsEnabled => !_disposed && (_enabledOverride != null ? _enabledOverride() :
            _collector != null && !_collector.IsFrozen && _collector.IsEnabled(BattleDiagnosticEventChannel.Skill));
        public long Revision { get; private set; }

        public void OnExecutionStarted(long traceContextId, int effectConfigId, int triggerId,
            in MobaCombatExecutionContext context)
        {
            if (!IsEnabled) return;
            TryCapture(traceContextId, effectConfigId, triggerId, context);
        }

        internal bool TryCapture(long traceContextId, int effectConfigId, int triggerId,
            in MobaCombatExecutionContext context)
        {
            try
            {
                if (_disposed || context.Frame < 0 || !_trace.TryGetNodeSnapshot(traceContextId, out var node) ||
                    node.Kind != (int)MobaTraceKind.EffectExecution || node.IsEnded ||
                    !(node.Metadata is MobaTraceMetadata metadata) || metadata.ExecutionSnapshot.IsValid)
                    return false;
                var payload = new MobaEffectExecutionEntrySnapshot(traceContextId, effectConfigId, triggerId, context);
                if (!_snapshots.TrySaveManaged(payload, new ContextSnapshotCapture(_generation, context.Frame, "execution-entry"), out var reference))
                    return false;
                metadata.ExecutionSnapshot = reference;
                Revision++;
                return true;
            }
            catch (Exception)
            {
                // Optional observation must not prevent effects, including malformed/throwing providers.
                return false;
            }
        }

        public BattleDiagnosticEffectExecutionFacts Read(in ContextSnapshotReference reference)
        {
            if (!reference.IsValid) return default;
            var status = _snapshots.Query(reference, out var record);
            if (status != ContextSnapshotQueryStatus.Found)
                return BattleDiagnosticEffectExecutionFacts.Missing(
                    status == ContextSnapshotQueryStatus.Unavailable ? BattleDiagnosticDataAvailability.Evicted : BattleDiagnosticDataAvailability.Error);
            var payload = (MobaEffectExecutionEntrySnapshot)record.Snapshot;
            return new BattleDiagnosticEffectExecutionFacts(BattleDiagnosticDataAvailability.Available,
                record.Reference.SnapshotId, record.Reference.Generation, record.Frame, record.TypeId, record.SchemaVersion,
                payload.EffectConfigId, payload.TriggerId, payload.PayloadTypeName, payload.HasRuntimeContext,
                payload.RuntimeContextId, payload.RuntimeContextVersion, payload.HasStageSnapshot,
                payload.Stage.StackCount, payload.Stage.ElapsedSeconds, payload.Stage.RemainingSeconds, payload.Stage.DurationSeconds);
        }

        private void OnTraceEvent(TraceRegistryEvent evt)
        {
            try
            {
                switch (evt.Kind)
                {
                    case TraceRegistryEventKind.RootPurged:
                        if (_snapshots.Remove(evt.ContextId)) Revision++;
                        break;
                    case TraceRegistryEventKind.PredictionRetracted:
                        _snapshots.RemoveFromEntityId(evt.ContextId);
                        Revision++;
                        break;
                    case TraceRegistryEventKind.RegistryCleared:
                        _snapshots.Clear();
                        _generation++;
                        Revision++;
                        break;
                }
            }
            catch (Exception) { }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _trace.RegistryEvent -= OnTraceEvent;
            _snapshots.Clear();
            Revision++;
        }
    }
}
