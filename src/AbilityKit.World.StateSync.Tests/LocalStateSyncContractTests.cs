using AbilityKit.Ability.StateSync;
using AbilityKit.Ability.StateSync.Aoi;
using AbilityKit.Ability.StateSync.Hash;
using AbilityKit.Ability.StateSync.Snapshot;
using Xunit;

namespace AbilityKit.World.StateSync.Tests;

public sealed class LocalStateSyncContractTests
{
    [Fact]
    public void AoiUsesBoundaryForExistingKeysAndAppendsSortedLeaves()
    {
        var interest = new AoiInterestSet();
        var scope = new AoiInterestScope(0, 0, visibleRadius: 5, boundaryRadius: 8);
        var leavingFirst = new AoiEntityKey(1, 2);
        var leavingSecond = new AoiEntityKey(2, 1);
        var staying = new AoiEntityKey(1, 4);
        var newOutside = new AoiEntityKey(1, 5);
        var entering = new AoiEntityKey(1, 6);
        var first = interest.Evaluate(new[]
        {
            new AoiEntitySample(leavingSecond, 1, 0),
            new AoiEntitySample(leavingFirst, 0, 1, layer: 4, ownerId: 12, flags: 2),
            new AoiEntitySample(staying, 5, 0)
        }, scope);

        var second = interest.Evaluate(new[]
        {
            new AoiEntitySample(staying, 8, 0),
            new AoiEntitySample(newOutside, 6, 0),
            new AoiEntitySample(entering, 0, 5)
        }, scope);

        Assert.Equal(3, first.VisibleCount);
        Assert.Equal(2, second.VisibleCount);
        Assert.False(interest.IsVisible(newOutside));
        Assert.Collection(second.Changes,
            change => AssertChange(change, staying, AoiInterestTransition.Stay, 0),
            change => AssertChange(change, entering, AoiInterestTransition.Enter, 2),
            change =>
            {
                AssertChange(change, leavingFirst, AoiInterestTransition.Leave, -1);
                Assert.Equal(0f, change.DistanceSquared);
                Assert.Equal(4, change.Layer);
                Assert.Equal(12, change.OwnerId);
                Assert.Equal((byte)2, change.Flags);
            },
            change => AssertChange(change, leavingSecond, AoiInterestTransition.Leave, -1));
    }

    [Fact]
    public void AoiZeroRadiusDoesNotFilterOrApplyPriorityAndCount()
    {
        var interest = new AoiInterestSet();
        var scope = new AoiInterestScope(0, 0, visibleRadius: 0, boundaryRadius: 1, maxEntities: 1);
        var low = new AoiEntityKey(1, 2);
        var high = new AoiEntityKey(1, 1);

        var result = interest.Evaluate(new[]
        {
            new AoiEntitySample(low, 10, 0, priority: 1),
            new AoiEntitySample(default, 0, 0),
            new AoiEntitySample(high, 20, 0, priority: 9)
        }, scope);

        Assert.Equal(2, result.VisibleCount);
        Assert.Collection(result.Changes,
            change => AssertChange(change, low, AoiInterestTransition.Enter, 0),
            change => AssertChange(change, high, AoiInterestTransition.Enter, 2));
        Assert.All(result.Changes, change => Assert.Equal(0f, change.DistanceSquared));
    }

    [Fact]
    public void AoiTransientReusesResultAndForcedBaselineDoesNotEmitOldLeaves()
    {
        var interest = new AoiInterestSet();
        var retained = new AoiEntityKey(1, 2);
        var removed = new AoiEntityKey(1, 1);
        var sample = new AoiEntitySample(retained, 0, 0);
        var owned = interest.Evaluate(new[] { sample, new AoiEntitySample(removed, 0, 0) }, default);

        var transient = interest.EvaluateTransient(new[] { sample }, default, forceFullBaseline: true);
        Assert.Equal(1, transient.VisibleCount);
        AssertChange(Assert.Single(transient.Changes), retained, AoiInterestTransition.Enter, 0);
        Assert.False(interest.IsVisible(removed));
        var reusableChanges = transient.Changes;

        var empty = interest.EvaluateTransient(System.Array.Empty<AoiEntitySample>(), default);
        Assert.Same(transient, empty);
        Assert.Same(reusableChanges, empty.Changes);
        Assert.Equal(0, empty.VisibleCount);
        AssertChange(Assert.Single(reusableChanges), retained, AoiInterestTransition.Leave, -1);
        Assert.Equal(2, owned.Changes.Count);
    }

    [Fact]
    public void HashDiagnosticsSkipMissingSnapshotsAndCompareInvalidSentinelsByValue()
    {
        var validator = new StateHashValidator();
        validator.OnValidationResult = _ => throw new System.InvalidOperationException();

        var missing = validator.Validate(17, null, new WorldStateSnapshot());
        var equalInvalid = validator.Validate(18, StateHash.Invalid, default(StateHash));

        Assert.True(missing.IsValid);
        Assert.Equal(17, missing.Frame);
        Assert.True(equalInvalid.IsValid);
        Assert.False(equalInvalid.ClientHash.IsValid);
        Assert.Equal(DesyncType.None, equalInvalid.DesyncKind);
    }

    [Fact]
    public void HashDiagnosticsOnlyNotifyOnSnapshotMetadataMismatch()
    {
        var notifications = new System.Collections.Generic.List<ValidationResult>();
        var validator = new StateHashValidator { OnValidationResult = notifications.Add };
        var client = new WorldStateSnapshot { WorldId = 1, Frame = 5 };
        var server = new WorldStateSnapshot { WorldId = 2, Frame = 5, IsFullSnapshot = false };

        Assert.True(validator.Validate(17, client, server).IsValid);
        Assert.False(validator.Validate(17, new StateHash(1), new StateHash(2)).IsValid);
        Assert.Empty(notifications);

        server.Frame = 6;
        var mismatch = validator.Validate(17, client, server);

        var notification = Assert.Single(notifications);
        Assert.False(mismatch.IsValid);
        Assert.Equal(17, notification.Frame);
        Assert.Equal(DesyncType.Unknown, notification.DesyncKind);
        Assert.Equal(client.ComputeHash(), notification.ClientHash);
        Assert.Equal(server.ComputeHash(), notification.ServerHash);
    }

    [Fact]
    public void KeyFramePlanPrioritizesFullThenMajorAndQueriesAdjacentFrames()
    {
        var plan = new KeyFrameStrategy();

        Assert.Equal((60, 10, 3), (plan.FullSnapshotInterval, plan.MajorDeltaInterval, plan.MinorDeltaInterval));
        Assert.Equal(KeyFrameLevel.FullSnapshot, plan.GetKeyFrameLevel(0));
        Assert.Equal(KeyFrameLevel.FullSnapshot, plan.GetKeyFrameLevel(60));
        Assert.Equal(KeyFrameLevel.MajorDelta, plan.GetKeyFrameLevel(30));
        Assert.Equal(KeyFrameLevel.MinorDelta, plan.GetKeyFrameLevel(3));
        Assert.Equal(KeyFrameLevel.Incremental, plan.GetKeyFrameLevel(1));
        Assert.Equal(120, plan.GetNextKeyFrame(60));
        Assert.Equal(60, plan.GetPreviousKeyFrame(120));
        Assert.Equal(0, plan.GetPreviousKeyFrame(0));
    }

    [Fact]
    public void KeyFramePlanUsesIntegerPerFrameEstimatesAndRejectsNonPositiveIntervals()
    {
        var plan = new KeyFrameStrategy();

        Assert.Equal((18, 4, 2, 1), (plan.EstimateBandwidth(60, 3, 3), plan.EstimateBandwidth(10, 3, 3),
            plan.EstimateBandwidth(3, 3, 3), plan.EstimateBandwidth(1, 3, 3)));
        Assert.Throws<System.ArgumentOutOfRangeException>(() => new KeyFrameStrategy(minorDeltaInterval: 0));
    }

    private static void AssertChange(AoiInterestChange change, AoiEntityKey key, AoiInterestTransition transition, int sourceIndex)
    {
        Assert.Equal(key, change.Key);
        Assert.Equal(transition, change.Transition);
        Assert.Equal(sourceIndex, change.SourceIndex);
    }
}
