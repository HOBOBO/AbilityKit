using AbilityKit.Ability.Host;
using AbilityKit.Core.Snapshots.Routing;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Protocol.Moba.StateSync;
using Xunit;

namespace AbilityKit.Demo.Moba.Presentation.SnapshotAdapter.Tests;

public sealed class ActorTransformSnapshotRouteTests
{
    [Fact]
    public void TryDecodeRejectsMissingPayloadWithEmptyContractBatch()
    {
        var snapshot = new WorldStateSnapshot(ActorTransformSnapshotRoute.OpCode, null!);

        var decoded = ActorTransformSnapshotRoute.TryDecode(in snapshot, out var transforms);

        Assert.False(decoded);
        Assert.Empty(transforms);
    }

    [Fact]
    public void TryDecodeAcceptsSerializedEmptyBatch()
    {
        var payload = MobaActorTransformSnapshotCodec.Serialize(
            Array.Empty<MobaActorTransformSnapshotEntry>());
        var snapshot = new WorldStateSnapshot(ActorTransformSnapshotRoute.OpCode, payload);

        var decoded = ActorTransformSnapshotRoute.TryDecode(in snapshot, out var transforms);

        Assert.True(decoded);
        Assert.Empty(transforms);
    }

    [Fact]
    public void RegisterDecoderPublishesTypedRouteUsingMobaOpcode()
    {
        var registry = new RecordingDecoderRegistry();
        ActorTransformSnapshotRoute.RegisterDecoder(registry);

        Assert.Equal(ActorTransformSnapshotRoute.OpCode, registry.OpCode);
        Assert.Equal(typeof(ActorTransformData[]), registry.PayloadType);

        var payload = MobaActorTransformSnapshotCodec.Serialize(new[]
        {
            new MobaActorTransformSnapshotEntry(
                actorId: 7,
                x: 11f,
                y: 22f,
                z: 33f,
                forwardX: 0f,
                forwardY: 1f,
                forwardZ: 0f),
        });
        var snapshot = new WorldStateSnapshot(ActorTransformSnapshotRoute.OpCode, payload);

        Assert.True(registry.Decode(in snapshot, out var transforms));
        var transform = Assert.Single(transforms);
        Assert.Equal(7, transform.ActorId);
        Assert.Equal(11f, transform.PositionX);
        Assert.Equal(22f, transform.PositionY);
        Assert.Equal(33f, transform.PositionZ);
        Assert.Equal(0f, transform.ForwardX);
        Assert.Equal(1f, transform.ForwardY);
        Assert.Equal(0f, transform.ForwardZ);
    }

    [Fact]
    public void RegisterDecoderRejectsNullRegistry()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ActorTransformSnapshotRoute.RegisterDecoder(null!));
    }

    private sealed class RecordingDecoderRegistry : ISnapshotDecoderRegistry
    {
        private ISnapshotDecoderRegistry.TryDecode<ActorTransformData[]>? _decoder;

        public int OpCode { get; private set; }
        public Type? PayloadType { get; private set; }

        public void RegisterDecoder<T>(int opCode, ISnapshotDecoderRegistry.TryDecode<T> decoder)
        {
            OpCode = opCode;
            PayloadType = typeof(T);
            _decoder = Assert.IsType<ISnapshotDecoderRegistry.TryDecode<ActorTransformData[]>>(decoder);
        }

        public bool Decode(in WorldStateSnapshot snapshot, out ActorTransformData[] transforms)
        {
            Assert.NotNull(_decoder);
            return _decoder(in snapshot, out transforms);
        }
    }
}
