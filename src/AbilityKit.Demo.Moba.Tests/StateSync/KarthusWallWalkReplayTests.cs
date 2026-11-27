using AbilityKit.Demo.Moba.BattleScenario.Runner;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.StateSync;

[Collection("Moba lifecycle preparation")]
[Trait("Gate", "BattleScenario")]
public sealed class KarthusWallWalkReplayTests
{
    [Fact]
    public void Formal_wall_slow_produces_repeatable_divergence_and_settled_replay()
    {
        var replay = KarthusWallWalkReplay.Record();
        Assert.Equal(replay.Serialize(), KarthusWallWalkReplay.Record().Serialize());
        var delivery = replay.Checkpoints.Single(point => point.Reconciliation == "Predicted");
        var before = replay.Checkpoints.Single(point =>
            point.DeliveryTick == delivery.DeliveryTick && point.HasUndeliveredAuthorityReference);
        Assert.Equal(1, delivery.Imports);
        Assert.Equal(1, delivery.TerminalSequence);
        Assert.Equal(new long[] { 2 }, delivery.Pending);
        Assert.Equal(new[] { delivery.AppliedTick + 1, delivery.AppliedTick + 2 }, delivery.ReplayTicks);
        Assert.NotEqual(before.Prediction, delivery.Prediction);
        Assert.Equal(before.Authority, delivery.Prediction);
        Assert.True(replay.Checkpoints[^1].Prediction > delivery.Prediction);
    }
}
