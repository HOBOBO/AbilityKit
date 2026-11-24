using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Network.Runtime;
using Xunit;

namespace AbilityKit.Host.Extension.Tests;

/// <summary>
/// 记录通用帧包原语的可观察行为；这些诊断不定义跨会话处理或权威结算语义。
/// </summary>
public sealed class FramePacketProtocolCharacterizationTests
{
    private static readonly WorldId World = new("authority");
    private static readonly FrameIndex Frame = new(5);

    [Fact]
    public void Aggregator_SnapshotOnlyFragmentCanArriveBeforeInputFragment()
    {
        var aggregator = new RemoteFrameAggregator();
        var snapshotOnly = new FramePacket(World, Frame, Array.Empty<PlayerInputCommand>(),
            new WorldStateSnapshot(2, new byte[] { 2 }));
        var withInput = new FramePacket(World, Frame, new[] { Command(101) },
            new WorldStateSnapshot(1, new byte[] { 1 }));

        aggregator.AddPacket(snapshotOnly);
        Assert.Empty(aggregator.BuildInputFrame(Frame).Commands);
        Assert.Same(snapshotOnly, Assert.Single(aggregator.BuildSnapshotFrame(Frame).Envelopes));

        aggregator.AddPacket(withInput);
        Assert.Equal(101, Assert.Single(aggregator.BuildInputFrame(Frame).Commands).OpCode);
        Assert.Collection(aggregator.BuildSnapshotFrame(Frame).Envelopes,
            envelope => Assert.Same(snapshotOnly, envelope),
            envelope => Assert.Same(withInput, envelope));
    }

    [Fact]
    public void Aggregator_CurrentFrameKeyDoesNotFilterOtherWorldsOrRepeatedPackets()
    {
        var aggregator = new RemoteFrameAggregator();
        var packet = new FramePacket(World, Frame, new[] { Command(101) }, null);
        var otherWorldPacket = new FramePacket(new WorldId("other-authority"), Frame,
            new[] { Command(202) }, null);

        aggregator.AddPacket(packet);
        aggregator.AddPacket(otherWorldPacket);
        aggregator.AddPacket(packet);

        Assert.Equal(new[] { 101, 202, 101 }, aggregator.BuildInputFrame(Frame).Commands.Select(command => command.OpCode));
    }

    [Fact]
    public void Aggregator_ExplicitEmptyPacketAndMissingFrameBothBuildEmptyInput()
    {
        var aggregator = new RemoteFrameAggregator();
        aggregator.AddPacket(new FramePacket(World, Frame, Array.Empty<PlayerInputCommand>(), null));

        Assert.Empty(aggregator.BuildInputFrame(Frame).Commands);
        Assert.Empty(aggregator.BuildInputFrame(new FrameIndex(Frame.Value + 1)).Commands);
    }

    [Fact]
    public void JitterBuffer_OnlyMissingFrameIncrementsFilledDefaultCount()
    {
        using var buffer = new FrameJitterBuffer<PlayerInputCommand[]>(
            delayFrames: 0, MissingFrameMode.FillDefault, Array.Empty<PlayerInputCommand>);
        var missingFrame = Frame.Value + 1;
        buffer.Add(Frame.Value, Array.Empty<PlayerInputCommand>());
        buffer.Add(missingFrame + 1, new[] { Command(101) });

        Assert.True(buffer.TryConsume(Frame.Value, out var explicitEmpty));
        Assert.Empty(explicitEmpty);
        Assert.Equal(0L, buffer.FilledDefaultCount);
        Assert.False(buffer.TryGet(missingFrame, out _));

        Assert.True(buffer.TryConsume(missingFrame, out var filledEmpty));
        Assert.Empty(filledEmpty);
        Assert.Equal(1L, buffer.FilledDefaultCount);
    }

    private static PlayerInputCommand Command(int opcode) =>
        new(Frame, new PlayerId("player"), opcode, new byte[] { 1 });
}
