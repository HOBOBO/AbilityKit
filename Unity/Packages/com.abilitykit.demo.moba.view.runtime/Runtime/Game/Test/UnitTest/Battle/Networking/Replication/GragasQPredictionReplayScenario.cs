using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.FrameSync;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.Management;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Area;
using AbilityKit.Demo.Moba.Services.Projectile;
using AbilityKit.Demo.Moba.Services.StateSync;
using AbilityKit.Network.Runtime;
using AbilityKit.Protocol.Moba;
using AbilityKit.Trace;

namespace AbilityKit.Game.Test.UnitTest
{
    /// <summary>
    /// 在 .NET 和 Unity 世界中用相同的正式输入、纠偏和结算断言复跑已发布的 Q。
    /// </summary>
    internal static class GragasQPredictionReplayScenario
    {
        internal static void Run(IWorld world, IWorldManager worlds, PlayerId player, int casterId, int targetId,
            string checkpointPhase, bool manualDetonation, bool correct, bool throughCleanup, float fixedDelta, Action<string> report,
            bool correctAfterCleanup = false, bool afterCastEntityCleanup = false)
        {
            var services = world.Services;
            var actors = services.Resolve<MobaActorRegistry>();
            var gate = services.Resolve<MobaLogicWorldRunGateService>();
            var frameTime = services.Resolve<IFrameTime>();
            var links = services.Resolve<MobaProjectileLinkService>();
            var areas = services.Resolve<MobaAreaRuntimeService>();
            var trace = services.Resolve<MobaTraceRegistry>();
            var calculator = new MobaAuthoritativeStateHashCalculator();
            var observations = new RollbackCoordinator(MobaRollbackRegistryBuilder.CreateForPrediction(world),
                new RollbackSnapshotRingBuffer(4));
            using var remote = new FrameJitterBuffer<PlayerInputCommand[]>(0);
            var options = new HostRuntimeOptions();
            var host = new HostRuntime(worlds, options);
            var limit = frameTime.Frame.Value;
            var mismatch = -1;
            uint BattleHash() => calculator.Compute(gate.InGame, actors);
            var prediction = new ClientPredictionDriverModule(_ => remote, _ => null,
                resolveIdealFrameLimit: _ => limit, minPredictionWindow: 4, enableRollback: true,
                buildRollbackRegistry: MobaRollbackRegistryBuilder.CreateForPrediction,
                buildComputeHash: _ => frame => new WorldStateHash(BattleHash() ^ (frame.Value == mismatch ? 1u : 0u)));
            prediction.Install(host, options);
            try
            {
                options.WorldCreated.Invoke(world);
                Assert.True(prediction.TryRebase(world.Id, frameTime.Frame));
                Assert.True(actors.TryGet(targetId, out var target));
                var initialHp = new MobaAttrs(target).Hp;
                var aimPoint = target.transform.Value.Position;
                Action<int> captureTail = null;
                Push(SkillInputPhase.Press, 1, aimPoint);
                Push(SkillInputPhase.Release, 2, aimPoint);
                Until(() => Phase() == checkpointPhase, 75, checkpointPhase);

                var checkpointBarrel = Assert.Single(FindBarrels());
                if (afterCastEntityCleanup) WaitForCastEntityCleanup();
                var first = limit;
                var last = first + 3;
                var checkpointEntityVersion = actors.GetEntityVersion(checkpointBarrel.ActorId);
                var expected = new Dictionary<int, (WorldRollbackSnapshot Snapshot, uint Hash)>();
                expected.Add(first, Observe(first));
                for (var frame = first + 1; frame <= last; frame++)
                {
                    mismatch = (correct && frame <= first + 2) || (correctAfterCleanup && frame == first + 1)
                        ? frame : -1;
                    Push();
                    Assert.Equal(checkpointPhase, Phase());
                    expected.Add(frame, Observe(frame));
                }
                if (afterCastEntityCleanup && correctAfterCleanup)
                    captureTail = frame => expected.Add(frame, Observe(frame));

                if (correct)
                {
                    mismatch = first + 2;
                    Correct(first, 1, last);
                    mismatch = -1;
                    Correct(first + 1, 2, last);
                }
                mismatch = -1;
                Assert.Equal(initialHp, new MobaAttrs(target).Hp);
                Assert.Equal(0, DamageCount());
                report($"Gragas Q {checkpointPhase}: checkpoints={first},{first + 1}; last={last}; " +
                    $"rollbacks={prediction.TotalRollbackCount}; failures={prediction.TotalRollbackRestoreFailed}; " +
                    $"hash={expected[last].Hash:X8}");
                if (!throughCleanup) return;
                Until(() => Phase() == "deployed", 75, "deployed");
                var deployed = new List<MobaAreaRuntimeInfo>();
                areas.TryGetAreas(deployed, casterId, 42050201);
                var barrelArea = Assert.Single(deployed);
                Assert.True(Vec3.Distance(barrelArea.Center, aimPoint) < barrelArea.Radius,
                    $"Q area {barrelArea.Center} (radius {barrelArea.Radius}) misses target {aimPoint}");
                if (manualDetonation)
                {
                    for (var step = 0; step < 22; step++) Push();
                    Push(SkillInputPhase.Press, 3);
                    Push(SkillInputPhase.Release, 4);
                }
                Until(() => Phase() == "detonating", manualDetonation ? 24 : 145, "detonating");
                Until(() => DamageCount() > 0, 15, "Q damage");
                var afterHit = new MobaAttrs(target).Hp;
                Assert.True(afterHit < initialHp, $"No Q damage on target {targetId}");
                Until(() => Phase() == "none", 35, "cleanup");
                for (var frame = 0; frame < 15; frame++) Push();
                Assert.Equal(afterHit, new MobaAttrs(target).Hp);
                Assert.Empty(FindAreas(42050201));
                Assert.Equal(1, DamageCount());
                report($"Gragas Q cleanup: HP={initialHp}->{afterHit}; manual={manualDetonation}; hits={DamageCount()}");
                if (correctAfterCleanup && afterCastEntityCleanup) ReplayAfterCleanup();
                else if (correctAfterCleanup) CheckCleanupBoundary();

                void ReplayAfterCleanup()
                {
                    var end = limit;
                    Assert.False(actors.TryGet(checkpointBarrel.ActorId, out _));
                    Correct(first, 1, end, () =>
                    {
                        Assert.Equal("deployed", Phase());
                        var restored = Assert.Single(FindBarrels());
                        Assert.Equal(checkpointBarrel.ActorId, restored.ActorId);
                        Assert.Equal(checkpointEntityVersion, actors.GetEntityVersion(restored.ActorId));
                        Assert.Equal(checkpointBarrel.ProjectileId, restored.ProjectileId);
                        Assert.Equal(checkpointBarrel.Source.SourceContextId, restored.Source.SourceContextId);
                        Assert.Equal(checkpointBarrel.Source.SkillRuntimeHandle, restored.Source.SkillRuntimeHandle);
                        Assert.Equal(barrelArea.AreaId, Assert.Single(FindAreas(42050201)).AreaId);
                        Assert.Equal(initialHp, new MobaAttrs(target).Hp);
                        Assert.Equal(0, DamageCount());
                    });
                    Assert.Equal(afterHit, new MobaAttrs(target).Hp);
                    Assert.Equal(1, DamageCount());
                    Assert.False(actors.TryGet(checkpointBarrel.ActorId, out _));
                    Assert.Empty(FindAreas(42050201));
                    report($"Gragas Q late checkpoint replay: restored={first}; replayedThrough={end}; " +
                        $"rollbacks={prediction.TotalRollbackCount}; failures={prediction.TotalRollbackRestoreFailed}; " +
                        $"HP={initialHp}->{afterHit}; hits={DamageCount()}");
                }

                void WaitForCastEntityCleanup()
                {
                    var contexts = (global::Contexts)services.Resolve<global::Entitas.IContexts>();
                    var castEntities = contexts.actor.GetGroup(ActorMatcher.SkillCastInstanceId);
                    var castId = Assert.Single(castEntities.GetEntities().Where(entity => entity.hasSkillCastOwnerActorId &&
                        entity.skillCastOwnerActorId.Value == casterId && entity.hasSkillCastSkillId &&
                        entity.skillCastSkillId.Value == 30050201)).skillCastInstanceId.Value;
                    var deployedAt = limit;
                    bool CastPresent() => castEntities.GetEntities().Any(entity => entity.skillCastInstanceId.Value == castId);
                    Until(() => !CastPresent() || Phase() != "deployed", 140, "Cast Entity cleanup during deployment");
                    Assert.True(!CastPresent() && Phase() == "deployed",
                        $"No natural checkpoint: Cast Entity {castId} present={CastPresent()}, phase={Phase()}, frame={limit}.");
                    var current = Assert.Single(FindBarrels());
                    Assert.Equal(checkpointBarrel.ActorId, current.ActorId);
                    Assert.Equal(checkpointBarrel.ProjectileId, current.ProjectileId);
                    var handle = current.Source.SkillRuntimeHandle;
                    Assert.True(handle.IsValid && services.Resolve<MobaSkillCastRuntimeService>().TryGet(in handle, out _),
                        "The barrel's original cast runtime must still be addressable.");
                    Assert.True(links.TryGetRetain(current.ProjectileId, out var retain) && retain.Runtime.Equals(handle),
                        "The barrel must still retain its original cast runtime.");
                    Assert.True(services.Resolve<MobaExecutionContextRegistry>().TryGet(current.Source.SourceContextId, out var source) &&
                        !source.IsEnded, "The barrel's execution source must still be live.");
                    Assert.Single(FindAreas(42050201));
                    report($"Gragas Q late deployment: deployed={deployedAt}; castEntity={castId} removed at {limit}; " +
                        $"projectile={current.ProjectileId}; actor={current.ActorId}; runtime={handle.RuntimeId}; source={source.ContextId}");
                }

                void CheckCleanupBoundary()
                {
                    Assert.False(actors.TryGet(checkpointBarrel.ActorId, out _));
                    var runtimes = services.Resolve<MobaSkillCastRuntimeService>();
                    var castHandle = checkpointBarrel.Source.SkillRuntimeHandle;
                    Assert.True(castHandle.IsValid);
                    var castSurvives = runtimes.TryGet(in castHandle, out _);
                    var end = limit;
                    var before = Observe(end);
                    var restored = observations.TryRestore(expected[first].Snapshot, out var rejection);
                    report($"Gragas Q post-cleanup {checkpointPhase}: target={first}; live={end}; " +
                        $"actor={checkpointBarrel.ActorId}; castSurvives={castSurvives}; {rejection}");
                    Assert.True(!restored, $"Post-cleanup restore unexpectedly succeeded: {rejection}");
                    Assert.Equal(RollbackOperationStatus.ProviderFailed, rejection.Status);
                    Assert.Equal(MobaEntitasEntityRollbackProvider.DefaultKey, rejection.ProviderKey);
                    Assert.Equal(0, rejection.ProviderCount);
                    Assert.True(castSurvives, "The retained runtime should outlive the removed cast entity.");
                    Assert.True(rejection.Message.Contains("Confirmed skill cast ") &&
                        rejection.Message.Contains("was destroyed before rollback"), rejection.Message);
                    Compare(before, end);

                    prediction.OnAuthoritativeStateHash(world.Id, new FrameIndex(first + 1),
                        new WorldStateHash(expected[first + 1].Hash));
                    Assert.Equal(1L, prediction.TotalRollbackRestoreFailed);
                    Assert.Equal(0L, prediction.TotalRollbackCount);
                    Assert.False(prediction.IsReplaying);
                    remote.Add(++limit, Array.Empty<PlayerInputCommand>());
                    host.Tick(fixedDelta);
                    Assert.True(prediction.TryGetFrames(world.Id, out var confirmed, out var predicted));
                    Assert.Equal(end, confirmed.Value);
                    Assert.Equal(end, predicted.Value);
                    Assert.Equal(end, frameTime.Frame.Value);
                    Assert.Equal(afterHit, new MobaAttrs(target).Hp);
                    Assert.Equal(1, DamageCount());
                    Assert.False(actors.TryGet(checkpointBarrel.ActorId, out _));
                    Assert.Empty(FindAreas(42050201));
                    Compare(before, end);
                    report($"Gragas Q prediction held at {end}; restore failures={prediction.TotalRollbackRestoreFailed}; " +
                        $"replays={prediction.TotalRollbackCount}; hits={DamageCount()}");
                }

                void Correct(int checkpoint, int count, int replayThrough, Action restored = null)
                {
                    prediction.OnAuthoritativeStateHash(world.Id, new FrameIndex(checkpoint + 1),
                        new WorldStateHash(expected[checkpoint + 1].Hash));
                    Assert.True(prediction.TotalRollbackRestoreFailed == 0,
                        $"{checkpointPhase} restore rejected at {checkpoint} (attempt {count})");
                    Assert.Equal(count, prediction.TotalRollbackCount);
                    Assert.Equal(checkpoint, prediction.LastRollbackFrame.Value);
                    Assert.True(prediction.IsReplaying);
                    Compare(expected[checkpoint], checkpoint);
                    restored?.Invoke();
                    for (var frame = checkpoint + 1; frame <= replayThrough; frame++)
                    {
                        Advance(frame);
                        Compare(expected[frame], frame);
                    }
                    host.Tick(fixedDelta);
                    Assert.False(prediction.IsReplaying);
                }

                void Compare((WorldRollbackSnapshot Snapshot, uint Hash) baseline, int frame)
                {
                    var actual = Observe(frame);
                    Assert.True(baseline.Hash == actual.Hash, $"Authority hash diverged at frame {frame} ({checkpointPhase})");
                    Assert.True(baseline.Snapshot.Entries.Select(entry => entry.Key).SequenceEqual(actual.Snapshot.Entries.Select(entry => entry.Key)),
                        $"Provider keys diverged at frame {frame} ({checkpointPhase})");
                    for (var index = 0; index < actual.Snapshot.Entries.Length; index++)
                    {
                        var expectedEntry = baseline.Snapshot.Entries[index];
                        Assert.True(expectedEntry.Payload.AsSpan().SequenceEqual(actual.Snapshot.Entries[index].Payload),
                            $"Provider {expectedEntry.Key} diverged at frame {frame} ({checkpointPhase})");
                    }
                }

                (WorldRollbackSnapshot Snapshot, uint Hash) Observe(int frame) =>
                    (observations.Capture(new FrameIndex(frame)), BattleHash());

                void Push(SkillInputPhase? phase = null, long sequence = 0, Vec3 aim = default)
                {
                    var frame = new FrameIndex(++limit);
                    var commands = Array.Empty<PlayerInputCommand>();
                    if (phase.HasValue)
                    {
                        var input = new SkillInputEvent(1, phase.Value, aimPos: in aim, predictionKey: (int)sequence,
                            targetFrame: frame.Value, inputSequence: sequence, entityVersion: actors.GetEntityVersion(casterId));
                        commands = new[] { new PlayerInputCommand(frame, player, MobaOpCodes.Input.SkillInput, SkillInputCodec.Serialize(in input)) };
                    }
                    remote.Add(frame.Value, commands);
                    Advance(frame.Value);
                    captureTail?.Invoke(frame.Value);
                }

                void Advance(int frame)
                {
                    host.Tick(fixedDelta);
                    Assert.True(prediction.TryGetFrames(world.Id, out var confirmed, out var predicted));
                    Assert.True(predicted.Value == frame && confirmed.Value == frame,
                        $"Expected frame {frame}, reached {confirmed.Value}/{predicted.Value}, phase={Phase()}");
                }

                string Phase()
                {
                    var barrels = FindBarrels();
                    if (barrels.Length == 0) return "none";
                    Assert.Single(barrels);
                    Assert.True(actors.TryGet(barrels[0].ActorId, out var actor));
                    return actor.actorStateMachine.Runtime.State.ActiveStatePath.TrimStart('/');
                }

                MobaProjectileLinkDiagnostics[] FindBarrels()
                {
                    var entries = new List<MobaProjectileLinkDiagnostics>();
                    links.CopyDiagnosticsTo(entries);
                    return entries.Where(entry => entry.Source.SourceActorId == casterId &&
                        entry.Source.ProjectileConfigId == 32050201).ToArray();
                }

                List<MobaAreaRuntimeInfo> FindAreas(int template)
                {
                    var instances = new List<MobaAreaRuntimeInfo>();
                    areas.TryGetAreas(instances, casterId, template);
                    return instances;
                }

                int DamageCount() => trace.GetNodesByKind((int)MobaExecutionKind.DamageApply)
                    .Count(node => node.Metadata?.ConfigId == 30050201);

                void Until(Func<bool> condition, int maxFrames, string stage)
                {
                    for (var step = 0; step < maxFrames && !condition(); step++) Push();
                    Assert.True(condition(), $"Q did not reach {stage} by frame {limit}; phase={Phase()}, " +
                        $"areas={FindAreas(42050201).Count}, hp={new MobaAttrs(target).Hp}/{initialHp}, damage={DamageCount()}, " +
                        $"target-now={target.transform.Value.Position}, aim={aimPoint}, " +
                        $"effects={string.Join(",", trace.GetNodesByKind((int)MobaExecutionKind.EffectExecution).Select(n => n.Metadata?.ConfigId))}, " +
                        $"warnings={string.Join(" | ", services.Resolve<IMobaBattleDiagnosticsService>().GetWarningsSnapshot().Select(warning => warning.Message))}");
                }
            }
            finally
            {
                prediction.Uninstall(host, options);
            }
        }

        private static class Assert
        {
            internal static void True(bool condition, string message = "Expected true.")
            {
                if (!condition) throw new InvalidOperationException(message);
            }

            internal static void False(bool condition) => True(!condition, "Expected false.");

            internal static void Equal<T>(T expected, T actual) =>
                True(EqualityComparer<T>.Default.Equals(expected, actual), $"Expected {expected}; got {actual}.");

            internal static T Single<T>(IEnumerable<T> items) => items.Single();
            internal static void Empty<T>(IEnumerable<T> items) => True(!items.Any(), "Expected an empty collection.");
        }
    }
}
