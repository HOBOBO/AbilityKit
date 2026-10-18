using AbilityKit.Demo.Moba.Presentation.GodotSample;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Game.Flow;
using Xunit;

namespace AbilityKit.Demo.Moba.Presentation.Core.Tests;

public sealed class BattleClientSessionDriverTests
{
    [Fact]
    public async Task TwoClientsKeepIndependentViewsAndRecoverFromFullSnapshots()
    {
        var firstHost = new TestHost();
        var secondHost = new TestHost();
        using var first = new BattleClientSessionDriver(firstHost);
        using var second = new BattleClientSessionDriver(secondHost);
        var firstView = new RecordingActorPort();
        var secondView = new RecordingActorPort();
        first.AttachProjection("main", new BattleActorProjection(firstView));
        second.AttachProjection("main", new BattleActorProjection(secondView));

        await Task.WhenAll(first.StartAsync(), second.StartAsync());
        first.QueueSnapshot(PresentationFixture.Initial());
        second.QueueSnapshot(PresentationFixture.Initial());
        first.Tick(1f / 30f);
        second.Tick(1f / 30f);
        Assert.Equal(2, firstView.Count);
        Assert.Equal(2, secondView.Count);
        Assert.Equal(1, firstHost.TickCount);
        Assert.Equal(1, secondHost.TickCount);

        first.NotifyConnectionLost();
        first.QueueSnapshot(PresentationFixture.Delta());
        first.Tick(1f / 30f);
        Assert.Equal(BattleClientSessionStatus.Recovering, first.Status);
        Assert.Equal(1, first.LastSnapshotFrame);
        Assert.Equal(2, firstView.Count);

        var recovered = new GatewayStateSyncSnapshot(
            42UL, 3, 0.1d, true,
            [new GatewayStateSyncActorSnapshot(101, 3f, 0f, 0f, 0f, 0f, 0f, 80f, 100f, 1)]);
        first.QueueSnapshot(recovered);
        first.Tick(1f / 30f);
        Assert.Equal(BattleClientSessionStatus.Running, first.Status);
        Assert.Equal(3, first.LastSnapshotFrame);
        Assert.Equal(1, firstView.Count);
        Assert.Equal(2, secondView.Count);

        await Task.WhenAll(first.StopAsync(), second.StopAsync());
        Assert.Empty(firstView.ActorIds);
        Assert.Empty(secondView.ActorIds);
        Assert.Equal(1, firstHost.StopCount);
        Assert.Equal(1, secondHost.StopCount);
    }

    [Fact]
    public async Task LateProjectionRehydratesAndDetachLeavesOtherViewsRunning()
    {
        using var driver = new BattleClientSessionDriver(new TestHost());
        await driver.StartAsync();
        Assert.True(driver.PresentSnapshot(PresentationFixture.Initial()));

        var primary = new RecordingActorPort();
        var observer = new RecordingActorPort();
        driver.AttachProjection("primary", new BattleActorProjection(primary));
        driver.AttachProjection("observer", new BattleActorProjection(observer));
        Assert.Equal(2, primary.Count);
        Assert.Equal(2, observer.Count);

        Assert.True(driver.DetachProjection("observer"));
        Assert.Empty(observer.ActorIds);
        Assert.True(driver.PresentSnapshot(PresentationFixture.Delta()));
        Assert.Equal(1, primary.Count);
        Assert.Empty(observer.ActorIds);
        Assert.False(driver.PresentSnapshot(PresentationFixture.Delta()));
        Assert.Equal(2, driver.LastSnapshotFrame);
        await driver.StopAsync();
    }

    [Fact]
    public async Task StartAndStopAreIdempotentAcrossConcurrentCallers()
    {
        var host = new TestHost();
        using var driver = new BattleClientSessionDriver(host);
        await Task.WhenAll(driver.StartAsync(), driver.StartAsync());
        Assert.Equal(1, host.StartCount);
        await Task.WhenAll(driver.StopAsync(), driver.StopAsync());
        Assert.Equal(1, host.StopCount);
        Assert.Equal(BattleClientSessionStatus.Stopped, driver.Status);
    }

    [Fact]
    public async Task InterpolationMayRefreshTheSameFrameWithoutAcceptingOlderFrames()
    {
        using var driver = new BattleClientSessionDriver(new TestHost());
        var port = new RecordingActorPort();
        driver.AttachProjection("main", new BattleActorProjection(port));
        await driver.StartAsync();
        Assert.True(driver.PresentSnapshot(PresentationFixture.Initial()));
        var interpolated = new GatewayStateSyncSnapshot(
            42UL, 1, 0d, true,
            [new GatewayStateSyncActorSnapshot(101, 5f, 0f, 0f, 0f, 0f, 0f, 100f, 100f, 1)]);
        Assert.True(driver.PresentInterpolatedSnapshot(interpolated));
        Assert.Equal(5f, port.GetActor(101).X);
        Assert.False(driver.PresentSnapshot(interpolated));
        await driver.StopAsync();
    }

    [Fact]
    public async Task BackgroundDisconnectPreservesNewFullBaselineUntilPresentationTick()
    {
        using var driver = new BattleClientSessionDriver(new TestHost());
        var baselineRequests = 0;
        driver.FullSnapshotRequired += () => baselineRequests++;
        var port = new RecordingActorPort();
        driver.AttachProjection("main", new BattleActorProjection(port));
        await driver.StartAsync();
        driver.QueueSnapshot(PresentationFixture.Initial());
        driver.Tick(0f);

        await Task.Run(driver.NotifyConnectionLost);
        Assert.Equal(BattleClientSessionStatus.Running, driver.Status);
        driver.QueueSnapshot(PresentationFixture.Delta());
        var baseline = new GatewayStateSyncSnapshot(
            42UL, 3, 0.1d, true,
            [new GatewayStateSyncActorSnapshot(303, 0f, 0f, 0f, 0f, 0f, 0f, 100f, 100f, 1)]);
        driver.QueueSnapshot(in baseline);
        driver.Tick(0f);
        Assert.Equal(BattleClientSessionStatus.Running, driver.Status);
        Assert.Equal(303, Assert.Single(port.ActorIds));
        Assert.Equal(0, baselineRequests);
    }

    [Fact]
    public async Task FailedStopStillClearsProjectionAndCanRetryStart()
    {
        var host = new TestHost { FailNextStop = true };
        using var driver = new BattleClientSessionDriver(host);
        var port = new RecordingActorPort();
        driver.AttachProjection("main", new BattleActorProjection(port));
        await driver.StartAsync();
        driver.PresentSnapshot(PresentationFixture.Initial());

        await Assert.ThrowsAsync<InvalidOperationException>(driver.StopAsync);
        Assert.Equal(BattleClientSessionStatus.Faulted, driver.Status);
        Assert.Empty(port.ActorIds);
        Assert.Equal(0, driver.LastSnapshotFrame);

        await driver.StartAsync();
        Assert.Equal(BattleClientSessionStatus.Running, driver.Status);
        Assert.Equal(2, host.StopCount);
        await driver.StopAsync();
    }

    [Fact]
    public async Task QueueOverflowRequiresFullBaselineAndKeepsTheNewestRecoverySnapshot()
    {
        using var driver = new BattleClientSessionDriver(
            new TestHost(), new BattleClientSessionOptions(maxPendingSnapshots: 2));
        var baselineRequests = 0;
        driver.FullSnapshotRequired += () => baselineRequests++;
        var port = new RecordingActorPort();
        driver.AttachProjection("main", new BattleActorProjection(port));
        await driver.StartAsync();
        driver.PresentSnapshot(PresentationFixture.Initial());

        driver.QueueSnapshot(PresentationFixture.Delta());
        driver.QueueSnapshot(Snapshot(frame: 3, full: false, actorId: 101));
        driver.QueueSnapshot(Snapshot(frame: 4, full: false, actorId: 101));
        Assert.Equal(0, driver.PendingSnapshotCount);
        Assert.Equal(3, driver.DiscardedPendingSnapshotCount);
        driver.Tick(0f);
        Assert.Equal(BattleClientSessionStatus.Recovering, driver.Status);
        Assert.True(driver.RequiresFullSnapshot);
        Assert.Equal(1, baselineRequests);
        Assert.Equal(1, driver.LastSnapshotFrame);
        Assert.Equal(2, port.Count);

        driver.QueueSnapshot(Snapshot(frame: 5, full: false, actorId: 101));
        driver.QueueSnapshot(Snapshot(frame: 6, full: true, actorId: 404));
        driver.Tick(0f);
        Assert.Equal(BattleClientSessionStatus.Running, driver.Status);
        Assert.False(driver.RequiresFullSnapshot);
        Assert.Equal(6, driver.LastSnapshotFrame);
        Assert.Equal(404, Assert.Single(port.ActorIds));
        Assert.Equal(3, driver.DiscardedPendingSnapshotCount);
    }

    [Fact]
    public async Task LateProjectionReceivesStableActorOrderAndFailedAttachDoesNotRegister()
    {
        using var driver = new BattleClientSessionDriver(new TestHost());
        await driver.StartAsync();
        var baseline = new GatewayStateSyncSnapshot(42UL, 1, 0d, true,
        [
            Actor(303), Actor(101), Actor(202)
        ]);
        Assert.True(driver.PresentSnapshot(in baseline));

        var probe = new SnapshotOrderProbe { ActorIds = [999] };
        driver.AttachProjection("observer", probe);
        Assert.Equal(1, probe.ResetCount);
        Assert.Equal(new[] { 101, 202, 303 }, probe.ActorIds);
        Assert.Throws<InvalidOperationException>(() =>
            driver.AttachProjection("broken", new FailingProjection()));
        Assert.Equal(1, driver.ProjectionCount);
        await driver.StopAsync();
    }

    [Fact]
    public async Task ContinuousIngressCannotKeepOneTickInsideSnapshotDrain()
    {
        using var driver = new BattleClientSessionDriver(new TestHost());
        driver.AttachProjection("producer", new EnqueueingProjection(driver));
        await driver.StartAsync();
        driver.QueueSnapshot(Snapshot(frame: 1, full: true, actorId: 101));

        driver.Tick(0f);
        Assert.Equal(2, driver.LastSnapshotFrame);
        Assert.Equal(1, driver.PendingSnapshotCount);
        driver.Tick(0f);
        Assert.Equal(4, driver.LastSnapshotFrame);
        Assert.Equal(1, driver.PendingSnapshotCount);
    }

    private static GatewayStateSyncSnapshot Snapshot(int frame, bool full, int actorId) =>
        new GatewayStateSyncSnapshot(42UL, frame, 0d, full, [Actor(actorId)]);

    private static GatewayStateSyncActorSnapshot Actor(int actorId) =>
        new GatewayStateSyncActorSnapshot(actorId, 0f, 0f, 0f, 0f, 0f, 0f, 100f, 100f, 1);

    private sealed class TestHost : IBattleClientSessionHost
    {
        public int StartCount { get; private set; }
        public int StopCount { get; private set; }
        public int TickCount { get; private set; }
        public bool FailNextStop { get; set; }
        public Task StartAsync()
        {
            StartCount++;
            return Task.CompletedTask;
        }
        public Task StopAsync()
        {
            StopCount++;
            if (FailNextStop)
            {
                FailNextStop = false;
                throw new InvalidOperationException("simulated stop failure");
            }
            return Task.CompletedTask;
        }
        public void Tick(float deltaTime) => TickCount++;
    }

    private sealed class RecordingActorPort : IBattleActorViewPort
    {
        private readonly Dictionary<int, GatewayStateSyncActorSnapshot> _actors = new();
        public int Count => _actors.Count;
        public IEnumerable<int> ActorIds => _actors.Keys;
        public GatewayStateSyncActorSnapshot GetActor(int id) => _actors[id];
        public IEnumerable<int> GetActorIds() => _actors.Keys.ToArray();
        public void Upsert(in GatewayStateSyncActorSnapshot actor) => _actors[actor.ActorId] = actor;
        public void Remove(int actorId) => _actors.Remove(actorId);
    }

    private sealed class SnapshotOrderProbe : IBattleClientProjection
    {
        public int[] ActorIds { get; set; } = [];
        public int ResetCount { get; private set; }
        public void Apply(in GatewayStateSyncSnapshot snapshot) =>
            ActorIds = snapshot.Actors.Select(actor => actor.ActorId).ToArray();
        public void Reset()
        {
            ActorIds = [];
            ResetCount++;
        }
    }

    private sealed class FailingProjection : IBattleClientProjection
    {
        public void Apply(in GatewayStateSyncSnapshot snapshot) =>
            throw new InvalidOperationException("projection unavailable");
        public void Reset() { }
    }

    private sealed class EnqueueingProjection : IBattleClientProjection
    {
        private readonly BattleClientSessionDriver _driver;
        public EnqueueingProjection(BattleClientSessionDriver driver) => _driver = driver;
        public void Apply(in GatewayStateSyncSnapshot snapshot)
        {
            if (snapshot.Frame < 10)
                _driver.QueueSnapshot(Snapshot(snapshot.Frame + 1, true, 101));
        }
        public void Reset() { }
    }
}
