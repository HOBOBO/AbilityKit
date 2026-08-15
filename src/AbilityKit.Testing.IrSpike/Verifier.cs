using AbilityKit.Testing.IrSpike.Ir;

namespace AbilityKit.Testing.IrSpike;

// 判定器：纯函数，无 Unity / 无模拟器依赖。
// 核心覆盖逻辑忠实移植自 MobaAcceptanceTraceExporter.BuildCoverage / BuildSummary：
//   passed = 所有必需 trace 命中 && 禁止 trace 缺席 && 期望动作执行 && 因果关系成立
// 扩展点（设计文档 §11）：在 canonical passed 之上再加 state 断言族，合成 allPassed，
// 演示「多断言族合取」如何在不破坏既有语义的前提下扩展。
public static class Verifier
{
    public static AcceptanceSummary Verify(
        TestScenario scenario,
        IReadOnlyList<TraceRecord> trace,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, double>> observedState,
        string observationSource)
    {
        var effectRootId = FindRootId(trace, "EffectExecution", scenario.EffectId);
        var exp = scenario.Expectations;

        var missingTrace = new List<string>();
        var matchedTrace = 0;
        foreach (var req in exp.MustContain)
        {
            var count = Count(trace, req.Kind, req.ConfigId, req.UnderEffectId > 0 ? effectRootId : 0);
            var min = req.MinCount > 0 ? req.MinCount : 1;
            if (count >= min && (req.MaxCount <= 0 || count <= req.MaxCount)) matchedTrace++;
            else missingTrace.Add($"{req.Kind}({req.ConfigId},underEffect={req.UnderEffectId},actual={count})");
        }

        var unexpected = new List<string>();
        foreach (var forb in exp.MustNotContain)
        {
            var count = Count(trace, forb.Kind, forb.ConfigId, forb.UnderEffectId > 0 ? effectRootId : 0);
            if (count > 0) unexpected.Add($"{forb.Kind}({forb.ConfigId},actual={count})");
        }

        var missingActions = new List<string>();
        var executedActions = 0;
        foreach (var act in exp.ExpectedActions)
        {
            if (Contains(trace, "EffectAction", act.ActionId, effectRootId)) executedActions++;
            else missingActions.Add($"{act.Type}({act.ActionId})");
        }

        var missingRels = new List<string>();
        var satisfiedRels = 0;
        foreach (var rel in exp.Relationships)
        {
            if (HasRelationship(trace, rel)) satisfiedRels++;
            else missingRels.Add($"{rel.ParentKind}({rel.ParentConfigId})->{rel.ChildKind}({rel.ChildConfigId})");
        }

        bool allRequiredMatched = missingTrace.Count == 0;
        bool allForbiddenAbsent = unexpected.Count == 0;
        bool allActionsExecuted = executedActions == exp.ExpectedActions.Length;
        bool allRelsSatisfied = satisfiedRels == exp.Relationships.Length;

        // canonical passed —— 与现有 BuildSummary 完全一致（仅 trace/action/relationship 四项）。
        bool passed = allRequiredMatched && allForbiddenAbsent && allActionsExecuted && allRelsSatisfied;

        // 扩展：state 断言族（现有实现里 state 是独立的 NUnit assert，不进 summary.passed）。
        // 这里把它纳入 allPassed，演示设计文档的多断言族合取。
        var stateResults = exp.State.Select(s => EvaluateState(s, observedState)).ToArray();
        bool statePassed = stateResults.Length == 0 || stateResults.All(r => r.Passed);
        bool allPassed = passed && statePassed;

        return new AcceptanceSummary
        {
            CaseId = scenario.CaseId,
            WorldId = scenario.WorldId,
            TickRate = scenario.TickRate,
            Accelerated = scenario.Accelerated,
            Category = scenario.Category,
            Tags = scenario.Tags,
            ObservationSource = observationSource,
            Result = new AcceptanceResult
            {
                Passed = passed,           // canonical：与现有 summary.json 语义一致
                AllPassed = allPassed,     // 扩展：含 state 族
                StatePassed = statePassed,
                SkillCastTraceFound = Contains(trace, "SkillCast", scenario.SkillId, 0),
                EffectExecutionTraceFound = Contains(trace, "EffectExecution", scenario.EffectId, 0),
                AllExpectedActionsExecuted = allActionsExecuted,
                EffectRootId = effectRootId,
                TraceNodeCount = trace.Count,
                ExpectedTraceNodeCount = exp.MustContain.Length,
                MatchedExpectedTraceNodeCount = matchedTrace,
                MissingExpectedTraceNodeCount = missingTrace.Count,
                ExpectedActionCount = exp.ExpectedActions.Length,
                ExecutedExpectedActionCount = executedActions,
                ExpectedRelationshipCount = exp.Relationships.Length,
                SatisfiedRelationshipCount = satisfiedRels,
            },
            Coverage = new AcceptanceCoverage
            {
                AllRequiredTraceNodesMatched = allRequiredMatched,
                AllForbiddenTraceNodesAbsent = allForbiddenAbsent,
                AllExpectedActionsExecuted = allActionsExecuted,
                AllRelationshipsSatisfied = allRelsSatisfied,
                MissingTraceNodes = string.Join(",", missingTrace),
                UnexpectedTraceNodes = string.Join(",", unexpected),
                MissingActions = string.Join(",", missingActions),
                MissingRelationships = string.Join(",", missingRels),
            },
            State = stateResults,
        };
    }

    // state 断言求值（子集：hp/mana/maxHp 数值比较 + hasBuff 布尔）。
    // 完整 property/comparator 集活在 MobaAcceptanceExpectationAssert，这里只演示扩展机制。
    private static StateResult EvaluateState(
        StateExpectation s,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, double>> observedState)
    {
        if (!observedState.TryGetValue(s.Alias, out var actor))
            return new StateResult(s.Alias, s.Property, Passed: false, Detail: "alias 未观测到");

        bool isBuff = s.Property is "hasBuff" or "buff";
        if (isBuff && s.ExpectedInt is { } buffId)
        {
            actor.TryGetValue($"buff:{buffId}", out var present);
            bool actual = present != 0d;
            bool ok = actual == (s.ExpectedBool ?? true);
            return new StateResult(s.Alias, s.Property, ok, $"buff {buffId} present={actual}");
        }

        // 数值属性
        if (!actor.TryGetValue(s.Property.ToLowerInvariant(), out var actualVal))
            return new StateResult(s.Alias, s.Property, false, $"property {s.Property} 未观测到");

        var expected = s.ExpectedFloat ?? 0d;
        var tol = s.Tolerance?.X ?? 0d;
        var cmp = (s.Comparator ?? "eq").ToLowerInvariant();
        bool passed = cmp switch
        {
            "eq" => Math.Abs(actualVal - expected) <= (tol > 0 ? tol : Math.Max(1e-6, Math.Abs(expected) * 1e-4)),
            "ne" or "neq" => Math.Abs(actualVal - expected) > tol,
            "gt" => actualVal > expected,
            "gte" or "ge" => actualVal >= expected,
            "lt" => actualVal < expected,
            "lte" or "le" => actualVal <= expected,
            _ => false,
        };
        return new StateResult(s.Alias, s.Property, passed, $"actual={actualVal} expected={expected} cmp={cmp} tol={tol}");
    }

    private static long FindRootId(IReadOnlyList<TraceRecord> trace, string kind, long configId)
    {
        foreach (var r in trace)
            if (r.Kind == kind && r.ConfigId == configId) return r.RootId;
        return 0;
    }

    private static int Count(IReadOnlyList<TraceRecord> trace, string kind, long configId, long rootId)
    {
        var n = 0;
        foreach (var r in trace)
            if (r.Kind == kind && r.ConfigId == configId && (rootId <= 0 || r.RootId == rootId)) n++;
        return n;
    }

    private static bool Contains(IReadOnlyList<TraceRecord> trace, string kind, long configId, long rootId)
        => Count(trace, kind, configId, rootId) > 0;

    private static bool HasRelationship(IReadOnlyList<TraceRecord> trace, Relationship rel)
    {
        foreach (var parent in trace)
        {
            if (parent.Kind != rel.ParentKind || parent.ConfigId != rel.ParentConfigId) continue;
            foreach (var child in trace)
                if (child.Kind == rel.ChildKind && child.ConfigId == rel.ChildConfigId && child.RootId == parent.RootId)
                    return true;
        }
        return false;
    }
}

// canonical summary 形态（与现有 *_summary.json 对齐 + 扩展字段）。
public sealed record AcceptanceSummary
{
    public required string CaseId { get; init; }
    public string? WorldId { get; init; }
    public int TickRate { get; init; }
    public bool Accelerated { get; init; }
    public string? Category { get; init; }
    public string[] Tags { get; init; } = [];
    public required string ObservationSource { get; init; }
    public required AcceptanceResult Result { get; init; }
    public required AcceptanceCoverage Coverage { get; init; }
    public StateResult[] State { get; init; } = [];
}

public sealed record AcceptanceResult
{
    public required bool Passed { get; init; }       // canonical
    public required bool AllPassed { get; init; }     // 含 state 族（扩展）
    public required bool StatePassed { get; init; }
    public bool SkillCastTraceFound { get; init; }
    public bool EffectExecutionTraceFound { get; init; }
    public bool AllExpectedActionsExecuted { get; init; }
    public long EffectRootId { get; init; }
    public int TraceNodeCount { get; init; }
    public int ExpectedTraceNodeCount { get; init; }
    public int MatchedExpectedTraceNodeCount { get; init; }
    public int MissingExpectedTraceNodeCount { get; init; }
    public int ExpectedActionCount { get; init; }
    public int ExecutedExpectedActionCount { get; init; }
    public int ExpectedRelationshipCount { get; init; }
    public int SatisfiedRelationshipCount { get; init; }
}

public sealed record AcceptanceCoverage
{
    public bool AllRequiredTraceNodesMatched { get; init; }
    public bool AllForbiddenTraceNodesAbsent { get; init; }
    public bool AllExpectedActionsExecuted { get; init; }
    public bool AllRelationshipsSatisfied { get; init; }
    public string MissingTraceNodes { get; init; } = "";
    public string UnexpectedTraceNodes { get; init; } = "";
    public string MissingActions { get; init; } = "";
    public string MissingRelationships { get; init; } = "";
}

public sealed record StateResult(string Alias, string Property, bool Passed, string Detail);
