using System;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Services.Triggering.PlanActions;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Triggering;

public sealed class SpawnAreaOrientationTests
{
    [Theory]
    [InlineData(0, 0, 1)]
    [InlineData(90, 1, 0)]
    [InlineData(-90, -1, 0)]
    [InlineData(450, 1, 0)]
    public void ShapeYaw_RotatesLongAxisWithoutChangingMagnitude(float yaw, float x, float z)
    {
        var forward = Vec3.Forward;
        var actual = SpawnAreaPlanActionModule.ResolveShapeForward(in forward, yaw);
        Assert.Equal(x, actual.X, 4);
        Assert.Equal(z, actual.Z, 4);
        Assert.Equal(1f, actual.SqrMagnitude, 4);
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void ShapeYaw_RejectsNonFiniteInput(float yaw)
    {
        Assert.Throws<ArgumentException>(() => SpawnAreaPlanActionModule.ResolveShapeForward(Vec3.Forward, yaw));
    }
}
