using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.Moba.Runtime;
using AbilityKit.Ability.World.Services;
using AbilityKit.Demo.Moba.Gameplay;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.StateSync;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Network.Battle.Projection;
using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Orleans.Grains.Battle;
using AbilityKit.Orleans.Grains.Battle.Gameplay;
using AbilityKit.Orleans.Grains.Gameplays.Moba.Protocol;
using IWorld = AbilityKit.Ability.World.Abstractions.IWorld;


namespace AbilityKit.Orleans.Grains.Gameplays.Moba.Battle;

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

    private sealed class MobaBattleRuntimeSession : IBattleRuntimeSession, IBattleRuntimeInputDiagnostics
    {
        private readonly string _battleId;
        private readonly ServerBattleWorldManager _worldManager;
        private readonly IOrleansBattleProtocolMapper _protocolMapper;
        private IWorld? _battleWorld;
        private IWorldStateSnapshotProvider? _snapshotProvider;
        private IMobaBattleRuntimePort? _runtimePort;
        private ulong _worldId;
        private MobaDeterministicCheckpointCoordinator? _checkpointCoordinator;
        private List<ActorProjectionData>? _projectionBuffer;
        private Dictionary<int, ActorProjectionData>? _lastProjectionData;
        private readonly Dictionary<uint, MobaBotState> _bots = new();
        private readonly Random _botRandom = new();

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
                LastInputSubmitDiagnostic = $"Protocol mapper produced no commands. frame={frame}, inputs={inputs.Count}.";
                return 0;
            }

            var result = _runtimePort.Submit(new FrameIndex(frame), commands);
            LastInputSubmitDiagnostic = result.Succeeded ? string.Empty : result.ToString();
            return result.Succeeded ? result.CommandCount : 0;
        }

        public bool Tick(int frame, int tickRate, float deltaTime)
        {
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

            _battleWorld.Tick(deltaTime);
            return true;
        }

        public BattleSnapshot? GetSnapshot(int frame)
        {
            var frameIndex = new FrameIndex(frame);
            if (_snapshotProvider != null && _snapshotProvider.TryGetSnapshot(frameIndex, out var snapshot))
            {
                return _protocolMapper.CreateBattleSnapshot(frame, snapshot, _runtimePort?.GetDiagnosticEntityStates());
            }

            return null;
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
        {
            ulong wId = worldId == 0 ? _worldId : worldId;

            // 优先走标准投影路径：与预测通道共享 MobaActorProjectionProducer 的字段提取逻辑，
            // 快照携带完整的 Position/Rotation/Velocity/Hp/TeamId，而非仅 X/Y/Z。
            // Delta 帧时携带上帧数据做实体级增量（跳过不变的 actor）。
            if (_battleWorld?.Services != null
                && _battleWorld.Services.TryResolve<IActorProjectionProducer>(out var producer)
                && producer != null)
            {
                _projectionBuffer ??= new List<ActorProjectionData>(64);
                _lastProjectionData ??= new Dictionary<int, ActorProjectionData>(64);
                return _protocolMapper.CreateStateSyncPushFromProjection(
                    wId, frame, producer, isFullSnapshot, _projectionBuffer, _lastProjectionData);
            }

            // 回退到旧路径（WorldStateSnapshot）
            var frameIndex = new FrameIndex(frame);
            WorldStateSnapshot snapshot = default;
            var hasSnapshot = _runtimePort?.TryGetSnapshot(frameIndex, out snapshot) == true;
            if (!hasSnapshot && _snapshotProvider != null)
            {
                hasSnapshot = _snapshotProvider.TryGetSnapshot(frameIndex, out snapshot);
            }

            return _protocolMapper.CreateStateSyncPush(
                wId,
                frame,
                hasSnapshot ? snapshot : null,
                _runtimePort?.GetDiagnosticEntityStates(),
                isFullSnapshot);
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
                // TODO(v0.2.0): Actually spawn the bot player entity via the MOBA world's
                // entity factory. Currently this method only checks whether gameplay is running
                // without creating any entity, so Bot AI input generation (GenerateBotCommand)
                // delivers commands into a void. The spawn should mirror what
                // IMobaBattleRuntimePort does for human players: create an actor entity with
                // the correct HeroId, TeamId, SpawnPointId, and AttributeTemplateId from
                // the PlayerInitInfo, then register it with MobaActorRegistry.
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

