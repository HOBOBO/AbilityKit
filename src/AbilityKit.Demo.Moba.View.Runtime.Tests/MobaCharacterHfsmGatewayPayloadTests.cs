using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.StateMachine;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Deterministic;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Protocol.Room;
using Xunit;

namespace AbilityKit.Demo.Moba.View.Runtime.Tests;

public sealed class MobaCharacterHfsmGatewayPayloadTests
{
    [Fact]
    public void WireGatewayAndMaterializedDeltaPreserveHfsmBaselinePayload()
    {
        var payload = new byte[] { 7, 11, 19 };
        var wire = new WireStateSyncSnapshotPush
        {
            WorldId = 17, Frame = 42, IsFullSnapshot = true, SchemaVersion = 2,
            PayloadOpCode = MobaCharacterHfsmRollbackProvider.DefaultKey,
            Payload = payload,
            EventEpoch = "epoch", EventWatermark = 101
        };
        var binary = WireRoomGatewayBinary.Serialize(in wire);
        var decoded = WireRoomGatewayBinary.Deserialize<WireStateSyncSnapshotPush>(binary);
        var gateway = GatewayRoomResponseMapper.ToGatewaySnapshot(in decoded);
        Assert.Equal(2, gateway.SchemaVersion);
        Assert.Equal(MobaCharacterHfsmRollbackProvider.DefaultKey, gateway.PayloadOpCode);
        Assert.Equal(payload, gateway.Payload);
        payload[0] = 99;
        Assert.Equal(7, gateway.Payload[0]);

        var state = new MobaAuthoritativeSnapshotState();
        var materialized = state.Apply(in gateway);
        Assert.Equal(gateway.Payload, materialized.Payload);
        Assert.Equal("epoch", materialized.EventEpoch);
        Assert.Equal(101, materialized.EventWatermark);

        var delta = new GatewayStateSyncSnapshot(
            17, 43, 0, false, Array.Empty<GatewayStateSyncActorSnapshot>(),
            schemaVersion: 2, payloadOpCode: MobaCharacterHfsmRollbackProvider.DefaultKey,
            payload: new byte[] { 31, 37 });
        materialized = state.Apply(in delta);
        Assert.True(materialized.IsFullSnapshot);
        Assert.Equal(new byte[] { 31, 37 }, materialized.Payload);
        Assert.Equal(43, materialized.Frame);

        var compressed = MobaCharacterHfsmWireCodec.Encode(new byte[1024], out var opCode);
        Assert.Equal(MobaCharacterHfsmWireCodec.CompressedOpCode, opCode);
        var compressedWire = new WireStateSyncSnapshotPush
        {
            WorldId = 17, Frame = 44, IsFullSnapshot = false, SchemaVersion = 3,
            PayloadOpCode = opCode, Payload = compressed
        };
        var encoded = WireRoomGatewayBinary.Serialize(in compressedWire);
        var decodedCompressed = WireRoomGatewayBinary.Deserialize<WireStateSyncSnapshotPush>(encoded);
        var compressedGateway = GatewayRoomResponseMapper.ToGatewaySnapshot(in decodedCompressed);
        materialized = state.Apply(in compressedGateway);
        Assert.Equal(3, materialized.SchemaVersion);
        Assert.Equal(opCode, materialized.PayloadOpCode);
        Assert.Equal(compressed, materialized.Payload);
        Assert.Equal(44, materialized.Frame);
    }

    [Fact]
    public void SchemaTwoImportsFrameExactlyAndRejectsMissingHfsmPayload()
    {
        var context = new ActorContext();
        var actor = context.CreateEntity();
        var actors = new MobaActorRegistry();
        var definition = MobaCharacterHfsmProfile.CreateDefinition();
        try
        {
            actors.Register(17, actor);
            actor.AddCharacterHfsm(new MobaCharacterHfsmRuntime(17, definition, 0, Fixed64.Zero));
            actor.characterHfsm.Runtime.Tick(1, Fixed64.FromRaw(1000), true, false, false, 321, 10001);
            actor.characterHfsm.Runtime.Tick(2, Fixed64.FromRaw(2000), true, false, false, 321, 10001);
            var bytes = new MobaCharacterHfsmRollbackProvider(actors, definition)
                .Export(new FrameIndex(2));
            actor.characterHfsm.Runtime.Tick(3, Fixed64.FromRaw(3000), true, false, false, 0, 0);

            var good = new GatewayStateSyncSnapshot(1, 2, 0, true,
                Array.Empty<GatewayStateSyncActorSnapshot>(), schemaVersion: 2,
                payloadOpCode: MobaCharacterHfsmRollbackProvider.DefaultKey, payload: bytes);
            Assert.True(CharacterHfsmGatewayStateImporter.TryApply(in good, actors, definition));
            Assert.Equal(2, actor.characterHfsm.Runtime.Frame);
            Assert.Equal("life/alive/action/casting", actor.characterHfsm.Runtime.Action.Path);
            Assert.Equal(1, actor.characterHfsm.Runtime.Action.LocalFrame);

            var compressed = MobaCharacterHfsmWireCodec.Encode(bytes, out var compressedOpCode);
            Assert.Equal(MobaCharacterHfsmWireCodec.CompressedOpCode, compressedOpCode);
            actor.characterHfsm.Runtime.Tick(3, Fixed64.FromRaw(3000), true, false, false, 0, 0);
            var encoded = new GatewayStateSyncSnapshot(1, 2, 0, true,
                Array.Empty<GatewayStateSyncActorSnapshot>(), schemaVersion: 3,
                payloadOpCode: compressedOpCode, payload: compressed);
            Assert.True(CharacterHfsmGatewayStateImporter.TryApply(in encoded, actors, definition));
            Assert.Equal(1, actor.characterHfsm.Runtime.Action.LocalFrame);
            Assert.Equal(2, actor.characterHfsm.Runtime.Frame);

            actor.characterHfsm.Runtime.Tick(3, Fixed64.FromRaw(3000), true, false, false, 0, 0);
            var corrupt = new GatewayStateSyncSnapshot(1, 2, 0, true,
                Array.Empty<GatewayStateSyncActorSnapshot>(), schemaVersion: 3,
                payloadOpCode: compressedOpCode, payload: new byte[] { 1, 2, 3 });
            Assert.Throws<System.IO.InvalidDataException>(() => CharacterHfsmGatewayStateImporter.TryApply(in corrupt,
                actors, definition));
            Assert.Equal(3, actor.characterHfsm.Runtime.Frame);

            var wrongEncoding = new GatewayStateSyncSnapshot(1, 2, 0, true,
                Array.Empty<GatewayStateSyncActorSnapshot>(), schemaVersion: 3,
                payloadOpCode: MobaCharacterHfsmRollbackProvider.DefaultKey, payload: bytes);
            Assert.False(CharacterHfsmGatewayStateImporter.TryApply(in wrongEncoding, actors, definition));

            var missing = new GatewayStateSyncSnapshot(1, 2, 0, true,
                Array.Empty<GatewayStateSyncActorSnapshot>(), schemaVersion: 2);
            Assert.False(CharacterHfsmGatewayStateImporter.TryApply(in missing, actors, definition));
            var old = new GatewayStateSyncSnapshot(1, 2, 0, true,
                Array.Empty<GatewayStateSyncActorSnapshot>(), schemaVersion: 1);
            Assert.True(CharacterHfsmGatewayStateImporter.TryApply(in old, actors, definition));
            Assert.Equal(3, actor.characterHfsm.Runtime.Frame);
        }
        finally
        {
            if (actor.hasCharacterHfsm) actor.RemoveCharacterHfsm();
            actor.Destroy();
        }
    }
}
