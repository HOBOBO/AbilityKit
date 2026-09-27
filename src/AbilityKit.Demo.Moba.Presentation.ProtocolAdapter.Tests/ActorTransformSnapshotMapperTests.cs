using AbilityKit.Demo.Moba.Share;
using AbilityKit.Protocol.Moba.StateSync;
using Xunit;

namespace AbilityKit.Demo.Moba.Presentation.ProtocolAdapter.Tests;

public sealed class ActorTransformSnapshotMapperTests
{
    [Fact]
    public void DecoderNormalizesEmptyPayloads()
    {
        Assert.Empty(ActorTransformSnapshotDecoder.Decode(null!));
        Assert.Empty(ActorTransformSnapshotDecoder.Decode(Array.Empty<byte>()));
        Assert.Empty(ActorTransformSnapshotDecoder.Decode(
            MobaActorTransformSnapshotCodec.Serialize(Array.Empty<MobaActorTransformSnapshotEntry>())));
    }

    [Fact]
    public void DecoderPreservesPresentationCoordinates()
    {
        var payload = MobaActorTransformSnapshotCodec.Serialize(new[]
        {
            new MobaActorTransformSnapshotEntry(
                actorId: 17,
                x: 1.25f,
                y: 2.5f,
                z: 3.75f,
                forwardX: 0.1f,
                forwardY: 0.2f,
                forwardZ: 0.3f),
        });

        var actual = Assert.Single(ActorTransformSnapshotDecoder.Decode(payload));

        Assert.Equal(17, actual.ActorId);
        Assert.Equal(1.25f, actual.PositionX);
        Assert.Equal(2.5f, actual.PositionY);
        Assert.Equal(3.75f, actual.PositionZ);
        Assert.Equal(0.1f, actual.ForwardX);
        Assert.Equal(0.2f, actual.ForwardY);
        Assert.Equal(0.3f, actual.ForwardZ);
        Assert.Equal(0f, actual.RotationY);
        Assert.Equal(1f, actual.Scale);
    }

    [Fact]
    public void LegacyConstructorRetainsPresentationContractDefaults()
    {
        var actual = new ActorTransformData(
            actorId: 9,
            x: 10f,
            y: 20f,
            z: 30f,
            rotationY: 45f,
            scale: 2f);

        Assert.Equal(9, actual.ActorId);
        Assert.Equal(10f, actual.PositionX);
        Assert.Equal(20f, actual.PositionY);
        Assert.Equal(30f, actual.PositionZ);
        Assert.Equal(0f, actual.ForwardX);
        Assert.Equal(0f, actual.ForwardY);
        Assert.Equal(1f, actual.ForwardZ);
        Assert.Equal(45f, actual.RotationY);
        Assert.Equal(2f, actual.Scale);
    }
}
