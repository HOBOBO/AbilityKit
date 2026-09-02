using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Demo.Moba.Acceptance;
using AbilityKit.Scenario;
using AbilityKit.Demo.Moba.Console;
using AbilityKit.Demo.Moba.Console.Battle.Config;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Game.Test.UnitTest;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Smoke;

/// <summary>
/// Seam #4 收敛件：真实 acceptance 期望的完整 live 判定。
/// 期望 → actors 装配（带 playerId 的绑 console 本地玩家，其余 spawn_actor 合成）
/// → setupActions（切片 2）→ timeline（切片 3）→ 结算 → 从 <c>MobaTraceRegistry</c> 采真实 trace
/// → <see cref="AcceptanceVerifier"/> 判定。全程纯 dotnet，无 Unity、无手动捕获。
/// 这是 needs-trace 清零路径：任何一个期望文件都能这样拿到 live verdict。
/// </summary>
public sealed class LiveSimAcceptanceScenarioRunner
{
    /// <summary>
    /// 场景锚点平移：期望坐标是给 harness 的空旷技能测试世界（worldId=skill_*_world）写的；
    /// console 默认图（Prototype Arena, mapId=1）在原点区有 Center Blocker(x≈2.2-7.8,z≈-2.6-6.6)
    /// 和 (-5,-2) 立柱，East/West 墙在 |x|≥17.5。锚点 (-15,0,0)（team1 出生区向东）提供一条
    /// 8+ 单位的开阔 +X 走廊（-15→-7），保持期望的相对几何（caster+3=target、dash 穿过 target）。
    /// 只平移 position，绝不动 direction。
    /// </summary>
    private static readonly (float X, float Y, float Z) DefaultAnchor = (-15f, 0f, 0f);

    public static MobaAcceptanceSummary Run(MobaAcceptanceExpectation expectation)
        => Run(expectation, null, null);

    /// <summary>
    /// Runs a scenario with optional carrier profile resolution and behavior binding. Keeping the
    /// dependencies optional preserves the legacy trace-only entry point while allowing BT/HFSM
    /// carriers to share the same scenario contract.
    /// </summary>
    public static MobaAcceptanceSummary Run(
        MobaAcceptanceExpectation expectation,
        ScenarioProfileCatalog? profileCatalog,
        IBehaviorProfileBinder? behaviorBinder)
    {
        ArgumentNullException.ThrowIfNull(expectation);

        // Validate the neutral contract before constructing a carrier world. This turns
        // malformed profiles into deterministic input errors instead of opaque sim failures.
        var scenario = TestScenarioAdapter.FromMoba(expectation, "dotnet.console");
        TestScenarioValidator.ThrowIfInvalid(scenario);
        profileCatalog?.ThrowIfInvalid(scenario);
        behaviorBinder ??= NoopBehaviorProfileBinder.Instance;

        using var bootstrapper = Boot();
        var executor = new LiveSimSetupActionExecutor(bootstrapper);

        AssembleActors(executor, PickActors(expectation), DefaultAnchor);
        BindBehaviors(scenario, executor, profileCatalog, behaviorBinder);
        behaviorBinder.Start();
        try
        {
        RunSetup(executor, PickSetupActions(expectation), DefaultAnchor);

        var timeline = new LiveSimTimelineRunner(bootstrapper, executor);
        timeline.Run(OffsetTimeline(PickTimeline(expectation), DefaultAnchor));

        // 结算尾巴（镜像 TickScenarioTail 的作用；timeline 自带的 wait 已推进主时段）
        executor.TickMilliseconds(500);

        var records = CaptureRecords(bootstrapper, expectation.caseId);
        var observations = ((IAcceptanceObservationSource)executor).Capture(scenario);
        observations = MergeBehaviorObservations(observations, behaviorBinder.CaptureSnapshots());
        return AcceptanceVerifier.VerifyWithObservations(expectation, records, observations);
        }
        finally
        {
            behaviorBinder.Stop();
        }
    }

    private static void BindBehaviors(
        TestScenario scenario,
        LiveSimSetupActionExecutor executor,
        ScenarioProfileCatalog? catalog,
        IBehaviorProfileBinder binder)
    {
        if (catalog == null) return;
        foreach (var actor in scenario.Actors)
        {
            if (string.IsNullOrWhiteSpace(actor.BehaviorProfileId)) continue;
            if (!executor.TryGetActorId(actor.Alias, out var actorId))
                throw new InvalidOperationException($"Behavior actor alias '{actor.Alias}' was not spawned.");
            if (!((IScenarioProfileResolver<BehaviorProfile>)catalog).TryResolve(actor.BehaviorProfileId, out var profile))
                throw new InvalidOperationException($"Behavior profile '{actor.BehaviorProfileId}' was not found.");
            binder.Bind(new BehaviorBindingRequest(scenario, actor, actorId, profile, scenario.Seed));
        }
    }

    private static AcceptanceObservations MergeBehaviorObservations(
        AcceptanceObservations observations,
        IReadOnlyList<BehaviorRuntimeSnapshot> snapshots)
    {
        if (snapshots == null || snapshots.Count == 0) return observations;
        var contexts = new List<AcceptanceObservation>(observations.Contexts);
        foreach (var snapshot in snapshots)
        {
            contexts.Add(new AcceptanceObservation(snapshot.Alias, snapshot.ActorId, "behavior", "state", snapshot.State));
            foreach (var pair in snapshot.Blackboard)
                contexts.Add(new AcceptanceObservation(snapshot.Alias, snapshot.ActorId, "behavior", "blackboard." + pair.Key, pair.Value));
        }
        return new AcceptanceObservations { States = observations.States, Contexts = contexts };
    }

    private static MobaAcceptanceVector3Expectation Offset(MobaAcceptanceVector3Expectation position, (float X, float Y, float Z) anchor)
        => position == null ? null : new MobaAcceptanceVector3Expectation
        {
            x = position.x + anchor.X,
            y = position.y + anchor.Y,
            z = position.z + anchor.Z,
        };

    private static MobaAcceptanceTimelineStepExpectation[] OffsetTimeline(MobaAcceptanceTimelineStepExpectation[] timeline, (float X, float Y, float Z) anchor)
    {
        if (timeline == null) return Array.Empty<MobaAcceptanceTimelineStepExpectation>();
        var result = new MobaAcceptanceTimelineStepExpectation[timeline.Length];
        for (var i = 0; i < timeline.Length; i++)
        {
            var step = timeline[i];
            result[i] = step == null ? null : new MobaAcceptanceTimelineStepExpectation
            {
                stepId = step.stepId,
                atMs = step.atMs,
                action = step.action,
                actorAlias = step.actorAlias,
                targetAlias = step.targetAlias,
                playerId = step.playerId,
                slot = step.slot,
                skillId = step.skillId,
                targetActorId = step.targetActorId,
                durationMs = step.durationMs,
                property = step.property,
                value = step.value,
                intValue = step.intValue,
                position = Offset(step.position, anchor), // direction 绝不平移
                direction = step.direction,
                payload = step.payload,
                note = step.note,
            };
        }
        return result;
    }

    // —— 装配（scenario.* 优先、扁平字段回退，与 BuildSummary 的回退一致）——

    private static void AssembleActors(LiveSimSetupActionExecutor executor, MobaAcceptanceActorExpectation[] actors, (float X, float Y, float Z) anchor)
    {
        if (actors == null || actors.Length == 0) return;

        // console 是单本地玩家：第一个带 playerId 的 actor 绑本地玩家（施法者），其余用 spawn_actor 合成
        // （heroId/attributeTemplateId/teamId/出生点直接取期望声明，与 harness BuildPlayerLoadouts 等效）。
        var seeded = false;
        foreach (var actor in actors)
        {
            if (actor == null || string.IsNullOrEmpty(actor.alias)) continue;
            if (!seeded && !string.IsNullOrEmpty(actor.playerId))
            {
                executor.SeedLocalPlayerAlias(actor.alias);
                // 玩家驱动的 actor 不应被 AI brain 带偏（console 本地玩家默认带 brain，会在 dash 后游走）。
                if (executor.TryGetActorId(actor.alias, out var seededActorId))
                {
                    executor.DisableActorBrain(seededActorId);
                }
                // 本地玩家出生在 console 默认点——显式移到期望声明的出生位（锚点平移后），保证位移/命中几何正确。
                if (actor.spawnPosition != null)
                {
                    executor.Execute(new MobaAcceptanceSetupActionExpectation
                    {
                        action = "move_to",
                        actorAlias = actor.alias,
                        position = Offset(actor.spawnPosition, anchor),
                    });
                }
                seeded = true;
                continue;
            }

            executor.Execute(new MobaAcceptanceSetupActionExpectation
            {
                action = "spawn_actor",
                alias = actor.alias,
                teamId = actor.teamId,
                heroId = actor.heroId,
                attributeTemplateId = actor.attributeTemplateId,
                playerId = actor.playerId,
                position = Offset(actor.spawnPosition, anchor),
            });
        }
    }

    private static void RunSetup(LiveSimSetupActionExecutor executor, MobaAcceptanceSetupActionExpectation[] setupActions, (float X, float Y, float Z) anchor)
    {
        if (setupActions == null) return;
        foreach (var action in setupActions)
        {
            if (action?.position != null)
            {
                action.position = Offset(action.position, anchor);
            }
            executor.Execute(action);
        }
    }

    // —— trace 采集：镜像 MobaAcceptanceTraceExporter.CaptureTraceRecords 的核心（无 label 富化，判定不依赖）——

    private static MobaAcceptanceTraceRecord[] CaptureRecords(ConsoleBattleBootstrapper bootstrapper, string caseId)
    {
        var services = bootstrapper.RuntimeServices;
        Assert.NotNull(services);
        Assert.True(services!.TryResolve<MobaTraceRegistry>(out var trace) && trace != null,
            "MobaTraceRegistry must be resolvable from the console world.");

        var records = new List<MobaAcceptanceTraceRecord>(64);
        var seen = new HashSet<long>();
        foreach (MobaTraceKind kind in Enum.GetValues(typeof(MobaTraceKind)))
        {
            if (kind == MobaTraceKind.None) continue;
            foreach (var node in trace!.GetNodesByKind((int)kind))
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

    private static MobaAcceptanceActorExpectation[] PickActors(MobaAcceptanceExpectation e)
        => e.scenario?.actors is { Length: > 0 } preferred ? preferred : e.actors ?? Array.Empty<MobaAcceptanceActorExpectation>();

    private static MobaAcceptanceSetupActionExpectation[] PickSetupActions(MobaAcceptanceExpectation e)
        => e.scenario?.setupActions is { Length: > 0 } preferred ? preferred : e.setupActions ?? Array.Empty<MobaAcceptanceSetupActionExpectation>();

    private static MobaAcceptanceTimelineStepExpectation[] PickTimeline(MobaAcceptanceExpectation e)
        => e.scenario?.timeline is { Length: > 0 } preferred ? preferred : e.timeline ?? Array.Empty<MobaAcceptanceTimelineStepExpectation>();
}
