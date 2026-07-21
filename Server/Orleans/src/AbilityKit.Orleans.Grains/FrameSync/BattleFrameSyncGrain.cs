using System;
using System.Collections.Generic;
using AbilityKit.Orleans.Contracts.FrameSync;
using Microsoft.Extensions.Logging;
using Orleans;

namespace AbilityKit.Orleans.Grains.FrameSync;

public sealed class BattleFrameSyncGrain : Grain, IBattleFrameSyncGrain
{
    private readonly ILogger<BattleFrameSyncGrain> _logger;
    private readonly HashSet<IFrameSyncObserver> _observers = new();

    // 按帧索引分组。
    private readonly Dictionary<int, List<FrameInputItem>> _inputsByFrame = new();

    private IDisposable? _timer;

    private ulong _roomId;
    private ulong _worldId;
    private string? _battleId;
    private string? _syncTemplateId;
    private int _frame;

    private DateTime _tickWindowStartUtc;
    private int _tickCountInWindow;
    private DateTime _lastTickUtc;
    private double _tickDeltaSumMs;
    private double _tickDeltaLastMs;

    private TimeSpan _tickInterval;
    private DateTime _nextTickDueUtc;

    private const int MaxCatchUpFramesPerTimer = 5;
    private const int MaxFutureLeadFrames = 120;

    private const int DefaultTickRate = 30;

    public BattleFrameSyncGrain(ILogger<BattleFrameSyncGrain> logger)
    {
        _logger = logger;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        var key = this.GetPrimaryKeyString();
        if (!ulong.TryParse(key, out _roomId))
        {
            throw new InvalidOperationException($"BattleFrameSyncGrain key must be numeric roomId. key='{key}'");
        }

        _frame = 0;

        var now = DateTime.UtcNow;
        _tickWindowStartUtc = now;
        _lastTickUtc = now;
        _tickCountInWindow = 0;
        _tickDeltaSumMs = 0;
        _tickDeltaLastMs = 0;

        _tickInterval = TimeSpan.FromSeconds(1.0 / DefaultTickRate);
        _nextTickDueUtc = now + _tickInterval;

        _timer = RegisterTimer(_ => OnTickAsync(), state: null, dueTime: _tickInterval, period: _tickInterval);
        return Task.CompletedTask;
    }

    public override Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        _timer?.Dispose();
        _timer = null;
        _inputsByFrame.Clear();
        return Task.CompletedTask;
    }

    public Task InitializeAsync(FrameSyncStartOptions options)
    {
        if (options is null) throw new ArgumentNullException(nameof(options));

        _roomId = options.RoomId != 0 ? options.RoomId : _roomId;
        _worldId = options.WorldId;
        _battleId = options.BattleId;
        _syncTemplateId = options.SyncTemplateId;

        var tickRate = options.TickRate > 0 ? options.TickRate : DefaultTickRate;
        _tickInterval = TimeSpan.FromSeconds(1.0 / tickRate);
        _nextTickDueUtc = DateTime.UtcNow + _tickInterval;

        _logger.LogInformation(
            "[BattleFrameSyncGrain] Initialized. RoomId={RoomId} WorldId={WorldId} BattleId={BattleId} TickRate={TickRate} SyncTemplate={SyncTemplate}",
            _roomId,
            _worldId,
            _battleId,
            tickRate,
            _syncTemplateId);
        return Task.CompletedTask;
    }

    public Task SubscribeAsync(IFrameSyncObserver observer)
    {
        if (observer == null) throw new ArgumentNullException(nameof(observer));
        _observers.Add(observer);
        return Task.CompletedTask;
    }

    public Task UnsubscribeAsync(IFrameSyncObserver observer)
    {
        if (observer == null) return Task.CompletedTask;
        _observers.Remove(observer);
        return Task.CompletedTask;
    }

    public async Task SubmitInputAsync(ulong worldId, int frame, FrameInputItem input)
    {
        await SubmitInputWithResultAsync(worldId, frame, input);
    }

    public Task<FrameInputSubmitResult> SubmitInputWithResultAsync(ulong worldId, int frame, FrameInputItem input)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));

        var reason = ValidateSubmission(_worldId, _frame, worldId, frame);
        if (reason != FrameInputSubmitReason.None)
        {
            return Task.FromResult(new FrameInputSubmitResult(false, _frame, reason));
        }

        if (!_inputsByFrame.TryGetValue(frame, out var list))
        {
            list = new List<FrameInputItem>(8);
            _inputsByFrame[frame] = list;
        }

        list.Add(input);
        _logger.LogInformation(
            "[BattleFrameSyncGrain] Input accepted. RoomId={RoomId} WorldId={WorldId} RequestedFrame={RequestedFrame} ServerFrame={ServerFrame} PlayerId={PlayerId} OpCode={OpCode} PayloadBytes={PayloadBytes}",
            _roomId,
            worldId,
            frame,
            _frame,
            input.PlayerId,
            input.OpCode,
            input.Payload?.Length ?? 0);
        return Task.FromResult(new FrameInputSubmitResult(true, _frame, FrameInputSubmitReason.None));
    }

    internal static FrameInputSubmitReason ValidateSubmission(
        ulong authoritativeWorldId,
        int serverFrame,
        ulong requestedWorldId,
        int requestedFrame)
    {
        if (requestedWorldId == 0
            || authoritativeWorldId == 0
            || requestedWorldId != authoritativeWorldId)
        {
            return FrameInputSubmitReason.WorldMismatch;
        }

        if (requestedFrame < 0)
        {
            return FrameInputSubmitReason.NegativeFrame;
        }

        if (requestedFrame < serverFrame)
        {
            return FrameInputSubmitReason.FrameAlreadyProcessed;
        }

        return requestedFrame > serverFrame + MaxFutureLeadFrames
            ? FrameInputSubmitReason.FrameTooFarAhead
            : FrameInputSubmitReason.None;
    }

    private Task OnTickAsync()
    {
        var now = DateTime.UtcNow;
        var deltaMs = (now - _lastTickUtc).TotalMilliseconds;
        _lastTickUtc = now;
        _tickDeltaLastMs = deltaMs;
        _tickDeltaSumMs += deltaMs;

        if (now < _nextTickDueUtc)
        {
            return Task.CompletedTask;
        }

        var lagTicks = (now - _nextTickDueUtc).Ticks;
        var intervalTicks = _tickInterval.Ticks;
        var due = intervalTicks > 0 ? (int)(lagTicks / intervalTicks) + 1 : 1;
        if (due < 1) due = 1;

        var toSend = due;
        if (toSend > MaxCatchUpFramesPerTimer) toSend = MaxCatchUpFramesPerTimer;

        for (int n = 0; n < toSend; n++)
        {
            var cur = _frame;

            List<FrameInputItem>? inputs;
            if (_inputsByFrame.TryGetValue(cur, out var list) && list != null && list.Count > 0)
            {
                inputs = list;
            }
            else
            {
                inputs = new List<FrameInputItem>(0);
            }

            _inputsByFrame.Remove(cur);

            if (inputs.Count > 0)
            {
                _logger.LogInformation(
                    "[BattleFrameSyncGrain] Emitting input frame. RoomId={RoomId} WorldId={WorldId} Frame={Frame} InputCount={InputCount} ObserverCount={ObserverCount}",
                    _roomId,
                    _worldId,
                    cur,
                    inputs.Count,
                    _observers.Count);
            }

            var evt = new FramePushedEvent(
                RoomId: _roomId,
                WorldId: _worldId,
                Frame: cur,
                Inputs: inputs);

            foreach (var o in _observers)
            {
                o.OnFramePushed(evt);
            }

            _frame = cur + 1;
            _tickCountInWindow++;
        }

        _nextTickDueUtc = _nextTickDueUtc + TimeSpan.FromTicks(intervalTicks * (long)toSend);

        if ((now - _tickWindowStartUtc).TotalSeconds >= 1.0)
        {
            var seconds = (now - _tickWindowStartUtc).TotalSeconds;
            var hz = seconds > 0 ? _tickCountInWindow / seconds : 0;
            var avgDelta = _tickCountInWindow > 0 ? _tickDeltaSumMs / _tickCountInWindow : 0;
            _logger.LogInformation("[BattleFrameSyncGrain] Tick stats. RoomId={RoomId} WorldId={WorldId} BattleId={BattleId} Frame={Frame} Obs={ObserverCount} Hz={Hz:F1} AvgDeltaMs={AvgDeltaMs:F2} LastDeltaMs={LastDeltaMs:F2}",
                _roomId, _worldId, _battleId, _frame, _observers.Count, hz, avgDelta, _tickDeltaLastMs);

            _tickWindowStartUtc = now;
            _tickCountInWindow = 0;
            _tickDeltaSumMs = 0;
        }

        return Task.CompletedTask;
    }
}
