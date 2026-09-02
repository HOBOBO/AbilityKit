using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Console;
using AbilityKit.Demo.Moba.Console.Battle.Config;
using AbilityKit.Demo.Moba.EnvironmentModel;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.EnvironmentModel;
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
