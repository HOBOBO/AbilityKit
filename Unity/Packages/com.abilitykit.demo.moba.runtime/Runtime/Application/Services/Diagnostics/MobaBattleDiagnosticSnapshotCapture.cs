using System;
using System.Collections.Generic;
using System.Diagnostics;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Demo.Moba.Diagnostics;

namespace AbilityKit.Demo.Moba.Services
{
    public interface IMobaBattleDiagnosticSnapshotCapture
    {
        BattleDiagnosticSessionSnapshot CaptureSnapshot();
    }

    [WorldService(typeof(IMobaBattleDiagnosticSnapshotCapture), WorldLifetime.Scoped)]
    public sealed class MobaBattleDiagnosticSnapshotCapture :
        IMobaBattleDiagnosticSnapshotCapture,
        IService
    {
        private readonly IBattleDiagnosticReadOnlySession _session;
        private readonly IBattleDiagnosticEventSnapshotSource _events;
        private readonly IBattleDiagnosticStateSnapshotSource _state;
        private readonly IBattleDiagnosticTraceSnapshotSource _trace;
        private readonly IBattleDiagnosticAttributeSnapshotSource _attributes;
        private readonly IBattleDiagnosticBuffSnapshotSource _buffs;
        private readonly IBattleDiagnosticTagSnapshotSource _tags;
        private readonly IBattleDiagnosticEffectSnapshotSource _effects;
        private readonly Func<long> _timestampProvider;

        [WorldInject(required: false)]
        private IBattleDiagnosticObjectCatalogSnapshotSource _objects = null;

        [WorldInject(required: false)]
        private IBattleDiagnosticMetricSnapshotSource _frameMetrics = null;

        [WorldInject(required: false)]
        private IBattleDiagnosticDefinitionCatalogSnapshotSource _definitions = null;

        public MobaBattleDiagnosticSnapshotCapture(
            IBattleDiagnosticReadOnlySession session,
            IBattleDiagnosticEventSnapshotSource events,
            IBattleDiagnosticStateSnapshotSource state,
            IBattleDiagnosticTraceSnapshotSource trace,
            IBattleDiagnosticAttributeSnapshotSource attributes,
            IBattleDiagnosticBuffSnapshotSource buffs,
            IBattleDiagnosticTagSnapshotSource tags,
            IBattleDiagnosticEffectSnapshotSource effects)
            : this(
                session,
                events,
                state,
                trace,
                attributes,
                buffs,
                tags,
                effects,
                Stopwatch.GetTimestamp)
        {
        }

        internal MobaBattleDiagnosticSnapshotCapture(
            IBattleDiagnosticReadOnlySession session,
            IBattleDiagnosticEventSnapshotSource events,
            IBattleDiagnosticStateSnapshotSource state,
            IBattleDiagnosticTraceSnapshotSource trace,
            IBattleDiagnosticAttributeSnapshotSource attributes,
            IBattleDiagnosticBuffSnapshotSource buffs,
            IBattleDiagnosticTagSnapshotSource tags,
            IBattleDiagnosticEffectSnapshotSource effects,
            Func<long> timestampProvider)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _trace = trace ?? throw new ArgumentNullException(nameof(trace));
            _attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));
            _buffs = buffs ?? throw new ArgumentNullException(nameof(buffs));
            _tags = tags ?? throw new ArgumentNullException(nameof(tags));
            _effects = effects ?? throw new ArgumentNullException(nameof(effects));
            _timestampProvider = timestampProvider ?? throw new ArgumentNullException(nameof(timestampProvider));

            var scope = _session.SessionInfo.Scope;
            EnsureScope(scope, _events.Scope, nameof(events));
            EnsureScope(scope, _state.Scope, nameof(state));
            EnsureScope(scope, _trace.Scope, nameof(trace));
            EnsureScope(scope, _attributes.Scope, nameof(attributes));
            EnsureScope(scope, _buffs.Scope, nameof(buffs));
            EnsureScope(scope, _tags.Scope, nameof(tags));
            EnsureScope(scope, _effects.Scope, nameof(effects));
        }

        public BattleDiagnosticSessionSnapshot CaptureSnapshot()
        {
            var capturedAtTimestamp = _timestampProvider();
            var events = _events.CaptureEventSnapshot();
            var state = _state.CaptureStateSnapshot();
            var trace = _trace.CaptureTraceSnapshot();
            var attributes = _attributes.CaptureAttributeSnapshot();
            var buffs = _buffs.CaptureBuffSnapshot();
            var tags = _tags.CaptureTagSnapshot();
            var effects = _effects.CaptureEffectSnapshot();
            var sourceSessionInfo = _session.SessionInfo;
            var effectiveCapabilities = _definitions != null
                ? sourceSessionInfo.Capabilities | BattleDiagnosticCapabilities.Definitions
                : sourceSessionInfo.Capabilities & ~BattleDiagnosticCapabilities.Definitions;
            var sessionInfo = effectiveCapabilities == sourceSessionInfo.Capabilities
                ? sourceSessionInfo
                : new BattleDiagnosticSessionInfo(
                    sourceSessionInfo.Scope,
                    sourceSessionInfo.DisplayName,
                    sourceSessionInfo.BuildId,
                    sourceSessionInfo.SchemaVersion,
                    sourceSessionInfo.MonotonicTimestampFrequency,
                    effectiveCapabilities,
                    sourceSessionInfo.ConnectionState,
                    sourceSessionInfo.CaptureState);
            var objects = _objects != null
                ? _objects.CaptureObjectCatalogSnapshot()
                : BattleDiagnosticObjectCatalogSnapshot.Empty(sessionInfo.Scope);
            EnsureScope(sessionInfo.Scope, objects.Scope, nameof(_objects));
            var frameMetrics = _frameMetrics != null
                ? _frameMetrics.CaptureMetricSnapshot()
                : BattleDiagnosticMetricTrackSnapshot.Empty;
            if (_frameMetrics != null)
                EnsureScope(sessionInfo.Scope, _frameMetrics.Scope, nameof(_frameMetrics));
            var definitions = _definitions != null
                ? _definitions.CaptureDefinitionCatalogSnapshot(
                    sessionInfo.Scope,
                    CollectDefinitionReferences(events, state, trace, buffs, objects))
                : BattleDiagnosticDefinitionCatalogSnapshot.Empty(sessionInfo.Scope);
            EnsureScope(sessionInfo.Scope, definitions.Scope, nameof(_definitions));

            return new BattleDiagnosticSessionSnapshot(
                in sessionInfo,
                capturedAtTimestamp,
                events,
                state,
                trace,
                attributes,
                buffs,
                tags,
                effects,
                objects,
                frameMetrics,
                definitions);
        }

        private static IReadOnlyList<BattleDiagnosticDefinitionReference>
            CollectDefinitionReferences(
                BattleDiagnosticEventTrackSnapshot events,
                BattleDiagnosticStateTrackSnapshot state,
                BattleDiagnosticTraceTrackSnapshot trace,
                BattleDiagnosticLatestTrackSnapshot<BattleDiagnosticActorBuff> buffs,
                BattleDiagnosticObjectCatalogSnapshot objects)
        {
            var result = new List<BattleDiagnosticDefinitionReference>();
            var seen = new HashSet<BattleDiagnosticDefinitionReference>();
            for (var i = 0; i < state.Actors.Count; i++)
                AddDefinition(
                    result,
                    seen,
                    BattleDiagnosticDefinitionKind.Actor,
                    state.Actors[i].ConfigId);
            for (var i = 0; i < events.Events.Count; i++)
            {
                var item = events.Events[i];
                AddDefinition(result, seen, item.DefinitionKind, item.ConfigId);
                if (item.Payload.TryGetTriggerAnalysis(out var triggerPayload))
                    AddDefinition(
                        result,
                        seen,
                        BattleDiagnosticDefinitionKind.Trigger,
                        triggerPayload.TriggerId);
            }
            for (var i = 0; i < trace.Nodes.Count; i++)
            {
                var item = trace.Nodes[i];
                AddDefinition(result, seen, item.Definition);
                AddDefinition(result, seen, item.TriggerDefinition);
                AddDefinition(result, seen, item.SkillDefinition);
            }
            for (var i = 0; i < buffs.Items.Count; i++)
                AddDefinition(
                    result,
                    seen,
                    BattleDiagnosticDefinitionKind.Buff,
                    buffs.Items[i].BuffId);
            for (var i = 0; i < objects.Items.Count; i++)
                AddDefinition(
                    result,
                    seen,
                    objects.Items[i].DefinitionKind,
                    objects.Items[i].DefinitionId);
            return result;
        }

        private static void AddDefinition(
            List<BattleDiagnosticDefinitionReference> result,
            HashSet<BattleDiagnosticDefinitionReference> seen,
            BattleDiagnosticDefinitionKind kind,
            int definitionId)
        {
            AddDefinition(
                result,
                seen,
                BattleDiagnosticDefinitionReference.Create(kind, definitionId));
        }

        private static void AddDefinition(
            List<BattleDiagnosticDefinitionReference> result,
            HashSet<BattleDiagnosticDefinitionReference> seen,
            BattleDiagnosticDefinitionReference reference)
        {
            if (!reference.HasDefinitionId ||
                reference.Kind == BattleDiagnosticDefinitionKind.Unknown ||
                !seen.Add(reference)) return;
            result.Add(reference);
        }

        public void Dispose()
        {
        }

        private static void EnsureScope(
            BattleDiagnosticSessionScope expected,
            BattleDiagnosticSessionScope actual,
            string parameterName)
        {
            if (expected != actual)
            {
                throw new ArgumentException(
                    "All diagnostic snapshot sources must use the session scope.",
                    parameterName);
            }
        }
    }
}
