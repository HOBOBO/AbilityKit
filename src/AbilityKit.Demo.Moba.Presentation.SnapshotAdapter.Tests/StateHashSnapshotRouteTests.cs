using AbilityKit.Ability.Host;
using AbilityKit.Core.Snapshots.Routing;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Protocol.Moba.StateSync;
using Xunit;

namespace AbilityKit.Demo.Moba.Presentation.SnapshotAdapter.Tests;

public sealed class StateHashSnapshotRouteTests
{
    [Fact]
    public void RouteRejectsMissingPayloadWithDefaultContract()
    {
        var snapshot = new WorldStateSnapshot(StateHashSnapshotRoute.OpCode, null!);

        Assert.False(StateHashSnapshotRoute.TryDecode(in snapshot, out var stateHash));
        Assert.False(stateHash.HasValue);
    }

    [Fact]
    public void RouteRegistersPlatformNeutralPayload()
    {
        var registry = new RecordingDecoderRegistry<StateHashData>();
        StateHashSnapshotRoute.RegisterDecoder(registry);
        var snapshot = new WorldStateSnapshot(
            StateHashSnapshotRoute.OpCode,
            MobaStateHashSnapshotCodec.Serialize(64, 987654321u));

        Assert.Equal(StateHashSnapshotRoute.OpCode, registry.OpCode);
        Assert.True(registry.Decode(in snapshot, out var stateHash));
        Assert.Equal(StateHashSnapshotRoute.SupportedVersion, stateHash.Version);
        Assert.Equal(64, stateHash.FrameIndex);
        Assert.Equal(987654321u, stateHash.StateHash);
    }

    [Fact]
    public void RouteRejectsNullRegistry()
    {
        Assert.Throws<ArgumentNullException>(() => StateHashSnapshotRoute.RegisterDecoder(null!));
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
