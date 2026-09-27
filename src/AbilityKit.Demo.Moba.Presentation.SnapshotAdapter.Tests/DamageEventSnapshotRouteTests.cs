using AbilityKit.Ability.Host;
using AbilityKit.Core.Snapshots.Routing;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Protocol.Moba.StateSync;
using Xunit;

namespace AbilityKit.Demo.Moba.Presentation.SnapshotAdapter.Tests;

public sealed class DamageEventSnapshotRouteTests
{
    [Fact]
    public void TryDecodeRejectsMissingPayloadWithEmptyContractBatch()
    {
        var snapshot = new WorldStateSnapshot(DamageEventSnapshotRoute.OpCode, null!);

        var decoded = DamageEventSnapshotRoute.TryDecode(in snapshot, out var events);

        Assert.False(decoded);
        Assert.Empty(events);
    }

    [Fact]
    public void TryDecodeAcceptsSerializedEmptyBatch()
    {
        var payload = MobaDamageEventSnapshotCodec.Serialize(
            Array.Empty<MobaDamageEventSnapshotEntry>());
        var snapshot = new WorldStateSnapshot(DamageEventSnapshotRoute.OpCode, payload);

        var decoded = DamageEventSnapshotRoute.TryDecode(in snapshot, out var events);

        Assert.True(decoded);
        Assert.Empty(events);
    }

    [Fact]
    public void RegisterDecoderPublishesTypedRouteUsingMobaOpcode()
    {
        var registry = new RecordingDecoderRegistry();
        DamageEventSnapshotRoute.RegisterDecoder(registry);

        Assert.Equal(DamageEventSnapshotRoute.OpCode, registry.OpCode);
        Assert.Equal(typeof(DamageEventData[]), registry.PayloadType);

        var payload = MobaDamageEventSnapshotCodec.Serialize(new[]
        {
            new MobaDamageEventSnapshotEntry(
                kind: (int)AbilityKit.Protocol.Moba.StateSync.DamageEventKind.Damage,
                attackerActorId: 4,
                targetActorId: 9,
                damageType: 2,
                value: 31f,
                reasonKind: 3,
                reasonParam: 6001,
                targetHp: 69f,
                targetMaxHp: 100f),
        });
        var snapshot = new WorldStateSnapshot(DamageEventSnapshotRoute.OpCode, payload);

        Assert.True(registry.Decode(in snapshot, out var events));
        var damage = Assert.Single(events);
        Assert.Equal(4, damage.AttackerId);
        Assert.Equal(9, damage.TargetId);
        Assert.Equal(31f, damage.Value);
    }

    [Fact]
    public void RegisterDecoderRejectsNullRegistry()
    {
        Assert.Throws<ArgumentNullException>(() =>
            DamageEventSnapshotRoute.RegisterDecoder(null!));
    }

    private sealed class RecordingDecoderRegistry : ISnapshotDecoderRegistry
    {
        private ISnapshotDecoderRegistry.TryDecode<DamageEventData[]>? _decoder;

        public int OpCode { get; private set; }
        public Type? PayloadType { get; private set; }

        public void RegisterDecoder<T>(int opCode, ISnapshotDecoderRegistry.TryDecode<T> decoder)
        {
            OpCode = opCode;
            PayloadType = typeof(T);
            _decoder = Assert.IsType<ISnapshotDecoderRegistry.TryDecode<DamageEventData[]>>(decoder);
        }

        public bool Decode(in WorldStateSnapshot snapshot, out DamageEventData[] events)
        {
            Assert.NotNull(_decoder);
            return _decoder(in snapshot, out events);
        }
    }
}

