using AbilityKit.BattleScenario.Editor;
using Newtonsoft.Json.Linq;
using Xunit;

namespace AbilityKit.World.StateSync.Tests;

public sealed class MobaLocalReplayArtifactTests
{
    [Theory]
    [InlineData("confirmed", true)]
    [InlineData("rejected", false)]
    public void ProjectileRecordingStepsOneTickAtATimeWithoutInventingAView(string variant, bool confirmed)
    {
        var recording = LocalStateSyncReplay.Parse(ReadProjectileRecording(variant));
        Assert.True(recording.HasPresentationTrack);
        Assert.Null(recording.ActorModelId);
        var timeline = recording.BuildTimeline();
        Assert.Equal(Enumerable.Range(37, 7), timeline.Frames.Select(frame => frame.Point.DeliveryTick));
        Assert.All(timeline.Frames, frame => Assert.Equal(LocalStateSyncReplay.FramePhase.Checkpoint, frame.Phase));
        var samples = timeline.Frames.Select(frame => frame.Point.Presentation!).ToArray();
        Assert.Equal(confirmed ? new int?[] { -2900, -1800, -700, 400, 1500, 2600, null }
                               : new int?[] { -2900, -1800, null, null, null, null, null }, samples.Select(sample => sample.View?.Z));
        Assert.Equal(confirmed ? samples.Select(sample => sample.View?.Z) : Enumerable.Repeat<int?>(null, 7),
            samples.Select(sample => sample.Authority?.Z));
        Assert.Equal(confirmed ? "confirmed" : "rejected", samples[2].Phase);
        Assert.Equal(confirmed, samples[2].Promoted);
        Assert.True(samples[2].DuplicateIgnored);
        Assert.All(samples.Where(sample => sample.View != null), sample => Assert.Equal(samples[0].Handle, sample.Handle));
        Assert.All(samples.Where(sample => sample.View == null), sample => Assert.Null(sample.Handle));
        Assert.All(samples, sample => Assert.Equal(1L, sample.Created));
        Assert.Equal((0, 1L), (samples[^1].Active, samples[^1].Destroyed));
        if (!confirmed)
            Assert.All(samples.Skip(3), sample => Assert.Equal("SupersededPrediction", sample.RejectReason));

        var other = LocalStateSyncReplay.Parse(ReadProjectileRecording(confirmed ? "rejected" : "confirmed")).BuildTimeline();
        Assert.Equal(2, other.FindMatchingCheckpoint(timeline, 2));
    }

    [Fact]
    public void ProjectileRecordingRejectsMissingTicksAndPositionsForInactiveViews()
    {
        var json = ReadProjectileRecording("rejected");
        var missing = JObject.Parse(json);
        ((JObject)missing["checkpoints"]![2]!).Remove("presentation");
        Assert.Throws<InvalidDataException>(() => LocalStateSyncReplay.Parse(missing.ToString()));

        var stale = JObject.Parse(json);
        stale["checkpoints"]![2]!["presentation"]!["view"] = stale["checkpoints"]![1]!["presentation"]!["view"]!.DeepClone();
        Assert.Throws<InvalidDataException>(() => LocalStateSyncReplay.Parse(stale.ToString()));
    }

    private static string ReadProjectileRecording(string variant) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
        "Samples", "ReplaySamples", $"jinx-w-local-presentation.{variant}.statesync-replay.json"));

    [Theory]
    [InlineData("confirmed", "AuthoritativeOnly", 2)]
    [InlineData("corrected", "Predicted", 6)]
    [InlineData("without-proof", "InvalidTerminalProof", 2)]
    [InlineData("history-gap", "MissingTickCoverage", 2)]
    public void RecordedMobaVariantsShowOnlyObservedMovement(string variant, string status, int count)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Samples", "ReplaySamples", $"gragas-e-local-statesync.{variant}.statesync-replay.json");
        var recording = LocalStateSyncReplay.Parse(File.ReadAllText(path));
        Assert.Equal(3005, recording.ActorModelId);
        Assert.Contains("Z", recording.PositionLabel);
        Assert.Equal(recording.PositionLabel, LocalStateSyncReplay.Parse(recording.Serialize()).PositionLabel);
        var timeline = recording.BuildTimeline();
        var frames = timeline.Frames;
        Assert.Equal(count, frames.Count);
        Assert.Equal(status, frames[^1].Point.Reconciliation);
        Assert.Equal(60, frames[^1].Point.AppliedTick);
        Assert.Equal(-2000, frames[^1].Point.Authority);
        Assert.Equal(variant == "confirmed" ? -2000 : 1354, frames[0].Point.Prediction);
        Assert.Equal(7f, frames[0].PredictionPose!.X);
        Assert.True(frames[0].PredictionPose!.IsValid);
        Assert.Null(frames[0].Point.AuthorityPose);
        Assert.Equal(-2f, frames[^1].Point.AuthorityPose!.Z);

        if (variant != "corrected")
        {
            Assert.All(frames, frame => Assert.Equal(LocalStateSyncReplay.FramePhase.Checkpoint, frame.Phase));
            Assert.Null(frames[^1].Point.Prediction);
            Assert.Null(frames[^1].PredictionPose);
            Assert.NotNull(frames[^1].AuthorityProjectionPose);
            Assert.Empty(frames[^1].Point.ReplayTicks);
            return;
        }

        Assert.Equal(new[] { LocalStateSyncReplay.FramePhase.Checkpoint, LocalStateSyncReplay.FramePhase.Compare,
            LocalStateSyncReplay.FramePhase.Anchor, LocalStateSyncReplay.FramePhase.ReplayTick,
            LocalStateSyncReplay.FramePhase.ReplayTick, LocalStateSyncReplay.FramePhase.Checkpoint }, frames.Select(frame => frame.Phase));
        Assert.Equal(1, timeline.Deliveries[1].FirstFrame);
        Assert.Equal(5, timeline.Deliveries[1].CheckpointFrame);
        var comparison = frames[1];
        Assert.Equal(1354, comparison.PreviousPrediction);
        Assert.Same(frames[0].PredictionPose, comparison.PredictionPose);
        Assert.Equal(-2f, comparison.AuthorityProjectionPose!.Z);
        var anchor = frames[2];
        Assert.Equal(-2000, anchor.Position);
        Assert.Same(anchor.Point.AuthorityPose, anchor.PredictionPose);
        Assert.True(anchor.SharesAnchorPose);
        Assert.Null(anchor.AuthorityProjectionPose);
        Assert.Equal(new[] { 61, 62 }, frames[^1].Point.ReplayTicks);
        Assert.Equal(61, frames[3].Step!.Tick);
        Assert.Equal(-2110, frames[3].Position);
        Assert.Equal(-2.11f, frames[3].PredictionPose!.Z, 4);
        Assert.Equal(62, frames[4].Step!.Tick);
        Assert.Equal(frames[3].Position, frames[4].Position);
        Assert.Equal(frames[4].Position, frames[^1].Point.Prediction);
        Assert.Equal(62, frames[^1].Point.DeliveryTick);
        Assert.Equal(new long[] { 2 }, frames[^1].Point.Pending);
    }

    [Fact]
    public void MovementHostRecordingStepsThroughSameTickComparisonAndContinues()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Samples", "ReplaySamples", "moba-local-movement.corrected.statesync-replay.json");
        var json = File.ReadAllText(path);
        var timeline = LocalStateSyncReplay.Parse(json).BuildTimeline();
        var frames = timeline.Frames;
        Assert.Equal(new[] { LocalStateSyncReplay.FramePhase.Checkpoint, LocalStateSyncReplay.FramePhase.Checkpoint,
            LocalStateSyncReplay.FramePhase.Checkpoint, LocalStateSyncReplay.FramePhase.Compare,
            LocalStateSyncReplay.FramePhase.Anchor, LocalStateSyncReplay.FramePhase.ReplayTick,
            LocalStateSyncReplay.FramePhase.ReplayTick, LocalStateSyncReplay.FramePhase.Checkpoint,
            LocalStateSyncReplay.FramePhase.Checkpoint }, frames.Select(frame => frame.Phase));
        Assert.Equal(new[] { 7625, 8000, 8375 }, frames.Take(3).Select(frame => frame.Point.Prediction!.Value));
        Assert.Equal(1, frames[3].Point.ComparisonTick);
        Assert.Equal(3, frames[3].Point.DeliveryTick);
        Assert.Equal(7625, frames[3].PreviousPrediction);
        Assert.Same(frames[0].PredictionPose, frames[3].PredictionPose);
        Assert.Equal(8.0f, frames[3].AuthorityProjectionPose!.Z);
        Assert.Equal(8000, frames[4].Position);
        Assert.Equal((2, 8375), (frames[5].Step!.Tick, frames[5].Step!.After));
        Assert.Equal((3, 8750), (frames[6].Step!.Tick, frames[6].Step!.After));
        Assert.Equal(8750, frames[7].Point.Prediction);
        Assert.Equal(new long[] { 2 }, frames[7].Point.Pending);
        Assert.Equal(4, frames[8].Point.DeliveryTick);
        Assert.Equal(9015, frames[8].Point.Prediction);
        Assert.True(frames[8].PredictionPose!.X > frames[7].PredictionPose!.X);
        Assert.Equal(new long[] { 2, 3 }, frames[8].Point.Pending);

        var invalid = JObject.Parse(json);
        invalid["checkpoints"]![0]!["generation"] = 2;
        Assert.Throws<InvalidDataException>(() => LocalStateSyncReplay.Parse(invalid.ToString()));
    }

    [Fact]
    public void MovementProofRecoveryHidesPredictionUntilReplayReachesTheLiveTick()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Samples", "ReplaySamples", "moba-local-movement.proof-recovery.statesync-replay.json");
        var replay = LocalStateSyncReplay.Parse(File.ReadAllText(path));
        var timeline = replay.BuildTimeline();
        var frames = timeline.Frames;
        Assert.Equal(9, frames.Count);
        Assert.Equal(new[] { 1, 2, 2, 3, 4, 4, 4, 4, 5 }, frames.Select(frame => frame.Point.DeliveryTick));
        Assert.DoesNotContain(frames, frame => frame.Phase == LocalStateSyncReplay.FramePhase.Compare);
        Assert.Equal(7250, frames[1].Point.Prediction);
        Assert.Equal("InvalidTerminalProof", frames[2].Point.Reconciliation);
        Assert.Equal(0L, frames[2].Point.TerminalSequence);
        Assert.Equal(new long[] { 1 }, frames[2].Point.Pending);
        Assert.Equal(new long[] { 1, 2 }, frames[3].Point.Pending);
        Assert.All(frames.Skip(2).Take(2), frame =>
        {
            Assert.True(frame.Point.PredictionWaiting);
            Assert.Empty(frame.Point.ReplayTicks);
            Assert.Null(frame.Point.Prediction);
            Assert.Null(frame.PredictionPose);
            Assert.Equal(8f, frame.AuthorityProjectionPose!.Z);
        });

        Assert.Equal(LocalStateSyncReplay.FramePhase.Anchor, frames[4].Phase);
        Assert.Equal(8000, frames[4].Position);
        Assert.Equal((3, 8375), (frames[5].Step!.Tick, frames[5].Step!.After));
        Assert.Equal((4, 8750), (frames[6].Step!.Tick, frames[6].Step!.After));
        Assert.Equal(2, frames[7].Point.BaselineTick);
        Assert.Equal(102L, frames[7].Point.PublishedRevision);
        Assert.Equal(2, frames[7].Point.Imports);
        Assert.False(frames[7].Point.PredictionWaiting);
        Assert.Equal(8750, frames[7].Point.Prediction);
        Assert.Equal(1L, frames[7].Point.TerminalSequence);
        Assert.Equal(new long[] { 2 }, frames[7].Point.Pending);
        Assert.Equal(9015, frames[8].Point.Prediction);
        Assert.Equal(new long[] { 2, 3 }, frames[8].Point.Pending);
    }

    [Fact]
    public void MissingSpatialSampleDoesNotReusePreviousPredictionAndInvalidSamplesAreRejected()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Samples", "ReplaySamples", "gragas-e-local-statesync.corrected.statesync-replay.json");
        var data = JObject.Parse(File.ReadAllText(path));
        var step = (JObject)data["checkpoints"]![1]!["replaySteps"]![0]!;
        step["afterPose"]!["z"] = 0;
        Assert.Throws<InvalidDataException>(() => LocalStateSyncReplay.Parse(data.ToString()));

        step.Remove("afterPose");
        var frames = LocalStateSyncReplay.Parse(data.ToString()).BuildTimeline().Frames;
        Assert.Null(frames[3].PredictionPose);
        Assert.NotNull(frames[3].AuthorityProjectionPose);
        Assert.NotNull(frames[4].PredictionPose);
    }

    [Fact]
    public void AnchorDoesNotInventPoseOrCompareOldPredictionAcrossGenerations()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Samples", "ReplaySamples", "gragas-e-local-statesync.corrected.statesync-replay.json");
        var data = JObject.Parse(File.ReadAllText(path));
        var delivery = (JObject)data["checkpoints"]![1]!;
        delivery["generation"] = 2;
        delivery.Remove("authorityPose");
        var timeline = LocalStateSyncReplay.Parse(data.ToString()).BuildTimeline();
        Assert.DoesNotContain(timeline.Frames, frame => frame.Phase == LocalStateSyncReplay.FramePhase.Compare);
        var anchor = timeline.Frames[timeline.Deliveries[1].FirstFrame];
        Assert.Equal(LocalStateSyncReplay.FramePhase.Anchor, anchor.Phase);
        Assert.Equal(-2000, anchor.Position);
        Assert.False(anchor.SharesAnchorPose);
        Assert.Null(anchor.PredictionPose);
        Assert.Null(anchor.AuthorityProjectionPose);
    }
}
