using AbilityKit.Coordinator;
using System.Collections.Generic;
using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Protocol.Shooter;
using Xunit;

namespace AbilityKit.Demo.Shooter.Runtime.Tests;

public sealed class ShooterBattleDriverHostTests
{
    [Fact]
    public void AdvanceFrameUsesSharedCoordinatorDriverBridge()
    {
        var runtime = CreateStartedRuntime();
        var driver = new ShooterBattleDriverHost(runtime);

        driver.Start();
        driver.AdvanceFrame(1f / 30f);

        Assert.True(driver.IsRunning);
        Assert.Equal(1, driver.CurrentFrame);
        Assert.Equal(runtime.CurrentFrame, driver.CurrentFrame);
        Assert.Equal(1d / 30d, driver.LogicTimeSeconds, precision: 6);
    }

    [Fact]
    public void SubmitInputsAcceptsCoordinatorPlayerInputPayload()
    {
        var runtime = CreateStartedRuntime();
        var driver = new ShooterBattleDriverHost(runtime);
        var command = new ShooterPlayerCommand(1, 1f, 0f, 1f, 0f, fire: true);
        var input = new PlayerInput(
            frame: 0,
            playerId: 1,
            opCode: ShooterOpCodes.Input.PlayerCommand,
            payload: ShooterInputCodec.Serialize(new[] { command }));

        driver.Start();
        driver.SubmitInputs(new[] { input });
        driver.AdvanceFrame(1f / 30f);

        var snapshot = runtime.GetSnapshot();
        Assert.Equal(1, driver.CurrentFrame);
        Assert.NotEmpty(snapshot.Bullets);
    }

    [Fact]
    public void GetAllEntityStatesProjectsShooterSnapshotToCoordinatorSnapshotStates()
    {
        var runtime = CreateStartedRuntime();
        var driver = new ShooterBattleDriverHost(runtime);

        driver.Start();
        var states = driver.GetAllEntityStates();

        Assert.Equal(2, states.Length);
        Assert.Contains(states, state => state.EntityId == 1);
        Assert.Contains(states, state => state.EntityId == 2);
    }

    [Fact]
    public void FillAllEntityStatesWritesCallerOwnedBuffer()
    {
        var runtime = CreateStartedRuntime();
        var driver = new ShooterBattleDriverHost(runtime);
        var destination = new List<SnapshotEntityState>();

        driver.Start();
        var count = driver.FillAllEntityStates(destination);

        Assert.Equal(2, count);
        Assert.Equal(2, destination.Count);
        Assert.Contains(destination, state => state.EntityId == 1);
        Assert.Contains(destination, state => state.EntityId == 2);
    }

    private static ShooterBattleRuntimePort CreateStartedRuntime()
    {
        var runtime = new ShooterBattleRuntimePort();
        var start = new ShooterStartGamePayload(
            "driver-host-tests",
            30,
            13579,
            new[]
            {
                new ShooterStartPlayer(1, "P1", 0f, 0f),
                new ShooterStartPlayer(2, "P2", 3f, 0f)
            });
        Assert.True(runtime.StartGame(in start));
        return runtime;
    }
}
