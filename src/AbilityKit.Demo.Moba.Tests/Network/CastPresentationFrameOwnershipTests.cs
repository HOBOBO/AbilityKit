using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Services;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Network;

public sealed class CastPresentationFrameOwnershipTests
{
    [Theory]
    [InlineData(91,7,10001)]
    [InlineData(817,63,60089)]
    public void Explicit_frame_is_immutable_and_bound_to_exact_original_cast(int skill,int actor,long context)
    {
        using var service=new MobaSkillCastRuntimeService();
        var cast=service.Create(new MobaSkillCastRuntimeCreateRequest(skill,2,1,1,actor,0,Vec3.Zero,Vec3.Right,context));
        Assert.False(service.TryGetPresentationFrame(context,actor,out _));
        Assert.False(service.TryCapturePresentationFrame(cast.Handle,actor+1,new Vec3(3,2,4),Vec3.Right));
        var stale=new MobaSkillCastRuntimeHandle(cast.RuntimeId,cast.Generation+1,context);
        Assert.False(service.TryCapturePresentationFrame(stale,actor,new Vec3(3,2,4),Vec3.Right));
        Assert.False(service.TryCapturePresentationFrame(cast.Handle,actor,new Vec3(float.NaN,0,0),Vec3.Right));
        Assert.True(service.TryCapturePresentationFrame(cast.Handle,actor,new Vec3(3,2,4),Vec3.Right));
        Assert.True(service.TryGetPresentationFrame(context,actor,out var original));
        cast.UpdateInput(Vec3.Zero,Vec3.Forward,0);
        Assert.True(service.TryCapturePresentationFrame(cast.Handle,actor,new Vec3(8,2,9),Vec3.Forward));
        Assert.True(service.TryGetPresentationFrame(context,actor,out var unchanged));Assert.Equal(original,unchanged);
        Assert.False(service.TryGetPresentationFrame(context,actor+1,out _));
        Assert.False(service.TryGetPresentationFrame(context+1,actor,out _));
    }
}
