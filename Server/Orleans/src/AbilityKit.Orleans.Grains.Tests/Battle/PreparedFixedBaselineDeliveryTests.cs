using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orleans.Serialization;
using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Grains.Battle;
using AbilityKit.Orleans.Grains.Gameplay;
using AbilityKit.Protocol.Room;
using Xunit;

namespace AbilityKit.Orleans.Grains.Tests.Battle;

public sealed class PreparedFixedBaselineDeliveryTests
{
    [Theory]
    [InlineData(ServerBattleSyncMode.StateSync, true)]
    [InlineData(ServerBattleSyncMode.FrameSync, false)]
    [InlineData(null, false)]
    public async Task OnlyAuthoritativeStateSyncUsesPreparedDelivery(ServerBattleSyncMode? mode, bool expectsPrepared)
    {
        var stream = new FixedBaselineSnapshotStream(tickRate: 30);
        Assert.True(BattleLogicHostGrain.TryBuildFixedBaselineDelivery(mode, stream, Projection(4), false,
            out var delivery, out var wireLength, out var reason));
        Assert.Equal(FixedBaselineFullReason.Initial, reason);
        Assert.Equal(4, delivery.Frame);
        Assert.True(delivery.IsFullSnapshot);
        if (expectsPrepared)
        {
            var prepared = Assert.IsType<PreparedFixedBaselineSnapshot>(delivery.Prepared);
            Assert.Equal(wireLength, prepared.Payload.Length);
            Assert.Equal("default", prepared.NetworkEnvironmentId);
            Assert.Null(delivery.Structured);
        }
        else
        {
            Assert.Null(delivery.Prepared);
            Assert.Equal(FixedBaselineStateSyncProtocol.SchemaVersion, delivery.Structured?.SchemaVersion);
        }

        var observer = CreateObserver(fixedBaseline: true);
        Assert.Equal(StateSyncDeliveryStatus.Accepted, (await delivery.SendAsync(observer)).Status);
        Assert.Equal((long)wireLength, (await observer.GetDeliveryMetricsAsync()).ProducedBytes);
    }

    [Fact]
    public void PreparedRevisionIsSharedWithoutSharingRecipient()
    {
        var stream = new FixedBaselineSnapshotStream(tickRate: 30);
        var full = Prepare(stream, 4);
        var delta = Prepare(stream, 8);
        var first = new StateSyncPreparedRecipient("first", Guid.NewGuid(), Guid.NewGuid());
        var second = new StateSyncPreparedRecipient("second", Guid.NewGuid(), Guid.NewGuid());
        var delivery = new BattleLogicHostGrain.SnapshotDeliveryPayload(delta);
        var firstDelivery = Assert.IsType<PreparedFixedBaselineSnapshot>(delivery.ForRecipient(first).Prepared);
        var secondDelivery = Assert.IsType<PreparedFixedBaselineSnapshot>(delivery.ForRecipient(second).Prepared);

        Assert.Equal(7UL, full.Revision.WorldId);
        Assert.NotEqual(Guid.Empty, full.Revision.StreamGeneration);
        Assert.Equal(1, full.Revision.SnapshotId);
        Assert.Equal(0, full.Revision.BaseSnapshotId);
        Assert.Equal(full.Revision.StreamGeneration, delta.Revision.StreamGeneration);
        Assert.Equal(2, delta.Revision.SnapshotId);
        Assert.Equal(full.Revision.SnapshotId, delta.Revision.BaseSnapshotId);
        Assert.Equal(8, firstDelivery.Frame);
        Assert.Equal(delta.Revision, firstDelivery.Revision);
        Assert.Equal(delta.Revision, secondDelivery.Revision);
        Assert.True(firstDelivery.Payload.Equals(secondDelivery.Payload));
        Assert.Null(delta.Recipient);
        Assert.Equal(first, firstDelivery.Recipient);
        Assert.Equal(second, secondDelivery.Recipient);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ObserverDropsOldPreparedDeliveryBeforeEnqueueAndSend(bool reconnect)
    {
        var stream = new FixedBaselineSnapshotStream(tickRate: 30);
        var oldRecipient = new StateSyncPreparedRecipient("old", Guid.NewGuid(), Guid.NewGuid());
        var replacement = reconnect
            ? new StateSyncPreparedRecipient("new", Guid.NewGuid(), Guid.NewGuid())
            : oldRecipient with { SubscriptionGeneration = Guid.NewGuid() };
        var oldGateway = new CountingGatewayObserver();
        var replacementGateway = new CountingGatewayObserver();
        var observer = CreateObserver(fixedBaseline: true);
        await observer.BindGatewayPushObserverAsync(oldRecipient.BindingId, oldGateway, oldRecipient.ConnectionGeneration);
        SetPreparedRecipient(observer, oldRecipient);
        var oldSnapshot = Prepare(stream, 4) with { Recipient = oldRecipient };
        Assert.Equal(StateSyncDeliveryStatus.Accepted,
            (await observer.OnPreparedFixedBaselineSnapshotPushedAsync(oldSnapshot)).Status);

        if (reconnect)
        {
            await observer.BindGatewayPushObserverAsync(replacement.BindingId, replacementGateway, replacement.ConnectionGeneration);
            Assert.Equal(StateSyncDeliveryStatus.DroppedStale,
                (await observer.OnPreparedFixedBaselineSnapshotPushedAsync(oldSnapshot)).Status);
        }

        SetPreparedRecipient(observer, replacement);
        await DrainAsync(observer);
        Assert.Equal(0, oldGateway.Count);
        Assert.Equal(0, replacementGateway.Count);
        Assert.Equal(StateSyncDeliveryStatus.DroppedStale,
            (await observer.OnPreparedFixedBaselineSnapshotPushedAsync(oldSnapshot)).Status);

        stream.RequestFull();
        var currentSnapshot = Prepare(stream, 12) with { Recipient = replacement };
        Assert.Equal(StateSyncDeliveryStatus.Accepted,
            (await observer.OnPreparedFixedBaselineSnapshotPushedAsync(currentSnapshot)).Status);
        await DrainAsync(observer);
        Assert.Equal(1, reconnect ? replacementGateway.Count : oldGateway.Count);
    }

    [Fact]
    public async Task FixedBaselineSubscribeRejectsReplacedBindingAndMissingSubscriptionGeneration()
    {
        var observer = CreateObserver(fixedBaseline: true);
        var currentConnection = Guid.NewGuid();
        await observer.BindGatewayPushObserverAsync("current", new CountingGatewayObserver(), currentConnection);
        var cursor = new ReliableBattleEventSubscribeCursor();
        var previous = new StateSyncPreparedRecipient("previous", Guid.NewGuid(), Guid.NewGuid());
        var missingGeneration = new StateSyncPreparedRecipient("current", currentConnection);

        await Assert.ThrowsAsync<InvalidOperationException>(() => observer.SubscribeFixedBaselineAsync("battle", cursor, previous));
        await Assert.ThrowsAsync<InvalidOperationException>(() => observer.SubscribeFixedBaselineAsync("battle", cursor, missingGeneration));
    }

    [Fact]
    public void PreparedSnapshotUsesSameReferenceThroughOrleansDeepCopier()
    {
        var stream = new FixedBaselineSnapshotStream(tickRate: 30);
        var prepared = Prepare(stream, 4);
        using var services = new ServiceCollection()
            .AddSerializer(builder => builder.AddAssembly(typeof(PreparedFixedBaselineSnapshot).Assembly))
            .BuildServiceProvider();
        var copier = services.GetRequiredService<DeepCopier<PreparedFixedBaselineSnapshot>>();

        Assert.Same(prepared, copier.Copy(prepared));
        Assert.False(prepared.Payload.Equals(Prepare(stream, 8).Payload));
    }

    [Fact]
    public async Task APreparedPublicationKeepsObserverQueuesIndependent()
    {
        var stream = new FixedBaselineSnapshotStream(tickRate: 30);
        var firstObserver = CreateObserver(fixedBaseline: true);
        var secondObserver = CreateObserver(fixedBaseline: true);
        var legacy = CreateObserver(fixedBaseline: false);
        var full = Prepare(stream, 4);
        Assert.Equal(StateSyncDeliveryStatus.Accepted, (await firstObserver.OnPreparedFixedBaselineSnapshotPushedAsync(full)).Status);
        Assert.Equal(StateSyncDeliveryStatus.Accepted, (await secondObserver.OnPreparedFixedBaselineSnapshotPushedAsync(full)).Status);
        Assert.Equal(StateSyncDeliveryStatus.Failed, (await legacy.OnPreparedFixedBaselineSnapshotPushedAsync(full)).Status);

        var delta = Prepare(stream, 8);
        var next = new BattleLogicHostGrain.SnapshotDeliveryPayload(delta);
        Assert.Same(full, BattleLogicHostGrain.CoalescePendingSnapshot(new BattleLogicHostGrain.SnapshotDeliveryPayload(full), next).Prepared);
        var result = await next.SendAsync(firstObserver);
        Assert.Equal(StateSyncDeliveryStatus.Backpressured, result.Status);
        Assert.True(result.BaselineInvalidated);
        var first = await firstObserver.GetDeliveryMetricsAsync();
        var second = await secondObserver.GetDeliveryMetricsAsync();
        Assert.Equal(1, first.QueueLength);
        Assert.Equal(1, second.QueueLength);
        Assert.Equal(1, first.ResyncCount);
        Assert.Equal(0, second.ResyncCount);
        Assert.Equal((long)delta.Payload.Length, first.DroppedBytes);
        Assert.Equal(0, second.DroppedBytes);
        Assert.Equal((long)full.Payload.Length, second.ProducedBytes);
        Assert.Equal(0, (await legacy.GetDeliveryMetricsAsync()).ProducedBytes);
    }

    [Fact]
    public void OverBudgetPublicationDoesNotHandOutACopyOrConsumeRefresh()
    {
        var stream = new FixedBaselineSnapshotStream(tickRate: 30);
        Prepare(stream, 4);
        stream.RequestFull();
        var candidate = Projection(12);
        candidate.Payload = new byte[FixedBaselineStateSyncProtocol.MaximumSnapshotBytes - 36];

        Assert.False(stream.TryPrepareDelivery(candidate, false, out var rejected, out var wireLength, out var reason));
        Assert.Null(rejected);
        Assert.True(wireLength > FixedBaselineStateSyncProtocol.MaximumSnapshotBytes);
        Assert.Equal(FixedBaselineFullReason.RefreshRequested, reason);
        Assert.True(stream.TryPrepareDelivery(Projection(16), false, out var next, out _, out reason));
        Assert.True(next.IsFullSnapshot);
        Assert.Equal(2, next.Revision.SnapshotId);
        Assert.Equal(FixedBaselineFullReason.RefreshRequested, reason);
    }

    private static PreparedFixedBaselineSnapshot Prepare(FixedBaselineSnapshotStream stream, int frame)
    {
        Assert.True(stream.TryPrepareDelivery(Projection(frame), false, out var prepared, out _, out _));
        return prepared;
    }

    private static StateSyncObserverGrain CreateObserver(bool fixedBaseline)
    {
        var observer = new StateSyncObserverGrain(NullLogger<StateSyncObserverGrain>.Instance,
            Options.Create(new StateSyncObserverOptions { BytesPerSecond = 0, MaxQueueLength = 1 }));
        typeof(StateSyncObserverGrain).GetField("_usesFixedBaseline", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(observer, fixedBaseline);
        return observer;
    }

    private static void SetPreparedRecipient(StateSyncObserverGrain observer, StateSyncPreparedRecipient recipient) =>
        typeof(StateSyncObserverGrain).GetField("_preparedRecipient", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(observer, recipient);

    private static Task DrainAsync(StateSyncObserverGrain observer) =>
        (Task)typeof(StateSyncObserverGrain).GetMethod("DrainQueueAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(observer, null)!;

    private sealed class CountingGatewayObserver : IStateSyncGatewayPushObserver
    {
        public int Count { get; private set; }

        public void OnPush(uint opCode, byte[] payload) => Count++;
    }

    private static StateSyncPush Projection(int frame) => new()
    {
        WorldId = 7,
        Frame = frame,
        SchemaVersion = 1,
        NetworkEnvironmentId = "default",
        Actors = [new ActorSnapshot { ActorId = 1, X = frame }, new ActorSnapshot { ActorId = 2 }]
    };
}
