using System;
using System.Collections.Generic;
using System.Threading;
using AbilityKit.Ability.Host.Extensions.Server.BattleHost;
using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Contracts.FrameSync;
using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Orleans.Grains.Battle.Gameplay;
using AbilityKit.Orleans.Grains.Gameplay;
using AbilityKit.Protocol.Shooter;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AbilityKit.Orleans.Grains.Battle;

/// <summary>
/// Battle Logic Host Grain 实现。
/// 负责通用战斗生命周期、输入缓冲、Tick 与状态同步，具体玩法运行时由 BattleRuntimeAdapter 提供。
/// </summary>
public sealed class BattleLogicHostGrain : Grain, IBattleLogicHostGrain
{
    private readonly ILogger<BattleLogicHostGrain> _logger;
    private readonly ServerGameplayModuleCatalog _gameplayModules;
    private readonly BattleRuntimeRegistry _runtimeRegistry;
    private readonly BattleHostState _battleHostState = new();
    private readonly IBattleInputBuffer<BattleInputItem> _inputBuffer = new BattleInputBuffer<BattleInputItem>();
    private readonly BattleInputSecurityOptions _inputSecurityOptions;
    private readonly BattleInputAdmissionGuard _inputAdmissionGuard;
    private readonly IBattleTickDriver<BattleInputItem> _tickDriver;
    private readonly BattleObserverRegistry<IStateSyncObserverGrain> _observerRegistry = new();
    private readonly Dictionary<IStateSyncObserverGrain, BattleStateSyncObserverContext> _observerContexts = new();
    private BattleSnapshotSyncPolicy _snapshotSyncPolicy = new();
    private readonly BattleSnapshotPublisher<IStateSyncObserverGrain, StateSyncPush> _snapshotPublisher;
    private readonly Dictionary<IStateSyncObserverGrain, SnapshotDeliveryInFlight> _snapshotDeliveries = new();
    private readonly Dictionary<uint, ulong> _consumedCommandSequences = new();
    private ShooterCommandAcknowledgement[] _cachedCommandAcknowledgements = Array.Empty<ShooterCommandAcknowledgement>();
    private long _consumedCommandSequenceVersion;
    private long _cachedCommandAcknowledgementVersion = -1;

    private IDisposable? _timer;
    private ReliableBattleEventRetention? _reliableEvents;
    private IBattleRuntimeSession? _runtimeSession;
    private int _tickRate = 30;
    private ulong _worldId;
    private string _battleId = string.Empty;
    private bool _initialized;
    private TimeSpan _tickInterval;
    private WorldStartAnchor? _worldStartAnchor;
    private int _inputDelayFrames;
    private ServerBattleSyncProfile? _syncProfile;
    private string _syncTemplateId = string.Empty;
    private string? _initSpecHash;
    private bool _externalTickMode;

    public BattleLogicHostGrain(
        ILogger<BattleLogicHostGrain> logger,
        ServerBattleWorldManager worldManager,
        IOptions<BattleInputSecurityOptions> inputSecurityOptions)
    {
        _logger = logger;
        _inputSecurityOptions = inputSecurityOptions.Value.Snapshot();
        _inputAdmissionGuard = new BattleInputAdmissionGuard(_inputSecurityOptions);
        _gameplayModules = ServerGameplayModuleCatalog.Default;
        _runtimeRegistry = new BattleRuntimeRegistry(
            _gameplayModules.CreateBattleRuntimeAdapters(worldManager),
            _gameplayModules.GameplayCatalog);
        _tickDriver = new BattleTickDriver<BattleInputItem>(SubmitRuntimeInputs, TickBattleWorld);
        _snapshotPublisher = new BattleSnapshotPublisher<IStateSyncObserverGrain, StateSyncPush>(
            BuildStateSyncPush,
            SendStateSyncPush,
            HandleSnapshotPublishError);
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        var key = this.GetPrimaryKeyString();
        _logger.LogInformation("[BattleLogicHost] Activated with key: {Key}", key);
        return Task.CompletedTask;
    }

    public override Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[BattleLogicHost] Deactivated: {Reason}", reason);
        StopBattleRuntime();
        return Task.CompletedTask;
    }

    public Task InitializeBattleAsync(BattleInitParams initParams)
    {
        if (initParams is null)
        {
            throw new ArgumentNullException(nameof(initParams));
        }

        // 旧 void 入口委托新结构化方法，忽略结果以保持兼容。
        _ = InitializeBattleWithResultAsync(initParams, initSpecHash: null);
        return Task.CompletedTask;
    }

    public Task<BattleInitResult> InitializeBattleWithResultAsync(BattleInitParams initParams, string? initSpecHash)
    {
        if (initParams is null)
        {
            throw new ArgumentNullException(nameof(initParams));
        }

        // 幂等 create-or-get：已初始化时返回当前状态，并做 hash 冲突检测。
        if (_initialized)
        {
            if (initSpecHash is not null
                && _initSpecHash is not null
                && !string.Equals(initSpecHash, _initSpecHash, StringComparison.Ordinal))
            {
                _logger.LogWarning(
                    "[BattleLogicHost] Init spec hash mismatch on already-initialized battle. BattleId: {BattleId}, Stored: {Stored}, Requested: {Requested}",
                    _battleId,
                    _initSpecHash,
                    initSpecHash);
                return Task.FromResult(BattleInitResult.FromHashMismatch(_initSpecHash));
            }

            _logger.LogDebug("[BattleLogicHost] Already initialized, returning idempotent result. BattleId: {BattleId}", _battleId);
            return Task.FromResult(BattleInitResult.FromAlreadyInitialized(_initSpecHash, _worldStartAnchor));
        }

        var result = InitializeBattleCore(initParams, initSpecHash);
        return Task.FromResult(result);
    }

    private BattleInitResult InitializeBattleCore(BattleInitParams initParams, string? initSpecHash)
    {
        _battleId = this.GetPrimaryKeyString();
        var module = _gameplayModules.ResolveModule(initParams.RoomType);
        _syncProfile = module.SyncProfile;
        var requestedSyncTemplateId = initParams.SyncOptions?.SyncTemplateId;
        if (!_syncProfile.TryResolveTemplate(requestedSyncTemplateId, out var syncTemplate))
        {
            _logger.LogError(
                "[BattleLogicHost] Unsupported sync template. BattleId: {BattleId}, RoomType: {RoomType}, RequestedTemplate: {RequestedTemplate}, DefaultTemplate: {DefaultTemplate}",
                _battleId,
                module.RoomType,
                requestedSyncTemplateId,
                _syncProfile.DefaultTemplateId);
            StopBattleRuntime();
            return BattleInitResult.FromError("UnsupportedSyncTemplate");
        }

        var syncOptions = initParams.SyncOptions;
        _syncTemplateId = syncTemplate.TemplateId;
        _snapshotSyncPolicy = new BattleSnapshotSyncPolicy(
            syncTemplate.SnapshotIntervalFrames,
            syncTemplate.FullSnapshotIntervalFrames);
        initParams.SyncOptions = new BattleSyncStartOptions(
            _syncTemplateId,
            syncOptions?.SyncModel ?? 0,
            syncOptions?.NetworkEnvironmentId,
            syncOptions?.CarrierName,
            syncOptions?.EnableAuthoritativeWorld ?? false,
            syncOptions?.InterpolationEnabled ?? false,
            syncOptions?.InputDelayFrames ?? 0);
        _worldId = initParams.WorldId;
        _tickRate = initParams.TickRate > 0 ? initParams.TickRate : 30;
        _tickInterval = TimeSpan.FromSeconds(1.0 / _tickRate);
        _inputDelayFrames = initParams.InputDelayFrames > 0 ? initParams.InputDelayFrames : 0;
        _worldStartAnchor = new WorldStartAnchor(
            DateTime.UtcNow.Ticks,
            TimeSpan.TicksPerSecond,
            0,
            1.0 / _tickRate);
        initParams.WorldStartAnchor = _worldStartAnchor;
        _battleHostState.Initialize(_worldId, _battleId, _tickRate);
        _reliableEvents = new ReliableBattleEventRetention(_battleId, Guid.NewGuid().ToString("N"));

        _logger.LogInformation(
            "[BattleLogicHost] Initializing battle - BattleId: {BattleId}, RoomType: {RoomType}, WorldId: {WorldId}, TickRate: {TickRate}, Players: {PlayerCount}, SyncMode: {SyncMode}, SyncTemplate: {SyncTemplate}, DurationFrames: {DurationFrames}, VictoryTargetDefeats: {VictoryTargetDefeats}",
            _battleId,
            module.RoomType,
            _worldId,
            _tickRate,
            initParams.Players?.Count ?? 0,
            syncTemplate.Mode,
            _syncTemplateId,
            initParams.DurationFrames,
            initParams.VictoryTargetDefeats);

        var adapter = _runtimeRegistry.Resolve(module.RoomType);
        _runtimeSession = adapter.CreateSession(_battleId);
        var startResult = _runtimeSession.Start(initParams);
        if (!startResult.Succeeded)
        {
            _logger.LogError("[BattleLogicHost] Battle initialization failed. Error: {Error}", startResult.Error);
            StopBattleRuntime();
            return BattleInitResult.FromError(startResult.Error ?? "BattleInitializationFailed");
        }

        _initSpecHash = initSpecHash;
        _initialized = true;
        PublishInitialSnapshot();
        if (syncTemplate.RuntimeMode == ServerBattleRuntimeMode.BattleWorldWithFrameSync)
        {
            _externalTickMode = true;
            _logger.LogInformation("[BattleLogicHost] External tick mode — BattleFrameSyncGrain drives clock. BattleId: {BattleId}", _battleId);
        }
        else
        {
            StartBattleTimer();
        }

        _logger.LogInformation("[BattleLogicHost] Battle initialized successfully");
        return BattleInitResult.FromInitialized(_initSpecHash, _worldStartAnchor);
    }

    public Task<BattleInputSubmitResult> SubmitInputAsync(ulong worldId, int frame, BattleInputItem input)
    {
        if (!_initialized)
        {
            _logger.LogWarning("[BattleLogicHost] SubmitInput called but not initialized");
            return Task.FromResult(CreateInputSubmitResult(false, frame, frame, BattleResultStatusCodes.RejectedNotInitialized, "Battle is not initialized."));
        }

        var currentFrame = _battleHostState.Frame;
        if (worldId != 0 && worldId != _worldId)
        {
            _logger.LogWarning("[BattleLogicHost] Input world mismatch. Expected: {ExpectedWorldId}, Actual: {ActualWorldId}", _worldId, worldId);
            return Task.FromResult(CreateInputSubmitResult(false, frame, frame, BattleResultStatusCodes.RejectedWorldMismatch, "Input world does not match battle world."));
        }

        if (input == null)
        {
            return Task.FromResult(CreateInputSubmitResult(false, frame, frame, BattleResultStatusCodes.RejectedNullInput, "Input is required."));
        }

        if (input.PlayerId == 0)
        {
            return Task.FromResult(CreateInputSubmitResult(false, frame, frame, BattleResultStatusCodes.RejectedInvalidPlayer, "Input player id must be positive."));
        }

        if (input.OpCode <= 0 || input.OpCode > _inputSecurityOptions.MaxOpCode)
        {
            return Task.FromResult(CreateInputSubmitResult(false, frame, frame, BattleResultStatusCodes.RejectedInvalidOpCode, "Input opcode is outside the supported transport range."));
        }

        if ((input.Payload?.Length ?? 0) > _inputSecurityOptions.MaxPayloadBytes)
        {
            return Task.FromResult(CreateInputSubmitResult(false, frame, frame, BattleResultStatusCodes.RejectedInvalidPayload, "Input payload exceeds the maximum size."));
        }

        if (_runtimeSession == null)
        {
            return Task.FromResult(CreateInputSubmitResult(false, frame, frame, BattleResultStatusCodes.RejectedNotInitialized, "Battle runtime is not initialized."));
        }

        var validation = _runtimeSession.ValidateInput(input);
        if (!validation.Accepted)
        {
            return Task.FromResult(CreateInputSubmitResult(false, frame, frame, validation.Status, validation.Message));
        }

        var schedule = BattleInputFrameScheduler.Schedule(
            frame,
            currentFrame,
            _inputDelayFrames,
            BattleInputFrameSchedulerOptions.Default);
        if (!schedule.Accepted)
        {
            _logger.LogWarning(
                "[BattleLogicHost] Input rejected by frame scheduler. Frame: {Frame}, CurrentFrame: {CurrentFrame}, Status: {Status}, PlayerId: {PlayerId}",
                frame,
                currentFrame,
                schedule.Status,
                input.PlayerId);
            return Task.FromResult(CreateInputSubmitResult(false, frame, schedule.AcceptedFrame, schedule.Status.ToString(), BuildInputSubmitMessage(schedule)));
        }

        var admission = _inputAdmissionGuard.Check(input.PlayerId, input.CommandSequence, DateTime.UtcNow.Ticks);
        if (admission.Status == BattleInputGuardStatus.RejectedDuplicate)
        {
            return Task.FromResult(CreateInputSubmitResult(true, frame, schedule.AcceptedFrame, "Deduplicated", "Command sequence was already accepted."));
        }

        if (!admission.Accepted)
        {
            return Task.FromResult(CreateInputSubmitResult(false, frame, schedule.AcceptedFrame, admission.StatusCode, "Input was rejected by the battle input security policy."));
        }

        if (!_inputBuffer.Enqueue(schedule.AcceptedFrame, input))
        {
            _logger.LogWarning("[BattleLogicHost] Input rejected by host input buffer. Frame: {Frame}, PlayerId: {PlayerId}", schedule.AcceptedFrame, input.PlayerId);
            return Task.FromResult(CreateInputSubmitResult(false, frame, schedule.AcceptedFrame, BattleResultStatusCodes.RejectedByInputBuffer, "Input buffer rejected the scheduled frame."));
        }

        _inputAdmissionGuard.RecordAccepted(input.PlayerId, input.CommandSequence);
        _logger.LogDebug(
            "[BattleLogicHost] Input received - RequestedFrame: {RequestedFrame}, AcceptedFrame: {AcceptedFrame}, CurrentFrame: {CurrentFrame}, PlayerId: {PlayerId}, OpCode: {OpCode}, Status: {Status}",
            frame,
            schedule.AcceptedFrame,
            currentFrame,
            input.PlayerId,
            input.OpCode,
            schedule.Status);

        return Task.FromResult(CreateInputSubmitResult(true, frame, schedule.AcceptedFrame, schedule.Status.ToString(), BuildInputSubmitMessage(schedule)));
    }

    private BattleInputSubmitResult CreateInputSubmitResult(bool accepted, int requestedFrame, int acceptedFrame, string status, string message)
    {
        return new BattleInputSubmitResult(
            accepted,
            requestedFrame,
            acceptedFrame,
            _battleHostState.Frame,
            status,
            message);
    }

    private static string BuildInputSubmitMessage(BattleInputFrameScheduleResult schedule)
    {
        return schedule.Status switch
        {
            BattleInputAcceptStatus.Accepted => string.Empty,
            BattleInputAcceptStatus.RemappedLate => $"Input frame {schedule.RequestedFrame} is late and was remapped to frame {schedule.AcceptedFrame}.",
            BattleInputAcceptStatus.RemappedTooEarly => $"Input frame {schedule.RequestedFrame} is earlier than the configured input delay and was remapped to frame {schedule.AcceptedFrame}.",
            BattleInputAcceptStatus.RejectedInvalidFrame => "Input frame is invalid.",
            BattleInputAcceptStatus.RejectedTooFarFuture => $"Input frame {schedule.RequestedFrame} is too far ahead of current frame {schedule.CurrentFrame}.",
            _ => schedule.Status.ToString()
        };
    }

    public Task<BattlePlayerJoinResult> JoinPlayerAsync(BattlePlayerJoinRequest request)
    {
        if (!_initialized || _runtimeSession == null)
        {
            return Task.FromResult(new BattlePlayerJoinResult(
                false,
                request?.Player?.PlayerId ?? 0u,
                _battleHostState.Frame,
                BattleResultStatusCodes.RejectedNotInitialized,
                "Battle is not initialized."));
        }

        if (request is null)
        {
            return Task.FromResult(new BattlePlayerJoinResult(false, 0u, _battleHostState.Frame, BattleResultStatusCodes.RejectedNullRequest, "Join request is required."));
        }

        if (request.Player is null)
        {
            return Task.FromResult(new BattlePlayerJoinResult(false, 0u, _battleHostState.Frame, BattleResultStatusCodes.RejectedNullPlayer, "Player init info is required."));
        }

        if (request.WorldId != 0 && request.WorldId != _worldId)
        {
            _logger.LogWarning("[BattleLogicHost] JoinPlayer world mismatch. Expected: {ExpectedWorldId}, Actual: {ActualWorldId}", _worldId, request.WorldId);
            return Task.FromResult(new BattlePlayerJoinResult(
                false,
                request.Player.PlayerId,
                _battleHostState.Frame,
                BattleResultStatusCodes.RejectedWorldMismatch,
                "Join world does not match battle world."));
        }

        var result = _runtimeSession.JoinPlayer(request, _battleHostState.Frame);
        if (ShouldBroadcastFullSnapshotAfterPlayerJoin(result))
        {
            PushSnapshot(isFullSnapshot: true);
        }

        if (result.Accepted)
        {
            _logger.LogInformation(
                "[BattleLogicHost] Player joined running battle. BattleId: {BattleId}, WorldId: {WorldId}, PlayerId: {PlayerId}, IsBot: {IsBot}",
                _battleId,
                _worldId,
                result.PlayerId,
                request.IsBot);
        }
        else
        {
            _logger.LogWarning(
                "[BattleLogicHost] Player join rejected. BattleId: {BattleId}, PlayerId: {PlayerId}, Status: {Status}, Message: {Message}",
                _battleId,
                request.Player.PlayerId,
                result.Status,
                result.Message);
        }

        return Task.FromResult(result);
    }

    public Task<BattleBotAiMountResult> MountBotAiAsync(BattleBotAiMountRequest request)
    {
        if (!_initialized || _runtimeSession == null)
        {
            return Task.FromResult(new BattleBotAiMountResult(
                false,
                request?.PlayerId ?? 0u,
                _battleHostState.Frame,
                BattleResultStatusCodes.RejectedNotInitialized,
                "Battle is not initialized."));
        }

        if (request is null)
        {
            return Task.FromResult(new BattleBotAiMountResult(false, 0u, _battleHostState.Frame, BattleResultStatusCodes.RejectedNullRequest, "Bot AI mount request is required."));
        }

        if (request.WorldId != 0 && request.WorldId != _worldId)
        {
            _logger.LogWarning("[BattleLogicHost] MountBotAi world mismatch. Expected: {ExpectedWorldId}, Actual: {ActualWorldId}", _worldId, request.WorldId);
            return Task.FromResult(new BattleBotAiMountResult(
                false,
                request.PlayerId,
                _battleHostState.Frame,
                BattleResultStatusCodes.RejectedWorldMismatch,
                "Bot AI mount world does not match battle world."));
        }

        var result = _runtimeSession.MountBotAi(request, _battleHostState.Frame);
        if (result.Accepted)
        {
            _logger.LogInformation(
                "[BattleLogicHost] Bot AI mounted. BattleId: {BattleId}, WorldId: {WorldId}, PlayerId: {PlayerId}, ProfileId: {ProfileId}",
                _battleId,
                _worldId,
                result.PlayerId,
                request.ProfileId ?? string.Empty);
        }
        else
        {
            _logger.LogWarning(
                "[BattleLogicHost] Bot AI mount rejected. BattleId: {BattleId}, PlayerId: {PlayerId}, Status: {Status}, Message: {Message}",
                _battleId,
                request.PlayerId,
                result.Status,
                result.Message);
        }

        return Task.FromResult(result);
    }

    public Task<int> GetCurrentFrameAsync()
    {
        return Task.FromResult(_battleHostState.Frame);
    }

    public Task<BattleSnapshot?> GetSnapshotAsync()
    {
        return Task.FromResult(_runtimeSession?.GetSnapshot(_battleHostState.Frame));
    }

    public Task<BattleWorldDiagnostics?> GetWorldDiagnosticsAsync()
    {
        return Task.FromResult(_runtimeSession?.GetWorldDiagnostics(_worldId, _battleHostState.Frame));
    }

    public Task<WorldStartAnchor?> GetWorldStartAnchorAsync()
    {
        return Task.FromResult(_worldStartAnchor);
    }

    public Task SubscribeAsync(
        IStateSyncObserverGrain observer,
        StateSyncObserverInfo observerInfo,
        ReliableBattleEventSubscribeCursor eventCursor)
    {
        if (observer == null)
        {
            throw new ArgumentNullException(nameof(observer));
        }

        eventCursor ??= new ReliableBattleEventSubscribeCursor();
        var observerContext = CreateObserverContext(observerInfo);
        var added = _observerRegistry.Subscribe(observer);
        _observerContexts[observer] = observerContext;
        if (added)
        {
            _logger.LogInformation(
                "[BattleLogicHost] Observer subscribed. Total observers: {Count}, ObserverKey: {ObserverKey}, AccountId: {AccountId}",
                _observerRegistry.Count,
                observerContext.ObserverKey,
                observerContext.AccountId);
        }

        if (!_initialized)
        {
            return Task.CompletedTask;
        }

        if (_reliableEvents != null)
        {
            _reliableEvents.RegisterObserver(
                observerContext.ObserverKey,
                eventCursor.Epoch,
                eventCursor.LastAcknowledgedSequence);
            var replay = _reliableEvents.CreateReplay(eventCursor.Epoch, eventCursor.LastAcknowledgedSequence);
            _ = ObserveReliableEventDeliveryAsync(observer, replay);
        }

        PushSnapshotTo(observer, isFullSnapshot: true);
        return Task.CompletedTask;
    }

    public Task<ReliableBattleEventAckResult> AcknowledgeReliableEventsAsync(string observerKey, string epoch, long sequence)
    {
        if (_reliableEvents == null)
        {
            return Task.FromResult(new ReliableBattleEventAckResult
            {
                Accepted = false,
                Epoch = epoch ?? string.Empty,
                RequiresResync = true
            });
        }

        var epochMatches = string.Equals(epoch, _reliableEvents.Epoch, StringComparison.Ordinal);
        var accepted = _reliableEvents.Acknowledge(observerKey, epoch, sequence);
        var observerRegistered = !string.IsNullOrWhiteSpace(observerKey)
            && _observerContexts.Values.Any(context => string.Equals(context.ObserverKey, observerKey, StringComparison.Ordinal));
        return Task.FromResult(new ReliableBattleEventAckResult
        {
            Accepted = epochMatches && observerRegistered,
            Epoch = _reliableEvents.Epoch,
            AcceptedSequence = accepted,
            Watermark = _reliableEvents.Watermark,
            RequiresResync = !epochMatches || !observerRegistered
        });
    }

    public Task RequestFullSnapshotAsync(IStateSyncObserverGrain observer, StateSyncObserverInfo observerInfo)
    {
        if (observer == null)
        {
            throw new ArgumentNullException(nameof(observer));
        }

        if (!_initialized)
        {
            return Task.CompletedTask;
        }

        _observerContexts[observer] = CreateObserverContext(observerInfo);
        PushSnapshotTo(observer, isFullSnapshot: true);
        return Task.CompletedTask;
    }

    public Task UnsubscribeAsync(IStateSyncObserverGrain observer)
    {
        if (observer == null)
        {
            return Task.CompletedTask;
        }

        if (_observerContexts.Remove(observer, out var observerContext))
        {
            _reliableEvents?.UnregisterObserver(observerContext.ObserverKey);
        }

        if (_observerRegistry.Unsubscribe(observer))
        {
            _logger.LogInformation("[BattleLogicHost] Observer unsubscribed. Total observers: {Count}", _observerRegistry.Count);
        }

        return Task.CompletedTask;
    }

    public Task DestroyAsync()
    {
        _logger.LogInformation("[BattleLogicHost] Destroying battle - WorldId: {WorldId}", _worldId);
        _observerRegistry.Clear();
        _observerContexts.Clear();
        StopBattleRuntime();
        DeactivateOnIdle();
        return Task.CompletedTask;
    }

    public Task<BattleTickFrameResult> TickFrameAsync(ulong worldId, int frame, float deltaTime,
        IReadOnlyList<FrameInputItem> frameInputs)
    {
        if (!_initialized || _runtimeSession is null)
        {
            return Task.FromResult(new BattleTickFrameResult(frame, false, 0));
        }

        if (_worldId != worldId)
        {
            _logger.LogWarning("[BattleLogicHost] TickFrameAsync worldId mismatch. Expected: {Expected}, Received: {Received}", _worldId, worldId);
            return Task.FromResult(new BattleTickFrameResult(frame, false, 0));
        }

        try
        {
            // 1. 将帧同步输入转换为 BattleInputItem 并提交给运行时
            var battleInputs = new BattleInputItem[frameInputs?.Count ?? 0];
            if (frameInputs is { Count: > 0 })
            {
                for (var i = 0; i < frameInputs.Count; i++)
                {
                    var fi = frameInputs[i];
                    battleInputs[i] = new BattleInputItem
                    {
                        PlayerId = fi.PlayerId,
                        OpCode = fi.OpCode,
                        Payload = fi.Payload
                    };
                }
            }

            var submittedInputCount = _runtimeSession.SubmitInputs(frame, battleInputs);
            if (battleInputs.Length > 0 && submittedInputCount != battleInputs.Length)
            {
                var diagnostic = (_runtimeSession as IBattleRuntimeInputDiagnostics)?.LastInputSubmitDiagnostic;
                _logger.LogWarning(
                    "[BattleLogicHost] Runtime input batch was not fully accepted. BattleId: {BattleId}, Frame: {Frame}, Received: {Received}, Submitted: {Submitted}, Diagnostic: {Diagnostic}",
                    _battleId,
                    frame,
                    battleInputs.Length,
                    submittedInputCount,
                    string.IsNullOrWhiteSpace(diagnostic) ? "unavailable" : diagnostic);
            }

            // 2. Tick 世界
            var worldTicked = _runtimeSession.Tick(frame, _tickRate, deltaTime);

            // 3. 推进帧计数器
            _battleHostState.AdvanceFrame();

            // 4. 捕获并发布可靠事件
            if (worldTicked)
            {
                // 可靠事件捕获（fire-and-forget，不阻塞帧时钟）
                _ = CaptureAndPublishReliableEventsAsync(frame);
            }

            // 5. 发布快照
            if (_snapshotSyncPolicy.ShouldPublish(frame, _observerRegistry.Count, worldTicked))
            {
                PushSnapshot(frame, _snapshotSyncPolicy.ShouldCreateFullSnapshot(frame));
            }

            // 6. 刷新快照投递（fire-and-forget）
            _ = FlushSnapshotDeliveriesAsync();

            // 7. 获取状态 hash（从诊断接口读取，若无则从 BattleHostState 计算近似值）
            var diagnostics = _runtimeSession.GetWorldDiagnostics(_worldId, frame);
            var stateHash = diagnostics?.StateHash ?? (uint)_battleHostState.Frame;

            return Task.FromResult(new BattleTickFrameResult(frame, worldTicked, stateHash));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BattleLogicHost] Error in TickFrameAsync. Frame: {Frame}", frame);
            return Task.FromResult(new BattleTickFrameResult(frame, false, 0));
        }
    }

    private async Task OnTickAsync()
    {
        if (_externalTickMode)
        {
            // 外部时钟模式 —— BattleFrameSyncGrain 通过 TickFrameAsync 驱动
            return;
        }

        try
        {
            var tickResult = _tickDriver.Tick(_battleHostState, _inputBuffer);
            if (!tickResult.InputSubmitted)
            {
                _logger.LogWarning("[BattleLogicHost] Runtime input rejected. Frame: {Frame}", tickResult.Frame);
            }

            if (tickResult.WorldTicked)
            {
                await CaptureAndPublishReliableEventsAsync(tickResult.Frame);
            }

            if (_snapshotSyncPolicy.ShouldPublish(tickResult.Frame, _observerRegistry.Count, tickResult.WorldTicked))
            {
                PushSnapshot(tickResult.Frame, _snapshotSyncPolicy.ShouldCreateFullSnapshot(tickResult.Frame));
            }

            await FlushSnapshotDeliveriesAsync();

            _logger.LogDebug(
                "[BattleLogicHost] Tick - Frame: {Frame}, Inputs: {InputCount}, Commands: {CommandCount}, Observers: {ObserverCount}",
                tickResult.Frame,
                tickResult.InputCount,
                tickResult.CommandCount,
                _observerRegistry.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BattleLogicHost] Error in OnTickAsync");
        }

        await Task.CompletedTask;
    }

    private async Task CaptureAndPublishReliableEventsAsync(int frame)
    {
        if (_runtimeSession is not IReliableBattleEventProducer producer || _reliableEvents == null)
        {
            return;
        }

        var sourceEvents = producer.CaptureReliableEvents(frame);
        if (sourceEvents.Count == 0)
        {
            return;
        }

        var appended = new List<ReliableBattleEventEnvelope>(sourceEvents.Count);
        foreach (var source in sourceEvents)
        {
            appended.Add(_reliableEvents.Append(source.SourceFrame, source.EventType, source.Payload));
        }

        var batch = new ReliableBattleEventBatch
        {
            BattleId = _reliableEvents.BattleId,
            Epoch = _reliableEvents.Epoch,
            FirstAvailableSequence = _reliableEvents.FirstAvailableSequence,
            Watermark = _reliableEvents.Watermark,
            Events = appended
        };
        foreach (var observer in _observerRegistry.Snapshot())
        {
            _ = ObserveReliableEventDeliveryAsync(observer, batch);
        }
    }

    private int SubmitRuntimeInputs(int frame, IReadOnlyList<BattleInputItem> inputs)
    {
        if (inputs == null || inputs.Count == 0 || _runtimeSession == null)
        {
            return 0;
        }

        var submitted = _runtimeSession.SubmitInputs(frame, inputs);
        if (submitted < inputs.Count)
        {
            return submitted;
        }

        for (var i = 0; i < inputs.Count; i++)
        {
            var input = inputs[i];
            if (input.PlayerId == 0 || input.CommandSequence == 0)
            {
                continue;
            }

            if (!_consumedCommandSequences.TryGetValue(input.PlayerId, out var current)
                || input.CommandSequence > current)
            {
                _consumedCommandSequences[input.PlayerId] = input.CommandSequence;
                _consumedCommandSequenceVersion++;
            }
        }

        return submitted;
    }

    private bool TickBattleWorld(int frame, int tickRate, float deltaTime)
    {
        return _runtimeSession?.Tick(frame, tickRate, deltaTime) == true;
    }

    private void PublishInitialSnapshot()
    {
        if (_observerRegistry.Count > 0)
        {
            PushSnapshot(isFullSnapshot: true);
        }
    }

    private void StartBattleTimer()
    {
        _timer = RegisterTimer(_ => OnTickAsync(), state: null, dueTime: _tickInterval, period: _tickInterval);
    }

    private void PushSnapshot(bool isFullSnapshot)
    {
        PushSnapshot(_battleHostState.Frame, isFullSnapshot);
    }

    private void PushSnapshotTo(IStateSyncObserverGrain observer, bool isFullSnapshot)
    {
        _snapshotPublisher.PublishTo(
            observer,
            _battleHostState.Frame,
            isFullSnapshot,
            BuildStateSyncPush);
    }

    private void PushSnapshot(int frame, bool isFullSnapshot)
    {
        var syncTemplate = _syncProfile?.ResolveTemplate(_syncTemplateId);
        if (syncTemplate?.SupportsStateSyncPush == false)
        {
            _logger.LogTrace(
                "[BattleLogicHost] Skipping state-sync publish for frame-sync template. BattleId: {BattleId}, SyncTemplate: {SyncTemplate}",
                _battleId,
                _syncTemplateId);
            return;
        }

        var observers = _observerRegistry.Snapshot();
        if (_runtimeSession is IObserverAwareBattleRuntimeSession)
        {
            _snapshotPublisher.PublishPerObserver(observers, frame, isFullSnapshot, BuildStateSyncPush);
            return;
        }

        _snapshotPublisher.Publish(observers, frame, isFullSnapshot);
    }

    private StateSyncPush BuildStateSyncPush(IStateSyncObserverGrain observer, int frame, bool isFullSnapshot)
    {
        if (_runtimeSession is IObserverAwareBattleRuntimeSession observerAwareSession)
        {
            if (!_observerContexts.TryGetValue(observer, out var observerContext))
            {
                observerContext = default;
            }

            observerContext = observerContext with { AcknowledgedCommands = GetConsumedCommandAcknowledgements() };
            return NormalizeStateSyncPush(
                observerAwareSession.CreateStateSyncPush(_worldId, frame, isFullSnapshot, in observerContext),
                frame,
                isFullSnapshot,
                attachCommandAcknowledgements: false);
        }

        return BuildStateSyncPush(frame, isFullSnapshot);
    }

    private StateSyncPush BuildStateSyncPush(int frame, bool isFullSnapshot)
    {
        var push = _runtimeSession?.CreateStateSyncPush(_worldId, frame, isFullSnapshot)
            ?? new StateSyncPush
            {
                WorldId = _worldId,
                Frame = frame,
                IsFullSnapshot = isFullSnapshot
            };

        return NormalizeStateSyncPush(push, frame, isFullSnapshot);
    }

    private StateSyncPush NormalizeStateSyncPush(
        StateSyncPush push,
        int frame,
        bool isFullSnapshot,
        bool attachCommandAcknowledgements = true)
    {
        if (push == null)
        {
            push = new StateSyncPush
            {
                Frame = frame,
                IsFullSnapshot = isFullSnapshot
            };
        }

        var serverTicks = DateTime.UtcNow.Ticks;
        push.ServerTicks = serverTicks;
        push.EventWatermark = _reliableEvents?.Watermark ?? 0;
        push.EventEpoch = _reliableEvents?.Epoch ?? string.Empty;
        if (push.Timestamp <= 0d)
        {
            push.Timestamp = serverTicks;
        }

        if (attachCommandAcknowledgements)
        {
            AttachConsumedCommandAcknowledgements(push);
        }
        return push;
    }

    private ShooterCommandAcknowledgement[] GetConsumedCommandAcknowledgements()
    {
        if (_consumedCommandSequences.Count == 0)
        {
            return Array.Empty<ShooterCommandAcknowledgement>();
        }

        if (_cachedCommandAcknowledgementVersion == _consumedCommandSequenceVersion)
        {
            return _cachedCommandAcknowledgements;
        }

        var acknowledgements = new ShooterCommandAcknowledgement[_consumedCommandSequences.Count];
        var index = 0;
        foreach (var pair in _consumedCommandSequences)
        {
            acknowledgements[index++] = new ShooterCommandAcknowledgement((int)pair.Key, pair.Value);
        }

        _cachedCommandAcknowledgements = acknowledgements;
        _cachedCommandAcknowledgementVersion = _consumedCommandSequenceVersion;
        return acknowledgements;
    }

    private void AttachConsumedCommandAcknowledgements(StateSyncPush push)
    {
        if (_consumedCommandSequences.Count == 0 || push.Payload == null || push.Payload.Length == 0)
        {
            return;
        }

        var acknowledgements = new ShooterCommandAcknowledgement[_consumedCommandSequences.Count];
        var index = 0;
        foreach (var pair in _consumedCommandSequences)
        {
            acknowledgements[index++] = new ShooterCommandAcknowledgement((int)pair.Key, pair.Value);
        }

        if (push.PayloadOpCode == ShooterOpCodes.Snapshot.PackedState
            || push.PayloadOpCode == ShooterOpCodes.Snapshot.PackedStateDelta)
        {
            var snapshot = ShooterPackedSnapshotCodec.Deserialize(push.Payload);
            snapshot.AcknowledgedCommands = acknowledgements;
            push.Payload = ShooterPackedSnapshotCodec.Serialize(in snapshot);
            return;
        }

        if (push.PayloadOpCode == ShooterOpCodes.Snapshot.PureState
            || push.PayloadOpCode == ShooterOpCodes.Snapshot.PureStateDelta)
        {
            var snapshot = ShooterPureStateSyncCodec.Deserialize(push.Payload);
            snapshot.AcknowledgedCommands = acknowledgements;
            push.Payload = ShooterPureStateSyncCodec.Serialize(in snapshot);
        }
    }

    private void SendStateSyncPush(IStateSyncObserverGrain observer, StateSyncPush push)
    {
        if (_snapshotDeliveries.TryGetValue(observer, out var inFlight))
        {
            inFlight.Pending = CoalescePendingSnapshot(inFlight.Pending, push);
            return;
        }

        _snapshotDeliveries.Add(observer, new SnapshotDeliveryInFlight(ObserveSnapshotDeliveryAsync(observer, push)));
    }

    private async Task ObserveReliableEventDeliveryAsync(
        IStateSyncObserverGrain observer,
        ReliableBattleEventBatch batch)
    {
        try
        {
            await observer.OnReliableEventsPushedAsync(batch);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "[BattleLogicHost] Error pushing reliable event replay to observer");
        }
    }

    private async Task ObserveSnapshotDeliveryAsync(IStateSyncObserverGrain observer, StateSyncPush push)
    {
        try
        {
            var result = await observer.OnSnapshotPushedAsync(push);
            HandleSnapshotDeliveryResult(observer, push.Frame, result);
        }
        catch (Exception exception)
        {
            HandleSnapshotPublishError(observer, exception);
        }
    }

    private Task FlushSnapshotDeliveriesAsync()
    {
        if (_snapshotDeliveries.Count == 0)
        {
            return Task.CompletedTask;
        }

        foreach (var pair in _snapshotDeliveries.ToArray())
        {
            if (!pair.Value.Delivery.IsCompleted)
            {
                continue;
            }

            if (pair.Value.Pending is { } pending)
            {
                pair.Value.Delivery = ObserveSnapshotDeliveryAsync(pair.Key, pending);
                pair.Value.Pending = null;
                continue;
            }

            _snapshotDeliveries.Remove(pair.Key);
        }

        return Task.CompletedTask;
    }

    internal static bool ShouldBroadcastFullSnapshotAfterPlayerJoin(BattlePlayerJoinResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return false;
    }

    internal static StateSyncPush CoalescePendingSnapshot(StateSyncPush? current, StateSyncPush candidate)
    {
        if (current == null || candidate.IsFullSnapshot || !current.IsFullSnapshot)
        {
            return candidate;
        }

        return current;
    }

    private void HandleSnapshotDeliveryResult(
        IStateSyncObserverGrain observer,
        int frame,
        StateSyncDeliveryResult result)
    {
        if (result.Status is StateSyncDeliveryStatus.Accepted or StateSyncDeliveryStatus.Queued)
            return;

        _logger.LogWarning(
            "[BattleLogicHost] Snapshot delivery was not accepted. Observer: {Observer}, Frame: {Frame}, Status: {Status}, QueueLength: {QueueLength}, DroppedItems: {DroppedItems}, BaselineInvalidated: {BaselineInvalidated}",
            observer.GetPrimaryKeyString(),
            frame,
            result.Status,
            result.QueueLength,
            result.DroppedItems,
            result.BaselineInvalidated);
    }

    private void HandleSnapshotPublishError(IStateSyncObserverGrain observer, Exception exception)
    {
        _logger.LogError(exception, "[BattleLogicHost] Error pushing snapshot to observer");
    }

    internal static BattleStateSyncObserverContext CreateObserverContext(StateSyncObserverInfo? info)
    {
        return new BattleStateSyncObserverContext(
            info?.ObserverKey ?? string.Empty,
            info?.AccountId ?? string.Empty,
            info?.RoomId ?? string.Empty);
    }

    private void StopBattleRuntime()
    {
        _reliableEvents = null;
        _timer?.Dispose();
        _timer = null;
        _runtimeSession?.Dispose();
        _runtimeSession = null;
        _observerContexts.Clear();
        _snapshotDeliveries.Clear();
        _battleId = string.Empty;
        _worldId = 0;
        _initialized = false;
        _worldStartAnchor = null;
        _syncProfile = null;
        _syncTemplateId = string.Empty;
        _snapshotSyncPolicy = new BattleSnapshotSyncPolicy();
        _initSpecHash = null;
        _inputBuffer.Clear();
        _inputAdmissionGuard.Clear();
        _consumedCommandSequences.Clear();
        _cachedCommandAcknowledgements = Array.Empty<ShooterCommandAcknowledgement>();
        _consumedCommandSequenceVersion = 0;
        _cachedCommandAcknowledgementVersion = -1;
        _battleHostState.Reset();
    }

    private sealed class SnapshotDeliveryInFlight
    {
        public SnapshotDeliveryInFlight(Task delivery)
        {
            Delivery = delivery;
        }

        public Task Delivery { get; set; }

        public StateSyncPush? Pending { get; set; }
    }
}

