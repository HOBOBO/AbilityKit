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
    public void All_supported_versions_decode_with_explicit_optional_frame(int version)
    {
        using var stream=new MemoryStream();using var writer=new BinaryWriter(stream);
        writer.Write(version);writer.Write(1);writer.Write(7);writer.Write(1);writer.Write(20);
        writer.Write("life/alive/action/casting");writer.Write(8L);writer.Write(10);writer.Write(10);
        writer.Write(42L);writer.Write(123);
        if(version>=2){writer.Write("normal");writer.Write(0L);writer.Write(0);writer.Write(20);}
        if(version>=3){writer.Write(true);foreach(var f in new[]{8f,2f,9f,1f,0f,0f})writer.Write(f);}
        var payload=stream.ToArray();
        Assert.True(CharacterActionSnapshotCodec.TryDecode(payload,out var entries));
        Assert.Equal(version==3,Assert.Single(entries).CastFrame.HasValue);
        var encoded=CharacterActionSnapshotCodec.Encode(entries);
        Assert.True(CharacterActionSnapshotCodec.TryDecode(encoded,out var restored));
        Assert.Equal(entries[0].CastFrame,restored[0].CastFrame);
        Assert.False(CharacterActionSnapshotCodec.TryDecode(payload[..^1],out _));
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
