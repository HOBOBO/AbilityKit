using AbilityKit.Ability.Host;
using AbilityKit.Core.Snapshots.Routing;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Protocol.Moba.StateSync;
using Xunit;

namespace AbilityKit.Demo.Moba.Presentation.SnapshotAdapter.Tests;

public sealed class ActorSpawnSnapshotRouteTests
{
    [Fact]
    public void TryDecodeRejectsMissingPayloadWithEmptyContractBatch()
    {
        var snapshot = new WorldStateSnapshot(ActorSpawnSnapshotRoute.OpCode, null!);

        var decoded = ActorSpawnSnapshotRoute.TryDecode(in snapshot, out var spawns);

        Assert.False(decoded);
        Assert.Empty(spawns);
    }

    [Fact]
    public void TryDecodeAcceptsSerializedEmptyBatch()
    {
        var payload = MobaActorSpawnSnapshotCodec.Serialize(
            Array.Empty<MobaActorSpawnSnapshotEntry>());
        var snapshot = new WorldStateSnapshot(ActorSpawnSnapshotRoute.OpCode, payload);

        var decoded = ActorSpawnSnapshotRoute.TryDecode(in snapshot, out var spawns);

        Assert.True(decoded);
        Assert.Empty(spawns);
    }

    [Fact]
    public void RegisterDecoderPublishesTypedRouteUsingMobaOpcode()
    {
        var registry = new RecordingDecoderRegistry();
        ActorSpawnSnapshotRoute.RegisterDecoder(registry);

        Assert.Equal(ActorSpawnSnapshotRoute.OpCode, registry.OpCode);
        Assert.Equal(typeof(ActorSpawnData[]), registry.PayloadType);

        var payload = MobaActorSpawnSnapshotCodec.Serialize(new[]
        {
            new MobaActorSpawnSnapshotEntry(
                netId: 7,
                kind: (int)SpawnEntityKind.Projectile,
                code: 6001,
                ownerNetId: 4,
                x: 11f,
                y: 22f,
                z: 33f),
        });
        var snapshot = new WorldStateSnapshot(ActorSpawnSnapshotRoute.OpCode, payload);

        Assert.True(registry.Decode(in snapshot, out var spawns));
        var spawn = Assert.Single(spawns);
        Assert.Equal(ActorSpawnKind.Projectile, spawn.Kind);
        Assert.Equal(7, spawn.ActorId);
        Assert.Equal(4, spawn.OwnerActorId);
        Assert.Equal(6001, spawn.EntityCode);
        Assert.Equal(11f, spawn.PositionX);
        Assert.Equal(22f, spawn.PositionY);
        Assert.Equal(33f, spawn.PositionZ);
    }

    [Fact]
    public void RegisterDecoderRejectsNullRegistry()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ActorSpawnSnapshotRoute.RegisterDecoder(null!));
    }

    private sealed class RecordingDecoderRegistry : ISnapshotDecoderRegistry
    {
        private ISnapshotDecoderRegistry.TryDecode<ActorSpawnData[]>? _decoder;

        public int OpCode { get; private set; }
        public Type? PayloadType { get; private set; }

        public void RegisterDecoder<T>(int opCode, ISnapshotDecoderRegistry.TryDecode<T> decoder)
        {
            OpCode = opCode;
            PayloadType = typeof(T);
            _decoder = Assert.IsType<ISnapshotDecoderRegistry.TryDecode<ActorSpawnData[]>>(decoder);
        }

        public bool Decode(in WorldStateSnapshot snapshot, out ActorSpawnData[] spawns)
        {
            Assert.NotNull(_decoder);
            return _decoder(in snapshot, out spawns);
        }
    }
}
