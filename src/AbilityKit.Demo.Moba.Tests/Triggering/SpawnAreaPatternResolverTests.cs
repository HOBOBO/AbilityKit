using AbilityKit.Combat.Projectile;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Services.Triggering.PlanActions;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Triggering;

public sealed class SpawnAreaPatternResolverTests
{
    [Fact]
    public void PolygonEdges_ResolveSixDeterministicRectangleEdges()
    {
        var args = Create(SpawnAreaPatternKind.PolygonEdges, count: 6, apothem: 3f, edgeWidth: 0.4f);

        Assert.True(SpawnAreaPatternResolver.TryResolve(in args, 0, out var first, out var error), error);
        Assert.Equal(3f, first.Offset.X, 3);
        Assert.Equal(0f, first.Offset.Z, 3);
        Assert.Equal(OverlapShapeKind.Rectangle, first.ShapeKind);
        Assert.Equal(0.4f, first.Width, 3);
        Assert.Equal((float)(2d * 3d * System.Math.Tan(System.Math.PI / 6d)), first.Length, 3);

        Assert.True(SpawnAreaPatternResolver.TryResolve(in args, 3, out var opposite, out error), error);
        Assert.Equal(-3f, opposite.Offset.X, 3);
        Assert.Equal(0f, opposite.Offset.Z, 3);
        Assert.Equal(-first.Forward.X, opposite.Forward.X, 3);
        Assert.Equal(-first.Forward.Z, opposite.Forward.Z, 3);
    }

    [Fact]
    public void RadialPoints_RespectStartAndStepAngles()
    {
        var args = Create(SpawnAreaPatternKind.RadialPoints, count: 4, apothem: 2f, startAngle: 45f, angleStep: 90f);

        Assert.True(SpawnAreaPatternResolver.TryResolve(in args, 1, out var point, out var error), error);
        Assert.Equal(-System.MathF.Sqrt(2f), point.Offset.X, 3);
        Assert.Equal(System.MathF.Sqrt(2f), point.Offset.Z, 3);
        Assert.Equal(OverlapShapeKind.Circle, point.ShapeKind);
    }

    [Fact]
    public void PolygonEdges_RejectInvalidGeometry()
    {
        var args = Create(SpawnAreaPatternKind.PolygonEdges, count: 2, apothem: 3f, edgeWidth: 0.4f);

        Assert.False(SpawnAreaPatternResolver.TryResolve(in args, 0, out _, out var error));
        Assert.Contains("pattern_count >= 3", error);
    }

    private static SpawnAreaBatchArgs Create(
        SpawnAreaPatternKind kind,
        int count,
        float apothem,
        float edgeWidth = 0.5f,
        float startAngle = 0f,
        float angleStep = 0f)
    {
        return new SpawnAreaBatchArgs(
            1, 0, 0f, 0, 1000, 0, 0,
            0f, 0f, 0f, 0f, 0f,
            (int)OverlapShapeKind.Circle, 0f, 0f, 0f, 0f, false,
            1, true, count, apothem, startAngle, angleStep, edgeWidth, 0f,
            false, default, default, default, (int)kind);
    }
}
