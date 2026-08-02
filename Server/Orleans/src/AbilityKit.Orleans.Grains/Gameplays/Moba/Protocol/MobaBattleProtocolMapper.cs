using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.Moba.CreateWorld;
using AbilityKit.Ability.Host.Extensions.Moba.Snapshot;
using AbilityKit.Ability.Host.Extensions.Moba.Struct;
using AbilityKit.Ability.Host.Extensions.Moba.Runtime;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Game.Battle.Transport.Projection;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;
using PlayerId = AbilityKit.Ability.Host.PlayerId;

namespace AbilityKit.Orleans.Grains.Gameplays.Moba.Protocol;

/// <summary>
/// 将 Orleans 战斗契约转换为运行时协议模型，并将运行时快照转换回 Orleans 契约。
/// 该边界保持在 grain 外部，便于房间、网关和玩法特定 host 后续替换映射规则。
/// </summary>
public interface IOrleansBattleProtocolMapper
{
    MobaBattleLaunchSpec CreateLaunchSpec(string battleId, int tickRate, BattleInitParams initParams);

    MobaGameStartSpec CreateGameStartSpec(string battleId, int tickRate, BattleInitParams initParams);

    IReadOnlyList<PlayerInputCommand> CreatePlayerInputCommands(int frame, IReadOnlyList<BattleInputItem>? inputs);

    StateSyncPush CreateStateSyncPush(ulong worldId, int frame, WorldStateSnapshot? snapshot, IReadOnlyList<MobaDiagnosticEntityState>? diagnosticStates, bool isFullSnapshot);

    /// <summary>
    /// 通过标准投影生产方提取 actor 状态，构建 StateSyncPush。
    ///
    /// 与 <see cref="CreateStateSyncPush"/> 的区别：不通过 WorldStateSnapshot payload
    /// 反序列化提取 actor，而是通过 IActorProjectionProducer 从逻辑 world 直接提取。
    /// 这消除了"快照 payload → actorSnapshotMappers → ActorSnapshot"的独立映射路径，
    /// 使网关提取与预测通道提取共享同一份逻辑（MobaActorProjectionProducer）。
    /// </summary>
    /// <param name="worldId">world ID。</param>
    /// <param name="frame">当前帧。</param>
    /// <param name="producer">标准投影生产方（MobaActorProjectionProducer）。</param>
    /// <param name="isFullSnapshot">是否为全量快照。</param>
    /// <param name="buffer">可复用的投影缓冲区（避免每帧分配）。</param>
    /// <param name="lastFrameData">上一帧的投影数据（可选）。Delta 帧时用于实体级差量比较，只输出变更的 actor。</param>
    StateSyncPush CreateStateSyncPushFromProjection(ulong worldId, int frame, IActorProjectionProducer producer, bool isFullSnapshot, List<ActorProjectionData> buffer = null, Dictionary<int, ActorProjectionData>? lastFrameData = null);

    BattleSnapshot CreateBattleSnapshot(int frame, WorldStateSnapshot snapshot, IReadOnlyList<MobaDiagnosticEntityState>? diagnosticStates);
}

/// <summary>
/// 示例服务器使用的默认 MOBA 映射 profile。未来玩法模式可以替换该类，而无需改变 battle host grain 生命周期。
/// </summary>
public sealed class DefaultOrleansBattleProtocolMapper : IOrleansBattleProtocolMapper
{
    public const int CurrentSnapshotSchemaVersion = 1;
    public static readonly DefaultOrleansBattleProtocolMapper Instance = new();

    private DefaultOrleansBattleProtocolMapper()
    {
    }

    private readonly MobaRuntimeSnapshotMapperRegistry<List<ActorSnapshot>> _actorSnapshotMappers =
        MobaRuntimeSnapshotMapperRegistryBuilder.FromMappers<List<ActorSnapshot>>(new ActorTransformSnapshotMapper());

    public MobaBattleLaunchSpec CreateLaunchSpec(string battleId, int tickRate, BattleInitParams initParams)
    {
        if (initParams == null)
        {
            throw new ArgumentNullException(nameof(initParams));
        }

        var loadouts = BuildLoadouts(initParams.Players);
        var localPlayerId = loadouts[0].PlayerId;
        var worldType = string.IsNullOrWhiteSpace(initParams.WorldType) ? GameplayRoomTypes.Default : initParams.WorldType;
        var clientId = string.IsNullOrWhiteSpace(initParams.ClientId) ? "orleans_logic_host" : initParams.ClientId;
        var profile = MobaBattleLaunchProfile.Create(
            clientId: clientId,
            launchMode: MobaBattleLaunchMode.RoomFlow,
            syncMode: MobaBattleLaunchSyncMode.StateSync,
            authorityMode: MobaBattleLaunchAuthorityMode.ServerAuthority,
            worldType: worldType,
            tickRate: tickRate,
            inputDelayFrames: initParams.InputDelayFrames,
            ruleSetId: initParams.RuleSetId,
            configVersion: initParams.ConfigVersion,
            protocolVersion: initParams.ProtocolVersion);

        return MobaBattleLaunchSpecBuilder.FromLoadouts(
            battleId: battleId,
            localPlayerId: localPlayerId,
            mapId: initParams.MapId > 0 ? initParams.MapId : 1,
            players: loadouts,
            profile: in profile,
            matchId: battleId,
            worldId: battleId,
            gameplayId: initParams.GameplayId,
            randomSeed: initParams.RandomSeed);

    }

    public MobaGameStartSpec CreateGameStartSpec(string battleId, int tickRate, BattleInitParams initParams)
    {
        var launchSpec = CreateLaunchSpec(battleId, tickRate, initParams);
        var startSpec = launchSpec.ToGameStartSpec();
        var validation = MobaProtocolValidation.ValidateEnterGameReq(in startSpec.EnterReq);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException("Invalid Orleans MOBA game start spec. " + validation);
        }

        return startSpec;
    }

    private static MobaPlayerLoadout[] BuildLoadouts(IReadOnlyList<PlayerInitInfo>? players)
    {
        if (players == null || players.Count == 0)
        {
            throw new InvalidOperationException("MOBA battle initialization requires at least one player loadout.");
        }

        var loadouts = new MobaPlayerLoadout[players.Count];
        for (int i = 0; i < players.Count; i++)
        {
            var player = players[i] ?? throw new InvalidOperationException($"MOBA player init info is null at index {i}.");
            var skillIds = player.SkillIds == null || player.SkillIds.Count == 0
                ? null
                : player.SkillIds.ToArray();

            loadouts[i] = new MobaPlayerLoadout(
                playerId: new PlayerId(player.PlayerId.ToString()),
                teamId: player.TeamId,
                heroId: player.HeroId,
                attributeTemplateId: player.AttributeTemplateId,
                level: player.Level,
                basicAttackSkillId: player.BasicAttackSkillId,
                skillIds: skillIds,
                spawnIndex: player.SpawnIndex >= 0 ? player.SpawnIndex : i,
                unitSubType: 1,
                mainType: 1,
                hasSpawnPosition: player.HasSpawnPosition,
                spawnX: player.PosX,
                spawnY: player.PosY,
                spawnZ: player.PosZ);

            var validation = MobaProtocolValidation.ValidatePlayerLoadout(in loadouts[i], i);
            if (!validation.IsValid)
            {
                throw new InvalidOperationException("Invalid Orleans MOBA player loadout. " + validation);
            }
        }

        return loadouts;
    }

    public IReadOnlyList<PlayerInputCommand> CreatePlayerInputCommands(int frame, IReadOnlyList<BattleInputItem>? inputs)
    {
        if (inputs == null || inputs.Count == 0)
        {
            return Array.Empty<PlayerInputCommand>();
        }

        var frameIndex = new FrameIndex(frame);
        var commands = new List<PlayerInputCommand>(inputs.Count);
        for (int i = 0; i < inputs.Count; i++)
        {
            var input = inputs[i];
            if (input == null)
            {
                continue;
            }

            commands.Add(new PlayerInputCommand(
                frameIndex,
                new PlayerId(input.PlayerId.ToString()),
                input.OpCode,
                input.Payload ?? Array.Empty<byte>()));
        }

        return commands;
    }

    public StateSyncPush CreateStateSyncPush(
        ulong worldId,
        int frame,
        WorldStateSnapshot? snapshot,
        IReadOnlyList<MobaDiagnosticEntityState>? diagnosticStates,
        bool isFullSnapshot)
    {
        return new StateSyncPush
        {
            WorldId = worldId,
            Frame = frame,
            Timestamp = DateTime.UtcNow.Ticks,
            Actors = CreateActorSnapshots(frame, snapshot, diagnosticStates),
            IsFullSnapshot = isFullSnapshot,
            SchemaVersion = CurrentSnapshotSchemaVersion
        };
    }

    public StateSyncPush CreateStateSyncPushFromProjection(
        ulong worldId,
        int frame,
        IActorProjectionProducer producer,
        bool isFullSnapshot,
        List<ActorProjectionData> buffer = null,
        Dictionary<int, ActorProjectionData>? lastFrameData = null)
    {
        if (producer == null) throw new ArgumentNullException(nameof(producer));

        buffer ??= new List<ActorProjectionData>(64);
        buffer.Clear();
        producer.ExtractAll(buffer);

        var actors = new List<ActorSnapshot>(isFullSnapshot ? buffer.Count : buffer.Count / 4);
        var currentActorIds = lastFrameData == null
            ? null
            : new HashSet<int>();
        for (int i = 0; i < buffer.Count; i++)
        {
            var p = buffer[i];
            currentActorIds?.Add(p.ActorId);

            // Delta 帧: 实体级增量——跳过与上一已发布帧相同的 actor。
            if (!isFullSnapshot && lastFrameData != null
                && lastFrameData.TryGetValue(p.ActorId, out var prev)
                && ProjectionDataEquals(in p, in prev))
            {
                continue;
            }

            actors.Add(new ActorSnapshot
            {
                ActorId = p.ActorId,
                X = p.PosX,
                Y = p.PosY,
                Z = p.PosZ,
                Rotation = QuatToYaw(p.RotX, p.RotY, p.RotZ, p.RotW),
                VelocityX = p.VelX,
                VelocityZ = p.VelZ,
                Hp = p.Hp,
                HpMax = p.HpMax,
                TeamId = p.TeamId,
                Kind = p.Kind,
                Code = p.Code,
                OwnerNetId = p.OwnerNetId
            });
        }

        var removedActorIds = new List<int>();
        if (!isFullSnapshot && lastFrameData != null && currentActorIds != null)
        {
            foreach (var actorId in lastFrameData.Keys)
            {
                if (!currentActorIds.Contains(actorId))
                {
                    removedActorIds.Add(actorId);
                }
            }
        }

        // 每个已发布帧都成为下一增量帧的比较基线。
        if (lastFrameData != null)
        {
            lastFrameData.Clear();
            for (int i = 0; i < buffer.Count; i++)
            {
                lastFrameData[buffer[i].ActorId] = buffer[i];
            }
        }

        return new StateSyncPush
        {
            WorldId = worldId,
            Frame = frame,
            Timestamp = DateTime.UtcNow.Ticks,
            Actors = actors,
            IsFullSnapshot = isFullSnapshot,
            SchemaVersion = CurrentSnapshotSchemaVersion,
            RemovedActorIds = removedActorIds
        };
    }

    // 比较两个投影数据是否相等（用于 Delta 帧跳过不变的 actor）。
    private static bool ProjectionDataEquals(in ActorProjectionData a, in ActorProjectionData b)
    {
        return a.ActorId == b.ActorId
            && a.PosX == b.PosX && a.PosY == b.PosY && a.PosZ == b.PosZ
            && a.RotX == b.RotX && a.RotY == b.RotY && a.RotZ == b.RotZ && a.RotW == b.RotW
            && a.Hp == b.Hp && a.HpMax == b.HpMax
            && a.TeamId == b.TeamId
            && a.VelX == b.VelX && a.VelZ == b.VelZ
            && a.Kind == b.Kind
            && a.Code == b.Code
            && a.OwnerNetId == b.OwnerNetId;
    }

    public BattleSnapshot CreateBattleSnapshot(int frame, WorldStateSnapshot snapshot, IReadOnlyList<MobaDiagnosticEntityState>? diagnosticStates)
    {
        return new BattleSnapshot
        {
            Frame = frame,
            Actors = CreateActorSnapshots(frame, snapshot, diagnosticStates)
        };
    }

    private List<ActorSnapshot> CreateActorSnapshots(int frame, WorldStateSnapshot? snapshot, IReadOnlyList<MobaDiagnosticEntityState>? diagnosticStates)
    {
        if (snapshot.HasValue)
        {
            var runtimeSnapshot = snapshot.Value;
            var context = new MobaRuntimeSnapshotContext(frame, DateTime.UtcNow.Ticks);
            if (_actorSnapshotMappers.TryMap(in runtimeSnapshot, in context, out var actors))
            {
                return actors;
            }
        }

        return ConvertDiagnosticFallbackActors(diagnosticStates);
    }

    private static List<ActorSnapshot> ConvertDiagnosticFallbackActors(IReadOnlyList<MobaDiagnosticEntityState>? states)
    {
        var actors = new List<ActorSnapshot>(states?.Count ?? 0);
        if (states == null)
        {
            return actors;
        }

        for (int i = 0; i < states.Count; i++)
        {
            var state = states[i];
            actors.Add(new ActorSnapshot
            {
                ActorId = state.EntityId,
                X = state.X,
                Y = state.Y,
                Z = state.Z,
                Rotation = state.Rotation,
                VelocityX = state.VelocityX,
                VelocityZ = state.VelocityZ,
                Hp = state.Hp,
                HpMax = state.HpMax,
                TeamId = state.TeamId
            });
        }

        return actors;
    }

    // yaw = atan2(forward.x, forward.z), where forward = q × (0,0,1)
    // forward.x = 2(xz + wy), forward.z = 1 − 2(x² + y²)
    private static float QuatToYaw(float x, float y, float z, float w)
    {
        return MathF.Atan2(2f * (x * z + w * y), 1f - 2f * (x * x + y * y));
    }

    private sealed class ActorTransformSnapshotMapper : IMobaRuntimeSnapshotMapper<List<ActorSnapshot>>
    {
        public int OpCode => MobaOpCodes.Snapshot.ActorTransform;

        public bool TryMap(in WorldStateSnapshot snapshot, in MobaRuntimeSnapshotContext context, out List<ActorSnapshot> output)
        {
            var actors = new List<ActorSnapshot>();
            var entries = MobaActorTransformSnapshotCodec.Deserialize(snapshot.Payload);
            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                actors.Add(new ActorSnapshot
                {
                    ActorId = entry.ActorId,
                    X = entry.X,
                    Y = entry.Y,
                    Z = entry.Z
                });
            }

            output = actors;
            return actors.Count > 0;
        }
    }
}
