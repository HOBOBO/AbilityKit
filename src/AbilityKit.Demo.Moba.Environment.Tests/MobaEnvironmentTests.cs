using AbilityKit.Demo.Moba.EnvironmentModel;
using AbilityKit.EnvironmentModel;
using Xunit;

namespace AbilityKit.Demo.Moba.Environment.Tests;

/// <summary>
/// MOBA 环境配置层的纯 .NET 测试：验证「项目给分类」的 taxonomy、常用组→原语展开、以及展开后并入解析结果。
/// 不依赖 Unity/Entitas，binder（接 MobaActorSpawnService）是下一个切片。
/// </summary>
public sealed class MobaEnvironmentTests
{
    [Fact]
    public void DefaultCatalog_RegistersConcernsAndStarterProfiles()
    {
        var catalog = MobaEnvironmentProfileCatalog.CreateDefault();

        Assert.True(catalog.TryGetConcern(MobaEnvironmentConcerns.UnitClass, out var unitClass));
        Assert.Contains("jungle", unitClass.Values);
        Assert.True(catalog.TryResolve("jungle-camp", out var jungle));
        Assert.Equal("jungle", jungle.Selections[MobaEnvironmentConcerns.UnitClass]);
    }

    [Fact]
    public void Expander_MapsUnitClassToSpawnPrimitives()
    {
        var expander = new MobaEnvironmentGroupExpander();
        Assert.True(expander.TryExpand(MobaEnvironmentConcerns.UnitClass, "jungle", out var primitives));

        Assert.Single(primitives);
        Assert.IsType<SpawnPrimitive>(primitives[0]);
        var spawn = (SpawnPrimitive)primitives[0];
        Assert.Equal("Monster", spawn.EntityKind);
        Assert.Equal(3, spawn.Count);
    }

    [Fact]
    public void Expander_MapsGeometryToObstaclePrimitives()
    {
        var expander = new MobaEnvironmentGroupExpander();
        Assert.True(expander.TryExpand(MobaEnvironmentConcerns.Geometry, "walled", out var primitives));

        Assert.Single(primitives);
        Assert.IsType<ObstaclePrimitive>(primitives[0]);
    }

    [Fact]
    public void Expander_TargetConcernsReturnFalse()
    {
        var expander = new MobaEnvironmentGroupExpander();
        Assert.False(expander.TryExpand(MobaEnvironmentConcerns.TargetShape, "group", out _));
        Assert.False(expander.TryExpand(MobaEnvironmentConcerns.State, "full", out _));
    }

    [Fact]
    public void Resolve_WithExpander_ExpandsSelectionToPrimitives()
    {
        var catalog = MobaEnvironmentProfileCatalog.CreateDefault();
        var expander = new MobaEnvironmentGroupExpander();

        Assert.True(catalog.TryResolve("jungle-camp", expander, out var resolved));
        Assert.Contains(resolved.Primitives, p => p is SpawnPrimitive s && s.EntityKind == "Monster" && s.Count == 3);
    }
}
