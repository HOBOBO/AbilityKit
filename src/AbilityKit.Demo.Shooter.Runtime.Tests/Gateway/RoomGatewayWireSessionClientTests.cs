using System.Collections.Generic;
using System.Threading.Tasks;
using AbilityKit.Network.Room;
using AbilityKit.Protocol;
using AbilityKit.Protocol.Room;
using Xunit;

namespace AbilityKit.Demo.Shooter.Runtime.Tests;

public sealed class RoomGatewayWireSessionClientTests
{
    [Fact]
    public void DefaultOpCodesResolveFromProtocolMessageMetadata()
    {
        var opCodes = RoomGatewayWireOpCodes.Default;

        Assert.Equal(ProtocolMessageDescriptor<WireCreateRoomReq>.OpCode, opCodes.CreateRoom);
        Assert.Equal(ProtocolMessageDescriptor<WireJoinRoomReq>.OpCode, opCodes.JoinRoom);
        Assert.Equal(ProtocolMessageDescriptor<WireLeaveRoomReq>.OpCode, opCodes.LeaveRoom);
        Assert.Equal(ProtocolMessageDescriptor<WireRoomReadyReq>.OpCode, opCodes.SetReady);
        Assert.Equal(ProtocolMessageDescriptor<WireStartRoomBattleReq>.OpCode, opCodes.StartBattle);
        Assert.Equal(ProtocolMessageDescriptor<WireSubscribeStateSyncReq>.OpCode, opCodes.SubscribeStateSync);
        Assert.Equal(ProtocolMessageDescriptor<WireRestoreRoomReq>.OpCode, opCodes.RestoreRoom);
        Assert.Equal(ProtocolMessageDescriptor<WireRoomPickHeroReq>.OpCode, opCodes.PickHero);
        Assert.Equal(ProtocolMessageDescriptor<WireBeginLoadingReq>.OpCode, opCodes.BeginLoading);
        Assert.Equal(ProtocolMessageDescriptor<WireReportLoadingProgressReq>.OpCode, opCodes.ReportLoadingProgress);
        Assert.Equal(ProtocolMessageDescriptor<WireReportAssetsLoadedReq>.OpCode, opCodes.ReportAssetsLoaded);
        Assert.Equal(ProtocolMessageDescriptor<WireCancelLoadingReq>.OpCode, opCodes.CancelLoading);
        Assert.Equal(ProtocolMessageDescriptor<WireGetSnapshotReq>.OpCode, opCodes.GetSnapshot);
        Assert.Equal(ProtocolMessageDescriptor<WireRoomStateChangedPush>.OpCode, opCodes.RoomStateChanged);
    }

    [Fact]
    public void ExplicitOpCodesRemainAvailableForAlternativeBackends()
    {
        var opCodes = new RoomGatewayWireOpCodes(
            1U, 2U, 3U, 4U, 5U, 6U, 7U,
            8U, 9U, 10U, 11U, 12U, 13U, 14U);

        Assert.Equal(1U, opCodes.CreateRoom);
        Assert.Equal(2U, opCodes.JoinRoom);
        Assert.Equal(3U, opCodes.LeaveRoom);
        Assert.Equal(4U, opCodes.SetReady);
        Assert.Equal(5U, opCodes.StartBattle);
        Assert.Equal(6U, opCodes.SubscribeStateSync);
        Assert.Equal(7U, opCodes.RestoreRoom);
        Assert.Equal(8U, opCodes.PickHero);
        Assert.Equal(9U, opCodes.BeginLoading);
        Assert.Equal(10U, opCodes.ReportLoadingProgress);
        Assert.Equal(11U, opCodes.ReportAssetsLoaded);
        Assert.Equal(12U, opCodes.CancelLoading);
        Assert.Equal(13U, opCodes.GetSnapshot);
        Assert.Equal(14U, opCodes.RoomStateChanged);
    }

    [Fact]
    public async Task CreateRoomUsesSharedOpcodeAndWirePayload()
    {
        var connection = new FakeGatewayConnection();
        using var client = new RoomGatewayWireSessionClient(connection);

        var task = client.CreateRoomAsync(new RoomGatewayCreateRequest(
            "session-1",
            "cn",
            "server-a",
            "shooter",
            "Shared Room",
            true,
            8,
            new Dictionary<string, string> { ["mode"] = "ranked" }));

        Assert.Equal(RoomGatewayOpCodes.CreateRoom, connection.LastSentOpCode);
        var request = WireRoomGatewayBinary.Deserialize<WireCreateRoomReq>(connection.LastSentPayload);
        Assert.Equal("session-1", request.SessionToken);
        Assert.Equal("cn", request.Region);
        Assert.Equal("server-a", request.ServerId);
        Assert.Equal("shooter", request.RoomType);
        Assert.Equal("Shared Room", request.Title);
        Assert.True(request.IsPublic);
        Assert.Equal(8, request.MaxPlayers);
        Assert.Equal("ranked", request.Tags!["mode"]);

        var response = new WireCreateRoomRes
        {
            Success = true,
            RoomId = "room-1",
            NumericRoomId = 1001ul,
            Message = "created"
        };
        connection.CompleteResponse(connection.LastSentOpCode, connection.LastSentSeq, in response);

        var result = await task;
        Assert.True(result.Success);
        Assert.Equal("room-1", result.RoomId);
        Assert.Equal(1001ul, result.NumericRoomId);
        Assert.Equal("created", result.Message);
    }

    [Fact]
    public async Task GetSnapshotProjectsWireStateAndPublishesFeed()
    {
        var connection = new FakeGatewayConnection();
        using var client = new RoomGatewayWireSessionClient(connection);
        RoomGatewaySnapshot? published = null;
        client.SnapshotChanged += snapshot => published = snapshot;

        var task = client.GetSnapshotAsync(new RoomGatewayGetSnapshotRequest("session-1", "room-1"));
        var response = new WireRoomSnapshotRes
        {
            Success = true,
            RoomId = "room-1",
            NumericRoomId = 1001ul,
            ServerNowTicks = 555L,
            Message = "ok",
            Snapshot = CreateSnapshot(7L, RoomGatewaySessionPhase.Loading)
        };
        connection.CompleteResponse(connection.LastSentOpCode, connection.LastSentSeq, in response);

        var result = await task;
        Assert.Equal(RoomGatewayOpCodes.GetSnapshot, connection.LastSentOpCode);
        Assert.True(result.Success);
        Assert.Equal(555L, result.ServerNowTicks);
        Assert.NotNull(result.Snapshot);
        Assert.Same(result.Snapshot, client.Current);
        Assert.Same(result.Snapshot, published);
        Assert.Equal("owner-1", result.Snapshot!.OwnerAccountId);
        Assert.Equal(RoomGatewaySessionPhase.Loading, result.Snapshot.Phase);
        Assert.Equal(7L, result.Snapshot.RoomRevision);
        Assert.Equal("battle-1", result.Snapshot.BattleId);
        Assert.Equal(9001ul, result.Snapshot.WorldId);
        Assert.Equal(new[] { "owner-1" }, result.Snapshot.Members);
        var player = Assert.Single(result.Snapshot.Players);
        Assert.Equal(42u, player.PlayerId);
        Assert.Equal(80, player.LoadingProgress);
        Assert.Equal(new[] { 100, 101 }, player.SkillIds);
        Assert.True(result.Snapshot.WorldStartAnchor.IsValid);
    }

    [Fact]
    public async Task RestoreMapsWireFailureSentinelsExplicitly()
    {
        var connection = new FakeGatewayConnection();
        using var client = new RoomGatewayWireSessionClient(connection);

        var task = client.RestoreRoomAsync(new RoomGatewayRestoreRoomRequest("session-1", "cn", "server-a"));
        var response = new WireRestoreRoomRes
        {
            Success = false,
            HasActiveRoom = false,
            Message = "internal-error",
            Status = WireRoomRestoreStatus.Failed,
            ErrorCode = WireRoomRestoreErrorCode.InternalError
        };
        connection.CompleteResponse(connection.LastSentOpCode, connection.LastSentSeq, in response);

        var result = await task;
        Assert.Equal(RoomGatewayOpCodes.RestoreRoom, connection.LastSentOpCode);
        Assert.False(result.Success);
        Assert.Equal(RoomGatewaySessionRestoreStatus.Failed, result.Status);
        Assert.Equal(RoomGatewaySessionRestoreErrorCode.InternalError, result.ErrorCode);
        Assert.Equal(6, (int)result.Status);
        Assert.Equal(6, (int)result.ErrorCode);
        Assert.Null(client.Current);
    }

    [Fact]
    public void PushFeedRejectsOlderRevisionAndStopsAfterDispose()
    {
        var connection = new FakeGatewayConnection();
        var client = new RoomGatewayWireSessionClient(connection);
        var changeCount = 0;
        client.SnapshotChanged += _ => changeCount++;

        PushSnapshot(connection, CreateSnapshot(10L, RoomGatewaySessionPhase.Loading));
        PushSnapshot(connection, CreateSnapshot(9L, RoomGatewaySessionPhase.Lobby));

        Assert.Equal(1, changeCount);
        Assert.Equal(10L, client.Current!.RoomRevision);
        Assert.Equal(RoomGatewaySessionPhase.Loading, client.Current.Phase);

        client.Dispose();
        PushSnapshot(connection, CreateSnapshot(11L, RoomGatewaySessionPhase.InBattle));

        Assert.Equal(1, changeCount);
        Assert.Null(client.Current);
    }

    private static void PushSnapshot(FakeGatewayConnection connection, WireRoomSnapshot snapshot)
    {
        var push = new WireRoomStateChangedPush
        {
            RoomId = snapshot.Summary.RoomId,
            Snapshot = snapshot,
            ServerNowTicks = 123L
        };
        connection.Push(RoomGatewayOpCodes.RoomStateChanged, WireRoomGatewayBinary.Serialize(in push));
    }

    private static WireRoomSnapshot CreateSnapshot(long revision, RoomGatewaySessionPhase phase)
    {
        return new WireRoomSnapshot
        {
            Summary = new WireRoomSummary
            {
                RoomId = "room-1",
                OwnerAccountId = "owner-1"
            },
            Members = new List<string> { "owner-1" },
            Players = new List<WireRoomPlayerSnapshot>
            {
                new WireRoomPlayerSnapshot
                {
                    AccountId = "owner-1",
                    PlayerId = 42u,
                    TeamId = 1,
                    HeroId = 7,
                    SpawnPointId = 3,
                    Level = 5,
                    AttributeTemplateId = 11,
                    BasicAttackSkillId = 99,
                    SkillIds = new List<int> { 100, 101 },
                    LobbyReady = true,
                    AssetsLoaded = false,
                    LoadingProgress = 80,
                    IsOnline = true,
                    JoinOrdinal = 1L,
                    LoadedManifestVersion = 2,
                    LoadedManifestHash = "manifest-a",
                    LastSeenTicks = 100L
                }
            },
            CanStart = true,
            BattleId = "battle-1",
            WorldId = 9001ul,
            WorldStartAnchor = new WireWorldStartAnchor
            {
                StartServerTicks = 1000L,
                ServerTickFrequency = 10000000L,
                StartFrame = 3,
                FixedDeltaSeconds = 1d / 30d
            },
            RoomRevision = revision,
            LastEventSequence = 22L,
            Phase = (int)phase,
            PhaseReason = "testing",
            LaunchGeneration = 2L,
            LoadingDeadlineUnixMs = 123456789L,
            LaunchManifestHash = "manifest-a",
            LaunchManifestVersion = 2
        };
    }
}
