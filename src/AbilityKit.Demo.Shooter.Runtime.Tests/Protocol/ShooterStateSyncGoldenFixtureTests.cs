using AbilityKit.Protocol.Room;
using AbilityKit.Protocol.Shooter;
using Xunit;

namespace AbilityKit.Demo.Shooter.Runtime.Tests.Protocol;

public sealed class ShooterStateSyncGoldenFixtureTests
{
    [Theory]
    [InlineData("packed-current.gateway-push.base64", ShooterStateSyncPayloadKind.Packed)]
    [InlineData("pure-state-current.gateway-push.base64", ShooterStateSyncPayloadKind.PureState)]
    public void CurrentGatewayPushRoundTripsAfterAppendOnlyExtension(string fixtureName, ShooterStateSyncPayloadKind payloadKind)
    {
        _ = fixtureName;
        var actual = CreateGatewayPushBytes(payloadKind);
        var wire = WireRoomGatewayBinary.Deserialize<WireStateSyncSnapshotPush>(actual);

        Assert.NotNull(wire.Payload);
        if (payloadKind == ShooterStateSyncPayloadKind.Packed)
        {
            var packed = ShooterPackedSnapshotCodec.Deserialize(wire.Payload!);
            Assert.Empty(packed.AcknowledgedCommands);
            return;
        }

        var pureState = ShooterPureStateSyncCodec.Deserialize(wire.Payload!);
        Assert.Empty(pureState.AcknowledgedCommands);
    }

    [Theory]
    [InlineData("packed-current.gateway-push.base64", ShooterStateSyncPayloadKind.Packed)]
    [InlineData("pure-state-current.gateway-push.base64", ShooterStateSyncPayloadKind.PureState)]
    public void GoldenGatewayPushDecodesExpectedSchema(string fixtureName, ShooterStateSyncPayloadKind payloadKind)
    {
        var fixtureText = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "StateSync", fixtureName)).Trim();
        if (string.Equals(fixtureText, "GENERATE", StringComparison.Ordinal))
        {
            return;
        }

        var bytes = Convert.FromBase64String(fixtureText);
        var wire = WireRoomGatewayBinary.Deserialize<WireStateSyncSnapshotPush>(bytes);

        Assert.Equal(0x0102_0304_0506_0708ul, wire.WorldId);
        Assert.Equal(42, wire.Frame);
        Assert.Equal(1234.5d, wire.Timestamp);
        Assert.Equal(42001L, wire.ServerTicks);
        Assert.True(wire.IsFullSnapshot);
        Assert.Null(wire.Actors);
        Assert.NotNull(wire.Payload);

        if (payloadKind == ShooterStateSyncPayloadKind.Packed)
        {
            Assert.Equal(ShooterOpCodes.Snapshot.PackedState, wire.PayloadOpCode);
            var payload = ShooterPackedSnapshotCodec.Deserialize(wire.Payload!);
            Assert.Equal(ShooterPackedSnapshotCodec.CurrentVersion, payload.Version);
            Assert.Equal(0xA1B2_C3D4u, payload.StateHash);
            Assert.Equal(new byte[] { 0x10, 0x20, 0x30 }, payload.ExtensionPayload);
            Assert.Single(payload.ComponentChunks);
            Assert.Equal(ShooterPackedComponentKinds.Transform, payload.ComponentChunks[0].ComponentKind);
            Assert.Empty(payload.AcknowledgedCommands);
            return;
        }

        Assert.Equal(ShooterOpCodes.Snapshot.PureState, wire.PayloadOpCode);
        var pureState = ShooterPureStateSyncCodec.Deserialize(wire.Payload!);
        Assert.Equal(ShooterPureStateSyncCodec.CurrentVersion, pureState.Version);
        Assert.Equal(ShooterPureStateSnapshotKinds.FullBaseline, pureState.SnapshotKind);
        Assert.Equal(0xB1C2_D3E4u, pureState.StateHash);
        Assert.Single(pureState.Entities);
        Assert.Single(pureState.VisibilityHints);
        Assert.Empty(pureState.AcknowledgedCommands);
    }

    internal static byte[] CreateGatewayPushBytes(ShooterStateSyncPayloadKind payloadKind)
    {
        byte[] payload;
        int payloadOpCode;
        if (payloadKind == ShooterStateSyncPayloadKind.Packed)
        {
            var packed = new ShooterPackedSnapshotPayload(
                ShooterPackedSnapshotCodec.CurrentVersion,
                0x0102_0304_0506_0708ul,
                42,
                42001L,
                ShooterPackedSnapshotFlags.Full | ShooterPackedSnapshotFlags.KeyFrame,
                0xA1B2_C3D4u,
                1,
                new byte[] { 0x10, 0x20, 0x30 },
                new[]
                {
                    new ShooterPackedComponentChunk(
                        ShooterPackedComponentKinds.Transform,
                        ShooterPackedEntityKinds.Player,
                        1,
                        new[] { 17 },
                        new[] { 1.25f },
                        new[] { -2.5f },
                        new[] { 0.5f },
                        new[] { 0.75f },
                        Array.Empty<int>(),
                        new byte[] { ShooterPackedEntityFlags.Alive | ShooterPackedEntityFlags.Player },
                        new[] { 9 },
                        Array.Empty<int>())
                });
            payload = ShooterPackedSnapshotCodec.Serialize(in packed);
            payloadOpCode = ShooterOpCodes.Snapshot.PackedState;
        }
        else
        {
            var pureState = new ShooterPureStateSnapshotPayload(
                ShooterPureStateSyncCodec.CurrentVersion,
                0x0102_0304_0506_0708ul,
                42,
                42001L,
                ShooterPureStateSnapshotKinds.FullBaseline,
                42,
                0xB1C2_D3E4u,
                0xB1C2_D3E4u,
                new ShooterPureStateSyncSettings(10000, 512, 60, 2, 15, 3),
                new[]
                {
                    new ShooterPureStateEntityDelta(
                        17,
                        ShooterPackedEntityKinds.Player,
                        ShooterPureStateEntityLayers.KeyInteraction,
                        ShooterPureStateDeltaKinds.Spawn,
                        9,
                        1250,
                        -2500,
                        50,
                        -25,
                        80,
                        7,
                        0,
                        ShooterPureStateEntityFlags.Alive | ShooterPureStateEntityFlags.Visible)
                },
                new[]
                {
                    new ShooterPureStateVisibilityHint(
                        17,
                        ShooterPackedEntityKinds.Player,
                        ShooterPureStateEntityLayers.KeyInteraction,
                        ShooterPureStateEntityFlags.Visible,
                        200)
                });
            payload = ShooterPureStateSyncCodec.Serialize(in pureState);
            payloadOpCode = ShooterOpCodes.Snapshot.PureState;
        }

        var wire = new WireStateSyncSnapshotPush
        {
            WorldId = 0x0102_0304_0506_0708ul,
            Frame = 42,
            Timestamp = 1234.5d,
            IsFullSnapshot = true,
            Actors = null,
            PayloadOpCode = payloadOpCode,
            Payload = payload,
            ServerTicks = 42001L
        };
        return WireRoomGatewayBinary.Serialize(in wire).ToArray();
    }
}
