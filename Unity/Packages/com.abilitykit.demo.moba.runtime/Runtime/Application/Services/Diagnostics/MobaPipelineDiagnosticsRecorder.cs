using System;
using System.Collections.Generic;
using AbilityKit.Pipeline;
using AbilityKit.Trace;

namespace AbilityKit.Demo.Moba.Services
{
    public sealed class MobaPipelineDiagnosticsRecorder : IPipelineTraceRecorder
    {
        private static readonly IReadOnlyList<PipelineTraceEvent> EmptySnapshot = Array.Empty<PipelineTraceEvent>();

        private readonly IMobaBattleDiagnosticsService _diagnostics;
        private readonly Dictionary<IPipelineLifeOwner, ActivePhaseTrace> _activePhaseTraces =
            new Dictionary<IPipelineLifeOwner, ActivePhaseTrace>();

        public MobaPipelineDiagnosticsRecorder(IMobaBattleDiagnosticsService diagnostics)
        {
            _diagnostics = diagnostics;
        }

        public bool IsEnabled => _diagnostics != null && _diagnostics.IsEnabled(MobaBattleDiagnosticChannel.PipelineHook);

        public void Record(IPipelineLifeOwner owner, PipelineTraceData data)
        {
            if (_diagnostics == null || !_diagnostics.IsEnabled(MobaBattleDiagnosticChannel.PipelineHook)) return;

            try
            {
                RecordSkillPhaseTrace(owner, in data);
            }
            catch (Exception ex)
            {
                _diagnostics.Warning(
                    MobaBattleDiagnosticMetric.PipelinePhaseError,
                    () => $"[MobaPipelineDiagnosticsRecorder] Skill phase trace failed. owner={owner?.OwnerName ?? string.Empty} phase={data.PhaseId} type={data.Type} error={ex.Message}");
            }

            if (data.Type == EPipelineTraceEventType.PhaseError)
            {
                _diagnostics.Counter(MobaBattleDiagnosticMetric.PipelinePhaseError);
                _diagnostics.Warning(
                    MobaBattleDiagnosticMetric.PipelinePhaseError,
                    () => $"[MobaPipelineDiagnosticsRecorder] Pipeline phase error. owner={owner?.OwnerName ?? string.Empty} phase={data.PhaseId} state={data.State} message={data.Message}");
                return;
            }

            if (!_diagnostics.ShouldSample(MobaBattleDiagnosticChannel.PipelineHook)) return;

            _diagnostics.Counter(MobaBattleDiagnosticMetric.PipelineTraceEvent);
            _diagnostics.Counter(GetMetricName(data.Type));
        }

        private void RecordSkillPhaseTrace(IPipelineLifeOwner owner, in PipelineTraceData data)
        {
            if (owner == null || !(owner is IAbilityPipelineRun<SkillPipelineContext> run)) return;

            var context = run.Context;
            if (context == null) return;

            switch (data.Type)
            {
                case EPipelineTraceEventType.PhaseStart:
                    BeginPhaseTrace(owner, context, data.PhaseId.ToString());
                    break;
                case EPipelineTraceEventType.PhaseComplete:
                    EndPhaseTrace(owner, TraceLifecycleReason.Completed);
                    break;
                case EPipelineTraceEventType.PhaseError:
                    EndPhaseTrace(owner, TraceLifecycleReason.Failed);
                    break;
                case EPipelineTraceEventType.Interrupt:
                    EndPhaseTrace(owner, TraceLifecycleReason.Interrupted);
                    break;
                case EPipelineTraceEventType.RunEnd:
                    EndPhaseTrace(
                        owner,
                        data.State == EAbilityPipelineState.Completed
                            ? TraceLifecycleReason.Completed
                            : TraceLifecycleReason.Failed);
                    break;
            }
        }

        private void BeginPhaseTrace(
            IPipelineLifeOwner owner,
            SkillPipelineContext context,
            string phaseId)
        {
            EndPhaseTrace(owner, TraceLifecycleReason.Interrupted);
            if (context.PipelineTraceParentContextId == 0L || string.IsNullOrEmpty(phaseId) ||
                context.WorldServices == null ||
                !context.WorldServices.TryResolve<MobaTraceRegistry>(out var trace) || trace == null)
            {
                return;
            }

            var contextId = trace.CreateChildContext(
                context.PipelineTraceParentContextId,
                MobaTraceKind.SkillPhase,
                context.SkillId,
                context.CasterActorId,
                context.TargetActorId,
                TraceEndpoint.Config(MobaRuntimeKindNames.SkillPipeline, context.CastFlowId),
                TraceEndpoint.Actor(context.TargetActorId));
            if (contextId == 0L) return;

            trace.TrySetSkillPhaseLocation(
                contextId,
                context.SkillId,
                context.CastFlowId,
                phaseId);
            _activePhaseTraces[owner] = new ActivePhaseTrace(trace, contextId);
        }

        private void EndPhaseTrace(IPipelineLifeOwner owner, TraceLifecycleReason reason)
        {
            if (!_activePhaseTraces.TryGetValue(owner, out var active)) return;

            _activePhaseTraces.Remove(owner);
            active.Registry.EndContext(active.ContextId, reason);
        }

        public IPipelineRunTrace GetTrace(int ownerId)
        {
            return null;
        }

        public IReadOnlyList<PipelineTraceEvent> GetSnapshot(int ownerId)
        {
            return EmptySnapshot;
        }

        private static string GetMetricName(EPipelineTraceEventType type)
        {
            switch (type)
            {
                case EPipelineTraceEventType.RunStart:
                    return MobaBattleDiagnosticMetric.PipelineRunStarted;
                case EPipelineTraceEventType.RunEnd:
                    return MobaBattleDiagnosticMetric.PipelineRunEnded;
                case EPipelineTraceEventType.PhaseStart:
                    return MobaBattleDiagnosticMetric.PipelinePhaseStarted;
                case EPipelineTraceEventType.PhaseComplete:
                    return MobaBattleDiagnosticMetric.PipelinePhaseCompleted;
                case EPipelineTraceEventType.PhaseError:
                    return MobaBattleDiagnosticMetric.PipelinePhaseError;
                case EPipelineTraceEventType.Tick:
                    return MobaBattleDiagnosticMetric.PipelineTick;
                case EPipelineTraceEventType.Pause:
                    return MobaBattleDiagnosticMetric.PipelinePaused;
                case EPipelineTraceEventType.Resume:
                    return MobaBattleDiagnosticMetric.PipelineResumed;
                case EPipelineTraceEventType.Interrupt:
                    return MobaBattleDiagnosticMetric.PipelineInterrupted;
                default:
                    return MobaBattleDiagnosticMetric.PipelineTraceEvent;
            }
        }

        private readonly struct ActivePhaseTrace
        {
            public ActivePhaseTrace(MobaTraceRegistry registry, long contextId)
            {
                Registry = registry;
                ContextId = contextId;
            }

            public MobaTraceRegistry Registry { get; }
            public long ContextId { get; }
        }
    }
}
