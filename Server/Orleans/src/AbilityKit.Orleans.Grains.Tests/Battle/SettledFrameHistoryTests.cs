using AbilityKit.Orleans.Contracts.FrameSync;
using AbilityKit.Orleans.Grains.FrameSync;
using Xunit;

namespace AbilityKit.Orleans.Grains.Tests.Battle;

public sealed class SettledFrameHistoryTests
{
    private static readonly Guid Generation = Guid.NewGuid();

    [Fact]
    public void Commit_returns_the_same_immutable_record_used_by_both_histories()
    {
        var history = CreateHistory();
        var settlement = new BattleFrameSettlement(Generation, 1,
            [new(0, SettledCommandDisposition.Executed, 1, 3020, [8, 9]),
             new(1, SettledCommandDisposition.Duplicate, 1, 3020, [])],
            [new(0, 1, 1, 42, 1, true, 1, 0, 2, 5),
             new(1, 1, 1, 42, 1, true, 1, 0, 2, 5),
             new(2, 1, 1, 0, 0, false, 1, 0, 3, 6)]);

        var live = history.Commit(1, settlement, 2);
        Assert.NotNull(live);
        Assert.Same(live, Assert.Single(history.CatchUp(Request(0, 1))!.Frames));
        Assert.Same(live, history.DumpRecording()!.Frames[1]);
        var cold = history.CatchUp(Request(-1, 1))!;
        Assert.True(cold.Frames[0].Settlement.IsInitialCheckpoint);
        Assert.Same(live, cold.Frames[1]);
    }

    [Fact]
    public void Fault_preserves_only_the_contiguous_committed_prefix()
    {
        var history = CreateHistory();
        Assert.NotNull(history.Commit(1, Empty(1), 0));
        Assert.Null(history.Commit(2, null, 0));
        Assert.Null(history.Commit(2, Empty(2), 0));
        Assert.Null(history.Commit(3, Empty(3), 0));
        Assert.Equal(2, history.FaultFrame);
        Assert.Equal(1, history.LastSettledFrame);
        Assert.Null(history.CatchUp(Request(1, 2)));
        Assert.Null(history.CatchUp(Request(-1, int.MaxValue)));
        Assert.Equal(2, history.CatchUp(Request(-1, 1))!.Frames.Length);
        Assert.Equal(new[] { 0, 1 }, history.DumpRecording()!.Frames.Select(frame => frame.Settlement.Frame));
    }

    [Theory]
    [InlineData("generation")]
    [InlineData("jump")]
    [InlineData("count")]
    [InlineData("ordinal")]
    [InlineData("nonexecuted-payload")]
    public void Invalid_settlement_cannot_advance_the_prefix(string scenario)
    {
        var history = CreateHistory();
        var frame = scenario == "jump" ? 2 : 1;
        var settlement = scenario switch
        {
            "generation" => Empty(frame) with { RuntimeGeneration = Guid.NewGuid() },
            "ordinal" => Empty(frame) with { Commands = [new(1, SettledCommandDisposition.Executed, 1, 1, [1])] },
            "nonexecuted-payload" => Empty(frame) with { Commands = [new(0, SettledCommandDisposition.Rejected, 1, 1, [1])] },
            _ => Empty(frame)
        };
        var submittedCount = scenario is "count" or "ordinal" or "nonexecuted-payload" ? 1 : 0;
        Assert.Null(history.Commit(frame, settlement, submittedCount));
        Assert.Equal(0, history.LastSettledFrame);
        Assert.Equal(1, history.FaultFrame);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Missing_or_truncated_cold_recording_never_falls_back_to_recent_frames(bool recording)
    {
        var history = CreateHistory(recording);
        for (var frame = 1; frame <= 4; frame++) Assert.NotNull(history.Commit(frame, Empty(frame), 0));
        Assert.Null(history.CatchUp(Request(-1, 4)));
        Assert.Null(history.CatchUp(Request(0, 4)));
        Assert.Null(history.CatchUp(Request(2, 4) with { RuntimeGeneration = Guid.NewGuid() }));
        Assert.Equal(new[] { 3, 4 }, history.CatchUp(Request(2, int.MaxValue))!.Frames.Select(frame => frame.Settlement.Frame));
        Assert.Equal(recording ? 3 : 0, history.RecordingCount);
    }

    [Fact]
    public void Cold_recording_pages_continue_after_frame_zero_outside_short_history()
    {
        var history = CreateHistory();
        for (var frame = 1; frame <= 4; frame++) Assert.NotNull(history.Commit(frame, Empty(frame), 0));

        var checkpoint = Assert.Single(history.CatchUp(Request(-1, 0))!.Frames);
        Assert.True(checkpoint.Settlement.IsInitialCheckpoint);
        var recording = history.DumpRecording()!;
        var nextPage = history.CatchUp(Request(0, 2))!.Frames;
        Assert.Same(recording.Frames[1], nextPage[0]);
        Assert.Same(recording.Frames[2], nextPage[1]);
        Assert.Null(history.CatchUp(Request(1, 3)));
    }

    private static SettledFrameHistory CreateHistory(bool recording = true) =>
        new(new FrameSyncStartOptions(10, 20, 30, "settled-history", "test", 1, EnableRecording: recording)
        { UseSettledFrames = true, InitialCheckpoint = new FrameSyncInitialCheckpoint(Generation) }, 2, 3);

    private static BattleFrameSettlement Empty(int frame) => new(Generation, frame, [], []);

    private static FrameSyncSettledCatchUpRequest Request(int from, int to) => new(10, 20, Generation, from, to);
}
