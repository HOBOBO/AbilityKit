using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Demo.Moba.Services.Observability;
using AbilityKit.Trace;

namespace AbilityKit.Demo.Moba.Services
{
    [WorldService(typeof(IBattleDiagnosticTraceReadStore), WorldLifetime.Scoped)]
    [WorldService(typeof(IBattleDiagnosticTraceRootReadStore), WorldLifetime.Scoped)]
    [WorldService(typeof(IBattleDiagnosticTraceSnapshotSource), WorldLifetime.Scoped)]
    public sealed class MobaBattleDiagnosticTraceReadStore :
        IBattleDiagnosticTraceReadStore,
        IBattleDiagnosticTraceRootReadStore,
        IBattleDiagnosticTraceSnapshotSource,
        IService
    {
        private static readonly TraceExportOptions QueryOptions = new TraceExportOptions(
            0,
            false,
            true,
            0,
            TraceExportOrder.TreePreOrder);

        private readonly MobaTraceRegistry _registry;

        [WorldInject(required: false)]
        private IMobaRuntimeObjectKeyResolver _runtimeObjectKeys = null;
        [WorldInject(required: false)] private IMobaEffectExecutionSnapshotReader _executionSnapshots = null;
        [WorldInject(required: false)] private IMobaActionExecutionSnapshotReader _actionSnapshots = null;

        public MobaBattleDiagnosticTraceReadStore(
            MobaTraceRegistry registry,
            IBattleDiagnosticEventReadStore eventStore)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            if (eventStore == null) throw new ArgumentNullException(nameof(eventStore));
            Scope = eventStore.Scope;
        }

        public BattleDiagnosticSessionScope Scope { get; }
        public long Revision => _registry.Revision + (_executionSnapshots?.Revision ?? 0) + (_actionSnapshots?.Revision ?? 0);

        public BattleDiagnosticTraceTrackSnapshot CaptureTraceSnapshot()
        {
            const int maximumAttempts = 3;
            var nodes = new List<BattleDiagnosticTraceNodeSummary>();
            var truncated = false;
            var revision = Revision;

            for (var attempt = 0; attempt < maximumAttempts; attempt++)
            {
                revision = Revision;
                nodes.Clear();
                truncated = false;
                var roots = _registry.ExportRoots(QueryOptions);
                foreach (var root in roots)
                {
                    truncated |= root.Truncated;
                    foreach (var node in root.Nodes)
                    {
                        nodes.Add(ToSummary(in node));
                    }
                }

                if (revision == Revision)
                {
                    return new BattleDiagnosticTraceTrackSnapshot(
                        revision,
                        nodes,
                        truncated,
                        true);
                }
            }

            return new BattleDiagnosticTraceTrackSnapshot(
                revision,
                nodes,
                truncated,
                false);
        }

        public BattleDiagnosticQueryResult<BattleDiagnosticTraceNodeSummary> QueryTrace(
            long requestId,
            long rootContextId)
        {
            if (requestId <= 0) throw new ArgumentOutOfRangeException(nameof(requestId));
            if (rootContextId == 0) throw new ArgumentOutOfRangeException(nameof(rootContextId));

            try
            {
                var export = _registry.ExportRoot(rootContextId, QueryOptions);
                if (export.Nodes.Count == 0)
                {
                    return BattleDiagnosticQueryResult<BattleDiagnosticTraceNodeSummary>.Unavailable(
                        requestId,
                        Revision,
                        Revision == 0
                            ? BattleDiagnosticDataAvailability.NotProduced
                            : BattleDiagnosticDataAvailability.Evicted,
                        Revision == 0
                            ? "No trace graph has been produced yet."
                            : $"Trace root {rootContextId} is not retained by the registry.");
                }

                var items = new List<BattleDiagnosticTraceNodeSummary>(export.Nodes.Count);
                foreach (var node in export.Nodes)
                {
                    items.Add(ToSummary(in node));
                }

                return BattleDiagnosticQueryResult<BattleDiagnosticTraceNodeSummary>.FromItems(
                    requestId,
                    Revision,
                    items,
                    export.Truncated);
            }
            catch (Exception ex)
            {
                return BattleDiagnosticQueryResult<BattleDiagnosticTraceNodeSummary>.Failed(
                    requestId,
                    Revision,
                    "QueryTrace.Exception",
                    ex.Message);
            }
        }

        public BattleDiagnosticQueryResult<BattleDiagnosticTraceRootSummary> QueryTraceRoots(
            BattleDiagnosticTraceRootQuery query)
        {
            var revision = Revision;
            if (query.Page.StoreRevision > 0L && query.Page.StoreRevision != revision)
            {
                return BattleDiagnosticQueryResult<BattleDiagnosticTraceRootSummary>.Unavailable(
                    query.RequestId,
                    query.Page.StoreRevision,
                    BattleDiagnosticDataAvailability.Evicted,
                    "The requested trace root index revision is no longer retained.");
            }

            try
            {
                var snapshot = CaptureTraceSnapshot();
                var roots = BattleDiagnosticTraceRootProjection.Project(snapshot.Nodes);
                var items = new List<BattleDiagnosticTraceRootSummary>(
                    Math.Min(query.Page.Limit, roots.Count));
                var end = Math.Min(roots.Count, query.Page.Offset + query.Page.Limit);
                for (var i = query.Page.Offset; i < end; i++) items.Add(roots[i]);

                if (snapshot.Truncated || !snapshot.IsStable)
                {
                    var message = !snapshot.IsStable
                        ? "Trace roots changed while the root index was captured."
                        : "One or more trace roots were truncated during export.";
                    return new BattleDiagnosticQueryResult<BattleDiagnosticTraceRootSummary>(
                        BattleDiagnosticQueryStatus.Partial(
                            query.RequestId,
                            snapshot.Revision,
                            items.Count,
                            BattleDiagnosticDataAvailability.Truncated,
                            message),
                        items);
                }

                return BattleDiagnosticQueryResult<BattleDiagnosticTraceRootSummary>.FromItems(
                    query.RequestId,
                    snapshot.Revision,
                    items,
                    end < roots.Count);
            }
            catch (Exception ex)
            {
                return BattleDiagnosticQueryResult<BattleDiagnosticTraceRootSummary>.Failed(
                    query.RequestId,
                    revision,
                    "QueryTraceRoots.Exception",
                    ex.Message);
            }
        }

        private BattleDiagnosticTraceNodeSummary ToSummary(in TraceNodeExportDto node)
        {
            var metadata = node.Metadata as MobaTraceMetadata;
            var sourceActor = ResolveActorReference(
                metadata?.SourceActorId ?? 0L,
                node.CreatedFrame);
            var targetActor = ResolveActorReference(
                metadata?.TargetActorId ?? 0L,
                node.CreatedFrame);
            return new BattleDiagnosticTraceNodeSummary(
                Scope,
                node.RootId,
                node.ContextId,
                node.ParentId,
                node.CreatedFrame,
                node.IsEnded ? node.EndedFrame : BattleDiagnosticFrames.Invalid,
                ResolveState(node.IsEnded, node.EndReason),
                sourceActor.RuntimeId,
                metadata?.ConfigId ?? 0,
                node.KindName ?? ((MobaTraceKind)node.Kind).ToString(),
                node.IsEnded ? ResolveEndReason(node.EndReason) : string.Empty,
                metadata?.SkillId ?? 0,
                metadata?.CastFlowId ?? 0,
                metadata?.PhaseId ?? string.Empty,
                targetActor.RuntimeId,
                metadata?.TriggerId ?? 0,
                sourceActor.Generation,
                targetActor.Generation,
                MobaTraceRegistry.ResolveDefinitionKind(node.Kind),
                (int)(metadata?.OriginKind ?? MobaTraceKind.None),
                metadata?.OriginConfigId ?? 0,
                MobaTraceRegistry.ResolveDefinitionKind((int)(metadata?.OriginKind ?? MobaTraceKind.None)),
                _executionSnapshots != null && metadata != null ? _executionSnapshots.Read(metadata.ExecutionSnapshot) : default,
                _actionSnapshots != null && metadata != null ? _actionSnapshots.Read(metadata.ActionSnapshot) : default);
        }

        private BattleDiagnosticRuntimeObjectReference ResolveActorReference(
            long actorId,
            int frame)
        {
            if (actorId == 0L) return default;

            var generation = 0;
            if (_runtimeObjectKeys != null &&
                _runtimeObjectKeys.TryResolve(
                    MobaRuntimeObjectKind.Actor,
                    actorId,
                    frame,
                    out var key))
            {
                generation = key.Generation;
            }

            return BattleDiagnosticRuntimeObjectReference.Create(
                BattleDiagnosticRuntimeObjectKind.Actor,
                actorId,
                generation);
        }

        private static BattleDiagnosticTraceNodeState ResolveState(bool isEnded, int reason)
        {
            if (!isEnded) return BattleDiagnosticTraceNodeState.Active;

            switch ((TraceLifecycleReason)reason)
            {
                case TraceLifecycleReason.Failed:
                    return BattleDiagnosticTraceNodeState.Failed;
                case TraceLifecycleReason.Cancelled:
                case TraceLifecycleReason.Dispelled:
                case TraceLifecycleReason.Dead:
                case TraceLifecycleReason.Replaced:
                case TraceLifecycleReason.Interrupted:
                case TraceLifecycleReason.Overridden:
                    return BattleDiagnosticTraceNodeState.ForceEnded;
                default:
                    return BattleDiagnosticTraceNodeState.Ended;
            }
        }

        private static string ResolveEndReason(int reason)
        {
            var lifecycleReason = (TraceLifecycleReason)reason;
            return Enum.IsDefined(typeof(TraceLifecycleReason), lifecycleReason)
                ? lifecycleReason.ToString()
                : reason.ToString();
        }

        public void Dispose()
        {
        }
    }
}
