using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Game.Test.UnitTest;

namespace AbilityKit.Demo.Moba.Acceptance;

/// <summary>
/// 纯判定器 —— 从 <c>MobaAcceptanceTraceExporter.BuildCoverage</c> / <c>BuildSummary</c> 忠实移植，
/// 但<b>不再接收 <c>MobaSkillConfigTestHarness</c></b>。这是 harness 解耦的第一刀：
/// 判定只依赖 (期望, 观测 trace)，与模拟器完全解耦。
/// </summary>
/// <remarks>
/// 与现有 BuildSummary 的差异（均为 harness 富化项，不参与 passed 判定，留空/默认）：
/// <list type="bullet">
/// <item><c>result.finalFrame / finalTimeMs</c> —— 原 harness.FrameTime，这里留 0。</item>
/// <item><c>diagnostics.*</c> —— 原 harness 战斗诊断服务，这里留 null。</item>
/// <item><c>traceDictionary / 各类 label</c> —— 原 harness.Config 反查名称，这里留空。</item>
/// </list>
/// <c>result.passed</c> 与 coverage 计算与生产实现完全一致。
/// </remarks>
public static class AcceptanceVerifier
{
    /// <summary>对一组观测 trace 判定期望，产出 canonical <see cref="MobaAcceptanceSummary"/>。</summary>
    public static MobaAcceptanceSummary Verify(
        MobaAcceptanceExpectation expectation,
        MobaAcceptanceTraceRecord[] records,
        string? expectationPath = null,
        string? traceJsonlPath = null,
        string? summaryJsonPath = null)
        => VerifyCore(expectation, records, null, expectationPath, traceJsonlPath, summaryJsonPath);

    /// <summary>
    /// Verifies trace plus carrier-provided state/context observations. Keeping observations
    /// outside the DTO lets console, Unity, replay and network carriers share this oracle.
    /// </summary>
    public static MobaAcceptanceSummary VerifyWithObservations(
        MobaAcceptanceExpectation expectation,
        MobaAcceptanceTraceRecord[] records,
        AcceptanceObservations? observations,
        string? expectationPath = null,
        string? traceJsonlPath = null,
        string? summaryJsonPath = null)
    {
        return VerifyCore(expectation, records, observations, expectationPath, traceJsonlPath, summaryJsonPath);
    }

    private static MobaAcceptanceSummary VerifyCore(
        MobaAcceptanceExpectation expectation,
        MobaAcceptanceTraceRecord[] records,
        AcceptanceObservations? observations,
        string? expectationPath,
        string? traceJsonlPath,
        string? summaryJsonPath)
    {
        ArgumentNullException.ThrowIfNull(expectation);
        ArgumentNullException.ThrowIfNull(records);

        var effectId = expectation.config != null ? expectation.config.effectId : 0;
        var skillId = expectation.config != null ? expectation.config.skillId : 0;
        var effectRootId = FindRootId(records, "EffectExecution", effectId);
        var coverage = BuildCoverage(expectation, records, effectRootId, observations, observations is not null);
        var passed = coverage.allRequiredTraceNodesMatched
                     && coverage.allForbiddenTraceNodesAbsent
                     && coverage.allExpectedActionsExecuted
                     && coverage.allRelationshipsSatisfied
                     && coverage.allStateExpectationsSatisfied
                     && coverage.allContextExpectationsSatisfied;

        return new MobaAcceptanceSummary
        {
            caseId = expectation.caseId,
            worldId = expectation.worldId,
            expectationPath = expectationPath,
            tickRate = expectation.tickRate,
            accelerated = expectation.accelerated,
            category = ResolveCategory(expectation),
            tags = PickTags(expectation),
            generatedFrom = expectation.generatedFrom,
            lastReviewedAt = expectation.lastReviewedAt,
            // scenario.* 优先，扁平字段回退 —— 与 BuildSummary 一致。
            scenario = expectation.scenario,
            actors = Pick(expectation.scenario?.actors, expectation.actors),
            setupActions = Pick(expectation.scenario?.setupActions, expectation.setupActions),
            timeline = Pick(expectation.scenario?.timeline, expectation.timeline),
            stateExpectations = Pick(expectation.scenario?.stateExpectations, expectation.stateExpectations),
            contextExpectations = Pick(expectation.scenario?.contextExpectations, expectation.contextExpectations),
            input = expectation.input,
            config = expectation.config,
            result = new MobaAcceptanceResult
            {
                passed = passed,
                skillCastTraceFound = Contains(records, "SkillCast", skillId, 0),
                effectExecutionTraceFound = Contains(records, "EffectExecution", effectId, 0),
                allExpectedActionsExecuted = coverage.allExpectedActionsExecuted,
                projectileLaunched = expectation.config == null || expectation.config.expectedProjectile == null
                                     || Contains(records, "ProjectileLaunch", expectation.config.expectedProjectile.projectileId, effectRootId),
                areaSpawned = ContainsKind(records, "AreaSpawn"),
                buffApplied = ContainsKind(records, "BuffApply"),
                effectRootId = effectRootId,
                finalFrame = 0,        // harness-enriched：留默认
                finalTimeMs = 0,       // harness-enriched：留默认
                traceNodeCount = records.Length,
                expectedTraceNodeCount = coverage.expectedTraceNodeCount,
                matchedExpectedTraceNodeCount = coverage.matchedExpectedTraceNodeCount,
                missingExpectedTraceNodeCount = coverage.missingExpectedTraceNodeCount,
                expectedActionCount = coverage.expectedActionCount,
                executedExpectedActionCount = coverage.executedExpectedActionCount,
                expectedRelationshipCount = coverage.expectedRelationshipCount,
                satisfiedRelationshipCount = coverage.satisfiedRelationshipCount,
                expectedStateCount = coverage.expectedStateCount,
                satisfiedStateCount = coverage.satisfiedStateCount,
                expectedContextCount = coverage.expectedContextCount,
                satisfiedContextCount = coverage.satisfiedContextCount,
            },
            coverage = coverage,
            traceCounts = CountByKind(records),
            // harness-enriched：dotnet 判定层不反查 config 名称，给空值（JSON 更干净）。
            traceDictionary = Array.Empty<MobaAcceptanceTraceDictionaryEntry>(),
            traceDictionaryVersion = string.Empty,
            diagnostics = new MobaAcceptanceDiagnosticsSummary
            {
                warningCount = 0,
                warnings = Array.Empty<MobaAcceptanceDiagnosticWarning>(),
                planActionRejections = string.Empty,
                triggerRuntimeSnapshot = string.Empty,
            },
            traceJsonlPath = NormalizePath(traceJsonlPath),
            summaryJsonPath = NormalizePath(summaryJsonPath),
        };
    }

    // —— 以下为 BuildCoverage / 辅助查询的忠实移植（纯函数，无 harness 依赖）——

    private static MobaAcceptanceCoverageSummary BuildCoverage(
        MobaAcceptanceExpectation expectation, MobaAcceptanceTraceRecord[] records, long effectRootId,
        AcceptanceObservations? observations, bool evaluateObservations)
    {
        var required = expectation.mustContain;
        var forbidden = expectation.mustNotContain;
        var actions = expectation.config != null ? expectation.config.expectedActions : null;
        var relationships = expectation.relationships;

        var coverage = new MobaAcceptanceCoverageSummary
        {
            expectedTraceNodeCount = required != null ? required.Length : 0,
            forbiddenTraceNodeCount = forbidden != null ? forbidden.Length : 0,
            expectedActionCount = actions != null ? actions.Length : 0,
            expectedRelationshipCount = relationships != null ? relationships.Length : 0,
            expectedStateCount = Pick(expectation.scenario?.stateExpectations, expectation.stateExpectations)?.Length ?? 0,
            expectedContextCount = Pick(expectation.scenario?.contextExpectations, expectation.contextExpectations)?.Length ?? 0,
        };

        var missingTraceNodes = new List<string>();
        if (required != null)
        {
            for (var i = 0; i < required.Length; i++)
            {
                var item = required[i];
                var count = Count(records, item.kind, item.configId, item.underEffectId > 0 ? effectRootId : 0);
                var minCount = item.minCount > 0 ? item.minCount : 1;
                if (count >= minCount && (item.maxCount <= 0 || count <= item.maxCount)) coverage.matchedExpectedTraceNodeCount++;
                else missingTraceNodes.Add(FormatTraceExpectation(item, count));
            }
        }

        var unexpectedTraceNodes = new List<string>();
        if (forbidden != null)
        {
            for (var i = 0; i < forbidden.Length; i++)
            {
                var item = forbidden[i];
                var count = Count(records, item.kind, item.configId, item.underEffectId > 0 ? effectRootId : 0);
                if (count > 0) unexpectedTraceNodes.Add(FormatTraceExpectation(item, count));
            }
        }

        var missingActions = new List<string>();
        if (actions != null)
        {
            for (var i = 0; i < actions.Length; i++)
            {
                if (Contains(records, "EffectAction", actions[i].actionId, effectRootId)) coverage.executedExpectedActionCount++;
                else missingActions.Add(actions[i].type + "(" + actions[i].actionId + ")");
            }
        }

        var missingRelationships = new List<string>();
        if (relationships != null)
        {
            for (var i = 0; i < relationships.Length; i++)
            {
                if (HasRelationship(records, relationships[i])) coverage.satisfiedRelationshipCount++;
                else missingRelationships.Add(relationships[i].parentKind + "(" + relationships[i].parentConfigId + ")->" + relationships[i].childKind + "(" + relationships[i].childConfigId + ")");
            }
        }

        var stateExpectations = Pick(expectation.scenario?.stateExpectations, expectation.stateExpectations);
        var contextExpectations = Pick(expectation.scenario?.contextExpectations, expectation.contextExpectations);
        var stateMatch = evaluateObservations
            ? AcceptanceObservationMatcher.Match(stateExpectations, observations!.States)
            : (stateExpectations?.Length ?? 0, string.Empty);
        var contextMatch = evaluateObservations
            ? AcceptanceObservationMatcher.Match(contextExpectations, observations!.Contexts)
            : (contextExpectations?.Length ?? 0, string.Empty);

        coverage.missingExpectedTraceNodeCount = missingTraceNodes.Count;
        coverage.unexpectedForbiddenTraceNodeCount = unexpectedTraceNodes.Count;
        coverage.allRequiredTraceNodesMatched = coverage.missingExpectedTraceNodeCount == 0;
        coverage.allForbiddenTraceNodesAbsent = coverage.unexpectedForbiddenTraceNodeCount == 0;
        coverage.allExpectedActionsExecuted = coverage.executedExpectedActionCount == coverage.expectedActionCount;
        coverage.allRelationshipsSatisfied = coverage.satisfiedRelationshipCount == coverage.expectedRelationshipCount;
        coverage.satisfiedStateCount = stateMatch.Item1;
        coverage.satisfiedContextCount = contextMatch.Item1;
        coverage.allStateExpectationsSatisfied = stateMatch.Item1 == coverage.expectedStateCount;
        coverage.allContextExpectationsSatisfied = contextMatch.Item1 == coverage.expectedContextCount;
        coverage.missingTraceNodes = string.Join(",", missingTraceNodes.ToArray());
        coverage.unexpectedTraceNodes = string.Join(",", unexpectedTraceNodes.ToArray());
        coverage.missingActions = string.Join(",", missingActions.ToArray());
        coverage.missingRelationships = string.Join(",", missingRelationships.ToArray());
        coverage.missingStates = stateMatch.Item2;
        coverage.missingContexts = contextMatch.Item2;
        return coverage;
    }

    private static string FormatTraceExpectation(MobaAcceptanceTraceExpectation e, int actualCount)
        => e.kind + "(" + e.configId + ",underEffectId=" + e.underEffectId + ",actual=" + actualCount + ")";

    private static long FindRootId(MobaAcceptanceTraceRecord[] records, string kind, int configId)
    {
        for (var i = 0; i < records.Length; i++)
            if (records[i].kind == kind && records[i].configId == configId) return records[i].rootId;
        return 0;
    }

    private static int Count(MobaAcceptanceTraceRecord[] records, string kind, int configId, long rootId)
    {
        var n = 0;
        for (var i = 0; i < records.Length; i++)
        {
            var r = records[i];
            if (r.kind == kind && r.configId == configId && (rootId <= 0 || r.rootId == rootId)) n++;
        }
        return n;
    }

    private static bool Contains(MobaAcceptanceTraceRecord[] records, string kind, int configId, long rootId)
        => Count(records, kind, configId, rootId) > 0;

    private static bool ContainsKind(MobaAcceptanceTraceRecord[] records, string kind)
    {
        for (var i = 0; i < records.Length; i++)
            if (records[i].kind == kind) return true;
        return false;
    }

    private static bool HasRelationship(MobaAcceptanceTraceRecord[] records, MobaAcceptanceRelationshipExpectation rel)
    {
        for (var i = 0; i < records.Length; i++)
        {
            var parent = records[i];
            if (parent.kind != rel.parentKind || parent.configId != rel.parentConfigId) continue;
            for (var j = 0; j < records.Length; j++)
            {
                var child = records[j];
                if (child.kind == rel.childKind && child.configId == rel.childConfigId && child.rootId == parent.rootId) return true;
            }
        }
        return false;
    }

    private static MobaAcceptanceTraceCount[] CountByKind(MobaAcceptanceTraceRecord[] records)
    {
        var counts = new Dictionary<string, int>();
        for (var i = 0; i < records.Length; i++)
        {
            var kind = records[i].kind ?? string.Empty;
            counts.TryGetValue(kind, out var c);
            counts[kind] = c + 1;
        }
        var result = new List<MobaAcceptanceTraceCount>(counts.Count);
        foreach (var pair in counts)
            result.Add(new MobaAcceptanceTraceCount { kind = pair.Key, count = pair.Value });
        result.Sort((a, b) => string.CompareOrdinal(a.kind, b.kind));
        return result.ToArray();
    }

    private static string ResolveCategory(MobaAcceptanceExpectation e)
    {
        if (e.scenario != null && !string.IsNullOrEmpty(e.scenario.category)) return e.scenario.category!;
        if (!string.IsNullOrEmpty(e.category)) return e.category!;
        return "contract";
    }

    private static string[]? PickTags(MobaAcceptanceExpectation e)
    {
        if (e.scenario != null && e.scenario.tags != null && e.scenario.tags.Length > 0) return e.scenario.tags;
        return e.tags;
    }

    private static T[]? Pick<T>(T[]? preferred, T[]? fallback) where T : class
        => preferred != null && preferred.Length > 0 ? preferred : fallback;

    private static string? NormalizePath(string? path) => string.IsNullOrEmpty(path) ? path : path.Replace('\\', '/');
}
