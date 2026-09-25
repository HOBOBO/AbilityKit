using AbilityKit.Demo.Tiny.View;
using AbilityKit.Protocol.Room;
using Xunit;

namespace AbilityKit.Demo.Tiny.Replication.Tests;

public sealed class TinyFrameReplicationTests
{
    [Fact]
    public void HybridPredictsLocalInputThenConfirmsHistoricalFrames()
    {
        var replication = new TinyFrameReplication();
        var baseline = Baseline();
        replication.ApplyFullSnapshot(in baseline);
        var input = new TinyInput(1, 0, false);
        var inputFrame = replication.ReserveInputFrame();
        replication.PredictLocalInput(inputFrame, 1, input);

        Assert.True(replication.TryGetPresentation(7, out var predicted));
        Assert.Equal(inputFrame + 1, predicted.Frame);
        Assert.Equal(0, predicted.Actors!.Single(actor => actor.ActorId == 1).X);
        Assert.Equal(1, replication.LocalPredictionCount);
        Assert.Equal(inputFrame + 1, replication.PredictedFrame);

        var authority = NewBattle();
        for (var frameIndex = 0; frameIndex < inputFrame; frameIndex++)
        {
            authority.Tick();
            var previous = Frame(frameIndex, authority.ComputeHash());
            replication.ApplyFrame(in previous, 7);
        }
        authority.Submit(1, input);
        authority.Tick();
        var confirmed = Frame(inputFrame, authority.ComputeHash(), 1, input);
        replication.ApplyFrame(in confirmed, 7);

        Assert.False(replication.NeedsFullSnapshot);
        Assert.True(replication.TryGetPresentation(7, out var final));
        Assert.Equal(0, final.Actors!.Single(actor => actor.ActorId == 1).X);
        Assert.True(replication.ReserveInputFrame() > inputFrame);
    }

    [Fact]
    public void AuthorityReplaysRemoteInputAcrossLocalPrediction()
    {
        var replication = new TinyFrameReplication();
        var baseline = Baseline();
        replication.ApplyFullSnapshot(in baseline);
        var localInput = new TinyInput(1, 0, false);
        var inputFrame = replication.ReserveInputFrame();
        replication.PredictLocalInput(inputFrame, 1, localInput);

        var authority = NewBattle();
        var remoteInput = new TinyInput(0, 0, true);
        authority.Submit(2, remoteInput);
        authority.Tick();
        var previous = Frame(0, authority.ComputeHash(), 2, remoteInput);
        replication.ApplyFrame(in previous, 7);
        for (var frameIndex = 1; frameIndex < inputFrame; frameIndex++)
        {
            authority.Tick();
            var empty = Frame(frameIndex, authority.ComputeHash());
            replication.ApplyFrame(in empty, 7);
        }
        authority.Submit(1, localInput);
        authority.Tick();
        var confirmed = Frame(inputFrame, authority.ComputeHash(), 1, localInput);
        replication.ApplyFrame(in confirmed, 7);

        Assert.False(replication.NeedsFullSnapshot);
        Assert.True(replication.TryGetPresentation(7, out var final));
        Assert.Equal(90, final.Actors!.Single(actor => actor.ActorId == 1).Hp);
        Assert.Equal(0, final.Actors.Single(actor => actor.ActorId == 1).X);
        Assert.True(replication.RollbackCount > 0);
    }

    [Fact]
    public void MatchingPeriodicSnapshotPreservesPredictedFuture()
    {
        var replication = new TinyFrameReplication();
        var baseline = Baseline();
        replication.ApplyFullSnapshot(in baseline);
        var inputFrame = replication.ReserveInputFrame();
        replication.PredictLocalInput(inputFrame, 1, new TinyInput(1, 0, false));
        var authority = NewBattle();
        authority.Tick();
        var periodic = Snapshot(authority);
        replication.ApplyFullSnapshot(in periodic);

        Assert.True(replication.TryGetPresentation(7, out var predicted));
        Assert.Equal(inputFrame + 1, predicted.Frame);
        Assert.Equal(0, predicted.Actors!.Single(actor => actor.ActorId == 1).X);
    }

    [Fact]
    public void MismatchedPeriodicSnapshotCorrectsPredictedState()
    {
        var replication = new TinyFrameReplication();
        var baseline = Baseline();
        replication.ApplyFullSnapshot(in baseline);
        var inputFrame = replication.ReserveInputFrame();
        replication.PredictLocalInput(inputFrame, 1, new TinyInput(1, 0, false));
        var authority = NewBattle();
        authority.Submit(2, new TinyInput(0, 0, true));
        authority.Tick();
        var periodic = Snapshot(authority);
        replication.ApplyFullSnapshot(in periodic);

        Assert.False(replication.NeedsFullSnapshot);
        Assert.True(replication.TryGetPresentation(7, out var corrected));
        Assert.Equal(1, corrected.Frame);
        Assert.Equal(90, corrected.Actors!.Single(actor => actor.ActorId == 1).Hp);
        Assert.Equal(1, replication.SnapshotCorrectionCount);
    }

    private static WireStateSyncSnapshotPush Baseline() => Snapshot(NewBattle());

    private static WireStateSyncSnapshotPush Snapshot(TinyBattle battle) => new()
    {
        WorldId = 7,
        Frame = battle.Frame,
        IsFullSnapshot = true,
        PayloadOpCode = TinyBattleStateCodec.PayloadOpCode,
        Payload = TinyBattleStateCodec.Encode(battle.CaptureState())
    };

    private static WireRoomFramePush Frame(int frame, uint hash, uint playerId = 0, TinyInput input = default) => new()
    {
        WorldId = 7,
        Frame = frame,
        StateHash = hash,
        Inputs = playerId == 0 ? Array.Empty<WireRoomFrameInput>() :
            new[] { new WireRoomFrameInput { PlayerId = playerId, InputOpCode = TinyBattle.InputOpCode, Payload = input.Encode() } }
    };

    private static TinyBattle NewBattle()
    {
        var battle = new TinyBattle();
        battle.AddPlayer(1, -1, 0);
        battle.AddPlayer(2, 1, 0);
        return battle;
    }
}
