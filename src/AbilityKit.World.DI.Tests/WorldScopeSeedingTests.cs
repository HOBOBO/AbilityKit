using System;
using AbilityKit.Ability.World.DI;
using Xunit;

namespace AbilityKit.World.DI.Tests;

/// <summary>
/// 播种机制（CreateScope(Action&lt;IWorldScopeSeeder&gt;)）的引擎层回归。
///
/// 语义见 MobaFlowSpec.md「播种机制」：
/// - 播种=向新 scope 注入跨阶段外部输入（如 per-battle 的 bootstrapper/gateway）。
/// - 播种实例优先命中（覆盖容器内同类型的 scoped 工厂）。
/// - 播种但未在容器注册的类型也能解析。
/// - 播种实例的生命周期归调用方：scope.Dispose() 不接管其释放。
/// </summary>
public sealed class WorldScopeSeedingTests
{
    [Fact]
    public void Scope_resolves_itself_through_the_IWorldScope_contract()
    {
        using var container = new WorldContainerBuilder().Build();
        using var scope = container.CreateScope();

        var resolved = scope.Resolve<IWorldScope>();
        Assert.Same(scope, resolved);
        Assert.Same(container, resolved.Root);
        Assert.True(scope.TryResolve<IWorldScope>(out var optional));
        Assert.Same(resolved, optional);
        Assert.Same(resolved, ((IServiceProvider)scope).GetService(typeof(IWorldScope)));
    }

    [Fact]
    public void SeededInstance_IsResolved_FromScope()
    {
        var builder = new WorldContainerBuilder();
        builder.RegisterType<IGateway, RegisteredGateway>(WorldLifetime.Scoped);
        using var container = builder.Build();

        var seeded = new SeededGateway();
        using var scope = container.CreateScope(s => s.Seed<IGateway>(seeded));

        Assert.Same(seeded, scope.Resolve<IGateway>());
    }

    [Fact]
    public void SeededInstance_OverridesRegisteredFactory()
    {
        var builder = new WorldContainerBuilder();
        builder.RegisterType<IGateway, RegisteredGateway>(WorldLifetime.Scoped);
        using var container = builder.Build();

        // 不播种时走容器工厂。
        using (var plain = container.CreateScope())
        {
            Assert.IsType<RegisteredGateway>(plain.Resolve<IGateway>());
        }

        // 播种后覆盖工厂。
        var seeded = new SeededGateway();
        using (var seededScope = container.CreateScope(s => s.Seed<IGateway>(seeded)))
        {
            Assert.Same(seeded, seededScope.Resolve<IGateway>());
        }
    }

    [Fact]
    public void SeededInstance_IsNotDisposed_WhenScopeDisposed()
    {
        var builder = new WorldContainerBuilder();
        using var container = builder.Build();

        var seeded = new DisposableProbe();
        var scope = container.CreateScope(s => s.Seed<DisposableProbe>(seeded));
        Assert.Same(seeded, scope.Resolve<DisposableProbe>());

        scope.Dispose();

        // 生命周期归调用方：scope 释放不连带释放播种实例。
        Assert.False(seeded.Disposed);
    }

    [Fact]
    public void SeededInstance_OfUnregisteredType_IsResolvable()
    {
        var builder = new WorldContainerBuilder();
        using var container = builder.Build();

        var seeded = new SeededGateway();
        using var scope = container.CreateScope(s => s.Seed<IGateway>(seeded));

        // IGateway 未在容器注册，但播种后仍可解析。
        Assert.Same(seeded, scope.Resolve<IGateway>());
    }

    [Fact]
    public void Seed_WithIncompatibleInstance_Throws()
    {
        var builder = new WorldContainerBuilder();
        using var container = builder.Build();

        Assert.Throws<ArgumentException>(() =>
            container.CreateScope(s => s.Seed(typeof(IGateway), new DisposableProbe())));
    }

    [Fact]
    public void TryResolve_SeededUnregisteredType_ReturnsTrue()
    {
        var builder = new WorldContainerBuilder();
        using var container = builder.Build();

        var seeded = new SeededGateway();
        using var scope = container.CreateScope(s => s.Seed<IGateway>(seeded));

        // IGateway 未在容器注册，但已播种：TryResolve 应命中（不被 IsRegistered 短路）。
        Assert.True(scope.TryResolve(typeof(IGateway), out var resolved));
        Assert.Same(seeded, resolved);

        // 未播种类型仍返回 false。
        Assert.False(scope.TryResolve(typeof(DisposableProbe), out _));
    }

    private interface IGateway
    {
    }

    private sealed class RegisteredGateway : IGateway
    {
    }

    private sealed class SeededGateway : IGateway
    {
    }

    private sealed class DisposableProbe : IDisposable
    {
        public bool Disposed { get; private set; }

        public void Dispose()
        {
            Disposed = true;
        }
    }
}
