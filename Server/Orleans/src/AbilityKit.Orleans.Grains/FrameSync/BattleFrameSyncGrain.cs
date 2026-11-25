using System;
using System.Collections.Generic;
using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Contracts.FrameSync;
using AbilityKit.Orleans.Grains.Gameplay;
using Microsoft.Extensions.Logging;
using Orleans;

namespace AbilityKit.Orleans.Grains.FrameSync;

public sealed class BattleFrameSyncGrain : Grain, IBattleFrameSyncGrain
{
    private readonly ILogger<BattleFrameSyncGrain> _logger;
    private readonly HashSet<IFrameSyncObserver> _observers = new();
    private readonly HashSet<ISettledFrameSyncObserver> _settledObservers = new();

    // 按帧索引分组。
    private readonly Dictionary<int, List<FrameInputItem>> _inputsByFrame = new();

    private IDisposable? _timer;

    private ulong _roomId;
    private ulong _worldId;
    private string? _battleId;
    private string? _syncTemplateId;
    private int _runtimeMode = (int)ServerBattleRuntimeMode.FrameRelayOnly;
    private bool _enableRecording;
    private bool _initialized;
    private int _minTickRate = 10;
    private int _maxTickRate = 60;
    private int _frame;
    private FrameSyncInitialCheckpoint? _initialCheckpoint;
    private Guid _settlementGeneration;
    private int? _lastSettledFrame;
    private SettledFrameHistory? _settledHistory;

    private DateTime _tickWindowStartUtc;
    private int _tickCountInWindow;
    private DateTime _lastTickUtc;
    private double _tickDeltaSumMs;
    private double _tickDeltaLastMs;

    private TimeSpan _tickInterval;
    private DateTime _nextTickDueUtc;

    private const int MaxCatchUpFramesPerTimer = 5;
    private const int MaxFutureLeadFrames = 120;
    private const int MaxHistoryFrames = 600;

    private const int DefaultTickRate = 30;

    /// <summary>完整输入历史上限：3 小时 @ 60fps，供新进程从确定性初始基线追帧。</summary>
    private const int MaxRecordingFrames = 648000;

    private DateTime _startedAtUtc;
    private int _totalInputCount;

    /// <summary>帧输入历史 ring buffer，用于 CatchUp 追帧。</summary>
    private readonly SortedDictionary<int, List<FrameInputItem>> _inputHistory = new();

    /// <summary>完整帧输入录制（不修剪），仅在 EnableRecording 时使用。</summary>
    private readonly List<List<FrameInputItem>> _fullRecording = new();

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
        _startedAtUtc = now;
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
        _inputHistory.Clear();
        _fullRecording.Clear();
        _settledHistory = null;
        _settledObservers.Clear();
        return Task.CompletedTask;
    }

    public Task InitializeAsync(FrameSyncStartOptions options)
    {
        if (options is null) throw new ArgumentNullException(nameof(options));

        var checkpoint = options.InitialCheckpoint;
        if (options.UseSettledFrames && (checkpoint == null || options.RoomId == 0 ||
            (_roomId != 0 && _roomId != options.RoomId)))
            throw new ArgumentException("Settled frames require a room-bound frame zero checkpoint.", nameof(options));
        if (checkpoint != null && (checkpoint.RuntimeGeneration == Guid.Empty ||
            options.RuntimeMode != (int)ServerBattleRuntimeMode.BattleWorldWithFrameSync ||
            options.WorldId == 0 || string.IsNullOrEmpty(options.BattleId)))
        {
            throw new ArgumentException("Frame zero checkpoint requires an initialized battle runtime.", nameof(options));
        }

        var sameBattle = _initialized && _battleId == options.BattleId;
        if (sameBattle && ((_settledHistory != null) != options.UseSettledFrames ||
            (_settledHistory != null && (_worldId != options.WorldId || _enableRecording != options.EnableRecording ||
                _runtimeMode != options.RuntimeMode || _syncTemplateId != options.SyncTemplateId ||
                _initialCheckpoint != checkpoint))))
            throw new InvalidOperationException("Cannot change the settlement contract of a running battle.");

        var changed = !_initialized || _worldId != options.WorldId || _battleId != options.BattleId ||
            _runtimeMode != options.RuntimeMode || _syncTemplateId != options.SyncTemplateId ||
            _initialCheckpoint != checkpoint;
        if (changed)
        {
            ClearSettlement();
            if (_settledHistory != null || options.UseSettledFrames)
            {
                _settledObservers.Clear();
                _observers.Clear();
                _settledHistory = null;
            }
            if (_initialCheckpoint != null || checkpoint != null)
            {
                _inputsByFrame.Clear();
                _inputHistory.Clear();
                _fullRecording.Clear();
                _totalInputCount = 0;
                _frame = 0;
            }
        }

        _initialCheckpoint = checkpoint;
        _roomId = options.RoomId != 0 ? options.RoomId : _roomId;
        _worldId = options.WorldId;
        _battleId = options.BattleId;
        _syncTemplateId = options.SyncTemplateId;
        _runtimeMode = options.RuntimeMode;
        _enableRecording = options.EnableRecording;
        _minTickRate = options.MinTickRate > 0 ? options.MinTickRate : 10;
        _maxTickRate = options.MaxTickRate >= _minTickRate ? options.MaxTickRate : 60;

        if (changed && checkpoint != null)
        {
            if (options.UseSettledFrames)
                _settledHistory = new SettledFrameHistory(options, MaxHistoryFrames, MaxRecordingFrames);
            else
                StoreInputHistory(0, new List<FrameInputItem>(0));
            _frame = 1;
            _settlementGeneration = checkpoint.RuntimeGeneration;
            _lastSettledFrame = 0;
        }

        var tickRate = options.TickRate > 0 ? options.TickRate : DefaultTickRate;
        _tickInterval = TimeSpan.FromSeconds(1.0 / tickRate);
        _nextTickDueUtc = DateTime.UtcNow + _tickInterval;
        _initialized = true;

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
        if (_settledHistory != null)
            throw new InvalidOperationException("This battle only publishes settled frames.");
        _observers.Add(observer);
        return Task.CompletedTask;
    }

    public Task UnsubscribeAsync(IFrameSyncObserver observer)
    {
        if (observer == null) return Task.CompletedTask;
        _observers.Remove(observer);
        return Task.CompletedTask;
    }

    public Task<bool> SubscribeSettledAsync(Guid runtimeGeneration, ISettledFrameSyncObserver observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        if (_settledHistory is not { FaultFrame: null } history || history.Generation != runtimeGeneration)
            return Task.FromResult(false);
        _settledObservers.Add(observer);
        return Task.FromResult(true);
    }

    public Task<FrameSyncSettledBinding?> GetSettledBindingAsync() =>
        Task.FromResult(_settledHistory is { } history
            ? new FrameSyncSettledBinding(_roomId, _worldId, _battleId!, history.Generation,
                history.SimulationTickRate > 0 ? history.SimulationTickRate : DefaultTickRate,
                history.LastSettledFrame, history.FaultFrame.HasValue)
            : null);

    public Task UnsubscribeSettledAsync(ISettledFrameSyncObserver observer)
    {
        if (observer != null) _settledObservers.Remove(observer);
        return Task.CompletedTask;
    }

    public async Task SubmitInputAsync(ulong worldId, int frame, FrameInputItem input)
    {
        await SubmitInputWithResultAsync(worldId, frame, input);
    }

    public Task<FrameInputSubmitResult> SubmitInputWithResultAsync(ulong worldId, int frame, FrameInputItem input) =>
        _settledHistory != null
            ? Task.FromResult(new FrameInputSubmitResult(false, _frame, FrameInputSubmitReason.SettlementModeMismatch))
            : SubmitInputCore(worldId, frame, input);

    public Task<FrameInputSubmitResult> SubmitSettledInputAsync(ulong worldId, Guid runtimeGeneration, int frame, FrameInputItem input)
    {
        if (_settledHistory is not { } history || history.Generation != runtimeGeneration)
            return Task.FromResult(new FrameInputSubmitResult(false, _frame, FrameInputSubmitReason.SettlementModeMismatch));
        if (history.FaultFrame.HasValue)
            return Task.FromResult(new FrameInputSubmitResult(false, _frame, FrameInputSubmitReason.SettlementFaulted));
        return SubmitInputCore(worldId, frame, input);
    }

    private Task<FrameInputSubmitResult> SubmitInputCore(ulong worldId, int frame, FrameInputItem input)
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
        _totalInputCount++;
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

    public Task<FrameSyncCatchUpPayload?> RequestCatchUpAsync(FrameSyncCatchUpRequest request)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        if (_settledHistory != null) return Task.FromResult<FrameSyncCatchUpPayload?>(null);

        var history = request.FromFrameExclusive == -1 && _enableRecording
            ? _fullRecording
            : null;
        /*
         * FromFrameExclusive=-1 专用于新进程锁步恢复。不得回退到短环形缓冲；完整记录缺失或截断时必须失败。
         */
        var maxAvailableFrames = request.FromFrameExclusive == -1
            ? history?.Count ?? 0
            : MaxHistoryFrames;
        if (!TryResolveCatchUpRange(
                _roomId,
                _worldId,
                _frame,
                request,
                maxAvailableFrames,
                out var from,
                out var to))
        {
            _logger.LogWarning(
                "[BattleFrameSyncGrain] Catch-up range rejected. AuthoritativeRoomId={AuthoritativeRoomId} AuthoritativeWorldId={AuthoritativeWorldId} NextFrame={NextFrame} RequestedRoomId={RequestedRoomId} RequestedWorldId={RequestedWorldId} FromFrameExclusive={FromFrameExclusive} ToFrameInclusive={ToFrameInclusive} MaxAvailableFrames={MaxAvailableFrames}",
                _roomId,
                _worldId,
                _frame,
                request.RoomId,
                request.WorldId,
                request.FromFrameExclusive,
                request.ToFrameInclusive,
                maxAvailableFrames);
            return Task.FromResult<FrameSyncCatchUpPayload?>(null);
        }

        var frameCount = to - from;
        var frameInputs = new List<List<FrameInputItem>>(frameCount);
        for (var f = from + 1; f <= to; f++)
        {
            List<FrameInputItem>? inputs;
            if (history != null && f >= 0 && f < history.Count)
            {
                inputs = history[f];
            }
            else if (!_inputHistory.TryGetValue(f, out inputs))
            {
                // MOBA 锁步恢复要求连续的权威输入历史。缺帧时拒绝恢复；不能回退到
                // shooter 状态同步的全量状态快照，否则会破坏确定性追帧语义。
                _logger.LogWarning(
                    "[BattleFrameSyncGrain] Catch-up history frame missing. RoomId={RoomId} WorldId={WorldId} MissingFrame={MissingFrame} FromFrameExclusive={FromFrameExclusive} ToFrameInclusive={ToFrameInclusive} NextFrame={NextFrame} HistoryCount={HistoryCount} RecordingCount={RecordingCount}",
                    _roomId,
                    _worldId,
                    f,
                    from,
                    to,
                    _frame,
                    _inputHistory.Count,
                    _fullRecording.Count);
                return Task.FromResult<FrameSyncCatchUpPayload?>(null);
            }

            frameInputs.Add(new List<FrameInputItem>(inputs));
        }

        var payload = new FrameSyncCatchUpPayload(
            _roomId,
            _worldId,
            from + 1,
            frameInputs);
        return Task.FromResult<FrameSyncCatchUpPayload?>(payload);
    }

    internal static bool TryResolveCatchUpRange(
        ulong authoritativeRoomId,
        ulong authoritativeWorldId,
        int currentFrame,
        FrameSyncCatchUpRequest request,
        out int fromFrameExclusive,
        out int toFrameInclusive)
    {
        return TryResolveCatchUpRange(
            authoritativeRoomId,
            authoritativeWorldId,
            currentFrame,
            request,
            MaxHistoryFrames,
            out fromFrameExclusive,
            out toFrameInclusive);
    }

    internal static bool TryResolveCatchUpRange(
        ulong authoritativeRoomId,
        ulong authoritativeWorldId,
        int currentFrame,
        FrameSyncCatchUpRequest request,
        int maxAvailableFrames,
        out int fromFrameExclusive,
        out int toFrameInclusive)
    {
        fromFrameExclusive = 0;
        toFrameInclusive = 0;

        if (request.RoomId == 0
            || request.RoomId != authoritativeRoomId
            || request.WorldId == 0
            || request.WorldId != authoritativeWorldId
            || request.FromFrameExclusive < -1
            || request.ToFrameInclusive <= request.FromFrameExclusive
            || maxAvailableFrames <= 0)
        {
            return false;
        }

        // _frame/currentFrame 表示下一个待处理帧；历史只写入到 currentFrame - 1。
        // CatchUp 尾帧必须 clamp 到最后一个已处理权威帧，否则会请求尚不存在的历史项。
        var latestProcessedFrame = currentFrame - 1;
        var clampedTo = Math.Min(request.ToFrameInclusive, latestProcessedFrame);
        var frameCount = (long)clampedTo - request.FromFrameExclusive;
        if (frameCount <= 0 || frameCount > maxAvailableFrames)
        {
            return false;
        }

        fromFrameExclusive = request.FromFrameExclusive;
        toFrameInclusive = clampedTo;
        return true;
    }

    public Task<FrameSyncSettledCatchUpPayload?> RequestSettledCatchUpAsync(FrameSyncSettledCatchUpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Task.FromResult(_settledHistory?.CatchUp(request));
    }

    public Task<FrameSyncSettledRecording?> DumpSettledRecordingAsync() =>
        Task.FromResult(_settledHistory?.DumpRecording());

    public Task<FrameSyncRecording?> DumpRecordingAsync()
    {
        if (!_enableRecording || _fullRecording.Count == 0 || _settledHistory != null)
        {
            return Task.FromResult<FrameSyncRecording?>(null);
        }

        var startOptions = new FrameSyncStartOptions(
            _roomId,
            _worldId,
            (int)(1.0 / _tickInterval.TotalSeconds),
            _battleId,
            _syncTemplateId,
            _runtimeMode,
            EnableRecording: true);

        var recording = new FrameSyncRecording(
            startOptions,
            _fullRecording,
            DateTime.UtcNow.Ticks,
            _fullRecording.Count);

        return Task.FromResult<FrameSyncRecording?>(recording);
    }

    /// <summary>
    /// 运行时调整 Tick 频率。调用方（如运维管理端或自动化测试）必须在调用后通过
    /// FramePushed 或其他带外机制通知所有客户端同步更新 tick rate，否则 lockstep
    /// 确定性会被破坏。
    /// </summary>
    public Task<int> AdjustTickRateAsync(int targetTickRate)
    {
        var clamped = Math.Max(_minTickRate, Math.Min(_maxTickRate, targetTickRate));
        var currentTickRate = (int)(1.0 / _tickInterval.TotalSeconds);

        if (clamped == currentTickRate)
        {
            return Task.FromResult(clamped);
        }

        _tickInterval = TimeSpan.FromSeconds(1.0 / clamped);
        _nextTickDueUtc = DateTime.UtcNow + _tickInterval;

        // 重置时间窗口统计，避免突跳影响 Hz 计算
        _tickWindowStartUtc = DateTime.UtcNow;
        _tickCountInWindow = 0;
        _tickDeltaSumMs = 0;

        _logger.LogInformation(
            "[BattleFrameSyncGrain] Tick rate adjusted. RoomId={RoomId} {OldHz}Hz -> {NewHz}Hz. Caller must broadcast new rate to all clients.",
            _roomId, currentTickRate, clamped);

        return Task.FromResult(clamped);
    }

    public Task<FrameSyncMetrics> GetMetricsAsync()
    {
        var now = DateTime.UtcNow;
        var uptimeSeconds = (long)(now - _startedAtUtc).TotalSeconds;

        var windowSeconds = (now - _tickWindowStartUtc).TotalSeconds;
        var hz = windowSeconds > 0 ? _tickCountInWindow / windowSeconds : 0;

        var avgTickDelta = _tickCountInWindow > 0
            ? _tickDeltaSumMs / _tickCountInWindow
            : 0;

        var metrics = new FrameSyncMetrics(
            _roomId,
            _worldId,
            _battleId,
            _frame,
            (int)(1.0 / _tickInterval.TotalSeconds),
            _settledHistory != null ? _settledObservers.Count : _observers.Count,
            avgTickDelta,
            _tickDeltaLastMs,
            hz,
            TotalInputsReceived: _totalInputCount,
            CatchUpHistoryFrames: _settledHistory?.RecentCount ?? _inputHistory.Count,
            RecordingFrameCount: _settledHistory?.RecordingCount ?? (_enableRecording ? _fullRecording.Count : 0),
            uptimeSeconds)
        {
            SettlementGeneration = _settlementGeneration,
            LastSettledFrame = _settledHistory?.LastSettledFrame ?? _lastSettledFrame,
            SettlementFaultFrame = _settledHistory?.FaultFrame
        };

        return Task.FromResult(metrics);
    }

    public Task DestroyAsync()
    {
        _initialized = false;
        _timer?.Dispose();
        _timer = null;
        _observers.Clear();
        _settledObservers.Clear();
        _settledHistory = null;
        _inputsByFrame.Clear();
        _inputHistory.Clear();
        _fullRecording.Clear();
        _worldId = 0UL;
        _battleId = null;
        _syncTemplateId = null;
        _frame = 0;
        _totalInputCount = 0;
        _initialCheckpoint = null;
        ClearSettlement();
        DeactivateOnIdle();
        return Task.CompletedTask;
    }

    private void ObserveSettlement(int frame, BattleTickFrameResult tick)
    {
        if (tick.Frame != frame || tick.RuntimeGeneration == Guid.Empty) return;

        if (_settlementGeneration != tick.RuntimeGeneration)
        {
            _settlementGeneration = tick.RuntimeGeneration;
            _lastSettledFrame = null;
        }

        if (tick.WorldTicked && tick.SettlementEligible && frame == (_lastSettledFrame ?? -1) + 1)
        {
            _lastSettledFrame = frame;
        }
    }

    private void ClearSettlement()
    {
        _settlementGeneration = Guid.Empty;
        _lastSettledFrame = null;
    }

    private void StoreInputHistory(int frame, List<FrameInputItem> inputs)
    {
        _inputHistory[frame] = new List<FrameInputItem>(inputs);

        /*
         * 裁剪旧条目，维护环形缓冲。
         */
        var threshold = frame - MaxHistoryFrames;
        var keysToRemove = new List<int>();
        foreach (var kv in _inputHistory)
        {
            if (kv.Key < threshold) keysToRemove.Add(kv.Key);
            else break;
        }

        foreach (var k in keysToRemove)
        {
            _inputHistory.Remove(k);
        }

        // 录制模式：追加到完整历史（不修剪）。达到上限后 Count 将小于权威帧数，
        // 冷启动 CatchUp 的连续范围校验会明确拒绝恢复，不会错误退回短历史或状态快照。
        if (_enableRecording && _fullRecording.Count < MaxRecordingFrames)
        {
            _fullRecording.Add(new List<FrameInputItem>(inputs));
        }
    }

    private async Task<bool> CommitSettledTickAsync(SettledFrameHistory history, int frame, List<FrameInputItem> inputs)
    {
        BattleFrameSettlement? settlement = null;
        try
        {
            var host = GrainFactory.GetGrain<IBattleLogicHostGrain>(_battleId!);
            settlement = await host.TickSettledFrameAsync(_worldId, history.Generation, frame, inputs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BattleFrameSyncGrain] Settled tick failed. BattleId={BattleId} Frame={Frame}", _battleId, frame);
        }

        var committed = history.Commit(frame, settlement, inputs.Count);
        if (committed == null)
        {
            _inputsByFrame.Clear();
            _logger.LogError("[BattleFrameSyncGrain] Settlement halted. BattleId={BattleId} FaultFrame={FaultFrame} LastSettledFrame={LastSettledFrame}",
                _battleId, history.FaultFrame, history.LastSettledFrame);
            return false;
        }

        _frame = frame + 1;
        _tickCountInWindow++;
        List<ISettledFrameSyncObserver>? failed = null;
        foreach (var observer in _settledObservers)
        {
            try { observer.OnSettledFramePushed(committed); }
            catch (Exception ex)
            {
                (failed ??= new List<ISettledFrameSyncObserver>()).Add(observer);
                _logger.LogWarning(ex, "[BattleFrameSyncGrain] Settled observer push failed. BattleId={BattleId} Frame={Frame}", _battleId, frame);
            }
        }
        if (failed != null) foreach (var observer in failed) _settledObservers.Remove(observer);
        return true;
    }

    private async Task OnTickAsync()
    {
        if (!_initialized || _settledHistory?.FaultFrame != null) return;

        var now = DateTime.UtcNow;
        var deltaMs = (now - _lastTickUtc).TotalMilliseconds;
        _lastTickUtc = now;
        _tickDeltaLastMs = deltaMs;
        _tickDeltaSumMs += deltaMs;

        if (now < _nextTickDueUtc)
        {
            return;
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

            if (_settledHistory is { } settledHistory)
            {
                _inputsByFrame.Remove(cur);
                if (!await CommitSettledTickAsync(settledHistory, cur, inputs)) return;
                continue;
            }

            StoreInputHistory(cur, inputs);
            _inputsByFrame.Remove(cur);
            uint stateHash = 0;

            // 混合模式：由 BattleFrameSyncGrain 外部驱动 BattleLogicHostGrain 的世界推进
            if (_runtimeMode == (int)ServerBattleRuntimeMode.BattleWorldWithFrameSync
                && !string.IsNullOrEmpty(_battleId))
            {
                var delta = (float)_tickInterval.TotalSeconds;
                try
                {
                    var battleHost = GrainFactory.GetGrain<IBattleLogicHostGrain>(_battleId);
                    var tick = await battleHost.TickFrameAsync(_worldId, cur, delta, inputs);
                    ObserveSettlement(cur, tick);
                    if (tick.WorldTicked) stateHash = checked((uint)tick.StateHash);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "[BattleFrameSyncGrain] Failed to drive BattleLogicHostGrain. RoomId={RoomId} BattleId={BattleId} Frame={Frame}",
                        _roomId, _battleId, cur);
                }
            }

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
                Inputs: inputs,
                StateHash: stateHash);

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
    }
}
