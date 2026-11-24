using AbilityKit.BattleScenario.Editor;
using AbilityKit.Scenario;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace AbilityKit.World.StateSync.Tests;

public sealed partial class LocalProjectionReferenceTests
{
    private static void VerifyAndExportReplay(TestScenario scenario, List<ScenarioCheckpoint> trace, bool withRecoveryProof)
    {
        var artifact = CreateProjectionReplay(scenario, trace, withRecoveryProof ? "with-proof" : "without-proof");
        var imported = LocalStateSyncReplay.Parse(artifact.Serialize());
        VerifyReplayTimeline(imported, withRecoveryProof);
        LocalReplaySample.VerifyAndExport("fixed-baseline-recovery", artifact);
    }

    private static LocalStateSyncReplay CreateProjectionReplay(TestScenario scenario, List<ScenarioCheckpoint> trace, string variant) => new()
    {
        CaseId = scenario.CaseId,
        Variant = variant,
        TickRate = scenario.ResolveExecution().TickRate,
        Checkpoints = trace.Select(point => new LocalStateSyncReplay.Checkpoint
            {
                Command = point.State.Command,
                DeliveryTick = point.State.DeliveryTick,
                Generation = point.State.Generation,
                Admission = point.State.Admission?.ToString(),
                Validation = point.State.Validation?.ToString(),
                Reconciliation = point.State.Reconciliation?.ToString(),
                Sequence = point.State.Sequence,
                AppliedTick = point.State.AppliedTick,
                BaselineTick = point.State.BaselineTick,
                PublishedRevision = point.State.PublishedRevision,
                ReconciledRevision = point.State.ReconciledRevision,
                TerminalSequence = point.State.TerminalSequence,
                StreamWaiting = point.State.StreamWaiting,
                PredictionWaiting = point.State.PredictionWaiting,
                Imports = point.State.Imports,
                PublishedPosition = point.State.PublishedPosition,
                Authority = point.State.Authority,
                Prediction = point.State.Prediction,
                Pending = point.Pending,
                ReplayTicks = point.ReplayTicks,
                Steps = point.ReplaySteps.Select(step => new LocalStateSyncReplay.ReplayStep
                {
                    Tick = step.Tick,
                    Before = step.Before,
                    After = step.After,
                }).ToArray(),
        }).ToArray(),
    };

    private static void VerifyReplayTimeline(LocalStateSyncReplay artifact, bool withRecoveryProof)
    {
        var timeline = artifact.BuildTimeline();
        var frames = timeline.Frames;
        Assert.Equal(withRecoveryProof ? 16 : 11, frames.Count);
        Assert.Equal(artifact.Checkpoints.Length, timeline.Deliveries.Count);
        for (var index = 0; index < timeline.Deliveries.Count; index++)
        {
            var delivery = timeline.Deliveries[index];
            Assert.Equal(index == 0 ? 0 : timeline.Deliveries[index - 1].CheckpointFrame + 1, delivery.FirstFrame);
            Assert.All(frames.Skip(delivery.FirstFrame).Take(delivery.CheckpointFrame - delivery.FirstFrame + 1),
                frame => Assert.Equal(index, frame.DeliveryIndex));
            Assert.Equal(LocalStateSyncReplay.FramePhase.Checkpoint, frames[delivery.CheckpointFrame].Phase);
        }
        Assert.Equal(artifact.Checkpoints, frames.Where(frame => frame.Phase == LocalStateSyncReplay.FramePhase.Checkpoint)
            .Select(frame => frame.Point));
        Assert.Equal("current-delta", frames[6].Point.Command);
        Assert.Equal("late-delta", frames[7].Point.Command);

        var recovery = frames.Where(frame => frame.Point.Command == "recovery-full").ToArray();
        Assert.All(recovery, frame => Assert.Equal(20, frame.Point.DeliveryTick));
        Assert.Equal(LocalStateSyncReplay.FramePhase.Checkpoint, recovery[^1].Phase);
        if (!withRecoveryProof)
        {
            Assert.Single(recovery);
            Assert.Null(recovery[0].Point.Prediction);
            return;
        }

        Assert.Equal(LocalStateSyncReplay.FramePhase.Anchor, recovery[0].Phase);
        Assert.Equal(130, recovery[0].Position);
        var steps = recovery[1..^1];
        Assert.All(steps, frame => Assert.Equal(LocalStateSyncReplay.FramePhase.ReplayTick, frame.Phase));
        Assert.Equal(new[] { (17, 130), (18, 130), (19, 135), (20, 135) },
            steps.Select(frame => (frame.Step!.Tick, frame.Position!.Value)));
        Assert.Equal(135, recovery[^1].Point.Prediction);
    }

    [Fact]
    public void LocalStateSyncReplay_OlderArtifactKeepsCheckpointTimelineWithoutInventingPositions()
    {
        var json = ValidReplayShape();
        json["version"] = 1;
        foreach (var point in json["checkpoints"]!.OfType<JObject>())
            point.Remove("replaySteps");

        var artifact = LocalStateSyncReplay.Parse(json.ToString());
        var frames = artifact.BuildTimeline().Frames;
        Assert.Equal(2, frames.Count);
        Assert.Equal(new[] { 9, 10 }, frames[1].Point.ReplayTicks);
        Assert.Equal(8, frames[1].Point.Prediction);
        Assert.All(frames, frame =>
        {
            Assert.Equal(LocalStateSyncReplay.FramePhase.Checkpoint, frame.Phase);
            Assert.Null(frame.Position);
        });
    }

    [Fact]
    public void LocalStateSyncReplay_SwitchesVariantsAtTheSameDeliveryDespiteDifferentFrameCounts()
    {
        var recorded = ValidReplayShape();
        var checkpoints = (JArray)recorded["checkpoints"]!;
        checkpoints.Add(checkpoints[checkpoints.Count - 1].DeepClone());
        var before = LocalStateSyncReplay.Parse(recorded.ToString()).BuildTimeline();

        var changed = (JObject)recorded.DeepClone();
        changed["variant"] = "without-proof";
        foreach (var point in changed["checkpoints"]!.OfType<JObject>())
        {
            point["replayTicks"] = new JArray();
            point["replaySteps"] = new JArray();
            point["prediction"] = JValue.CreateNull();
        }
        var after = LocalStateSyncReplay.Parse(changed.ToString()).BuildTimeline();

        Assert.Equal(0, after.FindMatchingCheckpoint(before, 0));
        Assert.Equal(1, after.FindMatchingCheckpoint(before, before.Deliveries[1].FirstFrame + 1));
        Assert.Equal(2, after.FindMatchingCheckpoint(before, before.Deliveries[2].FirstFrame + 1));
        Assert.Equal(before.Deliveries[2].CheckpointFrame, before.FindMatchingCheckpoint(after, 2));
        Assert.Null(after.FindMatchingCheckpoint(before, -1));

        ((JArray)changed["checkpoints"]!).RemoveAt(2);
        var missing = LocalStateSyncReplay.Parse(changed.ToString()).BuildTimeline();
        Assert.Null(missing.FindMatchingCheckpoint(before, before.Deliveries[2].FirstFrame));

        changed["caseId"] = "different-case";
        var unrelated = LocalStateSyncReplay.Parse(changed.ToString()).BuildTimeline();
        Assert.Null(unrelated.FindMatchingCheckpoint(before, 0));
    }

    [Theory]
    [InlineData("version")]
    [InlineData("missing-observation")]
    [InlineData("out-of-order")]
    [InlineData("missing-steps")]
    [InlineData("tick-mismatch")]
    [InlineData("position-discontinuity")]
    [InlineData("prediction-mismatch")]
    public void LocalStateSyncReplay_RejectsIncompatibleArtifact(string revision)
    {
        var json = ValidReplayShape();
        var later = (JObject)json["checkpoints"]![1]!;
        switch (revision)
        {
            case "version":
                json["version"] = 3;
                break;
            case "missing-observation":
                later.Remove("prediction");
                Assert.Throws<JsonSerializationException>(() => LocalStateSyncReplay.Parse(json.ToString()));
                return;
            case "out-of-order":
                later["deliveryTick"] = 9;
                break;
            case "missing-steps":
                later.Remove("replaySteps");
                break;
            case "tick-mismatch":
                later["replaySteps"]![1]!["tick"] = 9;
                break;
            case "position-discontinuity":
                later["replaySteps"]![1]!["before"] = 0;
                break;
            case "prediction-mismatch":
                later["prediction"] = 9;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(revision));
        }
        Assert.Throws<InvalidDataException>(() => LocalStateSyncReplay.Parse(json.ToString()));
    }

    private static JObject ValidReplayShape() => JObject.Parse(new LocalStateSyncReplay
    {
        CaseId = "replay-shape",
        Variant = "local",
        TickRate = 100,
        Checkpoints =
        [
            new() { Command = "initial", DeliveryTick = 10, Steps = [] },
            new()
            {
                Command = "later", DeliveryTick = 10, Authority = 5, Prediction = 8,
                ReplayTicks = [9, 10],
                Steps = [new() { Tick = 9, Before = 5, After = 6 }, new() { Tick = 10, Before = 6, After = 8 }],
            },
        ],
    }.Serialize());
}
