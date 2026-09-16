using AbilityKit.Ability.FrameSync;
using AbilityKit.Deterministic;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.StateMachine;
using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Grains.Gameplays.Moba.Battle;
using Xunit;
using Xunit.Abstractions;

namespace AbilityKit.Orleans.Grains.Tests.Battle;

public sealed class MobaCharacterHfsmPushPayloadTests
{
    private readonly ITestOutputHelper _output;

    public MobaCharacterHfsmPushPayloadTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void FullPayloadRestoresLocalFrameAndRejectsMismatchedSampleFrame()
    {
        var context = new ActorContext();
        var actor = context.CreateEntity();
        var actors = new MobaActorRegistry();
        var definition = MobaCharacterHfsmProfile.CreateDefinition();
        try
        {
            actors.Register(17, actor);
            actor.AddCharacterHfsm(new MobaCharacterHfsmRuntime(17, definition, 0, Fixed64.Zero));
            actor.characterHfsm.Runtime.Tick(1, Fixed64.FromRaw(1000), true, false, false, 321, 10001);
            actor.characterHfsm.Runtime.Tick(2, Fixed64.FromRaw(2000), true, false, false, 321, 10001);
            var expected = actor.characterHfsm.Runtime.Action;

            var push = MobaCharacterHfsmPushPayload.Attach(
                new StateSyncPush { Frame = 2, SchemaVersion = 1 }, actors, definition, 2);
            Assert.Equal(MobaCharacterHfsmWireCodec.CompressedSchemaVersion, push.SchemaVersion);
            Assert.Equal(MobaCharacterHfsmWireCodec.CompressedOpCode, push.PayloadOpCode);
            Assert.NotEmpty(push.Payload!);
            var originalBytes = new MobaCharacterHfsmRollbackProvider(actors, definition)
                .Export(new FrameIndex(2));
            _output.WriteLine($"Character HFSM single-actor wire bytes: {push.Payload!.Length}, " +
                $"raw bytes: {originalBytes.Length}");
            Assert.True(push.Payload!.Length < originalBytes.Length * 0.8,
                $"HFSM wire bytes: {push.Payload.Length}, raw bytes: {originalBytes.Length}");

            actor.characterHfsm.Runtime.Tick(3, Fixed64.FromRaw(3000), true, false, true, 0, 0);
            new MobaCharacterHfsmRollbackProvider(actors, definition)
                .Import(new FrameIndex(2),
                    MobaCharacterHfsmWireCodec.Decode(push.PayloadOpCode, push.Payload!));
            Assert.Equal(expected.InstanceId, actor.characterHfsm.Runtime.Action.InstanceId);
            Assert.Equal(1, actor.characterHfsm.Runtime.Action.LocalFrame);

            var historical = MobaCharacterHfsmPushPayload.Attach(
                new StateSyncPush { Frame = 1, SchemaVersion = 1 }, actors, definition, 1);
            Assert.Equal(1, historical.SchemaVersion);
            Assert.Equal(0, historical.PayloadOpCode);
        }
        finally
        {
            if (actor.hasCharacterHfsm) actor.RemoveCharacterHfsm();
            actor.Destroy();
        }
    }

    [Fact]
    public void SmallPayloadRemainsRawAndCompressedDecodeHasSizeLimit()
    {
        var raw = new byte[] { 1, 2, 3 };
        Assert.Same(raw, MobaCharacterHfsmWireCodec.Encode(raw, out var opCode));
        Assert.Equal(MobaCharacterHfsmRollbackProvider.DefaultKey, opCode);
        Assert.Equal(raw, MobaCharacterHfsmWireCodec.Decode(opCode, raw));

        var oversized = new byte[MobaCharacterHfsmWireCodec.MaxDecodedBytes + 1];
        var gzip = new System.IO.MemoryStream();
        using (var compressor = new System.IO.Compression.GZipStream(gzip,
                   System.IO.Compression.CompressionLevel.Fastest, true))
            compressor.Write(oversized);
        Assert.Throws<System.IO.InvalidDataException>(() => MobaCharacterHfsmWireCodec.Decode(
            MobaCharacterHfsmWireCodec.CompressedOpCode, gzip.ToArray()));
    }
}
