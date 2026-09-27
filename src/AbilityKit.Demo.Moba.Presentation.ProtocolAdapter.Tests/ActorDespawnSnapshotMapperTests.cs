using AbilityKit.Demo.Moba.Share;
using AbilityKit.Protocol.Moba.StateSync;
using Xunit;

namespace AbilityKit.Demo.Moba.Presentation.ProtocolAdapter.Tests;

public sealed class ActorDespawnSnapshotMapperTests
{
    [Fact]
    public void DecoderNormalizesEmptyPayloads()
    {
        Assert.Empty(ActorDespawnSnapshotDecoder.Decode(null!));
        Assert.Empty(ActorDespawnSnapshotDecoder.Decode(Array.Empty<byte>()));
        Assert.Empty(ActorDespawnSnapshotDecoder.Decode(
            MobaActorDespawnSnapshotCodec.Serialize(
                Array.Empty<MobaActorDespawnSnapshotEntry>())));
    }

    [Fact]
    public void DecoderMapsActorAndKnownReason()
    {
        var payload = MobaActorDespawnSnapshotCodec.Serialize(new[]
        {
            new MobaActorDespawnSnapshotEntry(
                actorId: 17,
                reason: (byte)ActorDespawnReason.ProjectileHitOrExit),
        });

        var actual = Assert.Single(ActorDespawnSnapshotDecoder.Decode(payload));

        Assert.Equal(17, actual.ActorId);
        Assert.Equal(ActorDespawnReason.ProjectileHitOrExit, actual.Reason);
    }

    [Fact]
    public void MapperPreservesUnknownWireReason()
    {
        const byte unknownReason = 222;
        var entry = new MobaActorDespawnSnapshotEntry(21, unknownReason);

        var actual = ActorDespawnSnapshotMapper.Map(in entry);

        Assert.Equal(21, actual.ActorId);
        Assert.Equal(unknownReason, (byte)actual.Reason);
    }
}
