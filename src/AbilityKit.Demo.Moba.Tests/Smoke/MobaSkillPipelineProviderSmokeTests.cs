using System.Linq;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Demo.Moba.Console;
using AbilityKit.Demo.Moba.Console.Battle.Config;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Demo.Moba.Services;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Smoke;

public sealed class MobaSkillPipelineProviderSmokeTests
{
    [Fact]
    public void Active_cast_roundtrips_through_runtime_and_runner_providers()
    {
        using var battle = new ConsoleBattleBootstrapper(BattleStartConfig.CreateDefault());
        battle.Initialize();
        battle.Start();
        for (var i = 0; i < 8 && battle.Context.EcsWorld == null; i++) battle.Tick();
        battle.SetupBattle();
        for (var i = 0; i < 10; i++) battle.Tick();

        var services = battle.RuntimeServices!;
        var actors = services.Resolve<MobaActorRegistry>();
        var casterId = actors.Entries.First(entry => entry.Value != null &&
            entry.Value.hasModelId && entry.Value.modelId.Value == 1001 &&
            entry.Value.hasTeam && entry.Value.hasAttributeGroup &&
            entry.Value.hasResourceContainer).Key;
        var casts = services.Resolve<SkillCastCoordinator>();
        var cast = casts.TryCastSkill(casterId, 10010301, 3);
        Assert.True(cast.Success, cast.FailReason);
        Assert.True(casts.TryGetRunningByInstanceId(casterId,
            cast.RuntimeHandle.RootContextId, out _));

        var runtime = new MobaSkillRuntimeRollbackProvider(
            services.Resolve<MobaSkillCastRuntimeService>());
        var runner = new MobaSkillPipelineRollbackProvider(casts);
        var frame = new FrameIndex(battle.Context.LastFrame);
        var runtimeBytes = runtime.Export(frame);
        var runnerBytes = runner.Export(frame);
        battle.Tick();
        Assert.NotEqual(runnerBytes, runner.Export(frame));

        runtime.ValidateImport(frame, runtimeBytes);
        runner.ValidateImport(frame, runnerBytes);
        runtime.Import(frame, runtimeBytes);
        runner.Import(frame, runnerBytes);

        Assert.Equal(runnerBytes, runner.Export(frame));
        Assert.True(casts.TryGetRunningByInstanceId(casterId,
            cast.RuntimeHandle.RootContextId, out _));
    }
}
