using AbilityKit.Demo.Tiny.View;
using AbilityKit.Network.Room;
using AbilityKit.Protocol.Room;

namespace AbilityKit.Demo.Tiny.Client;

public static class TinyRecoveryEvidence
{
    public static string RunOverflow()
    {
        const ulong worldId = 7;
        var inbox = new RoomGatewayFrameInbox(2);
        var cursor = new RoomGatewayFullSnapshotCursor();
        cursor.Reset(worldId);
        Require(inbox.Enqueue(new WireRoomFramePush { WorldId = worldId, Frame = 10 }), "First frame was rejected.");
        Require(inbox.Enqueue(new WireRoomFramePush { WorldId = worldId, Frame = 11 }), "Second frame was rejected.");
        Require(!inbox.Enqueue(new WireRoomFramePush { WorldId = worldId, Frame = 12 }), "Overflow was not triggered.");
        Require(inbox.TryGetOverflow(out var minimumFrame) && minimumFrame == 13,
            "Overflow did not report the covering snapshot frame.");
        cursor.RequireAtLeast(minimumFrame);
        Require(!inbox.TryDequeue(out _) && !inbox.Enqueue(new WireRoomFramePush { WorldId = worldId, Frame = 13 }),
            "Frame stream continued during recovery.");
        var wrongWorld = new WireStateSyncSnapshotPush { WorldId = 8, Frame = 13, IsFullSnapshot = true };
        var stale = new WireStateSyncSnapshotPush { WorldId = worldId, Frame = 12, IsFullSnapshot = true };
        var incomplete = new WireStateSyncSnapshotPush { WorldId = worldId, Frame = 13, IsFullSnapshot = false };
        Require(!cursor.TryAccept(in wrongWorld) && !cursor.TryAccept(in stale) &&
            !cursor.TryAccept(in incomplete) && cursor.LastFrame == -1,
            "Invalid snapshot advanced the recovery cursor.");
        var covering = new WireStateSyncSnapshotPush { WorldId = worldId, Frame = 13, IsFullSnapshot = true };
        Require(cursor.TryAccept(in covering), "Covering snapshot was rejected.");
        inbox.ResumeAfterSnapshot();
        Require(!inbox.TryGetOverflow(out _) &&
            inbox.Enqueue(new WireRoomFramePush { WorldId = worldId, Frame = 13 }) &&
            inbox.TryDequeue(out var resumed) && resumed.Frame == 13,
            "Frame stream did not resume after the snapshot.");
        return $"06 overflow passed: capacity=2, droppedFrame=12, minimumSnapshotFrame={minimumFrame}, " +
            $"rejected=world/stale/incomplete, restoredFrame={cursor.LastFrame}, overflowCount={inbox.OverflowCount}";
    }

    public static string RunSnapshotMismatch()
    {
        const ulong worldId = 7;
        var replication = new TinyFrameReplication();
        var authority = NewBattle();
        var initial = Snapshot(authority, worldId);
        replication.ApplyFullSnapshot(in initial);
        var localInput = new TinyInput(1, 0, false);
        replication.PredictLocalInput(replication.ReserveInputFrame(), 1, localInput);
        var preCorrectionFrame = replication.PredictedFrame;
        var preCorrectionHash = replication.StateHash;
        authority.Submit(2, new TinyInput(0, 0, true));
        authority.Tick();
        var corrected = Snapshot(authority, worldId);
        replication.ApplyFullSnapshot(in corrected);
        Require(replication.SnapshotCorrectionCount == 1 && !replication.NeedsFullSnapshot &&
            replication.StateHash == authority.ComputeHash(),
            "Mismatched periodic snapshot did not correct the prediction.");
        var restoredHash = replication.StateHash;
        replication.PredictLocalInput(replication.ReserveInputFrame(), 1, localInput);
        Require(!replication.NeedsFullSnapshot && replication.PredictedFrame > corrected.Frame,
            "Prediction did not resume after correction.");
        return $"06 snapshot-mismatch passed: snapshotFrame={corrected.Frame}, " +
            $"preCorrectionFrame={preCorrectionFrame}, preCorrectionHash={preCorrectionHash}, " +
            $"authoritativeHash={restoredHash}, " +
            $"corrections={replication.SnapshotCorrectionCount}, resumedFrame={replication.PredictedFrame}";
    }

    private static WireStateSyncSnapshotPush Snapshot(TinyBattle battle, ulong worldId) => new()
    {
        WorldId = worldId,
        Frame = battle.Frame,
        IsFullSnapshot = true,
        PayloadOpCode = TinyBattleStateCodec.PayloadOpCode,
        Payload = TinyBattleStateCodec.Encode(battle.CaptureState())
    };

    private static TinyBattle NewBattle()
    {
        var battle = new TinyBattle();
        battle.AddPlayer(1, -1, 0);
        battle.AddPlayer(2, 1, 0);
        return battle;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
