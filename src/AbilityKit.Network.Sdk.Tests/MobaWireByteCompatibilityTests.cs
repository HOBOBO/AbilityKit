using AbilityKit.Protocol.Catalog;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;
using MemoryPack;
using Xunit;

namespace AbilityKit.Network.Sdk.Tests;

/// <summary>
/// MOBA wire 正式化（P2）最小字节兼容闭包：Move 与 StateHash 两个已接管的 wire schema，
/// 固定 Base64 golden 锁住线上字节布局，生成类型与现有 codec/decoder 行为保持一致。
/// </summary>
public sealed class MobaWireByteCompatibilityTests
{
    // MobaMovePayload: sequential struct, x=1.5f (00 00 C0 3F) + z=-2.25f (00 00 10 C0)
    private const string MoveGoldenBase64 = "AADAPwAAEMA=";

    // MobaStateHashSnapshotPayload: sequential struct,
    // Version=1 (01 00 00 00) + Frame=120 (78 00 00 00) + Hash=123 (7B 00 00 00)
    private const string StateHashGoldenBase64 = "AQAAAHgAAAB7AAAA";

    [Fact]
    public void MobaMovePayload_GeneratedContract_PreservesBase64Golden()
    {
        var payload = MobaMoveCodec.Serialize(1.5f, -2.25f);

        Assert.Equal(MoveGoldenBase64, Convert.ToBase64String(payload));

        MobaMoveCodec.Deserialize(
            Convert.FromBase64String(MoveGoldenBase64),
            out var x,
            out var z);
        Assert.Equal(1.5f, x);
        Assert.Equal(-2.25f, z);
    }

    [Fact]
    public void MobaStateHashSnapshotPayload_GeneratedContract_PreservesBase64Golden()
    {
        var payload = MobaStateHashSnapshotCodec.Serialize(120, 123u);

        Assert.Equal(StateHashGoldenBase64, Convert.ToBase64String(payload));

        // 生成类型直接序列化必须与 codec 门面字节一致（同一 wire 布局）。
        var direct = MemoryPackSerializer.Serialize(new MobaStateHashSnapshotPayload(1, 120, 123u));
        Assert.Equal(payload, direct);

        var decoded = MemoryPackSerializer.Deserialize<MobaStateHashSnapshotPayload>(
            Convert.FromBase64String(StateHashGoldenBase64));
        Assert.Equal(1, decoded.Version);
        Assert.Equal(120, decoded.Frame);
        Assert.Equal(123u, decoded.Hash);
    }

    [Fact]
    public void MobaMovePayload_GeneratedContract_RoundTripsThroughMemoryPack()
    {
        var expected = new MobaMovePayload { X = -3.5f, Z = 7.25f };

        var bytes = MemoryPackSerializer.Serialize(expected);
        var actual = MemoryPackSerializer.Deserialize<MobaMovePayload>(bytes);

        Assert.Equal(expected.X, actual.X);
        Assert.Equal(expected.Z, actual.Z);
    }

    [Fact]
    public void MobaStateHashSnapshotPayload_GeneratedContract_RoundTripsThroughMemoryPack()
    {
        var expected = new MobaStateHashSnapshotPayload(2, 987654, 0xDEADBEEFu);

        var bytes = MemoryPackSerializer.Serialize(expected);
        var actual = MemoryPackSerializer.Deserialize<MobaStateHashSnapshotPayload>(bytes);

        Assert.Equal(expected.Version, actual.Version);
        Assert.Equal(expected.Frame, actual.Frame);
        Assert.Equal(expected.Hash, actual.Hash);
    }

    [Fact]
    public void MobaStateHashSnapshotCodec_StampsVersionAndRoundTrips()
    {
        var payload = MobaStateHashSnapshotCodec.Serialize(42, 123456789u);

        var decoded = MobaStateHashSnapshotCodec.Deserialize(payload);
        Assert.Equal(MobaStateHashSnapshotCodec.Version, decoded.Version);
        Assert.Equal(42, decoded.Frame);
        Assert.Equal(123456789u, decoded.Hash);

        Assert.Equal(default(MobaStateHashSnapshotPayload), MobaStateHashSnapshotCodec.Deserialize(null));
        Assert.Equal(default(MobaStateHashSnapshotPayload), MobaStateHashSnapshotCodec.Deserialize(Array.Empty<byte>()));
    }

    [Fact]
    public void MobaModule_DecodesStateHashPush()
    {
        var registry = new ProtocolPayloadDecoderRegistry();
        MobaProtocolDecoderModule.Register(registry);
        var payload = MobaStateHashSnapshotCodec.Serialize(120, 123u);

        var result = registry.Decode(
            MobaProtocolDecoderModule.CatalogId,
            "state-hash.push",
            new ArraySegment<byte>(payload));

        Assert.True(result.Success, result.Error);
        var decoded = Assert.IsType<MobaStateHashSnapshotPayload>(result.Value);
        Assert.Equal(MobaStateHashSnapshotCodec.Version, decoded.Version);
        Assert.Equal(120, decoded.Frame);
        Assert.Equal(123u, decoded.Hash);
    }
}
