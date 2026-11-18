using AbilityKit.Core.Mathematics;
using Xunit;

namespace AbilityKit.Core.Tests;

/*
 * 验证浮点数学原语当前的数值边界与组合顺序。
 * 一般逆变换的反例用于防止调用方误用兼容属性，不代表新增精确逆矩阵能力。
 */
public sealed class MathContractTests
{
    [Fact]
    public void AbsoluteToleranceIncludesBoundaryAndRejectsInvalidComparisons()
    {
        Assert.True(MathUtil.IsZero(-MathUtil.Epsilon));
        Assert.False(MathUtil.IsZero(MathF.BitIncrement(MathUtil.Epsilon)));
        Assert.False(MathUtil.IsZero(0f, -1f));
        Assert.True(MathUtil.Approximately(0f, MathUtil.Epsilon));
        Assert.False(MathUtil.Approximately(float.PositiveInfinity, float.PositiveInfinity));
        Assert.False(MathUtil.Approximately(float.NaN, 0f));
    }

    [Fact]
    public void ScalarNaNAndSignedZeroFollowComparisonSelectionRules()
    {
        Assert.Equal(0, MathUtil.Sign(float.NaN));
        Assert.True(float.IsNaN(MathUtil.Clamp01(float.NaN)));
        Assert.Equal(2f, MathUtil.Min(float.NaN, 2f));
        Assert.Equal(2f, MathUtil.Max(float.NaN, 2f));
        Assert.True(float.IsNaN(MathUtil.Min(2f, float.NaN)));
        Assert.True(float.IsNaN(MathUtil.Max(2f, float.NaN)));
        var negativeZero = BitConverter.Int32BitsToSingle(int.MinValue);
        Assert.Equal(int.MinValue, BitConverter.SingleToInt32Bits(MathUtil.Abs(negativeZero)));
        Assert.Equal(0, BitConverter.SingleToInt32Bits(MathUtil.Min(negativeZero, 0f)));
        Assert.Equal(int.MinValue, BitConverter.SingleToInt32Bits(MathUtil.Max(0f, negativeZero)));
    }

    [Fact]
    public void ReversedClampBoundsAreNotValidatedAndCompareLowerFirst()
    {
        Assert.Equal(10f, MathUtil.Clamp(5f, 10f, 0f));
        Assert.Equal(0f, MathUtil.Clamp(20f, 10f, 0f));
        Assert.Equal(10, MathUtil.Clamp(5, 10, 0));
        Assert.Equal(0, MathUtil.Clamp(20, 10, 0));
    }

    [Theory]
    [InlineData(-2f, 0f)]
    [InlineData(0f, 0f)]
    [InlineData(0.25f, 2f)]
    [InlineData(1f, 8f)]
    [InlineData(2f, 8f)]
    public void ScalarAndVectorLerpClampTheSameFiniteFactor(float factor, float expected)
    {
        Assert.Equal(expected, MathUtil.Lerp(0f, 8f, factor));
        Assert.Equal(new Vec3(expected, expected * 2f, -expected),
            Vec3.Lerp(Vec3.Zero, new Vec3(8f, 16f, -8f), factor));
    }

    [Fact]
    public void LerpDoesNotBypassNonFiniteArithmeticAtEndpoints()
    {
        Assert.True(float.IsNaN(MathUtil.Lerp(float.MaxValue, -float.MaxValue, 0f)));
        Assert.True(float.IsNaN(Vec3.Lerp(
            new Vec3(float.MaxValue, 0f, 0f),
            new Vec3(-float.MaxValue, 0f, 0f), 0f).X));
        Assert.Equal(0f, MathUtil.Saturate(float.NegativeInfinity));
        Assert.Equal(1f, MathUtil.Saturate(float.PositiveInfinity));
    }

    [Fact]
    public void SquareRootRetainsFixedPointResolutionAndFallbackBoundaries()
    {
        Assert.Equal(0f, MathUtil.Sqrt(MathF.Pow(2f, -34f)));
        Assert.Equal(MathF.Pow(2f, -16f), MathUtil.Sqrt(MathF.Pow(2f, -32f)));
        Assert.Equal(0.5f, MathUtil.Sqrt(0.25f));
        Assert.Equal(2f, MathUtil.Sqrt(4f));
        Assert.Equal((float)Math.Sqrt(2e9f), MathUtil.Sqrt(2e9f));
        Assert.Equal((float)Math.Sqrt(float.MaxValue), MathUtil.Sqrt(float.MaxValue));
    }

    [Fact]
    public void SquareRootPreservesSpecialInputResults()
    {
        Assert.True(float.IsNaN(MathUtil.Sqrt(-1f)));
        Assert.True(float.IsNaN(MathUtil.Sqrt(float.NaN)));
        Assert.Equal(float.PositiveInfinity, MathUtil.Sqrt(float.PositiveInfinity));
        Assert.True(float.IsNaN(MathUtil.Sqrt(float.NegativeInfinity)));
        Assert.Equal(0, BitConverter.SingleToInt32Bits(
            MathUtil.Sqrt(BitConverter.Int32BitsToSingle(int.MinValue))));
    }

    [Fact]
    public void ZeroAndVerySmallVectorNormalizationReturnZero()
    {
        Assert.Equal(Vec2.Zero, Vec2.Zero.Normalized);
        Assert.Equal(Vec3.Zero, Vec3.Zero.Normalized);
        Assert.Equal(Vec2.Zero, new Vec2(1e-7f, 0f).Normalized);
        Assert.Equal(Vec3.Zero, new Vec3(1e-7f, 0f, 0f).Normalized);
        var pair = new Vec2(3f, 4f);
        Assert.Equal(5f, pair.Magnitude);
        Assert.Equal(0.6f, pair.Normalized.X, 6);
        Assert.Equal(0.8f, pair.Normalized.Y, 6);
    }

    [Fact]
    public void VectorNormalizationAndDivisionAreNotFiniteInputValidation()
    {
        Assert.Equal(Vec2.Zero, new Vec2(float.NaN, 1f).Normalized);
        Assert.Equal(Vec3.Zero, new Vec3(float.NaN, 1f, 0f).Normalized);
        Assert.Equal(Vec3.Zero, new Vec3(float.MaxValue, 1f, 0f).Normalized);
        Assert.True(float.IsNaN(new Vec3(float.PositiveInfinity, 0f, 0f).Normalized.X));
        var divided = new Vec2(1f, 0f) / 0f;
        Assert.Equal(float.PositiveInfinity, divided.X);
        Assert.True(float.IsNaN(divided.Y));
    }

    [Fact]
    public void BasisCrossProductAndDistanceUseDocumentedOrientation()
    {
        Assert.Equal(Vec3.Forward, Vec3.Cross(Vec3.Right, Vec3.Up));
        Assert.Equal(Vec3.Back, Vec3.Cross(Vec3.Up, Vec3.Right));
        Assert.Equal(0f, Vec3.Dot(Vec3.Right, Vec3.Up));
        Assert.Equal(5f, Vec3.Distance(new Vec3(1f, 2f, 0f), new Vec3(4f, 6f, 0f)));
    }

    [Fact]
    public void ValueEqualityAndNumericsConversionDoNotUseApproximateGeometry()
    {
        var nan = new Vec3(float.NaN, 0f, -0f);
        Assert.True(nan.Equals(new Vec3(float.NaN, -0f, 0f)));
        Assert.Equal(nan, Vec3.FromNumerics(nan.ToNumerics()));
        Assert.False(new Vec2(1f, 0f).Equals(new Vec2(MathF.BitIncrement(1f), 0f)));
        var q = new Quat(0f, 0f, 2f, 2f);
        var opposite = new Quat(0f, 0f, -2f, -2f);
        Assert.False(q.Equals(opposite));
        Near(q.Rotate(Vec3.Right), opposite.Rotate(Vec3.Right));
        Assert.Equal(q, Quat.FromNumerics(q.ToNumerics()));
    }

    [Fact]
    public void AxisAngleUsesRadiansAndNormalizesNonzeroAxis()
    {
        var rotation = Quat.FromAxisAngle(new Vec3(0f, 5f, 0f), MathF.PI / 2f);
        Near(Vec3.Back, rotation.Rotate(Vec3.Right));
        Near(Vec3.Right, rotation.Rotate(Vec3.Forward));
    }

    [Fact]
    public void QuaternionMultiplicationAppliesRightOperandFirst()
    {
        var yaw = Quat.FromAxisAngle(Vec3.Up, MathF.PI / 2f);
        var pitch = Quat.FromAxisAngle(Vec3.Right, MathF.PI / 2f);
        Near(Vec3.Down, (yaw * pitch).Rotate(Vec3.Forward));
        Near(Vec3.Right, (pitch * yaw).Rotate(Vec3.Forward));
    }

    [Fact]
    public void NonUnitQuaternionUsesItsActualInverse()
    {
        var rotation = new Quat(0f, 0f, 2f, 2f);
        Assert.Equal(new Quat(0f, 0f, -0.25f, 0.25f), rotation.Inverse);
        Assert.False(rotation.Inverse.Equals(rotation.Conjugate));
        Near(Vec3.Up, rotation.Rotate(Vec3.Right));
        var value = new Vec3(2f, 3f, 4f);
        Near(value, rotation.Inverse.Rotate(rotation.Rotate(value)));
    }

    [Fact]
    public void ZeroAndNearZeroQuaternionHaveExplicitFallbacks()
    {
        Assert.Equal(Quat.Identity, default(Quat).Normalized);
        Assert.Equal(Quat.Identity, default(Quat).Inverse);
        Assert.Equal(Vec3.Zero, default(Quat).Rotate(Vec3.Forward));
        var tiny = new Quat(0.0005f, 0f, 0f, 0f);
        Assert.Equal(Quat.Identity, tiny.Normalized);
        Assert.Equal(Quat.Identity, tiny.Inverse);
        Assert.False(new Quat(0.002f, 0f, 0f, 0f).Inverse.Equals(Quat.Identity));
    }

    [Fact]
    public void ZeroAxisDoesNotConstructAnIdentityQuaternion()
    {
        var rotation = Quat.FromAxisAngle(Vec3.Zero, MathF.PI / 2f);
        Assert.Equal(0f, rotation.X);
        Assert.Equal(0f, rotation.Y);
        Assert.Equal(0f, rotation.Z);
        Assert.InRange(rotation.W, 0.70f, 0.71f);
        Assert.False(rotation.Equals(Quat.Identity));
    }

    [Theory]
    [InlineData(1f, 1f)]
    [InlineData(-1f, 1f)]
    [InlineData(-1f, -1f)]
    [InlineData(1f, -1f)]
    public void LookRotationMapsBasisForAllMatrixConversionBranches(float forwardZ, float upY)
    {
        var forward = new Vec3(0f, 0f, forwardZ);
        var up = new Vec3(0f, upY, 0f);
        var rotation = Quat.LookRotation(forward, up);
        Near(forward, rotation.Rotate(Vec3.Forward));
        Near(up, rotation.Rotate(Vec3.Up));
    }

    [Fact]
    public void LookRotationProjectsObliqueUpToAnOrthogonalBasis()
    {
        var forward = new Vec3(1f, 2f, 3f);
        var rotation = Quat.LookRotation(forward, Vec3.Up);
        Near(forward.Normalized, rotation.Rotate(Vec3.Forward));
        Assert.InRange(MathF.Abs(Vec3.Dot(rotation.Rotate(Vec3.Up), forward.Normalized)),
            0f, 0.0001f);
    }

    [Fact]
    public void LookRotationUsesTheExistingWorldUpFallback()
    {
        Assert.Equal(Quat.Identity, Quat.LookRotation(Vec3.Zero, Vec3.Up));
        Assert.Equal(Quat.Identity, Quat.LookRotation(Vec3.Up, Vec3.Up));
        var rotation = Quat.LookRotation(Vec3.Right, Vec3.Right);
        Near(Vec3.Right, rotation.Rotate(Vec3.Forward));
        Near(Vec3.Up, rotation.Rotate(Vec3.Up));
    }

    [Fact]
    public void TransformAppliesScaleThenRotationThenTranslationAndDirectInverse()
    {
        var transform = new Transform3(new Vec3(10f, 20f, 30f),
            Quat.FromAxisAngle(Vec3.Up, MathF.PI / 2f), new Vec3(2f, -3f, 4f));
        var local = new Vec3(1f, 2f, 3f);
        Near(new Vec3(22f, 14f, 28f), transform.TransformPoint(local));
        Near(local, transform.InverseTransformPoint(transform.TransformPoint(local)));
        Near(local, transform.InverseTransformDirection(transform.TransformDirection(local)));
    }

    [Fact]
    public void TransformDirectionsAndBasisIncludeScaleButExcludeTranslation()
    {
        var transform = new Transform3(new Vec3(10f, 20f, 30f),
            Quat.FromAxisAngle(Vec3.Up, MathF.PI / 2f), new Vec3(2f, -3f, 4f));
        Near(new Vec3(4f, 0f, 0f), transform.Forward);
        Near(new Vec3(0f, 0f, -2f), transform.Right);
        Near(new Vec3(0f, -3f, 0f), transform.Up);
        Near(transform.TransformPoint(Vec3.One) - transform.Position,
            transform.TransformDirection(Vec3.One));
    }

    [Fact]
    public void InverseScaleIncludesThresholdAndPreservesLargerNegativeScale()
    {
        var singular = new Transform3(Vec3.Zero, Quat.Identity,
            new Vec3(0f, MathUtil.Epsilon, -MathUtil.Epsilon));
        Assert.Equal(Vec3.Zero, singular.InverseTransformPoint(Vec3.One));
        Assert.Equal(Vec3.Zero, singular.InverseTransformDirection(Vec3.One));
        var usable = new Transform3(Vec3.Zero, Quat.Identity,
            new Vec3(MathUtil.Epsilon * 2f, -MathUtil.Epsilon * 2f, 1f));
        Near(new Vec3(500000f, -500000f, 1f), usable.InverseTransformPoint(Vec3.One));
    }

    [Fact]
    public void InverseTransformValueWorksForUniformScale()
    {
        var transform = new Transform3(new Vec3(2f, 4f, 6f),
            Quat.FromAxisAngle(Vec3.Up, MathF.PI / 2f), new Vec3(2f, 2f, 2f));
        var point = new Vec3(3f, 4f, 5f);
        Near(point, transform.Inverse.TransformPoint(transform.TransformPoint(point)));
    }

    [Fact]
    public void NonUniformScaleInverseValueDoesNotReplaceDirectInverse()
    {
        var transform = new Transform3(Vec3.Zero,
            Quat.FromAxisAngle(Vec3.Forward, MathF.PI / 2f), new Vec3(2f, 4f, 1f));
        var world = transform.TransformPoint(Vec3.Right);
        Near(Vec3.Right, transform.InverseTransformPoint(world));
        Near(new Vec3(0.5f, 0f, 0f), transform.Inverse.TransformPoint(world));
    }

    [Fact]
    public void DefaultTransformAndRayDoNotBecomeIdentityValues()
    {
        Assert.False(default(Transform3).Equals(Transform3.Identity));
        Assert.Equal(Vec3.Zero, default(Transform3).TransformPoint(Vec3.One));
        Assert.Equal(Vec3.One, Transform3.Identity.TransformPoint(Vec3.One));
        Assert.Equal(Vec3.Zero, default(Ray3).GetPoint(5f));
    }

    [Fact]
    public void RayParameterUsesRawDirectionAndAllowsNegativeValues()
    {
        var ray = new Ray3(new Vec3(1f, 2f, 3f), new Vec3(2f, 0f, 0f));
        Assert.Equal(new Vec3(-3f, 2f, 3f), ray.GetPoint(-2f));
        Assert.Equal(new Vec3(7f, 2f, 3f), ray.GetPoint(3f));
        Assert.Equal(ray.Origin, new Ray3(ray.Origin, Vec3.Zero).GetPoint(5f));
    }

    private static void Near(Vec3 expected, Vec3 actual)
    {
        Assert.InRange(MathF.Abs(expected.X - actual.X), 0f, 0.0001f);
        Assert.InRange(MathF.Abs(expected.Y - actual.Y), 0f, 0.0001f);
        Assert.InRange(MathF.Abs(expected.Z - actual.Z), 0f, 0.0001f);
    }
}

