using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.FrameSync;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.Management;
using AbilityKit.Ability.World.Services;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Network.Runtime;
using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Grains.Battle;
using AbilityKit.Orleans.Grains.Battle.Gameplay;
using AbilityKit.Orleans.Grains.Gameplays.Moba.Battle;
using AbilityKit.Orleans.Grains.Gameplays.Moba.Protocol;
using AbilityKit.Protocol.Moba;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace AbilityKit.Orleans.Grains.Tests.Battle;

public sealed class MobaColdReplayFrameZeroTests(ITestOutputHelper output)
{
    [Fact]
    public void ColdReplay_UsesTheSameUntickedFrameZeroAndSimulationClockAsOrleans()
    {
        const string battleId = "cold-replay-frame-zero";
        const int summonConfig = 30030502;
        const int pressFrame = 3;
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
            var expiryFrame = -1;
            ActorEntity? authoritySummon = null;
            ActorEntity? replaySummon = null;
            var summonIds = new List<int>();
            var firstDifferenceByKey = new SortedDictionary<int, int>();
            for (var frame = 0; frame <= 1200; frame++)
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

                var authoritySnapshot = authorityState.Capture(frameIndex);
                var replaySnapshot = replayState.Capture(frameIndex);
                Assert.Equal(ClockPayload(authoritySnapshot), ClockPayload(replaySnapshot));
                var replayEntries = replaySnapshot.Entries.ToDictionary(entry => entry.Key);
                foreach (var entry in authoritySnapshot.Entries)
                {
                    if (replayEntries.TryGetValue(entry.Key, out var replayEntry) && !entry.Payload.AsSpan().SequenceEqual(replayEntry.Payload))
                        firstDifferenceByKey.TryAdd(entry.Key, frame);
                }

                var alive = authoritySummons.GetAliveCount(1, summonConfig);
                Assert.Equal(alive, replaySummons.GetAliveCount(1, summonConfig));
                if (frame == pressFrame)
                {
                    Assert.Equal(1, alive);
                    Assert.True(authoritySummons.TryGetSummons(1, summonIds, summonConfig));
                    Assert.True(authorityActors.TryGet(Assert.Single(summonIds), out authoritySummon));
                    Assert.True(replayActors.TryGet(Assert.Single(summonIds), out replaySummon));
                    Assert.Equal(authoritySummon.lifetime.EndTimeMs, replaySummon.lifetime.EndTimeMs);
                }

                if (frame > pressFrame && alive == 0)
                {
                    expiryFrame = frame;
                    break;
                }
            }

            Assert.Equal(423, expiryFrame);
            Assert.False(authoritySummon!.isEnabled);
            Assert.True(replaySummon!.isEnabled);
            Assert.False(replaySummon.hasActorId);
            Assert.True(replaySummon.hasActorDespawnRequest);
            output.WriteLine($"COLD_REPLAY_COMPARISON spawn={pressFrame} expiry={expiryFrame} " +
                $"differentCommonProviderKeys={string.Join(',', firstDifferenceByKey.Select(pair => $"{pair.Key}@{pair.Value}"))}");
            Assert.Equal(new KeyValuePair<int, int>(MobaPendingActorSpawnRollbackProvider.DefaultKey, expiryFrame),
                Assert.Single(firstDifferenceByKey));
        }
        finally
        {
            prediction.Uninstall(host, options);
        }
    }

    private static byte[] ClockPayload(WorldRollbackSnapshot snapshot) =>
        Assert.Single(snapshot.Entries, entry => entry.Key == MobaWorldClockRollbackProvider.DefaultKey).Payload;

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
