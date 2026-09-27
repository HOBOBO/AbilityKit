using AbilityKit.Ability.Host;
using AbilityKit.Core.Snapshots.Routing;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Protocol.Moba.StateSync;
using Xunit;

namespace AbilityKit.Demo.Moba.Presentation.SnapshotAdapter.Tests;

public sealed class ActorDespawnSnapshotRouteTests
{
    [Fact]
    public void TryDecodeRejectsMissingPayloadWithEmptyContractBatch()
    {
        var snapshot = new WorldStateSnapshot(ActorDespawnSnapshotRoute.OpCode, null!);

        var decoded = ActorDespawnSnapshotRoute.TryDecode(in snapshot, out var despawns);

        Assert.False(decoded);
        Assert.Empty(despawns);
    }

    [Fact]
    public void TryDecodeAcceptsSerializedEmptyBatch()
    {
        var payload = MobaActorDespawnSnapshotCodec.Serialize(
            Array.Empty<MobaActorDespawnSnapshotEntry>());
        var snapshot = new WorldStateSnapshot(ActorDespawnSnapshotRoute.OpCode, payload);

        var decoded = ActorDespawnSnapshotRoute.TryDecode(in snapshot, out var despawns);

        Assert.True(decoded);
        Assert.Empty(despawns);
    }

    [Fact]
    public void RegisterDecoderPublishesTypedRouteUsingMobaOpcode()
    {
        var registry = new RecordingDecoderRegistry();
        ActorDespawnSnapshotRoute.RegisterDecoder(registry);

        Assert.Equal(ActorDespawnSnapshotRoute.OpCode, registry.OpCode);
        Assert.Equal(typeof(ActorDespawnData[]), registry.PayloadType);

        var payload = MobaActorDespawnSnapshotCodec.Serialize(new[]
        {
            new MobaActorDespawnSnapshotEntry(
                actorId: 7,
                reason: (byte)ActorDespawnReason.HeroReplaced),
        });
        var snapshot = new WorldStateSnapshot(ActorDespawnSnapshotRoute.OpCode, payload);

        Assert.True(registry.Decode(in snapshot, out var despawns));
        var despawn = Assert.Single(despawns);
        Assert.Equal(7, despawn.ActorId);
        Assert.Equal(ActorDespawnReason.HeroReplaced, despawn.Reason);
    }

    [Fact]
    public void RegisterDecoderRejectsNullRegistry()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ActorDespawnSnapshotRoute.RegisterDecoder(null!));
    }

    private sealed class RecordingDecoderRegistry : ISnapshotDecoderRegistry
    {
        private ISnapshotDecoderRegistry.TryDecode<ActorDespawnData[]>? _decoder;

        public int OpCode { get; private set; }
        public Type? PayloadType { get; private set; }

        public void RegisterDecoder<T>(int opCode, ISnapshotDecoderRegistry.TryDecode<T> decoder)
        {
            OpCode = opCode;
            PayloadType = typeof(T);
            _decoder = Assert.IsType<ISnapshotDecoderRegistry.TryDecode<ActorDespawnData[]>>(decoder);
        }

        public bool Decode(in WorldStateSnapshot snapshot, out ActorDespawnData[] despawns)
        {
            Assert.NotNull(_decoder);
            return _decoder(in snapshot, out despawns);
        }
    }
}
