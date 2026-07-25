using AbilityKit.Demo.Moba.Diagnostics;

namespace AbilityKit.Demo.Moba.Services
{
    /// <summary>
    /// Effect 执行生命周期诊断事件草稿生成器。
    /// 从 <see cref="MobaEffectExecutionService"/> 抽离为独立静态类，
    /// 避免诊断测试直接依赖 WorldService 基类所在的程序集链路。
    /// </summary>
    internal static class MobaEffectDiagnosticProducer
    {
        public static MobaBattleDiagnosticEventDraft CreateEffectStartedDraft(
            int effectConfigId,
            int triggerId,
            int sourceActorId,
            int targetActorId,
            long effectContextId,
            long rootContextId)
        {
            var resolvedRoot = rootContextId != 0L ? rootContextId : effectContextId;
            var summary = $"effectConfigId={effectConfigId}, triggerId={triggerId}, contextId={effectContextId}";

            return new MobaBattleDiagnosticEventDraft(
                BattleDiagnosticEventKind.EffectStarted,
                BattleDiagnosticEventChannel.Effect,
                BattleDiagnosticEventOutcome.None,
                sourceActorId,
                targetActorId,
                effectConfigId,
                resolvedRoot,
                effectContextId,
                summary: summary);
        }

        public static MobaBattleDiagnosticEventDraft CreateEffectEndedDraft(
            int effectConfigId,
            int triggerId,
            int sourceActorId,
            int targetActorId,
            long effectContextId,
            long rootContextId,
            bool executed)
        {
            var resolvedRoot = rootContextId != 0L ? rootContextId : effectContextId;
            var outcome = executed
                ? BattleDiagnosticEventOutcome.Succeeded
                : BattleDiagnosticEventOutcome.Failed;
            var summary = $"effectConfigId={effectConfigId}, triggerId={triggerId}, contextId={effectContextId}, executed={executed}";

            return new MobaBattleDiagnosticEventDraft(
                BattleDiagnosticEventKind.EffectEnded,
                BattleDiagnosticEventChannel.Effect,
                outcome,
                sourceActorId,
                targetActorId,
                effectConfigId,
                resolvedRoot,
                effectContextId,
                summary: summary);
        }

        public static MobaBattleDiagnosticEventDraft CreateTriggerAnalysisDraft(
            int triggerId,
            int contextKind,
            int originKind,
            BattleDiagnosticTriggerAnalysisStage stage,
            BattleDiagnosticTriggerAnalysisResult result,
            int sourceActorId,
            int targetActorId,
            long contextId,
            long rootContextId,
            int detailCode = 0,
            int currentDepth = 0,
            int currentFrameCount = 0,
            int currentRootCount = 0,
            int currentSameTriggerCount = 0,
            string failureKey = "",
            string reason = "")
        {
            var resolvedRoot = rootContextId != 0L ? rootContextId : contextId;
            var payloadData = new BattleDiagnosticTriggerAnalysisPayload(
                triggerId,
                contextKind,
                originKind,
                stage,
                result,
                detailCode,
                currentDepth,
                currentFrameCount,
                currentRootCount,
                currentSameTriggerCount,
                failureKey,
                reason);
            var payload = BattleDiagnosticEventPayload.FromTriggerAnalysis(in payloadData);
            var outcome = ResolveOutcome(result);
            var summary = $"triggerId={triggerId}, stage={stage}, result={result}, contextKind={contextKind}, originKind={originKind}";
            if (!string.IsNullOrEmpty(failureKey)) summary += $", failureKey={failureKey}";
            if (!string.IsNullOrEmpty(reason)) summary += $", reason={reason}";

            return new MobaBattleDiagnosticEventDraft(
                BattleDiagnosticEventKind.TriggerAnalysis,
                BattleDiagnosticEventChannel.Effect,
                outcome,
                sourceActorId,
                targetActorId,
                triggerId,
                resolvedRoot,
                contextId,
                payloadVersion: BattleDiagnosticTriggerAnalysisPayload.CurrentSchemaVersion,
                summary: summary,
                payload: payload);
        }

        private static BattleDiagnosticEventOutcome ResolveOutcome(
            BattleDiagnosticTriggerAnalysisResult result)
        {
            switch (result)
            {
                case BattleDiagnosticTriggerAnalysisResult.Passed:
                    return BattleDiagnosticEventOutcome.Succeeded;
                case BattleDiagnosticTriggerAnalysisResult.Blocked:
                case BattleDiagnosticTriggerAnalysisResult.Failed:
                    return BattleDiagnosticEventOutcome.Failed;
                default:
                    return BattleDiagnosticEventOutcome.None;
            }
        }
    }
}
