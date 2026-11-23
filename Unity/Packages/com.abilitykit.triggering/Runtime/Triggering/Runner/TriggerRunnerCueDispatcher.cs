using AbilityKit.Core.Eventing;

namespace AbilityKit.Triggering.Runtime
{
    /// <summary>
    /// 构建触发器级 Cue 上下文，并为触发器与计划动作解析 Cue 数据。
    /// </summary>
    internal static class TriggerRunnerCueDispatcher
    {
        public static TriggerCueContext BuildCueContext<TArgs, TCtx>(
            EventKey<TArgs> key,
            in TArgs args,
            int phase,
            int priority,
            long order,
            ITrigger<TArgs, TCtx> trigger,
            ShortCircuitReason reason,
            string interruptSourceName,
            int interruptTriggerId,
            bool interruptConditionPassed,
            ExecutionControl control,
            Config.ECueLevel cueLevel = Config.ECueLevel.Trigger,
            Config.ECueLifecycleStage cueStage = Config.ECueLifecycleStage.None,
            int actionIndex = -1,
            in TriggerCueDescriptor cueDescriptor = default,
            object cueData = null,
            string cuePayload = null)
        {
            ResolveCueData(in args, cueLevel, cueStage, actionIndex, in cueDescriptor, ref cueData, ref cuePayload);

            var triggerId = 0;
            var triggerTypeName = trigger?.GetType().Name ?? "Unknown";
            if (trigger is ITriggerWithId tid)
            {
                triggerId = tid.TriggerId;
            }

            return new TriggerCueContext(
                key.IntId,
                key.StringId,
                args,
                phase,
                priority,
                order,
                triggerId,
                triggerTypeName,
                MapReason(reason),
                interruptSourceName,
                interruptTriggerId,
                interruptConditionPassed,
                control,
                cueLevel == Config.ECueLevel.None ? Config.ECueLevel.Trigger : cueLevel,
                cueStage,
                actionIndex,
                in cueDescriptor,
                cueData,
                cuePayload);
        }

        public static void ResolveCueData<TArgs>(
            in TArgs args,
            Config.ECueLevel cueLevel,
            Config.ECueLifecycleStage cueStage,
            int actionIndex,
            in TriggerCueDescriptor cueDescriptor,
            ref object cueData,
            ref string cuePayload)
        {
            if (cueData != null) return;
            if (!(args is ITriggerCueDataProvider provider)) return;

            if (provider.TryGetCueData(cueLevel, cueStage, actionIndex, in cueDescriptor, out var providedData, out var providedPayload))
            {
                cueData = providedData;
                if (!string.IsNullOrEmpty(providedPayload)) cuePayload = providedPayload;
            }
        }

        public static ETriggerShortCircuitReason MapReason(ShortCircuitReason reason)
        {
            switch (reason)
            {
                case ShortCircuitReason.ConditionFailed:
                    return ETriggerShortCircuitReason.ConditionFailed;
                case ShortCircuitReason.StopPropagation:
                    return ETriggerShortCircuitReason.StopPropagation;
                case ShortCircuitReason.Cancel:
                    return ETriggerShortCircuitReason.Cancel;
                case ShortCircuitReason.InterruptedByHigherPriority:
                    return ETriggerShortCircuitReason.InterruptedByHigherPriority;
                case ShortCircuitReason.InterruptedByFailedCondition:
                    return ETriggerShortCircuitReason.InterruptedByFailedCondition;
                default:
                    return ETriggerShortCircuitReason.None;
            }
        }
    }
}
