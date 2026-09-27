using AbilityKit.Ability.Host;
using AbilityKit.Core.Snapshots.Routing;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Protocol.Moba.StateSync;
using Xunit;

namespace AbilityKit.Demo.Moba.Presentation.SnapshotAdapter.Tests;

public sealed class SkillStateSnapshotRouteTests
{
    [Fact]
    public void TryDecodeRejectsMissingPayloadWithEmptyContractBatch()
    {
        var snapshot = new WorldStateSnapshot(SkillStateSnapshotRoute.OpCode, null!);

        var decoded = SkillStateSnapshotRoute.TryDecode(in snapshot, out var states);

        Assert.False(decoded);
        Assert.Empty(states);
    }

    [Fact]
    public void RegisterDecoderPublishesContractTypeUsingMobaOpcode()
    {
        var registry = new RecordingDecoderRegistry();
        SkillStateSnapshotRoute.RegisterDecoder(registry);

        Assert.Equal(SkillStateSnapshotRoute.OpCode, registry.OpCode);
        Assert.Equal(typeof(SkillStateData[]), registry.PayloadType);

        var payload = MobaSkillStateSnapshotCodec.Serialize(new[]
        {
            new MobaSkillStateSnapshotEntry
            {
                ActorId = 7,
                Slot = 1,
                SkillId = 7101,
                Availability = MobaSkillAvailabilityState.Available,
            },
        });
        var snapshot = new WorldStateSnapshot(SkillStateSnapshotRoute.OpCode, payload);

        Assert.True(registry.Decode(in snapshot, out var states));
        var state = Assert.Single(states);
        Assert.Equal(7, state.ActorId);
        Assert.Equal(7101, state.SkillId);
    }

    [Fact]
    public void RegisterDecoderRejectsNullRegistry()
    {
        Assert.Throws<ArgumentNullException>(() =>
            SkillStateSnapshotRoute.RegisterDecoder(null!));
    }

    private sealed class RecordingDecoderRegistry : ISnapshotDecoderRegistry
    {
        private ISnapshotDecoderRegistry.TryDecode<SkillStateData[]>? _decoder;

        public int OpCode { get; private set; }
        public Type? PayloadType { get; private set; }

        public void RegisterDecoder<T>(int opCode, ISnapshotDecoderRegistry.TryDecode<T> decoder)
        {
            OpCode = opCode;
            PayloadType = typeof(T);
            _decoder = Assert.IsType<ISnapshotDecoderRegistry.TryDecode<SkillStateData[]>>(decoder);
        }

        public bool Decode(in WorldStateSnapshot snapshot, out SkillStateData[] states)
        {
            Assert.NotNull(_decoder);
            return _decoder(in snapshot, out states);
        }
    }
}
