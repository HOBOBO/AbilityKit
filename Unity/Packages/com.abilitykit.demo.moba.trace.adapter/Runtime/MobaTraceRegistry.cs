using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Core.Logging;
using AbilityKit.Context;
using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Trace;

namespace AbilityKit.Demo.Moba.Services
{
    [WorldService(typeof(MobaTraceRegistry))]
    [WorldService(typeof(IMobaOptionalHealthContributor))]
    public sealed class MobaTraceRegistry : TraceTreeRegistry<MobaTraceMetadata>,
        IService,
        IWorldInitializable,
        IWorldDeinitializable,
        IMobaOptionalHealthContributor
    {
        private readonly HashSet<long> _executionContextIds = new HashSet<long>();
        private MobaExecutionContextRegistry _executionContexts;
        private IDisposable _executionContextObservation;

        [WorldInject(required: false)] private IMobaBattleDiagnosticEventSink _eventCollector = null;
        [WorldInject(required: false)] private IFrameTime _frameTime = null;

        public MobaTraceRegistry()
            : base(new DictionaryTraceMetadataStore<MobaTraceMetadata>())
        {
            RegistryEvent += OnRegistryEvent;
        }

        protected override int Frame => _frameTime != null ? _frameTime.Frame.Value : 0;

        public void OnInit(IWorldResolver services)
        {
            if (services == null || !services.TryResolve(out MobaExecutionContextRegistry contexts) || contexts == null)
                return;

            AttachExecutionContexts(contexts);
        }

        public void OnDeinit(IWorldResolver services)
        {
            DetachExecutionContexts();
        }

        public override void Dispose()
        {
            DetachExecutionContexts();
            base.Dispose();
        }

        internal void AttachExecutionContexts(MobaExecutionContextRegistry contexts)
        {
            if (ReferenceEquals(_executionContexts, contexts)) return;
            DetachExecutionContexts();
            _executionContexts = contexts;
            _executionContextObservation = _executionContexts.Observe(
                OnExecutionContextEvent,
                replayExisting: true);
        }

        internal void DetachExecutionContexts()
        {
            if (_executionContexts == null) return;
            _executionContextObservation?.Dispose();
            _executionContextObservation = null;
            _executionContexts = null;
        }

        internal long CreateObservationRoot(
            MobaExecutionKind kind,
            int configId,
            long sourceActorId = 0,
            long targetActorId = 0,
            object originSource = null,
            object originTarget = null)
        {
            return CreateRoot((int)kind, sourceActorId, targetActorId, originSource, originTarget, configId);
        }

        internal long CreateObservationChild(
            long parentContextId,
            MobaExecutionKind kind,
            int configId,
            long sourceActorId = 0,
            long targetActorId = 0,
            object originSource = null,
            object originTarget = null)
        {
            return CreateChild(parentContextId, (int)kind, sourceActorId, targetActorId, originSource, originTarget, configId);
        }

        internal long RecordExecutionContext(
            in MobaExecutionContextNode node,
            object originSource = null,
            object originTarget = null)
        {
            if (node.ContextId == 0L)
                throw new ArgumentOutOfRangeException(nameof(node), "Execution context identity must be non-zero.");
            if (Contains(node.ContextId))
            {
                var existing = TryGetSnapshot(node.ContextId);
                if (existing.Kind == (int)NormalizeProjectedKind(node.Kind) &&
                    existing.RootId == node.RootContextId &&
                    existing.ParentId == node.ParentContextId &&
                    existing.Metadata != null &&
                    existing.Metadata.ConfigId == node.ConfigId &&
                    existing.Metadata.SourceActorId == node.SourceActorId &&
                    existing.Metadata.TargetActorId == node.TargetActorId)
                {
                    _executionContextIds.Add(node.ContextId);
                    return node.ContextId;
                }

                throw new InvalidOperationException(
                    $"Trace identity {node.ContextId} is already owned by another observation node.");
            }

            var recordedId = node.ParentContextId == 0L || !Contains(node.ParentContextId)
                ? CreateRootWithId(
                    node.ContextId,
                    (int)NormalizeProjectedKind(node.Kind),
                    node.SourceActorId,
                    node.TargetActorId,
                    originSource,
                    originTarget,
                    node.ConfigId,
                    node.CreatedFrame,
                    advanceAllocationFloor: false)
                : CreateChildWithId(
                    node.ContextId,
                    node.ParentContextId,
                    (int)NormalizeProjectedKind(node.Kind),
                    node.SourceActorId,
                    node.TargetActorId,
                    originSource,
                    originTarget,
                    node.ConfigId,
                    node.CreatedFrame,
                    advanceAllocationFloor: false);
            _executionContextIds.Add(recordedId);
            return recordedId;
        }

        internal int RetractExecutionContextPrediction(long nextContextId)
        {
            var ids = new List<long>();
            foreach (var contextId in _executionContextIds)
                if (contextId >= nextContextId)
                    ids.Add(contextId);
            return RetractExternalNodes(ids, nextContextId);
        }

        private void OnExecutionContextEvent(
            in ContextLifecycleEvent<MobaExecutionContextNode> evt)
        {
            try
            {
                switch (evt.Kind)
                {
                    case ContextLifecycleEventKind.Created:
                    {
                        var node = evt.Node;
                        RecordObservedExecutionContext(in node);
                        break;
                    }
                    case ContextLifecycleEventKind.Ended:
                        EndAtFrame(evt.Node.ContextId, evt.Node.EndReason, evt.Node.EndedFrame);
                        break;
                    case ContextLifecycleEventKind.PredictionRetracted:
                        RetractExecutionContextPrediction(evt.PredictionBoundary);
                        break;
                    case ContextLifecycleEventKind.Reconciled:
                        ReconcileExecutionContextProjection();
                        break;
                    case ContextLifecycleEventKind.Cleared:
                        RetractAllExecutionContexts();
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Exception(
                    ex,
                    $"[MobaTraceRegistry] Execution-context observation failed (event={evt.Kind}, contextId={evt.Node.ContextId}, boundary={evt.PredictionBoundary}).");
            }
        }

        private void RecordObservedExecutionContext(in MobaExecutionContextNode node)
        {
            var kind = NormalizeProjectedKind(node.Kind);
            var source = kind == MobaExecutionKind.SkillCast
                ? TraceEndpoint.Actor(node.SourceActorId)
                : ResolveProjectionEndpoint(kind, node.ConfigId);
            RecordExecutionContext(
                in node,
                source,
                TraceEndpoint.Actor(node.TargetActorId));
            if (node.CastFlowId != 0 || node.Kind == MobaExecutionKind.SkillCast)
                TrySetSkillPhaseLocation(node.ContextId, node.ConfigId, node.CastFlowId, string.Empty);
            if (node.TriggerId != 0)
                TrySetEffectTrigger(node.ContextId, node.TriggerId);
            if (node.OriginKind != MobaExecutionKind.None || node.OriginConfigId != 0)
                TrySetEffectOrigin(node.ContextId, NormalizeProjectedKind(node.OriginKind), node.OriginConfigId);
        }

        private void ReconcileExecutionContextProjection()
        {
            if (_executionContexts == null) return;
            var nodes = _executionContexts.CaptureLifecycleSnapshot();
            var currentIds = new HashSet<long>();
            for (var i = 0; i < nodes.Count; i++) currentIds.Add(nodes[i].ContextId);

            var staleIds = new List<long>();
            foreach (var contextId in _executionContextIds)
                if (!currentIds.Contains(contextId)) staleIds.Add(contextId);
            if (staleIds.Count > 0)
                RetractExternalNodes(staleIds, _executionContexts.NextContextId);

            var lifecycle = new List<TraceNodeSnapshot>(nodes.Count);
            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                RecordObservedExecutionContext(in node);
                var current = TryGetSnapshot(node.ContextId);
                lifecycle.Add(new TraceNodeSnapshot(
                    node.ContextId,
                    node.RootContextId,
                    node.ParentContextId,
                    (int)NormalizeProjectedKind(node.Kind),
                    node.CreatedFrame,
                    node.EndedFrame,
                    node.EndReason,
                    current.ChildCount,
                    current.Metadata,
                    node.IsEnded));
            }

            if (lifecycle.Count > 0) RestoreLifecycle(lifecycle);
        }

        private void RetractAllExecutionContexts()
        {
            if (_executionContextIds.Count == 0) return;
            var ids = new List<long>(_executionContextIds);
            var boundary = _executionContexts != null
                ? _executionContexts.NextContextId
                : MobaExecutionContextRegistry.FirstExecutionContextId;
            RetractExternalNodes(ids, boundary);
        }

        private static TraceEndpoint ResolveProjectionEndpoint(MobaExecutionKind kind, int configId)
        {
            switch (kind)
            {
                case MobaExecutionKind.SkillCast:
                case MobaExecutionKind.SkillEffect:
                case MobaExecutionKind.SkillPhase:
                    return TraceEndpoint.Config(MobaRuntimeKindNames.Skill, configId);
                case MobaExecutionKind.EffectExecution:
                    return TraceEndpoint.Config(MobaRuntimeKindNames.Effect, configId);
                case MobaExecutionKind.EffectAction:
                    return TraceEndpoint.Config(MobaRuntimeKindNames.Action, configId);
                case MobaExecutionKind.BuffApply:
                case MobaExecutionKind.BuffTick:
                case MobaExecutionKind.BuffRemove:
                    return TraceEndpoint.Config(MobaRuntimeKindNames.Buff, configId);
                case MobaExecutionKind.ProjectileLaunch:
                    return TraceEndpoint.Config(MobaRuntimeKindNames.Projectile, configId);
                case MobaExecutionKind.ProjectileHit:
                    return TraceEndpoint.Config(MobaRuntimeKindNames.ProjectileHit, configId);
                case MobaExecutionKind.AreaSpawn:
                case MobaExecutionKind.AreaExpire:
                    return TraceEndpoint.Config(MobaRuntimeKindNames.Area, configId);
                case MobaExecutionKind.AreaEnter:
                    return TraceEndpoint.Config(MobaRuntimeKindNames.AreaEnter, configId);
                case MobaExecutionKind.SummonSpawn:
                case MobaExecutionKind.SummonDeath:
                    return TraceEndpoint.Config(MobaRuntimeKindNames.Summon, configId);
                case MobaExecutionKind.PresentationPlay:
                case MobaExecutionKind.PresentationStop:
                    return TraceEndpoint.Config(MobaRuntimeKindNames.Presentation, configId);
                case MobaExecutionKind.DamageAttack:
                    return TraceEndpoint.Config(MobaRuntimeKindNames.DamageAttack, configId);
                case MobaExecutionKind.DamageCalc:
                    return TraceEndpoint.Config(MobaRuntimeKindNames.DamageCalc, configId);
                case MobaExecutionKind.DamageApply:
                    return TraceEndpoint.Config(MobaRuntimeKindNames.DamageResult, configId);
                default:
                    return TraceEndpoint.Config(MobaRuntimeKindNames.Action, configId);
            }
        }

        private static MobaExecutionKind NormalizeProjectedKind(MobaExecutionKind kind)
        {
            switch (kind)
            {
                case MobaExecutionKind.SkillCast: return MobaExecutionKind.SkillCast;
                case MobaExecutionKind.SkillEffect: return MobaExecutionKind.SkillEffect;
                case MobaExecutionKind.SkillPhase: return MobaExecutionKind.SkillPhase;
                case MobaExecutionKind.PassiveActivation: return MobaExecutionKind.SkillEffect;
                case MobaExecutionKind.EffectExecution: return MobaExecutionKind.EffectExecution;
                case MobaExecutionKind.EffectAction: return MobaExecutionKind.EffectAction;
                case MobaExecutionKind.BuffApply: return MobaExecutionKind.BuffApply;
                case MobaExecutionKind.BuffTick: return MobaExecutionKind.BuffTick;
                case MobaExecutionKind.BuffRemove: return MobaExecutionKind.BuffRemove;
                case MobaExecutionKind.ProjectileLaunch: return MobaExecutionKind.ProjectileLaunch;
                case MobaExecutionKind.ProjectileHit: return MobaExecutionKind.ProjectileHit;
                case MobaExecutionKind.AreaSpawn: return MobaExecutionKind.AreaSpawn;
                case MobaExecutionKind.AreaEnter: return MobaExecutionKind.AreaEnter;
                case MobaExecutionKind.AreaExit: return MobaExecutionKind.AreaExit;
                case MobaExecutionKind.AreaExpire: return MobaExecutionKind.AreaExpire;
                case MobaExecutionKind.AreaStay: return MobaExecutionKind.AreaStay;
                case MobaExecutionKind.SummonSpawn: return MobaExecutionKind.SummonSpawn;
                case MobaExecutionKind.SummonDeath: return MobaExecutionKind.SummonDeath;
                case MobaExecutionKind.UnitSpawn: return MobaExecutionKind.UnitSpawn;
                case MobaExecutionKind.UnitDespawn: return MobaExecutionKind.UnitDespawn;
                case MobaExecutionKind.UnitDeath: return MobaExecutionKind.UnitDeath;
                case MobaExecutionKind.UnitRespawn: return MobaExecutionKind.UnitRespawn;
                case MobaExecutionKind.DamageAttack: return MobaExecutionKind.DamageAttack;
                case MobaExecutionKind.DamageCalc: return MobaExecutionKind.DamageCalc;
                case MobaExecutionKind.DamageApply: return MobaExecutionKind.DamageApply;
                case MobaExecutionKind.PresentationPlay: return MobaExecutionKind.PresentationPlay;
                case MobaExecutionKind.PresentationStop: return MobaExecutionKind.PresentationStop;
                default: return MobaExecutionKind.None;
            }
        }

        protected override void OnPredictionNodeRemoved(long contextId)
        {
            _executionContextIds.Remove(contextId);
            base.OnPredictionNodeRemoved(contextId);
        }

        protected override void OnClear()
        {
            _executionContextIds.Clear();
            base.OnClear();
        }

        public bool EndContext(long contextId, MobaExecutionEndReason reason)
        {
            return End(contextId, (int)reason);
        }

        public bool EndContext(long contextId, int reason = 0)
        {
            return End(contextId, reason);
        }

        public List<TraceSnapshot<MobaTraceMetadata>> GetChain(long rootId)
        {
            var list = new List<TraceSnapshot<MobaTraceMetadata>>();
            foreach (var snapshot in GetNodesByRoot(rootId)) list.Add(snapshot);
            return list;
        }

        public MobaOptionalHealthContribution CollectHealth(
            IMobaBattleDiagnosticsService diagnostics,
            int currentFrame,
            string warningKeyPrefix)
        {
            var scan = this.ScanRetention(
                diagnostics,
                600,
                currentFrame,
                warningKeyPrefix);
            var metrics = new Dictionary<string, double>
            {
                ["trace.roots"] = scan.TotalRoots,
                ["trace.active_roots"] = scan.ActiveRoots,
                ["trace.retained_roots"] = scan.RetainedRoots,
                ["trace.retained_ended_roots"] = scan.RetainedEndedRoots,
                ["trace.stale_retained_roots"] = scan.StaleRetainedRoots,
            };
            var findings = new List<MobaOptionalHealthFinding>(2);
            if (scan.RetainedEndedRoots > 0)
            {
                findings.Add(new MobaOptionalHealthFinding(
                    MobaRuntimeValidationSeverity.Warning,
                    "retention.ended",
                    $"Ended trace roots are still externally retained. retainedEndedRoots={scan.RetainedEndedRoots}.",
                    "moba.runtime.health.trace_retained_ended"));
            }
            if (scan.StaleRetainedRoots > 0)
            {
                findings.Add(new MobaOptionalHealthFinding(
                    MobaRuntimeValidationSeverity.Warning,
                    "retention.stale",
                    $"Stale retained trace roots detected. staleRetainedRoots={scan.StaleRetainedRoots}.",
                    "moba.runtime.health.trace_retained_stale"));
            }

            return new MobaOptionalHealthContribution("trace.observation", metrics, findings);
        }

        public bool ValidateChain(long rootId)
        {
            return ValidateChainDetailed(rootId).IsValid;
        }

        public MobaTraceValidationResult ValidateChainDetailed(long rootId)
        {
            return MobaTraceValidation.Validate(this, rootId);
        }

        public bool TrySetSkillPhaseLocation(
            long contextId,
            int skillId,
            int castFlowId,
            string phaseId)
        {
            if (contextId == 0 || !TryGetNodeSnapshot(contextId, out var snapshot) ||
                !(snapshot.Metadata is MobaTraceMetadata metadata))
            {
                return false;
            }

            metadata.SkillId = skillId;
            metadata.CastFlowId = castFlowId;
            metadata.PhaseId = phaseId ?? string.Empty;
            return true;
        }

        public bool TrySetEffectTrigger(long contextId, int triggerId)
        {
            if (contextId == 0 || !TryGetNodeSnapshot(contextId, out var snapshot) ||
                !(snapshot.Metadata is MobaTraceMetadata metadata))
            {
                return false;
            }

            metadata.TriggerId = triggerId;
            return true;
        }

        public bool TrySetEffectOrigin(long contextId, MobaExecutionKind originKind, int originConfigId)
        {
            if (contextId == 0 || !TryGetNodeSnapshot(contextId, out var snapshot) ||
                !(snapshot.Metadata is MobaTraceMetadata metadata))
            {
                return false;
            }

            metadata.OriginKind = originKind;
            metadata.OriginConfigId = originConfigId;
            return true;
        }

        public override string GetKindName(int kind)
        {
            return ((MobaExecutionKind)kind).ToString();
        }

        protected override MobaTraceMetadata CreateMetadata(
            long rootId,
            int kind,
            long sourceActorId,
            long targetActorId,
            long originId,
            string originDisplay,
            long targetId,
            string targetDisplay,
            int configId)
        {
            return new MobaTraceMetadata
            {
                RootId = rootId,
                ParentId = 0,
                ExecutionKind = (MobaExecutionKind)kind,
                ConfigId = configId,
                SourceActorId = sourceActorId,
                TargetActorId = targetActorId,
                SourceId = sourceActorId,
                TargetId = targetActorId,
                OriginSourceId = originId,
                OriginSource = originDisplay,
                OriginTargetId = targetId,
                OriginTarget = targetDisplay
            };
        }

        protected override long GetSourceActorId(MobaTraceMetadata metadata) => metadata.SourceActorId;
        protected override long GetTargetActorId(MobaTraceMetadata metadata) => metadata.TargetActorId;
        protected override long GetOriginSourceId(MobaTraceMetadata metadata) => metadata.OriginSourceId;
        protected override string GetOriginSourceDisplay(MobaTraceMetadata metadata) => metadata.OriginSource;
        protected override long GetOriginTargetId(MobaTraceMetadata metadata) => metadata.OriginTargetId;
        protected override string GetOriginTargetDisplay(MobaTraceMetadata metadata) => metadata.OriginTarget;

        // ===== TraceNode 诊断 Producer =====

        internal void AttachDiagnosticCollector(IMobaBattleDiagnosticEventSink collector)
        {
            _eventCollector = collector;
        }

        internal void AttachFrameTime(IFrameTime frameTime)
        {
            _frameTime = frameTime;
        }

        private void OnRegistryEvent(TraceRegistryEvent evt)
        {
            if (_eventCollector == null || !_eventCollector.IsEnabled(BattleDiagnosticEventChannel.Skill)) return;
            if (evt.Kind != TraceRegistryEventKind.RootCreated
                && evt.Kind != TraceRegistryEventKind.ChildCreated
                && evt.Kind != TraceRegistryEventKind.NodeEnded
                && evt.Kind != TraceRegistryEventKind.PredictionRetracted)
                return;

            try
            {
                if (evt.Kind == TraceRegistryEventKind.PredictionRetracted)
                {
                    var retraction = new MobaBattleDiagnosticEventDraft(
                        BattleDiagnosticEventKind.TracePredictionRetracted,
                        BattleDiagnosticEventChannel.Skill,
                        BattleDiagnosticEventOutcome.Succeeded,
                        summary: $"Retracted predicted Trace allocations: [{evt.ContextId}, {NextContextId}). Historical events in this range are no longer current.");
                    _eventCollector.TryCollect(in retraction);
                    return;
                }
                if (evt.Kind == TraceRegistryEventKind.NodeEnded)
                {
                    if (!TryResolveTraceNodeFields(evt.ContextId, out var kind, out var configId, out var sourceActorId, out var targetActorId))
                        return;
                    var draft = CreateTraceNodeEndedDraft(
                        evt.ContextId,
                        evt.RootId,
                        evt.ParentId,
                        kind,
                        configId,
                        sourceActorId,
                        targetActorId,
                        evt.Reason);
                    _eventCollector.TryCollect(in draft);
                }
                else
                {
                    if (!TryResolveTraceNodeFields(evt.ContextId, out var kind, out var configId, out var sourceActorId, out var targetActorId))
                        return;
                    var draft = CreateTraceNodeStartedDraft(
                        evt.ContextId,
                        evt.RootId,
                        evt.ParentId,
                        kind,
                        configId,
                        sourceActorId,
                        targetActorId);
                    _eventCollector.TryCollect(in draft);
                }
            }
            catch (Exception) { }
        }

        private bool TryResolveTraceNodeFields(
            long contextId,
            out int kind,
            out int configId,
            out long sourceActorId,
            out long targetActorId)
        {
            kind = 0;
            configId = 0;
            sourceActorId = 0;
            targetActorId = 0;
            if (!TryGetNodeSnapshot(contextId, out var snapshot))
                return false;
            kind = snapshot.Kind;
            if (snapshot.Metadata is MobaTraceMetadata metadata)
            {
                configId = metadata.ConfigId;
                sourceActorId = metadata.SourceActorId;
                targetActorId = metadata.TargetActorId;
            }
            return true;
        }

        internal static MobaBattleDiagnosticEventDraft CreateTraceNodeStartedDraft(
            long contextId,
            long rootContextId,
            long parentContextId,
            int executionKind,
            int configId,
            long sourceActorId,
            long targetActorId)
        {
            var resolvedRoot = rootContextId != 0L ? rootContextId : contextId;
            var summary = $"executionKind={executionKind}, configId={configId}, contextId={contextId}, parentContextId={parentContextId}";

            return new MobaBattleDiagnosticEventDraft(
                BattleDiagnosticEventKind.TraceNodeStarted,
                BattleDiagnosticEventChannel.Skill,
                BattleDiagnosticEventOutcome.Succeeded,
                sourceActorId,
                targetActorId,
                configId,
                resolvedRoot,
                contextId,
                summary: summary,
                definitionKind: ResolveDefinitionKind(executionKind));
        }

        internal static MobaBattleDiagnosticEventDraft CreateTraceNodeEndedDraft(
            long contextId,
            long rootContextId,
            long parentContextId,
            int executionKind,
            int configId,
            long sourceActorId,
            long targetActorId,
            int reason)
        {
            var resolvedRoot = rootContextId != 0L ? rootContextId : contextId;
            var summary = $"executionKind={executionKind}, configId={configId}, contextId={contextId}, parentContextId={parentContextId}, reason={reason}";

            return new MobaBattleDiagnosticEventDraft(
                BattleDiagnosticEventKind.TraceNodeEnded,
                BattleDiagnosticEventChannel.Skill,
                BattleDiagnosticEventOutcome.Succeeded,
                sourceActorId,
                targetActorId,
                configId,
                resolvedRoot,
                contextId,
                summary: summary,
                definitionKind: ResolveDefinitionKind(executionKind));
        }

        internal static BattleDiagnosticDefinitionKind ResolveDefinitionKind(int executionKind)
        {
            switch ((MobaExecutionKind)executionKind)
            {
                case MobaExecutionKind.SkillCast:
                case MobaExecutionKind.SkillEffect:
                case MobaExecutionKind.SkillPhase:
                    return BattleDiagnosticDefinitionKind.Skill;
                case MobaExecutionKind.EffectExecution:
                    return BattleDiagnosticDefinitionKind.Effect;
                case MobaExecutionKind.EffectAction:
                    return BattleDiagnosticDefinitionKind.Action;
                case MobaExecutionKind.BuffApply:
                case MobaExecutionKind.BuffTick:
                case MobaExecutionKind.BuffRemove:
                    return BattleDiagnosticDefinitionKind.Buff;
                case MobaExecutionKind.ProjectileLaunch:
                case MobaExecutionKind.ProjectileHit:
                    return BattleDiagnosticDefinitionKind.Projectile;
                case MobaExecutionKind.AreaSpawn:
                case MobaExecutionKind.AreaEnter:
                case MobaExecutionKind.AreaExit:
                case MobaExecutionKind.AreaExpire:
                case MobaExecutionKind.AreaStay:
                    return BattleDiagnosticDefinitionKind.Area;
                case MobaExecutionKind.SummonSpawn:
                case MobaExecutionKind.SummonDeath:
                    return BattleDiagnosticDefinitionKind.Summon;
                default:
                    return BattleDiagnosticDefinitionKind.Unknown;
            }
        }
    }
}
