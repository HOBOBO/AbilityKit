using System;
using System.Collections.Generic;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime.Config;

namespace AbilityKit.Triggering.Runtime.Plan.Json
{
    /// <summary>
    /// ? JSON ??????????????????????????? Cue ???
    /// </summary>
    internal static class TriggerPlanJsonActionConverter
    {
        internal static ActionCallPlan[] ConvertActions(
            List<TriggerPlanJsonDatabase.ActionCallPlanDto> dtos, in TriggerPlanJsonValueConverter values)
        {
            if (dtos == null || dtos.Count == 0) return Array.Empty<ActionCallPlan>();

            var arr = new ActionCallPlan[dtos.Count];
            for (int i = 0; i < dtos.Count; i++)
            {
                arr[i] = ConvertAction(dtos[i], in values);
            }
            return arr;
        }

        internal static ActionCallPlan ConvertAction(
            TriggerPlanJsonDatabase.ActionCallPlanDto dto, in TriggerPlanJsonValueConverter values)
        {
            if (dto == null) return default;

            var id = new ActionId(dto.ActionId);
            var scheduleMode = ParseActionScheduleMode(dto.ScheduleMode);
            var executionPolicy = ParseActionExecutionPolicy(dto.ExecutionPolicy);
            var retryMaxRetries = dto.RetryMaxRetries;

            if (dto.RetryMaxRetries < 0)
            {
                throw new InvalidOperationException($"RetryMaxRetries cannot be negative: {dto.RetryMaxRetries} actionId={dto.ActionId}");
            }

            if (dto.RetryDelayMs < 0f)
            {
                throw new InvalidOperationException($"RetryDelayMs cannot be negative: {dto.RetryDelayMs} actionId={dto.ActionId}");
            }

            var cueDescriptor = BuildActionCueDescriptor(dto);

            if (dto.Args != null && dto.Args.Count > 0)
            {
                if (dto.Arity > 2)
                {
                    throw new InvalidOperationException($"Unsupported named action arity: {dto.Arity} actionId={dto.ActionId}. PlannedTrigger currently supports arity 0/1/2; use Action schema named args for additional business parameters.");
                }

                var namedArgs = new Dictionary<string, ActionArgValue>(dto.Args.Count, StringComparer.OrdinalIgnoreCase);
                foreach (var pair in dto.Args)
                {
                    namedArgs[pair.Key] = values.ConvertActionArgument(pair.Value, pair.Key);
                }

                var arity = dto.Arity;
                var arg0 = arity > 0 ? values.ConvertNumeric(dto.Arg0) : default;
                var arg1 = arity > 1 ? values.ConvertNumeric(dto.Arg1) : default;
                return new ActionCallPlan(
                    id,
                    (byte)arity,
                    arg0,
                    arg1,
                    namedArgs,
                    scheduleMode,
                    dto.ScheduleParam,
                    dto.MaxExecutions,
                    dto.CanBeInterrupted,
                    executionPolicy,
                    retryMaxRetries,
                    dto.RetryDelayMs,
                    in cueDescriptor);
            }

            ActionCallPlan plan;
            switch (dto.Arity)
            {
                case 0:
                    plan = new ActionCallPlan(id);
                    break;
                case 1:
                    plan = new ActionCallPlan(id, values.ConvertNumeric(dto.Arg0));
                    break;
                case 2:
                    plan = new ActionCallPlan(id, values.ConvertNumeric(dto.Arg0), values.ConvertNumeric(dto.Arg1));
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported action arity: {dto.Arity} actionId={dto.ActionId}");
            }

            return new ActionCallPlan(
                plan.Id,
                plan.Arity,
                plan.Arg0,
                plan.Arg1,
                plan.Args,
                scheduleMode,
                dto.ScheduleParam,
                dto.MaxExecutions,
                dto.CanBeInterrupted,
                executionPolicy,
                retryMaxRetries,
                dto.RetryDelayMs,
                in cueDescriptor);
        }

        private static TriggerCueDescriptor BuildActionCueDescriptor(TriggerPlanJsonDatabase.ActionCallPlanDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.CueId)) return TriggerCueDescriptor.Empty;

            return new TriggerCueDescriptor(
                kind: dto.CueId,
                cueId: dto.CueId,
                primaryAssetId: dto.CuePrimaryAssetId,
                secondaryAssetId: dto.CueSecondaryAssetId,
                payload: dto.CuePayload,
                level: ECueLevel.Behavior);
        }

        private static EActionScheduleMode ParseActionScheduleMode(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return EActionScheduleMode.Immediate;
            }

            if (Enum.TryParse<EActionScheduleMode>(value, true, out var mode))
            {
                return mode;
            }

            throw new InvalidOperationException($"Unknown action schedule mode: {value}");
        }

        private static EActionExecutionPolicy ParseActionExecutionPolicy(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return EActionExecutionPolicy.Immediate;
            }

            if (Enum.TryParse<EActionExecutionPolicy>(value, true, out var policy))
            {
                return policy;
            }

            throw new InvalidOperationException($"Unknown action execution policy: {value}");
        }
    }
}
