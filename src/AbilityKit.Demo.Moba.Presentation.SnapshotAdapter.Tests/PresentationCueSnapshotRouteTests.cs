using AbilityKit.Ability.Host;
using AbilityKit.Core.Snapshots.Routing;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Protocol.Moba.StateSync;
using Xunit;
using ContractStage = AbilityKit.Demo.Moba.Share.PresentationCueStage;
using ProtocolStage = AbilityKit.Protocol.Moba.StateSync.PresentationCueStage;

namespace AbilityKit.Demo.Moba.Presentation.SnapshotAdapter.Tests;

public sealed class PresentationCueSnapshotRouteTests
{
    [Fact]
    public void TryDecodeRejectsMissingPayloadWithEmptyContractBatch()
    {
        var snapshot = new WorldStateSnapshot(PresentationCueSnapshotRoute.OpCode, null!);

        var decoded = PresentationCueSnapshotRoute.TryDecode(in snapshot, out var cues);

        Assert.False(decoded);
        Assert.Empty(cues);

        snapshot = new WorldStateSnapshot(PresentationCueSnapshotRoute.OpCode, Array.Empty<byte>());
        decoded = PresentationCueSnapshotRoute.TryDecode(in snapshot, out cues);

        Assert.False(decoded);
        Assert.Empty(cues);
    }

    [Fact]
    public void TryDecodeAcceptsSerializedEmptyBatch()
    {
        var payload = MobaPresentationCueSnapshotCodec.Serialize(
            Array.Empty<MobaPresentationCueSnapshotEntry>());
        var snapshot = new WorldStateSnapshot(PresentationCueSnapshotRoute.OpCode, payload);

        var decoded = PresentationCueSnapshotRoute.TryDecode(in snapshot, out var cues);

        Assert.True(decoded);
        Assert.Empty(cues);
    }

    [Fact]
    public void TryDecodeMapsWireSnapshotToPresentationContracts()
    {
        var payload = MobaPresentationCueSnapshotCodec.Serialize(new[]
        {
            new MobaPresentationCueSnapshotEntry
            {
                Stage = (int)ProtocolStage.Executed,
                RequestKey = "cast-q-19",
                SourceActorId = 7,
                TargetActorId = 19,
            },
        });
        var snapshot = new WorldStateSnapshot(PresentationCueSnapshotRoute.OpCode, payload);

        var decoded = PresentationCueSnapshotRoute.TryDecode(in snapshot, out var cues);

        Assert.True(decoded);
        var cue = Assert.Single(cues);
        Assert.Equal(ContractStage.Executed, cue.Stage);
        Assert.Equal("cast-q-19", cue.RequestKey);
        Assert.Equal(7, cue.SourceActorId);
        Assert.Equal(19, cue.TargetActorId);
    }

    [Fact]
    public void RegisterDecoderPublishesTypedRouteUsingMobaOpcode()
    {
        var registry = new RecordingDecoderRegistry();
        PresentationCueSnapshotRoute.RegisterDecoder(registry);

        Assert.Equal(PresentationCueSnapshotRoute.OpCode, registry.OpCode);
        Assert.Equal(typeof(PresentationCueData[]), registry.PayloadType);

        var payload = MobaPresentationCueSnapshotCodec.Serialize(new[]
        {
            new MobaPresentationCueSnapshotEntry { RequestKey = "registered-route" },
        });
        var snapshot = new WorldStateSnapshot(PresentationCueSnapshotRoute.OpCode, payload);

        Assert.True(registry.Decode(in snapshot, out var cues));
        Assert.Equal("registered-route", Assert.Single(cues).RequestKey);
    }

    [Fact]
    public void RegisterDecoderRejectsNullRegistry()
    {
        Assert.Throws<ArgumentNullException>(() =>
            PresentationCueSnapshotRoute.RegisterDecoder(null!));
    }

    private sealed class RecordingDecoderRegistry : ISnapshotDecoderRegistry
    {
        private ISnapshotDecoderRegistry.TryDecode<PresentationCueData[]>? _decoder;

        public int OpCode { get; private set; }
        public Type? PayloadType { get; private set; }

        public void RegisterDecoder<T>(int opCode, ISnapshotDecoderRegistry.TryDecode<T> decoder)
        {
            OpCode = opCode;
            PayloadType = typeof(T);
            _decoder = Assert.IsType<ISnapshotDecoderRegistry.TryDecode<PresentationCueData[]>>(decoder);
        }

        public bool Decode(in WorldStateSnapshot snapshot, out PresentationCueData[] cues)
        {
            Assert.NotNull(_decoder);
            return _decoder(in snapshot, out cues);
        }
    }
}
