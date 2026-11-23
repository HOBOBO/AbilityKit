using System.Collections.Concurrent;

namespace AbilityKit.Pipeline.Tests;

internal sealed class RunDiagnosticSubscription : IDisposable
{
    private readonly IAbilityPipelineConfig[] _configs;
    private readonly Action<PipelineRunStartedData>? _onStart;
    private readonly Action<IPipelineLifeOwner, PipelineTraceData>? _onTrace;
    private readonly Action<PipelineRunEndedData>? _onEnd;
    private readonly ConcurrentDictionary<int, byte> _ownerIds = new();

    internal RunDiagnosticSubscription(
        IAbilityPipelineConfig[] configs,
        Action<PipelineRunStartedData>? onStart = null,
        Action<IPipelineLifeOwner, PipelineTraceData>? onTrace = null,
        Action<PipelineRunEndedData>? onEnd = null)
    {
        _configs = configs;
        _onStart = onStart;
        _onTrace = onTrace;
        _onEnd = onEnd;

        PipelineDebugHooks.OnRunStartedDetailed += CaptureStart;
        if (_onTrace != null)
            PipelineDebugHooks.OnTrace += CaptureTrace;
        if (_onEnd != null)
            PipelineDebugHooks.OnRunEnded += CaptureEnd;
    }

    private void CaptureStart(PipelineRunStartedData data)
    {
        if (!Array.Exists(_configs, config => ReferenceEquals(data.Config, config)))
            return;

        _ownerIds.TryAdd(data.Owner.OwnerId, 0);
        _onStart?.Invoke(data);
    }

    private void CaptureTrace(IPipelineLifeOwner owner, PipelineTraceData data)
    {
        if (_ownerIds.ContainsKey(owner.OwnerId))
            _onTrace?.Invoke(owner, data);
    }

    private void CaptureEnd(PipelineRunEndedData data)
    {
        if (_ownerIds.ContainsKey(data.Owner.OwnerId))
            _onEnd?.Invoke(data);
    }

    public void Dispose()
    {
        PipelineDebugHooks.OnRunStartedDetailed -= CaptureStart;
        if (_onTrace != null)
            PipelineDebugHooks.OnTrace -= CaptureTrace;
        if (_onEnd != null)
            PipelineDebugHooks.OnRunEnded -= CaptureEnd;
    }
}
