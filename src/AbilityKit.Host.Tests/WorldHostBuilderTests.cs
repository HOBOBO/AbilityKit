using AbilityKit.Ability.Host.Builder;
using AbilityKit.Ability.Host.Framework;
using Xunit;

namespace AbilityKit.Host.Tests;

/// <summary>
/// host 包 WorldHost 构建器的直接契约测试（脱离 demo）。
/// 覆盖构建器成功/失败/边界契约；不依赖真实 IWorldFactory 等运行时依赖。
/// </summary>
public sealed class WorldHostBuilderTests
{
    [Fact]
    public void Create_returns_non_null_builder()
    {
        Assert.NotNull(WorldHostBuilder.Create());
    }

    [Fact]
    public void SetWorldFactory_null_throws()
    {
        var b = WorldHostBuilder.Create();
        Assert.Throws<ArgumentNullException>(() => b.SetWorldFactory(null!));
    }

    [Fact]
    public void AddModule_null_is_tolerant_and_returns_builder()
    {
        var b = WorldHostBuilder.Create();
        // AddModule 对 null 静默容忍（不抛、不加入）并返回构建器以支持链式。
        Assert.Same(b, b.AddModule(null!));
    }

    [Fact]
    public void AddModules_null_is_tolerant_and_returns_builder()
    {
        var b = WorldHostBuilder.Create();
        Assert.Same(b, b.AddModules(null!));
        // 传入含 null 元素的集合也应容忍。
        Assert.Same(b, b.AddModules(new IHostRuntimeModule[] { null! }));
    }
}
