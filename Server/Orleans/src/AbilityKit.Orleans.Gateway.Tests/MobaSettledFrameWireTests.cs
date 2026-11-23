using AbilityKit.Demo.Moba.Services;
using AbilityKit.Orleans.Contracts.FrameSync;
using AbilityKit.Orleans.Gateway.Core;
using AbilityKit.Protocol.Moba.SettledFrames;
using MemoryPack;
using Xunit;

namespace AbilityKit.Orleans.Gateway.Tests;

public sealed class MobaSettledFrameWireTests
{
    [Fact]
    public void RealtimeAndCatchUpMergeWithoutSkippingMissingFrames()
    {
        var generation = Guid.NewGuid();
        var checkpoint = ToWire(generation, 0);
        var second = ToWire(generation, 2);
        var first = GatewaySettledFrameWire.ToWire(new FrameSyncSettledFrame(2, 7, 9, "battle",
            new BattleFrameSettlement(generation, 1,
                [new SettledFrameCommand(0, SettledCommandDisposition.Rejected, 1, 3020, []),
                 new SettledFrameCommand(1, SettledCommandDisposition.Executed, 3, 3003, [7, 8])],
                [new SettledFrameDecision(0, 4, 1, 37, 101, false, 1, 1, 0, 7)])));
        Assert.Equal(1, first.Decisions.Single().AuthoritativeFrame);
        Assert.Equal(7, first.Decisions.Single().AuthoritySequence);
        using var receiver = Create(checkpoint);
        receiver.Receive(MemoryPackSerializer.Deserialize<WireSettledFrame>(MemoryPackSerializer.Serialize(second)));
        Assert.False(receiver.TryGet(2, out _));

        receiver.ReceiveCatchUp(new WireSettledCatchUpResponse { Frames = [checkpoint] }, -1, 0);
        Assert.True(receiver.HasCheckpoint);
        Assert.False(receiver.TryConsume(2, out _));
        receiver.ReceiveCatchUp(new WireSettledCatchUpResponse { Frames = [first, second] }, 0, 2);
        Assert.True(receiver.TryGet(1, out var prepared));
        Assert.True(receiver.TryConsume(1, out var applied));
        Assert.Same(prepared, applied);
        var input = Assert.Single(applied.ExecutedInputs);
        Assert.Equal("3", input.Player.Value);
        Assert.Equal(new byte[] { 7, 8 }, input.Payload);
        Assert.Equal(37, Assert.Single(Assert.IsType<MobaSettledFrameMetadata>(applied.Metadata).Entries).Entry.PredictionKey);
        receiver.Receive(first);
        Assert.True(receiver.TryConsume(2, out _));
        Assert.False(receiver.TryConsume(2, out _));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConflictingOverlapAndForeignGenerationFailClosed(bool foreignGeneration)
    {
        var generation = Guid.NewGuid();
        var checkpoint = ToWire(generation, 0);
        using var receiver = Create(checkpoint);
        receiver.Receive(checkpoint);
        receiver.Receive(ToWire(generation, 2));
        var conflict = ToWire(foreignGeneration ? Guid.NewGuid() : generation, 2);
        conflict.Commands = [new WireSettledCommand { Ordinal = 0, PlayerId = 1, OpCode = 3003, Disposition = 1, ExecutedPayload = [42] }];
        Assert.Throws<InvalidOperationException>(() => receiver.ReceiveCatchUp(
            new WireSettledCatchUpResponse { Frames = [ToWire(generation, 1), conflict] }, 0, 2));
        Assert.Equal(0, receiver.LastConsumedFrame);
        Assert.False(receiver.TryConsume(1, out _));
        Assert.False(receiver.TryGet(2, out _));
    }

    private static MobaSettledFrameReceiver Create(WireSettledFrame checkpoint) =>
        new(new WireSettledSubscribeResponse { Identity = checkpoint.Identity, TickRate = 30, LastSettledFrame = 2 });

    private static WireSettledFrame ToWire(Guid generation, int frame) => GatewaySettledFrameWire.ToWire(
        new FrameSyncSettledFrame(2, 7, 9, "battle", new BattleFrameSettlement(generation, frame, [], [], frame == 0)));
}
