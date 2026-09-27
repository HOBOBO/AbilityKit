using AbilityKit.Demo.Moba.Share;
using AbilityKit.Protocol.Moba.StateSync;
using Xunit;

namespace AbilityKit.Demo.Moba.Presentation.ProtocolAdapter.Tests;

public sealed class ProjectileAndAreaEventSnapshotMapperTests
{
    [Fact]
    public void ProjectileDecoderMapsEveryWireField()
    {
        var payload = MobaProjectileEventSnapshotCodec.Serialize(new[]
        {
            new MobaProjectileEventSnapshotEntry(
                kind: 77,
                projectileActorId: 101,
                ownerActorId: 102,
                templateId: 103,
                launcherActorId: 104,
                rootActorId: 105,
                x: 1.25f,
                y: 2.5f,
                z: 3.75f,
                hitCollider: 106,
                exitReason: 107,
                projectileId: 108,
                forwardX: 0.1f,
                forwardY: 0.2f,
                forwardZ: 0.3f),
        });

        var actual = Assert.Single(ProjectileEventSnapshotDecoder.Decode(payload));

        Assert.Equal((ProjectilePresentationEventKind)77, actual.Kind);
        Assert.Equal(101, actual.ProjectileActorId);
        Assert.Equal(102, actual.OwnerActorId);
        Assert.Equal(103, actual.TemplateId);
        Assert.Equal(104, actual.LauncherActorId);
        Assert.Equal(105, actual.RootActorId);
        Assert.Equal(1.25f, actual.X);
        Assert.Equal(2.5f, actual.Y);
        Assert.Equal(3.75f, actual.Z);
        Assert.Equal(106, actual.HitCollider);
        Assert.Equal(107, actual.ExitReason);
        Assert.Equal(108, actual.ProjectileId);
        Assert.Equal(0.1f, actual.ForwardX);
        Assert.Equal(0.2f, actual.ForwardY);
        Assert.Equal(0.3f, actual.ForwardZ);
    }

    [Fact]
    public void AreaDecoderMapsEveryWireFieldAndPreservesUnknownKind()
    {
        var payload = MobaAreaEventSnapshotCodec.Serialize(new[]
        {
            new MobaAreaEventSnapshotEntry(88, 201, 202, 203, 4.5f, 5.5f, 6.5f, 7.5f),
        });

        var actual = Assert.Single(AreaEventSnapshotDecoder.Decode(payload));

        Assert.Equal((AreaPresentationEventKind)88, actual.Kind);
        Assert.Equal(201, actual.AreaId);
        Assert.Equal(202, actual.OwnerActorId);
        Assert.Equal(203, actual.TemplateId);
        Assert.Equal(4.5f, actual.X);
        Assert.Equal(5.5f, actual.Y);
        Assert.Equal(6.5f, actual.Z);
        Assert.Equal(7.5f, actual.Radius);
    }

    [Fact]
    public void DecodersNormalizeMissingAndEmptyPayloads()
    {
        Assert.Empty(ProjectileEventSnapshotDecoder.Decode(null!));
        Assert.Empty(ProjectileEventSnapshotDecoder.Decode(Array.Empty<byte>()));
        Assert.Empty(AreaEventSnapshotDecoder.Decode(null!));
        Assert.Empty(AreaEventSnapshotDecoder.Decode(Array.Empty<byte>()));
    }
}
