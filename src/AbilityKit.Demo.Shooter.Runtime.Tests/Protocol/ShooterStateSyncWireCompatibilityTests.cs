using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Protocol.Room;
using AbilityKit.Protocol.Shooter;
using MemoryPack;
using Xunit;

namespace AbilityKit.Demo.Shooter.Runtime.Tests.Protocol;

public sealed class ShooterStateSyncWireCompatibilityTests
{
    [Theory]
    [InlineData(ShooterStateSyncPayloadKind.Packed, 2, ShooterStateSyncCompatibilityStatus.Compatible)]
    [InlineData(ShooterStateSyncPayloadKind.Packed, 3, ShooterStateSyncCompatibilityStatus.Compatible)]
    [InlineData(ShooterStateSyncPayloadKind.Packed, 1, ShooterStateSyncCompatibilityStatus.UnsupportedOldVersion)]
    [InlineData(ShooterStateSyncPayloadKind.Packed, 4, ShooterStateSyncCompatibilityStatus.UnsupportedFutureVersion)]
    [InlineData(ShooterStateSyncPayloadKind.PureState, 1, ShooterStateSyncCompatibilityStatus.Compatible)]
    [InlineData(ShooterStateSyncPayloadKind.PureState, 2, ShooterStateSyncCompatibilityStatus.Compatible)]
    [InlineData(ShooterStateSyncPayloadKind.PureState, 0, ShooterStateSyncCompatibilityStatus.UnsupportedOldVersion)]
    [InlineData(ShooterStateSyncPayloadKind.PureState, 3, ShooterStateSyncCompatibilityStatus.UnsupportedFutureVersion)]
    public void CompatibilityPolicyClassifiesSupportedAndUnsupportedVersions(
        ShooterStateSyncPayloadKind payloadKind,
        int version,
        ShooterStateSyncCompatibilityStatus expectedStatus)
    {
        var result = payloadKind == ShooterStateSyncPayloadKind.Packed
            ? ShooterStateSyncCompatibilityPolicy.EvaluatePacked(version)
            : ShooterStateSyncCompatibilityPolicy.EvaluatePureState(version);

        Assert.Equal(payloadKind, result.PayloadKind);
        Assert.Equal(version, result.RequestedVersion);
        Assert.Equal(expectedStatus, result.Status);
        Assert.Equal(expectedStatus == ShooterStateSyncCompatibilityStatus.Compatible, result.IsCompatible);
        Assert.Equal(
            payloadKind == ShooterStateSyncPayloadKind.Packed
                ? ShooterStateSyncCompatibilityPolicy.MinimumPackedVersion
                : ShooterStateSyncCompatibilityPolicy.MinimumPureStateVersion,
            result.MinimumSupportedVersion);
        Assert.Equal(
            payloadKind == ShooterStateSyncPayloadKind.Packed
                ? ShooterPackedSnapshotCodec.CurrentVersion
                : ShooterPureStateSyncCodec.CurrentVersion,
            result.CurrentVersion);
    }

    [Theory]
    [InlineData(true, ShooterOpCodes.Snapshot.PackedState)]
    [InlineData(false, ShooterOpCodes.Snapshot.PackedStateDelta)]
    public void PackedSnapshotWireRoundTripPreservesFullAndDeltaPayloads(bool isFullSnapshot, int expectedPayloadOpCode)
    {
        var runtime = CreateRuntime("packed-wire", 6100);
        var baseline = runtime.ExportPackedSnapshot(7001ul, isFullSnapshot: true, authorityOverride: true);
        if (!isFullSnapshot)
        {
            Assert.True(runtime.ImportPackedSnapshot(in baseline));
            runtime.SubmitInput(0, new[] { new ShooterPlayerCommand(1, 0f, 1f, 0f, 1f, false) });
            Assert.True(runtime.Tick(1f / 30f));
        }

        var packed = runtime.ExportPackedSnapshot(7001ul, isFullSnapshot, authorityOverride: true);
        packed.AcknowledgedCommands = new[] { new ShooterCommandAcknowledgement(1, 41ul) };
        var packedBytes = ShooterPackedSnapshotCodec.Serialize(in packed);
        var wire = new WireStateSyncSnapshotPush
        {
            WorldId = packed.WorldId,
            Frame = packed.Frame,
            Timestamp = 321.25d,
            ServerTicks = packed.ServerTick,
            Actors = null,
            IsFullSnapshot = isFullSnapshot,
            PayloadOpCode = expectedPayloadOpCode,
            Payload = packedBytes
        };

        var wireBytes = WireRoomGatewayBinary.Serialize(in wire);
        var restoredWire = WireRoomGatewayBinary.Deserialize<WireStateSyncSnapshotPush>(wireBytes);
        var restoredPacked = ShooterPackedSnapshotCodec.Deserialize(restoredWire.Payload!);

        Assert.Equal(packed.WorldId, restoredWire.WorldId);
        Assert.Equal(packed.Frame, restoredWire.Frame);
        Assert.Equal(isFullSnapshot, restoredWire.IsFullSnapshot);
        Assert.Equal(expectedPayloadOpCode, restoredWire.PayloadOpCode);
        Assert.Equal(packedBytes, restoredWire.Payload);
        Assert.Equal(ShooterPackedSnapshotCodec.CurrentVersion, restoredPacked.Version);
        Assert.Equal(packed.SnapshotFlags, restoredPacked.SnapshotFlags);
        Assert.Equal(packed.EntityCount, restoredPacked.EntityCount);
        Assert.Equal(packed.StateHash, restoredPacked.StateHash);
        var acknowledgement = Assert.Single(restoredPacked.AcknowledgedCommands);
        Assert.Equal(1, acknowledgement.PlayerId);
        Assert.Equal(41ul, acknowledgement.CommandSequence);
    }

    [Theory]
    [InlineData(ShooterPureStateSnapshotKinds.FullBaseline, true, ShooterOpCodes.Snapshot.PureState)]
    [InlineData(ShooterPureStateSnapshotKinds.Delta, false, ShooterOpCodes.Snapshot.PureStateDelta)]
    public void PureStateSnapshotWireRoundTripPreservesFullAndDeltaPayloads(int snapshotKind, bool isFullSnapshot, int expectedPayloadOpCode)
    {
        var snapshot = new ShooterPureStateSnapshotPayload(
            ShooterPureStateSyncCodec.CurrentVersion,
            8002ul,
            44,
            4400,
            snapshotKind,
            isFullSnapshot ? 0 : 40,
            isFullSnapshot ? 0u : 0x00AB_CDEFu,
            0x1020_3040u,
            ShooterPureStateSyncSettings.Default,
            new[]
            {
                new ShooterPureStateEntityDelta(
                    11,
                    ShooterPackedEntityKinds.Player,
                    ShooterPureStateEntityLayers.KeyInteraction,
                    isFullSnapshot ? ShooterPureStateDeltaKinds.Spawn : ShooterPureStateDeltaKinds.Update,
                    3,
                    1000,
                    2000,
                    30,
                    40,
                    0,
                    0,
                    75,
                    ShooterPureStateEntityFlags.Alive | ShooterPureStateEntityFlags.Visible)
            },
            new[]
            {
                new ShooterPureStateVisibilityHint(
                    11,
                    ShooterPackedEntityKinds.Player,
                    ShooterPureStateEntityLayers.KeyInteraction,
                    ShooterPureStateEntityFlags.Visible,
                    250)
            },
            new[] { new ShooterCommandAcknowledgement(11, 73ul) });
        var payload = ShooterPureStateSyncCodec.Serialize(in snapshot);
        var wire = new WireStateSyncSnapshotPush
        {
            WorldId = snapshot.WorldId,
            Frame = snapshot.Frame,
            Timestamp = 456.75d,
            ServerTicks = snapshot.ServerTick,
            Actors = null,
            IsFullSnapshot = isFullSnapshot,
            PayloadOpCode = expectedPayloadOpCode,
            Payload = payload
        };

        var wireBytes = WireRoomGatewayBinary.Serialize(in wire);
        var restoredWire = WireRoomGatewayBinary.Deserialize<WireStateSyncSnapshotPush>(wireBytes);
        var restoredSnapshot = ShooterPureStateSyncCodec.Deserialize(restoredWire.Payload!);

        Assert.Equal(snapshot.WorldId, restoredWire.WorldId);
        Assert.Equal(snapshot.Frame, restoredWire.Frame);
        Assert.Equal(isFullSnapshot, restoredWire.IsFullSnapshot);
        Assert.Equal(expectedPayloadOpCode, restoredWire.PayloadOpCode);
        Assert.Equal(payload, restoredWire.Payload);
        Assert.Equal(ShooterPureStateSyncCodec.CurrentVersion, restoredSnapshot.Version);
        Assert.Equal(snapshot.SnapshotKind, restoredSnapshot.SnapshotKind);
        Assert.Equal(snapshot.BaselineFrame, restoredSnapshot.BaselineFrame);
        Assert.Equal(snapshot.BaselineHash, restoredSnapshot.BaselineHash);
        Assert.Equal(snapshot.StateHash, restoredSnapshot.StateHash);
        Assert.Single(restoredSnapshot.Entities);
        Assert.Single(restoredSnapshot.VisibilityHints);
        var acknowledgement = Assert.Single(restoredSnapshot.AcknowledgedCommands);
        Assert.Equal(11, acknowledgement.PlayerId);
        Assert.Equal(73ul, acknowledgement.CommandSequence);
    }

    [Fact]
    public void LegacySubscribePayloadDefaultsReliableCursorFields()
    {
        var legacy = new LegacyWireSubscribeStateSyncReq
        {
            SessionToken = "session-token",
            BattleId = "battle-1",
            RoomId = "room-1"
        };
        var bytes = MemoryPackSerializer.Serialize(legacy);

        var restored = WireRoomGatewayBinary.Deserialize<WireSubscribeStateSyncReq>(bytes);

        Assert.Equal("session-token", restored.SessionToken);
        Assert.Equal("battle-1", restored.BattleId);
        Assert.Equal("room-1", restored.RoomId);
        Assert.Equal(string.Empty, restored.EventEpoch ?? string.Empty);
        Assert.Equal(0L, restored.LastEventAck);
    }

    [Fact]
    public void LegacySnapshotPayloadDefaultsEventWatermark()
    {
        var legacy = new LegacyWireStateSyncSnapshotPush
        {
            WorldId = 7001ul,
            Frame = 42,
            Timestamp = 42.5d,
            IsFullSnapshot = true,
            Actors = null,
            PayloadOpCode = ShooterOpCodes.Snapshot.PackedState,
            Payload = new byte[] { 1, 2, 3 },
            ServerTicks = 4200L
        };
        var bytes = MemoryPackSerializer.Serialize(legacy);

        var restored = WireRoomGatewayBinary.Deserialize<WireStateSyncSnapshotPush>(bytes);

        Assert.Equal(7001ul, restored.WorldId);
        Assert.Equal(42, restored.Frame);
        Assert.Equal(4200L, restored.ServerTicks);
        Assert.Equal(0L, restored.EventWatermark);
    }

    private static ShooterBattleRuntimePort CreateRuntime(string matchId, int seed)
    {
        var runtime = new ShooterBattleRuntimePort();
        var start = new ShooterStartGamePayload(
            matchId,
            30,
            seed,
            new[]
            {
                new ShooterStartPlayer(1, "P1", 0f, 0f),
                new ShooterStartPlayer(2, "P2", 3f, 0f)
            });

        Assert.True(runtime.StartGame(in start));
        runtime.SubmitInput(0, new[] { new ShooterPlayerCommand(1, 1f, 0f, 1f, 0f, true) });
        Assert.True(runtime.Tick(1f / 30f));
        return runtime;
    }
}

[MemoryPackable]
internal partial struct LegacyWireSubscribeStateSyncReq
{
    [MemoryPackOrder(0)] public string SessionToken { get; set; }
    [MemoryPackOrder(1)] public string BattleId { get; set; }
    [MemoryPackOrder(2)] public string RoomId { get; set; }
}

[MemoryPackable]
internal partial struct LegacyWireStateSyncSnapshotPush
{
    [MemoryPackOrder(0)] public ulong WorldId { get; set; }
    [MemoryPackOrder(1)] public int Frame { get; set; }
    [MemoryPackOrder(2)] public double Timestamp { get; set; }
    [MemoryPackOrder(3)] public bool IsFullSnapshot { get; set; }
    [MemoryPackOrder(4)] public List<WireStateSyncActorSnapshot>? Actors { get; set; }
    [MemoryPackOrder(5)] public int PayloadOpCode { get; set; }
    [MemoryPackOrder(6)] public byte[]? Payload { get; set; }
    [MemoryPackOrder(7)] public long ServerTicks { get; set; }
}
