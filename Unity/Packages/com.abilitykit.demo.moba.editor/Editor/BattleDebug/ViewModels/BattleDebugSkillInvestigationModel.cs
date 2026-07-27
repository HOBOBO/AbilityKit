using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Diagnostics;

namespace AbilityKit.Game.Editor
{
    internal enum BattleDebugInvestigationConfidence
    {
        InsufficientEvidence = 0,
        Inferred = 1,
        Confirmed = 2
    }

    internal enum BattleDebugInvestigationCause
    {
        Unknown = 0,
        TriggerConditionFailed = 1,
        TriggerBudgetBlocked = 2,
        TriggerPlanRejected = 3,
        TriggerExecutionFailed = 4,
        EffectExecutionFailed = 5,
        RuntimeFailed = 6,
        SkillFailure = 7
    }

    internal enum BattleDebugInvestigationConfidenceFilter
    {
        All = 0,
        Confirmed = 1,
        Inferred = 2,
        InsufficientEvidence = 3
    }

    internal enum BattleDebugInvestigationCauseFilter
    {
        All = 0,
        SkillFailure = 1,
        TriggerConditionFailed = 2,
        TriggerBudgetBlocked = 3,
        TriggerPlanRejected = 4,
        TriggerExecutionFailed = 5,
        EffectExecutionFailed = 6,
        RuntimeFailed = 7,
        Unknown = 8
    }

    /// <summary>
    /// 一个可操作的技能失败调查案例。该投影只解释已采集到的诊断证据，
    /// 不替代权威游戏状态，也不会把缺少关联 ID 的独立事件推断为同一条链路。
    /// </summary>
    internal readonly struct BattleDebugSkillInvestigationCase
    {
        public BattleDebugSkillInvestigationCase(
            string key,
            BattleDebugInvestigationCause cause,
            BattleDebugInvestigationConfidence confidence,
            string conclusion,
            string evidenceSummary,
            int firstFrame,
            int lastFrame,
            long rootContextId,
            long contextId,
            long sourceActorId,
            long targetActorId,
            int configId,
            BattleDiagnosticRuntimeHandle skillRuntime,
            IReadOnlyList<BattleDiagnosticEvent> evidence)
        {
            Key = key ?? string.Empty;
            Cause = cause;
            Confidence = confidence;
            Conclusion = conclusion ?? string.Empty;
            EvidenceSummary = evidenceSummary ?? string.Empty;
            FirstFrame = firstFrame;
            LastFrame = lastFrame;
            RootContextId = rootContextId;
            ContextId = contextId;
            SourceActorId = sourceActorId;
            TargetActorId = targetActorId;
            ConfigId = configId;
            SkillRuntime = skillRuntime;
            Evidence = evidence ?? Array.Empty<BattleDiagnosticEvent>();
        }

        public string Key { get; }
        public BattleDebugInvestigationCause Cause { get; }
        public BattleDebugInvestigationConfidence Confidence { get; }
        public string Conclusion { get; }
        public string EvidenceSummary { get; }
        public int FirstFrame { get; }
        public int LastFrame { get; }
        public long RootContextId { get; }
        public long ContextId { get; }
        public long SourceActorId { get; }
        public long TargetActorId { get; }
        public int ConfigId { get; }
        public BattleDiagnosticRuntimeHandle SkillRuntime { get; }
        public IReadOnlyList<BattleDiagnosticEvent> Evidence { get; }
        public bool CanOpenTrace => RootContextId > 0;
    }

    /// <summary>
    /// 将失败诊断事件投影为案例。根 Trace 是唯一的跨事件聚合键；没有根 Trace
    /// 时每一个失败事件单独成案，确保不会因为 Actor/配置相同而误归因。
    /// </summary>
    internal static class BattleDebugSkillInvestigationModel
    {
        public static IReadOnlyList<BattleDebugSkillInvestigationCase> Build(
            IReadOnlyList<BattleDiagnosticEvent> events,
            int maximumCases = 24)
        {
            return Build(
                events,
                BattleDebugInvestigationConfidenceFilter.All,
                BattleDebugInvestigationCauseFilter.All,
                maximumCases);
        }

        public static IReadOnlyList<BattleDebugSkillInvestigationCase> Build(
            IReadOnlyList<BattleDiagnosticEvent> events,
            BattleDebugInvestigationConfidenceFilter confidenceFilter,
            BattleDebugInvestigationCauseFilter causeFilter,
            int maximumCases = 24)
        {
            if (events == null || events.Count == 0 || maximumCases <= 0)
            {
                return Array.Empty<BattleDebugSkillInvestigationCase>();
            }

            var issueRoots = new HashSet<long>();
            for (var i = 0; i < events.Count; i++)
            {
                var item = events[i];
                if (item.RootContextId > 0 && IsInvestigable(in item))
                {
                    issueRoots.Add(item.RootContextId);
                }
            }

            var builders = new Dictionary<string, CaseBuilder>(StringComparer.Ordinal);
            for (var i = 0; i < events.Count; i++)
            {
                var item = events[i];
                var belongsToFailedRoot = item.RootContextId > 0 && issueRoots.Contains(item.RootContextId);
                if (!belongsToFailedRoot && !IsInvestigable(in item)) continue;

                var key = item.RootContextId > 0
                    ? "root:" + item.RootContextId
                    : "event:" + item.Sequence;
                if (!builders.TryGetValue(key, out var builder))
                {
                    builder = new CaseBuilder(key);
                    builders.Add(key, builder);
                }

                builder.Add(in item);
            }

            if (builders.Count == 0) return Array.Empty<BattleDebugSkillInvestigationCase>();

            var cases = new List<BattleDebugSkillInvestigationCase>(builders.Count);
            foreach (var pair in builders)
            {
                var investigation = pair.Value.Build();
                if (MatchesFilters(in investigation, confidenceFilter, causeFilter))
                {
                    cases.Add(investigation);
                }
            }

            cases.Sort(CompareCases);
            if (cases.Count > maximumCases)
            {
                cases.RemoveRange(maximumCases, cases.Count - maximumCases);
            }

            return cases;
        }

        private static bool MatchesFilters(
            in BattleDebugSkillInvestigationCase investigation,
            BattleDebugInvestigationConfidenceFilter confidenceFilter,
            BattleDebugInvestigationCauseFilter causeFilter)
        {
            var confidenceMatches = confidenceFilter == BattleDebugInvestigationConfidenceFilter.All ||
                                    (confidenceFilter == BattleDebugInvestigationConfidenceFilter.Confirmed &&
                                     investigation.Confidence == BattleDebugInvestigationConfidence.Confirmed) ||
                                    (confidenceFilter == BattleDebugInvestigationConfidenceFilter.Inferred &&
                                     investigation.Confidence == BattleDebugInvestigationConfidence.Inferred) ||
                                    (confidenceFilter == BattleDebugInvestigationConfidenceFilter.InsufficientEvidence &&
                                     investigation.Confidence == BattleDebugInvestigationConfidence.InsufficientEvidence);
            if (!confidenceMatches) return false;

            return causeFilter == BattleDebugInvestigationCauseFilter.All ||
                   (causeFilter == BattleDebugInvestigationCauseFilter.SkillFailure &&
                    investigation.Cause == BattleDebugInvestigationCause.SkillFailure) ||
                   (causeFilter == BattleDebugInvestigationCauseFilter.TriggerConditionFailed &&
                    investigation.Cause == BattleDebugInvestigationCause.TriggerConditionFailed) ||
                   (causeFilter == BattleDebugInvestigationCauseFilter.TriggerBudgetBlocked &&
                    investigation.Cause == BattleDebugInvestigationCause.TriggerBudgetBlocked) ||
                   (causeFilter == BattleDebugInvestigationCauseFilter.TriggerPlanRejected &&
                    investigation.Cause == BattleDebugInvestigationCause.TriggerPlanRejected) ||
                   (causeFilter == BattleDebugInvestigationCauseFilter.TriggerExecutionFailed &&
                    investigation.Cause == BattleDebugInvestigationCause.TriggerExecutionFailed) ||
                   (causeFilter == BattleDebugInvestigationCauseFilter.EffectExecutionFailed &&
                    investigation.Cause == BattleDebugInvestigationCause.EffectExecutionFailed) ||
                   (causeFilter == BattleDebugInvestigationCauseFilter.RuntimeFailed &&
                    investigation.Cause == BattleDebugInvestigationCause.RuntimeFailed) ||
                   (causeFilter == BattleDebugInvestigationCauseFilter.Unknown &&
                    investigation.Cause == BattleDebugInvestigationCause.Unknown);
        }

        private static bool IsInvestigable(in BattleDiagnosticEvent item)
        {
            if (item.IsFailure) return true;
            return item.Payload.TryGetTriggerAnalysis(out var trigger) &&
                   (trigger.Result == BattleDiagnosticTriggerAnalysisResult.Failed ||
                    trigger.Result == BattleDiagnosticTriggerAnalysisResult.Blocked);
        }

        private static int CompareCases(BattleDebugSkillInvestigationCase left, BattleDebugSkillInvestigationCase right)
        {
            var frame = right.LastFrame.CompareTo(left.LastFrame);
            if (frame != 0) return frame;
            var confidence = right.Confidence.CompareTo(left.Confidence);
            if (confidence != 0) return confidence;
            return string.CompareOrdinal(left.Key, right.Key);
        }

        private sealed class CaseBuilder
        {
            private readonly string _key;
            private readonly List<BattleDiagnosticEvent> _evidence = new List<BattleDiagnosticEvent>();
            private int _firstFrame = int.MaxValue;
            private int _lastFrame = BattleDiagnosticFrames.Invalid;
            private long _rootContextId;
            private long _contextId;
            private long _sourceActorId;
            private long _targetActorId;
            private int _configId;
            private BattleDiagnosticRuntimeHandle _skillRuntime;

            public CaseBuilder(string key)
            {
                _key = key;
            }

            public void Add(in BattleDiagnosticEvent item)
            {
                _evidence.Add(item);
                if (item.Frame < _firstFrame) _firstFrame = item.Frame;
                if (item.Frame > _lastFrame) _lastFrame = item.Frame;
                if (_rootContextId == 0) _rootContextId = item.RootContextId;
                if (_contextId == 0) _contextId = item.ContextId;
                if (_sourceActorId == 0) _sourceActorId = item.SourceActorId;
                if (_targetActorId == 0) _targetActorId = item.TargetActorId;
                if (_configId == 0) _configId = item.ConfigId;
                if (!_skillRuntime.IsValid && item.SkillRuntime.IsValid) _skillRuntime = item.SkillRuntime;
            }

            public BattleDebugSkillInvestigationCase Build()
            {
                var classification = Classify(_evidence);
                return new BattleDebugSkillInvestigationCase(
                    _key,
                    classification.Cause,
                    classification.Confidence,
                    classification.Conclusion,
                    BuildEvidenceSummary(_evidence, classification.Reason),
                    _firstFrame == int.MaxValue ? BattleDiagnosticFrames.Invalid : _firstFrame,
                    _lastFrame,
                    _rootContextId,
                    _contextId,
                    _sourceActorId,
                    _targetActorId,
                    _configId,
                    _skillRuntime,
                    _evidence);
            }

            private static Classification Classify(IReadOnlyList<BattleDiagnosticEvent> evidence)
            {
                for (var i = 0; i < evidence.Count; i++)
                {
                    var item = evidence[i];
                    if (!item.Payload.TryGetTriggerAnalysis(out var trigger)) continue;

                    if (trigger.Stage == BattleDiagnosticTriggerAnalysisStage.Conditions &&
                        trigger.Result == BattleDiagnosticTriggerAnalysisResult.Failed)
                    {
                        return new Classification(
                            BattleDebugInvestigationCause.TriggerConditionFailed,
                            BattleDebugInvestigationConfidence.Confirmed,
                            "触发条件未通过",
                            string.IsNullOrEmpty(trigger.Reason) ? trigger.FailureKey : trigger.Reason);
                    }

                    if (trigger.Stage == BattleDiagnosticTriggerAnalysisStage.Budget &&
                        trigger.Result == BattleDiagnosticTriggerAnalysisResult.Blocked)
                    {
                        return new Classification(
                            BattleDebugInvestigationCause.TriggerBudgetBlocked,
                            BattleDebugInvestigationConfidence.Confirmed,
                            "触发预算阻断",
                            string.IsNullOrEmpty(trigger.Reason) ? trigger.FailureKey : trigger.Reason);
                    }

                    if (trigger.Stage == BattleDiagnosticTriggerAnalysisStage.Plan &&
                        (trigger.Result == BattleDiagnosticTriggerAnalysisResult.Failed ||
                         trigger.Result == BattleDiagnosticTriggerAnalysisResult.Blocked))
                    {
                        return new Classification(
                            BattleDebugInvestigationCause.TriggerPlanRejected,
                            BattleDebugInvestigationConfidence.Confirmed,
                            "触发计划未执行",
                            string.IsNullOrEmpty(trigger.Reason) ? trigger.FailureKey : trigger.Reason);
                    }

                    if (trigger.Stage == BattleDiagnosticTriggerAnalysisStage.Execution &&
                        (trigger.Result == BattleDiagnosticTriggerAnalysisResult.Failed ||
                         trigger.Result == BattleDiagnosticTriggerAnalysisResult.Blocked))
                    {
                        return new Classification(
                            BattleDebugInvestigationCause.TriggerExecutionFailed,
                            BattleDebugInvestigationConfidence.Confirmed,
                            "触发执行失败",
                            string.IsNullOrEmpty(trigger.Reason) ? trigger.FailureKey : trigger.Reason);
                    }
                }

                for (var i = 0; i < evidence.Count; i++)
                {
                    var item = evidence[i];
                    if (!item.Payload.TryGetSkillFailure(out var failure)) continue;

                    var conclusion = string.IsNullOrEmpty(failure.Code)
                        ? "技能请求失败"
                        : $"技能请求失败：{failure.Code}";
                    var reason = string.IsNullOrEmpty(failure.Message)
                        ? $"{failure.Source}.{failure.Stage}".TrimEnd('.')
                        : failure.Message;
                    return new Classification(
                        BattleDebugInvestigationCause.SkillFailure,
                        BattleDebugInvestigationConfidence.Confirmed,
                        conclusion,
                        reason);
                }

                for (var i = 0; i < evidence.Count; i++)
                {
                    var item = evidence[i];
                    if (item.Kind == BattleDiagnosticEventKind.EffectEnded && item.IsFailure)
                    {
                        return new Classification(
                            BattleDebugInvestigationCause.EffectExecutionFailed,
                            BattleDebugInvestigationConfidence.Inferred,
                            "效果执行未完成",
                            item.Summary);
                    }

                    if (item.Kind == BattleDiagnosticEventKind.SkillRuntimeEnded && item.IsFailure)
                    {
                        return new Classification(
                            BattleDebugInvestigationCause.RuntimeFailed,
                            BattleDebugInvestigationConfidence.Inferred,
                            "技能运行时异常结束",
                            item.Summary);
                    }
                }

                return new Classification(
                    BattleDebugInvestigationCause.Unknown,
                    BattleDebugInvestigationConfidence.InsufficientEvidence,
                    "检测到失败，但缺少可确认的根因证据",
                    string.Empty);
            }

            private static string BuildEvidenceSummary(
                IReadOnlyList<BattleDiagnosticEvent> evidence,
                string reason)
            {
                var first = evidence.Count > 0 ? evidence[0] : default;
                var summary = $"{evidence.Count} 条事件";
                if (first.RootContextId > 0) summary += $"；Root Trace={first.RootContextId}";
                if (first.ConfigId != 0) summary += $"；Cfg={first.ConfigId}";
                if (!string.IsNullOrEmpty(reason)) summary += $"；{reason}";
                return summary;
            }
        }

        private readonly struct Classification
        {
            public Classification(
                BattleDebugInvestigationCause cause,
                BattleDebugInvestigationConfidence confidence,
                string conclusion,
                string reason)
            {
                Cause = cause;
                Confidence = confidence;
                Conclusion = conclusion;
                Reason = reason;
            }

            public BattleDebugInvestigationCause Cause { get; }
            public BattleDebugInvestigationConfidence Confidence { get; }
            public string Conclusion { get; }
            public string Reason { get; }
        }
    }
}
