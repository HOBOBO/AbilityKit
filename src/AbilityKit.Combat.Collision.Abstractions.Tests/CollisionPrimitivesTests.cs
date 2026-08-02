using AbilityKit.Core.Mathematics;
using Xunit;

namespace AbilityKit.Combat.Collision.Abstractions.Tests;

/// <summary>
/// 碰撞基础原语（Aabb / Obb / Sphere / Capsule / ColliderShape）的直接测试。
/// 这些类型是 collision/motion/navigation 的基础。
/// </summary>
public sealed class CollisionPrimitivesTests
{
    // ---------- Aabb ----------

    [Fact]
    public void Aabb_Center_and_Contains()
    {
        var a = new Aabb(new Vec3(0f, 0f, 0f), new Vec3(2f, 2f, 2f));
        Assert.True(a.Contains(new Vec3(1f, 1f, 1f)));
        Assert.False(a.Contains(new Vec3(3f, 0f, 0f)));
    }

    [Fact]
    public void Aabb_Intersects_overlapping()
    {
        var a = new Aabb(new Vec3(0f, 0f, 0f), new Vec3(2f, 2f, 2f));
        var b = new Aabb(new Vec3(1f, 1f, 1f), new Vec3(3f, 3f, 3f));
        Assert.True(a.Intersects(b));
    }

    [Fact]
    public void Aabb_Intersects_separated()
    {
        var a = new Aabb(new Vec3(0f, 0f, 0f), new Vec3(1f, 1f, 1f));
        var b = new Aabb(new Vec3(5f, 5f, 5f), new Vec3(6f, 6f, 6f));
        Assert.False(a.Intersects(b));
    }

    // ---------- Obb ----------

    [Fact]
    public void Obb_identity_axes_are_world()
    {
        var o = new Obb(Vec3.Zero, Quat.Identity, new Vec3(1f, 2f, 3f));
        o.GetAxes(out var r, out var u, out var f);
        Assert.Equal(Vec3.Right, r);
        Assert.Equal(Vec3.Up, u);
        Assert.Equal(Vec3.Forward, f);
    }

    // ---------- Sphere ----------

    [Fact]
    public void Sphere_constructor_radius_clamped()
    {
        var s = new Sphere(Vec3.Zero, -5f);
        Assert.Equal(0f, s.Radius);  // 负数半径被钳到 0
    }

    // ---------- ColliderShape ----------

    [Fact]
    public void ColliderShape_CreateSphere_roundtrips()
    {
        var shape = ColliderShape.CreateSphere(new Vec3(1f, 2f, 3f), 0.5f);
        Assert.Equal(ColliderShapeType.Sphere, shape.Type);
        Assert.Equal(new Vec3(1f, 2f, 3f), shape.Sphere.Center);
        Assert.Equal(0.5f, shape.Sphere.Radius);
    }

    [Fact]
    public void ColliderShape_CreateAabb_roundtrips()
    {
        var shape = ColliderShape.CreateAabb(new Vec3(0f, 0f, 0f), new Vec3(1f, 1f, 1f));
        Assert.Equal(ColliderShapeType.Aabb, shape.Type);
        Assert.Equal(new Vec3(0f, 0f, 0f), shape.Aabb.Min);
        Assert.Equal(new Vec3(1f, 1f, 1f), shape.Aabb.Max);
    }
}
