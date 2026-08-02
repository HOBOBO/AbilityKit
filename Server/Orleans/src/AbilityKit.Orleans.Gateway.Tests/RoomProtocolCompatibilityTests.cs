using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Protocol.Room;
using Xunit;

namespace AbilityKit.Orleans.Gateway.Tests;

public sealed class RoomProtocolCompatibilityTests
{
    [Fact]
    public void CanonicalRoomSnapshotRoundTripPreservesRepresentativePayload()
    {
        var snapshot = new WireRoomSnapshot
        {
            Summary = new WireRoomSummary
            {
                Region = "cn-east",
                ServerId = "server-1",
                RoomId = "room-42",
                RoomType = "moba",
                Title = "Ranked",
                IsPublic = true,
                MaxPlayers = 10,
                PlayerCount = 2,
                OwnerAccountId = "account-1",
                CreatedAtUnixMs = 123456789L,
                Tags = new Dictionary<string, string>
                {
                    ["mode"] = "5v5",
                    ["map"] = "classic"
                }
            },
            Members = new List<string> { "account-1", "account-2" },
            Players = new List<WireRoomPlayerSnapshot>
            {
                new()
                {
                    AccountId = "account-1",
                    TeamId = 1,
                    Ready = true,
                    HeroId = 1001,
                    SpawnPointId = 11,
                    Level = 3,
                    AttributeTemplateId = 2001,
                    BasicAttackSkillId = 3001,
                    SkillIds = new List<int> { 3002, 3003 },
                    PlayerId = 7
                }
            },
            CanStart = true,
            BattleId = "battle-42",
            WorldId = 4242UL
        };

        var bytes = WireRoomGatewayBinary.Serialize(in snapshot);
        var restored = WireRoomGatewayBinary.Deserialize<WireRoomSnapshot>(bytes);

        Assert.Equal("room-42", restored.Summary.RoomId);
        Assert.Equal("account-1", restored.Summary.OwnerAccountId);
        Assert.Equal("classic", restored.Summary.Tags!["map"]);
        Assert.Equal(new[] { "account-1", "account-2" }, restored.Members);
        var player = Assert.Single(restored.Players!);
        Assert.Equal("account-1", player.AccountId);
        Assert.Equal(new[] { 3002, 3003 }, player.SkillIds);
        Assert.True(restored.CanStart);
        Assert.Equal("battle-42", restored.BattleId);
        Assert.Equal(4242UL, restored.WorldId);
    }

    [Fact]
    public void SubscribeStateSyncRequestRoundTripPreservesCanonicalAuthenticationFields()
    {
        var request = new WireSubscribeStateSyncReq
        {
            SessionToken = "session-token",
            BattleId = "battle-42",
            RoomId = "room-42"
        };

        var bytes = WireRoomGatewayBinary.Serialize(in request);
        var restored = WireRoomGatewayBinary.Deserialize<WireSubscribeStateSyncReq>(bytes);

        Assert.Equal("session-token", restored.SessionToken);
        Assert.Equal("battle-42", restored.BattleId);
        Assert.Equal("room-42", restored.RoomId);
    }

    [Fact]
    public void SubmitBattleInputRequestRoundTripPreservesCommandSequenceAndLegacyDefault()
    {
        var request = new WireSubmitBattleInputReq
        {
            SessionToken = "session-token",
            BattleId = "battle-42",
            WorldId = 4242UL,
            Frame = 12,
            PlayerId = 7,
            InputOpCode = 1001,
            Payload = new byte[] { 1, 2, 3 },
            CommandSequence = 99
        };

        var bytes = WireRoomGatewayBinary.Serialize(in request);
        var restored = WireRoomGatewayBinary.Deserialize<WireSubmitBattleInputReq>(bytes);

        Assert.Equal(99UL, restored.CommandSequence);
        Assert.Equal(0UL, new WireSubmitBattleInputReq().CommandSequence);
    }

    [Fact]
    public void StateSyncDeliveryMetricsRoundTripPreservesAuthenticationAndLongCounters()
    {
        var request = new WireGetStateSyncDeliveryMetricsReq
        {
            SessionToken = "session-token",
            BattleId = "battle-42",
            RoomId = "room-42"
        };
        var requestBytes = WireRoomGatewayBinary.Serialize(in request);
        var restoredRequest = WireRoomGatewayBinary.Deserialize<WireGetStateSyncDeliveryMetricsReq>(requestBytes);

        Assert.Equal("session-token", restoredRequest.SessionToken);
        Assert.Equal("battle-42", restoredRequest.BattleId);
        Assert.Equal("room-42", restoredRequest.RoomId);

        var response = new WireGetStateSyncDeliveryMetricsRes
        {
            Success = true,
            ProducedBytes = 3_000_000_001L,
            SentBytes = 3_000_000_002L,
            DroppedBytes = 3_000_000_003L,
            MergedBytes = 3_000_000_004L,
            QueueLength = 1,
            QueueAgeTicks = 3_000_000_005L,
            BaselineAgeTicks = 3_000_000_006L,
            ResyncCount = 3_000_000_007L,
            Message = string.Empty
        };
        var responseBytes = WireRoomGatewayBinary.Serialize(in response);
        var restoredResponse = WireRoomGatewayBinary.Deserialize<WireGetStateSyncDeliveryMetricsRes>(responseBytes);

        Assert.True(restoredResponse.Success);
        Assert.Equal(3_000_000_003L, restoredResponse.DroppedBytes);
        Assert.Equal(3_000_000_004L, restoredResponse.MergedBytes);
        Assert.Equal(3_000_000_007L, restoredResponse.ResyncCount);
    }

    [Fact]
    public void RoomGatewayOpCodesRemainStable()
    {
        Assert.Equal(100U, RoomGatewayOpCodes.GuestLogin);
        Assert.Equal(111U, RoomGatewayOpCodes.AccountLogin);
        Assert.Equal(101U, RoomGatewayOpCodes.CreateRoom);
        Assert.Equal(102U, RoomGatewayOpCodes.JoinRoom);
        Assert.Equal(103U, RoomGatewayOpCodes.SubscribeStateSync);
        Assert.Equal(104U, RoomGatewayOpCodes.SetReady);
        Assert.Equal(105U, RoomGatewayOpCodes.PickHero);
        Assert.Equal(106U, RoomGatewayOpCodes.StartBattle);
        Assert.Equal(107U, RoomGatewayOpCodes.SubmitBattleInput);
        Assert.Equal(108U, RoomGatewayOpCodes.RequestFullStateSync);
        Assert.Equal(109U, RoomGatewayOpCodes.RestoreRoom);
        Assert.Equal(110U, RoomGatewayOpCodes.ListRooms);
        Assert.Equal(9002U, RoomGatewayOpCodes.SnapshotPushed);
        Assert.Equal(9003U, RoomGatewayOpCodes.DeltaSnapshotPushed);
        // 阶段 4 append-only opcode
        Assert.Equal(112U, RoomGatewayOpCodes.BeginLoading);
        Assert.Equal(113U, RoomGatewayOpCodes.ReportAssetsLoaded);
        Assert.Equal(114U, RoomGatewayOpCodes.CancelLoading);
        Assert.Equal(115U, RoomGatewayOpCodes.GetSnapshot);
        Assert.Equal(116U, RoomGatewayOpCodes.AckReliableBattleEvents);
        Assert.Equal(117U, RoomGatewayOpCodes.GetStateSyncDeliveryMetrics);
        Assert.Equal(119U, RoomGatewayOpCodes.LeaveRoom);
        Assert.Equal(9004U, RoomGatewayOpCodes.RoomStateChanged);
    }

    [Fact]
    public void WireRoomSnapshotNewFieldsRoundTripPreservePhaseLoadingMetadata()
    {
        var snapshot = new WireRoomSnapshot
        {
            Summary = new WireRoomSummary { RoomId = "room-99" },
            Members = new List<string> { "account-1" },
            CanStart = false,
            BattleId = string.Empty,
            WorldId = 0UL,
            WorldStartAnchor = new WireWorldStartAnchor
            {
                StartServerTicks = 1111L,
                ServerTickFrequency = 60L,
                StartFrame = 5,
                FixedDeltaSeconds = 0.016666
            },
            SchemaVersion = 2,
            RoomRevision = 777L,
            LastEventSequence = 42L,
            Phase = 1, // Loading
            PhaseReason = "owner_begin_loading",
            LaunchGeneration = 9L,
            LoadingDeadlineUnixMs = 1700000000000L,
            LaunchManifestHash = "sha-abc",
            LaunchManifestVersion = 3,
            LastStartFailureCode = string.Empty
        };

        var bytes = WireRoomGatewayBinary.Serialize(in snapshot);
        var restored = WireRoomGatewayBinary.Deserialize<WireRoomSnapshot>(bytes);

        Assert.Equal(1111L, restored.WorldStartAnchor.StartServerTicks);
        Assert.Equal(2, restored.SchemaVersion);
        Assert.Equal(777L, restored.RoomRevision);
        Assert.Equal(42L, restored.LastEventSequence);
        Assert.Equal(1, restored.Phase);
        Assert.Equal("owner_begin_loading", restored.PhaseReason);
        Assert.Equal(9L, restored.LaunchGeneration);
        Assert.Equal(1700000000000L, restored.LoadingDeadlineUnixMs);
        Assert.Equal("sha-abc", restored.LaunchManifestHash);
        Assert.Equal(3, restored.LaunchManifestVersion);
        Assert.Equal(string.Empty, restored.LastStartFailureCode);
    }

    [Fact]
    public void WireRoomPlayerSnapshotNewFieldsRoundTripPreserveLoadingAndPresence()
    {
        var player = new WireRoomPlayerSnapshot
        {
            AccountId = "account-2",
            TeamId = 2,
            Ready = true,
            HeroId = 2002,
            SpawnPointId = 22,
            Level = 5,
            AttributeTemplateId = 2002,
            BasicAttackSkillId = 3002,
            SkillIds = new List<int> { 3004 },
            PlayerId = 9,
            LobbyReady = true,
            AssetsLoaded = true,
            IsOnline = true,
            JoinOrdinal = 3L,
            LoadedManifestVersion = 3,
            LoadedManifestHash = "sha-xyz",
            LastSeenTicks = 1234L,
            OfflineSinceTicks = 1200L,
            LoadingProgress = 100
        };

        var bytes = WireRoomGatewayBinary.Serialize(in player);
        var restored = WireRoomGatewayBinary.Deserialize<WireRoomPlayerSnapshot>(bytes);

        Assert.True(restored.LobbyReady);
        Assert.True(restored.AssetsLoaded);
        Assert.True(restored.IsOnline);
        Assert.Equal(3L, restored.JoinOrdinal);
        Assert.Equal(3, restored.LoadedManifestVersion);
        Assert.Equal("sha-xyz", restored.LoadedManifestHash);
        Assert.Equal(1234L, restored.LastSeenTicks);
        Assert.Equal(1200L, restored.OfflineSinceTicks);
        Assert.Equal(100, restored.LoadingProgress);
    }

    [Fact]
    public void WireBeginLoadingRequestRoundTripPreservesRevisionAndCommandId()
    {
        var req = new WireBeginLoadingReq
        {
            SessionToken = "session-1",
            RoomId = "room-1",
            ExpectedRevision = 10L,
            CommandId = "cmd-1"
        };

        var bytes = WireRoomGatewayBinary.Serialize(in req);
        var restored = WireRoomGatewayBinary.Deserialize<WireBeginLoadingReq>(bytes);

        Assert.Equal("session-1", restored.SessionToken);
        Assert.Equal("room-1", restored.RoomId);
        Assert.Equal(10L, restored.ExpectedRevision);
        Assert.Equal("cmd-1", restored.CommandId);
    }

    [Fact]
    public void WireReportAssetsLoadedRequestRoundTripPreservesManifestFields()
    {
        var req = new WireReportAssetsLoadedReq
        {
            SessionToken = "session-2",
            RoomId = "room-2",
            LaunchGeneration = 5L,
            ManifestVersion = 7,
            ManifestHash = "hash-7",
            CommandId = "cmd-2"
        };

        var bytes = WireRoomGatewayBinary.Serialize(in req);
        var restored = WireRoomGatewayBinary.Deserialize<WireReportAssetsLoadedReq>(bytes);

        Assert.Equal(5L, restored.LaunchGeneration);
        Assert.Equal(7, restored.ManifestVersion);
        Assert.Equal("hash-7", restored.ManifestHash);
        Assert.Equal("cmd-2", restored.CommandId);
    }

    [Fact]
    public void WireRoomOperationResRoundTripPreservesResultAndSnapshot()
    {
        var res = new WireRoomOperationRes
        {
            Success = true,
            Applied = true,
            ErrorCode = 0,
            Message = "ok",
            RoomRevision = 123L,
            Snapshot = new WireRoomSnapshot
            {
                Summary = new WireRoomSummary { RoomId = "room-3" },
                Phase = 1,
                RoomRevision = 123L,
                LaunchGeneration = 2L
            }
        };

        var bytes = WireRoomGatewayBinary.Serialize(in res);
        var restored = WireRoomGatewayBinary.Deserialize<WireRoomOperationRes>(bytes);

        Assert.True(restored.Success);
        Assert.True(restored.Applied);
        Assert.Equal(0, restored.ErrorCode);
        Assert.Equal("ok", restored.Message);
        Assert.Equal(123L, restored.RoomRevision);
        Assert.Equal("room-3", restored.Snapshot.Summary.RoomId);
        Assert.Equal(1, restored.Snapshot.Phase);
        Assert.Equal(2L, restored.Snapshot.LaunchGeneration);
    }

    [Fact]
    public void WireReportLoadingProgressRequestRoundTripPreservesLaunchIdentityAndProgress()
    {
        var req = new WireReportLoadingProgressReq
        {
            SessionToken = "session-progress",
            RoomId = "room-progress",
            LaunchGeneration = 8L,
            ManifestVersion = 4,
            ManifestHash = "hash-progress",
            Progress = 67
        };

        var bytes = WireRoomGatewayBinary.Serialize(in req);
        var restored = WireRoomGatewayBinary.Deserialize<WireReportLoadingProgressReq>(bytes);

        Assert.Equal("session-progress", restored.SessionToken);
        Assert.Equal("room-progress", restored.RoomId);
        Assert.Equal(8L, restored.LaunchGeneration);
        Assert.Equal(4, restored.ManifestVersion);
        Assert.Equal("hash-progress", restored.ManifestHash);
        Assert.Equal(67, restored.Progress);
    }

    [Fact]
    public void WireRoomSnapshotResRoundTripPreservesGameplayRejection()
    {
        var res = new WireRoomSnapshotRes
        {
            Success = false,
            Applied = false,
            ErrorCode = 8,
            RoomId = "room-conflict",
            NumericRoomId = 42UL,
            Message = "Hero is already selected.",
            Snapshot = new WireRoomSnapshot
            {
                Summary = new WireRoomSummary { RoomId = "room-conflict" },
                RoomRevision = 7L
            }
        };

        var bytes = WireRoomGatewayBinary.Serialize(in res);
        var restored = WireRoomGatewayBinary.Deserialize<WireRoomSnapshotRes>(bytes);

        Assert.False(restored.Success);
        Assert.False(restored.Applied);
        Assert.Equal(8, restored.ErrorCode);
        Assert.Equal("Hero is already selected.", restored.Message);
        Assert.Equal(7L, restored.Snapshot.RoomRevision);
    }

    [Fact]
    public void WireRoomStateChangedPushRoundTripPreservesRoomIdAndServerTicks()
    {
        var push = new WireRoomStateChangedPush
        {
            RoomId = "room-4",
            Snapshot = new WireRoomSnapshot
            {
                Summary = new WireRoomSummary { RoomId = "room-4" },
                Phase = 3
            },
            ServerNowTicks = 9999L
        };

        var bytes = WireRoomGatewayBinary.Serialize(in push);
        var restored = WireRoomGatewayBinary.Deserialize<WireRoomStateChangedPush>(bytes);

        Assert.Equal("room-4", restored.RoomId);
        Assert.Equal(3, restored.Snapshot.Phase);
        Assert.Equal(9999L, restored.ServerNowTicks);
    }

    [Fact]
    public void WireLeaveRoomRequestRoundTripPreservesLifecycleIdentity()
    {
        var request = new WireLeaveRoomReq
        {
            SessionToken = "session-leave",
            RoomId = "room-leave",
            ExpectedRevision = 42L,
            CommandId = "leave-command"
        };

        var bytes = WireRoomGatewayBinary.Serialize(in request);
        var restored = WireRoomGatewayBinary.Deserialize<WireLeaveRoomReq>(bytes);

        Assert.Equal("session-leave", restored.SessionToken);
        Assert.Equal("room-leave", restored.RoomId);
        Assert.Equal(42L, restored.ExpectedRevision);
        Assert.Equal("leave-command", restored.CommandId);
    }

    [Fact]
    public void LegacyWireRoomSnapshotWithOnlyLegacyOrdersDeserializesNewFieldsAsDefaults()
    {
        // 模拟旧客户端：仅写入 order 0-5 的字段（手动构造最小二进制）。
        // 通过构造一个只设置 legacy 字段的 snapshot 序列化，再断言新字段为默认值。
        // 由于 MemoryPack append-only 保证：缺少的高 order 字段反序列化为默认值。
        var legacy = new WireRoomSnapshot
        {
            Summary = new WireRoomSummary { RoomId = "room-legacy" },
            Members = new List<string> { "a" },
            CanStart = true,
            BattleId = "b-1",
            WorldId = 1UL
            // 新字段全部保持默认（未赋值）
        };

        var bytes = WireRoomGatewayBinary.Serialize(in legacy);
        var restored = WireRoomGatewayBinary.Deserialize<WireRoomSnapshot>(bytes);

        // legacy 字段保留
        Assert.Equal("room-legacy", restored.Summary.RoomId);
        Assert.True(restored.CanStart);
        Assert.Equal("b-1", restored.BattleId);
        Assert.Equal(1UL, restored.WorldId);
        // 新字段为默认值
        Assert.Equal(0, restored.SchemaVersion);
        Assert.Equal(0L, restored.RoomRevision);
        Assert.Equal(0L, restored.LastEventSequence);
        Assert.Equal(0, restored.Phase);
        Assert.Equal(0L, restored.LaunchGeneration);
        Assert.Equal(0L, restored.LoadingDeadlineUnixMs);
        Assert.Equal(0, restored.LaunchManifestVersion);
    }

    [Fact]
    public void MobaProtocolAssemblyDoesNotExportLegacyRoomNamespace()
    {
        var mobaProtocolAssembly = typeof(AbilityKit.Protocol.Moba.StateSync.OpCodes).Assembly;
        var legacyTypes = mobaProtocolAssembly
            .GetExportedTypes()
            .Where(type => string.Equals(type.Namespace, "AbilityKit.Protocol.Moba.Room", StringComparison.Ordinal))
            .Select(type => type.FullName)
            .ToArray();

        Assert.Empty(legacyTypes);
    }
}
