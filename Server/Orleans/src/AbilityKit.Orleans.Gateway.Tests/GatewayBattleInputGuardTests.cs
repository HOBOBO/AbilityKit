using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Gateway.Handlers;
using Xunit;

namespace AbilityKit.Orleans.Gateway.Tests;

public sealed class GatewayBattleInputGuardTests
{
    private const string SessionToken = "session-a";
    private const string BattleId = "battle-a";
    private const uint PlayerId = 1;

    [Fact]
    public void Check_AcceptedSequenceBecomesDuplicateOnlyAfterRecordAccepted()
    {
        var guard = new GatewayBattleInputGuard();

        Assert.Equal(GatewayBattleInputGuardResult.Accepted, guard.Check(SessionToken, BattleId, PlayerId, 7, 0));
        Assert.Equal(GatewayBattleInputGuardResult.Accepted, guard.Check(SessionToken, BattleId, PlayerId, 7, 0));

        guard.RecordAccepted(SessionToken, BattleId, PlayerId, 7);

        Assert.Equal(GatewayBattleInputGuardResult.Duplicate, guard.Check(SessionToken, BattleId, PlayerId, 7, 0));
    }

    [Fact]
    public void Check_AcceptsUnseenOutOfOrderSequenceInsideReplayWindow()
    {
        var guard = new GatewayBattleInputGuard();
        RecordAccepted(guard, 200);

        Assert.Equal(GatewayBattleInputGuardResult.Accepted, guard.Check(SessionToken, BattleId, PlayerId, 199, 0));
    }

    [Fact]
    public void Check_RejectsSequenceAtReplayWindowBoundary()
    {
        var guard = new GatewayBattleInputGuard();
        RecordAccepted(guard, 200);

        Assert.Equal(
            GatewayBattleInputGuardResult.TooOld,
            guard.Check(SessionToken, BattleId, PlayerId, 200 - GatewayBattleInputGuard.ReplayWindowSize, 0));
    }

    [Fact]
    public void Check_SequenceZeroUsesLegacyCompatibilityPath()
    {
        var guard = new GatewayBattleInputGuard();

        Assert.Equal(GatewayBattleInputGuardResult.AcceptedLegacy, guard.Check(SessionToken, BattleId, PlayerId, 0, 0));
        Assert.Equal(GatewayBattleInputGuardResult.AcceptedLegacy, guard.Check(SessionToken, BattleId, PlayerId, 0, 0));
        Assert.Equal(0, guard.GetTrackedSequenceCount(SessionToken, BattleId, PlayerId));
    }

    [Fact]
    public void Check_RejectsBurstBeyondTokenCapacityAndRefillsOverTime()
    {
        var guard = new GatewayBattleInputGuard();

        for (ulong sequence = 1; sequence <= GatewayBattleInputGuard.BurstInputs; sequence++)
        {
            Assert.Equal(GatewayBattleInputGuardResult.Accepted, guard.Check(SessionToken, BattleId, PlayerId, sequence, 0));
        }

        Assert.Equal(GatewayBattleInputGuardResult.RateLimited, guard.Check(SessionToken, BattleId, PlayerId, 1000, 0));
        Assert.Equal(
            GatewayBattleInputGuardResult.Accepted,
            guard.Check(SessionToken, BattleId, PlayerId, 1000, TimeSpan.TicksPerSecond / GatewayBattleInputGuard.InputsPerSecond + 1));
    }

    [Fact]
    public void Check_FrameInputRateLimitDoesNotConsumeBattleCommandCapacity()
    {
        var guard = new GatewayBattleInputGuard(new BattleInputSecurityOptions
        {
            InputsPerSecond = 1,
            BurstInputs = 2
        });

        Assert.Equal(
            GatewayBattleInputGuardResult.AcceptedLegacy,
            guard.Check(SessionToken, BattleId, PlayerId, 0, 0, GatewayBattleInputLane.FrameInput));
        Assert.Equal(
            GatewayBattleInputGuardResult.AcceptedLegacy,
            guard.Check(SessionToken, BattleId, PlayerId, 0, 0, GatewayBattleInputLane.FrameInput));
        Assert.Equal(
            GatewayBattleInputGuardResult.RateLimited,
            guard.Check(SessionToken, BattleId, PlayerId, 0, 0, GatewayBattleInputLane.FrameInput));

        Assert.Equal(
            GatewayBattleInputGuardResult.Accepted,
            guard.Check(SessionToken, BattleId, PlayerId, 1, 0));
        Assert.Equal(2, guard.TrackedKeyCount);
    }

    [Fact]
    public void Check_DuplicateDoesNotConsumeRateLimitToken()
    {
        var guard = new GatewayBattleInputGuard();
        RecordAccepted(guard, 1);

        for (var index = 0; index < GatewayBattleInputGuard.BurstInputs * 2; index++)
        {
            Assert.Equal(
                GatewayBattleInputGuardResult.Duplicate,
                guard.Check(SessionToken, BattleId, PlayerId, 1, 0));
        }

        Assert.Equal(
            GatewayBattleInputGuardResult.Accepted,
            guard.Check(SessionToken, BattleId, PlayerId, 2, 0));
    }

    [Fact]
    public void RecordAccepted_BoundsRetainedReplaySequences()
    {
        var guard = new GatewayBattleInputGuard();

        for (ulong sequence = 1; sequence <= 512; sequence++)
        {
            var nowTicks = (long)sequence * TimeSpan.TicksPerSecond;
            Assert.Equal(GatewayBattleInputGuardResult.Accepted, guard.Check(SessionToken, BattleId, PlayerId, sequence, nowTicks));
            guard.RecordAccepted(SessionToken, BattleId, PlayerId, sequence);
        }

        Assert.Equal(GatewayBattleInputGuard.ReplayWindowSize, guard.GetTrackedSequenceCount(SessionToken, BattleId, PlayerId));
    }

    [Fact]
    public void Check_BoundsTrackedSessionBattlePlayerKeys()
    {
        var guard = new GatewayBattleInputGuard();

        for (var index = 0; index < GatewayBattleInputGuard.MaxTrackedKeys; index++)
        {
            Assert.Equal(
                GatewayBattleInputGuardResult.AcceptedLegacy,
                guard.Check($"session-{index}", BattleId, PlayerId, 0, 0));
        }

        Assert.Equal(GatewayBattleInputGuard.MaxTrackedKeys, guard.TrackedKeyCount);
        Assert.Equal(
            GatewayBattleInputGuardResult.RateLimited,
            guard.Check("session-over-capacity", BattleId, PlayerId, 0, 0));
        Assert.Equal(GatewayBattleInputGuard.MaxTrackedKeys, guard.TrackedKeyCount);
    }

    [Fact]
    public void Check_ReclaimsIdleKeysWhenCapacityIsReached()
    {
        var guard = new GatewayBattleInputGuard();
        for (var index = 0; index < GatewayBattleInputGuard.MaxTrackedKeys; index++)
        {
            Assert.Equal(
                GatewayBattleInputGuardResult.AcceptedLegacy,
                guard.Check($"session-{index}", BattleId, PlayerId, 0, 0));
        }

        var afterIdleWindow = GatewayBattleInputGuard.IdleStateTtlTicks + 1;
        Assert.Equal(
            GatewayBattleInputGuardResult.AcceptedLegacy,
            guard.Check("session-after-idle", BattleId, PlayerId, 0, afterIdleWindow));
        Assert.Equal(1, guard.TrackedKeyCount);
    }

    [Fact]
    public void Check_CustomReplayWindowControlsTooOldBoundary()
    {
        var guard = new GatewayBattleInputGuard(new BattleInputSecurityOptions
        {
            ReplayWindowSize = 2
        });
        RecordAccepted(guard, 10);

        Assert.Equal(
            GatewayBattleInputGuardResult.TooOld,
            guard.Check(SessionToken, BattleId, PlayerId, 8, 0));
    }

    [Fact]
    public void Check_CustomCapacityAndIdleTtlControlReclamation()
    {
        var guard = new GatewayBattleInputGuard(new BattleInputSecurityOptions
        {
            MaxGatewayTrackedKeys = 1,
            GatewayIdleStateTtlSeconds = 1
        });
        Assert.Equal(
            GatewayBattleInputGuardResult.AcceptedLegacy,
            guard.Check("session-first", BattleId, PlayerId, 0, 0));
        Assert.Equal(
            GatewayBattleInputGuardResult.RateLimited,
            guard.Check("session-before-ttl", BattleId, PlayerId, 0, TimeSpan.TicksPerSecond));

        Assert.Equal(
            GatewayBattleInputGuardResult.AcceptedLegacy,
            guard.Check("session-after-ttl", BattleId, PlayerId, 0, TimeSpan.TicksPerSecond + 1));
        Assert.Equal(1, guard.TrackedKeyCount);
    }

    [Fact]
    public void Constructor_CreatesStableValidatedOptionsSnapshot()
    {
        var options = new BattleInputSecurityOptions
        {
            ReplayWindowSize = 2
        };
        var guard = new GatewayBattleInputGuard(options);
        options.ReplayWindowSize = 100;
        RecordAccepted(guard, 10);

        Assert.Equal(2, guard.Options.ReplayWindowSize);
        Assert.Equal(
            GatewayBattleInputGuardResult.TooOld,
            guard.Check(SessionToken, BattleId, PlayerId, 8, 0));
        Assert.Throws<ArgumentException>(() =>
            new GatewayBattleInputGuard(new BattleInputSecurityOptions { BurstInputs = 0 }));
    }

    private static void RecordAccepted(GatewayBattleInputGuard guard, ulong sequence)
    {
        Assert.Equal(GatewayBattleInputGuardResult.Accepted, guard.Check(SessionToken, BattleId, PlayerId, sequence, 0));
        guard.RecordAccepted(SessionToken, BattleId, PlayerId, sequence);
    }
}
