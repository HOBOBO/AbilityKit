using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Demo.Shooter.View.PlayMode;
using AbilityKit.Network.Runtime;
using AbilityKit.Protocol.Shooter;
using Xunit;

namespace AbilityKit.Demo.Shooter.Runtime.Tests;

public sealed class ShooterGameplayScenarioWorldHostFactoryTests
{
    [Theory]
    [InlineData(NetworkSyncModel.AuthoritativeInterpolation)]
    [InlineData(NetworkSyncModel.BatchStateSync)]
    [InlineData(NetworkSyncModel.MassBattleLodSync)]
    public void AuthoritativeRemoteClientWorldDoesNotSimulateServerEnemies(NetworkSyncModel syncModel)
    {
        var options = CreateSessionOptions(syncModel);
        using var world = ShooterGameplayScenarioWorldHostFactory.CreateBattleWorld(
            $"lightweight-client-{syncModel}",
            options);
        var start = new ShooterStartGamePayload(
            "lightweight-client",
            30,
            7101,
            new[] { new ShooterStartPlayer(1, "P1", 0f, 0f) });

        Assert.True(world.Runtime.StartGame(in start));
        for (var i = 0; i < 8; i++)
        {
            Assert.True(world.Runtime.Tick(1f / 30f));
        }

        Assert.Empty(world.Runtime.GetSnapshotTransient().Enemies);
        Assert.True(world.World.Services.TryResolve<ShooterEnemyWaveOptions>(out var enemyWaves));
        Assert.NotNull(enemyWaves);
        Assert.False(enemyWaves!.Enabled);
    }

    [Fact]
    public void PredictRollbackClientWorldRetainsLocalEnemySimulation()
    {
        var options = CreateSessionOptions(NetworkSyncModel.PredictRollback);
        using var world = ShooterGameplayScenarioWorldHostFactory.CreateBattleWorld(
            "predictive-client",
            options);
        var start = new ShooterStartGamePayload(
            "predictive-client",
            30,
            7102,
            new[] { new ShooterStartPlayer(1, "P1", 0f, 0f) });

        Assert.True(world.Runtime.StartGame(in start));
        for (var i = 0; i < 8; i++)
        {
            Assert.True(world.Runtime.Tick(1f / 30f));
        }

        Assert.NotEmpty(world.Runtime.GetSnapshotTransient().Enemies);
        Assert.True(world.World.Services.TryResolve<ShooterEnemyWaveOptions>(out var enemyWaves));
        Assert.NotNull(enemyWaves);
        Assert.True(enemyWaves!.Enabled);
    }

    private static ShooterPlayModeSessionOptions CreateSessionOptions(NetworkSyncModel syncModel)
    {
        var defaults = ShooterPlayModeSessionOptions.Default;
        return new ShooterPlayModeSessionOptions(
            syncModel,
            defaults.TickRate,
            playerCount: 1,
            randomSeed: 7100,
            controlledPlayerId: 1,
            enableAuthoritativeWorld: true,
            latencyMs: 0,
            jitterMs: 0,
            packetLossRate: 0f,
            reorderRate: 0f,
            bandwidthKbps: 0,
            worldScale: 1f,
            networkName: "client-world-test",
            syncTemplateId: defaults.SyncTemplateId,
            defaults.GameplayScenario);
    }
}
