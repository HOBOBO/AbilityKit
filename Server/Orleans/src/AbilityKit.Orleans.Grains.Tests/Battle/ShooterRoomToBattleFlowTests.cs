using AbilityKit.Demo.Shooter;
using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Orleans.Contracts.Shooter;
using AbilityKit.Orleans.Grains.Battle;
using AbilityKit.Orleans.Grains.Battle.Gameplay;
using AbilityKit.Orleans.Grains.Gameplay;
using AbilityKit.Orleans.Grains.Gameplays.Moba.Battle;
using AbilityKit.Orleans.Grains.Gameplays.Moba.Protocol;
using AbilityKit.Orleans.Grains.Gameplays.Shooter.Battle;
using AbilityKit.Orleans.Grains.Gameplays.Shooter.Rooms;
using AbilityKit.Orleans.Grains.Rooms.Gameplay;
using AbilityKit.Protocol.Shooter;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AbilityKit.Orleans.Grains.Tests.Battle;

public sealed class ShooterRoomToBattleFlowTests
{
    [Fact]
    public void MultiUnitShooterRoom_WhenBattleRunsForConfiguredDuration_ClosesBattleAndEndsRoom()
    {
        var roomAdapter = new ShooterRoomGameplayAdapter();
        var roomSummary = CreateRoomSummary(maxPlayers: 4);
        var roomState = roomAdapter.CreateState(roomSummary);
        var roomMembers = new HashSet<string>();
        var accountIds = new[] { "player-a", "player-b", "player-c", "player-d" };
        for (int i = 0; i < accountIds.Length; i++)
        {
            roomAdapter.Join(roomState, roomSummary, roomMembers, accountIds[i]);
            roomMembers.Add(accountIds[i]);
            roomAdapter.SetReady(roomState, new RoomReadyRequest(accountIds[i], true));
        }

        var initParams = roomAdapter.BuildBattleInitParams(
            roomState,
            roomSummary,
            new StartRoomBattleRequest(
                "player-a",
                GameplayId: 0,
                RuleSetId: 21,
                ConfigVersion: 22,
                ProtocolVersion: 23,
                WorldType: null,
                ClientId: "client-a"));

        using var worldManager = new ServerBattleWorldManager(NullLogger.Instance);
        var battleAdapter = new ShooterBattleRuntimeAdapter(worldManager);
        var session = battleAdapter.CreateSession("shooter-multi-unit-lifecycle-test");
        var start = session.Start(initParams);

        Assert.True(roomAdapter.CanStart(roomState));
        Assert.True(start.Succeeded, start.Error);
        Assert.Equal(4, initParams.Players!.Count);
        Assert.True(worldManager.TryGetBattleWorld("shooter-multi-unit-lifecycle-test", out _));

        const int durationFrames = 90;
        for (int frame = 0; frame < durationFrames; frame++)
        {
            var accepted = session.SubmitInputs(frame, CreateMoveInputs(accountIds.Length, frame));
            Assert.Equal(accountIds.Length, accepted);
            Assert.True(session.Tick(frame + 1, initParams.TickRate, 1f / initParams.TickRate));
        }

        var finalSnapshot = session.GetSnapshot(durationFrames);
        Assert.NotNull(finalSnapshot);
        Assert.Equal(durationFrames, finalSnapshot!.Frame);
        Assert.Equal(accountIds.Length, finalSnapshot.Actors.Count);
        AssertActorMoved(finalSnapshot, actorId: 1);
        AssertActorMoved(finalSnapshot, actorId: 4);

        var finalPush = session.CreateStateSyncPush(initParams.WorldId, durationFrames, isFullSnapshot: true);
        Assert.Equal(initParams.WorldId, finalPush.WorldId);
        Assert.Equal(durationFrames, finalPush.Frame);
        Assert.True(finalPush.IsFullSnapshot);
        Assert.NotNull(finalPush.Payload);
        Assert.NotEmpty(finalPush.Payload!);

        var packed = ShooterPackedSnapshotCodec.Deserialize(finalPush.Payload!);
        Assert.NotEqual(0u, packed.StateHash);
        Assert.True(packed.EntityCount > accountIds.Length);
        AssertPackedEnemiesVisible(packed);
        AssertPackedPlayerDamageVisible(packed);

        session.Dispose();
        Assert.False(worldManager.TryGetBattleWorld("shooter-multi-unit-lifecycle-test", out _));

        for (int i = 0; i < accountIds.Length; i++)
        {
            roomAdapter.Leave(roomState, accountIds[i]);
            roomMembers.Remove(accountIds[i]);
        }

        Assert.False(roomAdapter.CanStart(roomState));
        Assert.Empty(roomAdapter.BuildPlayerSnapshots(roomState));
        Assert.Empty(roomMembers);
    }

    [Fact]
    public void ReadyShooterRoom_WhenResolvedThroughBattleRegistry_StartsRuntimeAndPublishesPackedSnapshot()
    {
        var roomAdapter = new ShooterRoomGameplayAdapter();
        var roomSummary = CreateRoomSummary();
        var roomState = roomAdapter.CreateState(roomSummary);
        roomAdapter.Join(roomState, roomSummary, new HashSet<string>(), "player-a");
        roomAdapter.Join(roomState, roomSummary, new HashSet<string> { "player-a" }, "player-b");
        roomAdapter.SetReady(roomState, new RoomReadyRequest("player-a", true));
        roomAdapter.SetReady(roomState, new RoomReadyRequest("player-b", true));

        var initParams = roomAdapter.BuildBattleInitParams(
            roomState,
            roomSummary,
            new StartRoomBattleRequest(
                "player-a",
                GameplayId: 0,
                RuleSetId: 11,
                ConfigVersion: 12,
                ProtocolVersion: 13,
                WorldType: null,
                ClientId: "client-a"));

        using var worldManager = new ServerBattleWorldManager(NullLogger.Instance);
        var shooterRuntimeAdapter = new ShooterBattleRuntimeAdapter(worldManager);
        var registry = new BattleRuntimeRegistry(
            new IBattleRuntimeAdapter[]
            {
                new MobaBattleRuntimeAdapter(worldManager, DefaultOrleansBattleProtocolMapper.Instance),
                shooterRuntimeAdapter
            },
            ServerGameplayCatalog.Default);
        var battleAdapter = registry.Resolve(initParams.RoomType);
        using var session = battleAdapter.CreateSession("shooter-room-to-battle-flow-test");

        var start = session.Start(initParams);

        Assert.True(roomAdapter.CanStart(roomState));
        Assert.IsType<ShooterBattleRuntimeAdapter>(battleAdapter);
        Assert.True(start.Succeeded, start.Error);
        Assert.Equal(ShooterGameplay.RoomType, initParams.RoomType);
        Assert.Equal(ShooterGameplay.WorldType, initParams.WorldType);
        Assert.Equal(ShooterGameplay.GameplayId, initParams.GameplayId);
        Assert.Equal(30, initParams.TickRate);
        Assert.Equal(77, initParams.MapId);
        Assert.Equal(2468, initParams.RandomSeed);
        Assert.Equal(11, initParams.RuleSetId);
        Assert.Equal(12, initParams.ConfigVersion);
        Assert.Equal(13, initParams.ProtocolVersion);
        Assert.Equal(4, initParams.InputDelayFrames);
        Assert.NotNull(initParams.SyncOptions);
        Assert.Equal(ShooterServerProtocol.RuntimeSnapshotInterpolationTemplate, initParams.SyncOptions!.SyncTemplateId);
        Assert.Equal(3, initParams.SyncOptions.SyncModel);
        Assert.Equal("wan-lossy", initParams.SyncOptions.NetworkEnvironmentId);
        Assert.Equal("OrleansGateway", initParams.SyncOptions.CarrierName);
        Assert.False(initParams.SyncOptions.EnableAuthoritativeWorld);
        Assert.True(initParams.SyncOptions.InterpolationEnabled);
        Assert.Equal(4, initParams.SyncOptions.InputDelayFrames);

        var initialSnapshot = session.GetSnapshot(0);
        Assert.NotNull(initialSnapshot);
        Assert.Equal(0, initialSnapshot!.Frame);
        Assert.Collection(
            initialSnapshot.Actors,
            first =>
            {
                Assert.Equal(1, first.ActorId);
                Assert.Equal(0f, first.X);
            },
            second =>
            {
                Assert.Equal(2, second.ActorId);
                Assert.Equal(2f, second.X);
            });

        var accepted = session.SubmitInputs(
            0,
            new[]
            {
                new BattleInputItem
                {
                    PlayerId = 1,
                    OpCode = ShooterOpCodes.Input.PlayerCommand,
                    Payload = ShooterInputCodec.Serialize(new[]
                    {
                        new ShooterPlayerCommand(1, 1f, 0f, 1f, 0f, true)
                    })
                }
            });

        Assert.Equal(1, accepted);
        Assert.True(session.Tick(frame: 1, tickRate: initParams.TickRate, deltaTime: 1f / initParams.TickRate));

        var snapshot = session.GetSnapshot(1);
        Assert.NotNull(snapshot);
        Assert.Equal(1, snapshot!.Frame);
        Assert.True(snapshot.Actors[0].X > 0f);

        var push = session.CreateStateSyncPush(initParams.WorldId, frame: 1, isFullSnapshot: true);
        Assert.Equal(initParams.WorldId, push.WorldId);
        Assert.Equal(1, push.Frame);
        Assert.True(push.IsFullSnapshot);
        Assert.Equal(ShooterOpCodes.Snapshot.PackedState, push.PayloadOpCode);
        Assert.NotNull(push.Payload);
        Assert.NotEmpty(push.Payload!);

        var packed = ShooterPackedSnapshotCodec.Deserialize(push.Payload!);
        Assert.Equal(initParams.WorldId, packed.WorldId);
        Assert.Equal(push.Frame, packed.Frame);
        var playerLifecycle = FindPackedChunk(
            packed,
            ShooterPackedComponentKinds.EntityLifecycle,
            ShooterPackedEntityKinds.Player);
        var enemyLifecycle = FindPackedChunk(
            packed,
            ShooterPackedComponentKinds.EntityLifecycle,
            ShooterPackedEntityKinds.Enemy);
        Assert.NotNull(playerLifecycle);
        Assert.NotNull(enemyLifecycle);
        Assert.Equal(2, playerLifecycle.Value.Count);
        Assert.True(packed.EntityCount >= playerLifecycle.Value.Count + enemyLifecycle.Value.Count);
        Assert.NotEqual(0u, packed.StateHash);
        AssertPackedEnemiesVisible(packed);
    }

    private static BattleInputItem[] CreateMoveInputs(int playerCount, int frame)
    {
        var inputs = new BattleInputItem[playerCount];
        for (int i = 0; i < playerCount; i++)
        {
            var playerId = i + 1;
            var direction = playerId % 2 == 0 ? -1f : 1f;
            inputs[i] = new BattleInputItem
            {
                PlayerId = (uint)playerId,
                OpCode = ShooterOpCodes.Input.PlayerCommand,
                Payload = ShooterInputCodec.Serialize(new[]
                {
                    new ShooterPlayerCommand(playerId, direction, 0f, direction, 0f, frame % 10 == 0)
                })
            };
        }

        return inputs;
    }

    private static void AssertPackedEnemiesVisible(in ShooterPackedSnapshotPayload packed)
    {
        var enemyLifecycleChunk = FindPackedChunk(packed, ShooterPackedComponentKinds.EntityLifecycle, ShooterPackedEntityKinds.Enemy);
        var enemyTransformChunk = FindPackedChunk(packed, ShooterPackedComponentKinds.Transform, ShooterPackedEntityKinds.Enemy);
        var enemyHealthChunk = FindPackedChunk(packed, ShooterPackedComponentKinds.Health, ShooterPackedEntityKinds.Enemy);
        Assert.NotNull(enemyLifecycleChunk);
        Assert.NotNull(enemyTransformChunk);
        Assert.NotNull(enemyHealthChunk);
        Assert.True(enemyLifecycleChunk.Value.Count > 0);
        Assert.Equal(enemyLifecycleChunk.Value.Count, enemyTransformChunk.Value.Count);
        Assert.Equal(enemyLifecycleChunk.Value.Count, enemyHealthChunk.Value.Count);
    }

    private static void AssertPackedPlayerDamageVisible(in ShooterPackedSnapshotPayload packed)
    {
        var playerHealthChunk = FindPackedChunk(packed, ShooterPackedComponentKinds.Health, ShooterPackedEntityKinds.Player);
        Assert.NotNull(playerHealthChunk);
        Assert.Contains(playerHealthChunk.Value.IntValues, hp => hp < ShooterGameplay.DefaultPlayerHp);
    }

    private static ShooterPackedComponentChunk? FindPackedChunk(in ShooterPackedSnapshotPayload packed, int componentKind, int entityKind)
    {
        for (int i = 0; i < packed.ComponentChunks.Length; i++)
        {
            var chunk = packed.ComponentChunks[i];
            if (chunk.ComponentKind == componentKind && chunk.EntityKind == entityKind)
            {
                return chunk;
            }
        }

        return null;
    }

    private static void AssertActorMoved(BattleSnapshot snapshot, int actorId)
    {
        for (int i = 0; i < snapshot.Actors.Count; i++)
        {
            var actor = snapshot.Actors[i];
            if (actor.ActorId != actorId)
            {
                continue;
            }

            Assert.NotEqual((actorId - 1) * 2f, actor.X);
            return;
        }

        Assert.Fail($"Actor {actorId} was not found in the final snapshot.");
    }

    private static RoomSummary CreateRoomSummary(int maxPlayers = 2)
    {
        return new RoomSummary(
            Region: "local",
            ServerId: "server-a",
            RoomId: "shooter-room-to-battle-flow",
            RoomType: ShooterGameplay.RoomType,
            Title: "Shooter Room To Battle Flow",
            IsPublic: true,
            MaxPlayers: maxPlayers,
            PlayerCount: 0,
            OwnerAccountId: "player-a",
            CreatedAtUnixMs: 0,
            Tags: new Dictionary<string, string>
            {
                [ShooterRoomTagKeys.TickRate] = "30",
                [ShooterRoomTagKeys.MapId] = "77",
                [ShooterRoomTagKeys.RandomSeed] = "2468",
                [ShooterRoomTagKeys.SyncTemplateId] = ShooterServerProtocol.RuntimeSnapshotInterpolationTemplate,
                [ShooterRoomTagKeys.SyncModel] = "3",
                [ShooterRoomTagKeys.NetworkEnvironmentId] = "wan-lossy",
                [ShooterRoomTagKeys.CarrierName] = "OrleansGateway",
                [ShooterRoomTagKeys.EnableAuthoritativeWorld] = "false",
                [ShooterRoomTagKeys.InterpolationEnabled] = "true",
                [ShooterRoomTagKeys.InputDelayFrames] = "4"
            });
    }
}

