using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.FrameSync;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.World.Services;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Network.Runtime;
using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Grains.Battle;
using AbilityKit.Orleans.Grains.Battle.Gameplay;
using AbilityKit.Orleans.Grains.Gameplays.Moba.Battle;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.SettledFrames;
using AbilityKit.Protocol.Moba.StateSync;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AbilityKit.Orleans.Grains.Tests.Battle;

public sealed partial class MobaColdReplayFrameZeroTests
{
    [Theory]
    [InlineData(0, false, false)]
    [InlineData(1, false, false)]
    [InlineData(0, true, false)]
    [InlineData(1, true, false)]
    [InlineData(1, false, true)]
    public void SettledTimelineReevaluatesOriginalSuffixWhenExecutionListsMatch(int inputEpoch, bool mismatchAtFourth, bool boundaryExpired)
    {
        const string battleId = "settled-timeline-suffix";
        const int tickRate = 30;
        const float delta = 1f / tickRate;
        const int summonConfig = 30030502;
        using var authorityWorlds = new ServerBattleWorldManager(NullLogger.Instance);
        using var clientWorlds = new ServerBattleWorldManager(NullLogger.Instance);
        using var authority = Start(authorityWorlds, battleId);
        using var client = Start(clientWorlds, battleId);
        var authorityWorld = authorityWorlds.CreateBattleWorld(battleId, tickRate);
        var clientWorld = clientWorlds.CreateBattleWorld(battleId, tickRate);
        var settled = Assert.IsAssignableFrom<IMobaSettledFrameRuntime>(authority);
        var authorityAcks = authorityWorld.Services.Resolve<MobaActionAckSnapshotService>();
        var clientAcks = clientWorld.Services.Resolve<MobaActionAckSnapshotService>();
        var summons = clientWorld.Services.Resolve<MobaSummonService>();
        var guard = clientWorld.Services.Resolve<MobaCombatFrameGuard>();
        var actorVersion = authorityWorld.Services.Resolve<MobaActorRegistry>().GetEntityVersion(1);
        var caster = Assert.Single(authority.CreateStateSyncPush(1, 0, isFullSnapshot: true).Actors);
        var identity = new WireSettledIdentity
        {
            Version = SettledFrameProtocol.Version, RoomId = 1, WorldId = 1, BattleId = battleId,
            RuntimeGeneration = Guid.NewGuid().ToString("N")
        };
        using var remote = new MobaSettledFrameReceiver(new WireSettledSubscribeResponse
        {
            Identity = identity, TickRate = tickRate, LastSettledFrame = 2
        });
        remote.ReceiveCatchUp(new WireSettledCatchUpResponse
        {
            Frames = [CreateWire(0, [], [], []), TickAuthority(1, []), TickAuthority(2, [])]
        }, -1, 2);
        using var local = new QueuedLocalInputs();
        var options = new HostRuntimeOptions();
        var outbound = 0;
        options.OnBeforeSendMessage = (_, _) => outbound++;
        var host = new HostRuntime(new ReplayWorldManager(clientWorld), options);
        var limit = 2;
        var driver = new ClientPredictionDriverModule(null!, _ => local,
            resolveIdealFrameLimit: _ => limit, minPredictionWindow: 8, maxPredictionAheadFrames: 8,
            enableRollback: true, rollbackHistoryFrames: 16,
            buildRollbackRegistry: MobaRollbackRegistryBuilder.CreateForPrediction,
            bufferOptions: boundaryExpired ? new ClientPredictionDriverBufferOptions(
                ClientPredictionDriverBufferFeatures.AppliedInputHistory | ClientPredictionDriverBufferFeatures.RollbackSnapshots,
                inputHistoryCapacity: 16, rollbackSnapshotCapacity: 1) : null!, resolveSettledFrames: _ => remote);
        driver.Install(host, options);
        try
        {
            options.WorldCreated.Invoke(clientWorld);
            for (var frame = 1; frame <= 2; frame++)
            {
                host.Tick(delta);
            }
            AssertFrames(driver, clientWorld, 2, 2);

            var futureAck = new MobaActionAckEntry
            {
                ActorId = 1, EntityVersion = actorVersion, PredictionKey = 37, AuthoritativeFrame = 4,
                InterruptEpoch = 97, AuthoritySequence = 999
            };
            clientAcks.ApplyAuthoritative([futureAck]);
            Assert.Equal(0, clientAcks.GetInterruptEpoch(1, actorVersion));
            Assert.False(clientAcks.TryGetDecision(1, actorVersion, 37, out _));

            limit = 4;
            host.Tick(delta);
            var aim = new Vec3(caster.X + 2f, caster.Y, caster.Z);
            var skill = new SkillInputEvent(4, SkillInputPhase.Press, aimPos: in aim, targetFrame: 4,
                inputSequence: 1, predictionKey: 37, entityVersion: actorVersion, interruptEpoch: inputEpoch);
            var bytes = SkillInputCodec.Serialize(in skill);
            var input = new BattleInputItem { PlayerId = 1, OpCode = MobaOpCodes.Input.SkillInput, Payload = bytes };
            local.Enqueue(4, input);
            host.Tick(delta);
            AssertFrames(driver, clientWorld, 2, 4);
            Assert.Equal(inputEpoch == 0 ? 1 : 0, summons.GetAliveCount(1, summonConfig));

            Assert.True(Assert.IsAssignableFrom<IBattleRuntimeExternalFrameClock>(authority).TryPrepareExternalFrame(3, delta));
            using (var capture = authorityWorld.Services.Resolve<MobaInputCoordinator>().BeginAuthorityFrameCapture(new FrameIndex(3)))
            {
                Assert.True(capture.Submit([]));
                Assert.Equal(1, authorityAcks.AdvanceInterruptEpoch(1, actorVersion, 3));
                Assert.True(authority.Tick(3, tickRate, delta));
                Assert.True(capture.TryComplete(Assert.IsAssignableFrom<IBattleRuntimeFrameCompletion>(authority).CanFinalizeFrame(3),
                    out var outcomes, out var entries));
                Assert.Empty(outcomes);
                Assert.Equal(0, Assert.Single(entries).Ordinal);
                Receive(CreateWire(3, [], outcomes, entries));
            }

            var completedBeforeCorrection = guard.CompletedTickCount;
            host.Tick(delta);
            if (boundaryExpired)
            {
                AssertFrames(driver, clientWorld, 2, 4);
                Assert.Equal(1, driver.TotalRollbackRestoreFailed);
                Assert.Equal(0, clientAcks.GetInterruptEpoch(1, actorVersion));
                Assert.True(remote.TryGet(3, out _));
                host.Tick(delta);
                Assert.Equal(completedBeforeCorrection, guard.CompletedTickCount);
                return;
            }
            AssertFrames(driver, clientWorld, 3, 3);
            Assert.Equal(completedBeforeCorrection, guard.CompletedTickCount);
            Assert.Equal(new FrameIndex(3), driver.LastRollbackFrame);
            Assert.Equal(1, clientAcks.GetInterruptEpoch(1, actorVersion));
            Assert.Equal(0, summons.GetAliveCount(1, summonConfig));

            var submittedEpoch = mismatchAtFourth ? 1 - inputEpoch : inputEpoch;
            var submittedSkill = new SkillInputEvent(4, SkillInputPhase.Press, aimPos: in aim, targetFrame: 4,
                inputSequence: 1, predictionKey: 37, entityVersion: actorVersion, interruptEpoch: submittedEpoch);
            var fourth = TickAuthority(4, [new BattleInputItem
            {
                PlayerId = 1, OpCode = MobaOpCodes.Input.SkillInput, Payload = SkillInputCodec.Serialize(in submittedSkill)
            }]);
            Array.Clear(bytes, 0, bytes.Length);
            clientAcks.ApplyAuthoritative([futureAck, futureAck]);
            host.Tick(delta);
            AssertFrames(driver, clientWorld, 3, 4);
            Assert.Equal(completedBeforeCorrection + 1, guard.CompletedTickCount);
            Assert.Equal(inputEpoch, summons.GetAliveCount(1, summonConfig));
            Assert.Equal(0, outbound);

            var repeated = new SkillInputEvent(4, SkillInputPhase.Press, aimPos: in aim, targetFrame: 5,
                inputSequence: 2, predictionKey: 37, entityVersion: actorVersion, interruptEpoch: inputEpoch);
            var rejectedPredictedSummon = inputEpoch == 1 && submittedEpoch == 0;
            if (rejectedPredictedSummon)
            {
                Assert.Equal((byte)MobaFrameInputDisposition.Rejected, Assert.Single(fourth.Commands).Disposition);
                Assert.False(Assert.Single(fourth.Decisions).Accepted);
                Assert.Single(SummonIdentities(clientWorld));
                Assert.Empty(SummonIdentities(authorityWorld));
            }

            var fifth = TickAuthority(5, [new BattleInputItem
            {
                PlayerId = 1, OpCode = MobaOpCodes.Input.SkillInput, Payload = SkillInputCodec.Serialize(in repeated)
            }]);
            Receive(fifth);
            host.Tick(delta);
            AssertFrames(driver, clientWorld, 3, 4);
            Receive(fourth);
            host.Tick(delta);
            AssertFrames(driver, clientWorld, 4, 4);
            Assert.True(clientAcks.TryGetDecision(1, actorVersion, 37, out var applied));
            Assert.Equal(submittedEpoch == 1, applied.Accepted);
            Assert.Equal(4, applied.AuthoritativeFrame);
            Assert.Equal(1, applied.InterruptEpoch);
            if (rejectedPredictedSummon)
            {
                Assert.Equal(new FrameIndex(3), driver.LastRollbackFrame);
                Assert.Empty(SummonIdentities(clientWorld));
                Assert.Equal(4, remote.LastConsumedFrame);
            }
            host.Tick(delta);
            AssertFrames(driver, clientWorld, 5, 5);
            Assert.Equal(mismatchAtFourth ? 2 : 1, driver.TotalRollbackCount);
            Assert.Equal(0, driver.TotalRollbackRestoreFailed);
            Assert.Equal(applied, AssertDecision());
            Assert.Equal(1, clientAcks.GetInterruptEpoch(1, actorVersion));
            Assert.False(clientAcks.TryGetSnapshot(new FrameIndex(5), out _));
            Assert.Equal(SummonIdentities(authorityWorld), SummonIdentities(clientWorld));
        }
        finally
        {
            driver.Uninstall(host, options);
        }

        WireSettledFrame TickAuthority(int frame, BattleInputItem[] inputs)
        {
            Assert.True(settled.TryTickSettledFrame(frame, tickRate, inputs, out var outcomes, out var entries));
            return CreateWire(frame, inputs, outcomes, entries);
        }

        WireSettledFrame CreateWire(int frame, IReadOnlyList<BattleInputItem> inputs, IReadOnlyList<MobaFrameInputOutcome> outcomes,
            IReadOnlyList<MobaFrameAuthorityEntry> entries) => new()
        {
            Identity = identity, Frame = frame, InitialCheckpoint = frame == 0,
            Commands = outcomes.Select(outcome => new WireSettledCommand
            {
                Ordinal = outcome.Ordinal, PlayerId = inputs[outcome.Ordinal].PlayerId, OpCode = inputs[outcome.Ordinal].OpCode,
                Disposition = (byte)outcome.Disposition,
                ExecutedPayload = outcome.Disposition == MobaFrameInputDisposition.Executed
                    ? outcome.CopyExecutedInput().Payload : []
            }).ToArray(),
            Decisions = entries.Select(entry => new WireSettledDecision
            {
                Ordinal = entry.Ordinal, ActorId = entry.Entry.ActorId, EntityVersion = entry.Entry.EntityVersion,
                PredictionKey = entry.Entry.PredictionKey, SkillId = entry.Entry.SkillId,
                Accepted = entry.Entry.Accepted, AuthoritativeFrame = entry.Entry.AuthoritativeFrame,
                Reason = entry.Entry.Reason, InterruptEpoch = entry.Entry.InterruptEpoch,
                AuthoritySequence = entry.Entry.AuthoritySequence
            }).ToArray()
        };

        void Receive(WireSettledFrame frame) => remote.Receive(SettledFrameProtocol.Decode<WireSettledFrame>(
            new ArraySegment<byte>(SettledFrameProtocol.Encode(frame))));

        (int ActorId, int Version)[] SummonIdentities(AbilityKit.Ability.World.Abstractions.IWorld world)
        {
            var actorIds = new List<int>();
            world.Services.Resolve<MobaSummonService>().TryGetSummons(1, actorIds, summonConfig);
            var registry = world.Services.Resolve<MobaActorRegistry>();
            return actorIds.OrderBy(id => id).Select(id => (id, registry.GetEntityVersion(id))).ToArray();
        }

        MobaActionAckEntry AssertDecision()
        {
            Assert.True(clientAcks.TryGetDecision(1, actorVersion, 37, out var entry));
            return entry;
        }

    }

    [Fact]
    public void SettledTimelineAppliesOrderedDecisionsAndWrappedInterruptsOnlyOnCommit()
    {
        const string battleId = "settled-timeline-order";
        using var worlds = new ServerBattleWorldManager(NullLogger.Instance);
        using var battle = Start(worlds, battleId);
        var world = worlds.CreateBattleWorld(battleId, 30);
        var acks = world.Services.Resolve<MobaActionAckSnapshotService>();
        var version = world.Services.Resolve<MobaActorRegistry>().GetEntityVersion(1);
        var sink = (IWorldSettledTimelineSink)world.Services.Resolve<MobaInputCoordinator>();
        sink.BindClientInput();
        sink.BindSettledTimeline();

        var before = Entry(1, 0, 1, 37, 0, 1);
        var interrupt = Entry(1, 0, 2, 0, int.MaxValue, 0);
        var after = Entry(1, 1, 4, 38, int.MaxValue, 1);
        var wrapped = Entry(1, 3, 5, 0, 1, 0);
        var duplicate = new MobaFrameAuthorityEntry(2, before.Entry);
        var update = sink.PrepareSettledFrame(new WorldSettledInputFrame(new FrameIndex(1), [],
            new MobaSettledFrameMetadata(3, [before, interrupt, after, duplicate, wrapped, wrapped])));
        Assert.True(update.InvalidatesPrediction);
        Assert.Equal(0, acks.GetInterruptEpoch(1, version));
        Assert.False(acks.TryGetDecision(1, version, 37, out _));
        update.Commit();
        Assert.Equal(1, acks.GetInterruptEpoch(1, version));
        Assert.True(acks.TryGetDecision(1, version, 37, out var first));
        Assert.Equal(before.Entry, first);
        Assert.True(acks.TryGetDecision(1, version, 38, out var second));
        Assert.Equal(after.Entry, second);

        var repeatOnly = sink.PrepareSettledFrame(new WorldSettledInputFrame(new FrameIndex(2), [],
            new MobaSettledFrameMetadata(1, [new MobaFrameAuthorityEntry(0, before.Entry), new MobaFrameAuthorityEntry(1, interrupt.Entry)])));
        Assert.False(repeatOnly.InvalidatesPrediction);
        repeatOnly.Commit();
        Assert.Equal(1, acks.GetInterruptEpoch(1, version));

        MobaFrameAuthorityEntry Entry(int frame, int ordinal, long sequence, int key, int epoch, int skill) =>
            new(ordinal, new MobaActionAckEntry
            {
                ActorId = 1, EntityVersion = version, AuthoritySequence = sequence, PredictionKey = key,
                AuthoritativeFrame = frame, InterruptEpoch = epoch, SkillId = skill,
                Accepted = key != 0, Reason = (int)(key == 0 ? MobaActionAckReason.Interrupted : MobaActionAckReason.None)
            });
    }

    [Fact]
    public void SettledTimelineDoesNotPublishOrConsumeFrameWhenCaptureFails()
    {
        const string battleId = "settled-timeline-capture-failure";
        using var worlds = new ServerBattleWorldManager(NullLogger.Instance);
        using var battle = Start(worlds, battleId);
        var world = worlds.CreateBattleWorld(battleId, 30);
        var acks = world.Services.Resolve<MobaActionAckSnapshotService>();
        var version = world.Services.Resolve<MobaActorRegistry>().GetEntityVersion(1);
        var guard = world.Services.Resolve<MobaCombatFrameGuard>();
        using var remote = new FrameJitterBuffer<WorldSettledInputFrame>(0);
        var options = new HostRuntimeOptions();
        var host = new HostRuntime(new ReplayWorldManager(world), options);
        var driver = new ClientPredictionDriverModule(null!, null!, enableRollback: true,
            buildRollbackRegistry: target =>
            {
                var registry = MobaRollbackRegistryBuilder.CreateForPrediction(target);
                registry.Register(new FailTimelineCapture());
                return registry;
            }, resolveSettledFrames: _ => remote);
        driver.Install(host, options);
        try
        {
            options.WorldCreated.Invoke(world);
            var interrupt = new MobaFrameAuthorityEntry(0, new MobaActionAckEntry
            {
                ActorId = 1, EntityVersion = version, AuthoritativeFrame = 1, AuthoritySequence = 1,
                InterruptEpoch = 1, Reason = (int)MobaActionAckReason.Interrupted
            });
            var record = new WorldSettledInputFrame(new FrameIndex(1), [], new MobaSettledFrameMetadata(0, [interrupt]));
            remote.Add(1, record);
            var initialTickCount = guard.CompletedTickCount;
            host.Tick(1f / 30);
            AssertFrames(driver, world, 0, 0);
            Assert.Equal(initialTickCount + 1, guard.CompletedTickCount);
            Assert.Equal(0, acks.GetInterruptEpoch(1, version));
            Assert.True(remote.TryGet(1, out var retained));
            Assert.Same(record, retained);
            host.Tick(1f / 30);
            AssertFrames(driver, world, 0, 0);
            Assert.Equal(initialTickCount + 1, guard.CompletedTickCount);
        }
        finally
        {
            driver.Uninstall(host, options);
        }
    }

    private sealed class FailTimelineCapture : IRollbackStateProvider
    {
        public int Key => int.MaxValue;
        public byte[] Export(FrameIndex frame) => frame.Value == 0 ? [] : throw new InvalidOperationException("Test capture failure.");
        public void Import(FrameIndex frame, byte[] payload) { }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SettlementRecordsActualExecutionRatherThanDuplicateAck(bool rejected)
    {
        const string battleId = "settled-input-execution";
        const int tickRate = 30;
        const int actionFrame = 3;
        const int predictionKey = 37;
        const int summonConfig = 30030502;
        using var authorityWorlds = new ServerBattleWorldManager(NullLogger.Instance);
        using var clientWorlds = new ServerBattleWorldManager(NullLogger.Instance);
        using var authority = Start(authorityWorlds, battleId);
        using var client = Start(clientWorlds, battleId);
        var settled = Assert.IsAssignableFrom<IMobaSettledFrameRuntime>(authority);
        var clientClock = Assert.IsAssignableFrom<IBattleRuntimeExternalFrameClock>(client);
        var authorityWorld = authorityWorlds.CreateBattleWorld(battleId, tickRate);
        var clientWorld = clientWorlds.CreateBattleWorld(battleId, tickRate);
        var authorityAcks = authorityWorld.Services.Resolve<MobaActionAckSnapshotService>();
        var clientAcks = clientWorld.Services.Resolve<MobaActionAckSnapshotService>();
        var clientInput = clientWorld.Services.Resolve<MobaInputCoordinator>();
        ((IWorldClientInputSink)clientInput).BindClientInput();
        var actorVersion = authorityWorld.Services.Resolve<MobaActorRegistry>().GetEntityVersion(1);
        var caster = Assert.Single(authority.CreateStateSyncPush(1, 0, isFullSnapshot: true).Actors);

        for (var frame = 1; frame < actionFrame; frame++)
        {
            Assert.True(settled.TryTickSettledFrame(frame, tickRate, [], out var empty, out var metadata));
            Assert.Empty(empty);
            Assert.Empty(metadata);
            Assert.True(clientClock.TryPrepareExternalFrame(frame, 1f / tickRate));
            Assert.True(client.Tick(frame, tickRate, 1f / tickRate));
        }

        if (rejected) Assert.Equal(1, authorityAcks.AdvanceInterruptEpoch(1, actorVersion, actionFrame - 1));
        var aim = new Vec3(caster.X + 2f, caster.Y, caster.Z);
        var skill = new SkillInputEvent(4, SkillInputPhase.Press, aimPos: in aim, targetFrame: actionFrame,
            inputSequence: 1, predictionKey: predictionKey, entityVersion: actorVersion);
        var input = new BattleInputItem { PlayerId = 1, OpCode = MobaOpCodes.Input.SkillInput, Payload = SkillInputCodec.Serialize(in skill) };
        Assert.True(settled.TryTickSettledFrame(actionFrame, tickRate, [input, input], out var outcomes, out var entries));
        Assert.Collection(outcomes,
            result => Assert.Equal(rejected ? MobaFrameInputDisposition.Rejected : MobaFrameInputDisposition.Executed, result.Disposition),
            result => Assert.Equal(MobaFrameInputDisposition.Duplicate, result.Disposition));
        Assert.Equal(new[] { 0, 1 }, outcomes.Select(result => result.Ordinal));
        var decisions = entries.Where(entry => entry.Entry.PredictionKey != 0).ToArray();
        Assert.Equal(new[] { 0, 1 }, decisions.Select(decision => decision.Ordinal));
        Assert.Equal(decisions[0].Entry.AuthoritySequence, decisions[1].Entry.AuthoritySequence);
        Assert.Equal(!rejected, decisions[0].Entry.Accepted);
        Assert.Equal(actionFrame, decisions[0].Entry.AuthoritativeFrame);

        var executed = outcomes.Where(item => item.Disposition == MobaFrameInputDisposition.Executed)
            .Select(item => item.CopyExecutedInput()).ToArray();
        if (!rejected)
        {
            var retained = outcomes[0].CopyExecutedInput();
            retained.Payload[0] ^= 0xFF;
            Assert.Equal(input.Payload, outcomes[0].CopyExecutedInput().Payload);
            clientAcks.ApplyAuthoritative([new MobaActionAckEntry
            {
                ActorId = 1, EntityVersion = actorVersion, PredictionKey = predictionKey,
                AuthoritativeFrame = actionFrame + 1, InterruptEpoch = 9, AuthoritySequence = 10
            }]);
        }

        Assert.True(clientClock.TryPrepareExternalFrame(actionFrame, 1f / tickRate));
        clientInput.ExecuteSettledInputs(new FrameIndex(actionFrame), executed);
        Assert.True(client.Tick(actionFrame, tickRate, 1f / tickRate));
        Assert.Equal(rejected ? 0 : 1, clientWorld.Services.Resolve<MobaSummonService>().GetAliveCount(1, summonConfig));
        Assert.Equal(rejected ? 0 : 1, authorityWorld.Services.Resolve<MobaSummonService>().GetAliveCount(1, summonConfig));
        Assert.False(clientAcks.TryGetSnapshot(new FrameIndex(actionFrame), out _));
    }

    [Fact]
    public void UnprovenDispatchCannotProduceAnEmptySettlement()
    {
        const string battleId = "settled-input-fault";
        using var worlds = new ServerBattleWorldManager(NullLogger.Instance);
        using var authority = Start(worlds, battleId);
        var settled = Assert.IsAssignableFrom<IMobaSettledFrameRuntime>(authority);
        var unknown = new BattleInputItem { PlayerId = 1, OpCode = int.MaxValue, Payload = [] };
        Assert.Throws<InvalidOperationException>(() => settled.TryTickSettledFrame(1, 30, [unknown], out _, out _));
        Assert.False(Assert.IsAssignableFrom<IBattleRuntimeFrameCompletion>(authority).CanFinalizeFrame(1));
        Assert.False(settled.TryTickSettledFrame(2, 30, [], out _, out _));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void SettledExecutionReconcilesTheSameSubmittedBytes(bool authorityExecutes, bool clientExecutes)
    {
        const string battleId = "settled-input-correction";
        const int tickRate = 30;
        const float delta = 1f / tickRate;
        const int baselineFrame = 2;
        const int actionFrame = 3;
        const int summonConfig = 30030502;
        using var authorityWorlds = new ServerBattleWorldManager(NullLogger.Instance);
        using var clientWorlds = new ServerBattleWorldManager(NullLogger.Instance);
        using var authority = Start(authorityWorlds, battleId);
        using var client = Start(clientWorlds, battleId);
        var settled = Assert.IsAssignableFrom<IMobaSettledFrameRuntime>(authority);
        var authorityWorld = authorityWorlds.CreateBattleWorld(battleId, tickRate);
        var clientWorld = clientWorlds.CreateBattleWorld(battleId, tickRate);
        var authorityAcks = authorityWorld.Services.Resolve<MobaActionAckSnapshotService>();
        var clientAcks = clientWorld.Services.Resolve<MobaActionAckSnapshotService>();
        var clientSummons = clientWorld.Services.Resolve<MobaSummonService>();
        var actorVersion = authorityWorld.Services.Resolve<MobaActorRegistry>().GetEntityVersion(1);
        var caster = Assert.Single(authority.CreateStateSyncPush(1, 0, isFullSnapshot: true).Actors);
        using var remote = new FrameJitterBuffer<PlayerInputCommand[]>(0);
        using var local = new QueuedLocalInputs();
        var options = new HostRuntimeOptions();
        var host = new HostRuntime(new ReplayWorldManager(clientWorld), options);
        var limit = 0;
        var driver = new ClientPredictionDriverModule(_ => remote, _ => local,
            resolveIdealFrameLimit: _ => limit, minPredictionWindow: 8, maxPredictionAheadFrames: 8,
            enableRollback: true, rollbackHistoryFrames: 16,
            buildRollbackRegistry: MobaRollbackRegistryBuilder.CreateForPrediction, useSettledExecutionInputs: true);

        driver.Install(host, options);
        try
        {
            options.WorldCreated.Invoke(clientWorld);
            for (var frame = 1; frame <= baselineFrame; frame++)
            {
                remote.Add(frame, TickAuthority(frame, []));
                limit = frame;
                host.Tick(delta);
            }
            AssertFrames(driver, clientWorld, baselineFrame, baselineFrame);
            if (!authorityExecutes) authorityAcks.AdvanceInterruptEpoch(1, actorVersion, baselineFrame);
            if (!clientExecutes)
            {
                clientAcks.ApplyAuthoritative([new MobaActionAckEntry
                {
                    ActorId = 1, EntityVersion = actorVersion, InterruptEpoch = 1, AuthoritySequence = 1,
                    AuthoritativeFrame = baselineFrame
                }]);
            }

            var aim = new Vec3(caster.X + 2f, caster.Y, caster.Z);
            var skill = new SkillInputEvent(4, SkillInputPhase.Press, aimPos: in aim, targetFrame: actionFrame,
                inputSequence: 1, predictionKey: 37, entityVersion: actorVersion);
            var input = new BattleInputItem { PlayerId = 1, OpCode = MobaOpCodes.Input.SkillInput, Payload = SkillInputCodec.Serialize(in skill) };
            local.Enqueue(actionFrame, input);
            limit = actionFrame;
            host.Tick(delta);
            AssertFrames(driver, clientWorld, baselineFrame, actionFrame);
            Assert.Equal(clientExecutes ? 1 : 0, clientSummons.GetAliveCount(1, summonConfig));

            var authoritative = TickAuthority(actionFrame, [input]);
            remote.Add(actionFrame + 1, TickAuthority(actionFrame + 1, []));
            host.Tick(delta);
            AssertFrames(driver, clientWorld, baselineFrame, actionFrame);
            remote.Add(actionFrame, authoritative);
            host.Tick(delta);
            AssertFrames(driver, clientWorld, actionFrame, actionFrame);
            Assert.Equal(authorityExecutes == clientExecutes ? 0 : 1, driver.TotalRollbackCount);
            Assert.Equal(0, driver.TotalRollbackRestoreFailed);
            if (authorityExecutes != clientExecutes) Assert.Equal(new FrameIndex(baselineFrame), driver.LastRollbackFrame);
            Assert.Equal(authorityExecutes ? 1 : 0, clientSummons.GetAliveCount(1, summonConfig));
            Assert.False(clientAcks.TryGetSnapshot(new FrameIndex(actionFrame), out _));
        }
        finally
        {
            driver.Uninstall(host, options);
        }

        PlayerInputCommand[] TickAuthority(int frame, BattleInputItem[] inputs)
        {
            Assert.True(settled.TryTickSettledFrame(frame, tickRate, inputs, out var outcomes, out _));
            return outcomes.Where(item => item.Disposition == MobaFrameInputDisposition.Executed)
                .Select(item => item.CopyExecutedInput()).ToArray();
        }
    }
}
