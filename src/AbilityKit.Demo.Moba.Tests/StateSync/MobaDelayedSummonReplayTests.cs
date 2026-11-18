using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Core.Eventing;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Acceptance;
using AbilityKit.Demo.Moba.Acceptance.LiveSim;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Console;
using AbilityKit.Demo.Moba.Console.Battle.Config;
using AbilityKit.Demo.Moba.Events.Summon;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.StateMachine;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Game.Test.UnitTest;
using AbilityKit.Protocol.Moba;
using MemoryPack;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace AbilityKit.Demo.Moba.Tests.StateSync;

[Collection("Moba lifecycle preparation")]
public sealed class MobaDelayedSummonReplayTests(ITestOutputHelper output)
{
    private const float Delta = 1f / 30f;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Delayed_summons_replay_from_input_or_running_cast_with_stable_identity(bool checkpointRunningCast)
    {
        using var battle = new ConsoleBattleBootstrapper(BattleStartConfig.CreateDefault());
        SkillTimelinePhaseDTO? fixtureTimeline = null;
        SkillTimelineEventDTO[]? originalEvents = null;
        battle.Initialize();
        battle.Start();
        try
        {
            for (var i = 0; i < 8 && battle.Context.EcsWorld == null; i++) battle.Tick();
            battle.SetupBattle();
            for (var i = 0; i < 10; i++) battle.Tick(Delta);
            var world = (IWorld)typeof(ConsoleBattleBootstrapper)
                .GetField("_runtimeWorld", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(battle)!;
            var castSync = FindSystems<MobaSkillCastInstanceSyncSystem>(((IEntitasWorld)world).Systems).Single();
            typeof(MobaSkillCastInstanceSyncSystem).GetField("_retainCompletedFrames", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(castSync, 1000); // Keep the test roster addressable across both input replays.
            var services = world.Services;
            var configs = services.Resolve<MobaConfigDatabase>();
            // Test-only composition of existing Timeline and published summon actions. This
            // deliberately differs from Annie R's production timing; no assets are changed.
            Assert.True(configs.TryGetSkillFlow(30030501, out var flow));
            var timeline = Assert.Single(flow.Phases, phase => phase.Timeline != null).Timeline;
            fixtureTimeline = timeline;
            originalEvents = timeline.Events;
            timeline.Events = new[]
            {
                new SkillTimelineEventDTO { AtMs = 200, EffectId = 30030501, EventTag = "delayed-first" },
                new SkillTimelineEventDTO { AtMs = 600, EffectId = 30030501, EventTag = "delayed-second" },
            };
            // Dispose only clears this library's compiled definitions; rebuild against the fixture.
            Assert.IsType<TableDrivenMobaSkillPipelineLibrary>(services.Resolve<IMobaSkillPipelineLibrary>()).Dispose();
            var setup = new LiveSimSetupActionExecutor(battle) { FixedDelta = Delta };
            setup.Execute(new MobaAcceptanceSetupActionExpectation
            {
                action = "spawn_actor", alias = "caster", playerId = "player_1",
                heroId = 3003, attributeTemplateId = 3003, teamId = 1,
                position = new MobaAcceptanceVector3Expectation { x = 100, z = 100 },
            });
            Assert.True(setup.TryGetActorId("caster", out var caster));
            battle.Tick(Delta);
            var actors = services.Resolve<MobaActorRegistry>();
            var summons = services.Resolve<MobaSummonService>();
            var pending = services.Resolve<MobaActorSpawnSnapshotService>();
            var input = services.Resolve<IMobaInputCoordinator>();
            var replay = services.Resolve<IWorldInputReplaySink>();
            var registry = MobaRollbackRegistryBuilder.Create(world);
            var coordinator = new RollbackCoordinator(registry, new RollbackSnapshotRingBuffer(4));
            var initialActiveSummons = summons.ActiveCount;
            var initialActorIds = actors.CopyActorIdsInOrder().ToArray();
            var inputFrame = battle.Context.LastFrame + 1;
            // Console normally confirms every tick. Hold confirmation through the real
            // authority-source seam to exercise unconfirmed journal capture and replay.
            var authoritySource = new HeldAuthorityFrames(() => battle.Context.LastFrame, inputFrame - 1);
            var authority = services.Resolve<MobaAuthorityFrameService>();
            typeof(MobaAuthorityFrameService).GetField("_authorityFrames", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(authority, authoritySource);
            authority.BindWorld(default);
            var identity = new MobaSummonInputIdentity(caster, actors.GetEntityVersion(caster), 1, inputFrame);
            var evt = new SkillInputEvent(4, SkillInputPhase.Press, aimPos: new Vec3(102, 0, 100),
                aimDir: Vec3.Forward, predictionKey: 37, targetFrame: inputFrame,
                inputSequence: identity.InputSequence, entityVersion: identity.EntityVersion);
            var payload = SkillInputCodec.Serialize(in evt);
            var commands = new[]
            {
                new PlayerInputCommand(new FrameIndex(inputFrame), new PlayerId("player_1"), MobaOpCodes.Input.SkillInput, payload),
                new PlayerInputCommand(new FrameIndex(inputFrame), new PlayerId("player_1"), MobaOpCodes.Input.SkillInput, payload),
            };
            var events = new List<SpawnRecord>();
            using var subscription = services.Resolve<AbilityKit.Triggering.Eventing.IEventBus>().Subscribe(
                new EventKey<SummonEventPayload>(TriggeringIdUtil.GetEventEid(MobaSummonTriggering.Events.Spawned)),
                e => { if (e.OwnerActorId == caster) events.Add(new SpawnRecord(battle.Context.LastFrame, e.SummonActorId)); });

            if (checkpointRunningCast)
            {
                Submit();
                battle.Tick(Delta);
                battle.Tick(Delta);
                Assert.Empty(events);
                Assert.Empty(summons.CaptureRollbackState().Requests);
            }
            var checkpoint = new FrameIndex(battle.Context.LastFrame);
            Assert.True(coordinator.CaptureAndStore(checkpoint));
            var checkpointState = coordinator.Capture(checkpoint);
            AssertInvalidComponentSnapshotsLeaveWholeWorldUnchanged();
            AssertInvalidParameterSnapshotsLeaveWholeWorldUnchanged();
            var end = new FrameIndex(inputFrame + 480);

            var baseline = Advance(false);
            Assert.Equal(2, baseline.Spawns.Length);
            Assert.True(baseline.Spawns[0].Frame > inputFrame);
            Assert.True(baseline.Spawns[1].Frame > baseline.Spawns[0].Frame);
            Assert.NotEqual(baseline.Spawns[0].ActorId, baseline.Spawns[1].ActorId);
            Assert.Equal(new[] { 1, 2 }, baseline.Requests.Select(r => r.Key.Ordinal));
            Assert.Equal(baseline.Spawns.Select(e => e.ActorId), baseline.Requests.Select(r => r.SummonActorId));
            Assert.Equal(baseline.Spawns.Select(e => e.ActorId), baseline.Pending.Select(e => e.ActorId));
            Assert.All(baseline.Requests, r => { Assert.True(r.Accepted); Assert.Equal(identity, r.Key.Identity); });
            Assert.Equal(2, Assert.Single(summons.CaptureRollbackState().NextOrdinals, p => p.Key.Equals(identity)).Value);
            Assert.Equal(initialActiveSummons, summons.ActiveCount);
            Assert.Equal(initialActorIds, actors.CopyActorIdsInOrder());
            Assert.All(baseline.Spawns, e => Assert.False(actors.TryGet(e.ActorId, out _)));
            Assert.Empty(summons.CaptureRollbackState().Requests);

            for (var attempt = 1; attempt <= 2; attempt++)
            {
                authoritySource.ConfirmedFrame = inputFrame - 1;
                Assert.True(battle.TryRestoreSimulationFrame(coordinator, checkpoint, out var restored), restored.ToString());
                Assert.Equal(Hash(checkpointState), Hash(coordinator.Capture(checkpoint)));
                events.Clear();
                var actual = Advance(true);
                Assert.Equal(baseline.Spawns, actual.Spawns);
                Assert.Equal(baseline.Pending, actual.Pending);
                Assert.Equal(baseline.Requests, actual.Requests);
                AssertSameState(baseline.State, actual.State);
                output.WriteLine($"runningCast={checkpointRunningCast}, replay={attempt}, checkpoint={checkpoint.Value}, end={end.Value}, hash={Hash(actual.State)}");
            }

            // Confirmed duplicate packets still traverse the normal action-ack gate. They must
            // not start a new cast or reserve another request ordinal after replay.
            authoritySource.ConfirmedFrame = end.Value;
            battle.Tick(Delta);
            Assert.Empty(summons.CaptureRollbackState().Requests);
            Submit(battle.Context.LastFrame + 1);
            for (var i = 0; i < 45; i++) battle.Tick(Delta);
            Assert.Equal(2, events.Count);
            Assert.Empty(summons.CaptureRollbackState().Requests);
            Assert.Equal(2, Assert.Single(summons.CaptureRollbackState().NextOrdinals, p => p.Key.Equals(identity)).Value);

            void Submit(int? deliveryFrame = null)
            {
                var frame = new FrameIndex(deliveryFrame ?? inputFrame);
                var delivered = commands.Select(c => new PlayerInputCommand(frame, c.Player, c.OpCode, c.Payload)).ToArray();
                var result = input.TrySubmit(frame, delivered);
                Assert.True(result.Succeeded && result.HandledCount == commands.Length, result.ToString());
                var acks = services.Resolve<MobaActionAckSnapshotService>();
                Assert.True(acks.TryGetDecision(caster, identity.EntityVersion, 37, out var decision));
                Assert.True(decision.Accepted, $"Input rejected: {decision}");
            }

            void AssertInvalidComponentSnapshotsLeaveWholeWorldUnchanged()
            {
                foreach (var defect in checkpointRunningCast
                    ? new[] { "v1", "missing-anchor", "future-anchor", "running-request", "trailer" }
                    : new[] { "v1" })
                {
                    var entries = checkpointState.Entries.ToArray();
                    var index = Array.FindIndex(entries, e => e.Key == MobaEntitasComponentRollbackProvider.DefaultKey);
                    var component = MemoryPackSerializer.Deserialize<MobaEntitasComponentRollbackPayload>(entries[index].Payload);
                    if (defect != "v1")
                    {
                        var castIndex = Array.FindIndex(component.Entries, e => e.CastId > 0);
                        Assert.True(castIndex >= 0);
                        var original = component.Entries[castIndex];
                        var temporary = new ActorContext().CreateEntity();
                        try
                        {
                            using (var reader = new BinaryReader(new MemoryStream(original.Data)))
                                ActorComponentSerialization.Restore(temporary, reader);
                            switch (defect)
                            {
                                case "missing-anchor": temporary.RemoveSkillCastRetention(); break;
                                case "future-anchor": temporary.ReplaceSkillCastRetention(checkpoint.Value + 1); break;
                                case "running-request": temporary.AddSkillCastDestroyRequest(checkpoint.Value, checkpoint.Value + 10,
                                    AbilityKit.Demo.Moba.Components.SkillDestroyReason.CompletedRetainExpired); break;
                            }
                            using var stream = new MemoryStream();
                            using var writer = new BinaryWriter(stream);
                            ActorComponentSerialization.Write(temporary, writer);
                            if (defect != "trailer") writer.Write(original.Data, original.Data.Length - 13, 13);
                            component.Entries[castIndex] = new(original.ActorId, original.CastId, stream.ToArray());
                        }
                        finally { temporary.Destroy(); }
                    }
                    entries[index] = new(entries[index].Key, MemoryPackSerializer.Serialize(
                        new MobaEntitasComponentRollbackPayload(defect == "v1" ? 1 : MobaEntitasComponentRollbackProvider.CurrentVersion,
                            component.Entries)));
                    var invalid = new WorldRollbackSnapshot(checkpointState.Version, checkpoint, entries);
                    Assert.False(coordinator.TryRestore(invalid, out var rejected));
                    Assert.Equal(0, rejected.ProviderCount);
                    Assert.Equal(Hash(checkpointState), Hash(coordinator.Capture(checkpoint)));
                    Assert.Equal(checkpoint.Value, battle.Context.LastFrame);
                }
            }

            void AssertInvalidParameterSnapshotsLeaveWholeWorldUnchanged()
            {
                var modifier = AbilityKit.Modifiers.ModifierData.Override(
                    AbilityKit.Modifiers.ModifierKey.FromPacked(1), 1f, sourceId: 37);
                var valid = new MobaCharacterHfsmParameterService.SnapshotEntry(caster, 37, 1, in modifier);
                foreach (var defect in new[] { "owner", "undeclared", "null" })
                {
                    var parameters = JArray.FromObject(new[] { valid, valid });
                    switch (defect)
                    {
                        case "owner": parameters[1]!["OwnerId"] = 0; break;
                        case "undeclared": parameters[1]!["ParameterId"] = int.MaxValue; break;
                        case "null": parameters[1] = null!; break;
                    }
                    var entries = checkpointState.Entries.ToArray();
                    var index = Array.FindIndex(entries, e => e.Key == MobaCharacterHfsmParameterRollbackProvider.DefaultKey);
                    Assert.True(index >= 0);
                    entries[index] = new(entries[index].Key, System.Text.Encoding.UTF8.GetBytes(parameters.ToString()));
                    var invalid = new WorldRollbackSnapshot(checkpointState.Version, checkpoint, entries);
                    Assert.False(coordinator.TryRestore(invalid, out var rejected));
                    Assert.Equal(MobaCharacterHfsmParameterRollbackProvider.DefaultKey, rejected.ProviderKey);
                    Assert.Equal(0, rejected.ProviderCount);
                    Assert.Equal(Hash(checkpointState), Hash(coordinator.Capture(checkpoint)));
                    Assert.Equal(checkpoint.Value, battle.Context.LastFrame);
                }
            }

            RunResult Advance(bool isReplay)
            {
                var observed = new List<PendingRecord>();
                var seen = new HashSet<long>();
                var checkedActiveRestore = false;
                var observedRequests = Array.Empty<MobaSummonRequestResult>();
                if (isReplay) replay.BeginReplay(checkpoint, end);
                try
                {
                    while (battle.Context.LastFrame < end.Value)
                    {
                        var frame = new FrameIndex(battle.Context.LastFrame + 1);
                        if (frame.Value == inputFrame)
                        {
                            if (isReplay) replay.Replay(frame, commands);
                            else Submit();
                        }
                        // Summon lifetime cleanup needs confirmation. Cast expiry is covered by
                        // the focused default-settings tests; this replay keeps the roster stable.
                        if (frame.Value >= end.Value - 1) authoritySource.ConfirmedFrame = frame.Value;
                        battle.Tick(Delta);
                        Assert.Equal(frame.Value, battle.Context.LastFrame);
                        foreach (var entry in pending.CapturePendingRollbackState(frame).Entries)
                        {
                            if (entry.Entry.OwnerNetId == caster && seen.Add(entry.Sequence))
                                observed.Add(new PendingRecord(frame.Value, entry.Sequence, entry.Entry.NetId,
                                    entry.Entry.EntityVersion, entry.Entry.Kind, entry.Entry.Code, entry.Entry.OwnerNetId,
                                    entry.Entry.X, entry.Entry.Y, entry.Entry.Z));
                        }
                        var state = summons.CaptureRollbackState();
                        if (state.Requests.Length > 0) observedRequests = state.Requests;
                        if (state.Requests.Length > 0 && !checkedActiveRestore)
                        {
                            Assert.Single(state.Requests);
                            var live = Assert.Single(state.Entries, e => e.RootOwnerActorId == caster);
                            Assert.Equal(caster, live.OwnerActorId);
                            Assert.True(live.Source.IsValid);
                            Assert.True(live.Retain.IsValid);
                            AssertCachedRequestHasNoEffects(state.Requests[0]);
                            // Existing same-roster preflight must reject before any provider imports.
                            var before = coordinator.Capture(frame);
                            Assert.False(battle.TryRestoreSimulationFrame(coordinator, checkpoint, out var rejected));
                            Assert.Equal(0, rejected.ProviderCount);
                            Assert.Equal(Hash(before), Hash(coordinator.Capture(frame)));
                            Assert.Equal(frame.Value, battle.Context.LastFrame);
                            // Same-roster restore with an accepted summon must restore its cached
                            // result, ordinal allocator, source retain and pending spawn queue.
                            var activeCoordinator = new RollbackCoordinator(registry, new RollbackSnapshotRingBuffer(2));
                            Assert.True(activeCoordinator.CaptureAndStore(frame));
                            var extra = summons.ReserveInputRequest(in identity);
                            summons.RejectInputRequest(in extra);
                            var eventCount = events.Count;
                            Assert.True(battle.TryRestoreSimulationFrame(activeCoordinator, frame, out var activeRestored), activeRestored.ToString());
                            Assert.Equal(eventCount, events.Count);
                            AssertSameState(before, coordinator.Capture(frame));
                            AssertCachedRequestHasNoEffects(state.Requests[0]);
                            checkedActiveRestore = true;
                        }
                    }
                }
                finally { if (isReplay) replay.EndReplay(); }
                Assert.True(checkedActiveRestore, $"A real accepted delayed summon must execute. events={events.Count}, active={summons.ActiveCount}, ordinals={summons.CaptureRollbackState().NextOrdinals.Length}");
                return new RunResult(events.ToArray(), observed.ToArray(), observedRequests, coordinator.Capture(end));
            }

            void AssertCachedRequestHasNoEffects(MobaSummonRequestResult request)
            {
                var frame = new FrameIndex(battle.Context.LastFrame);
                var before = Hash(coordinator.Capture(frame));
                var eventCount = events.Count;
                var ticket = new MobaSummonRequestTicket(request.Key.Identity, request.Key.Ordinal);
                Assert.True(summons.TrySummon(in ticket, caster, 30030502, new Vec3(102, 0, 100),
                    Vec3.Forward, default, out var sameActor));
                Assert.Equal(request.SummonActorId, sameActor);
                Assert.Equal(eventCount, events.Count);
                Assert.Equal(before, Hash(coordinator.Capture(frame)));
            }
        }
        finally
        {
            if (fixtureTimeline != null) fixtureTimeline.Events = originalEvents!;
            battle.Stop();
        }
    }

    private static string Hash(WorldRollbackSnapshot snapshot)
        => Convert.ToHexString(SHA256.HashData(WorldRollbackSnapshotCodec.Serialize(in snapshot)));

    private void AssertSameState(WorldRollbackSnapshot expected, WorldRollbackSnapshot actual)
    {
        foreach (var entry in expected.Entries)
        {
            var other = Assert.Single(actual.Entries, e => e.Key == entry.Key);
            if (!entry.Payload.SequenceEqual(other.Payload) && entry.Key == MobaEntitasEntityRollbackProvider.DefaultKey)
            {
                var json = new JsonSerializerOptions { IncludeFields = true };
                output.WriteLine("Expected entities: " + JsonSerializer.Serialize(MemoryPackSerializer.Deserialize<MobaEntitasEntityRollbackPayload>(entry.Payload), json));
                output.WriteLine("Actual entities: " + JsonSerializer.Serialize(MemoryPackSerializer.Deserialize<MobaEntitasEntityRollbackPayload>(other.Payload), json));
            }
            if (entry.Key == MobaSkillLifecycleRollbackProvider.DefaultKey)
            {
                var left = MobaSkillLifecycleCheckpointCodec.Decode<MobaSkillLifecycleCheckpoint>(entry.Payload);
                var right = MobaSkillLifecycleCheckpointCodec.Decode<MobaSkillLifecycleCheckpoint>(other.Payload);
                foreach (var section in left.Sections)
                    Assert.True(section.Payload.SequenceEqual(Assert.Single(right.Sections, s => s.Name == section.Name).Payload),
                        $"Lifecycle section differs: {section.Name}");
            }
            Assert.True(entry.Payload.SequenceEqual(other.Payload), $"Rollback provider differs: {entry.Key}");
        }
        Assert.Equal(Hash(expected), Hash(actual));
    }

    private sealed record SpawnRecord(int Frame, int ActorId);
    private sealed record PendingRecord(int Frame, long Sequence, int ActorId, int EntityVersion,
        int Kind, int Code, int Owner, float X, float Y, float Z);
    private sealed record RunResult(SpawnRecord[] Spawns, PendingRecord[] Pending,
        MobaSummonRequestResult[] Requests, WorldRollbackSnapshot State);

    private static IEnumerable<T> FindSystems<T>(Entitas.Systems systems)
    {
        var field = typeof(Entitas.Systems).GetField("_executeSystems", BindingFlags.Instance | BindingFlags.NonPublic)!;
        foreach (var child in (IEnumerable<Entitas.IExecuteSystem>)field.GetValue(systems)!)
        {
            if (child is T match) yield return match;
            if (child is Entitas.Systems nested)
                foreach (var nestedMatch in FindSystems<T>(nested)) yield return nestedMatch;
        }
    }

    private sealed class HeldAuthorityFrames(Func<int> predictedFrame, int confirmedFrame) : IWorldAuthorityFramesSource
    {
        public int ConfirmedFrame { get; set; } = confirmedFrame;
        public bool TryGetFrames(WorldId worldId, out FrameIndex confirmed, out FrameIndex predicted)
        {
            confirmed = new FrameIndex(ConfirmedFrame);
            predicted = new FrameIndex(predictedFrame());
            return true;
        }
    }
}
