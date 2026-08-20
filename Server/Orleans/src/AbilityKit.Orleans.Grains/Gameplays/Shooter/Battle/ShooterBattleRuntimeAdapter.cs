using AbilityKit.Ability.StateSync.Aoi;
using AbilityKit.Demo.Shooter;
using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Protocol.Serialization;
using AbilityKit.Protocol.Shooter;
using AbilityKit.Orleans.Grains.Battle;
using AbilityKit.Orleans.Grains.Battle.Gameplay;
using IWorld = AbilityKit.Ability.World.Abstractions.IWorld;

namespace AbilityKit.Orleans.Grains.Gameplays.Shooter.Battle;

internal sealed class ShooterBattleRuntimeAdapter : IBattleRuntimeAdapter
{
    private readonly ServerBattleWorldManager _worldManager;
    private readonly ShooterStateSyncPushOptions? _stateSyncPushOptionsOverride;

    public ShooterBattleRuntimeAdapter(ServerBattleWorldManager worldManager)
        : this(worldManager, null)
    {
    }

    internal ShooterBattleRuntimeAdapter(ServerBattleWorldManager worldManager, ShooterStateSyncPushOptions? stateSyncPushOptions)
    {
        _worldManager = worldManager ?? throw new ArgumentNullException(nameof(worldManager));
        _stateSyncPushOptionsOverride = stateSyncPushOptions;
    }

    public string RoomType => ShooterGameplay.RoomType;

    public IBattleRuntimeSession CreateSession(string battleId)
    {
        return new ShooterBattleRuntimeSession(battleId, _worldManager, _stateSyncPushOptionsOverride);
    }

    internal static ShooterStateSnapshotPayload WithoutLegacyEvents(ShooterStateSnapshotPayload snapshot)
    {
        snapshot.Events = Array.Empty<ShooterEventSnapshot>();
        return snapshot;
    }

    private sealed class ShooterBattleRuntimeSession : IBattleRuntimeSession, IObserverAwareBattleRuntimeSession, IReliableBattleEventProducer
    {
        private readonly string _battleId;
        private readonly ServerBattleWorldManager _worldManager;
        private readonly ShooterStateSyncPushOptions? _stateSyncPushOptionsOverride;
        private ShooterStateSyncPushOptions _stateSyncPushOptions = ShooterStateSyncPushOptions.PackedDefault;
        private IWorld? _battleWorld;
        private IShooterBattleRuntimePort? _runtime;
        private ShooterBattleDriverHost? _driverHost;
        private ulong _worldId;
        private int _lastPureStateBaselineFrame;
        private uint _lastPureStateBaselineHash;
        private readonly Dictionary<string, ShooterObserverPureStateSyncState> _observerPureStateSyncStates = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _playerIdsByAccountId = new(StringComparer.Ordinal);

        public ShooterBattleRuntimeSession(
            string battleId,
            ServerBattleWorldManager worldManager,
            ShooterStateSyncPushOptions? stateSyncPushOptions)
        {
            _battleId = battleId ?? string.Empty;
            _worldManager = worldManager ?? throw new ArgumentNullException(nameof(worldManager));
            _stateSyncPushOptionsOverride = stateSyncPushOptions;
        }

        public BattleRuntimeStartResult Start(BattleInitParams initParams)
        {
            if (initParams is null)
            {
                return BattleRuntimeStartResult.Fail("Battle init params are missing.");
            }

            _stateSyncPushOptions = ResolveStateSyncPushOptions(initParams);

            _worldId = initParams.WorldId;
            _battleWorld = _worldManager.CreateBattleWorld(
                _battleId,
                ShooterGameplay.WorldType,
                initParams.TickRate,
                options => ConfigureShooterWorldOptions(options, initParams));
            if (_battleWorld == null)
            {
                return BattleRuntimeStartResult.Fail("Shooter battle world creation returned null.");
            }

            if (!_battleWorld.Services.TryResolve<IShooterBattleRuntimePort>(out _runtime) || _runtime == null)
            {
                return BattleRuntimeStartResult.Fail("IShooterBattleRuntimePort not resolved from Shooter logic world.");
            }

            var tickRate = initParams.TickRate > 0 ? initParams.TickRate : ShooterGameplay.DefaultTickRate;
            RebuildPlayerAccountIndex(initParams.Players);
            var players = BuildStartPlayers(initParams.Players);
            var anchor = initParams.WorldStartAnchor;
            var start = new ShooterStartGamePayload(
                _battleId,
                tickRate,
                initParams.RandomSeed,
                players,
                _worldId,
                anchor?.StartServerTicks ?? 0L,
                anchor?.ServerTickFrequency ?? 0L,
                anchor?.StartFrame ?? 0,
                anchor?.FixedDeltaSeconds ?? 0d);

            if (!_runtime.StartGame(in start))
            {
                return BattleRuntimeStartResult.Fail("Shooter runtime rejected start spec.");
            }

            _driverHost = new ShooterBattleDriverHost(_runtime);
            _driverHost.Start();
            return BattleRuntimeStartResult.Success();
        }

        private ShooterStateSyncPushOptions ResolveStateSyncPushOptions(BattleInitParams initParams)
        {
            if (_stateSyncPushOptionsOverride != null)
            {
                return _stateSyncPushOptionsOverride;
            }

            if (ShooterStateSyncPushOptions.TryFromEnvironment(out var environmentOverride))
            {
                return environmentOverride;
            }

            var policy = ShooterServerSyncTemplateCatalog.Resolve(initParams.SyncOptions?.SyncTemplateId);
            return policy.CreatePushOptions(initParams.SyncOptions?.NetworkEnvironmentId);
        }

        private static void ConfigureShooterWorldOptions(
            AbilityKit.Ability.World.Abstractions.WorldCreateOptions options,
            BattleInitParams initParams)
        {
            if (initParams.EnemyBudget > 0)
            {
                options.Extensions[typeof(ShooterEnemyBudgetOverride)] =
                    new ShooterEnemyBudgetOverride(initParams.EnemyBudget);
            }

            if (initParams.DurationFrames > 0 ||
                initParams.VictoryTargetDefeats > 0 ||
                initParams.ContinueAfterAllPlayersDefeated)
            {
                options.Extensions[typeof(ShooterBattleFlowOverrides)] = new ShooterBattleFlowOverrides(
                    initParams.DurationFrames,
                    initParams.VictoryTargetDefeats,
                    initParams.ContinueAfterAllPlayersDefeated);
            }
        }

        public BattlePlayerJoinResult JoinPlayer(BattlePlayerJoinRequest request, int currentFrame)
        {
            if (_runtime == null)
            {
                return new BattlePlayerJoinResult(false, request?.Player?.PlayerId ?? 0u, currentFrame, "RejectedRuntimeNotReady", "Shooter runtime is not ready.");
            }

            if (request?.Player == null)
            {
                return new BattlePlayerJoinResult(false, 0u, currentFrame, BattleResultStatusCodes.RejectedNullPlayer, "Player init info is required.");
            }

            var playerId = request.Player.PlayerId == 0 ? 1 : (int)request.Player.PlayerId;
            if (playerId <= 0)
            {
                return new BattlePlayerJoinResult(false, 0u, currentFrame, "RejectedInvalidPlayerId", "Player id must be positive.");
            }

            if (_runtime.TryGetPlayer(playerId, out _))
            {
                return new BattlePlayerJoinResult(true, (uint)playerId, currentFrame, "AlreadyJoined", "Player already exists in Shooter runtime.");
            }

            var player = new ShooterSveltoPlayerComponent
            {
                PlayerId = playerId,
                X = request.Player.PosX,
                Y = request.Player.PosZ,
                AimX = 1f,
                AimY = 0f,
                Hp = ShooterGameplay.DefaultPlayerHp,
                Score = 0,
                Alive = true
            };

            RegisterPlayerAccount(request.Player);
            _runtime.SetPlayer(in player);
            return _runtime.TryGetPlayer(playerId, out _)
                ? new BattlePlayerJoinResult(true, (uint)playerId, currentFrame, "Joined", "Player joined Shooter runtime.")
                : new BattlePlayerJoinResult(false, (uint)playerId, currentFrame, "RejectedRuntimeAddFailed", "Shooter runtime did not retain joined player.");
        }

        public BattleBotAiMountResult MountBotAi(BattleBotAiMountRequest request, int currentFrame)
        {
            if (_runtime == null)
            {
                return new BattleBotAiMountResult(false, request?.PlayerId ?? 0u, currentFrame, "RejectedRuntimeNotReady", "Shooter runtime is not ready.");
            }

            if (request == null || request.PlayerId == 0)
            {
                return new BattleBotAiMountResult(false, request?.PlayerId ?? 0u, currentFrame, "RejectedInvalidPlayerId", "Player id must be positive.");
            }

            var playerId = (int)request.PlayerId;
            if (!_runtime.TryGetPlayer(playerId, out _))
            {
                return new BattleBotAiMountResult(false, request.PlayerId, currentFrame, "RejectedPlayerMissing", "Player does not exist in Shooter runtime.");
            }

            var mounted = _runtime.MountBotAi(new ShooterBotAiMountOptions(playerId, ShooterBotAiProfile.SimpleBattle, request.ProfileId));
            return mounted
                ? new BattleBotAiMountResult(true, request.PlayerId, currentFrame, "Mounted", "Shooter bot AI mounted.")
                : new BattleBotAiMountResult(false, request.PlayerId, currentFrame, "RejectedMountFailed", "Shooter runtime rejected bot AI mount.");
        }

        public BattleInputValidationResult ValidateInput(BattleInputItem input)
        {
            if (input.OpCode != ShooterOpCodes.Input.PlayerCommand)
            {
                return BattleInputValidationResult.Reject(
                    BattleResultStatusCodes.RejectedInvalidOpCode,
                    $"Shooter input opcode {input.OpCode} is not supported.");
            }

            try
            {
                var commands = ShooterInputCodec.Deserialize(input.Payload ?? Array.Empty<byte>());
                if (commands.Length != 1)
                {
                    return BattleInputValidationResult.Reject(
                        BattleResultStatusCodes.RejectedInvalidPayload,
                        "Shooter input must contain exactly one player command.");
                }

                var command = commands[0];
                if (command.PlayerId != (int)input.PlayerId
                    || !float.IsFinite(command.MoveX)
                    || !float.IsFinite(command.MoveY)
                    || !float.IsFinite(command.AimX)
                    || !float.IsFinite(command.AimY))
                {
                    return BattleInputValidationResult.Reject(
                        BattleResultStatusCodes.RejectedInvalidPayload,
                        "Shooter player command does not match the submitting player or contains non-finite values.");
                }

                return BattleInputValidationResult.Valid;
            }
            catch (Exception)
            {
                return BattleInputValidationResult.Reject(
                    BattleResultStatusCodes.RejectedInvalidPayload,
                    "Shooter input payload could not be decoded.");
            }
        }

        public int SubmitInputs(int frame, IReadOnlyList<BattleInputItem> inputs)
        {
            if (inputs == null || inputs.Count == 0 || _driverHost == null)
            {
                return 0;
            }

            var commands = new List<ShooterPlayerCommand>(inputs.Count);
            for (int i = 0; i < inputs.Count; i++)
            {
                var input = inputs[i];
                if (input == null) continue;
                commands.AddRange(ShooterInputCodec.Deserialize(input.Payload ?? Array.Empty<byte>()));
            }

            return _driverHost.SubmitCommands(frame, commands);
        }

        public bool Tick(int frame, int tickRate, float deltaTime)
        {
            if (_battleWorld == null || _driverHost == null)
            {
                return false;
            }

            _driverHost.AdvanceFrame(deltaTime);
            return _driverHost.CurrentFrame >= frame;
        }

        public IReadOnlyList<ReliableBattleEventSource> CaptureReliableEvents(int frame)
        {
            var events = _runtime?.GetSnapshot().Events;
            if (events == null || events.Length == 0)
            {
                return Array.Empty<ReliableBattleEventSource>();
            }

            var result = new ReliableBattleEventSource[events.Length];
            for (int i = 0; i < events.Length; i++)
            {
                var battleEvent = events[i];
                result[i] = new ReliableBattleEventSource(
                    frame,
                    battleEvent.EventType,
                    ShooterStateSnapshotCodec.SerializeEvent(in battleEvent));
            }

            return result;
        }

        public BattleSnapshot? GetSnapshot(int frame)
        {
            if (_runtime == null)
            {
                return null;
            }

            var snapshot = _runtime.GetSnapshot();
            var matchResult = _runtime.MatchResult;
            return new BattleSnapshot
            {
                Frame = snapshot.Frame,
                Actors = CreateActorSnapshots(in snapshot),
                MatchState = snapshot.MatchState,
                MatchFinal = matchResult.IsFinal,
                MatchVictory = matchResult.IsVictory,
                MatchCompletedFrame = matchResult.CompletedFrame,
                DefeatedEnemies = matchResult.DefeatedEnemies,
                VictoryTargetDefeats = matchResult.VictoryTargetDefeats,
                TimeLimitFrames = snapshot.TimeLimitFrames,
                RemainingTimeFrames = snapshot.RemainingTimeFrames
            };
        }

        public BattleWorldDiagnostics? GetWorldDiagnostics(ulong worldId, int frame)
        {
            if (_runtime == null)
            {
                return null;
            }

            var resolvedWorldId = worldId == 0 ? _worldId : worldId;
            var packed = _runtime.ExportPackedSnapshot(resolvedWorldId, isFullSnapshot: true, authorityOverride: true);
            var entities = new Dictionary<string, BattleWorldEntityDiagnostics>(StringComparer.Ordinal);
            var chunks = new List<BattleWorldComponentChunkDiagnostics>();
            foreach (var chunk in packed.ComponentChunks ?? Array.Empty<ShooterPackedComponentChunk>())
            {
                chunks.Add(new BattleWorldComponentChunkDiagnostics
                {
                    ComponentKind = DescribeComponentKind(chunk.ComponentKind),
                    EntityKind = DescribeEntityKind(chunk.EntityKind),
                    Count = chunk.Count
                });

                ApplyChunk(chunk, entities);
            }

            return new BattleWorldDiagnostics
            {
                BattleId = _battleId,
                WorldType = ShooterGameplay.WorldType,
                WorldId = packed.WorldId,
                Frame = packed.Frame,
                StateHash = packed.StateHash,
                EntityCount = packed.EntityCount,
                Entities = entities.Values
                    .OrderBy(entity => entity.EntityKind, StringComparer.Ordinal)
                    .ThenBy(entity => entity.EntityId)
                    .ToList(),
                ComponentChunks = chunks,
                ServerNowTicks = DateTime.UtcNow.Ticks
            };
        }

        private static void ApplyChunk(in ShooterPackedComponentChunk chunk, Dictionary<string, BattleWorldEntityDiagnostics> entities)
        {
            if (chunk.ComponentKind == ShooterPackedComponentKinds.RuntimeMetadata)
            {
                return;
            }

            var count = Math.Max(0, chunk.Count);
            for (int i = 0; i < count; i++)
            {
                var entityId = GetInt(chunk.EntityIds, i);
                if (entityId <= 0)
                {
                    continue;
                }

                var entity = GetOrCreateEntity(entities, chunk.EntityKind, entityId);
                var fields = BuildComponentFields(chunk, i);
                if (chunk.ComponentKind == ShooterPackedComponentKinds.EntityLifecycle)
                {
                    entity.Alive = IsAlive(GetByte(chunk.Flags, i));
                    if (fields.TryGetValue("ownerId", out var ownerId) && !string.IsNullOrWhiteSpace(ownerId))
                    {
                        entity.Label = $"{entity.EntityKind} {entity.EntityId} / owner {ownerId}";
                    }
                }

                entity.Components.Add(new BattleWorldComponentDiagnostics
                {
                    Name = DescribeComponentName(chunk.ComponentKind, chunk.EntityKind),
                    ComponentKind = DescribeComponentKind(chunk.ComponentKind),
                    Fields = fields
                });
            }
        }

        private static BattleWorldEntityDiagnostics GetOrCreateEntity(Dictionary<string, BattleWorldEntityDiagnostics> entities, int entityKind, int entityId)
        {
            var kind = DescribeEntityKind(entityKind);
            var key = $"{kind}:{entityId}";
            if (entities.TryGetValue(key, out var entity))
            {
                return entity;
            }

            entity = new BattleWorldEntityDiagnostics
            {
                Key = key,
                EntityId = entityId,
                EntityKind = kind,
                Group = ResolveEntityGroup(entityKind),
                Label = $"{kind} {entityId}"
            };
            entities[key] = entity;
            return entity;
        }

        private static Dictionary<string, string> BuildComponentFields(in ShooterPackedComponentChunk chunk, int index)
        {
            var fields = new Dictionary<string, string>(StringComparer.Ordinal);
            switch (chunk.ComponentKind)
            {
                case ShooterPackedComponentKinds.EntityLifecycle:
                    fields["alive"] = IsAlive(GetByte(chunk.Flags, index)).ToString();
                    fields["flags"] = GetByte(chunk.Flags, index).ToString();
                    AddIfPresent(fields, "ownerId", chunk.OwnerIds, index);
                    break;
                case ShooterPackedComponentKinds.Transform:
                    fields["x"] = FormatFloat(GetFloat(chunk.ValueX, index));
                    fields["y"] = FormatFloat(GetFloat(chunk.ValueY, index));
                    fields["directionX"] = FormatFloat(GetFloat(chunk.ValueZ, index));
                    fields["directionY"] = FormatFloat(GetFloat(chunk.ValueW, index));
                    if (chunk.Aux != null && chunk.Aux.Length >= (index + 1) * 2)
                    {
                        fields["velocityX"] = FormatFloat(BitConverter.Int32BitsToSingle(chunk.Aux[index * 2]));
                        fields["velocityY"] = FormatFloat(BitConverter.Int32BitsToSingle(chunk.Aux[index * 2 + 1]));
                    }
                    break;
                case ShooterPackedComponentKinds.Health:
                    fields["current"] = GetInt(chunk.IntValues, index).ToString();
                    AddIfPresent(fields, "max", chunk.Aux, index);
                    break;
                case ShooterPackedComponentKinds.Score:
                    fields["score"] = GetInt(chunk.IntValues, index).ToString();
                    break;
                case ShooterPackedComponentKinds.ProjectileLifetime:
                    fields["remainingFrames"] = GetInt(chunk.IntValues, index).ToString();
                    break;
                default:
                    fields["componentKind"] = chunk.ComponentKind.ToString();
                    break;
            }

            return fields;
        }

        private static void AddIfPresent(Dictionary<string, string> fields, string key, int[]? values, int index)
        {
            if (values != null && index >= 0 && index < values.Length)
            {
                fields[key] = values[index].ToString();
            }
        }

        private static string DescribeComponentName(int componentKind, int entityKind)
        {
            return componentKind switch
            {
                ShooterPackedComponentKinds.EntityLifecycle => $"{DescribeEntityKind(entityKind)}LifecycleComponent",
                ShooterPackedComponentKinds.Transform => "ShooterSveltoTransformComponent",
                ShooterPackedComponentKinds.Health => "ShooterSveltoHealthComponent",
                ShooterPackedComponentKinds.Score => "ShooterSveltoScoreComponent",
                ShooterPackedComponentKinds.ProjectileLifetime => "ShooterSveltoProjectileLifetimeComponent",
                _ => $"Component{componentKind}"
            };
        }

        private static string DescribeComponentKind(int componentKind)
        {
            return componentKind switch
            {
                ShooterPackedComponentKinds.EntityLifecycle => "EntityLifecycle",
                ShooterPackedComponentKinds.Transform => "Transform",
                ShooterPackedComponentKinds.Health => "Health",
                ShooterPackedComponentKinds.Score => "Score",
                ShooterPackedComponentKinds.ProjectileLifetime => "ProjectileLifetime",
                ShooterPackedComponentKinds.RuntimeMetadata => "RuntimeMetadata",
                _ => $"Component{componentKind}"
            };
        }

        private static string DescribeEntityKind(int entityKind)
        {
            return entityKind switch
            {
                ShooterPackedEntityKinds.Player => "Player",
                ShooterPackedEntityKinds.Projectile => "Projectile",
                ShooterPackedEntityKinds.Enemy => "Enemy",
                _ => entityKind == 0 ? "World" : $"Entity{entityKind}"
            };
        }

        private static string ResolveEntityGroup(int entityKind)
        {
            return entityKind switch
            {
                ShooterPackedEntityKinds.Player => "ShooterSveltoGroups.Players",
                ShooterPackedEntityKinds.Projectile => "ShooterSveltoGroups.Projectiles",
                ShooterPackedEntityKinds.Enemy => "ShooterSveltoGroups.GameplayTargets",
                _ => "Unknown"
            };
        }

        private static int GetInt(int[]? values, int index) => values != null && index >= 0 && index < values.Length ? values[index] : 0;

        private static float GetFloat(float[]? values, int index) => values != null && index >= 0 && index < values.Length ? values[index] : 0f;

        private static byte GetByte(byte[]? values, int index) => values != null && index >= 0 && index < values.Length ? values[index] : (byte)0;

        private static bool IsAlive(byte flags) => (flags & ShooterPackedEntityFlags.Alive) != 0;

        private static string FormatFloat(float value) => value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

        public StateSyncPush CreateStateSyncPush(ulong worldId, int frame, bool isFullSnapshot)
        {
            return CreateStateSyncPushCore(worldId, frame, isFullSnapshot, Array.Empty<ShooterCommandAcknowledgement>());
        }

        private StateSyncPush CreateStateSyncPushCore(
            ulong worldId,
            int frame,
            bool isFullSnapshot,
            ShooterCommandAcknowledgement[] acknowledgements)
        {
            var resolvedWorldId = worldId == 0 ? _worldId : worldId;
            if (_stateSyncPushOptions.PayloadMode == ShooterStateSyncPushPayloadMode.PureState)
            {
                return CreatePureStateSyncPush(resolvedWorldId, frame, isFullSnapshot, acknowledgements);
            }

            // Packed 路径：Actors 列表直接从 Packed 玩家 chunk 构建，
            // 不再调用 GetSnapshot() 做全量 StateSnapshot 导出
            // （高单位量优化：消除每 push 3 个大数组 + 事件数组的重复分配）。
            var packed = _runtime?.ExportPackedSnapshot(resolvedWorldId, isFullSnapshot, authorityOverride: isFullSnapshot) ?? default;
            packed.AcknowledgedCommands = acknowledgements;
            return new StateSyncPush
            {
                WorldId = resolvedWorldId,
                Frame = packed.Frame,
                Timestamp = DateTime.UtcNow.Ticks,
                Actors = CreateActorSnapshotsFromPacked(in packed),
                IsFullSnapshot = isFullSnapshot,
                PayloadOpCode = isFullSnapshot ? ShooterOpCodes.Snapshot.PackedState : ShooterOpCodes.Snapshot.PackedStateDelta,
                Payload = ShooterPackedSnapshotCodec.Serialize(in packed)
            };
        }

        public StateSyncPush CreateStateSyncPush(ulong worldId, int frame, bool isFullSnapshot, in BattleStateSyncObserverContext observerContext)
        {
            if (_stateSyncPushOptions.PayloadMode != ShooterStateSyncPushPayloadMode.PureState
                || !_stateSyncPushOptions.UseObserverAoi)
            {
                return CreateStateSyncPushCore(
                    worldId,
                    frame,
                    isFullSnapshot,
                    observerContext.AcknowledgedCommands ?? Array.Empty<ShooterCommandAcknowledgement>());
            }

            var resolvedWorldId = worldId == 0 ? _worldId : worldId;
            var observerKey = ResolveObserverKey(in observerContext);
            var syncState = GetObserverPureStateSyncState(observerKey);
            if (!TryCreateObserverInterestScope(in observerContext, out var interestScope))
            {
                return CreateFailClosedPureStateSyncPush(
                    resolvedWorldId,
                    frame,
                    syncState,
                    observerContext.AcknowledgedCommands ?? Array.Empty<ShooterCommandAcknowledgement>());
            }

            var requiresFullSnapshot = isFullSnapshot || syncState.RequiresFullSnapshot;
            return CreatePureStateSyncPush(
                resolvedWorldId,
                frame,
                requiresFullSnapshot,
                interestScope,
                syncState,
                observerContext.AcknowledgedCommands ?? Array.Empty<ShooterCommandAcknowledgement>());
        }

        private StateSyncPush CreatePureStateSyncPush(
            ulong worldId,
            int fallbackFrame,
            bool isFullSnapshot,
            ShooterCommandAcknowledgement[] acknowledgements)
        {
            var settings = _stateSyncPushOptions.ResolvePureStateSettings();
            var pureState = _runtime?.ExportPureStateSnapshotTransient(
                worldId,
                isFullBaseline: isFullSnapshot,
                settings,
                baselineFrame: isFullSnapshot ? 0 : _lastPureStateBaselineFrame,
                baselineHash: isFullSnapshot ? 0u : _lastPureStateBaselineHash) ?? ShooterPureStateSnapshotPayload.Empty(fallbackFrame);

            if (isFullSnapshot)
            {
                _lastPureStateBaselineFrame = pureState.Frame;
                _lastPureStateBaselineHash = pureState.StateHash;
            }

            return CreatePureStateSyncPush(worldId, isFullSnapshot, in pureState, acknowledgements);
        }

        private StateSyncPush CreateFailClosedPureStateSyncPush(
            ulong worldId,
            int frame,
            ShooterObserverPureStateSyncState syncState,
            ShooterCommandAcknowledgement[] acknowledgements)
        {
            syncState.AoiInterestSet.Clear();
            syncState.BaselineFrame = 0;
            syncState.BaselineHash = 0u;
            syncState.RequiresFullSnapshot = true;

            var settings = _stateSyncPushOptions.ResolvePureStateSettings();
            var pureState = new ShooterPureStateSnapshotPayload(
                ShooterPureStateSyncCodec.CurrentVersion,
                worldId,
                frame,
                frame,
                ShooterPureStateSnapshotKinds.FullBaseline,
                frame,
                0u,
                0u,
                settings,
                Array.Empty<ShooterPureStateEntityDelta>(),
                Array.Empty<ShooterPureStateVisibilityHint>());
            return CreatePureStateSyncPush(
                worldId,
                isFullSnapshot: true,
                in pureState,
                acknowledgements);
        }

        private StateSyncPush CreatePureStateSyncPush(
            ulong worldId,
            int fallbackFrame,
            bool isFullSnapshot,
            ShooterPureStateInterestScope interestScope,
            ShooterObserverPureStateSyncState syncState,
            ShooterCommandAcknowledgement[] acknowledgements)
        {
            var settings = _stateSyncPushOptions.ResolvePureStateSettings();
            var pureState = _runtime?.ExportPureStateSnapshotTransient(
                worldId,
                isFullBaseline: isFullSnapshot,
                settings,
                baselineFrame: isFullSnapshot ? 0 : syncState.BaselineFrame,
                baselineHash: isFullSnapshot ? 0u : syncState.BaselineHash,
                interestScope,
                syncState.AoiInterestSet) ?? ShooterPureStateSnapshotPayload.Empty(fallbackFrame);

            if (isFullSnapshot)
            {
                syncState.BaselineFrame = pureState.Frame;
                syncState.BaselineHash = pureState.StateHash;
                syncState.RequiresFullSnapshot = false;
            }

            return CreatePureStateSyncPush(worldId, isFullSnapshot, in pureState, acknowledgements);
        }

        private static StateSyncPush CreatePureStateSyncPush(
            ulong worldId,
            bool isFullSnapshot,
            in ShooterPureStateSnapshotPayload pureState,
            ShooterCommandAcknowledgement[] acknowledgements)
        {
            var snapshot = pureState;
            snapshot.AcknowledgedCommands = acknowledgements;
            snapshot.SetTransientCounts(
                snapshot.EffectiveEntityCount,
                snapshot.EffectiveVisibilityHintCount,
                acknowledgements.Length);
            return new StateSyncPush
            {
                WorldId = worldId,
                Frame = pureState.Frame,
                Timestamp = DateTime.UtcNow.Ticks,
                Actors = new List<ActorSnapshot>(),
                IsFullSnapshot = isFullSnapshot,
                PayloadOpCode = isFullSnapshot ? ShooterOpCodes.Snapshot.PureState : ShooterOpCodes.Snapshot.PureStateDelta,
                Payload = ShooterPureStateSyncCodec.Serialize(in snapshot)
            };
        }

        private static string ResolveObserverKey(in BattleStateSyncObserverContext observerContext)
        {
            if (!string.IsNullOrWhiteSpace(observerContext.ObserverKey))
            {
                return observerContext.ObserverKey;
            }

            return !string.IsNullOrWhiteSpace(observerContext.AccountId)
                ? $"account:{observerContext.AccountId}"
                : "unresolved-observer";
        }

        private ShooterObserverPureStateSyncState GetObserverPureStateSyncState(string observerKey)
        {
            if (!_observerPureStateSyncStates.TryGetValue(observerKey, out var state))
            {
                state = new ShooterObserverPureStateSyncState();
                _observerPureStateSyncStates[observerKey] = state;
            }

            return state;
        }

        private bool TryCreateObserverInterestScope(in BattleStateSyncObserverContext observerContext, out ShooterPureStateInterestScope interestScope)
        {
            interestScope = default;
            if (_runtime == null || !TryResolveObserverPlayerId(in observerContext, out var playerId))
            {
                return false;
            }

            if (!_runtime.TryGetPlayer(playerId, out var player) || !player.Alive)
            {
                return false;
            }

            interestScope = new ShooterPureStateInterestScope(
                playerId,
                player.X,
                player.Y,
                _stateSyncPushOptions.AoiVisibleRadius,
                _stateSyncPushOptions.AoiBoundaryRadius,
                _stateSyncPushOptions.ResolvePureStateSettings().MaxEntityCount);
            return true;
        }

        private bool TryResolveObserverPlayerId(in BattleStateSyncObserverContext observerContext, out int playerId)
        {
            playerId = 0;
            return !string.IsNullOrWhiteSpace(observerContext.AccountId)
                && _playerIdsByAccountId.TryGetValue(observerContext.AccountId, out playerId);
        }

        private void RebuildPlayerAccountIndex(IReadOnlyList<PlayerInitInfo>? players)
        {
            _playerIdsByAccountId.Clear();
            if (players == null)
            {
                return;
            }

            for (int i = 0; i < players.Count; i++)
            {
                RegisterPlayerAccount(players[i], i + 1);
            }
        }

        private void RegisterPlayerAccount(PlayerInitInfo player, int fallbackPlayerId = 0)
        {
            if (player == null || string.IsNullOrWhiteSpace(player.AccountId))
            {
                return;
            }

            var playerId = player.PlayerId == 0 ? fallbackPlayerId : (int)player.PlayerId;
            if (playerId <= 0)
            {
                return;
            }

            _playerIdsByAccountId[player.AccountId] = playerId;
        }

        public void Dispose()
        {
            _driverHost?.Stop();
            _driverHost = null;
            _worldManager.DestroyBattleWorld(_battleId);
            _battleWorld = null;
            _runtime = null;
            _observerPureStateSyncStates.Clear();
            _playerIdsByAccountId.Clear();
        }

        private static ShooterStartPlayer[] BuildStartPlayers(IReadOnlyList<PlayerInitInfo>? players)
        {
            if (players == null || players.Count == 0)
            {
                return Array.Empty<ShooterStartPlayer>();
            }

            var result = new ShooterStartPlayer[players.Count];
            for (int i = 0; i < players.Count; i++)
            {
                var player = players[i];
                var playerId = player.PlayerId == 0 ? i + 1 : (int)player.PlayerId;
                var spawnX = player.PosX;
                var spawnY = player.PosZ;
                result[i] = new ShooterStartPlayer(
                    playerId,
                    playerId.ToString(),
                    spawnX,
                    spawnY);
            }

            return result;
        }

        private static List<ActorSnapshot> CreateActorSnapshots(in ShooterStateSnapshotPayload snapshot)
        {
            var actors = new List<ActorSnapshot>(snapshot.Players?.Length ?? 0);
            if (snapshot.Players == null)
            {
                return actors;
            }

            for (int i = 0; i < snapshot.Players.Length; i++)
            {
                var player = snapshot.Players[i];
                actors.Add(new ActorSnapshot
                {
                    ActorId = player.PlayerId,
                    X = player.X,
                    Y = 0f,
                    Z = player.Y,
                    Rotation = 0f,
                    VelocityX = 0f,
                    VelocityZ = 0f,
                    Hp = player.Hp,
                    HpMax = ShooterGameplay.DefaultPlayerHp,
                    TeamId = 1
                });
            }

            return actors;
        }

        /// <summary>
        /// 从 Packed 快照的玩家 Transform/Health chunk 直接构建 Actors 列表。
        /// Exporter 对玩家 chunk 共享同一排序索引，Transform 与 Health chunk 同序对齐，
        /// 因此按下标配对；下标越界或 id 不匹配时回退默认 HP。
        /// </summary>
        private static List<ActorSnapshot> CreateActorSnapshotsFromPacked(in ShooterPackedSnapshotPayload packed)
        {
            var chunks = packed.ComponentChunks;
            if (chunks == null || chunks.Length == 0)
            {
                return new List<ActorSnapshot>(0);
            }

            var hasTransform = false;
            var hasHealth = false;
            ShooterPackedComponentChunk transform = default;
            ShooterPackedComponentChunk health = default;
            for (int i = 0; i < chunks.Length; i++)
            {
                if (chunks[i].EntityKind != ShooterPackedEntityKinds.Player) continue;
                if (chunks[i].ComponentKind == ShooterPackedComponentKinds.Transform)
                {
                    transform = chunks[i];
                    hasTransform = true;
                }
                else if (chunks[i].ComponentKind == ShooterPackedComponentKinds.Health)
                {
                    health = chunks[i];
                    hasHealth = true;
                }
            }

            if (!hasTransform || transform.EntityIds == null)
            {
                return new List<ActorSnapshot>(0);
            }

            var count = Math.Max(0, transform.Count);
            var actors = new List<ActorSnapshot>(count);
            for (int i = 0; i < count; i++)
            {
                var playerId = transform.EntityIds[i];
                var hp = ShooterGameplay.DefaultPlayerHp;
                if (hasHealth && health.EntityIds != null && health.IntValues != null
                    && i < health.Count && health.EntityIds[i] == playerId)
                {
                    hp = health.IntValues[i];
                }

                actors.Add(new ActorSnapshot
                {
                    ActorId = playerId,
                    X = GetFloat(transform.ValueX, i),
                    Y = 0f,
                    Z = GetFloat(transform.ValueY, i),
                    Rotation = 0f,
                    VelocityX = 0f,
                    VelocityZ = 0f,
                    Hp = hp,
                    HpMax = ShooterGameplay.DefaultPlayerHp,
                    TeamId = 1
                });
            }

            return actors;
        }

        private sealed class ShooterObserverPureStateSyncState
        {
            public readonly AoiInterestSet AoiInterestSet = new();
            public int BaselineFrame;
            public uint BaselineHash;
            public bool RequiresFullSnapshot;
        }
    }
}

