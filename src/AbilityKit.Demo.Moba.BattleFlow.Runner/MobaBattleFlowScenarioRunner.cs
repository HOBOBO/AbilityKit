using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.DI;
using AbilityKit.BattleFlow;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Console;
using AbilityKit.Demo.Moba.Console.Battle.Config;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.EntityConstruction;
using AbilityKit.Demo.Moba.Services.EnvironmentModel;
using AbilityKit.Demo.Moba.Services.LogicWorld;
using AbilityKit.Demo.Moba.EnvironmentModel;
using AbilityKit.Protocol.Moba;
using AbilityKit.Scenario;
using AbilityKit.Trace;

namespace AbilityKit.Demo.Moba.BattleFlow;

/// <summary>
/// MOBA 战斗流程的世界执行核心（纯 .NET）：boot console 世界 → 生成 actors → 按 timeline 施放技能 → 采 trace → 中性结果。
/// 这是「编辑器 headless 跑」的引擎；编辑器（Unity）侧通过 shell-out 调用本工程的命令行入口。
/// 当前是 smoke 级：只生成 + 施放 + 采 trace，不判断言、不绑环境（后续切片）。
/// </summary>
public sealed class MobaBattleFlowScenarioRunner
{
    private const string LocalPlayerId = "player_1";

    public static BattleFlowRunResult Run(TestScenario scenario)
    {
        using var bootstrapper = Boot();
        var services = bootstrapper.RuntimeServices!;
        var aliases = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var envEntities = BindEnvironment(scenario, services);
        SpawnActors(scenario, services, aliases);
        RunTimeline(scenario, bootstrapper, services, aliases);
        var traceNodes = CountTraceNodes(services);

        var summary = $"actors={scenario.Actors.Count}, timeline={scenario.Timeline.Count}, traceNodes={traceNodes}";
        if (!string.IsNullOrEmpty(scenario.EnvironmentProfileId))
            summary += $", env={scenario.EnvironmentProfileId}({envEntities}个)";

        return new BattleFlowRunResult
        {
            Passed = true,
            Summary = summary,
        };
    }

    /// <summary>按 EnvironmentProfileId 解析环境（常用组→原语）并用 binder 生成环境实体（背景怪/墙等）。返回生成的实体数。</summary>
    private static int BindEnvironment(TestScenario scenario, IWorldResolver services)
    {
        if (string.IsNullOrEmpty(scenario.EnvironmentProfileId)) return 0;

        var catalog = MobaEnvironmentProfileCatalog.CreateDefault();
        var expander = new MobaEnvironmentGroupExpander();
        if (!catalog.TryResolve(scenario.EnvironmentProfileId, expander, out var resolved)) return 0;

        var binder = new MobaEnvironmentProfileBinder(services);
        var result = binder.Bind(in resolved);
        return result.Handles.Count;
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

    private static void SpawnActors(TestScenario scenario, IWorldResolver services, Dictionary<string, int> aliases)
    {
        var spawn = services.Resolve<IMobaActorSpawnService>();
        foreach (var actor in scenario.Actors)
        {
            if (string.IsNullOrEmpty(actor.Alias)) continue;
            var pos = actor.Position ?? new TestVector3(0, 0, 0);
            var transform = new Transform3(new Vec3(pos.X, pos.Y, pos.Z), Quat.Identity, Vec3.One);
            var ownerPlayer = string.IsNullOrEmpty(actor.PlayerId) ? default : new PlayerId(actor.PlayerId);
            var info = new MobaEntityInfo(
                actorId: 0,
                kind: MobaEntityKind.Hero,
                transform: transform,
                team: (Team)(actor.TeamId > 0 ? actor.TeamId : 1),
                mainType: EntityMainType.Unit,
                unitSubType: UnitSubType.Hero,
                ownerPlayer: ownerPlayer,
                templateId: actor.HeroId > 0 ? actor.HeroId : 1001);
            var spec = new MobaActorBuildSpec(in info, MobaActorBuildSourceKind.Unknown, 0, 0);
            var request = MobaActorSpawnRequest.FromSpec(in spec);
            request.AllocateActorIdIfMissing = true;
            if (spawn.TrySpawn(in request, out var result))
                aliases[actor.Alias] = result.ActorId;
        }
    }

    private static void RunTimeline(
        TestScenario scenario, ConsoleBattleBootstrapper bootstrapper, IWorldResolver services, Dictionary<string, int> aliases)
    {
        var input = services.Resolve<IMobaInputCoordinator>();
        var steps = new List<TestTimelineStep>();
        foreach (var step in scenario.Timeline) if (step != null) steps.Add(step);
        steps.Sort((a, b) => Math.Max(0, a.AtMs).CompareTo(Math.Max(0, b.AtMs)));

        var cursorMs = 0;
        foreach (var step in steps)
        {
            var atMs = Math.Max(0, step.AtMs);
            if (atMs > cursorMs) TickMilliseconds(bootstrapper, atMs - cursorMs);
            cursorMs = Math.Max(cursorMs, atMs);

            if (!IsCast(step.Action) || string.IsNullOrEmpty(step.ActorAlias)) continue;
            if (!aliases.TryGetValue(step.ActorAlias, out var actorId)) continue;

            var targetActorId = 0;
            if (!string.IsNullOrEmpty(step.TargetAlias)) aliases.TryGetValue(step.TargetAlias, out targetActorId);

            SubmitSkill(input, bootstrapper, actorId, targetActorId, step.Slot);
            bootstrapper.Tick();
        }
    }

    private static void SubmitSkill(IMobaInputCoordinator input, ConsoleBattleBootstrapper bootstrapper, int actorId, int targetActorId, int slot)
    {
        var playerId = new PlayerId(LocalPlayerId);
        var castFrame = new FrameIndex(bootstrapper.Context.LastFrame + 1);
        var aimPos = default(Vec3);
        var aimDir = default(Vec3);
        var skillInput = new SkillInputEvent(slot: slot, phase: SkillInputPhase.Press, targetActorId: targetActorId, aimPos: in aimPos, aimDir: in aimDir);
        var command = new PlayerInputCommand(castFrame, playerId, MobaOpCodes.Input.SkillInput, SkillInputCodec.Serialize(in skillInput));
        input.TrySubmit(castFrame, new[] { command });
    }

    private static int CountTraceNodes(IWorldResolver services)
    {
        if (!services.TryResolve<MobaTraceRegistry>(out var trace) || trace == null) return 0;
        var count = 0;
        foreach (MobaTraceKind kind in Enum.GetValues(typeof(MobaTraceKind)))
        {
            if (kind == MobaTraceKind.None) continue;
            foreach (var node in trace.GetNodesByKind((int)kind))
                if (node.IsValid) count++;
        }
        return count;
    }

    private static bool IsCast(string action)
        => string.IsNullOrEmpty(action) || string.Equals(action, "cast_skill", StringComparison.OrdinalIgnoreCase);

    private static void TickMilliseconds(ConsoleBattleBootstrapper bootstrapper, int milliseconds)
    {
        if (milliseconds <= 0) return;
        var ticks = Math.Max(1, (int)Math.Round(milliseconds / (1000f / 30f)));
        for (var i = 0; i < ticks; i++) bootstrapper.Tick();
    }
}
