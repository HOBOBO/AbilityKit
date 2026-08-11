using AbilityKit.Game.Flow;
using Xunit;

namespace AbilityKit.Demo.Moba.View.Runtime.Tests;

/// <summary>
/// Contract tests for <see cref="SessionSimRuntimeTuning"/> — the session's sync-mode and input-timing
/// decision logic. Now dotnet-compilable after BattleHostMode/BattleRunMode enums were extracted from
/// the ScriptableObject to pure C# (BattleModes.cs).
/// </summary>
public sealed class SessionSimRuntimeTuningTests
{
    [Theory]
    [InlineData(BattleSyncMode.Lockstep, true)]
    [InlineData(BattleSyncMode.HybridPredictReconcile, true)]
    [InlineData(BattleSyncMode.SnapshotAuthority, false)]
    public void ShouldUseFrameSyncInput_DecidesBySyncMode(BattleSyncMode mode, bool expected)
    {
        Assert.Equal(expected, SessionSimRuntimeTuning.ShouldUseFrameSyncInput(mode));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    [InlineData(5, 5)]
    public void NormalizeInputDelayFrames_ClampsNegativeToZero(int input, int expected)
    {
        Assert.Equal(expected, SessionSimRuntimeTuning.NormalizeInputDelayFrames(input));
    }

    [Fact]
    public void ResolveInputTrimBeforeFrame_RespectsRetainedWindow()
    {
        // RetainedInputFrames = 120. At frame 200, floor = 80.
        // lastConsumedFrame = -1 (< 0) and floor = 80 (≥ 0): the method returns 0 (don't trim past origin).
        Assert.Equal(0, SessionSimRuntimeTuning.ResolveInputTrimBeforeFrame(200, -1));
        // With lastConsumedFrame = 90 (≥ 0): trim = min(80, 91) = 80.
        Assert.Equal(80, SessionSimRuntimeTuning.ResolveInputTrimBeforeFrame(200, 90));
    }

    [Fact]
    public void ResolveInputTrimBeforeFrame_AtEarlyFrames_ReturnsNonPositiveFloor()
    {
        // At frame 50 (below retained window), floor = 50 - 120 = -70 → clamped to -70 (negative OK).
        Assert.Equal(-70, SessionSimRuntimeTuning.ResolveInputTrimBeforeFrame(50, -1));
    }
}
