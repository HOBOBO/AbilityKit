using System;

namespace AbilityKit.Triggering.Runtime.Plan.Json
{
    /// <summary>
    /// 为单条 JSON 记录组装扁平计划与执行根，两者共用该记录的模板绑定。
    /// </summary>
    internal static class TriggerPlanConverter
    {
        internal static (TriggerPlan<object> Plan, ITriggerPlanExecutable ExecutionRoot) Convert(
            TriggerPlanJsonDatabase.TriggerPlanDto dto,
            Lazy<TriggerPlanExecutionNodeReferenceCatalog> referenceCatalog,
            ITriggerCue cue)
        {
            var values = new TriggerPlanJsonValueConverter(dto.Template);
            var plan = ConvertPlan(dto, cue, in values);
            var executionRoot = TriggerPlanExecutionNodeConverter.ConvertExecutionRoot(dto, in values, referenceCatalog);
            return (plan, executionRoot);
        }

        private static TriggerPlan<object> ConvertPlan(
            TriggerPlanJsonDatabase.TriggerPlanDto dto, ITriggerCue cue, in TriggerPlanJsonValueConverter values)
        {
            var actions = TriggerPlanJsonActionConverter.ConvertActions(dto.Actions, in values);
            var predicate = TriggerPlanJsonPredicateConverter.ConvertPredicateExpr(dto.Predicate, in values);
            var executionControl = ConvertExecutionControl(dto.ExecutionControl);

            return predicate.Nodes == null
                ? new TriggerPlan<object>(dto.Phase, dto.Priority, dto.TriggerId,
                    actions: actions, cue: cue, executionControl: in executionControl)
                : new TriggerPlan<object>(dto.Phase, dto.Priority, dto.TriggerId,
                    predicateExpr: predicate, actions: actions, cue: cue, executionControl: in executionControl);
        }

        private static TriggerExecutionControlPlan ConvertExecutionControl(TriggerPlanJsonDatabase.ExecutionControlPlanDto dto)
        {
            if (dto == null || string.IsNullOrEmpty(dto.Mode))
            {
                return TriggerExecutionControlPlan.Always;
            }

            var modeText = dto.Mode.Trim().ToLowerInvariant();
            switch (modeText)
            {
                case "once":
                    return new TriggerExecutionControlPlan(ETriggerExecutionMode.Once, maxExecutions: 1);
                case "cooldown":
                    return new TriggerExecutionControlPlan(ETriggerExecutionMode.Cooldown, cooldownMs: Math.Max(0f, dto.CooldownMs));
                case "repeat":
                    return new TriggerExecutionControlPlan(ETriggerExecutionMode.Repeat, maxExecutions: Math.Max(0, dto.MaxExecutions));
                case "always":
                default:
                    return TriggerExecutionControlPlan.Always;
            }
        }
    }
}
