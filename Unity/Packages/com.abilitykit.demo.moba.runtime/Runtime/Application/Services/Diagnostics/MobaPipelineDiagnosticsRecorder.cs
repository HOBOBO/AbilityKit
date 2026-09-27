using System;
using System.Collections.Generic;
using AbilityKit.Pipeline;

namespace AbilityKit.Demo.Moba.Services
{
    public sealed class MobaPipelineDiagnosticsRecorder : IPipelineTraceRecorder
    {
        private static readonly IReadOnlyList<PipelineTraceEvent> EmptySnapshot = Array.Empty<PipelineTraceEvent>();

        private readonly IMobaBattleDiagnosticsService _diagnostics;

        public MobaPipelineDiagnosticsRecorder(IMobaBattleDiagnosticsService diagnostics)
        {
            _diagnostics = diagnostics;
        }

        public bool IsEnabled => _diagnostics != null && _diagnostics.IsEnabled(MobaBattleDiagnosticChannel.PipelineHook);

        public void Record(IPipelineLifeOwner owner, PipelineTraceData data)
        {
            if (_diagnostics == null || !_diagnostics.IsEnabled(MobaBattleDiagnosticChannel.PipelineHook)) return;

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

    }
}
