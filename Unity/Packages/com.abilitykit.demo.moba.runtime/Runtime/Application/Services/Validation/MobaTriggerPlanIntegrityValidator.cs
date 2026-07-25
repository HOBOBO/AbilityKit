using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Payload;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Config.Plans;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Runtime.Plan.Json;

namespace AbilityKit.Demo.Moba.Services
{
    public sealed class MobaTriggerPlanIntegrityValidator : IMobaRuntimeValidator
    {
        public const string SourceName = "trigger.plan.integrity";

        public string Name => SourceName;

        public void Validate(in MobaRuntimeValidationContext context, MobaRuntimeValidationReport report)
        {
            if (report == null) return;

            if (!context.TryResolve<TriggerPlanJsonDatabase>(out var database) || database == null)
            {
                report.Error(SourceName, "trigger.database", "TriggerPlanJsonDatabase is required for trigger plan integrity validation.", nameof(TriggerPlanJsonDatabase), blocksStartup: true, category: MobaRuntimeValidationCategory.Config);
                return;
            }

            context.TryResolve<MobaEventSubscriptionRegistry>(out var eventRegistry);
            context.TryResolve<IPayloadAccessorRegistry>(out var payloadRegistry);
            ValidateDatabase(database, eventRegistry, payloadRegistry, report);
        }

        public static void ValidateDatabase(
            TriggerPlanJsonDatabase database,
            MobaEventSubscriptionRegistry eventRegistry,
            IPayloadAccessorRegistry payloadRegistry,
            MobaRuntimeValidationReport report)
        {
            if (report == null) return;
            var records = database?.Records;
            if (records == null || records.Count == 0)
            {
                report.Error(SourceName, "trigger.database.records", "TriggerPlanJsonDatabase has no records; configured trigger plans cannot execute during battle bootstrap.", nameof(TriggerPlanJsonDatabase), code: "moba.trigger.plan.empty", category: MobaRuntimeValidationCategory.Config);
                return;
            }

            var ids = new HashSet<int>();
            for (int i = 0; i < records.Count; i++)
            {
                var record = records[i];
                var path = $"trigger.records[{i}].{record.TriggerId}";
                ValidateRecord(database, eventRegistry, payloadRegistry, record, path, ids, report);
            }
        }

        private static void ValidateRecord(
            TriggerPlanJsonDatabase database,
            MobaEventSubscriptionRegistry eventRegistry,
            IPayloadAccessorRegistry payloadRegistry,
            TriggerPlanJsonDatabase.Record record,
            string path,
            HashSet<int> ids,
            MobaRuntimeValidationReport report)
        {
            var businessId = record.TriggerId > 0 ? record.TriggerId.ToString() : null;
            if (record.TriggerId <= 0)
            {
                report.Error(SourceName, path + ".triggerId", "trigger id must be positive.", businessId, category: MobaRuntimeValidationCategory.Config);
                return;
            }

            if (!ids.Add(record.TriggerId))
            {
                report.Error(SourceName, path + ".triggerId", "duplicated trigger id in TriggerPlanJsonDatabase records.", businessId, code: "moba.trigger.plan.duplicate_id", category: MobaRuntimeValidationCategory.Config, businessNumericId: record.TriggerId);
            }

            if (!database.TryGetRecordByTriggerId(record.TriggerId, out _))
            {
                report.Error(SourceName, path + ".index", "trigger record is not reachable from TriggerPlanJsonDatabase.TryGetRecordByTriggerId; database indexes are inconsistent.", businessId, code: "moba.trigger.plan.index_missing", category: MobaRuntimeValidationCategory.Config, businessNumericId: record.TriggerId);
            }

            if (!database.TryGetPlanByTriggerId(record.TriggerId, out _))
            {
                report.Error(SourceName, path + ".planIndex", "trigger plan is not reachable from TriggerPlanJsonDatabase.TryGetPlanByTriggerId; effect execution cannot resolve this trigger.", businessId, code: "moba.trigger.plan.plan_index_missing", category: MobaRuntimeValidationCategory.Config, businessNumericId: record.TriggerId);
            }

            ValidateScopeAndEvent(record, eventRegistry, path, businessId, report);
            var argsType = ResolveEventArgsType(record, eventRegistry);
            ValidatePlan(record.Plan, argsType, payloadRegistry, path + ".plan", businessId, report);
        }

        private static void ValidateScopeAndEvent(TriggerPlanJsonDatabase.Record record, MobaEventSubscriptionRegistry eventRegistry, string path, string businessId, MobaRuntimeValidationReport report)
        {
            if (!Enum.IsDefined(typeof(TriggerPlanScope), record.Scope))
            {
                report.Error(SourceName, path + ".scope", $"trigger plan scope is not recognized. scope={record.Scope}", businessId, code: "moba.trigger.plan.invalid_scope", category: MobaRuntimeValidationCategory.Config, businessNumericId: record.TriggerId);
            }

            var hasEventName = !string.IsNullOrWhiteSpace(record.EventName);
            var hasEventId = record.EventId != 0;
            if (!hasEventName && !hasEventId)
            {
                // Eventless records are explicit plans resolved and executed by trigger id.
                return;
            }

            if (!hasEventName)
            {
                report.Error(SourceName, path + ".eventName", $"{record.Scope} trigger plan with an event id must declare event name.", businessId, code: "moba.trigger.plan.event_name_missing", category: MobaRuntimeValidationCategory.Config, businessNumericId: record.TriggerId);
                return;
            }

            if (!hasEventId)
            {
                report.Error(SourceName, path + ".eventId", $"{record.Scope} trigger plan with an event name must declare non-zero event id.", businessId, code: "moba.trigger.plan.event_id_missing", category: MobaRuntimeValidationCategory.Config, businessNumericId: record.TriggerId);
                return;
            }

            if (eventRegistry == null)
            {
                report.Warning(SourceName, path + ".eventRegistry", "MobaEventSubscriptionRegistry is not resolved; event args type cannot be checked.", businessId, code: "moba.trigger.plan.event_registry_missing", category: MobaRuntimeValidationCategory.Config, businessNumericId: record.TriggerId);
                return;
            }

            if (!eventRegistry.TryGetArgsType(record.EventName, out var argsType) || argsType == null)
            {
                report.Error(SourceName, path + ".eventName", $"trigger event is not registered. eventName={record.EventName}", businessId, code: "moba.trigger.plan.event_unregistered", category: MobaRuntimeValidationCategory.Config, businessNumericId: record.TriggerId);
            }
        }

        private static Type ResolveEventArgsType(
            TriggerPlanJsonDatabase.Record record,
            MobaEventSubscriptionRegistry eventRegistry)
        {
            if (eventRegistry == null || string.IsNullOrWhiteSpace(record.EventName)) return null;
            return eventRegistry.TryGetArgsType(record.EventName, out var argsType) ? argsType : null;
        }

        private static void ValidatePlan(
            TriggerPlan<object> plan,
            Type argsType,
            IPayloadAccessorRegistry payloadRegistry,
            string path,
            string businessId,
            MobaRuntimeValidationReport report)
        {
            ValidateNumericRef(plan.PredicateArg0, argsType, payloadRegistry, path + ".predicate.arg0", businessId, report);
            ValidateNumericRef(plan.PredicateArg1, argsType, payloadRegistry, path + ".predicate.arg1", businessId, report);
            ValidatePredicateExpr(plan.PredicateExpr, argsType, payloadRegistry, path + ".predicateExpr", businessId, report);
            ValidateActions(plan.Actions, argsType, payloadRegistry, path + ".actions", businessId, report);
        }

        private static void ValidatePredicateExpr(
            PredicateExprPlan expr,
            Type argsType,
            IPayloadAccessorRegistry payloadRegistry,
            string path,
            string businessId,
            MobaRuntimeValidationReport report)
        {
            var nodes = expr.Nodes;
            if (nodes == null) return;

            for (int i = 0; i < nodes.Length; i++)
            {
                var node = nodes[i];
                ValidateNumericRef(node.Left, argsType, payloadRegistry, $"{path}.nodes[{i}].left", businessId, report);
                ValidateNumericRef(node.Right, argsType, payloadRegistry, $"{path}.nodes[{i}].right", businessId, report);
            }
        }

        private static void ValidateActions(
            ActionCallPlan[] actions,
            Type argsType,
            IPayloadAccessorRegistry payloadRegistry,
            string path,
            string businessId,
            MobaRuntimeValidationReport report)
        {
            if (actions == null || actions.Length == 0)
            {
                report.Warning(SourceName, path, "trigger plan has no direct actions; this is valid only when ExecutionRoot supplies executable behavior.", businessId, code: "moba.trigger.plan.no_actions", category: MobaRuntimeValidationCategory.Config);
                return;
            }

            for (int i = 0; i < actions.Length; i++)
            {
                var action = actions[i];
                var actionPath = $"{path}[{i}]";
                if (action.Id.Value == 0)
                {
                    report.Error(SourceName, actionPath + ".id", "action id must be non-zero.", businessId, code: "moba.trigger.plan.action_id_missing", category: MobaRuntimeValidationCategory.Config);
                    continue;
                }

                ValidateActionSchedule(action, actionPath, businessId, report);
                ValidateActionArgs(action, argsType, payloadRegistry, actionPath, businessId, report);
            }
        }

        private static void ValidateActionSchedule(ActionCallPlan action, string path, string businessId, MobaRuntimeValidationReport report)
        {
            if (action.ScheduleParam < 0f)
            {
                report.Error(SourceName, path + ".scheduleParam", "action schedule parameter cannot be negative.", businessId, code: "moba.trigger.plan.action_schedule_negative", category: MobaRuntimeValidationCategory.Config);
            }

            if (action.MaxExecutions == 0)
            {
                report.Warning(SourceName, path + ".maxExecutions", "action max executions is zero; action will never execute.", businessId, code: "moba.trigger.plan.action_zero_execution", category: MobaRuntimeValidationCategory.Config);
            }
        }

        private static void ValidateActionArgs(
            ActionCallPlan action,
            Type argsType,
            IPayloadAccessorRegistry payloadRegistry,
            string path,
            string businessId,
            MobaRuntimeValidationReport report)
        {
            if (!ActionSchemaRegistry.TryGet(action.Id, out var schema) || schema == null)
            {
                report.Error(SourceName, path + ".schema", $"action schema is not registered. actionId={action.Id.Value}", businessId, code: "moba.trigger.plan.action_schema_missing", category: MobaRuntimeValidationCategory.Config);
                return;
            }

            var args = ToArgsArray(action);
            var span = new ReadOnlySpan<KeyValuePair<string, ActionArgValue>>(args);
            if (!schema.TryValidateArgs(span, out var error))
            {
                report.Error(SourceName, path + ".args", $"action args invalid. actionId={action.Id.Value}, error={error}", businessId, code: "moba.trigger.plan.action_args_invalid", category: MobaRuntimeValidationCategory.Config);
            }

            for (int i = 0; i < args.Length; i++)
            {
                ValidateNumericRef(args[i].Value.Ref, argsType, payloadRegistry, path + ".args." + args[i].Key, businessId, report);
            }
        }

        private static KeyValuePair<string, ActionArgValue>[] ToArgsArray(ActionCallPlan action)
        {
            if (action.Args != null && action.Args.Count > 0)
            {
                var args = new KeyValuePair<string, ActionArgValue>[action.Args.Count];
                var index = 0;
                foreach (var pair in action.Args)
                {
                    args[index++] = pair;
                }

                return args;
            }

            if (action.Arity <= 0)
            {
                return Array.Empty<KeyValuePair<string, ActionArgValue>>();
            }

            var positional = new List<KeyValuePair<string, ActionArgValue>>(Math.Min(action.Arity, (byte)2));
            positional.Add(new KeyValuePair<string, ActionArgValue>("arg0", ActionArgValue.Of(action.Arg0, "arg0")));
            if (action.Arity > 1)
            {
                positional.Add(new KeyValuePair<string, ActionArgValue>("arg1", ActionArgValue.Of(action.Arg1, "arg1")));
            }

            return positional.ToArray();
        }

        private static void ValidateNumericRef(
            NumericValueRef valueRef,
            Type argsType,
            IPayloadAccessorRegistry payloadRegistry,
            string path,
            string businessId,
            MobaRuntimeValidationReport report)
        {
            switch (valueRef.Kind)
            {
                case ENumericValueRefKind.PayloadField:
                    if (valueRef.FieldId == 0)
                    {
                        report.Error(SourceName, path, "payload field ref must declare non-zero field id.", businessId, code: "moba.trigger.plan.payload_field_missing", category: MobaRuntimeValidationCategory.Config);
                    }
                    else if (argsType != null
                             && payloadRegistry != null
                             && payloadRegistry.TryIsFieldSupported(argsType, valueRef.FieldId, out var supported)
                             && !supported)
                    {
                        report.Error(
                            SourceName,
                            path,
                            $"payload field is not supported by trigger event args. argsType={argsType.Name}, fieldId={valueRef.FieldId}",
                            businessId,
                            code: "moba.trigger.plan.payload_field_incompatible",
                            category: MobaRuntimeValidationCategory.Config);
                    }
                    break;
                case ENumericValueRefKind.Blackboard:
                    if (valueRef.BoardId == 0 || valueRef.KeyId == 0)
                    {
                        report.Warning(SourceName, path, "blackboard ref should declare non-zero board id and key id.", businessId, code: "moba.trigger.plan.blackboard_ref_incomplete", category: MobaRuntimeValidationCategory.Config);
                    }
                    break;
            }
        }
    }
}
