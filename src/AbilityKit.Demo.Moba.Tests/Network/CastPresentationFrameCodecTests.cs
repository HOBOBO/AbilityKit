using System.IO;
using AbilityKit.Demo.Moba.Share;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Network;

public sealed class CastPresentationFrameCodecTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void All_supported_versions_decode_with_explicit_optional_frame(int version)
    {
        using var stream=new MemoryStream();using var writer=new BinaryWriter(stream);
        writer.Write(version);writer.Write(1);writer.Write(7);writer.Write(1);writer.Write(20);
        writer.Write("life/alive/action/casting");writer.Write(8L);writer.Write(10);writer.Write(10);
        writer.Write(42L);writer.Write(123);
        if(version>=2){writer.Write("normal");writer.Write(0L);writer.Write(0);writer.Write(20);}
        if(version>=3){writer.Write(true);foreach(var f in new[]{8f,2f,9f,1f,0f,0f})writer.Write(f);}
        if(version>=4){writer.Write(10);writer.Write(1550);}
        var payload=stream.ToArray();
        Assert.True(CharacterActionSnapshotCodec.TryDecode(payload,out var entries));
        Assert.Equal(version>=3,Assert.Single(entries).CastFrame.HasValue);
        var encoded=CharacterActionSnapshotCodec.Encode(entries);
        Assert.True(CharacterActionSnapshotCodec.TryDecode(encoded,out var restored));
        Assert.Equal(entries[0].CastFrame,restored[0].CastFrame);
        Assert.False(CharacterActionSnapshotCodec.TryDecode(payload[..^1],out _));
    }

    [Theory]
    [InlineData(600, 18)]
    [InlineData(500, 15)]
    [InlineData(400, 12)]
    public void Frozen_windup_round_trips_and_crosses_the_marker_on_the_launch_frame(int actualMs, int launchTick)
    {
        var windup = new CastWindupTiming(10, 500, actualMs);
        var unscaled = new CharacterActionData(1, 1, 12, "casting", 1, 10, 2, 4, 11);
        var scaled = new CharacterActionData(2, 1, 12, "casting", 1, 10, 2, 5, 12,
            "normal", 0, 0, 12, windupTiming: windup);
        Assert.Equal(4, System.BitConverter.ToInt32(CharacterActionSnapshotCodec.Encode(new[] { unscaled }), 0));
        var payload = CharacterActionSnapshotCodec.Encode(new[] { unscaled, scaled });
        Assert.Equal(5, System.BitConverter.ToInt32(payload, 0));
        Assert.True(CharacterActionSnapshotCodec.TryDecode(payload, out var decoded));
        Assert.False(decoded[0].WindupTiming.HasValue);
        var timing = decoded[1].WindupTiming;
        Assert.Equal(windup, timing);
        Assert.True(timing.ProjectAuthoredMilliseconds(10 + launchTick - 1, 1f / 30) < 500);
        Assert.Equal(500, timing.ProjectAuthoredMilliseconds(10 + launchTick, 1f / 30));
        Assert.Equal(1500, timing.ProjectAuthoredMilliseconds(10 + launchTick + 30, 1f / 30));
        Assert.False(CharacterActionSnapshotCodec.TryDecode(payload[..^1], out _));
    }

    [Theory]
    [InlineData(float.NaN,1,0)]
    [InlineData(float.PositiveInfinity,1,0)]
    [InlineData(1,0,0)]
    [InlineData(1,2,0)]
    public void Invalid_frame_is_rejected_by_encoder_and_decoder(float x,float dx,float dz)
    {
        var value=new CharacterActionData(1,1,10,"life/alive/action/casting",1,0,10,2,3,
            "normal",0,0,10,new CastPresentationFrame(x,0,0,dx,0,dz));
        Assert.Throws<System.InvalidOperationException>(()=>CharacterActionSnapshotCodec.Encode(new[]{value}));
        var valid=new CharacterActionData(1,1,10,"life/alive/action/casting",1,0,10,2,3,
            "normal",0,0,10,new CastPresentationFrame(0,0,0,1,0,0));
        var payload=CharacterActionSnapshotCodec.Encode(new[]{valid});
        using var stream=new MemoryStream(payload);using var writer=new BinaryWriter(stream);
        stream.Position=payload.Length-24;writer.Write(x);writer.Write(0f);writer.Write(0f);writer.Write(dx);writer.Write(0f);writer.Write(dz);
        Assert.False(CharacterActionSnapshotCodec.TryDecode(payload,out _));
    }
}
