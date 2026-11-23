using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.FrameSync;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.Management;
using AbilityKit.Ability.World.Services;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Events.Summon;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Runtime;
using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Grains.Battle;
using AbilityKit.Orleans.Grains.Battle.Gameplay;
using AbilityKit.Orleans.Grains.Gameplays.Moba.Battle;
using AbilityKit.Orleans.Grains.Gameplays.Moba.Protocol;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace AbilityKit.Orleans.Grains.Tests.Battle;

public sealed partial class MobaColdReplayFrameZeroTests(ITestOutputHelper output)
{
    /// <summary>
    /// 验证真实服务端适配器在状态投影中携带同帧活动集，并保留 HFSM 负载。
    /// </summary>
    [Fact]
    public void StateSyncProjectionCarriesActiveCueSetWithoutConsumingFrameEvents()
    {
        const string battleId = "moba-state-sync-cue-projection";
        using var worlds = new ServerBattleWorldManager(NullLogger.Instance);
        using var session = Start(worlds, battleId);
        var world = worlds.CreateBattleWorld(battleId, 30);
        var cues = world.Services.Resolve<MobaPresentationCueSnapshotService>();
        var frame = world.Services.Resolve<IFrameTime>().Frame;
        Assert.True(cues.TryCaptureStateSyncBaseline(frame, out var expected));

        var state = Assert.IsAssignableFrom<IFixedBaselineBattleSnapshotSource>(session)
            .CaptureFixedBaselineProjection(1, frame.Value);
        Assert.Equal(MobaStateSyncCuePayloadCodec.OpCode, state.PayloadOpCode);
        Assert.Equal(MobaStateSyncCuePayloadCodec.CarrierSchemaVersion, state.SchemaVersion);
        Assert.True(MobaStateSyncCuePayloadCodec.TryDecode(state.Payload!, out var projectionOpCode,
            out var projectionSchema, out var projection, out var activeCueSet));
        Assert.Contains(projectionOpCode, new[] { MobaCharacterHfsmRollbackProvider.DefaultKey, MobaCharacterHfsmWireCodec.CompressedOpCode });
        Assert.Equal(projectionOpCode == MobaCharacterHfsmWireCodec.CompressedOpCode
            ? MobaCharacterHfsmWireCodec.CompressedSchemaVersion : 2, projectionSchema);
        Assert.NotEmpty(MobaCharacterHfsmWireCodec.Decode(projectionOpCode, projection));
        Assert.Equal(expected.Payload, activeCueSet);
        Assert.True(cues.TryCaptureStateSyncBaseline(frame, out var after));
        Assert.Equal(expected.Payload, after.Payload);
    }

    [Fact]
    public void InputCorrection_ImportFailureDoesNotConsumeAuthorityOrTick()
    {
        const string battleId = "input-correction-import-failure";
        const float delta = 1f / 30f;
        using var worlds = new ServerBattleWorldManager(NullLogger.Instance);
        using var session = Start(worlds, battleId);
        var world = worlds.CreateBattleWorld(battleId, 30);
        var clock = world.Services.Resolve<IWorldClock>();
        var guard = world.Services.Resolve<MobaCombatFrameGuard>();
        var move = new LocalPlayerInputEvent(new FrameIndex(2), new PlayerId("1"), MobaOpCodes.Input.Move,
            MobaMoveCodec.Serialize(1f, 0f));
        using var local = new SingleLocalInput(move);
        using var remote = new FrameJitterBuffer<PlayerInputCommand[]>(0);
        var failing = new RejectingImportProvider();
        var options = new HostRuntimeOptions();
        var host = new HostRuntime(new ReplayWorldManager(world), options);
        var limit = 1;
        var prediction = new ClientPredictionDriverModule(_ => remote, _ => local, resolveIdealFrameLimit: _ => limit,
            enableRollback: true, buildRollbackRegistry: current =>
            {
                var registry = MobaRollbackRegistryBuilder.CreateForPrediction(current);
                registry.Register(failing);
                return registry;
            });
        prediction.Install(host, options);
        try
        {
            options.WorldCreated.Invoke(world);
            remote.Add(1, []);
            host.Tick(delta);
            var targetTime = clock.Time;
            limit = 2;
            host.Tick(delta);
            Assert.True(prediction.TryGetFrames(world.Id, out var confirmed, out var predicted));
            Assert.Equal((1, 2), (confirmed.Value, predicted.Value));
            var beforeCorrection = guard.CompletedTickCount;
            Assert.Equal(1, remote.LastConsumedFrame);
            remote.Add(2, []);

            for (var attempt = 0; attempt < 2; attempt++)
            {
                host.Tick(delta);
                Assert.Equal(1, failing.ImportAttempts);
                Assert.Equal(1, prediction.TotalRollbackRestoreFailed);
                Assert.Equal(0, prediction.TotalRollbackCount);
                Assert.Equal(1, remote.LastConsumedFrame);
                Assert.True(remote.TryGet(2, out _));
                Assert.True(prediction.TryGetFrames(world.Id, out confirmed, out predicted));
                Assert.Equal((1, 2), (confirmed.Value, predicted.Value));
                Assert.Equal(beforeCorrection, guard.CompletedTickCount);
                Assert.Equal(targetTime, clock.Time);
            }
        }
        finally
        {
            prediction.Uninstall(host, options);
        }
    }

    private sealed class SingleLocalInput(LocalPlayerInputEvent input) : ILocalInputSource<LocalPlayerInputEvent[]>
    {
        private bool _sent;
        public int LocalFrame => input.Frame.Value;

        public bool TryDequeue(out LocalPlayerInputEvent[] batch)
        {
            batch = _sent ? [] : [input];
            var available = !_sent;
            _sent = true;
            return available;
        }

        public void Dispose() { }
    }

    private sealed class RejectingImportProvider : IRollbackStateProvider
    {
        public int Key => int.MaxValue;
        public int ImportAttempts { get; private set; }
        public byte[] Export(FrameIndex frame) => [1];

        public void Import(FrameIndex frame, byte[] payload)
        {
            ImportAttempts++;
            throw new InvalidOperationException("Intentional provider import failure.");
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(true, false)]
    public void ColdReplay_SeparatesExpiredSummonAndPendingPublication(bool drainAuthority, bool drainPrediction)
    {
        const string battleId = "cold-replay-frame-zero";
        const int summonConfig = 30030502;
        const int pressFrame = 3;
        const int expiryFrame = 423;
        const int followUpFrame = expiryFrame + 7;
        const int lastFrame = expiryFrame + 27;
        const float delta = 1f / 30f;
        using var authorityWorlds = new ServerBattleWorldManager(NullLogger.Instance);
        using var replayWorlds = new ServerBattleWorldManager(NullLogger.Instance);
        using var authority = Start(authorityWorlds, battleId);
        using var replayBootstrap = Start(replayWorlds, battleId);
        var authorityWorld = authorityWorlds.CreateBattleWorld(battleId, 30);
        var replayWorld = replayWorlds.CreateBattleWorld(battleId, 30);
        var authorityClock = authorityWorld.Services.Resolve<IWorldClock>();
        var replayClock = replayWorld.Services.Resolve<IWorldClock>();
        var authorityGuard = authorityWorld.Services.Resolve<MobaCombatFrameGuard>();
        var replayGuard = replayWorld.Services.Resolve<MobaCombatFrameGuard>();
        var authoritySummons = authorityWorld.Services.Resolve<MobaSummonService>();
        var replaySummons = replayWorld.Services.Resolve<MobaSummonService>();
        var authorityState = new RollbackCoordinator(MobaRollbackRegistryBuilder.Create(authorityWorld), new RollbackSnapshotRingBuffer(2));
        var replayState = new RollbackCoordinator(MobaRollbackRegistryBuilder.CreateForPrediction(replayWorld), new RollbackSnapshotRingBuffer(2));
        var frameClock = Assert.IsAssignableFrom<IBattleRuntimeExternalFrameClock>(authority);
        var caster = Assert.Single(authority.CreateStateSyncPush(1, 0, isFullSnapshot: true).Actors);
        var authorityActors = authorityWorld.Services.Resolve<MobaActorRegistry>();
        var replayActors = replayWorld.Services.Resolve<MobaActorRegistry>();
        var casterVersion = authorityActors.GetEntityVersion(1);
        var replayCasterVersion = replayActors.GetEntityVersion(1);
        Assert.Equal(casterVersion, replayCasterVersion);

        using var remote = new FrameJitterBuffer<PlayerInputCommand[]>(0);
        var options = new HostRuntimeOptions();
        var host = new HostRuntime(new ReplayWorldManager(replayWorld), options);
        var limit = 0;
        var prediction = new ClientPredictionDriverModule(_ => remote, _ => null!,
            resolveIdealFrameLimit: _ => limit, maxPredictionAheadFrames: 0, enableRollback: true,
            buildRollbackRegistry: MobaRollbackRegistryBuilder.CreateForPrediction);
        prediction.Install(host, options);
        try
        {
            options.WorldCreated.Invoke(replayWorld);
            Assert.True(prediction.TryRebase(replayWorld.Id, new FrameIndex(0)));
            Assert.True(frameClock.HasInitialFrameCheckpoint);
            Assert.False(frameClock.TryPrepareExternalFrame(0, delta));
            var authorityInitialTicks = authorityGuard.CompletedTickCount;
            var replayInitialTicks = replayGuard.CompletedTickCount;
            ActorEntity? authoritySummon = null;
            ActorEntity? replaySummon = null;
            var summonIds = new List<int>();
            var firstDifferenceByKey = new SortedDictionary<int, int>();
            var replayTail = new Dictionary<int, (PlayerInputCommand[] Commands, WorldRollbackSnapshot Snapshot)>();
            WorldRollbackSnapshot beforeExpiry = default;
            WorldRollbackSnapshot authorityExpiry = default;
            PendingSpawnState spawnState = default;
            var ownerBeforeMove = default(Vec3);
            for (var frame = 0; frame <= lastFrame; frame++)
            {
                var frameIndex = new FrameIndex(frame);
                var inputs = Array.Empty<BattleInputItem>();
                if (frame is pressFrame or pressFrame + 1)
                {
                    var aim = new Vec3(caster.X + 2f, caster.Y, caster.Z);
                    var phase = frame == pressFrame ? SkillInputPhase.Press : SkillInputPhase.Release;
                    var skill = new SkillInputEvent(4, phase, aimPos: in aim, targetFrame: frame,
                        inputSequence: frame, predictionKey: frame, entityVersion: casterVersion);
                    inputs = [new BattleInputItem { PlayerId = 1, OpCode = MobaOpCodes.Input.SkillInput, Payload = SkillInputCodec.Serialize(in skill) }];
                }
                else if (frame is followUpFrame or followUpFrame + 10)
                {
                    inputs = [new BattleInputItem { PlayerId = 1, OpCode = MobaOpCodes.Input.Move,
                        Payload = MobaMoveCodec.Serialize(frame == followUpFrame ? 1f : 0f, 0f) }];
                }

                var commands = DefaultOrleansBattleProtocolMapper.Instance.CreatePlayerInputCommands(frame, inputs).ToArray();
                if (frame > 0)
                {
                    Assert.True(frameClock.TryPrepareExternalFrame(frame, delta));
                    Assert.Equal(inputs.Length, authority.SubmitInputs(frame, inputs));
                    Assert.True(authority.Tick(frame, 30, delta));
                }
                remote.Add(frame, commands);
                limit = frame;
                host.Tick(delta);

                Assert.True(prediction.TryGetFrames(replayWorld.Id, out var confirmed, out var predicted));
                Assert.Equal(frameIndex, confirmed);
                Assert.Equal(frameIndex, predicted);
                Assert.Equal(authorityInitialTicks + frame, authorityGuard.CompletedTickCount);
                Assert.Equal(replayInitialTicks + frame, replayGuard.CompletedTickCount);
                Assert.Equal(authorityClock.Time, replayClock.Time);
                Assert.Equal(authorityWorld.Services.Resolve<IFrameTime>().Time, replayWorld.Services.Resolve<IFrameTime>().Time);
                Assert.Equal(ActiveRoster(authorityActors), ActiveRoster(replayActors));

                if (frame == 1 || frame == pressFrame + 2)
                {
                    var actorId = frame == 1 ? 1 : Assert.Single(summonIds);
                    if (drainAuthority) DrainSpawn(authorityWorld, frameIndex, actorId);
                    if (drainPrediction) DrainSpawn(replayWorld, frameIndex, actorId);
                }

                var authoritySnapshot = authorityState.Capture(frameIndex);
                var replaySnapshot = replayState.Capture(frameIndex);
                Assert.Equal(ClockPayload(authoritySnapshot), ClockPayload(replaySnapshot));
                var replayEntries = replaySnapshot.Entries.ToDictionary(entry => entry.Key);
                foreach (var entry in authoritySnapshot.Entries)
                {
                    if (replayEntries.TryGetValue(entry.Key, out var replayEntry) && !entry.Payload.AsSpan().SequenceEqual(replayEntry.Payload))
                    {
                        Assert.True(frame < expiryFrame, $"Provider {entry.Key} differs at or after expiry (frame {frame}).");
                        firstDifferenceByKey.TryAdd(entry.Key, frame);
                    }
                }

                var alive = authoritySummons.GetAliveCount(1, summonConfig);
                Assert.Equal(frame >= pressFrame && frame < expiryFrame ? 1 : 0, alive);
                Assert.Equal(alive, replaySummons.GetAliveCount(1, summonConfig));
                if (frame == pressFrame)
                {
                    Assert.True(authoritySummons.TryGetSummons(1, summonIds, summonConfig));
                    Assert.True(authorityActors.TryGet(Assert.Single(summonIds), out authoritySummon));
                    Assert.True(replayActors.TryGet(Assert.Single(summonIds), out replaySummon));
                    Assert.Equal(authoritySummon.lifetime.EndTimeMs, replaySummon.lifetime.EndTimeMs);
                    spawnState = ReadPendingSpawn(authoritySnapshot);
                    var entry = Assert.Single(spawnState.Entries);
                    Assert.Equal((Assert.Single(summonIds), (int)SpawnEntityKind.Summon, summonConfig, 1,
                            authorityActors.GetEntityVersion(entry.NetId)),
                        (entry.NetId, entry.Kind, entry.Code, entry.OwnerNetId, entry.EntityVersion));
                    output.WriteLine($"SUMMON_SPAWN_QUEUE Enqueued={entry} NextSequence={spawnState.NextSequence}");
                }

                if (frame == expiryFrame - 1)
                    beforeExpiry = replaySnapshot;

                if (frame >= expiryFrame)
                {
                    replayTail.Add(frame, (commands, replaySnapshot));
                    Assert.False(authorityActors.TryGet(Assert.Single(summonIds), out _));
                    Assert.False(replayActors.TryGet(Assert.Single(summonIds), out _));
                }

                if (frame == expiryFrame)
                {
                    authorityExpiry = authoritySnapshot;
                    var authorityPending = ReadPendingSpawn(authoritySnapshot);
                    var replayPending = ReadPendingSpawn(replaySnapshot);
                    Assert.Equal(expiryFrame, authorityPending.Frame);
                    Assert.Equal(authorityPending.Frame, replayPending.Frame);
                    Assert.Equal(spawnState.NextSequence, authorityPending.NextSequence);
                    Assert.Equal(authorityPending.NextSequence, replayPending.NextSequence);
                    Assert.Empty(replayPending.Entries);
                    Assert.Empty(authorityPending.Entries);
                    output.WriteLine($"SUMMON_EXPIRY_QUEUE DrainAuthority={drainAuthority} DrainPrediction={drainPrediction} " +
                        $"NextSequence={authorityPending.NextSequence} " +
                        $"AuthorityCount={authorityPending.Entries.Length} PredictionCount={replayPending.Entries.Length}");
                }

                if (frame == followUpFrame - 1)
                    ownerBeforeMove = Assert.Single(ActiveRoster(authorityActors), actor => actor.Id == 1).Position;
            }

            Assert.False(authoritySummon!.isEnabled);
            Assert.True(replaySummon!.isEnabled);
            Assert.False(replaySummon.hasActorId);
            Assert.True(replaySummon.hasActorDespawnRequest);
            Assert.NotEqual(ownerBeforeMove, Assert.Single(ActiveRoster(authorityActors), actor => actor.Id == 1).Position);
            output.WriteLine($"COLD_REPLAY_COMPARISON spawn={pressFrame} expiry={expiryFrame} lastFrame={lastFrame} " +
                $"differentCommonProviderKeys={string.Join(',', firstDifferenceByKey.Select(pair => $"{pair.Key}@{pair.Value}"))}");
            if (drainAuthority == drainPrediction)
            {
                Assert.Empty(firstDifferenceByKey);
            }
            else
            {
                Assert.Equal(new KeyValuePair<int, int>(MobaPendingActorSpawnRollbackProvider.DefaultKey, pressFrame + 2),
                    Assert.Single(firstDifferenceByKey));
            }

            Assert.True(authorityState.TryRestore(in authorityExpiry, out var authorityRestored), authorityRestored.ToString());
            output.WriteLine($"SUMMON_EXPIRY_AUTHORITY_RESTORED Frame={authorityExpiry.Frame.Value}");

            Assert.True(replayState.TryRestore(in beforeExpiry, out var restored), restored.ToString());
            Assert.True(replayActors.TryGet(Assert.Single(summonIds), out var restoredSummon));
            Assert.Same(replaySummon, restoredSummon);
            var restoredPending = ReadPendingSpawn(replayState.Capture(beforeExpiry.Frame));
            Assert.Equal(spawnState.NextSequence, restoredPending.NextSequence);
            Assert.Equal(drainPrediction ? Array.Empty<PendingSpawn>() : spawnState.Entries, restoredPending.Entries);
            remote.Clear();
            Assert.True(prediction.TryRebase(replayWorld.Id, beforeExpiry.Frame));
            for (var frame = expiryFrame; frame <= lastFrame; frame++)
            {
                var (commands, expected) = replayTail[frame];
                remote.Add(frame, commands);
                limit = frame;
                host.Tick(delta);
                var frameIndex = new FrameIndex(frame);
                Assert.True(prediction.TryGetFrames(replayWorld.Id, out var confirmed, out var predicted));
                Assert.Equal(frameIndex, confirmed);
                Assert.Equal(frameIndex, predicted);
                Assert.Equal(0, replaySummons.GetAliveCount(1, summonConfig));
                var actual = replayState.Capture(frameIndex);
                Assert.Equal(expected.Entries.Select(entry => entry.Key), actual.Entries.Select(entry => entry.Key));
                for (var index = 0; index < expected.Entries.Length; index++)
                    Assert.True(expected.Entries[index].Payload.AsSpan().SequenceEqual(actual.Entries[index].Payload),
                        $"Replay payload differs at frame {frame}, provider {expected.Entries[index].Key}.");
            }
            output.WriteLine($"SUMMON_EXPIRY_PREDICTION_RESTORED From={beforeExpiry.Frame.Value} ReplayedThrough={lastFrame}");
        }
        finally
        {
            prediction.Uninstall(host, options);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ManualSummonDespawn_CancelsOnlyUnpublishedSpawnAndKeepsDespawnNotification(bool publishSpawn)
    {
        const string battleId = "manual-summon-despawn";
        const float delta = 1f / 30f;
        using var worlds = new ServerBattleWorldManager(NullLogger.Instance);
        using var authority = Start(worlds, battleId);
        var world = worlds.CreateBattleWorld(battleId, 30);
        var frameClock = Assert.IsAssignableFrom<IBattleRuntimeExternalFrameClock>(authority);
        var summons = world.Services.Resolve<MobaSummonService>();
        var actors = world.Services.Resolve<MobaActorRegistry>();
        var state = new RollbackCoordinator(MobaRollbackRegistryBuilder.Create(world), new RollbackSnapshotRingBuffer(2));

        Advance(1);
        DrainSpawn(world, new FrameIndex(1), 1);
        Assert.True(actors.TryGet(1, out var owner));
        var position = owner.transform.Value.Position + Vec3.Right;
        Assert.True(summons.TrySummon(1, 30030502, in position, Vec3.Right, default, out var summonId));
        Advance(2);
        Assert.True(actors.TryGet(summonId, out var summon));
        var queued = ReadPendingSpawn(state.Capture(new FrameIndex(2)));
        Assert.Equal(summonId, Assert.Single(queued.Entries).NetId);
        if (publishSpawn) DrainSpawn(world, new FrameIndex(2), summonId);

        Assert.True(summons.TryDespawn(summonId, SummonDespawnReason.ManualRemove));
        Advance(3);
        Assert.False(actors.TryGet(summonId, out _));
        Assert.False(summon.isEnabled);
        var exitFrame = new FrameIndex(3);
        var exit = state.Capture(exitFrame);
        var pending = ReadPendingSpawn(exit);
        Assert.Empty(pending.Entries);
        Assert.Equal(queued.NextSequence, pending.NextSequence);
        Assert.False(world.Services.Resolve<MobaActorSpawnSnapshotService>().TryGetSnapshot(exitFrame, out _));
        Assert.True(world.Services.Resolve<MobaActorDespawnSnapshotService>().TryGetSnapshot(exitFrame, out var despawn));
        Assert.Equal(MobaOpCodes.Snapshot.ActorDespawn, despawn.OpCode);
        var notification = Assert.Single(MobaActorDespawnSnapshotCodec.Deserialize(despawn.Payload), entry => entry.ActorId == summonId);
        Assert.Equal((byte)ActorDespawnReason.SummonManualRemove, notification.Reason);
        Assert.True(state.TryRestore(in exit, out var restored), restored.ToString());

        void Advance(int frame)
        {
            Assert.True(frameClock.TryPrepareExternalFrame(frame, delta));
            Assert.True(authority.Tick(frame, 30, delta));
        }
    }

    private static byte[] ClockPayload(WorldRollbackSnapshot snapshot) =>
        Assert.Single(snapshot.Entries, entry => entry.Key == MobaWorldClockRollbackProvider.DefaultKey).Payload;

    private static (int Id, int Version, Vec3 Position)[] ActiveRoster(MobaActorRegistry actors) =>
        actors.Entries.OrderBy(entry => entry.Key).Select(entry => (entry.Key, actors.GetEntityVersion(entry.Key),
            entry.Value.hasTransform ? entry.Value.transform.Value.Position : default)).ToArray();

    private static void DrainSpawn(IWorld world, FrameIndex frame, int actorId)
    {
        var emitter = world.Services.Resolve<MobaActorSpawnSnapshotService>();
        Assert.True(emitter.TryGetSnapshot(frame, out var payload));
        Assert.Equal(MobaOpCodes.Snapshot.ActorSpawn, payload.OpCode);
        Assert.Contains(MobaActorSpawnSnapshotCodec.Deserialize(payload.Payload), entry => entry.NetId == actorId);
    }

    private static PendingSpawnState ReadPendingSpawn(WorldRollbackSnapshot snapshot)
    {
        using var reader = new BinaryReader(new MemoryStream(Assert.Single(snapshot.Entries,
            entry => entry.Key == MobaPendingActorSpawnRollbackProvider.DefaultKey).Payload));
        Assert.Equal(1, reader.ReadInt32());
        var frame = reader.ReadInt32();
        var nextSequence = reader.ReadInt64();
        var entries = new PendingSpawn[reader.ReadInt32()];
        for (var index = 0; index < entries.Length; index++)
        {
            entries[index] = new PendingSpawn(reader.ReadInt64(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(),
                reader.ReadInt32(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadInt32());
        }
        Assert.Equal(reader.BaseStream.Length, reader.BaseStream.Position);
        return new PendingSpawnState(frame, nextSequence, entries);
    }

    private readonly record struct PendingSpawnState(int Frame, long NextSequence, PendingSpawn[] Entries);

    private readonly record struct PendingSpawn(long Sequence, int NetId, int Kind, int Code, int OwnerNetId,
        float X, float Y, float Z, int EntityVersion);

    private static IBattleRuntimeSession Start(ServerBattleWorldManager worlds, string battleId)
    {
        var session = new MobaBattleRuntimeAdapter(worlds, DefaultOrleansBattleProtocolMapper.Instance).CreateSession(battleId);
        var result = session.Start(new BattleInitParams
        {
            WorldId = 1, TickRate = 30, MapId = 1, GameplayId = 1, RandomSeed = 12345,
            Players = [new PlayerInitInfo
            {
                PlayerId = 1, ActorId = 1, HeroId = 3003, TeamId = 1, Level = 1, AttributeTemplateId = 3003,
                BasicAttackSkillId = 30030101, SkillIds = [30030201, 30030301, 30030401, 30030501]
            }]
        });
        Assert.True(result.Succeeded, result.Error);
        return session;
    }

    private sealed class ReplayWorldManager(IWorld world) : IWorldManager
    {
        public IReadOnlyDictionary<WorldId, IWorld> Worlds { get; } = new Dictionary<WorldId, IWorld> { [world.Id] = world };
        public bool TryGet(WorldId id, out IWorld result) => Worlds.TryGetValue(id, out result!);
        public void Tick(float deltaTime) => world.Tick(deltaTime);
        public IWorld Create(WorldCreateOptions options) => throw new NotSupportedException();
        public bool Destroy(WorldId id) => throw new NotSupportedException();
        public void DisposeAll() => throw new NotSupportedException();
    }
}
