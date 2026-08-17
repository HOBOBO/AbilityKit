using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Protocol.Shooter;
using Xunit;

namespace AbilityKit.Demo.Shooter.Runtime.Tests.Application.Runtime;

public sealed class ShooterStateHashCacheTests
{
    [Fact]
    public void StableFrameExportsShareOneStateHashComputationAndMutationsInvalidateIt()
    {
        var runtime = new ShooterBattleRuntimePort();
        var start = new ShooterStartGamePayload(
            "state-hash-cache",
            30,
            9201,
            new[] { new ShooterStartPlayer(1, "P1", 0f, 0f) });
        Assert.True(runtime.StartGame(in start));

        var firstHash = runtime.ComputeStateHash();
        var packed = runtime.ExportPackedSnapshot(9201ul, isFullSnapshot: true, authorityOverride: true);
        var pureState = runtime.ExportPureStateSnapshot(9201ul, isFullBaseline: true);
        var stableDiagnostics = runtime.StateHashCacheDiagnostics;

        Assert.Equal(firstHash, packed.StateHash);
        Assert.Equal(firstHash, pureState.StateHash);
        Assert.Equal(1, stableDiagnostics.ComputationCount);
        Assert.Equal(2, stableDiagnostics.CacheHitCount);

        Assert.True(runtime.Tick(1f / 30f));
        runtime.ComputeStateHash();
        var tickDiagnostics = runtime.StateHashCacheDiagnostics;
        Assert.Equal(2, tickDiagnostics.ComputationCount);
        Assert.Equal(runtime.CurrentFrame, tickDiagnostics.CachedFrame);

        Assert.True(runtime.TryGetPlayer(1, out var player));
        player.X += 5f;
        runtime.SetPlayer(in player);
        var mutatedHash = runtime.ComputeStateHash();
        var mutationDiagnostics = runtime.StateHashCacheDiagnostics;

        Assert.NotEqual(firstHash, mutatedHash);
        Assert.Equal(3, mutationDiagnostics.ComputationCount);
    }
}
