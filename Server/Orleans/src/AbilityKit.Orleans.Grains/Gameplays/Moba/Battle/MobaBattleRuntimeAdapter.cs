using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.Moba.Runtime;
using AbilityKit.Ability.World.Services;
using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Demo.Moba.Gameplay;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Motion;
using AbilityKit.Demo.Moba.Services.StateSync;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Network.Battle.Projection;
using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Orleans.Grains.Battle;
using AbilityKit.Orleans.Grains.Battle.Gameplay;
using AbilityKit.Orleans.Grains.Gameplay;
using AbilityKit.Orleans.Grains.Gameplays.Moba.Protocol;
using IWorld = AbilityKit.Ability.World.Abstractions.IWorld;


namespace AbilityKit.Orleans.Grains.Gameplays.Moba.Battle;

internal interface IMobaSettledFrameRuntime
{
    bool TryTickSettledFrame(int frame, int tickRate, IReadOnlyList<BattleInputItem> inputs,
        out IReadOnlyList<MobaFrameInputOutcome> outcomes, out IReadOnlyList<MobaFrameAuthorityEntry> entries);
}

/// <summary>
/// 仅状态同步宿主在同一已完成 Tick 内读取的类型化截面。
/// </summary>
internal interface IMobaStateSyncMovementSource
{
    bool TryCaptureCommittedMovement(int tick, uint playerId, Guid expectedSourceConnectionGeneration,
        out MobaCommittedMovementSample sample, Guid expectedInputGeneration = default);
}

internal sealed class MobaBattleRuntimeAdapter : IBattleRuntimeAdapter
{
    private readonly ServerBattleWorldManager _worldManager;
    private readonly IOrleansBattleProtocolMapper _protocolMapper;

    public MobaBattleRuntimeAdapter(ServerBattleWorldManager worldManager, IOrleansBattleProtocolMapper protocolMapper)
    {
        _worldManager = worldManager ?? throw new ArgumentNullException(nameof(worldManager));
        _protocolMapper = protocolMapper ?? throw new ArgumentNullException(nameof(protocolMapper));
    }

    public string RoomType => GameplayRoomTypes.Moba;

    public IBattleRuntimeSession CreateSession(string battleId)
    {
        return new MobaBattleRuntimeSession(battleId, _worldManager, _protocolMapper);
    }

    private sealed class MobaBattleRuntimeSession : IBattleRuntimeSession, IBattleRuntimeInputDiagnostics,
        IBattleRuntimeFrameCompletion, IBattleRuntimeExternalFrameClock, IMobaSettledFrameRuntime,
        IFixedBaselineBattleSnapshotSource, IMobaStateSyncMovementSource, IBattleRuntimeStateSyncClock
    {
        private bool _settledFrameFaulted;
        private readonly string _battleId;
        private readonly ServerBattleWorldManager _worldManager;
        private readonly IOrleansBattleProtocolMapper _protocolMapper;
        private IWorld? _battleWorld;
        private IWorldStateSnapshotProvider? _snapshotProvider;
        private IMobaBattleRuntimePort? _runtimePort;
        private MobaCombatFrameGuard? _frameGuard;
        private IFrameTime? _frameTime;
        private int _nextExternalFrame = 1;
        private int? _completedSettlementFrame;
        private bool _hasUnrecordedPlayerJoin;
        private ulong _worldId;
        private MobaDeterministicCheckpointCoordinator? _checkpointCoordinator;
        private List<ActorProjectionData>? _projectionBuffer;
        private Dictionary<int, ActorProjectionData>? _lastProjectionData;
        private readonly Dictionary<uint, MobaBotState> _bots = new();
        private readonly Random _botRandom = new();
        private MobaStateSyncInputTerminalLedger? _stateSyncTerminals;
        private readonly List<StateSyncDispatch> _stateSyncDispatches = new();
        private int? _stateSyncDispatchTick;
        private int? _preparedStateSyncTick;
        private int? _completedStateSyncTick;
        private int _nextStateSyncTick;

        private readonly record struct StateSyncDispatch(uint PlayerId, ulong Sequence, Guid SourceConnectionGeneration, Guid InputGeneration, PlayerId Controller,
            int ActorId, int Lifetime, MobaFrameInputDisposition Disposition);

        public string LastInputSubmitDiagnostic { get; private set; } = string.Empty;

        public MobaBattleRuntimeSession(string battleId, ServerBattleWorldManager worldManager, IOrleansBattleProtocolMapper protocolMapper)
        {
            _battleId = battleId;
            _worldManager = worldManager;
            _protocolMapper = protocolMapper;
        }

        public BattleRuntimeStartResult Start(BattleInitParams initParams)
        {
            if (initParams is null)
            {
                return BattleRuntimeStartResult.Fail("Battle init params are missing.");
            }

            _worldId = initParams.WorldId;
            _stateSyncTerminals = string.Equals(initParams.SyncOptions?.SyncTemplateId,
                ServerGameplayDescriptors.MobaStateSyncTemplate, StringComparison.Ordinal)
                ? new MobaStateSyncInputTerminalLedger() : null;
            var launchSpec = _protocolMapper.CreateLaunchSpec(_battleId, initParams.TickRate, initParams);
            var initData = launchSpec.ToWorldInitData(MobaWorldBootstrapModule.InitOpCode);
            _battleWorld = _worldManager.CreateBattleWorld(
                _battleId,
                initParams.TickRate,
                options =>
                {
                    options.ServiceBuilder ??= WorldServiceContainerFactory.CreateDefaultOnly();
                    options.ServiceBuilder.RegisterInstance(initData);
                });
            if (_battleWorld == null)
            {
                return BattleRuntimeStartResult.Fail("Battle world creation returned null.");
            }

            if (!_battleWorld.Services.TryResolve<IMobaBattleRuntimePort>(out _runtimePort) || _runtimePort == null)
            {
                return BattleRuntimeStartResult.Fail("IMobaBattleRuntimePort not resolved.");
            }

            if (!_runtimePort.Status.IsReadyForGameStart)
            {
                return BattleRuntimeStartResult.Fail(_runtimePort.Status.ToString());
            }

            if (!_battleWorld.Services.TryResolve<MobaGameplayService>(out var gameplay) || gameplay == null)
            {
                return BattleRuntimeStartResult.Fail("MobaGameplayService not resolved.");
            }

            if (!gameplay.IsRunning || gameplay.CurrentGameplayId != initParams.GameplayId)
            {
                return BattleRuntimeStartResult.Fail(
                    $"MOBA bootstrap did not start the requested gameplay. phase={gameplay.Phase}, " +
                    $"currentGameplayId={gameplay.CurrentGameplayId}, requestedGameplayId={initParams.GameplayId}.");
            }

            _snapshotProvider = _battleWorld.Services.Resolve<IWorldStateSnapshotProvider>();
            if (_snapshotProvider == null)
            {
                return BattleRuntimeStartResult.Fail("IWorldStateSnapshotProvider not resolved.");
            }

            // 解析确定性检查点协调器（用于每帧 hash 对账）。
            // 如果 MOBA 世界装配未注册此服务（例如测试场景），优雅降级为 null。
            _battleWorld.Services.TryResolve(out _checkpointCoordinator);
            _battleWorld.Services.TryResolve(out _frameGuard);
            _battleWorld.Services.TryResolve(out _frameTime);

            return BattleRuntimeStartResult.Success();
        }

        public BattlePlayerJoinResult JoinPlayer(BattlePlayerJoinRequest request, int currentFrame)
        {
            if (request?.Player == null || request.Player.PlayerId == 0)
            {
                return new BattlePlayerJoinResult(false, request?.Player?.PlayerId ?? 0u, currentFrame,
                    "RejectedInvalidPlayer", "Player id must be positive.");
            }

            if (_battleWorld == null || _runtimePort == null)
            {
                return new BattlePlayerJoinResult(false, request.Player.PlayerId, currentFrame,
                    "RejectedRuntimeNotReady", "MOBA runtime is not ready.");
            }

            var spawnResult = _battleWorld.Services.TryResolve<MobaGameplayService>(out var gameplay) && gameplay != null
                ? TrySpawnBotPlayer(gameplay, request.Player)
                : false;
            _hasUnrecordedPlayerJoin |= spawnResult;

            return spawnResult
                ? new BattlePlayerJoinResult(true, request.Player.PlayerId, currentFrame, "Joined", string.Empty)
                : new BattlePlayerJoinResult(false, request.Player.PlayerId, currentFrame,
                    "RejectedSpawnFailed", "Failed to spawn bot player in MOBA world.");
        }

        public BattleBotAiMountResult MountBotAi(BattleBotAiMountRequest request, int currentFrame)
        {
            if (request == null || request.PlayerId == 0)
            {
                return new BattleBotAiMountResult(false, request?.PlayerId ?? 0u, currentFrame,
                    "RejectedInvalidPlayerId", "Player id must be positive.");
            }

            if (_battleWorld == null || _runtimePort == null)
            {
                return new BattleBotAiMountResult(false, request.PlayerId, currentFrame,
                    "RejectedRuntimeNotReady", "MOBA runtime is not ready.");
            }

            // 简单 Bot：注册到内部 bot 列表，每帧生成随机移动输入
            if (_bots.ContainsKey(request.PlayerId))
            {
                return new BattleBotAiMountResult(false, request.PlayerId, currentFrame,
                    "RejectedAlreadyMounted", "Bot already mounted for this player.");
            }

            _bots[request.PlayerId] = new MobaBotState(request.PlayerId);
            return new BattleBotAiMountResult(true, request.PlayerId, currentFrame, "Mounted", string.Empty);
        }

        public bool HasInitialFrameCheckpoint => true;

        public bool TryPrepareExternalFrame(int frame, float fixedDeltaTime)
        {
            _completedSettlementFrame = null;
            if (frame != _nextExternalFrame || _frameGuard?.CanAdvance != true || _frameTime is not FrameTime time ||
                !float.IsFinite(fixedDeltaTime) || fixedDeltaTime <= 0f ||
                !time.AlignTo(new FrameIndex(frame), fixedDeltaTime))
            {
                return false;
            }

            _nextExternalFrame++;
            return true;
        }

        public bool TryBeginStateSyncTick(int tick, int tickRate)
        {
            if (_stateSyncTerminals == null || tick != _nextStateSyncTick || tickRate <= 0 ||
                _frameGuard?.CanAdvance != true || _frameTime is not FrameTime time ||
                !time.AlignTo(new FrameIndex(tick), 1f / tickRate)) return false;

            _completedStateSyncTick = null;
            _preparedStateSyncTick = tick;
            _nextStateSyncTick++;
            return true;
        }

        public int SubmitInputs(int frame, IReadOnlyList<BattleInputItem> inputs)
        {
            if (inputs.Count == 0 || _runtimePort == null)
            {
                LastInputSubmitDiagnostic = _runtimePort == null
                    ? "MOBA runtime port is unavailable."
                    : string.Empty;
                return 0;
            }

            var commands = _protocolMapper.CreatePlayerInputCommands(frame, inputs);
            if (commands.Count == 0)
            {
                if (_stateSyncTerminals is { } ledger)
                    foreach (var input in inputs) ledger.Invalidate(input.PlayerId);
                LastInputSubmitDiagnostic = $"Protocol mapper produced no commands. frame={frame}, inputs={inputs.Count}.";
                return 0;
            }

            var result = SubmitObservedStateSyncInputs(frame, inputs, commands);
            LastInputSubmitDiagnostic = result.Succeeded ? string.Empty : result.ToString();
            return result.Succeeded ? result.CommandCount : 0;
        }

        private MobaInputSubmitResult SubmitObservedStateSyncInputs(int tick, IReadOnlyList<BattleInputItem> inputs,
            IReadOnlyList<PlayerInputCommand> commands)
        {
            var ledger = _stateSyncTerminals;
            if (ledger == null) return _runtimePort!.Submit(new FrameIndex(tick), commands);

            _completedStateSyncTick = null;
            var services = _battleWorld?.Services;
            if (_preparedStateSyncTick != tick || commands.Count != inputs.Count || services == null ||
                !services.TryResolve<MobaBattleIOPort>(out var port) || port == null)
            {
                foreach (var input in inputs) ledger.Invalidate(input.PlayerId);
                return _runtimePort!.Submit(new FrameIndex(tick), commands);
            }

            if (_stateSyncDispatchTick != tick)
            {
                FinishStateSyncInputs(_stateSyncDispatchTick, committed: false);
                _stateSyncDispatchTick = tick;
            }

            var start = _stateSyncDispatches.Count;
            for (var index = 0; index < inputs.Count; index++)
            {
                var input = inputs[index];
                var controller = commands[index].Player;
                TryResolveStateSyncActor(controller, out var actorId, out var version);
                _stateSyncDispatches.Add(new StateSyncDispatch(input.PlayerId, input.CommandSequence,
                    input.SourceConnectionGeneration, input.InputGeneration, controller, actorId, version, MobaFrameInputDisposition.Unknown));
            }

            return port.SubmitObserved(new FrameIndex(tick), commands, (ordinal, disposition) =>
            {
                var index = start + ordinal;
                _stateSyncDispatches[index] = _stateSyncDispatches[index] with { Disposition = disposition };
            });
        }

        private bool TryResolveStateSyncActor(PlayerId controller, out int actorId, out int version)
        {
            actorId = version = 0;
            var services = _battleWorld?.Services;
            if (services == null || !services.TryResolve<MobaPlayerActorMapService>(out var map) ||
                !map.TryGetActorId(controller, out actorId) || !services.TryResolve<MobaActorRegistry>(out var actors) ||
                !actors.TryGet(actorId, out _)) return false;
            version = actors.GetEntityVersion(actorId);
            return version > 0;
        }

        private void FinishStateSyncInputs(int? tick, bool committed)
        {
            var ledger = _stateSyncTerminals;
            if (ledger == null) return;
            foreach (var dispatch in _stateSyncDispatches)
            {
                if (committed && _stateSyncDispatchTick == tick &&
                    TryResolveStateSyncActor(dispatch.Controller, out var actorId, out var version) &&
                    dispatch.ActorId == actorId && dispatch.Lifetime == version)
                    ledger.ObserveCommitted(dispatch.PlayerId, actorId, version, dispatch.SourceConnectionGeneration,
                        dispatch.Sequence, dispatch.Disposition, dispatch.InputGeneration);
                else
                    ledger.Invalidate(dispatch.PlayerId);
            }
            _stateSyncDispatches.Clear();
            _stateSyncDispatchTick = null;
        }

        public bool TryCaptureCommittedMovement(int tick, uint playerId, Guid expectedSourceConnectionGeneration,
            out MobaCommittedMovementSample sample, Guid expectedInputGeneration = default)
        {
            sample = default;
            if (_stateSyncTerminals == null || playerId == 0 || _completedStateSyncTick != tick ||
                !MobaAuthoritativeMovementAnchor.TryCapture(_battleWorld?.Services,
                    new PlayerId(playerId.ToString(System.Globalization.CultureInfo.InvariantCulture)), out var anchor)) return false;

            var hasPrefix = _stateSyncTerminals.TryRead(playerId, anchor.ActorId, anchor.EntityVersion,
                expectedSourceConnectionGeneration, out var terminalThrough, expectedInputGeneration);
            sample = new MobaCommittedMovementSample(_battleId, _worldId, tick, playerId, anchor,
                hasPrefix ? expectedSourceConnectionGeneration : Guid.Empty, hasPrefix ? terminalThrough : null,
                hasPrefix ? expectedInputGeneration : Guid.Empty);
            return true;
        }

        public bool TryTickSettledFrame(int frame, int tickRate, IReadOnlyList<BattleInputItem> inputs,
            out IReadOnlyList<MobaFrameInputOutcome> outcomes, out IReadOnlyList<MobaFrameAuthorityEntry> entries)
        {
            outcomes = null!;
            entries = null!;
            if (_settledFrameFaulted || tickRate <= 0 || _battleWorld == null || _bots.Count != 0 || _hasUnrecordedPlayerJoin ||
                !_battleWorld.Services.TryResolve<MobaInputCoordinator>(out var coordinator) || coordinator == null)
                return false;

            var commands = _protocolMapper.CreatePlayerInputCommands(frame, inputs);
            if (commands.Count != inputs.Count) return false;

            var deltaTime = 1f / tickRate;
            if (!TryPrepareExternalFrame(frame, deltaTime)) return false;

            try
            {
                using var capture = coordinator.BeginAuthorityFrameCapture(new FrameIndex(frame));
                if (!capture.Submit(commands)) return false;
                var worldCompleted = Tick(frame, tickRate, deltaTime) && CanFinalizeFrame(frame);
                return capture.TryComplete(worldCompleted, out outcomes, out entries);
            }
            finally
            {
                _settledFrameFaulted = outcomes == null;
            }
        }

        public bool Tick(int frame, int tickRate, float deltaTime)
        {
            _completedSettlementFrame = null;
            _completedStateSyncTick = null;
            var isPreparedStateSyncTick = _preparedStateSyncTick == frame;
            _preparedStateSyncTick = null;
            if (_battleWorld == null)
            {
                return false;
            }

            // 为已挂载的 Bot 生成并提交帧输入
            if (_bots.Count > 0 && _runtimePort != null)
            {
                var botCommands = new List<PlayerInputCommand>(_bots.Count);
                foreach (var (playerId, state) in _bots)
                {
                    var cmd = GenerateBotCommand(playerId, frame, state);
                    if (cmd.HasValue)
                    {
                        botCommands.Add(cmd.Value);
                    }
                }

                if (botCommands.Count > 0)
                {
                    _runtimePort.Submit(new FrameIndex(frame), botCommands);
                }
            }

            var completedTicks = _frameGuard?.CompletedTickCount;
            try
            {
                _battleWorld.Tick(deltaTime);
            }
            catch
            {
                FinishStateSyncInputs(frame, committed: false);
                throw;
            }
            if (_bots.Count == 0 && !_hasUnrecordedPlayerJoin && completedTicks.HasValue &&
                _frameGuard!.CompletedTickCount == completedTicks.Value + 1 && _frameGuard.CanCommit &&
                _frameTime is { DeltaTime: > 0f } && _frameTime.Frame.Value == frame)
            {
                if (_stateSyncTerminals == null)
                    _completedSettlementFrame = frame;
                else if (isPreparedStateSyncTick)
                    _completedStateSyncTick = frame;
            }

            FinishStateSyncInputs(frame, _completedStateSyncTick == frame);
            return true;
        }

        public bool CanFinalizeFrame(int frame) => _completedSettlementFrame == frame;

        public BattleSnapshot? GetSnapshot(int frame)
        {
            var frameIndex = new FrameIndex(frame);
            if (_snapshotProvider != null && _snapshotProvider.TryGetSnapshot(frameIndex, out var snapshot))
            {
                return _protocolMapper.CreateBattleSnapshot(frame, snapshot, _runtimePort?.GetDiagnosticEntityStates());
            }

            return null;
        }

        public BattleDiagnosticEventsResult QueryDiagnosticEvents(BattleDiagnosticEventsQuery query)
        {
            if (_battleWorld == null)
            {
                return CreateDiagnosticResult(
                    query,
                    "Unavailable",
                    BattleDiagnosticDataAvailability.Disconnected,
                    0,
                    false,
                    "Battle world is not initialized.",
                    Array.Empty<BattleDiagnosticEvent>());
            }

            if (!_battleWorld.Services.TryResolve<IBattleDiagnosticEventReadStore>(out var store) || store == null)
            {
                return CreateDiagnosticResult(
                    query,
                    "Unavailable",
                    BattleDiagnosticDataAvailability.Unsupported,
                    0,
                    false,
                    "The runtime does not expose a diagnostic event read store.",
                    Array.Empty<BattleDiagnosticEvent>());
            }

            var effectiveLimit = query.Limit <= 0 ? 100 : Math.Min(query.Limit, 500);
            var effectiveOffset = Math.Max(0, query.Offset);
            var filter = new BattleDiagnosticFilter(
                new BattleDiagnosticFrameFilter(BattleDiagnosticFrames.Invalid, BattleDiagnosticFrames.Invalid),
                BattleDiagnosticEventChannel.All,
                query.ActorId ?? 0,
                BattleDiagnosticActorRelation.Either,
                query.SkillId ?? 0);
            var result = store.Query(new BattleDiagnosticEventQuery(
                query.RequestId <= 0 ? 1 : query.RequestId,
                filter,
                new BattleDiagnosticPageRequest(query.StoreRevision, effectiveOffset, effectiveLimit),
                query.NewestFirst));
            var status = result.Status;
            return CreateDiagnosticResult(
                query,
                status.Phase.ToString(),
                status.Availability,
                status.StoreRevision,
                status.HasMore,
                string.IsNullOrEmpty(status.Message) ? null : status.Message,
                result.Items);
        }

        private BattleDiagnosticEventsResult CreateDiagnosticResult(
            BattleDiagnosticEventsQuery query,
            string status,
            BattleDiagnosticDataAvailability availability,
            long storeRevision,
            bool hasMore,
            string? message,
            IReadOnlyList<BattleDiagnosticEvent> items)
        {
            var store = _battleWorld?.Services.TryResolve<IBattleDiagnosticEventReadStore>(out var resolvedStore) == true
                ? resolvedStore
                : null;
            var scope = store?.Scope ?? default;
            var sessionInfo = _battleWorld?.Services.TryResolve<IBattleDiagnosticReadOnlySession>(out var session) == true && session != null
                ? session.SessionInfo
                : default;
            var events = items.Select(static item => new BattleDiagnosticEventRecord(
                item.Frame,
                item.Sequence,
                item.MonotonicTimestamp,
                item.Kind.ToString(),
                item.Channel.ToString(),
                item.Outcome.ToString(),
                item.SourceActorId,
                item.TargetActorId,
                item.ConfigId,
                item.SkillRuntime.RuntimeId,
                item.RootContextId,
                item.ContextId,
                item.Summary,
                item.SkillRuntime.Generation,
                item.ContextId,
                item.RootContextId,
                0,
                0,
                0)).ToArray();
            return new BattleDiagnosticEventsResult(
                status,
                availability.ToString(),
                storeRevision,
                hasMore,
                BattleDiagnosticContractConstants.SchemaVersion,
                sessionInfo.MonotonicTimestampFrequency,
                scope.SessionId,
                scope.WorldId,
                scope.WorldEpoch,
                message,
                events,
                Math.Max(0, query.Offset),
                query.Limit <= 0 ? 100 : Math.Min(query.Limit, 500));
        }

        public BattleWorldDiagnostics? GetWorldDiagnostics(ulong worldId, int frame)
        {
            var diagnostics = new BattleWorldDiagnostics
            {
                BattleId = _battleId,
                WorldId = _worldId,
                Frame = frame,
                ServerNowTicks = DateTime.UtcNow.Ticks,
            };

            if (_checkpointCoordinator != null)
            {
                diagnostics.StateHash = _checkpointCoordinator.ComputeStateHash(new FrameIndex(frame));
            }
            else
            {
                // 无检查点协调器时使用帧号近似值（与 BattleLogicHostGrain 回退逻辑一致）。
                diagnostics.StateHash = (uint)frame;
            }

            return diagnostics;
        }

        public StateSyncPush CreateStateSyncPush(ulong worldId, int frame, bool isFullSnapshot)
            => CaptureStateSyncProjection(worldId, frame, isFullSnapshot, trackLegacyBaseline: true);

        public StateSyncPush CaptureFixedBaselineProjection(ulong worldId, int frame)
            => CaptureStateSyncProjection(worldId, frame, isFullSnapshot: true, trackLegacyBaseline: false);

        private StateSyncPush CaptureStateSyncProjection(ulong worldId, int frame, bool isFullSnapshot, bool trackLegacyBaseline)
        {
            ulong wId = worldId == 0 ? _worldId : worldId;

            // 优先走标准投影路径：与预测通道共享 MobaActorProjectionProducer 的字段提取逻辑，
            // 快照携带完整的 Position/Rotation/Velocity/Hp/TeamId，而非仅 X/Y/Z。
            // Delta 帧时携带上帧数据做实体级增量（跳过不变的 actor）。
            StateSyncPush push;
            if (_battleWorld?.Services != null
                && _battleWorld.Services.TryResolve<IActorProjectionProducer>(out var producer)
                && producer != null)
            {
                _projectionBuffer ??= new List<ActorProjectionData>(64);
                if (trackLegacyBaseline)
                    _lastProjectionData ??= new Dictionary<int, ActorProjectionData>(64);
                push = _protocolMapper.CreateStateSyncPushFromProjection(
                    wId, frame, producer, isFullSnapshot, _projectionBuffer,
                    trackLegacyBaseline ? _lastProjectionData : null);
            }
            else
            {
                // 回退到旧路径（WorldStateSnapshot）
                var frameIndex = new FrameIndex(frame);
                WorldStateSnapshot snapshot = default;
                var hasSnapshot = _runtimePort?.TryGetSnapshot(frameIndex, out snapshot) == true;
                if (!hasSnapshot && _snapshotProvider != null)
                    hasSnapshot = _snapshotProvider.TryGetSnapshot(frameIndex, out snapshot);

                push = _protocolMapper.CreateStateSyncPush(
                    wId, frame, hasSnapshot ? snapshot : null,
                    _runtimePort?.GetDiagnosticEntityStates(), isFullSnapshot);
            }

            if (_battleWorld?.Services == null)
                return push;

            if (_battleWorld.Services.TryResolve<MobaActorRegistry>(out var registry) && registry != null)
            {
                _battleWorld.Services.TryResolve<AbilityKit.HFSM.Definition.StateMachineDefinition>(out var definition);
                MobaCharacterHfsmPushPayload.Attach(push, registry, definition, frame);
            }

            if (_battleWorld.Services.TryResolve<MobaActorIndicatorSnapshotService>(out var indicators) &&
                _battleWorld.Services.TryResolve<MobaShieldStateSnapshotService>(out var shields) &&
                indicators.TryGetActiveBaseline(out var indicatorSnapshot) && shields.TryGetActiveBaseline(out var shieldSnapshot))
            {
                push.Payload = AbilityKit.Protocol.Moba.StateSync.MobaStateSyncOverheadPayloadCodec.Encode(
                    push.PayloadOpCode, push.SchemaVersion, push.Payload, indicatorSnapshot.Payload, shieldSnapshot.Payload);
                push.PayloadOpCode = AbilityKit.Protocol.Moba.StateSync.MobaStateSyncOverheadPayloadCodec.OpCode;
                push.SchemaVersion = AbilityKit.Protocol.Moba.StateSync.MobaStateSyncOverheadPayloadCodec.SchemaVersion;
            }

            if (_battleWorld.Services.TryResolve<MobaPresentationCueSnapshotService>(out var cueService) &&
                cueService != null && cueService.TryCaptureStateSyncBaseline(new FrameIndex(frame), out var cues))
            {
                push.Payload = AbilityKit.Protocol.Moba.StateSync.MobaStateSyncCuePayloadCodec.Encode(
                    push.PayloadOpCode, push.SchemaVersion, push.Payload, cues.Payload);
                push.PayloadOpCode = AbilityKit.Protocol.Moba.StateSync.MobaStateSyncCuePayloadCodec.OpCode;
                push.SchemaVersion = AbilityKit.Protocol.Moba.StateSync.MobaStateSyncCuePayloadCodec.CarrierSchemaVersion;
            }
            return push;
        }

        public void Dispose()
        {
            _bots.Clear();
            _worldManager.DestroyBattleWorld(_battleId);
            _battleWorld = null;
            _snapshotProvider = null;
            _checkpointCoordinator = null;
            _runtimePort = null;
        }

        private static bool TrySpawnBotPlayer(MobaGameplayService gameplay, PlayerInitInfo player)
        {
            try
            {
                /*
                 * TODO(v0.2.0)：通过 MOBA 世界实体工厂实际生成机器人玩家实体。当前方法仅检查玩法是否运行，没有创建实体，因此机器人 AI 输入生成（GenerateBotCommand）的命令没有接收对象。生成应参照 IMobaBattleRuntimePort 对人类玩家的处理：用 PlayerInitInfo 中正确的 HeroId、TeamId、SpawnPointId 及 AttributeTemplateId 创建角色实体，再向 MobaActorRegistry 注册。
                 */
                return gameplay.IsRunning;
            }
            catch
            {
                return false;
            }
        }

        private PlayerInputCommand? GenerateBotCommand(uint playerId, int frame, MobaBotState state)
        {
            // 每个 Bot 每 30 帧更换一次随机移动方向，其余帧保持原方向
            if (frame - state.LastDirectionChangeFrame >= 30)
            {
                state.MoveX = (float)((_botRandom.NextDouble() * 2.0) - 1.0);
                state.MoveY = (float)((_botRandom.NextDouble() * 2.0) - 1.0);
                state.LastDirectionChangeFrame = frame;

                // 10% 概率释放技能
                state.UseSkill = _botRandom.NextDouble() < 0.1;
                state.SkillId = state.UseSkill ? (1 + _botRandom.Next(4)) : 0;
            }
            else
            {
                state.UseSkill = false;
                state.SkillId = 0;
            }

            // 构造简单的移动+技能输入命令
            var payload = new byte[20]; // 4 floats: moveX, moveY, aimX, aimY + 1 int: skillId
            System.BitConverter.GetBytes(state.MoveX).CopyTo(payload, 0);
            System.BitConverter.GetBytes(state.MoveY).CopyTo(payload, 4);
            System.BitConverter.GetBytes(state.MoveX).CopyTo(payload, 8);  // aim = move direction
            System.BitConverter.GetBytes(state.MoveY).CopyTo(payload, 12);
            System.BitConverter.GetBytes(state.SkillId).CopyTo(payload, 16);

            return new PlayerInputCommand(
                new FrameIndex(frame),
                new PlayerId(playerId.ToString()),
                1, // OpCode=1: move+skill
                payload);
        }

        private sealed class MobaBotState
        {
            public readonly uint PlayerId;
            public float MoveX;
            public float MoveY;
            public bool UseSkill;
            public int SkillId;
            public int LastDirectionChangeFrame;

            public MobaBotState(uint playerId)
            {
                PlayerId = playerId;
            }
        }
    }
}
