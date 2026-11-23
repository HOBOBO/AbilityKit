using System.Reflection;
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
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.EntityManager;
using AbilityKit.Demo.Moba.Services.StateSync;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Runtime;
using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Grains.Battle;
using AbilityKit.Orleans.Grains.Battle.Gameplay;
using AbilityKit.Orleans.Grains.Gameplays.Moba.Battle;
using AbilityKit.Orleans.Grains.Gameplays.Moba.Protocol;
using AbilityKit.Protocol.Moba;
using MemoryPack;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace AbilityKit.Orleans.Grains.Tests.Battle;

public sealed partial class MobaColdReplayFrameZeroTests
{
    [Theory]
    [InlineData(4)]
    [InlineData(423)]
    public void InputCorrection_RemovesPredictedSummonBeforeReusingItsIdentity(int predictionEnd)
        => VerifySummonCorrection(predictionEnd);

    [Theory]
    [InlineData("missing-checkpoint", "10009")]
    [InlineData("missing-brain", "10006")]
    [InlineData("wrong-structure-order", "ordered actor journal and lifecycle providers")]
    [InlineData("checkpoint-after-birth", "missing its rollback birth command")]
    [InlineData("forged-birth", "missing its rollback birth command")]
    [InlineData("wrong-owner", "source or owner")]
    [InlineData("missing-retain", "SkillRuntime retain")]
    public void LiveSummonCorrection_RejectsUnprovenBirthBeforeImport(string invalidEvidence, string expectedReason)
        => VerifySummonCorrection(4, invalidEvidence, expectedReason);

    private void VerifySummonCorrection(int predictionEnd, string? invalidEvidence = null, string? expectedReason = null)
    {
        const string battleId = "predicted-summon-correction";
        const int summonConfig = 30030502;
        const int baselineFrame = 2;
        const int pressFrame = baselineFrame + 1;
        const int expiryFrame = 423;
        const int predictionHistory = 512;
        const float delta = 1f / 30f;
        using var predictedWorlds = new ServerBattleWorldManager(NullLogger.Instance);
        using var cleanWorlds = new ServerBattleWorldManager(NullLogger.Instance);
        using var predictedSession = Start(predictedWorlds, battleId);
        using var cleanSession = Start(cleanWorlds, battleId);
        var predictedWorld = predictedWorlds.CreateBattleWorld(battleId, 30);
        var cleanWorld = cleanWorlds.CreateBattleWorld(battleId, 30);
        var predictedActors = predictedWorld.Services.Resolve<MobaActorRegistry>();
        var cleanActors = cleanWorld.Services.Resolve<MobaActorRegistry>();
        var entities = predictedWorld.Services.Resolve<MobaEntityManager>();
        var summons = predictedWorld.Services.Resolve<MobaSummonService>();
        var brains = predictedWorld.Services.Resolve<MobaBrainService>();
        var predictedBrainState = new MobaBrainRollbackProvider(predictedActors, brains);
        var cleanBrainState = new MobaBrainRollbackProvider(cleanActors, cleanWorld.Services.Resolve<MobaBrainService>());
        var ids = predictedWorld.Services.Resolve<ActorIdAllocator>();
        var cleanIds = cleanWorld.Services.Resolve<ActorIdAllocator>();
        var caster = Assert.Single(predictedSession.CreateStateSyncPush(1, 0, isFullSnapshot: true).Actors);

        using var remote = new FrameJitterBuffer<PlayerInputCommand[]>(0);
        using var cleanRemote = new FrameJitterBuffer<PlayerInputCommand[]>(0);
        using var local = new QueuedLocalInputs();
        var options = new HostRuntimeOptions();
        var cleanOptions = new HostRuntimeOptions();
        var host = new HostRuntime(new ReplayWorldManager(predictedWorld), options);
        var cleanHost = new HostRuntime(new ReplayWorldManager(cleanWorld), cleanOptions);
        var limit = 1;
        var prediction = new ClientPredictionDriverModule(_ => remote, _ => local,
            resolveIdealFrameLimit: _ => limit, minPredictionWindow: predictionHistory,
            maxPredictionAheadFrames: predictionHistory, enableRollback: true, rollbackHistoryFrames: predictionHistory,
            buildRollbackRegistry: MobaRollbackRegistryBuilder.CreateForPrediction);
        var clean = new ClientPredictionDriverModule(_ => cleanRemote, _ => null!, maxPredictionAheadFrames: 0,
            enableRollback: true, buildRollbackRegistry: MobaRollbackRegistryBuilder.CreateForPrediction);
        var predictedRegistry = MobaRollbackRegistryBuilder.CreateForPrediction(predictedWorld);
        var predictedState = new RollbackCoordinator(predictedRegistry, new RollbackSnapshotRingBuffer(2));
        var cleanState = new RollbackCoordinator(MobaRollbackRegistryBuilder.CreateForPrediction(cleanWorld), new RollbackSnapshotRingBuffer(2));
        prediction.Install(host, options);
        clean.Install(cleanHost, cleanOptions);
        try
        {
            options.WorldCreated.Invoke(predictedWorld);
            cleanOptions.WorldCreated.Invoke(cleanWorld);
            for (var frame = 1; frame <= baselineFrame; frame++)
                AdvanceBoth(frame, []);

            var expectedActorId = ids.NextId;
            var baseline = predictedState.Capture(new FrameIndex(baselineFrame));
            var baselineSpawn = ReadPendingSpawn(baseline);
            limit = pressFrame;
            local.Enqueue(pressFrame, SkillInput(pressFrame, SkillInputPhase.Press, withoutActionAck: true));
            host.Tick(delta);
            AssertFrames(prediction, predictedWorld, baselineFrame, pressFrame);
            Assert.True(predictedActors.TryGet(expectedActorId, out var predictedSummon));
            Assert.True(entities.TryGetActorEntity(expectedActorId, out var indexed));
            Assert.Same(predictedSummon, indexed);
            Assert.Equal(1, summons.GetAliveCount(1, summonConfig));
            var predictedVersion = predictedActors.GetEntityVersion(expectedActorId);
            var brainId = predictedSummon.actorBrain.BehaviorInstanceId;
            Assert.True(brains.TryGetBehavior(brainId, out var speculativeBrain));
            var rollbackBrainEnds = 0;
            speculativeBrain.OnComplete += _ => rollbackBrainEnds++;
            speculativeBrain.OnInterrupt += (_, _) => rollbackBrainEnds++;
            var born = ReadPendingSpawn(predictedState.Capture(new FrameIndex(pressFrame)));
            var bornEntry = Assert.Single(born.Entries);
            Assert.Equal((expectedActorId, predictedVersion, summonConfig), (bornEntry.NetId, bornEntry.EntityVersion, bornEntry.Code));
            Assert.Equal(baselineSpawn.NextSequence + 1, born.NextSequence);

            local.Enqueue(pressFrame + 1, SkillInput(pressFrame + 1, SkillInputPhase.Release, withoutActionAck: true));
            for (var frame = pressFrame + 1; frame <= predictionEnd; frame++)
            {
                limit = frame;
                host.Tick(delta);
            }

            AssertFrames(prediction, predictedWorld, baselineFrame, predictionEnd);
            Assert.False(prediction.TryGetRestoreFloor(predictedWorld.Id, out _));
            var hasExpired = predictionEnd >= expiryFrame;
            Assert.Equal(!hasExpired, predictedActors.TryGet(expectedActorId, out _));
            Assert.Equal(!hasExpired, entities.TryGetActorEntity(expectedActorId, out _));
            Assert.Equal(!hasExpired, brains.TryGetBehavior(brainId, out _));
            Assert.Equal(hasExpired ? 0 : 1, summons.GetAliveCount(1, summonConfig));
            Assert.True(predictedSummon.isEnabled);
            Assert.Equal(!hasExpired, predictedSummon.hasActorId);
            Assert.Equal(hasExpired, predictedSummon.hasActorDespawnRequest);
            var beforeCorrection = ReadPendingSpawn(predictedState.Capture(new FrameIndex(predictionEnd)));
            Assert.Equal(hasExpired ? Array.Empty<PendingSpawn>() : born.Entries, beforeCorrection.Entries);
            Assert.Equal(born.NextSequence, beforeCorrection.NextSequence);
            Assert.Equal(expectedActorId + 1, ids.NextId);
            output.WriteLine($"SUMMON_PREDICTION Confirmed={baselineFrame} Predicted={predictionEnd} Expired={hasExpired} Actor={expectedActorId} Version={predictedVersion} BrainRuntime={brainId}");
            Assert.Equal(0, rollbackBrainEnds);

            if (invalidEvidence != null)
            {
                Assert.False(hasExpired);
                const int journalKey = MobaActorRollbackCommandRuntime.RollbackProviderKey;
                var target = baseline.Entries.ToDictionary(entry => entry.Key);
                var restoring = predictedState;
                switch (invalidEvidence)
                {
                    case "missing-checkpoint":
                        Assert.True(target.Remove(journalKey));
                        break;
                    case "missing-brain":
                        Assert.True(target.Remove(MobaBrainRollbackProvider.DefaultKey));
                        break;
                    case "wrong-structure-order":
                        var misordered = new RollbackRegistry();
                        foreach (var registeredProvider in predictedRegistry.Providers) misordered.Register(registeredProvider);
                        misordered.SetStructureRestoreOrder(journalKey, MobaEntitasEntityRollbackProvider.DefaultKey,
                            MobaSkillLifecycleRollbackProvider.DefaultKey);
                        restoring = new RollbackCoordinator(misordered, new RollbackSnapshotRingBuffer(2));
                        break;
                    case "checkpoint-after-birth":
                        target[journalKey] = predictedState.Capture(new FrameIndex(predictionEnd)).Entries.Single(entry => entry.Key == journalKey);
                        break;
                    case "forged-birth":
                        Assert.True(predictedRegistry.TryGet(journalKey, out var provider));
                        var field = typeof(CommandRollbackStateProvider).GetField("_log", BindingFlags.Instance | BindingFlags.NonPublic);
                        Assert.NotNull(field);
                        var journal = Assert.IsType<CommandRollbackLog>(field.GetValue(provider));
                        var forgedCheckpoint = journal.CreateCheckpoint();
                        journal.Record(new FrameIndex(pressFrame), MobaActorRollbackCommandRuntime.DestroyCreatedActorCommand,
                            MobaActorRollbackCommandRuntime.PayloadVersion,
                            MemoryPackSerializer.Serialize(new MobaActorRollbackIdPayload(expectedActorId)));
                        target[journalKey] = new WorldRollbackSnapshotEntry(journalKey, CommandJournalCheckpointCodec.Encode(forgedCheckpoint));
                        break;
                    case "wrong-owner":
                        predictedSummon.ReplaceOwnerLink(99, 99);
                        break;
                    case "missing-retain":
                        var runtimes = predictedWorld.Services.Resolve<MobaSkillCastRuntimeService>();
                        var diagnostics = new List<MobaSkillRuntimeDiagnostics>();
                        runtimes.CopyDiagnosticsTo(diagnostics);
                        var parent = Assert.Single(diagnostics, runtime => runtime.Children.Any(IsSummonChild));
                        Assert.True(runtimes.ReleaseChild(parent.Handle.RuntimeId, Assert.Single(parent.Children, IsSummonChild)));
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(invalidEvidence));
                }

                var currentFrame = new FrameIndex(predictionEnd);
                var before = predictedState.Capture(currentFrame);
                var invalid = new WorldRollbackSnapshot(baseline.Version, baseline.Frame, target.Values.ToArray());
                Assert.False(restoring.TryRestore(in invalid, out var denied));
                Assert.Equal(0, denied.ProviderCount);
                Assert.Contains(expectedReason!, denied.Message);
                if (invalidEvidence is "wrong-owner" or "missing-retain")
                {
                    Assert.True(predictedRegistry.TryGet(MobaEntitasEntityRollbackProvider.DefaultKey, out var entityProvider));
                    var entityPayload = target[MobaEntitasEntityRollbackProvider.DefaultKey].Payload;
                    var directDenial = Assert.Throws<InvalidOperationException>(() => entityProvider.Import(baseline.Frame, entityPayload));
                    Assert.Contains(expectedReason!, directDenial.Message);
                }

                Assert.True(predictedSummon.isEnabled);
                Assert.True(predictedActors.TryGet(expectedActorId, out var registered));
                Assert.Same(predictedSummon, registered);
                Assert.True(entities.TryGetActorEntity(expectedActorId, out var indexedAfterDenial));
                Assert.Same(predictedSummon, indexedAfterDenial);
                Assert.True(brains.TryGetBehavior(brainId, out var brainAfterDenial));
                Assert.Same(speculativeBrain, brainAfterDenial);
                Assert.Equal(predictedVersion, predictedActors.GetEntityVersion(expectedActorId));
                Assert.Equal(expectedActorId + 1, ids.NextId);
                Assert.Equal(1, summons.GetAliveCount(1, summonConfig));
                Assert.Equal(0, rollbackBrainEnds);
                AssertFrames(prediction, predictedWorld, baselineFrame, predictionEnd);
                var after = predictedState.Capture(currentFrame);
                Assert.Equal(before.Entries.Select(entry => entry.Key), after.Entries.Select(entry => entry.Key));
                foreach (var entry in before.Entries.Zip(after.Entries)) Assert.Equal(entry.First.Payload, entry.Second.Payload);
                return;

                bool IsSummonChild(MobaSkillRuntimeChildRef child) =>
                    child.Kind == MobaSkillRuntimeChildKind.Summon && child.ChildId == expectedActorId;
            }

            AdvanceBoth(pressFrame, []);
            Assert.Equal(0, prediction.TotalRollbackRestoreFailed);
            Assert.Equal(1, prediction.TotalRollbackCount);
            Assert.Equal(new FrameIndex(baselineFrame), prediction.LastRollbackFrame);
            Assert.Equal(0, rollbackBrainEnds);
            Assert.False(predictedSummon.isEnabled);
            Assert.False(predictedActors.TryGet(expectedActorId, out _));
            Assert.False(entities.TryGetActorEntity(expectedActorId, out _));
            Assert.False(brains.TryGetBehavior(brainId, out _));
            Assert.Equal(0, summons.GetAliveCount(1, summonConfig));
            Assert.Equal(0, predictedActors.GetEntityVersion(expectedActorId));
            Assert.Equal(expectedActorId, ids.NextId);
            var corrected = ReadPendingSpawn(predictedState.Capture(new FrameIndex(pressFrame)));
            Assert.Equal(baselineSpawn.NextSequence, corrected.NextSequence);
            Assert.Equal(baselineSpawn.Entries, corrected.Entries);
            AssertWorldsMatch(pressFrame);

            for (var frame = pressFrame + 1; frame <= predictionEnd; frame++)
            {
                AdvanceBoth(frame, []);
                AssertWorldsMatch(frame);
            }

            host.Tick(delta);
            AssertFrames(prediction, predictedWorld, predictionEnd, predictionEnd);
            Assert.False(prediction.IsReplaying);

            AdvanceBoth(predictionEnd + 1, [SkillInput(predictionEnd + 1, SkillInputPhase.Press)]);
            Assert.True(predictedActors.TryGet(expectedActorId, out var respawned));
            Assert.True(entities.TryGetActorEntity(expectedActorId, out var respawnedIndex));
            Assert.Same(respawned, respawnedIndex);
            Assert.True(cleanActors.TryGet(expectedActorId, out var cleanSummon));
            Assert.Equal(predictedVersion, predictedActors.GetEntityVersion(expectedActorId));
            Assert.Equal(cleanSummon.actorBrain.BehaviorInstanceId, respawned.actorBrain.BehaviorInstanceId);
            Assert.Equal(brainId, respawned.actorBrain.BehaviorInstanceId);
            Assert.True(brains.TryGetBehavior(respawned.actorBrain.BehaviorInstanceId, out var respawnedBrain));
            Assert.NotSame(speculativeBrain, respawnedBrain);
            Assert.Equal(1, summons.GetAliveCount(1, summonConfig));
            var respawn = ReadPendingSpawn(predictedState.Capture(new FrameIndex(predictionEnd + 1)));
            Assert.Equal((bornEntry.Sequence, expectedActorId, predictedVersion),
                (Assert.Single(respawn.Entries).Sequence, respawn.Entries[0].NetId, respawn.Entries[0].EntityVersion));
            AssertWorldsMatch(predictionEnd + 1);
            AdvanceBoth(predictionEnd + 2, [SkillInput(predictionEnd + 2, SkillInputPhase.Release)]);
            AssertWorldsMatch(predictionEnd + 2);
            Assert.Equal(0, rollbackBrainEnds);
            Assert.Equal(1, prediction.TotalRollbackCount);
            Assert.Equal(0, prediction.TotalReplayTimeout);
        }
        finally
        {
            clean.Uninstall(cleanHost, cleanOptions);
            prediction.Uninstall(host, options);
        }

        BattleInputItem SkillInput(int frame, SkillInputPhase phase, bool withoutActionAck = false)
        {
            var aim = new Vec3(caster.X + 2f, caster.Y, caster.Z);
            var skill = new SkillInputEvent(4, phase, aimPos: in aim, targetFrame: frame,
                inputSequence: frame, predictionKey: withoutActionAck ? 0 : frame, entityVersion: predictedActors.GetEntityVersion(1));
            return new BattleInputItem { PlayerId = 1, OpCode = MobaOpCodes.Input.SkillInput, Payload = SkillInputCodec.Serialize(in skill) };
        }

        void AdvanceBoth(int frame, BattleInputItem[] inputs)
        {
            var commands = DefaultOrleansBattleProtocolMapper.Instance.CreatePlayerInputCommands(frame, inputs).ToArray();
            remote.Add(frame, commands);
            cleanRemote.Add(frame, commands);
            limit = frame;
            host.Tick(delta);
            cleanHost.Tick(delta);
            AssertFrames(prediction, predictedWorld, frame, frame);
            AssertFrames(clean, cleanWorld, frame, frame);
        }

        void AssertWorldsMatch(int frame)
        {
            Assert.Equal(ActiveRoster(cleanActors), ActiveRoster(predictedActors));
            Assert.Equal(cleanIds.NextId, ids.NextId);
            var index = new FrameIndex(frame);
            var expected = cleanState.Capture(index).Entries.ToDictionary(entry => entry.Key);
            var actual = predictedState.Capture(index).Entries.ToDictionary(entry => entry.Key);
            Assert.Equal(expected.Keys.Order(), actual.Keys.Order());
            var differences = expected.Values.Where(entry => (frame <= predictionEnd || entry.Key != MobaBrainRollbackProvider.DefaultKey) &&
                    !entry.Payload.AsSpan().SequenceEqual(actual[entry.Key].Payload))
                .Select(entry => entry.Key).Order().ToArray();
            Assert.True(differences.Length == 0, $"Rollback Provider differences at frame {frame}: {string.Join(',', differences)}");
            var expectedBrainHash = new MobaStateHashBuilder(0);
            var actualBrainHash = new MobaStateHashBuilder(0);
            cleanBrainState.AddStateHash(index, ref expectedBrainHash);
            predictedBrainState.AddStateHash(index, ref actualBrainHash);
            Assert.Equal(expectedBrainHash.Value, actualBrainHash.Value);
        }
    }

    private static void AssertFrames(ClientPredictionDriverModule driver, IWorld world, int confirmed, int predicted)
    {
        Assert.True(driver.TryGetFrames(world.Id, out var actualConfirmed, out var actualPredicted));
        Assert.True(actualConfirmed.Value == confirmed && actualPredicted.Value == predicted,
            $"Expected confirmed={confirmed}, predicted={predicted}; actual confirmed={actualConfirmed.Value}, " +
            $"predicted={actualPredicted.Value}, rollbacks={driver.TotalRollbackCount}, restore failures={driver.TotalRollbackRestoreFailed}.");
    }

    private sealed class QueuedLocalInputs : ILocalInputSource<LocalPlayerInputEvent[]>
    {
        private readonly Queue<LocalPlayerInputEvent[]> _inputs = new();

        public int LocalFrame { get; private set; }

        public void Enqueue(int frame, BattleInputItem input)
        {
            LocalFrame = frame;
            _inputs.Enqueue([new LocalPlayerInputEvent(new FrameIndex(frame), new PlayerId(input.PlayerId.ToString()), input.OpCode,
                input.Payload ?? throw new ArgumentException("Skill input payload is required.", nameof(input)))]);
        }

        public bool TryDequeue(out LocalPlayerInputEvent[] input) => _inputs.TryDequeue(out input!);

        public void Dispose() => _inputs.Clear();
    }
}
