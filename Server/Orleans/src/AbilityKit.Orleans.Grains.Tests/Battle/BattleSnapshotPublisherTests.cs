using System;
using System.Collections.Generic;
using AbilityKit.Ability.Host.Extensions.Server.BattleHost;
using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Grains.Battle;
using Xunit;

namespace AbilityKit.Orleans.Grains.Tests.Battle;

public sealed class BattleSnapshotPublisherTests
{
    [Fact]
    public void PublishTo_WhenObserverIsNull_ReturnsZeroWithoutCreatingSnapshot()
    {
        var factoryCalls = 0;
        var sent = new List<(string Observer, TestSnapshot Snapshot)>();
        var errors = new List<(string Observer, Exception Exception)>();
        var publisher = new BattleSnapshotPublisher<string, TestSnapshot>(
            (frame, isFullSnapshot) =>
            {
                factoryCalls++;
                return new TestSnapshot(frame, isFullSnapshot);
            },
            (observer, snapshot) => sent.Add((observer, snapshot)),
            (observer, exception) => errors.Add((observer, exception)));

        var result = publisher.PublishTo(null!, 12, isFullSnapshot: true);

        Assert.Equal(0, result);
        Assert.Equal(0, factoryCalls);
        Assert.Empty(sent);
        Assert.Empty(errors);
    }

    [Fact]
    public void PublishTo_WhenObserverIsValid_SendsOnlyTargetObserver()
    {
        var factoryCalls = 0;
        var sent = new List<(string Observer, TestSnapshot Snapshot)>();
        var errors = new List<(string Observer, Exception Exception)>();
        var publisher = new BattleSnapshotPublisher<string, TestSnapshot>(
            (frame, isFullSnapshot) =>
            {
                factoryCalls++;
                return new TestSnapshot(frame, isFullSnapshot);
            },
            (observer, snapshot) => sent.Add((observer, snapshot)),
            (observer, exception) => errors.Add((observer, exception)));

        var result = publisher.PublishTo("observer-b", 34, isFullSnapshot: true);

        Assert.Equal(1, result);
        Assert.Equal(1, factoryCalls);
        var sentItem = Assert.Single(sent);
        Assert.Equal("observer-b", sentItem.Observer);
        Assert.Equal(34, sentItem.Snapshot.Frame);
        Assert.True(sentItem.Snapshot.IsFullSnapshot);
        Assert.Empty(errors);
    }

    [Fact]
    public void PublishTo_WithObserverFactory_DoesNotRepublishExistingObserver()
    {
        var factoryObservers = new List<string>();
        var sent = new List<(string Observer, TestSnapshot Snapshot)>();
        var publisher = new BattleSnapshotPublisher<string, TestSnapshot>(
            (frame, isFullSnapshot) => throw new InvalidOperationException("Shared factory must not be used."),
            (observer, snapshot) => sent.Add((observer, snapshot)),
            (observer, exception) => throw exception);

        TestSnapshot CreateObserverSnapshot(string observer, int frame, bool isFullSnapshot)
        {
            factoryObservers.Add(observer);
            return new TestSnapshot(frame, isFullSnapshot);
        }

        publisher.PublishTo("observer-a", 3, isFullSnapshot: true, CreateObserverSnapshot);
        publisher.PublishTo("observer-b", 5, isFullSnapshot: true, CreateObserverSnapshot);

        Assert.Equal(new[] { "observer-a", "observer-b" }, factoryObservers);
        Assert.Equal(2, sent.Count);
        Assert.Equal(("observer-a", new TestSnapshot(3, true)), sent[0]);
        Assert.Equal(("observer-b", new TestSnapshot(5, true)), sent[1]);
        Assert.Single(sent, item => item.Observer == "observer-a");
    }

    [Fact]
    public void PublishTo_WhenSenderThrows_ReportsErrorAndReturnsZero()
    {
        var expected = new InvalidOperationException("send failed");
        var sent = new List<string>();
        var errors = new List<(string Observer, Exception Exception)>();
        var publisher = new BattleSnapshotPublisher<string, TestSnapshot>(
            (frame, isFullSnapshot) => new TestSnapshot(frame, isFullSnapshot),
            (observer, snapshot) =>
            {
                sent.Add(observer);
                throw expected;
            },
            (observer, exception) => errors.Add((observer, exception)));

        var result = publisher.PublishTo("observer-b", 34, isFullSnapshot: true);

        Assert.Equal(0, result);
        Assert.Equal(new[] { "observer-b" }, sent);
        var error = Assert.Single(errors);
        Assert.Equal("observer-b", error.Observer);
        Assert.Same(expected, error.Exception);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ShouldBroadcastFullSnapshotAfterPlayerJoin_NeverRepublishesExistingObservers(bool accepted)
    {
        var result = new BattlePlayerJoinResult(
            accepted,
            PlayerId: 7,
            CurrentFrame: 34,
            Status: accepted ? "Accepted" : "Rejected",
            Message: string.Empty);

        Assert.False(BattleLogicHostGrain.ShouldBroadcastFullSnapshotAfterPlayerJoin(result));
    }

    [Fact]
    public void CoalescePendingSnapshot_WhenFullBaselineIsPending_PreservesItOverNewerDelta()
    {
        var baseline = CreateStateSyncPush(frame: 34, isFullSnapshot: true);
        var delta = CreateStateSyncPush(frame: 35, isFullSnapshot: false);

        var result = BattleLogicHostGrain.CoalescePendingSnapshot(baseline, delta);

        Assert.Same(baseline, result);
    }

    [Fact]
    public void CoalescePendingSnapshot_WhenNewFullBaselineArrives_ReplacesOlderPendingSnapshot()
    {
        var delta = CreateStateSyncPush(frame: 34, isFullSnapshot: false);
        var baseline = CreateStateSyncPush(frame: 35, isFullSnapshot: true);

        var result = BattleLogicHostGrain.CoalescePendingSnapshot(delta, baseline);

        Assert.Same(baseline, result);
    }

    private static StateSyncPush CreateStateSyncPush(int frame, bool isFullSnapshot)
    {
        return new StateSyncPush
        {
            Frame = frame,
            IsFullSnapshot = isFullSnapshot
        };
    }

    private readonly record struct TestSnapshot(int Frame, bool IsFullSnapshot);
}
