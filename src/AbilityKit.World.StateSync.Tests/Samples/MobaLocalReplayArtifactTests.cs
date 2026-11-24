using AbilityKit.BattleScenario.Editor;
using Xunit;

namespace AbilityKit.World.StateSync.Tests;

public sealed class MobaLocalReplayArtifactTests
{
    [Theory]
    [InlineData("confirmed", "AuthoritativeOnly", 2)]
    [InlineData("corrected", "Predicted", 5)]
    [InlineData("without-proof", "InvalidTerminalProof", 2)]
    [InlineData("history-gap", "MissingTickCoverage", 2)]
    public void RecordedMobaVariantsShowOnlyObservedMovement(string variant, string status, int count)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Samples", "ReplaySamples", $"gragas-e-local-statesync.{variant}.statesync-replay.json");
        var recording = LocalStateSyncReplay.Parse(File.ReadAllText(path));
        Assert.Contains("Z", recording.PositionLabel);
        Assert.Equal(recording.PositionLabel, LocalStateSyncReplay.Parse(recording.Serialize()).PositionLabel);
        var frames = recording.BuildTimeline().Frames;
        Assert.Equal(count, frames.Count);
        Assert.Equal(status, frames[^1].Point.Reconciliation);
        Assert.Equal(60, frames[^1].Point.AppliedTick);
        Assert.Equal(-2000, frames[^1].Point.Authority);
        Assert.Equal(variant == "confirmed" ? -2000 : 1354, frames[0].Point.Prediction);

        if (variant != "corrected")
        {
            Assert.Null(frames[^1].Point.Prediction);
            Assert.Empty(frames[^1].Point.ReplayTicks);
            return;
        }

        Assert.Equal(-2000, frames[1].Position);
        Assert.Equal(1354, frames[1].PreviousPrediction);
        Assert.Equal(new[] { 61, 62 }, frames[^1].Point.ReplayTicks);
        Assert.Equal(-2110, frames[2].Position);
        Assert.Equal(frames[2].Position, frames[3].Position);
        Assert.Equal(frames[3].Position, frames[^1].Point.Prediction);
        Assert.Equal(new long[] { 2 }, frames[^1].Point.Pending);
    }
}
