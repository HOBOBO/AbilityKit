using AbilityKit.Demo.Shooter;
using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Network.Runtime.Conditioning;
using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Grains.Battle;
using AbilityKit.Orleans.Grains.Battle.Gameplay;
using AbilityKit.Orleans.Grains.Gameplays.Shooter.Battle;
using AbilityKit.Protocol.Shooter;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AbilityKit.Orleans.Grains.Tests.Battle;

public sealed class ShooterBattleRuntimeAdapterTests
{
    [Fact]
    public void SessionStartTickAndSnapshotPush_UsesShooterRuntimeWorldBoundary()
    {
        using var worldManager = new ServerBattleWorldManager(NullLogger.Instance);
        var adapter = new ShooterBattleRuntimeAdapter(worldManager);
        using var session = adapter.CreateSession("shooter-battle-adapter-test");
        var initParams = CreateInitParams();

        var start = session.Start(initParams);

        Assert.True(start.Succeeded, start.Error);
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
                Assert.Equal(3f, second.X);
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
        Assert.True(session.Tick(frame: 1, tickRate: 30, deltaTime: 1f / 30f));

        var reliableProducer = Assert.IsAssignableFrom<IReliableBattleEventProducer>(session);
        var reliableEvents = reliableProducer.CaptureReliableEvents(frame: 1);
        var fire = Assert.Single(reliableEvents);
        Assert.Equal(1, fire.SourceFrame);
        Assert.Equal((int)ShooterEventType.Fire, fire.EventType);
        Assert.NotNull(fire.Payload);
        var firePayload = ShooterStateSnapshotCodec.DeserializeEvent(fire.Payload!);
        Assert.Equal((int)ShooterEventType.Fire, firePayload.EventType);
        Assert.Equal(1, firePayload.SourcePlayerId);

        var snapshot = session.GetSnapshot(1);
        Assert.NotNull(snapshot);
        Assert.Equal(1, snapshot!.Frame);
        Assert.Equal(2, snapshot.Actors.Count);
        Assert.True(snapshot.Actors[0].X > 0f);

        var push = session.CreateStateSyncPush(initParams.WorldId, frame: 1, isFullSnapshot: true);
        Assert.Equal(initParams.WorldId, push.WorldId);
        Assert.Equal(1, push.Frame);
        Assert.True(push.IsFullSnapshot);
        Assert.Equal(ShooterOpCodes.Snapshot.PackedState, push.PayloadOpCode);
        Assert.NotNull(push.Payload);
        Assert.NotEmpty(push.Payload!);

        var packed = ShooterPackedSnapshotCodec.Deserialize(push.Payload!);
        Assert.NotEqual(fire.Payload, push.Payload);
        Assert.Equal(initParams.WorldId, packed.WorldId);
        Assert.Equal(push.Frame, packed.Frame);
        Assert.Equal(4, packed.EntityCount);
        Assert.NotEqual(0u, packed.StateHash);
        AssertPackedEnemiesVisible(packed);
    }

    [Fact]
    public void WithoutLegacyEvents_RemovesFireAndHitWithoutChangingSnapshotState()
    {
        var snapshot = new ShooterStateSnapshotPayload
        {
            Frame = 7,
            Players = new[]
            {
                new ShooterPlayerSnapshot(1, 2f, 3f, 1f, 0f, 90, 0, true)
            },
            Events = new[]
            {
                new ShooterEventSnapshot(ShooterEventType.Fire, 1, 0, 11, 2f, 3f, 0),
                new ShooterEventSnapshot(ShooterEventType.Hit, 1, -1, 11, 4f, 3f, 10)
            }
        };

        var projected = ShooterBattleRuntimeAdapter.WithoutLegacyEvents(snapshot);

        Assert.Equal(7, projected.Frame);
        Assert.Single(projected.Players);
        Assert.Empty(projected.Events);
        Assert.Equal(2, snapshot.Events.Length);
    }

    [Fact]
    public void SessionStart_WhenBattleFlowOverridesProvided_UsesOverridesForShooterWorld()
    {
        using var worldManager = new ServerBattleWorldManager(NullLogger.Instance);
        var adapter = new ShooterBattleRuntimeAdapter(worldManager);
        using var session = adapter.CreateSession("shooter-battle-flow-override-test");
        var initParams = CreateInitParams();
        initParams.DurationFrames = 3600;
        initParams.VictoryTargetDefeats = 100000;

        var start = session.Start(initParams);

        Assert.True(start.Succeeded, start.Error);
        var snapshot = session.GetSnapshot(0);
        Assert.NotNull(snapshot);
        Assert.Equal(3600, snapshot!.TimeLimitFrames);
        Assert.Equal(3600, snapshot.RemainingTimeFrames);
        Assert.Equal(100000, snapshot.VictoryTargetDefeats);
    }

    [Fact]
    public void SessionTick_WhenSoakLongevityOverridesProvided_AdvancesBeyondFrame5401()
    {
        using var worldManager = new ServerBattleWorldManager(NullLogger.Instance);
        var adapter = new ShooterBattleRuntimeAdapter(worldManager);
        using var session = adapter.CreateSession("shooter-battle-soak-longevity-test");
        var initParams = CreateInitParams();
        initParams.DurationFrames = 37200;
        initParams.VictoryTargetDefeats = int.MaxValue;
        initParams.ContinueAfterAllPlayersDefeated = true;

        var start = session.Start(initParams);

        Assert.True(start.Succeeded, start.Error);
        for (var frame = 1; frame <= 5402; frame++)
        {
            var ticked = session.Tick(frame, initParams.TickRate, 1f / initParams.TickRate);
            var current = session.GetSnapshot(frame);
            Assert.True(
                ticked,
                $"Runtime stopped at requested frame {frame}; actual frame {current?.Frame}, match state {current?.MatchState}, time limit {current?.TimeLimitFrames}.");
        }

        var snapshot = session.GetSnapshot(5402);
        Assert.NotNull(snapshot);
        Assert.Equal(5402, snapshot!.Frame);
        Assert.Equal((int)ShooterBattleMatchState.Running, snapshot.MatchState);
        Assert.False(snapshot.MatchFinal);
        Assert.Equal(37200, snapshot.TimeLimitFrames);
    }

    [Fact]
    public void SessionTick_MovesWaveEnemiesInServerAuthorityWorld()
    {
        using var worldManager = new ServerBattleWorldManager(NullLogger.Instance);
        var adapter = new ShooterBattleRuntimeAdapter(worldManager);
        using var session = adapter.CreateSession("shooter-battle-adapter-enemy-move-test");
        var initParams = CreateInitParams();
        var start = session.Start(initParams);
        Assert.True(start.Succeeded, start.Error);

        Assert.True(session.Tick(frame: 1, tickRate: 30, deltaTime: 1f / 30f));
        var initialPush = session.CreateStateSyncPush(initParams.WorldId, frame: 1, isFullSnapshot: true);
        var initialPacked = ShooterPackedSnapshotCodec.Deserialize(initialPush.Payload!);
        var initialEnemy = FindFirstPackedTransform(initialPacked, ShooterPackedEntityKinds.Enemy);
        Assert.NotNull(initialEnemy);
        var beforeDistanceSquared = initialEnemy.Value.X * initialEnemy.Value.X + initialEnemy.Value.Y * initialEnemy.Value.Y;

        Assert.True(session.Tick(frame: 2, tickRate: 30, deltaTime: 1f / 30f));

        var movedPush = session.CreateStateSyncPush(initParams.WorldId, frame: 2, isFullSnapshot: true);
        var movedPacked = ShooterPackedSnapshotCodec.Deserialize(movedPush.Payload!);
        var movedEnemy = FindPackedTransform(movedPacked, ShooterPackedEntityKinds.Enemy, initialEnemy.Value.EntityId);
        Assert.NotNull(movedEnemy);
        var afterDistanceSquared = movedEnemy.Value.X * movedEnemy.Value.X + movedEnemy.Value.Y * movedEnemy.Value.Y;
        Assert.True(afterDistanceSquared < beforeDistanceSquared);
    }

    [Fact]
    public void CreateStateSyncPush_WhenPureStateEnabled_EmitsPureStateFullAndDeltaPayloads()
    {
        using var worldManager = new ServerBattleWorldManager(NullLogger.Instance);
        var adapter = new ShooterBattleRuntimeAdapter(
            worldManager,
            ShooterStateSyncPushOptions.PureState(NetworkConditionProfile.Lan));
        using var session = adapter.CreateSession("shooter-pure-state-adapter-test");
        var initParams = CreateInitParams();
        var start = session.Start(initParams);
        Assert.True(start.Succeeded, start.Error);
        Assert.True(session.Tick(frame: 1, tickRate: 30, deltaTime: 1f / 30f));

        var full = session.CreateStateSyncPush(initParams.WorldId, frame: 1, isFullSnapshot: true);
        var fullPayload = ShooterPureStateSyncCodec.Deserialize(full.Payload!);
        Assert.Equal(ShooterOpCodes.Snapshot.PureState, full.PayloadOpCode);
        Assert.True(full.IsFullSnapshot);
        Assert.Equal(ShooterPureStateSnapshotKinds.FullBaseline, fullPayload.SnapshotKind);
        Assert.Equal(initParams.WorldId, fullPayload.WorldId);
        Assert.Equal(full.Frame, fullPayload.Frame);
        Assert.Equal(ShooterPureStateSyncSettings.Default.ActiveSyncBudget, fullPayload.Settings.ActiveSyncBudget);
        Assert.NotEqual(0u, fullPayload.StateHash);

        session.SubmitInputs(
            1,
            new[]
            {
                new BattleInputItem
                {
                    PlayerId = 1,
                    OpCode = ShooterOpCodes.Input.PlayerCommand,
                    Payload = ShooterInputCodec.Serialize(new[]
                    {
                        new ShooterPlayerCommand(1, 1f, 0f, 1f, 0f, false)
                    })
                }
            });
        Assert.True(session.Tick(frame: 2, tickRate: 30, deltaTime: 1f / 30f));

        var delta = session.CreateStateSyncPush(initParams.WorldId, frame: 2, isFullSnapshot: false);
        var deltaPayload = ShooterPureStateSyncCodec.Deserialize(delta.Payload!);
        Assert.Equal(ShooterOpCodes.Snapshot.PureStateDelta, delta.PayloadOpCode);
        Assert.False(delta.IsFullSnapshot);
        Assert.Equal(ShooterPureStateSnapshotKinds.Delta, deltaPayload.SnapshotKind);
        Assert.Equal(fullPayload.Frame, deltaPayload.BaselineFrame);
        Assert.Equal(fullPayload.StateHash, deltaPayload.BaselineHash);
    }

    [Theory]
    [InlineData(null, true, 0)]
    [InlineData("", true, 0)]
    [InlineData("packed", true, 0)]
    [InlineData("pure-state", true, 1)]
    [InlineData("purestate", true, 1)]
    [InlineData("pure_state", true, 1)]
    [InlineData("unknown", false, 0)]
    public void TryParsePayloadMode_ParsesSupportedSmokeValues(string? value, bool expectedParsed, int expectedModeValue)
    {
        var expectedMode = (ShooterStateSyncPushPayloadMode)expectedModeValue;
        var parsed = ShooterStateSyncPushOptions.TryParsePayloadMode(value, out var mode);

        Assert.Equal(expectedParsed, parsed);
        Assert.Equal(expectedMode, mode);
    }

    [Fact]
    public void DefaultAdapter_WhenPureStateEnvironmentModeIsSet_EmitsPureStatePayload()
    {
        var previous = Environment.GetEnvironmentVariable(ShooterStateSyncPushOptions.PayloadModeEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(ShooterStateSyncPushOptions.PayloadModeEnvironmentVariable, "pure-state");
            using var worldManager = new ServerBattleWorldManager(NullLogger.Instance);
            var adapter = new ShooterBattleRuntimeAdapter(worldManager);
            using var session = adapter.CreateSession("shooter-pure-state-env-adapter-test");
            var initParams = CreateInitParams();
            var start = session.Start(initParams);
            Assert.True(start.Succeeded, start.Error);
            Assert.True(session.Tick(frame: 1, tickRate: 30, deltaTime: 1f / 30f));

            var push = session.CreateStateSyncPush(initParams.WorldId, frame: 1, isFullSnapshot: true);
            var payload = ShooterPureStateSyncCodec.Deserialize(push.Payload!);

            Assert.Equal(ShooterOpCodes.Snapshot.PureState, push.PayloadOpCode);
            Assert.Equal(ShooterPureStateSnapshotKinds.FullBaseline, payload.SnapshotKind);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ShooterStateSyncPushOptions.PayloadModeEnvironmentVariable, previous);
        }
    }

    [Fact]
    public void ValidateInput_AcceptsSingleFiniteCommandForSubmittingPlayer()
    {
        using var session = CreateSession("shooter-valid-input-test");
        var result = session.ValidateInput(CreateInput(
            playerId: 1,
            opCode: ShooterOpCodes.Input.PlayerCommand,
            new ShooterPlayerCommand(1, 1f, 0f, 0f, 1f, true)));

        Assert.True(result.Accepted, result.Message);
    }

    [Fact]
    public void ValidateInput_RejectsUnsupportedOpcodeAndMalformedPayload()
    {
        using var session = CreateSession("shooter-invalid-encoding-test");

        var invalidOpcode = session.ValidateInput(CreateInput(1, ShooterOpCodes.Input.PlayerCommand + 1,
            new ShooterPlayerCommand(1, 0f, 0f, 1f, 0f, false)));
        var malformed = session.ValidateInput(new BattleInputItem
        {
            PlayerId = 1,
            OpCode = ShooterOpCodes.Input.PlayerCommand,
            Payload = new byte[] { 1, 2, 3 }
        });

        Assert.False(invalidOpcode.Accepted);
        Assert.Equal(BattleResultStatusCodes.RejectedInvalidOpCode, invalidOpcode.Status);
        Assert.False(malformed.Accepted);
        Assert.Equal(BattleResultStatusCodes.RejectedInvalidPayload, malformed.Status);
    }

    [Fact]
    public void ValidateInput_RejectsEmptyMultipleMismatchedAndNonFiniteCommands()
    {
        using var session = CreateSession("shooter-invalid-command-test");
        var empty = session.ValidateInput(CreateInput(1, ShooterOpCodes.Input.PlayerCommand));
        var multiple = session.ValidateInput(CreateInput(
            1,
            ShooterOpCodes.Input.PlayerCommand,
            new ShooterPlayerCommand(1, 0f, 0f, 1f, 0f, false),
            new ShooterPlayerCommand(1, 0f, 0f, 1f, 0f, false)));
        var mismatched = session.ValidateInput(CreateInput(
            1,
            ShooterOpCodes.Input.PlayerCommand,
            new ShooterPlayerCommand(2, 0f, 0f, 1f, 0f, false)));
        var nonFinite = session.ValidateInput(CreateInput(
            1,
            ShooterOpCodes.Input.PlayerCommand,
            new ShooterPlayerCommand(1, float.NaN, 0f, float.PositiveInfinity, 0f, false)));

        Assert.All(new[] { empty, multiple, mismatched, nonFinite }, result =>
        {
            Assert.False(result.Accepted);
            Assert.Equal(BattleResultStatusCodes.RejectedInvalidPayload, result.Status);
        });
    }

    [Fact]
    public void JoinPlayer_WhenPlayerIsMissing_ReturnsSharedRejectedNullPlayerStatus()
    {
        using var worldManager = new ServerBattleWorldManager(NullLogger.Instance);
        var adapter = new ShooterBattleRuntimeAdapter(worldManager);
        using var session = adapter.CreateSession("shooter-null-player-status-test");
        var initParams = CreateInitParams();
        var start = session.Start(initParams);
        Assert.True(start.Succeeded, start.Error);

        var result = session.JoinPlayer(new BattlePlayerJoinRequest(initParams.WorldId, Player: null!), currentFrame: 12);

        Assert.False(result.Accepted);
        Assert.Equal(BattleResultStatusCodes.RejectedNullPlayer, result.Status);
        Assert.Equal(12, result.CurrentFrame);
    }

    [Fact]
    public void BattleResultStatusCodes_KeepProtocolStatusStringsStable()
    {
        Assert.Equal("RejectedNotInitialized", BattleResultStatusCodes.RejectedNotInitialized);
        Assert.Equal("RejectedWorldMismatch", BattleResultStatusCodes.RejectedWorldMismatch);
        Assert.Equal("RejectedNullInput", BattleResultStatusCodes.RejectedNullInput);
        Assert.Equal("RejectedByInputBuffer", BattleResultStatusCodes.RejectedByInputBuffer);
        Assert.Equal("RejectedNullRequest", BattleResultStatusCodes.RejectedNullRequest);
        Assert.Equal("RejectedNullPlayer", BattleResultStatusCodes.RejectedNullPlayer);
    }

    [Fact]
    public void CreateStateSyncPush_WhenPureStateUsesLimitedBandwidth_ReducesActiveBudget()
    {
        using var worldManager = new ServerBattleWorldManager(NullLogger.Instance);
        var adapter = new ShooterBattleRuntimeAdapter(
            worldManager,
            ShooterStateSyncPushOptions.PureState(NetworkConditionProfile.LimitedBandwidth));
        using var session = adapter.CreateSession("shooter-pure-state-budget-test");
        var initParams = CreateInitParams();
        var start = session.Start(initParams);
        Assert.True(start.Succeeded, start.Error);
        Assert.True(session.Tick(frame: 1, tickRate: 30, deltaTime: 1f / 30f));

        var push = session.CreateStateSyncPush(initParams.WorldId, frame: 1, isFullSnapshot: true);
        var payload = ShooterPureStateSyncCodec.Deserialize(push.Payload!);

        Assert.Equal(ShooterOpCodes.Snapshot.PureState, push.PayloadOpCode);
        Assert.Equal(128, payload.Settings.ActiveSyncBudget);
        Assert.Equal(4, payload.Settings.DeltaIntervalFrames);
        Assert.Equal(30, payload.Settings.LowFrequencyIntervalFrames);
        Assert.Equal(6, payload.Settings.InterpolationDelayFrames);
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

    private static (int EntityId, float X, float Y)? FindFirstPackedTransform(in ShooterPackedSnapshotPayload packed, int entityKind)
    {
        var transformChunk = FindPackedChunk(packed, ShooterPackedComponentKinds.Transform, entityKind);
        if (!transformChunk.HasValue || transformChunk.Value.Count <= 0)
        {
            return null;
        }

        return (
            transformChunk.Value.EntityIds[0],
            transformChunk.Value.ValueX[0],
            transformChunk.Value.ValueY[0]);
    }

    private static (int EntityId, float X, float Y)? FindPackedTransform(in ShooterPackedSnapshotPayload packed, int entityKind, int entityId)
    {
        var transformChunk = FindPackedChunk(packed, ShooterPackedComponentKinds.Transform, entityKind);
        if (!transformChunk.HasValue)
        {
            return null;
        }

        var chunk = transformChunk.Value;
        for (var i = 0; i < chunk.Count; i++)
        {
            if (chunk.EntityIds[i] == entityId)
            {
                return (entityId, chunk.ValueX[i], chunk.ValueY[i]);
            }
        }

        return null;
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

    private static IBattleRuntimeSession CreateSession(string battleId)
    {
        var worldManager = new ServerBattleWorldManager(NullLogger.Instance);
        var adapter = new ShooterBattleRuntimeAdapter(worldManager);
        return adapter.CreateSession(battleId);
    }

    private static BattleInputItem CreateInput(uint playerId, int opCode, params ShooterPlayerCommand[] commands) =>
        new()
        {
            PlayerId = playerId,
            OpCode = opCode,
            Payload = ShooterInputCodec.Serialize(commands)
        };

    private static BattleInitParams CreateInitParams()
    {
        return new BattleInitParams
        {
            WorldId = 707ul,
            TickRate = 30,
            RandomSeed = 1357,
            RoomType = ShooterGameplay.RoomType,
            WorldType = ShooterGameplay.WorldType,
            GameplayId = ShooterGameplay.GameplayId,
            Players = new List<PlayerInitInfo>
            {
                new PlayerInitInfo
                {
                    PlayerId = 1,
                    PosX = 0f,
                    PosZ = 0f,
                    TeamId = 1
                },
                new PlayerInitInfo
                {
                    PlayerId = 2,
                    PosX = 3f,
                    PosZ = 0f,
                    TeamId = 2
                }
            }
        };
    }
}

