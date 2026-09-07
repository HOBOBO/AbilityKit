using System.Collections.Generic;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Console;
using AbilityKit.Demo.Moba.Console.Battle.Config;
using AbilityKit.Demo.Moba.EnvironmentModel;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.EnvironmentModel;
using AbilityKit.EnvironmentModel;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Smoke;

/// <summary>
/// 完整流程的纯 .NET 冒烟验证：环境 profile → resolve（含常用组展开）→ bind（生成真实 MOBA 实体）→ 校验实体已注册。
/// 跑在 console 逻辑世界上，不依赖 Unity。
/// </summary>
public sealed class MobaEnvironmentProfileBinderTests
{
    [Fact]
    public void Bind_JungleCamp_SpawnsMonstersAndReturnsHandles()
    {
        using var bootstrapper = Boot();

        var catalog = MobaEnvironmentProfileCatalog.CreateDefault();
        var expander = new MobaEnvironmentGroupExpander();
        Assert.True(catalog.TryResolve("jungle-camp", expander, out var resolved));

        var binder = new MobaEnvironmentProfileBinder(bootstrapper.RuntimeServices!);
        var result = binder.Bind(in resolved);

        // unit-class:jungle → 3 个 Monster（别名 jungle_0 / jungle_1 / jungle_2）
        Assert.True(result.TryGetHandle("jungle_0", out var actorId));
        Assert.True(actorId > 0);

        var lookup = bootstrapper.RuntimeServices!.Resolve<MobaActorLookupService>();
        Assert.True(lookup.TryGetActorEntity(actorId, out var entity));
        Assert.NotNull(entity);
    }

    [Fact]
    public void Bind_SpawnWithComponents_AppliesHpOverride()
    {
        using var bootstrapper = Boot();

        // 显式原语：生成一只 boss 怪并把血量压到 500（自由构建战斗世界的数值覆盖）。
        var catalog = new EnvironmentProfileCatalog();
        catalog.AddProfile(new EnvironmentProfile
        {
            Id = "custom-boss",
            Primitives = new EnvironmentPrimitive[]
            {
                new SpawnPrimitive
                {
                    EntityKind = "Monster",
                    Alias = "boss",
                    Count = 1,
                    Components = new Dictionary<string, string> { ["hp"] = "500" },
                },
            },
        });

        var expander = new MobaEnvironmentGroupExpander();
        Assert.True(catalog.TryResolve("custom-boss", expander, out var resolved));

        var binder = new MobaEnvironmentProfileBinder(bootstrapper.RuntimeServices!);
        var result = binder.Bind(in resolved);

        Assert.True(result.TryGetHandle("boss", out var actorId));
        Assert.True(actorId > 0);

        var lookup = bootstrapper.RuntimeServices!.Resolve<MobaActorLookupService>();
        Assert.True(lookup.TryGetActorEntity(actorId, out var entity));
        Assert.Equal(500f, new MobaAttrs(entity).Hp);
    }

    [Fact]
    public void PlaceObstacle_AddsColliderToWorld()
    {
        using var bootstrapper = Boot();
        var binder = new MobaEnvironmentProfileBinder(bootstrapper.RuntimeServices!);

        var id = binder.PlaceObstacle(new ObstaclePrimitive
        {
            Shape = "box",
            Position = new EnvironmentVector3(5, 0, 0),
            Size = new EnvironmentVector3(2, 2, 2),
        });

        Assert.True(id.Value > 0, "障碍物应返回有效碰撞体 id");
    }

    private static ConsoleBattleBootstrapper Boot()
    {
        var bootstrapper = new ConsoleBattleBootstrapper(BattleStartConfig.CreateDefault());
        bootstrapper.Initialize();
        bootstrapper.Start();
        for (var i = 0; i < 8 && bootstrapper.Context.EcsWorld == null; i++) bootstrapper.Tick();
        bootstrapper.SetupBattle();
        for (var i = 0; i < 10; i++) bootstrapper.Tick();
        return bootstrapper;
    }
}
