using System;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Network.Room;
using Xunit;

namespace AbilityKit.Network.Room.Tests;

/// <summary>
/// Pins the staged-restore contract of <see cref="RoomGatewaySessionFlow.RestoreAsync"/>:
/// timeout/failure handling + phase/next-step resolution (the newest, otherwise-untested WIP logic).
/// Uses a fake <see cref="IRoomGatewaySessionClientBase"/> so no real network is needed.
/// </summary>
public sealed class RoomGatewaySessionFlowRestoreTests
{
    private const string Token = "session-token";
    private const uint PlayerId = 1u;

    [Fact]
    public async Task RestoreAsync_RestoreRequestTimeout_ReturnsTimeoutResultWithNoNextStep()
    {
        var flow = new RoomGatewaySessionFlow(new FakeRoomClient { RestoreResult = null });

        var result = await flow.RestoreAsync(Token, "region", "server", PlayerId);

        Assert.Equal(RoomGatewayStagedRestoreNextStep.None, result.NextStep);
        Assert.Equal(RoomGatewaySessionPhase.Closed, result.Phase);
        Assert.Equal(RoomGatewaySessionRestoreStatus.Timeout, result.RestoreStatus);
    }

    [Fact]
    public async Task RestoreAsync_RestoreFailure_PreservesStatusAndErrorCodeWithoutNextStep()
    {
        var flow = new RoomGatewaySessionFlow(new FakeRoomClient
        {
            RestoreResult = Restore(
                success: false,
                isInBattle: false,
                status: RoomGatewaySessionRestoreStatus.NoActiveRoom,
                errorCode: RoomGatewaySessionRestoreErrorCode.NoAccountRoomMapping),
        });

        var result = await flow.RestoreAsync(Token, "region", "server", PlayerId);

        Assert.Equal(RoomGatewayStagedRestoreNextStep.None, result.NextStep);
        Assert.Equal(RoomGatewaySessionPhase.Closed, result.Phase);
        Assert.Equal(RoomGatewaySessionRestoreStatus.NoActiveRoom, result.RestoreStatus);
        Assert.Equal(RoomGatewaySessionRestoreErrorCode.NoAccountRoomMapping, result.RestoreErrorCode);
    }

    [Fact]
    public async Task RestoreAsync_ActiveBattle_NextStepIsSubscribeStateSync()
    {
        var flow = new RoomGatewaySessionFlow(new FakeRoomClient
        {
            // GetSnapshot timing out forces the CreateRestoreSnapshot(restored) path, avoiding merge.
            RestoreResult = Restore(success: true, isInBattle: true, battleId: "battle-1"),
            GetSnapshotTimesOut = true,
        });

        var result = await flow.RestoreAsync(Token, "region", "server", PlayerId);

        Assert.Equal(RoomGatewaySessionPhase.InBattle, result.Phase);
        Assert.Equal(RoomGatewayStagedRestoreNextStep.SubscribeStateSync, result.NextStep);
    }

    [Fact]
    public async Task RestoreAsync_ActiveLobby_NextStepIsSetReadyAndBeginLoading()
    {
        var flow = new RoomGatewaySessionFlow(new FakeRoomClient
        {
            RestoreResult = Restore(success: true, isInBattle: false),
            GetSnapshotTimesOut = true,
        });

        var result = await flow.RestoreAsync(Token, "region", "server", PlayerId);

        Assert.Equal(RoomGatewaySessionPhase.Lobby, result.Phase);
        Assert.Equal(RoomGatewayStagedRestoreNextStep.SetReadyAndBeginLoading, result.NextStep);
    }

    private static RoomGatewayRestoreRoomResult Restore(
        bool success,
        bool isInBattle = false,
        string battleId = "",
        RoomGatewaySessionRestoreStatus status = RoomGatewaySessionRestoreStatus.Restored,
        RoomGatewaySessionRestoreErrorCode errorCode = RoomGatewaySessionRestoreErrorCode.None)
        => new RoomGatewayRestoreRoomResult(
            success,
            hasActiveRoom: success,
            isInBattle,
            roomId: success ? "room-1" : string.Empty,
            numericRoomId: 1ul,
            worldStartAnchor: new RoomGatewayWorldStartAnchor(0L, 30L, 0, 1.0 / 30.0),
            message: string.Empty,
            battleId: battleId,
            canStart: false,
            joinKind: RoomGatewaySessionEntryKind.Reconnect,
            serverNowTicks: 0L,
            worldId: 1ul,
            status: status,
            errorCode: errorCode);

    private sealed class FakeRoomClient : IRoomGatewaySessionClientBase
    {
        public RoomGatewayRestoreRoomResult? RestoreResult;
        public bool GetSnapshotTimesOut = true;

        public Task<RoomGatewayCreateResult> CreateRoomAsync(RoomGatewayCreateRequest request, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task<RoomGatewayJoinResult> JoinRoomAsync(RoomGatewayJoinRequest request, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task<RoomGatewayLeaveResult> LeaveRoomAsync(RoomGatewayLeaveRequest request, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task<RoomGatewayReadyResult> SetReadyAsync(RoomGatewayReadyRequest request, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<RoomGatewayRestoreRoomResult> RestoreRoomAsync(RoomGatewayRestoreRoomRequest request, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
            => RestoreResult.HasValue
                ? Task.FromResult(RestoreResult.Value)
                : throw new TimeoutException("restore timeout");

        public Task<RoomGatewayGetSnapshotResult> GetSnapshotAsync(RoomGatewayGetSnapshotRequest request, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
            => GetSnapshotTimesOut
                ? throw new TimeoutException("snapshot timeout")
                : Task.FromResult(new RoomGatewayGetSnapshotResult(true, "room-1", 1ul, null, "ok"));
    }
}
