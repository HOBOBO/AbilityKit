using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.BattleFlow;
using AbilityKit.EnvironmentModel;
using AbilityKit.Demo.Moba.Acceptance;
using AbilityKit.Demo.Moba.Acceptance.LiveSim;
using AbilityKit.Demo.Moba.Console;
using AbilityKit.Demo.Moba.Console.Battle.Config;
using AbilityKit.Demo.Moba.EnvironmentModel;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.EnvironmentModel;
using AbilityKit.Game.Test.UnitTest;
using AbilityKit.Scenario;
using AbilityKit.Trace;

namespace AbilityKit.Demo.Moba.BattleFlow;

/// <summary>
/// MOBA 战斗流程的世界执行核心（纯 .NET）：把玩法中立的 <see cref="TestScenario"/> 翻译成完整 acceptance 期望，
/// 再复用 <see cref="LiveSimSetupActionExecutor"/> + <see cref="LiveSimTimelineRunner"/> 在真实 console 逻辑世界里
/// 装配真实英雄（属性+技能）、跑完整时间线、采 trace + 状态观测，最后 <see cref="AcceptanceVerifier.VerifyWithObservations"/> 判 verdict。
/// 无断言时 smoke（跑通即通过）。
/// </summary>
public sealed class MobaBattleFlowScenarioRunner
{
    public static BattleFlowRunResult Run(TestScenario scenario) => RunDetailed(scenario).Result;

    public static MobaBattleFlowRunOutcome RunDetailed(TestScenario scenario)
    {
        using var bootstrapper = Boot();
        var expectation = ToExpectation(scenario);
        var executor = new LiveSimSetupActionExecutor(bootstrapper);

        var envEntities = BindEnvironment(scenario, bootstrapper.RuntimeServices!);
        PlaceObstacles(scenario, bootstrapper.RuntimeServices!);
        AssembleActors(expectation, executor);
        new LiveSimTimelineRunner(bootstrapper, executor).Run(
            expectation.timeline ?? Array.Empty<MobaAcceptanceTimelineStepExpectation>());
        executor.TickMilliseconds(500);

        var records = CaptureTraceRecords(bootstrapper, scenario.CaseId);
        var traceNodes = ToTraceNodes(records);
        var summary = $"actors={scenario.Actors.Count}, timeline={scenario.Timeline.Count}, traceNodes={records.Length}";
        if (!string.IsNullOrEmpty(scenario.EnvironmentProfileId))
            summary += $", env={scenario.EnvironmentProfileId}({envEntities}个)";

        if (!HasAssertions(expectation))
            return new MobaBattleFlowRunOutcome { Result = new BattleFlowRunResult { Passed = true, Summary = summary }, TraceNodes = traceNodes };

        var observations = executor.CaptureObservations(expectation);
        var verdict = AcceptanceVerifier.VerifyWithObservations(expectation, records, observations);
        summary += $", verdict={(verdict.result.passed ? "PASSED" : "FAILED")}";
        if (!verdict.result.passed)
            summary += $", missing={verdict.coverage.missingTraceNodes}";
        return new MobaBattleFlowRunOutcome { Result = new BattleFlowRunResult { Passed = verdict.result.passed, Summary = summary }, TraceNodes = traceNodes, Summary = verdict };
    }

    private static BattleFlowTraceNode[] ToTraceNodes(MobaAcceptanceTraceRecord[] records) =>
        records.Select(r => new BattleFlowTraceNode
        {
            Id = r.nodeId,
            ParentId = r.parentId,
            RootId = r.rootId,
            Kind = r.kind ?? string.Empty,
            ConfigId = r.configId,
            Frame = r.frame,
        }).ToArray();

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

    /// <summary>按 EnvironmentProfileId 解析环境（常用组→原语）并用 binder 生成环境实体（背景怪/墙等）。返回生成的实体数。</summary>
    private static int BindEnvironment(TestScenario scenario, AbilityKit.Ability.World.DI.IWorldResolver services)
    {
        if (string.IsNullOrEmpty(scenario.EnvironmentProfileId)) return 0;

        var catalog = MobaEnvironmentProfileCatalog.CreateDefault();
        var expander = new MobaEnvironmentGroupExpander();
        if (!catalog.TryResolve(scenario.EnvironmentProfileId, expander, out var resolved)) return 0;

        var binder = new MobaEnvironmentProfileBinder(services);
        var result = binder.Bind(in resolved);
        return result.Handles.Count;
    }

    /// <summary>把场景里声明的障碍物（PlaceObstacleBlock → TestScenario.Obstacles）放进碰撞世界（WorldId 层）。</summary>
    private static void PlaceObstacles(TestScenario scenario, AbilityKit.Ability.World.DI.IWorldResolver services)
    {
        if (scenario.Obstacles == null || scenario.Obstacles.Count == 0) return;
        var binder = new MobaEnvironmentProfileBinder(services);
        foreach (var obstacle in scenario.Obstacles)
        {
            if (obstacle == null) continue;
            binder.PlaceObstacle(new ObstaclePrimitive
            {
                Shape = obstacle.Shape,
                Position = new EnvironmentVector3(obstacle.Position.X, obstacle.Position.Y, obstacle.Position.Z),
                Size = new EnvironmentVector3(obstacle.Size.X, obstacle.Size.Y, obstacle.Size.Z),
            });
        }
    }

    private static void AssembleActors(MobaAcceptanceExpectation expectation, LiveSimSetupActionExecutor executor)
    {
        var actors = expectation.actors ?? Array.Empty<MobaAcceptanceActorExpectation>();
        for (var i = 0; i < actors.Length; i++)
        {
            var actor = actors[i];
            if (actor == null || string.IsNullOrEmpty(actor.alias)) continue;
            // 每个 actor 分配独立 playerId（未显式指定时按序生成），保证 MobaPlayerActorMapService 的输入路由不互相覆盖。
            var playerId = string.IsNullOrEmpty(actor.playerId) ? $"player_{i + 1}" : actor.playerId;
            executor.Execute(new MobaAcceptanceSetupActionExpectation
            {
                action = "spawn_actor",
                alias = actor.alias,
                playerId = playerId,
                teamId = actor.teamId,
                heroId = actor.heroId,
                attributeTemplateId = actor.attributeTemplateId,
                position = actor.spawnPosition,
            });
        }
    }

    private static MobaAcceptanceTraceRecord[] CaptureTraceRecords(ConsoleBattleBootstrapper bootstrapper, string caseId)
    {
        var services = bootstrapper.RuntimeServices;
        if (services == null || !services.TryResolve<MobaTraceRegistry>(out var trace) || trace == null)
            return Array.Empty<MobaAcceptanceTraceRecord>();

        var records = new List<MobaAcceptanceTraceRecord>(64);
        var seen = new HashSet<long>();
        foreach (MobaTraceKind kind in Enum.GetValues(typeof(MobaTraceKind)))
        {
            if (kind == MobaTraceKind.None) continue;
            foreach (var node in trace.GetNodesByKind((int)kind))
            {
                if (!node.IsValid || !seen.Add(node.ContextId)) continue;
                var metadata = node.Metadata;
                var frame = node.CreatedFrame;
                records.Add(new MobaAcceptanceTraceRecord
                {
                    caseId = caseId,
                    frame = frame,
                    timeMs = (int)Math.Round(frame * (1000f / 30f)),
                    rootId = node.RootId,
                    parentId = node.ParentId,
                    nodeId = node.ContextId,
                    kind = kind.ToString(),
                    kindValue = node.Kind,
                    configId = metadata != null ? metadata.ConfigId : 0,
                    sourceActorId = metadata != null ? metadata.SourceActorId : 0,
                    targetActorId = metadata != null ? metadata.TargetActorId : 0,
                    sourceId = metadata != null ? metadata.SourceId : 0,
                    targetId = metadata != null ? metadata.TargetId : 0,
                    isRoot = node.IsRoot,
                    isEnded = node.IsEnded,
                    endedFrame = node.EndedFrame,
                    endReason = node.EndReason,
                    childCount = node.ChildCount,
                });
            }
        }
        records.Sort((x, y) =>
        {
            var c = x.rootId.CompareTo(y.rootId);
            return c != 0 ? c : x.nodeId.CompareTo(y.nodeId);
        });
        return records.ToArray();
    }

    private static bool HasAssertions(MobaAcceptanceExpectation expectation) =>
        (expectation.mustContain?.Length ?? 0) > 0 || (expectation.mustNotContain?.Length ?? 0) > 0 ||
        (expectation.stateExpectations?.Length ?? 0) > 0 || (expectation.contextExpectations?.Length ?? 0) > 0 ||
        (expectation.relationships?.Length ?? 0) > 0;

    // —— 完整反向适配：TestScenario + MobaBattleFlowAssertions → MobaAcceptanceExpectation ——

    private static MobaAcceptanceExpectation ToExpectation(TestScenario scenario)
    {
        var assertions = scenario.Expectations as MobaBattleFlowAssertions;
        return new MobaAcceptanceExpectation
        {
            caseId = scenario.CaseId,
            actors = scenario.Actors.Select(ToActor).ToArray(),
            setupActions = scenario.Setup.Select(ToSetupAction).ToArray(),
            timeline = scenario.Timeline.Select(ToTimelineStep).ToArray(),
            stateExpectations = assertions?.State.Select(ToState).ToArray() ?? Array.Empty<MobaAcceptanceStateExpectation>(),
            contextExpectations = assertions?.Context.Select(ToContext).ToArray() ?? Array.Empty<MobaAcceptanceContextExpectation>(),
            mustContain = assertions?.MustContain.Select(ToTrace).ToArray() ?? Array.Empty<MobaAcceptanceTraceExpectation>(),
            mustNotContain = assertions?.MustNotContain.Select(ToTrace).ToArray() ?? Array.Empty<MobaAcceptanceTraceExpectation>(),
            relationships = assertions?.Relationships.Select(ToRelationship).ToArray() ?? Array.Empty<MobaAcceptanceRelationshipExpectation>(),
        };
    }

    private static MobaAcceptanceActorExpectation ToActor(TestActor a) => new()
    {
        alias = a.Alias,
        playerId = a.PlayerId,
        teamId = a.TeamId,
        heroId = a.HeroId,
        attributeTemplateId = a.AttributeTemplateId,
        skillIds = a.SkillIds ?? Array.Empty<int>(),
        hasSpawnPosition = a.Position != null,
        spawnPosition = ToVector(a.Position),
        facingDirection = ToVector(a.Facing),
    };

    private static MobaAcceptanceSetupActionExpectation ToSetupAction(TestSetupAction s) => new()
    {
        action = s.Action,
        actorAlias = s.ActorAlias,
        property = s.Property,
        value = (float)s.Value,
    };

    private static MobaAcceptanceTimelineStepExpectation ToTimelineStep(TestTimelineStep s) => new()
    {
        atMs = s.AtMs,
        action = s.Action,
        actorAlias = s.ActorAlias,
        targetAlias = s.TargetAlias,
        slot = s.Slot,
        position = ToVector(s.Position),
        direction = ToVector(s.Direction),
        durationMs = s.DurationMs,
    };

    private static MobaAcceptanceTraceExpectation ToTrace(MobaTraceAssertion a) => new()
    {
        kind = a.Kind,
        configId = a.ConfigId,
        minCount = a.MinCount,
        maxCount = a.MaxCount,
        underEffectId = a.UnderEffectId,
    };

    private static MobaAcceptanceStateExpectation ToState(MobaStateAssertion a) => new()
    {
        alias = a.Alias,
        property = a.Property,
        comparator = a.Comparator,
        expectedValue = a.ExpectedValue,
    };

    private static MobaAcceptanceContextExpectation ToContext(MobaContextAssertion a) => new()
    {
        alias = a.Alias,
        kind = a.Kind,
        property = a.Property,
        comparator = a.Comparator,
        expectedValue = a.ExpectedValue,
    };

    private static MobaAcceptanceRelationshipExpectation ToRelationship(MobaRelationshipAssertion a) => new()
    {
        parentKind = a.ParentKind,
        parentConfigId = a.ParentConfigId,
        childKind = a.ChildKind,
        childConfigId = a.ChildConfigId,
    };

    private static MobaAcceptanceVector3Expectation ToVector(TestVector3? v)
        => v is null ? null : new MobaAcceptanceVector3Expectation { x = v.Value.X, y = v.Value.Y, z = v.Value.Z };
}

/// <summary>详细运行结果：中性 verdict + 中性 trace 树 + 富判定摘要。编辑器/CLI 用前两者，webadmin 用 Summary 做回归分析。</summary>
public sealed class MobaBattleFlowRunOutcome
{
    public BattleFlowRunResult Result { get; init; } = new BattleFlowRunResult { Passed = false };
    public BattleFlowTraceNode[] TraceNodes { get; init; } = Array.Empty<BattleFlowTraceNode>();

    /// <summary>富判定摘要（verdict/coverage/traceCounts），供 webadmin 回归总结分析；无断言（smoke）时为 null。</summary>
    public MobaAcceptanceSummary? Summary { get; init; }
}
