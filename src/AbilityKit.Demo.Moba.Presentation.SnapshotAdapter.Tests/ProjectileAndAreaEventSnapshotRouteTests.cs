using AbilityKit.Ability.Host;
using AbilityKit.Core.Snapshots.Routing;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Protocol.Moba.StateSync;
using Xunit;

namespace AbilityKit.Demo.Moba.Presentation.SnapshotAdapter.Tests;

public sealed class ProjectileAndAreaEventSnapshotRouteTests
{
    [Fact]
    public void RoutesRejectMissingPayloadWithEmptyContractBatch()
    {
        var projectile = new WorldStateSnapshot(ProjectileEventSnapshotRoute.OpCode, null!);
        var area = new WorldStateSnapshot(AreaEventSnapshotRoute.OpCode, null!);

        Assert.False(ProjectileEventSnapshotRoute.TryDecode(in projectile, out var projectileEvents));
        Assert.Empty(projectileEvents);
        Assert.False(AreaEventSnapshotRoute.TryDecode(in area, out var areaEvents));
        Assert.Empty(areaEvents);
    }

    [Fact]
    public void ProjectileRouteRegistersPlatformNeutralPayload()
    {
        var registry = new RecordingDecoderRegistry<ProjectileEventData[]>();
        ProjectileEventSnapshotRoute.RegisterDecoder(registry);
        var payload = MobaProjectileEventSnapshotCodec.Serialize(new[]
        {
            new MobaProjectileEventSnapshotEntry(1, 10, 20, 30, 40, 50, 1f, 2f, 3f, 0, 0, 60),
        });
        var snapshot = new WorldStateSnapshot(ProjectileEventSnapshotRoute.OpCode, payload);

        Assert.Equal(ProjectileEventSnapshotRoute.OpCode, registry.OpCode);
        Assert.True(registry.Decode(in snapshot, out var events));
        Assert.Equal(60, Assert.Single(events).ProjectileId);
    }

    [Fact]
    public void AreaRouteRegistersPlatformNeutralPayload()
    {
        var registry = new RecordingDecoderRegistry<AreaEventData[]>();
        AreaEventSnapshotRoute.RegisterDecoder(registry);
        var payload = MobaAreaEventSnapshotCodec.Serialize(new[]
        {
            new MobaAreaEventSnapshotEntry(1, 10, 20, 30, 1f, 2f, 3f, 4f),
        });
        var snapshot = new WorldStateSnapshot(AreaEventSnapshotRoute.OpCode, payload);

        Assert.Equal(AreaEventSnapshotRoute.OpCode, registry.OpCode);
        Assert.True(registry.Decode(in snapshot, out var events));
        Assert.Equal(30, Assert.Single(events).TemplateId);
    }

    [Fact]
    public void RoutesRejectNullRegistry()
    {
        Assert.Throws<ArgumentNullException>(() => ProjectileEventSnapshotRoute.RegisterDecoder(null!));
        Assert.Throws<ArgumentNullException>(() => AreaEventSnapshotRoute.RegisterDecoder(null!));
    }

    private sealed class RecordingDecoderRegistry<TPayload> : ISnapshotDecoderRegistry
    {
        private ISnapshotDecoderRegistry.TryDecode<TPayload>? _decoder;

        public int OpCode { get; private set; }

        public void RegisterDecoder<T>(int opCode, ISnapshotDecoderRegistry.TryDecode<T> decoder)
        {
            OpCode = opCode;
            _decoder = Assert.IsType<ISnapshotDecoderRegistry.TryDecode<TPayload>>(decoder);
        }

        public bool Decode(in WorldStateSnapshot snapshot, out TPayload payload)
        {
            Assert.NotNull(_decoder);
            return _decoder(in snapshot, out payload);
        }
    }
}
