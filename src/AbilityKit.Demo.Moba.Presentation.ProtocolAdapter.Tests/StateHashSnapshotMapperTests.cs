using AbilityKit.Demo.Moba.Share;
using AbilityKit.Protocol.Moba.StateSync;
using Xunit;

namespace AbilityKit.Demo.Moba.Presentation.ProtocolAdapter.Tests;

public sealed class StateHashSnapshotMapperTests
{
    [Fact]
    public void MapperPreservesEveryWireField()
    {
        var wirePayload = new MobaStateHashSnapshotPayload(
            version: 77,
            frame: 1234,
            hash: 0xDEADBEEFu);

        var actual = StateHashSnapshotMapper.Map(in wirePayload);

        Assert.True(actual.HasValue);
        Assert.Equal(77, actual.Version);
        Assert.Equal(1234, actual.FrameIndex);
        Assert.Equal(0xDEADBEEFu, actual.StateHash);
    }

    [Fact]
    public void DecoderMapsSerializedPayload()
    {
        var payload = MobaStateHashSnapshotCodec.Serialize(42, 123456789u);

        var actual = StateHashSnapshotDecoder.Decode(payload);

        Assert.True(actual.HasValue);
        Assert.Equal(StateHashSnapshotDecoder.SupportedVersion, actual.Version);
        Assert.Equal(42, actual.FrameIndex);
        Assert.Equal(123456789u, actual.StateHash);
    }

    [Fact]
    public void DecoderRejectsMissingAndEmptyPayloadsAsDefaultContract()
    {
        Assert.False(StateHashSnapshotDecoder.Decode(null!).HasValue);
        Assert.False(StateHashSnapshotDecoder.Decode(Array.Empty<byte>()).HasValue);
    }
}
