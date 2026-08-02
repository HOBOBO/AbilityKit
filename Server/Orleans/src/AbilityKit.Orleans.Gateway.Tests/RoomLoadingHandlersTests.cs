using System;
using System.Collections.Generic;
using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Orleans.Gateway;
using AbilityKit.Orleans.Gateway.Handlers;
using AbilityKit.Protocol.Room;
using Orleans;
using Xunit;

namespace AbilityKit.Orleans.Gateway.Tests;

/// <summary>
/// 阶段 4 资源加载 / 状态查询 Handler 与 Mapper 测试。
/// Handler 的合法路径需要真实 IClusterClient（集成测试范畴），此处覆盖：
/// - 入参校验（空 payload / 缺字段 -> BadRequest）
/// - StartRoomBattle deprecated 路径（-> Conflict）
/// - RoomGatewayWireMapper.ToRoomOperationRes 字段映射正确性
/// </summary>
public sealed class RoomLoadingHandlersTests
{
    private static GatewaySessionContext NewContext() => new(connectionId: 1L);

    private static GatewayRequest NewRequest<T>(T payload) where T : struct
    {
        var bytes = WireRoomGatewayBinary.Serialize(in payload);
        return new GatewayRequest(seq: 1u, bytes.ToArray());
    }

    private static GatewayRequest NewEmptyRequest() => new GatewayRequest(seq: 1u, Array.Empty<byte>());

    // ===== 入参校验：空 payload -> BadRequest =====

    [Fact]
    public async System.Threading.Tasks.Task BeginLoadingHandler_empty_payload_returns_bad_request()
    {
        var handler = new BeginLoadingHandler(clusterClient: null!);
        var response = await handler.HandleAsync(NewEmptyRequest(), NewContext(), default);
        Assert.Equal(GatewayStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async System.Threading.Tasks.Task ReportAssetsLoadedHandler_empty_payload_returns_bad_request()
    {
        var handler = new ReportAssetsLoadedHandler(clusterClient: null!);
        var response = await handler.HandleAsync(NewEmptyRequest(), NewContext(), default);
        Assert.Equal(GatewayStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async System.Threading.Tasks.Task ReportLoadingProgressHandler_empty_payload_returns_bad_request()
    {
        var handler = new ReportLoadingProgressHandler(clusterClient: null!);
        var response = await handler.HandleAsync(NewEmptyRequest(), NewContext(), default);
        Assert.Equal(GatewayStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async System.Threading.Tasks.Task CancelLoadingHandler_empty_payload_returns_bad_request()
    {
        var handler = new CancelLoadingHandler(clusterClient: null!);
        var response = await handler.HandleAsync(NewEmptyRequest(), NewContext(), default);
        Assert.Equal(GatewayStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async System.Threading.Tasks.Task GetSnapshotHandler_empty_payload_returns_bad_request()
    {
        var handler = new GetSnapshotHandler(clusterClient: null!);
        var response = await handler.HandleAsync(NewEmptyRequest(), NewContext(), default);
        Assert.Equal(GatewayStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async System.Threading.Tasks.Task LeaveRoomHandler_empty_payload_returns_bad_request()
    {
        var handler = new LeaveRoomHandler(clusterClient: null!, roomMembership: null!);
        var response = await handler.HandleAsync(NewEmptyRequest(), NewContext(), default);
        Assert.Equal(GatewayStatusCode.BadRequest, response.StatusCode);
    }

    // ===== 入参校验：缺 session/room -> BadRequest（不触达 cluster） =====

    [Fact]
    public async System.Threading.Tasks.Task BeginLoadingHandler_missing_session_returns_bad_request()
    {
        var handler = new BeginLoadingHandler(clusterClient: null!);
        var req = new WireBeginLoadingReq { RoomId = "room-1" };
        var response = await handler.HandleAsync(NewRequest(req), NewContext(), default);
        Assert.Equal(GatewayStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async System.Threading.Tasks.Task GetSnapshotHandler_missing_room_returns_bad_request()
    {
        var handler = new GetSnapshotHandler(clusterClient: null!);
        var req = new WireGetSnapshotReq { SessionToken = "session-1" };
        var response = await handler.HandleAsync(NewRequest(req), NewContext(), default);
        Assert.Equal(GatewayStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async System.Threading.Tasks.Task LeaveRoomHandler_missing_session_returns_bad_request()
    {
        var handler = new LeaveRoomHandler(clusterClient: null!, roomMembership: null!);
        var req = new WireLeaveRoomReq { RoomId = "room-1" };
        var response = await handler.HandleAsync(NewRequest(req), NewContext(), default);
        Assert.Equal(GatewayStatusCode.BadRequest, response.StatusCode);
    }

    // ===== StartRoomBattle deprecated 路径 -> Conflict =====

    [Fact]
    public async System.Threading.Tasks.Task StartRoomBattleHandler_returns_conflict_with_deprecated_message()
    {
        // deprecated 路径不调用 cluster，传入 null 安全。
        var handler = new StartRoomBattleHandler(clusterClient: null!);
        var req = new WireStartRoomBattleReq { SessionToken = "s", RoomId = "r" };
        var response = await handler.HandleAsync(NewRequest(req), NewContext(), default);

        Assert.Equal(GatewayStatusCode.Conflict, response.StatusCode);
        var res = WireRoomGatewayBinary.Deserialize<WireStartRoomBattleRes>(
            new ArraySegment<byte>(response.Payload));
        Assert.False(res.Success);
        Assert.Contains("deprecated", res.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async System.Threading.Tasks.Task StartRoomBattleHandler_empty_payload_still_returns_conflict()
    {
        // deprecated 路径不解析 payload，任何请求都返回 Conflict。
        var handler = new StartRoomBattleHandler(clusterClient: null!);
        var response = await handler.HandleAsync(NewEmptyRequest(), NewContext(), default);
        Assert.Equal(GatewayStatusCode.Conflict, response.StatusCode);
    }

    // ===== Mapper：ToRoomOperationRes 字段映射 =====

    [Fact]
    public void ToRoomOperationRes_maps_all_result_fields_and_snapshot()
    {
        var result = new RoomOperationResult(
            Success: true,
            Applied: true,
            ErrorCode: RoomOperationErrorCode.None,
            Message: "applied",
            RoomRevision: 555L);

        var snapshot = new RoomSnapshot(
            Summary: new RoomSummary("cn", "s1", "room-555", "moba", "t", true, 10, 2, "owner-1", 1L, null),
            Members: new List<string> { "owner-1" },
            Players: new List<RoomPlayerSnapshot>
            {
                new("owner-1", 1, true, 1001, 1, 1, 1, 1, null, 1u,
                    LobbyReady: true, AssetsLoaded: false, IsOnline: true, JoinOrdinal: 0L,
                    LoadedManifestVersion: 0, LoadedManifestHash: null)
            },
            CanStart: true,
            BattleId: null,
            WorldStartAnchor: null,
            WorldId: 0UL,
            MemberStates: null,
            SchemaVersion: 2,
            RoomRevision: 555L,
            LastEventSequence: 9L,
            Phase: RoomPhase.Loading,
            PhaseReason: "begin",
            LaunchGeneration: 3L,
            LoadingDeadlineUnixMs: 123L,
            LaunchManifestHash: "h",
            LaunchManifestVersion: 3,
            LastStartFailureCode: null);

        var wire = RoomGatewayWireMapper.ToRoomOperationRes(result, snapshot);

        Assert.True(wire.Success);
        Assert.True(wire.Applied);
        Assert.Equal((int)RoomOperationErrorCode.None, wire.ErrorCode);
        Assert.Equal("applied", wire.Message);
        Assert.Equal(555L, wire.RoomRevision);
        // snapshot 字段
        Assert.Equal("room-555", wire.Snapshot.Summary.RoomId);
        Assert.Equal((int)RoomPhase.Loading, wire.Snapshot.Phase);
        Assert.Equal("begin", wire.Snapshot.PhaseReason);
        Assert.Equal(3L, wire.Snapshot.LaunchGeneration);
        Assert.Equal(555L, wire.Snapshot.RoomRevision);
        Assert.Equal(2, wire.Snapshot.SchemaVersion);
        // player 新字段
        var player = Assert.Single(wire.Snapshot.Players!);
        Assert.True(player.LobbyReady);
        Assert.True(player.IsOnline);
    }

    [Fact]
    public void ToRoomOperationRes_without_snapshot_keeps_lightweight_result_fields()
    {
        var result = RoomOperationResult.AppliedAt(77L);

        var wire = RoomGatewayWireMapper.ToRoomOperationRes(result, snapshot: null);

        Assert.True(wire.Success);
        Assert.True(wire.Applied);
        Assert.Equal(77L, wire.RoomRevision);
        Assert.True(string.IsNullOrEmpty(wire.Snapshot.Summary.RoomId));
        Assert.Null(wire.Snapshot.Players);
    }

    [Fact]
    public void ToRoomOperationRes_maps_rejected_result_with_error_code()
    {
        var result = RoomOperationResult.Rejected(
            RoomOperationErrorCode.InvalidPhase, "not in lobby", 10L);
        var snapshot = new RoomSnapshot(
            Summary: new RoomSummary("cn", "s1", "room-1", "moba", "t", true, 10, 1, "o", 1L, null),
            Members: new List<string>(),
            Players: new List<RoomPlayerSnapshot>(),
            CanStart: false,
            BattleId: null,
            WorldStartAnchor: null,
            WorldId: 0UL,
            MemberStates: null);

        var wire = RoomGatewayWireMapper.ToRoomOperationRes(result, snapshot);

        Assert.False(wire.Success);
        Assert.False(wire.Applied);
        Assert.Equal((int)RoomOperationErrorCode.InvalidPhase, wire.ErrorCode);
        Assert.Equal("not in lobby", wire.Message);
        Assert.Equal(10L, wire.RoomRevision);
    }

    // ===== Mapper：wire req -> Grain req =====

    [Fact]
    public void ToBeginLoadingReq_maps_account_revision_command()
    {
        var wire = new WireBeginLoadingReq
        {
            SessionToken = "s",
            RoomId = "r",
            ExpectedRevision = 7L,
            CommandId = "cmd-1"
        };

        var grain = RoomGatewayWireMapper.ToBeginLoadingReq("account-1", wire);

        Assert.Equal("account-1", grain.AccountId);
        Assert.Equal(7L, grain.ExpectedRevision);
        Assert.Equal("cmd-1", grain.CommandId);
    }

    [Fact]
    public void ToBeginLoadingReq_nullifies_empty_command_id()
    {
        var wire = new WireBeginLoadingReq { SessionToken = "s", RoomId = "r", CommandId = "" };
        var grain = RoomGatewayWireMapper.ToBeginLoadingReq("a", wire);
        Assert.Null(grain.CommandId);
    }

    [Fact]
    public void ToReportAssetsLoadedReq_maps_manifest_fields()
    {
        var wire = new WireReportAssetsLoadedReq
        {
            SessionToken = "s",
            RoomId = "r",
            LaunchGeneration = 4L,
            ManifestVersion = 8,
            ManifestHash = "hash-8",
            CommandId = "cmd-2"
        };

        var grain = RoomGatewayWireMapper.ToReportAssetsLoadedReq("account-2", wire);

        Assert.Equal("account-2", grain.AccountId);
        Assert.Equal(4L, grain.LaunchGeneration);
        Assert.Equal(8, grain.ManifestVersion);
        Assert.Equal("hash-8", grain.ManifestHash);
        Assert.Equal("cmd-2", grain.CommandId);
    }

    [Fact]
    public void ToReportLoadingProgressReq_maps_launch_identity_and_progress()
    {
        var wire = new WireReportLoadingProgressReq
        {
            SessionToken = "s",
            RoomId = "r",
            LaunchGeneration = 5L,
            ManifestVersion = 9,
            ManifestHash = "hash-9",
            Progress = 64
        };

        var grain = RoomGatewayWireMapper.ToReportLoadingProgressReq("account-progress", wire);

        Assert.Equal("account-progress", grain.AccountId);
        Assert.Equal(5L, grain.LaunchGeneration);
        Assert.Equal(9, grain.ManifestVersion);
        Assert.Equal("hash-9", grain.ManifestHash);
        Assert.Equal(64, grain.Progress);
    }

    [Fact]
    public void ToCancelLoadingReq_maps_revision_and_command()
    {
        var wire = new WireCancelLoadingReq
        {
            SessionToken = "s",
            RoomId = "r",
            ExpectedRevision = 9L,
            CommandId = "cmd-3"
        };

        var grain = RoomGatewayWireMapper.ToCancelLoadingReq("account-3", wire);

        Assert.Equal("account-3", grain.AccountId);
        Assert.Equal(9L, grain.ExpectedRevision);
        Assert.Equal("cmd-3", grain.CommandId);
    }
}
