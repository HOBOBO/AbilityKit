using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Network.Runtime.LagCompensation;
using AbilityKit.Protocol.Shooter;
using Xunit;

namespace AbilityKit.Demo.Shooter.Runtime.Tests.Client;

public sealed class ShooterLagCompensationServiceTests
{
    [Fact]
    public void RecordsPlayerHitboxesDirectlyFromRuntime()
    {
        var runtime = new ShooterBattleRuntimePort();
        var start = new ShooterStartGamePayload(
            "lag-compensation-runtime",
            tickRate: 30,
            randomSeed: 7201,
            new[]
            {
                new ShooterStartPlayer(1, "P1", 0f, 0f),
                new ShooterStartPlayer(2, "P2", 5f, 0f)
            });
        var service = new ShooterLagCompensationService(new ServerRewindLagCompensationConfig(
            maxHistoryFrames: 8,
            maxRewindFrames: 10));

        Assert.True(runtime.StartGame(in start));
        Assert.True(runtime.Tick(1f / 30f));
        service.RecordFrame(runtime);

        Assert.Equal(1, service.CapturedFrameCount);
        Assert.Equal(runtime.CurrentFrame, service.LatestFrame);
    }

    [Fact]
    public void RecordsPlayerHitboxesFromShooterSnapshots()
    {
        var service = new ShooterLagCompensationService(new ServerRewindLagCompensationConfig(
            maxHistoryFrames: 8,
            maxRewindFrames: 10));
        var snapshot = Snapshot(10,
            new ShooterPlayerSnapshot(1, 0f, 0f, 1f, 0f, hp: 3, score: 0, alive: true),
            new ShooterPlayerSnapshot(2, 5f, 0f, 1f, 0f, hp: 3, score: 0, alive: true));

        service.RecordFrame(in snapshot);

        Assert.Equal(1, service.CapturedFrameCount);
        Assert.Equal(10, service.LatestFrame);
    }

    [Fact]
    public void AcceptsShotAgainstRewoundShooterPlayerPosition()
    {
        var service = new ShooterLagCompensationService(new ServerRewindLagCompensationConfig(
            maxHistoryFrames: 8,
            maxRewindFrames: 10));
        var frame10 = Snapshot(10,
            new ShooterPlayerSnapshot(1, 0f, 0f, 1f, 0f, hp: 3, score: 0, alive: true),
            new ShooterPlayerSnapshot(2, 5f, 0f, 1f, 0f, hp: 3, score: 0, alive: true));
        var frame12 = Snapshot(12,
            new ShooterPlayerSnapshot(1, 0f, 0f, 1f, 0f, hp: 3, score: 0, alive: true),
            new ShooterPlayerSnapshot(2, 5f, 3f, 1f, 0f, hp: 3, score: 0, alive: true));
        service.RecordFrame(in frame10);
        service.RecordFrame(in frame12);
        var shot = new ShooterLagCompensationShot(
            shooterPlayerId: 1,
            originX: 0f,
            originY: 0f,
            directionX: 1f,
            directionY: 0f,
            maxDistance: 10f,
            rewindFrame: 10,
            serverReceiveFrame: 12);

        var accepted = service.TryEvaluateShot(in shot, out var result);

        Assert.True(accepted);
        Assert.True(result.Accepted);
        Assert.Equal(LagCompensationResultReason.Hit, result.Reason);
        Assert.Equal(10, result.RequestedFrame);
        Assert.Equal(10, result.EvaluatedFrame);
        Assert.Equal(2, result.HitEntityId);
        Assert.True(result.Distance >= 0f);
    }

    [Fact]
    public void RejectsDeadPlayersAndShooterSelf()
    {
        var service = new ShooterLagCompensationService(new ServerRewindLagCompensationConfig(
            maxHistoryFrames: 8,
            maxRewindFrames: 10));
        var frame10 = Snapshot(10,
            new ShooterPlayerSnapshot(1, 0.5f, 0f, 1f, 0f, hp: 3, score: 0, alive: true),
            new ShooterPlayerSnapshot(2, 3f, 0f, 1f, 0f, hp: 0, score: 0, alive: false));
        service.RecordFrame(in frame10);
        var shot = new ShooterLagCompensationShot(
            shooterPlayerId: 1,
            originX: 0f,
            originY: 0f,
            directionX: 1f,
            directionY: 0f,
            maxDistance: 10f,
            rewindFrame: 10,
            serverReceiveFrame: 10);

        var accepted = service.TryEvaluateShot(in shot, out var result);

        Assert.False(accepted);
        Assert.False(result.Accepted);
        Assert.Equal(LagCompensationResultReason.Miss, result.Reason);
        Assert.Equal(10, result.RequestedFrame);
        Assert.Equal(10, result.EvaluatedFrame);
        Assert.Equal(0, result.HitEntityId);
    }

    private static ShooterStateSnapshotPayload Snapshot(int frame, params ShooterPlayerSnapshot[] players)
    {
        return new ShooterStateSnapshotPayload(
            frame,
            players,
            System.Array.Empty<ShooterBulletSnapshot>(),
            System.Array.Empty<ShooterEventSnapshot>());
    }
}
