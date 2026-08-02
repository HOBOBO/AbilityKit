using AbilityKit.Core.Mathematics;
using Xunit;

namespace AbilityKit.Core.Tests;

/// <summary>
/// core 包公共数学原语（MathUtil/Vec3/Quat/Transform3）的直接契约测试。
/// 这是 core 包脱离 demo 的首批单测，覆盖成功 + 边界用例。
/// （Aabb/Obb 属于 com.abilitykit.combat.collision.abstractions，不在此测试。）
/// </summary>
public sealed class MathPrimitivesTests
{
    // ---------- MathUtil ----------

    [Fact]
    public void MathUtil_Clamp_clamps_to_range()
    {
        Assert.Equal(5f, MathUtil.Clamp(5f, 0f, 10f));
        Assert.Equal(0f, MathUtil.Clamp(-1f, 0f, 10f));
        Assert.Equal(10f, MathUtil.Clamp(11f, 0f, 10f));
    }

    [Fact]
    public void MathUtil_Clamp01_saturates()
    {
        Assert.Equal(0f, MathUtil.Clamp01(-0.5f));
        Assert.Equal(1f, MathUtil.Clamp01(2f));
        Assert.Equal(0.3f, MathUtil.Clamp01(0.3f));
    }

    [Fact]
    public void MathUtil_Lerp_interpolates_and_clamps_t()
    {
        Assert.Equal(0f, MathUtil.Lerp(0f, 10f, 0f));
        Assert.Equal(10f, MathUtil.Lerp(0f, 10f, 1f));
        Assert.Equal(5f, MathUtil.Lerp(0f, 10f, 0.5f));
        // t 被钳到 [0,1]
        Assert.Equal(10f, MathUtil.Lerp(0f, 10f, 2f));
    }

    [Fact]
    public void MathUtil_Approximately_and_IsZero_respect_epsilon()
    {
        Assert.True(MathUtil.Approximately(1f, 1f + MathUtil.Epsilon * 0.5f));
        Assert.False(MathUtil.Approximately(1f, 1.01f));
        Assert.True(MathUtil.IsZero(0f));
        Assert.False(MathUtil.IsZero(0.01f));
    }

    [Fact]
    public void MathUtil_Sign_and_Abs()
    {
        Assert.Equal(1, MathUtil.Sign(3f));
        Assert.Equal(-1, MathUtil.Sign(-2f));
        Assert.Equal(0, MathUtil.Sign(0f));
        Assert.Equal(2.5f, MathUtil.Abs(-2.5f));
    }

    // ---------- Vec3 ----------

    [Fact]
    public void Vec3_arithmetic_and_length()
    {
        var a = new Vec3(1f, 2f, 2f);
        Assert.Equal(9f, a.SqrMagnitude);
        Assert.Equal(3f, a.Magnitude);

        var b = new Vec3(0f, 0f, 1f);
        Assert.Equal(new Vec3(1f, 2f, 3f), a + b);
        Assert.Equal(new Vec3(2f, 4f, 4f), a * 2f);
    }

    [Fact]
    public void Vec3_Normalized_is_unit()
    {
        var n = new Vec3(0f, 5f, 0f).Normalized;
        Assert.Equal(1f, n.Magnitude, 5);
        Assert.Equal(new Vec3(0f, 1f, 0f), n);
    }

    [Fact]
    public void Vec3_Dot_and_Min_Max()
    {
        Assert.Equal(1f, Vec3.Dot(new Vec3(1f, 2f, 3f), Vec3.Right));
        Assert.Equal(new Vec3(-1f, 0f, 9f), Vec3.Min(new Vec3(-1f, 5f, 9f), new Vec3(2f, 0f, 10f)));
        Assert.Equal(new Vec3(2f, 5f, 10f), Vec3.Max(new Vec3(-1f, 5f, 9f), new Vec3(2f, 0f, 10f)));
    }

    // ---------- Quat ----------

    [Fact]
    public void Quat_Identity_rotates_unchanged()
    {
        var v = new Vec3(1f, 2f, 3f);
        var r = Quat.Identity.Rotate(v);
        Assert.Equal(v.X, r.X, 5);
        Assert.Equal(v.Y, r.Y, 5);
        Assert.Equal(v.Z, r.Z, 5);
    }

    [Fact]
    public void Quat_FromAxisAngle_yaws_around_Y()
    {
        // 绕 Y 轴转 90°：+X 落到 XZ 平面、模长不变。
        var q = Quat.FromAxisAngle(Vec3.Up, MathF.PI / 2f);
        var r = q.Rotate(Vec3.Right);
        Assert.Equal(1f, r.Magnitude, 5);
        Assert.Equal(0f, r.Y, 5);
        Assert.True(Math.Abs(r.X) < 1e-3f || Math.Abs(r.Z) < 1e-3f);
    }

    // ---------- Transform3 ----------

    [Fact]
    public void Transform3_TransformPoint_identity_is_translate()
    {
        // 恒等旋转 + 单位缩放：TransformPoint(local) = position + local。
        var t = new Transform3(new Vec3(1f, 2f, 3f), Quat.Identity, Vec3.One);
        var p = t.TransformPoint(new Vec3(1f, 0f, 0f));
        Assert.Equal(new Vec3(2f, 2f, 3f), p);
    }
}
