using System.Collections.Generic;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Protocol.Room;
using Xunit;

namespace AbilityKit.Demo.Moba.View.Runtime.Tests;

public sealed class MobaSynchronizationFaultMatrixTests
{
    [Fact]
    public void SnapshotFrameLoss_InvalidatesBaselineUntilReplacementFullSnapshot()
    {
        var admission = new MobaSnapshotAdmission(maxDeltaFrameGap: 5);
        admission.Reset(42UL);

        Assert.True(admission.Admit(42UL, 10, isFullSnapshot: true).Accepted);

        var gap = admission.Admit(42UL, 16, isFullSnapshot: false);
        var blockedDelta = admission.Admit(42UL, 17, isFullSnapshot: false);
        var replacement = admission.Admit(42UL, 18, isFullSnapshot: true);
        var convergedDelta = admission.Admit(42UL, 19, isFullSnapshot: false);

        Assert.Equal(MobaSnapshotAdmissionStatus.FrameGapTooLarge, gap.Status);
        Assert.True(gap.ShouldRequestFullResync);
        Assert.Equal(MobaSnapshotAdmissionStatus.BaselineRequired, blockedDelta.Status);
        Assert.True(blockedDelta.ShouldRequestFullResync);
        Assert.True(replacement.Accepted);
        Assert.True(convergedDelta.Accepted);
        Assert.True(admission.HasBaseline);
        Assert.Equal(19, admission.LastAcceptedFrame);
    }

    [Fact]
    public void ReorderedReliableEvents_RequestResyncWithoutAdvancingCursor()
    {
        var cursor = DeliveredThrough(1);
        var reordered = Push("epoch-1", Event(3), Event(2));

        var result = cursor.Admit(in reordered);

        Assert.Equal(MobaReliableBattleEventBatchStatus.SequenceGap, result.Status);
        Assert.True(result.ShouldRequestFullResync);
        Assert.Equal(2, result.ExpectedSequence);
        Assert.Equal(3, result.ReceivedSequence);
        Assert.Equal(1, cursor.LastDeliveredSequence);
    }

    [Fact]
    public void RetentionGap_NewEpochBaselineAndReplayTail_Converge()
    {
        var cursor = DeliveredThrough(2);
        Assert.True(cursor.ConfirmAcknowledged("epoch-1", 2));
        var retentionGap = Push("epoch-1", Event(5));
        retentionGap.RetentionGap = true;
        retentionGap.FirstAvailableSequence = 5;

        var gap = cursor.Admit(in retentionGap);

        Assert.Equal(MobaReliableBattleEventBatchStatus.RetentionGap, gap.Status);
        Assert.True(gap.ShouldRequestFullResync);
        Assert.Equal(2, cursor.LastDeliveredSequence);

        Assert.True(cursor.AdoptAuthoritativeBaseline("epoch-2", 8));
        Assert.Equal(0, cursor.LastAcknowledgedSequence);

        var replay = Push(
            "epoch-2",
            Event(7, "epoch-2"),
            Event(8, "epoch-2"),
            Event(9, "epoch-2"),
            Event(10, "epoch-2"));
        var tail = cursor.Admit(in replay);

        Assert.Equal(MobaReliableBattleEventBatchStatus.Accepted, tail.Status);
        Assert.Collection(
            tail.Events,
            item => Assert.Equal(9, item.Sequence),
            item => Assert.Equal(10, item.Sequence));
        Assert.True(cursor.CommitDelivered(tail.Epoch, tail.CommitSequence));
        Assert.True(cursor.ConfirmAcknowledged("epoch-2", 10));
        Assert.Equal(10, cursor.LastDeliveredSequence);
        Assert.Equal(10, cursor.LastAcknowledgedSequence);
    }

    [Fact]
    public void EpochChange_IsRejectedUntilAuthoritativeBaselineAdoptsNewTimeline()
    {
        var cursor = DeliveredThrough(3);
        Assert.True(cursor.ConfirmAcknowledged("epoch-1", 3));
        var newEpochPush = Push("epoch-2", Event(1, "epoch-2"));

        var rejected = cursor.Admit(in newEpochPush);

        Assert.Equal(MobaReliableBattleEventBatchStatus.EpochChanged, rejected.Status);
        Assert.Equal("epoch-1", cursor.Epoch);
        Assert.Equal(3, cursor.LastAcknowledgedSequence);

        Assert.True(cursor.AdoptAuthoritativeBaseline("epoch-2", 5));
        var resumed = Push("epoch-2", Event(6, "epoch-2"));
        var accepted = cursor.Admit(in resumed);

        Assert.Equal(MobaReliableBattleEventBatchStatus.Accepted, accepted.Status);
        Assert.Single(accepted.Events);
        Assert.Equal(6, accepted.CommitSequence);
    }

    [Fact]
    public void OutOfOrderAcknowledgements_DoNotMoveConfirmedCursorBackward()
    {
        var cursor = DeliveredThrough(3);

        Assert.True(cursor.ConfirmAcknowledged("epoch-1", 3));
        Assert.True(cursor.ConfirmAcknowledged("epoch-1", 1));

        Assert.Equal(3, cursor.LastAcknowledgedSequence);
    }

    [Fact]
    public void Checkpoint_RestoresAcknowledgedCursorWithoutSharingRuntimeObject()
    {
        var original = DeliveredThrough(3);
        Assert.True(original.ConfirmAcknowledged("epoch-1", 2));
        var checkpoint = original.CreateCheckpoint();
        var restored = new MobaReliableBattleEventCursor("battle-1");

        Assert.True(restored.TryRestore(in checkpoint));
        Assert.Equal("epoch-1", restored.Epoch);
        Assert.Equal(2, restored.LastDeliveredSequence);
        Assert.Equal(2, restored.LastAcknowledgedSequence);

        var replay = Push("epoch-1", Event(2), Event(3));
        var result = restored.Admit(in replay);
        Assert.True(result.Accepted);
        Assert.Single(result.Events);
        Assert.Equal(3, result.Events[0].Sequence);
    }

    [Fact]
    public void Checkpoint_FromDifferentBattle_IsRejected()
    {
        var cursor = new MobaReliableBattleEventCursor("battle-2");
        var checkpoint = new MobaReliableBattleEventCheckpoint(
            "battle-1",
            "epoch-1",
            4);

        Assert.False(cursor.TryRestore(in checkpoint));
        Assert.Equal(0, cursor.LastAcknowledgedSequence);
    }

    private static MobaReliableBattleEventCursor DeliveredThrough(long sequence)
    {
        var cursor = new MobaReliableBattleEventCursor("battle-1");
        var events = new WireReliableBattleEvent[checked((int)sequence)];
        for (var i = 0; i < events.Length; i++)
        {
            events[i] = Event(i + 1);
        }

        var push = Push("epoch-1", events);
        var admitted = cursor.Admit(in push);
        Assert.True(admitted.Accepted);
        Assert.True(cursor.CommitDelivered(admitted.Epoch, admitted.CommitSequence));
        return cursor;
    }

    private static WireReliableBattleEventPush Push(
        string epoch,
        params WireReliableBattleEvent[] events)
    {
        return new WireReliableBattleEventPush
        {
            BattleId = "battle-1",
            Epoch = epoch,
            FirstAvailableSequence = events.Length > 0 ? events[0].Sequence : 0,
            Watermark = events.Length > 0 ? events[^1].Sequence : 0,
            Events = new List<WireReliableBattleEvent>(events)
        };
    }

    private static WireReliableBattleEvent Event(
        long sequence,
        string epoch = "epoch-1")
    {
        return new WireReliableBattleEvent
        {
            EventId = $"event-{sequence}",
            BattleId = "battle-1",
            Epoch = epoch,
            Sequence = sequence,
            SourceFrame = checked((int)sequence),
            EventType = 1
        };
    }
}
