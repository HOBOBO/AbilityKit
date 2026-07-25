using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Trace;

namespace AbilityKit.Demo.Moba.Services
{
    [WorldService(typeof(IBattleDiagnosticTraceReadStore), WorldLifetime.Scoped)]
    [WorldService(typeof(IBattleDiagnosticTraceSnapshotSource), WorldLifetime.Scoped)]
    public sealed class MobaBattleDiagnosticTraceReadStore :
        IBattleDiagnosticTraceReadStore,
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

        public MobaBattleDiagnosticTraceReadStore(
            MobaTraceRegistry registry,
            IBattleDiagnosticEventReadStore eventStore)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            if (eventStore == null) throw new ArgumentNullException(nameof(eventStore));
            Scope = eventStore.Scope;
        }

        public BattleDiagnosticSessionScope Scope { get; }
        public long Revision => _registry.Revision;

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

        private BattleDiagnosticTraceNodeSummary ToSummary(in TraceNodeExportDto node)
        {
            var metadata = node.Metadata as MobaTraceMetadata;
            return new BattleDiagnosticTraceNodeSummary(
                Scope,
                node.RootId,
                node.ContextId,
                node.ParentId,
                node.CreatedFrame,
                node.IsEnded ? node.EndedFrame : BattleDiagnosticFrames.Invalid,
                ResolveState(node.IsEnded, node.EndReason),
                metadata?.SourceActorId ?? 0,
                metadata?.ConfigId ?? 0,
                node.KindName ?? ((MobaTraceKind)node.Kind).ToString(),
                node.IsEnded ? ResolveEndReason(node.EndReason) : string.Empty);
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
            return Enum.IsDefined(typeof(TraceLifecycleReason), reason)
                ? ((TraceLifecycleReason)reason).ToString()
                : reason.ToString();
        }

        public void Dispose()
        {
        }
    }
}
