using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.FrameSync;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.World.Management;
using AbilityKit.Ability.World.Services;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Runtime;
using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Grains.Battle;
using AbilityKit.Orleans.Grains.Battle.Gameplay;
using AbilityKit.Orleans.Grains.Gameplays.Moba.Protocol;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AbilityKit.Orleans.Grains.Tests.Battle;

public sealed partial class MobaColdReplayFrameZeroTests
{
    [Theory]
    [InlineData("accepted-echo")]
    [InlineData("omitted-input")]
    [InlineData("rejected-different-input-ack-first")]
    [InlineData("rejected-different-input-ack-late")]
    [InlineData("rejected-identical-input-ack-first")]
    [InlineData("received-only-ack-first")]
    [InlineData("received-only-no-ack")]
    public void KeyedSummonInput_IsolatesLocalPredictionFromAuthority(string scenario)
    {
        const string battleId = "keyed-summon-ack-ownership";
        const int baselineFrame = 2;
        const int actionFrame = 3;
        const int checkFrame = 4;
        const int predictionKey = 37;
        const int summonConfig = 30030502;
        const float delta = 1f / 30f;
        using var authorityWorlds = new ServerBattleWorldManager(NullLogger.Instance);
        using var predictionWorlds = new ServerBattleWorldManager(NullLogger.Instance);
        using var authority = Start(authorityWorlds, battleId);
        using var predictedSession = Start(predictionWorlds, battleId);
        var authorityWorld = authorityWorlds.CreateBattleWorld(battleId, 30);
        var predictionWorld = predictionWorlds.CreateBattleWorld(battleId, 30);
        var authorityClock = Assert.IsAssignableFrom<IBattleRuntimeExternalFrameClock>(authority);
        var authorityAcks = authorityWorld.Services.Resolve<MobaActionAckSnapshotService>();
        var predictedAcks = predictionWorld.Services.Resolve<MobaActionAckSnapshotService>();
        Assert.NotSame(authorityAcks, predictedAcks);
        var authoritySummons = authorityWorld.Services.Resolve<MobaSummonService>();
        var predictedSummons = predictionWorld.Services.Resolve<MobaSummonService>();
        var caster = Assert.Single(authority.CreateStateSyncPush(1, 0, isFullSnapshot: true).Actors);
        var version = predictionWorld.Services.Resolve<MobaActorRegistry>().GetEntityVersion(1);
        Assert.Equal(version, authorityWorld.Services.Resolve<MobaActorRegistry>().GetEntityVersion(1));
        using var remote = new FrameJitterBuffer<PlayerInputCommand[]>(0);
        using var local = new QueuedLocalInputs();
        var options = new HostRuntimeOptions();
        var host = new HostRuntime(new ReplayWorldManager(predictionWorld), options);
        var limit = 0;
        var driver = new ClientPredictionDriverModule(_ => remote, _ => local,
            resolveIdealFrameLimit: _ => limit, minPredictionWindow: 8, maxPredictionAheadFrames: 8,
            enableRollback: true, rollbackHistoryFrames: 16,
            buildRollbackRegistry: MobaRollbackRegistryBuilder.CreateForPrediction);

        driver.Install(host, options);
        try
        {
            options.WorldCreated.Invoke(predictionWorld);
            for (var frame = 1; frame <= baselineFrame; frame++)
            {
                AdvanceAuthority(frame, []);
                Deliver(frame, []);
            }
            AssertFrames(driver, predictionWorld, baselineFrame, baselineFrame);
            var receivedOnly = scenario.StartsWith("received-only", StringComparison.Ordinal);
            var availableAck = scenario != "received-only-no-ack";
            var localInput = SkillInput(2f);
            if (!receivedOnly)
            {
                local.Enqueue(actionFrame, localInput);
                limit = actionFrame;
                host.Tick(delta);
                AssertFrames(driver, predictionWorld, baselineFrame, actionFrame);
                Assert.Equal(1, predictedSummons.GetAliveCount(1, summonConfig));
            }
            Assert.False(authorityAcks.TryGetDecision(1, version, predictionKey, out _));
            Assert.False(predictedAcks.TryGetDecision(1, version, predictionKey, out _));
            Assert.Equal(0, predictedAcks.AdvanceInterruptEpoch(1, version, actionFrame));
            Assert.Equal(0, predictedAcks.GetInterruptEpoch(1, version));
            Assert.False(predictedAcks.TryGetSnapshot(new FrameIndex(actionFrame), out _));

            var omitted = scenario == "omitted-input";
            var accepted = receivedOnly || scenario == "accepted-echo";
            var differentPayload = scenario.StartsWith("rejected-different", StringComparison.Ordinal);
            var lateAck = scenario == "rejected-different-input-ack-late";
            if (!omitted && !accepted)
                Assert.Equal(1, authorityAcks.AdvanceInterruptEpoch(1, version, baselineFrame));
            BattleInputItem[] authorityInputs = omitted ? [] : [differentPayload ? SkillInput(3f) : localInput];
            AdvanceAuthority(actionFrame, authorityInputs, accepted ? 1 : 0);
            var authoritativeEntries = Array.Empty<MobaActionAckEntry>();
            if (!omitted)
            {
                Assert.True(authorityAcks.TryGetSnapshot(new FrameIndex(actionFrame), out var snapshot));
                Assert.Equal(MobaOpCodes.Snapshot.ActionAck, snapshot.OpCode);
                authoritativeEntries = MobaActionAckCodec.Deserialize(snapshot.Payload);
                var action = Assert.Single(authoritativeEntries, entry => entry.PredictionKey == predictionKey);
                Assert.Equal((1, version, actionFrame, accepted), (action.ActorId, action.EntityVersion, action.AuthoritativeFrame, action.Accepted));
                Assert.Equal((int)(accepted ? MobaActionAckReason.None : MobaActionAckReason.Interrupted), action.Reason);
                Assert.Equal(accepted ? 1L : 2L, action.AuthoritySequence);
                if (!lateAck && availableAck) predictedAcks.ApplyAuthoritative(authoritativeEntries);
            }
            else
            {
                Assert.False(authorityAcks.TryGetSnapshot(new FrameIndex(actionFrame), out _));
            }

            Deliver(actionFrame, authorityInputs);
            Assert.Equal(0, driver.TotalRollbackRestoreFailed);
            Assert.Equal(omitted || differentPayload ? 1 : 0, driver.TotalRollbackCount);
            if (omitted || differentPayload) Assert.Equal(new FrameIndex(baselineFrame), driver.LastRollbackFrame);
            Assert.Equal(accepted ? 1 : 0, authoritySummons.GetAliveCount(1, summonConfig));
            Assert.Equal(!omitted && availableAck, predictedAcks.TryGetDecision(1, version, predictionKey, out var decisionAtCorrection));
            if (!omitted && availableAck)
            {
                Assert.Equal(accepted || lateAck, decisionAtCorrection.Accepted);
                Assert.Equal(lateAck ? 0L : accepted ? 1L : 2L, decisionAtCorrection.AuthoritySequence);
            }
            if (lateAck) predictedAcks.ApplyAuthoritative(authoritativeEntries);
            if (omitted)
            {
                options.PreTick.Invoke(delta);
                Assert.False(driver.IsReplaying);
            }

            AdvanceAuthority(checkFrame, []);
            Deliver(checkFrame, []);
            for (var attempt = 0; attempt < 2 && driver.TryGetFrames(predictionWorld.Id, out var current, out _) && current.Value < checkFrame; attempt++)
                host.Tick(delta);
            Assert.True(driver.TryGetFrames(predictionWorld.Id, out var confirmed, out var predicted));
            Assert.Equal(checkFrame, confirmed.Value);
            Assert.InRange(predicted.Value, checkFrame, checkFrame + 1);
            Assert.Equal(0, driver.TotalReplayTimeout);
            Assert.False(driver.IsReplaying);
            var hasFinalDecision = predictedAcks.TryGetDecision(1, version, predictionKey, out var finalDecision);
            Assert.Equal(!omitted && availableAck, hasFinalDecision);
            if (hasFinalDecision) Assert.Equal(accepted, finalDecision.Accepted);
            Assert.False(predictedAcks.TryGetSnapshot(new FrameIndex(checkFrame), out _));
            if (receivedOnly && !availableAck)
            {
                predictedAcks.ApplyAuthoritative(authoritativeEntries);
                Assert.True(predictedAcks.TryGetDecision(1, version, predictionKey, out var delivered));
                Assert.Equal((true, actionFrame, 1L), (delivered.Accepted, delivered.AuthoritativeFrame, delivered.AuthoritySequence));
                Assert.False(predictedAcks.TryGetSnapshot(new FrameIndex(checkFrame), out _));
            }
            var clientAlive = predictedSummons.GetAliveCount(1, summonConfig);
            Assert.Equal(accepted || lateAck || scenario == "rejected-identical-input-ack-first" ? 1 : 0, clientAlive);
            output.WriteLine($"ACK_OWNERSHIP Scenario={scenario} Confirmed={checkFrame} HasDecision={hasFinalDecision} Rollbacks={driver.TotalRollbackCount} " +
                $"ClientSummons={clientAlive} AuthoritySummons={authoritySummons.GetAliveCount(1, summonConfig)}");
        }
        finally
        {
            driver.Uninstall(host, options);
        }

        BattleInputItem SkillInput(float aimOffset)
        {
            var aim = new Vec3(caster.X + aimOffset, caster.Y, caster.Z);
            var skill = new SkillInputEvent(4, SkillInputPhase.Press, aimPos: in aim, targetFrame: actionFrame,
                inputSequence: 1, predictionKey: predictionKey, entityVersion: version);
            return new BattleInputItem { PlayerId = 1, OpCode = MobaOpCodes.Input.SkillInput, Payload = SkillInputCodec.Serialize(in skill) };
        }

        void AdvanceAuthority(int frame, BattleInputItem[] inputs, int acceptedCount = 0)
        {
            Assert.True(authorityClock.TryPrepareExternalFrame(frame, delta));
            Assert.Equal(acceptedCount, authority.SubmitInputs(frame, inputs));
            Assert.True(authority.Tick(frame, 30, delta));
        }

        void Deliver(int frame, BattleInputItem[] inputs)
        {
            remote.Add(frame, DefaultOrleansBattleProtocolMapper.Instance.CreatePlayerInputCommands(frame, inputs).ToArray());
            limit = frame;
            host.Tick(delta);
        }
    }
}
