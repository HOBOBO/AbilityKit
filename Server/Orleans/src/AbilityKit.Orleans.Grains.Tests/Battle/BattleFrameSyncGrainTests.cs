using AbilityKit.Orleans.Contracts.FrameSync;
using AbilityKit.Orleans.Grains.FrameSync;
using Xunit;

namespace AbilityKit.Orleans.Grains.Tests.Battle;

public sealed class BattleFrameSyncGrainTests
{
    [Theory]
    [InlineData(0ul, 10, 100ul, 10)]
    [InlineData(100ul, 10, 0ul, 10)]
    [InlineData(100ul, 10, 101ul, 10)]
    public void ValidateSubmission_rejects_world_mismatch(
        ulong authoritativeWorldId,
        int serverFrame,
        ulong requestedWorldId,
        int requestedFrame)
    {
        Assert.Equal(
            FrameInputSubmitReason.WorldMismatch,
            BattleFrameSyncGrain.ValidateSubmission(
                authoritativeWorldId,
                serverFrame,
                requestedWorldId,
                requestedFrame));
    }

    [Fact]
    public void ValidateSubmission_rejects_negative_frame()
    {
        Assert.Equal(
            FrameInputSubmitReason.NegativeFrame,
            BattleFrameSyncGrain.ValidateSubmission(100, 10, 100, -1));
    }

    [Fact]
    public void ValidateSubmission_rejects_processed_frame()
    {
        Assert.Equal(
            FrameInputSubmitReason.FrameAlreadyProcessed,
            BattleFrameSyncGrain.ValidateSubmission(100, 10, 100, 9));
    }

    [Fact]
    public void ValidateSubmission_rejects_frame_more_than_120_ahead()
    {
        Assert.Equal(
            FrameInputSubmitReason.FrameTooFarAhead,
            BattleFrameSyncGrain.ValidateSubmission(100, 10, 100, 131));
    }

    [Theory]
    [InlineData(10)]
    [InlineData(130)]
    public void ValidateSubmission_accepts_current_and_maximum_future_frame(int requestedFrame)
    {
        Assert.Equal(
            FrameInputSubmitReason.None,
            BattleFrameSyncGrain.ValidateSubmission(100, 10, 100, requestedFrame));
    }

    [Fact]
    public void TryResolveCatchUpRange_clamps_unbounded_target_to_latest_processed_frame()
    {
        var request = new FrameSyncCatchUpRequest(100, 200, 418, int.MaxValue);

        var accepted = BattleFrameSyncGrain.TryResolveCatchUpRange(
            100,
            200,
            463,
            request,
            out var from,
            out var to);

        Assert.True(accepted);
        Assert.Equal(418, from);
        Assert.Equal(462, to);
    }

    [Fact]
    public void TryResolveCatchUpRange_rejects_when_no_processed_frame_exists_after_source()
    {
        var request = new FrameSyncCatchUpRequest(100, 200, 462, int.MaxValue);

        Assert.False(BattleFrameSyncGrain.TryResolveCatchUpRange(
            100,
            200,
            463,
            request,
            out _,
            out _));
    }

    [Theory]
    [InlineData(101ul, 200ul)]
    [InlineData(100ul, 201ul)]
    [InlineData(0ul, 200ul)]
    [InlineData(100ul, 0ul)]
    public void TryResolveCatchUpRange_rejects_identity_mismatch(ulong roomId, ulong worldId)
    {
        var request = new FrameSyncCatchUpRequest(roomId, worldId, 418, int.MaxValue);

        Assert.False(BattleFrameSyncGrain.TryResolveCatchUpRange(
            100,
            200,
            463,
            request,
            out _,
            out _));
    }

    [Fact]
    public void TryResolveCatchUpRange_rejects_range_beyond_retained_history()
    {
        var request = new FrameSyncCatchUpRequest(100, 200, 398, int.MaxValue);

        Assert.False(BattleFrameSyncGrain.TryResolveCatchUpRange(
            100,
            200,
            1000,
            request,
            out _,
            out _));
    }

    [Fact]
    public void TryResolveCatchUpRange_accepts_cold_start_sentinel_with_complete_recording_capacity()
    {
        var request = new FrameSyncCatchUpRequest(100, 200, -1, int.MaxValue);

        var accepted = BattleFrameSyncGrain.TryResolveCatchUpRange(
            100,
            200,
            1000,
            request,
            1000,
            out var from,
            out var to);

        Assert.True(accepted);
        Assert.Equal(-1, from);
        Assert.Equal(999, to);
    }

    [Fact]
    public void TryResolveCatchUpRange_rejects_cold_start_sentinel_with_short_history_capacity()
    {
        var request = new FrameSyncCatchUpRequest(100, 200, -1, int.MaxValue);

        Assert.False(BattleFrameSyncGrain.TryResolveCatchUpRange(
            100,
            200,
            1000,
            request,
            out _,
            out _));
    }

    [Fact]
    public void TryResolveCatchUpRange_rejects_source_before_cold_start_sentinel()
    {
        var request = new FrameSyncCatchUpRequest(100, 200, -2, int.MaxValue);

        Assert.False(BattleFrameSyncGrain.TryResolveCatchUpRange(
            100,
            200,
            100,
            request,
            100,
            out _,
            out _));
    }
}
