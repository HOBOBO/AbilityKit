using Xunit;
using Host = AbilityKit.Demo.Moba.BattleScenario.Runner.LocalStateSyncMovementHost;
using Recorder = AbilityKit.Demo.Moba.BattleScenario.Runner.LocalStateSyncMovementReplay;

namespace AbilityKit.Demo.Moba.Tests.StateSync;

[Collection("Moba lifecycle preparation")]
[Trait("Gate", "BattleScenario")]
public sealed class MobaLocalStateSyncMovementHostTests
{
    private const float DeltaTime = .05f;
    private static readonly Host.Input Right = new(1, 1, 0);
    private static readonly Host.Input Forward = new(2, 0, 1);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Movement_recording_captures_verified_ticks_and_is_repeatable(bool recovery)
    {
        string Record() => (recovery ? Recorder.RecordProofRecovery() : Recorder.Record()).Serialize();
        Assert.Equal(Record(), Record());
    }

    [Fact]
    public void Same_tick_match_preserves_live_prediction_even_when_snapshot_arrives_late()
    {
        using var host = new Host(DeltaTime);
        using var authority = new Host.MovementWorld("local-statesync-match", DeltaTime);
        authority.Step([Right]);
        var snapshot = new Host.Snapshot(101, 1, authority.Capture(), 1);
        host.Advance(Right);
        host.Advance(Forward);
        var before = host.Current;
        Assert.NotEqual(snapshot.State, before);

        var result = host.Receive(snapshot)!.Value;

        Assert.Equal(Host.Comparison.Match, result.Comparison);
        Assert.False(result.RebasedLiveWorld);
        Assert.Equal(1, host.TerminalSequence);
        Assert.Equal(2, host.StateTick);
        Assert.Equal(before, host.Current);
        Assert.Null(host.Receive(snapshot));
    }

    [Theory]
    [InlineData(64, false)]
    [InlineData(1, true)]
    public void Late_authority_rebases_movement_and_continues_live_prediction(int historyCapacity, bool missingHistory)
    {
        using var host = new Host(DeltaTime, historyCapacity);
        using var authority = new Host.MovementWorld("local-statesync-correction", DeltaTime);
        var rejected = new Host.Input(1, 0, -1);
        authority.Step([]);
        var snapshot = new Host.Snapshot(101, 1, authority.Capture(), 1);
        host.Advance(rejected);
        host.Advance(Forward);
        host.Advance();
        var before = host.Current;
        authority.Step([Forward]);
        var tickTwo = authority.Capture();
        authority.Step([]);
        Assert.True(authority.Capture().Pose.Position.Z > tickTwo.Pose.Position.Z);
        Assert.NotEqual(authority.Capture(), before);

        var result = host.Receive(snapshot)!.Value;

        Assert.Equal(missingHistory ? Host.Comparison.MissingHistory : Host.Comparison.Mismatch, result.Comparison);
        Assert.True(result.RebasedLiveWorld);
        Assert.Equal(2, result.ReplayedTicks);
        Assert.True(host.IsPredicting);
        Assert.Equal(3, host.StateTick);
        Assert.Equal(1, host.TerminalSequence);
        Assert.Equal(authority.Capture(), host.Current);
        var nextInput = new Host.Input(3, -1, 0);
        host.Advance(nextInput);
        authority.Step([nextInput]);
        Assert.Equal(4, host.StateTick);
        Assert.Equal(authority.Capture(), host.Current);

        if (!missingHistory)
        {
            var nextSnapshot = new Host.Snapshot(102, 2, tickTwo, 2);
            var nextResult = host.Receive(nextSnapshot)!.Value;
            Assert.Equal(Host.Comparison.Match, nextResult.Comparison);
            Assert.False(nextResult.RebasedLiveWorld);
            Assert.Equal(authority.Capture(), host.Current);
        }
    }

    [Fact]
    public void Missing_terminal_proof_keeps_authority_until_a_new_valid_snapshot()
    {
        using var host = new Host(DeltaTime);
        using var authority = new Host.MovementWorld("local-statesync-fallback", DeltaTime);
        authority.Step([]);
        var snapshot = new Host.Snapshot(101, 1, authority.Capture(), null);
        host.Advance(Right);
        host.Advance();

        var result = host.Receive(snapshot)!.Value;

        Assert.Equal(0, result.ReplayedTicks);
        Assert.False(host.IsPredicting);
        Assert.Equal(1, host.StateTick);
        Assert.Equal(0, host.TerminalSequence);
        Assert.Equal(snapshot.State, host.Current);
        host.Advance(Forward);
        Assert.Equal(snapshot.State, host.Current);
        authority.Step([]);
        var recovery = new Host.Snapshot(102, 2, authority.Capture(), 1);
        Assert.Equal(1, host.Receive(recovery)!.Value.ReplayedTicks);
        authority.Step([Forward]);
        Assert.Equal(authority.Capture(), host.Current);
        Assert.True(host.IsPredicting);
        Assert.Equal(3, host.StateTick);
    }
}
