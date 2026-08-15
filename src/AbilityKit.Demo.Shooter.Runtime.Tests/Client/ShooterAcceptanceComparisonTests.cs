using System.Linq;
using AbilityKit.Demo.Shooter.View;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Conditioning;
using AbilityKit.Demo.Harness;
using Xunit;

namespace AbilityKit.Demo.Shooter.Runtime.Tests;

[Collection("ShooterAcceptance")]
public sealed class ShooterAcceptanceComparisonTests
{
    [Fact]
    public void AuthoritativeWorldIsOptionalAndDisabledByDefault()
    {
        var session = ShooterAcceptanceLab.Create(
            NetworkSyncModel.PredictRollback,
            NetworkConditionProfile.Ideal);

        Assert.False(session.HasAuthoritativeWorld);
        Assert.Null(session.AuthoritativeWorld);
        Assert.Null(session.AuthoritativePresentation);

        var comparison = session.CompareWorlds();
        Assert.Empty(comparison.Divergences);
    }

    [Fact]
    public void EnablingAuthoritativeWorldStartsAnIndependentGroundTruthWorld()
    {
        var session = ShooterAcceptanceLab.Create(
            NetworkSyncModel.PredictRollback,
            NetworkConditionProfile.Ideal,
            enableAuthoritativeWorld: true);

        Assert.True(session.HasAuthoritativeWorld);
        Assert.NotNull(session.AuthoritativeWorld);
        Assert.NotNull(session.AuthoritativePresentation);
        Assert.True(session.AuthoritativeWorld!.IsStarted);
        // Both worlds start frame-aligned.
        Assert.Equal(session.Runtime.CurrentFrame, session.AuthoritativeWorld.CurrentFrame);
    }

    [Fact]
    public void AuthoritativeWorldAdvancesInLockstepAfterRun()
    {
        var session = ShooterAcceptanceLab.Create(
            NetworkSyncModel.PredictRollback,
            NetworkConditionProfile.Ideal,
            enableAuthoritativeWorld: true);

        session.Run(stepCount: 4, deltaSeconds: 1f / 30f);

        Assert.Equal(session.Runtime.CurrentFrame, session.AuthoritativeWorld!.CurrentFrame);
        // The authoritative presentation was projected so the shell can render the second world.
        Assert.Equal(session.AuthoritativeWorld.CurrentFrame, session.AuthoritativePresentation!.ViewModel.Frame);
    }

    [Fact]
    public void CompareWorldsReportsPerPlayerDivergence()
    {
        var session = ShooterAcceptanceLab.Create(
            NetworkSyncModel.PredictRollback,
            NetworkConditionProfile.Ideal,
            enableAuthoritativeWorld: true);

        session.Run(stepCount: 3, deltaSeconds: 1f / 30f);

        var comparison = session.CompareWorlds();

        Assert.Equal(session.Runtime.CurrentFrame, comparison.ClientFrame);
        Assert.Equal(session.AuthoritativeWorld!.CurrentFrame, comparison.AuthorityFrame);
        // Default roster has two players; both should be reported.
        Assert.Equal(2, comparison.Divergences.Count);
        Assert.All(comparison.Divergences, d => Assert.InRange(d.Distance, 0d, double.MaxValue));
    }

    [Fact]
    public void ApplyNetworkChangesActiveProfileForSubsequentRun()
    {
        var session = ShooterAcceptanceLab.Create(
            NetworkSyncModel.PredictRollback,
            NetworkConditionProfile.Ideal,
            networkName: "Ideal");

        Assert.Equal(NetworkConditionProfile.Ideal, session.NetworkProfile);

        // Live-tune to a poor-wifi-like profile built from runtime sliders.
        var tuned = new NetworkConditionProfile(120, 40, 0.03d, 0.02d, 0);
        session.ApplyNetwork(tuned, "Tuned 120ms");

        Assert.Equal(tuned, session.NetworkProfile);
        Assert.Equal("Tuned 120ms", session.NetworkName);

        var result = session.Run(stepCount: 2);
        Assert.True(result.Completed);
        Assert.Equal(tuned, result.Scenario.NetworkProfile);
    }

    [Fact]
    public void TickAuthoritativeWorldKeepsSecondWorldAlignedDuringFrameByFrameDrive()
    {
        var session = ShooterAcceptanceLab.Create(
            NetworkSyncModel.PredictRollback,
            NetworkConditionProfile.Ideal,
            enableAuthoritativeWorld: true);

        // Simulate the Unity shell driving frame-by-frame: step the client carrier, then keep the
        // authoritative world aligned via the dedicated helper.
        for (var i = 0; i < 3; i++)
        {
            session.Controller.Tick(1f / 30f);
            session.TickAuthoritativeWorld(1f / 30f);
        }

        Assert.Equal(session.Runtime.CurrentFrame, session.AuthoritativeWorld!.CurrentFrame);
    }

    [Fact]
    public void MaxDistanceReflectsLargestPerEntityDivergence()
    {
        var session = ShooterAcceptanceLab.Create(
            NetworkSyncModel.PredictRollback,
            NetworkConditionProfile.Ideal,
            enableAuthoritativeWorld: true);

        session.Run(stepCount: 5, deltaSeconds: 1f / 30f);

        var comparison = session.CompareWorlds();
        var expectedMax = comparison.Divergences.Max(d => d.Distance);

        Assert.Equal(expectedMax, comparison.MaxDistance, precision: 9);
    }
}
