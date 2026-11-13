using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.Services;
using AbilityKit.Demo.Moba.Acceptance;
using AbilityKit.Demo.Moba.Acceptance.LiveSim;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Console;
using AbilityKit.Demo.Moba.Console.Battle.Config;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Game.Test.UnitTest;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;
using MemoryPack;
using Xunit;
using Xunit.Abstractions;

namespace AbilityKit.Demo.Moba.Tests.StateSync;

/// <summary>Real input, simulation and registry bytes; no state normalization or synthetic world.</summary>
[Collection("Moba lifecycle preparation")]
public sealed class MobaRecordedInputReplayTests(ITestOutputHelper output)
{
    private const float Delta = 1f / 30f;
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, IncludeFields = true };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Annie_q_same_input_replay_restores_exact_registered_state(bool reverseSpawnOrder)
    {
        using var bootstrapper = new ConsoleBattleBootstrapper(BattleStartConfig.CreateDefault());
        var artifacts = Environment.GetEnvironmentVariable("MOBA_REPLAY_ARTIFACT_ROOT")
            ?? Path.Combine(AppContext.BaseDirectory, "artifacts", "moba-recorded-input-replay");
        artifacts = Path.Combine(artifacts, reverseSpawnOrder ? "source-8-target-7" : "source-7-target-8");
        Directory.CreateDirectory(artifacts);
        bootstrapper.Initialize();
        bootstrapper.Start();
        try
        {
            for (var i = 0; i < 8 && bootstrapper.Context.EcsWorld == null; i++) bootstrapper.Tick();
            bootstrapper.SetupBattle();
            for (var i = 0; i < 10; i++) bootstrapper.Tick(Delta);
            var setup = new LiveSimSetupActionExecutor(bootstrapper) { FixedDelta = Delta };
            if (reverseSpawnOrder) Spawn(setup, "target", "player_2", 1001, 2, 4);
            Spawn(setup, "annie", "player_1", 3003, 1, 0);
            if (!reverseSpawnOrder) Spawn(setup, "target", "player_2", 1001, 2, 4);
            Assert.True(setup.TryGetActorId("annie", out var annie));
            Assert.True(setup.TryGetActorId("target", out var target));
            bootstrapper.Tick(Delta);
            var world = (IWorld)typeof(ConsoleBattleBootstrapper)
                .GetField("_runtimeWorld", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(bootstrapper)!;
            var actors = world.Services.Resolve<MobaActorRegistry>();
            var combat = world.Services.Resolve<MobaCombatActivityService>();
            var input = world.Services.Resolve<IMobaInputCoordinator>();
            var replay = world.Services.Resolve<IWorldInputReplaySink>();
            var registry = MobaRollbackRegistryBuilder.Create(world);
            var coordinator = new RollbackCoordinator(registry, new RollbackSnapshotRingBuffer(4));
            var checkpoint = new FrameIndex(bootstrapper.Context.LastFrame);
            var checkpointWorldTime = world.Services.Resolve<IWorldClock>().Time;
            var checkpointWorldDelta = world.Services.Resolve<IWorldClock>().DeltaTime;
            Assert.True(coordinator.CaptureAndStore(checkpoint));
            var checkpointState = coordinator.Capture(checkpoint);
            Assert.True(actors.TryGet(target, out var checkpointTarget));
            var initialRageRatio = checkpointTarget.attributeGroup.Ctx.GetFloat("resource.rage.ratio");
            // A pre-upgrade local snapshot must fail before even structural imports.
            foreach (var missingKey in new[] { MobaCombatActivityRollbackProvider.DefaultKey, MobaWorldClockRollbackProvider.DefaultKey })
            {
                var old = new WorldRollbackSnapshot(checkpointState.Version, checkpoint,
                    checkpointState.Entries.Where(entry => entry.Key != missingKey).ToArray());
                Assert.False(coordinator.TryRestore(in old, out var rejected));
                Assert.Equal(missingKey, rejected.ProviderKey);
                Assert.Equal(0, rejected.ProviderCount);
                Assert.Equal(Hash(checkpointState), Hash(coordinator.Capture(checkpoint)));
            }
            Assert.False(combat.TryGetLastCombatTime(target, out _));
            var initialProcesses = world.Services.Resolve<MobaContinuousManager>().GetAllContinuous()
                .OfType<MobaTriggerIntervalContinuousRuntime>().Where(runtime => runtime.TargetActorId == target)
                .Select(runtime => runtime.Config.Id).ToArray();
            Assert.NotEmpty(initialProcesses);
            SaveSnapshot(artifacts, "checkpoint", checkpointState);
            var endFrame = new FrameIndex(checkpoint.Value + 60);
            var castFrame = new FrameIndex(checkpoint.Value + 4);
            var evt = new SkillInputEvent(1, SkillInputPhase.Press, targetActorId: target,
                predictionKey: 37, targetFrame: castFrame.Value, inputSequence: 1,
                entityVersion: actors.GetEntityVersion(annie));
            // Capture immutable wire bytes once. Both runs deserialize the saved record, including
            // the duplicate packet: the existing action journal must execute the skill only once.
            var payload = SkillInputCodec.Serialize(in evt);
            var record = new[]
            {
                new InputRecord(castFrame.Value, "player_1", MobaOpCodes.Input.SkillInput, Convert.ToBase64String(payload)),
                new InputRecord(castFrame.Value, "player_1", MobaOpCodes.Input.SkillInput, Convert.ToBase64String(payload)),
            };
            var inputPath = Path.Combine(artifacts, "inputs.json");
            File.WriteAllText(inputPath, JsonSerializer.Serialize(record, Json));
            var events = new List<EventRecord>();
            var snapshots = world.Services.Resolve<IWorldStateSnapshotBatchProvider>();
            void CollectEvents(FrameIndex frame)
            {
                var batch = new List<WorldStateSnapshot>();
                snapshots.CollectSnapshots(frame, batch, 128);
                foreach (var snapshot in batch)
                {
                    if (snapshot.OpCode == MobaOpCodes.Snapshot.DamageEvent)
                        events.AddRange(MobaDamageEventSnapshotCodec.Deserialize(snapshot.Payload).Select(entry =>
                            new EventRecord(frame.Value, "damage", JsonSerializer.SerializeToElement(entry, Json))));
                    if (snapshot.OpCode == MobaOpCodes.Snapshot.PresentationCue)
                        events.AddRange(MobaPresentationCueSnapshotCodec.Deserialize(snapshot.Payload).Select(entry =>
                            new EventRecord(frame.Value, "cue", JsonSerializer.SerializeToElement(entry, Json))));
                }
            }
            CollectEvents(checkpoint);
            events.Clear();
            var beforeHp = setup.GetActorHp(target);
            var beforeMana = setup.GetActorMana(annie);
            Advance(bootstrapper, input, null, inputPath, checkpoint, endFrame, CollectEvents);
            var baseline = coordinator.Capture(endFrame);
            SaveSnapshot(artifacts, "baseline", baseline);
            var baselineEvents = events.ToArray();
            File.WriteAllText(Path.Combine(artifacts, "baseline-events.json"), JsonSerializer.Serialize(baselineEvents, Json));
            Assert.True(setup.GetActorHp(target) < beforeHp, "Q must damage the target through real input.");
            Assert.True(setup.GetActorMana(annie) < beforeMana, "Q must consume mana.");
            Assert.Contains(baselineEvents, e => e.Kind == "damage");
            var baselineHp = setup.GetActorHp(target);
            var baselineMana = setup.GetActorMana(annie);
            var baselineWorldTime = world.Services.Resolve<IWorldClock>().Time;
            Assert.True(combat.TryGetLastCombatTime(target, out var afterHitCombatTime));
            var damage = Assert.Single(baselineEvents, e => e.Kind == "damage" &&
                e.Payload.GetProperty("AttackerActorId").GetInt32() == annie &&
                e.Payload.GetProperty("TargetActorId").GetInt32() == target &&
                e.Payload.GetProperty("ReasonParam").GetInt32() == 30030201);
            for (var attempt = 1; attempt <= 2; attempt++)
            {
                // Deliberate predicted divergence, repaired only by the formal restore path.
                Assert.True(actors.TryGet(target, out var targetActor));
                targetActor.attributeGroup.Group.SetBase(MobaAttributeIds.HP, 1f);
                var perturbed = coordinator.Capture(endFrame);
                Assert.NotEqual(Hash(baseline), Hash(perturbed));
                SaveSnapshot(artifacts, $"perturbed-{attempt}", perturbed);
                Assert.True(combat.TryGetLastCombatTime(target, out var combatTimeBeforeRestore));
                var worldTimeBeforeRestore = world.Services.Resolve<IWorldClock>().Time;
                Assert.True(bootstrapper.TryRestoreSimulationFrame(coordinator, checkpoint, out var restored), restored.ToString());
                Assert.Equal(beforeHp, setup.GetActorHp(target));
                Assert.Equal(beforeMana, setup.GetActorMana(annie));
                Assert.Equal(initialRageRatio, targetActor.attributeGroup.Ctx.GetFloat("resource.rage.ratio"));
                var restoredCombatPresent = combat.TryGetLastCombatTime(target, out var restoredCombatTime);
                Assert.False(restoredCombatPresent);
                var restoredWorldTime = world.Services.Resolve<IWorldClock>().Time;
                Assert.Equal(BitConverter.SingleToInt32Bits(checkpointWorldTime), BitConverter.SingleToInt32Bits(restoredWorldTime));
                Assert.Equal(BitConverter.SingleToInt32Bits(checkpointWorldDelta), BitConverter.SingleToInt32Bits(world.Services.Resolve<IWorldClock>().DeltaTime));
                Assert.Equal(Hash(checkpointState), Hash(coordinator.Capture(checkpoint)));
                events.Clear();
                Advance(bootstrapper, input, replay, inputPath, checkpoint, endFrame, CollectEvents);
                var actual = coordinator.Capture(endFrame);
                SaveSnapshot(artifacts, $"replay-{attempt}", actual);
                var differences = Differences(baseline, actual);
                var missingProcesses = initialProcesses.Except(world.Services.Resolve<MobaContinuousManager>()
                    .GetAllContinuous().Select(runtime => runtime.Config.Id)).ToArray();
                var report = new
                {
                    schemaVersion = 1, checkpointFrame = checkpoint.Value, endFrame = endFrame.Value,
                    inputSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(inputPath))),
                    baselineHash = Hash(baseline), perturbedHash = Hash(perturbed), replayHash = Hash(actual),
                    stateEqual = differences.Count == 0, differences,
                    checkpointActors = ActorIds(checkpointState), baselineActors = ActorIds(baseline), replayActors = ActorIds(actual),
                    missingProcesses,
                    combatActivity = new { target, presentAtCheckpoint = false, afterHitCombatTime, combatTimeBeforeRestore, restoredCombatPresent, restoredCombatTime },
                    worldClock = new { checkpointWorldTime, worldTimeBeforeRestore, restoredWorldTime,
                        replayWorldTime = world.Services.Resolve<IWorldClock>().Time },
                    baselineEventCount = baselineEvents.Length, replayEventCount = events.Count,
                    events = events.ToArray(),
                    // Observation is not a promise of externally committed event deduplication.
                    reliableEventDeduplicationVerified = false,
                    verdict = differences.Count == 0 ? "StateEqual_EventDeliveryPending" : "StateMismatch",
                };
                var reportPath = Path.Combine(artifacts, $"report-{attempt}.json");
                File.WriteAllText(reportPath, JsonSerializer.Serialize(report, Json));
                output.WriteLine(reportPath);
                Assert.Empty(differences);
                Assert.Empty(missingProcesses);
                Assert.Equal(Hash(baseline), Hash(actual));
                Assert.Equal(BitConverter.SingleToInt32Bits(baselineWorldTime), BitConverter.SingleToInt32Bits(world.Services.Resolve<IWorldClock>().Time));
                Assert.True(combat.TryGetLastCombatTime(target, out var replayCombatTime));
                Assert.Equal(BitConverter.SingleToInt32Bits(afterHitCombatTime), BitConverter.SingleToInt32Bits(replayCombatTime));
                Assert.Equal(baselineHp, setup.GetActorHp(target));
                Assert.Equal(baselineMana, setup.GetActorMana(annie));
                var replayDamage = Assert.Single(events, e => e.Kind == "damage" &&
                    e.Payload.GetProperty("AttackerActorId").GetInt32() == annie &&
                    e.Payload.GetProperty("TargetActorId").GetInt32() == target &&
                    e.Payload.GetProperty("ReasonParam").GetInt32() == 30030201);
                Assert.Equal(damage.Frame, replayDamage.Frame);
                Assert.Equal(damage.Payload.GetRawText(), replayDamage.Payload.GetRawText());
            }
            Assert.Equal(ActorIds(baseline), ActorIds(checkpointState));
        }
        finally { bootstrapper.Stop(); }
    }

    private static void Spawn(LiveSimSetupActionExecutor setup, string alias, string player, int hero, int team, float x)
        => setup.Execute(new MobaAcceptanceSetupActionExpectation
        {
            action = "spawn_actor", alias = alias, playerId = player, heroId = hero,
            attributeTemplateId = hero, teamId = team,
            position = new MobaAcceptanceVector3Expectation { x = x },
        });

    private static void Advance(ConsoleBattleBootstrapper bootstrapper, IMobaInputCoordinator input,
        IWorldInputReplaySink? replay, string inputPath, FrameIndex start, FrameIndex end, Action<FrameIndex> afterTick)
    {
        var records = JsonSerializer.Deserialize<InputRecord[]>(File.ReadAllText(inputPath), Json)!;
        Assert.All(records, record => Assert.InRange(record.Frame, start.Value + 1, end.Value));
        if (replay != null) replay.BeginReplay(start, end);
        try
        {
            while (bootstrapper.Context.LastFrame < end.Value)
            {
                var frame = new FrameIndex(bootstrapper.Context.LastFrame + 1);
                var commands = records.Where(record => record.Frame == frame.Value).Select(record =>
                    new PlayerInputCommand(frame, new PlayerId(record.Player), record.OpCode, Convert.FromBase64String(record.Payload))).ToArray();
                if (commands.Length != 0)
                {
                    if (replay != null) replay.Replay(frame, commands);
                    else
                    {
                        var submitted = input.TrySubmit(frame, commands);
                        Assert.True(submitted.Succeeded && submitted.HandledCount == commands.Length, submitted.ToString());
                    }
                }
                bootstrapper.Tick(Delta);
                Assert.Equal(frame.Value, bootstrapper.Context.LastFrame);
                afterTick(frame);
            }
        }
        finally { if (replay != null) replay.EndReplay(); }
    }

    private static string Hash(WorldRollbackSnapshot snapshot)
        => Convert.ToHexString(SHA256.HashData(WorldRollbackSnapshotCodec.Serialize(in snapshot)));

    private static void SaveSnapshot(string directory, string name, WorldRollbackSnapshot snapshot)
    {
        var bytes = WorldRollbackSnapshotCodec.Serialize(in snapshot);
        var restored = WorldRollbackSnapshotCodec.Deserialize(bytes);
        Assert.Equal(bytes, WorldRollbackSnapshotCodec.Serialize(in restored));
        File.WriteAllBytes(Path.Combine(directory, name + ".bin"), bytes);
        var hp = MemoryPackSerializer.Deserialize<MobaActorHpRollbackPayload>(snapshot.Entries
            .Single(entry => entry.Key == MobaActorHpRollbackProvider.DefaultKey).Payload);
        var resources = MemoryPackSerializer.Deserialize<MobaActorResourceRollbackPayload>(snapshot.Entries
            .Single(entry => entry.Key == MobaActorResourceRollbackProvider.DefaultKey).Payload);
        File.WriteAllText(Path.Combine(directory, name + "-resources.json"), JsonSerializer.Serialize(new { hp, resources }, Json));
        foreach (var entry in snapshot.Entries.Where(entry => entry.Key == MobaSkillLifecycleRollbackProvider.DefaultKey))
        {
            var checkpoint = MobaSkillLifecycleCheckpointCodec.Decode<MobaSkillLifecycleCheckpoint>(entry.Payload);
            foreach (var section in checkpoint.Sections)
                File.WriteAllBytes(Path.Combine(directory, $"{name}-{section.Name}.bin"), section.Payload);
        }
    }

    private static int[] ActorIds(WorldRollbackSnapshot snapshot)
        => MobaSkillLifecycleCheckpointCodec.Decode<MobaSkillLifecycleCheckpoint>(snapshot.Entries
            .Single(entry => entry.Key == MobaSkillLifecycleRollbackProvider.DefaultKey).Payload).ActorIds;

    private static List<StateDifference> Differences(WorldRollbackSnapshot expected, WorldRollbackSnapshot actual)
    {
        var result = new List<StateDifference>();
        var before = expected.Entries.ToDictionary(entry => entry.Key, entry => entry.Payload);
        var after = actual.Entries.ToDictionary(entry => entry.Key, entry => entry.Payload);
        foreach (var key in before.Keys.Union(after.Keys).OrderBy(key => key))
        {
            before.TryGetValue(key, out var left);
            after.TryGetValue(key, out var right);
            if (left != null && right != null && left.SequenceEqual(right)) continue;
            if (key == MobaSkillLifecycleRollbackProvider.DefaultKey && left != null && right != null)
            {
                var l = MobaSkillLifecycleCheckpointCodec.ReadSections(MobaSkillLifecycleCheckpointCodec.Decode<MobaSkillLifecycleCheckpoint>(left));
                var r = MobaSkillLifecycleCheckpointCodec.ReadSections(MobaSkillLifecycleCheckpointCodec.Decode<MobaSkillLifecycleCheckpoint>(right));
                foreach (var section in l.Keys.Union(r.Keys))
                    if (!l.TryGetValue(section, out var a) || !r.TryGetValue(section, out var b) || !a.SequenceEqual(b))
                        result.Add(new StateDifference(key, section));
                if (!result.Any(entry => entry.Key == key)) result.Add(new StateDifference(key, "envelope"));
            }
            else result.Add(new StateDifference(key, "payload"));
        }
        return result;
    }

    private sealed record InputRecord(int Frame, string Player, int OpCode, string Payload);
    private sealed record EventRecord(int Frame, string Kind, JsonElement Payload);
    private sealed record StateDifference(int Key, string Section);
}
