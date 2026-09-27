using AbilityKit.Network.Room;
using AbilityKit.Protocol.Room;
using Xunit;

namespace AbilityKit.Network.Room.Tests;

public sealed class RoomGatewaySessionPrimitivesTests
{
    [Fact]
    public void CommandIdRemainsStableUntilCompletion()
    {
        var ledger = new RoomGatewayCommandIdLedger();
        var first = ledger.GetOrCreate("loading:3");
        Assert.Equal(first, ledger.GetOrCreate("loading:3"));
        Assert.NotEqual(first, ledger.GetOrCreate("loading:4"));
        ledger.Complete("loading:3");
        Assert.NotEqual(first, ledger.GetOrCreate("loading:3"));
    }

    [Fact]
    public void FullSnapshotCursorRejectsDeltaOtherWorldAndOldFrames()
    {
        var cursor = new RoomGatewayFullSnapshotCursor();
        cursor.Reset(7);
        Assert.False(cursor.TryAccept(new WireStateSyncSnapshotPush
            { WorldId = 7, Frame = 1, IsFullSnapshot = false }));
        Assert.False(cursor.TryAccept(new WireStateSyncSnapshotPush
            { WorldId = 8, Frame = 1, IsFullSnapshot = true }));
        Assert.True(cursor.TryAccept(new WireStateSyncSnapshotPush
            { WorldId = 7, Frame = 1, IsFullSnapshot = true }));
        Assert.False(cursor.TryAccept(new WireStateSyncSnapshotPush
            { WorldId = 7, Frame = 1, IsFullSnapshot = true }));
        cursor.Reset(7);
        Assert.True(cursor.TryAccept(new WireStateSyncSnapshotPush
            { WorldId = 7, Frame = 1, IsFullSnapshot = true }));
    }

    [Fact]
    public void FrameInboxOverflowRequiresCoveringSnapshot()
    {
        var inbox = new RoomGatewayFrameInbox(2);
        Assert.True(inbox.Enqueue(new WireRoomFramePush { WorldId = 7, Frame = 10 }));
        Assert.True(inbox.Enqueue(new WireRoomFramePush { WorldId = 7, Frame = 11 }));
        Assert.False(inbox.Enqueue(new WireRoomFramePush { WorldId = 7, Frame = 12 }));
        Assert.True(inbox.TryGetOverflow(out var minimumFrame));
        Assert.Equal(13, minimumFrame);
        Assert.Equal(1, inbox.OverflowCount);
        Assert.False(inbox.TryDequeue(out _));
        Assert.False(inbox.Enqueue(new WireRoomFramePush { WorldId = 7, Frame = 13 }));

        var cursor = new RoomGatewayFullSnapshotCursor();
        cursor.Reset(7);
        cursor.RequireAtLeast(minimumFrame);
        Assert.Equal(-1, cursor.LastFrame);
        Assert.False(cursor.CanAccept(new WireStateSyncSnapshotPush
            { WorldId = 7, Frame = 12, IsFullSnapshot = true }));
        Assert.False(cursor.TryAccept(new WireStateSyncSnapshotPush
            { WorldId = 7, Frame = 12, IsFullSnapshot = true }));
        Assert.True(cursor.TryAccept(new WireStateSyncSnapshotPush
            { WorldId = 7, Frame = 13, IsFullSnapshot = true }));
        Assert.Equal(13, cursor.LastFrame);
        inbox.ResumeAfterSnapshot();
        Assert.False(inbox.TryGetOverflow(out _));
        Assert.True(inbox.Enqueue(new WireRoomFramePush { WorldId = 7, Frame = 13 }));
        Assert.True(inbox.TryDequeue(out var resumed));
        Assert.Equal(13, resumed.Frame);
    }

    [Fact]
    public void RecoveryPauseDoesNotReportOverflow()
    {
        var inbox = new RoomGatewayFrameInbox(1);
        inbox.Enqueue(new WireRoomFramePush { WorldId = 7, Frame = 1 });
        inbox.PauseForRecovery();
        Assert.False(inbox.TryDequeue(out _));
        Assert.False(inbox.TryGetOverflow(out _));
        Assert.False(inbox.Enqueue(new WireRoomFramePush { WorldId = 7, Frame = 2 }));
        inbox.ResumeAfterSnapshot();
        Assert.True(inbox.Enqueue(new WireRoomFramePush { WorldId = 7, Frame = 3 }));
    }
}
