using System.Collections.Immutable;
using AbilityKit.Orleans.Contracts.FrameSync;

namespace AbilityKit.Orleans.Grains.FrameSync;

internal sealed class SettledFrameHistory
{
    private readonly FrameSyncStartOptions _options;
    private readonly int _shortCapacity;
    private readonly int _recordingCapacity;
    private readonly Dictionary<int, FrameSyncSettledFrame> _recent = new();
    private readonly List<FrameSyncSettledFrame> _recording = new();

    public SettledFrameHistory(FrameSyncStartOptions options, int shortCapacity, int recordingCapacity)
    {
        _options = options;
        _shortCapacity = shortCapacity;
        _recordingCapacity = options.EnableRecording ? recordingCapacity : 0;
        Generation = options.InitialCheckpoint!.RuntimeGeneration;
        Store(new BattleFrameSettlement(Generation, 0, [], [], IsInitialCheckpoint: true));
    }

    public Guid Generation { get; }
    public int SimulationTickRate => _options.TickRate;
    public int LastSettledFrame { get; private set; }
    public int? FaultFrame { get; private set; }
    public int RecentCount => _recent.Count;
    public int RecordingCount => _recording.Count;

    public FrameSyncSettledFrame? Commit(int frame, BattleFrameSettlement? settlement, int submittedCount)
    {
        if (FaultFrame.HasValue) return null;
        if (frame != LastSettledFrame + 1 || settlement == null || settlement.Frame != frame ||
            settlement.RuntimeGeneration != Generation || settlement.IsInitialCheckpoint ||
            settlement.Commands.IsDefault || settlement.Decisions.IsDefault || settlement.Commands.Length != submittedCount)
            return Fault();

        for (var index = 0; index < settlement.Commands.Length; index++)
        {
            var command = settlement.Commands[index];
            if (command == null || command.Ordinal != index || command.ExecutedPayload.IsDefault ||
                command.Disposition is not (SettledCommandDisposition.Executed or SettledCommandDisposition.Rejected or SettledCommandDisposition.Duplicate) ||
                (command.Disposition != SettledCommandDisposition.Executed && !command.ExecutedPayload.IsEmpty))
                return Fault();
        }

        var previousOrdinal = -1;
        foreach (var decision in settlement.Decisions)
        {
            if (decision == null || decision.Ordinal < previousOrdinal || decision.Ordinal > submittedCount ||
                decision.AuthoritySequence <= 0 || (decision.PredictionKey > 0 &&
                    (decision.Ordinal < 0 || decision.Ordinal >= submittedCount || decision.AuthoritativeFrame > frame)))
                return Fault();
            previousOrdinal = decision.Ordinal;
        }

        return Store(settlement);
    }

    private FrameSyncSettledFrame? Fault()
    {
        FaultFrame = LastSettledFrame + 1;
        return null;
    }

    private FrameSyncSettledFrame Store(BattleFrameSettlement settlement)
    {
        var frame = new FrameSyncSettledFrame(FrameSyncSettledFrame.CurrentVersion,
            _options.RoomId, _options.WorldId, _options.BattleId!, settlement);
        _recent.Add(settlement.Frame, frame);
        _recent.Remove(settlement.Frame - _shortCapacity);
        if (_recording.Count < _recordingCapacity) _recording.Add(frame);
        LastSettledFrame = settlement.Frame;
        return frame;
    }

    public FrameSyncSettledCatchUpPayload? CatchUp(FrameSyncSettledCatchUpRequest request)
    {
        var from = request.FromFrameExclusive;
        var to = Math.Min(request.ToFrameInclusive, LastSettledFrame);
        if (request.RoomId != _options.RoomId || request.WorldId != _options.WorldId ||
            request.RuntimeGeneration != Generation || from < -1 || to <= from ||
            (FaultFrame.HasValue && request.ToFrameInclusive >= FaultFrame.Value))
            return null;

        var count = to - from;
        var useRecording = from == -1 || from + 1 <= LastSettledFrame - _recent.Count;
        if (useRecording && to >= _recording.Count) return null;

        var frames = ImmutableArray.CreateBuilder<FrameSyncSettledFrame>(count);
        for (var index = from + 1; index <= to; index++)
        {
            if (useRecording) frames.Add(_recording[index]);
            else if (_recent.TryGetValue(index, out var frame)) frames.Add(frame);
            else return null;
        }
        return new FrameSyncSettledCatchUpPayload(frames.MoveToImmutable());
    }

    public FrameSyncSettledRecording? DumpRecording() => _options.EnableRecording
        ? new FrameSyncSettledRecording(_options, _recording.ToImmutableArray(), LastSettledFrame, FaultFrame)
        : null;
}
