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

    private static StateSyncPush Projection(int frame) => new()
    {
        WorldId = 7,
        Frame = frame,
        SchemaVersion = 1,
        NetworkEnvironmentId = "default",
        Actors = [new ActorSnapshot { ActorId = 1, X = frame }, new ActorSnapshot { ActorId = 2 }]
    };
}
