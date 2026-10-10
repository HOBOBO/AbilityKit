using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Runtime.Config.Plans;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Runtime.Plan.Json;

namespace AbilityKit.Demo.Moba.Gameplay.Triggering
{
    public sealed class MobaGameplayTriggerRuntimeValidator : IMobaRuntimeValidator
    {
        public string Name => "gameplay.trigger";

        public void Validate(in MobaRuntimeValidationContext context, MobaRuntimeValidationReport report)
        {
            if (report == null) return;

            context.TryResolve<TriggerPlanJsonDatabase>(out var db);
            context.TryResolve<MobaEventSubscriptionRegistry>(out var eventRegistry);
            MobaGameplayTriggerValidator.ValidateDatabase(db, eventRegistry, report);
        }
    }

    public static class MobaGameplayTriggerValidator
    {
        private const string Source = "gameplay.trigger";

        private static readonly HashSet<int> KnownPayloadFieldIds = new HashSet<int>
        {
            StableStringId.Get("payload:" + GameplayTriggerEvents.FrameIndexField),
            StableStringId.Get("payload:" + GameplayTriggerEvents.ElapsedSecondsField),
            StableStringId.Get("payload:" + GameplayTriggerEvents.DeltaSecondsField),
            StableStringId.Get("payload:" + GameplayTriggerEvents.WinTeamIdField),
            StableStringId.Get("payload:" + GameplayTriggerEvents.TeamIdField),
            StableStringId.Get("payload:" + GameplayTriggerEvents.ScoreField),
            MobaBattlePayloadFields.FieldId(MobaBattlePayloadFields.AttackerActorId),
            MobaBattlePayloadFields.FieldId(MobaBattlePayloadFields.TargetActorId),
            MobaBattlePayloadFields.FieldId(MobaBattlePayloadFields.DamageValue),
            MobaBattlePayloadFields.FieldId(MobaBattlePayloadFields.TargetHp),
            MobaBattlePayloadFields.FieldId(MobaBattlePayloadFields.TargetMaxHp),
            MobaBattlePayloadFields.FieldId(MobaBattlePayloadFields.DamageType),
            MobaBattlePayloadFields.FieldId(MobaBattlePayloadFields.CritType),
            MobaBattlePayloadFields.FieldId(MobaBattlePayloadFields.ReasonKind),
            MobaBattlePayloadFields.FieldId(MobaBattlePayloadFields.ReasonParam),
            MobaBattlePayloadFields.FieldId(MobaBattlePayloadFields.UnitActorId),
            MobaBattlePayloadFields.FieldId(MobaBattlePayloadFields.KillerActorId),
        };

        public static bool Validate(TriggerPlanJsonDatabase db, MobaEventSubscriptionRegistry eventRegistry)
        {
            var report = new MobaRuntimeValidationReport();
            ValidateDatabase(db, eventRegistry, report);
            Report(report);
            return !report.HasErrors;
        }

        public static void ValidateDatabase(TriggerPlanJsonDatabase db, MobaEventSubscriptionRegistry eventRegistry, MobaRuntimeValidationReport report)
        {
            if (report == null) return;

            if (db?.Records == null || db.Records.Count == 0)
            {
                report.Info(Source, "trigger.database", "gameplay trigger database is empty");
                return;
            }

            for (int i = 0; i < db.Records.Count; i++)
            {
                var record = db.Records[i];
                if (!IsGameplayRecord(record))
                {
                    continue;
                }

                ValidateRecord(record, eventRegistry, $"trigger[{record.TriggerId}:{record.EventName}]", report);
            }
        }

        public static bool ValidateGameplay(GameplayMO gameplay, TriggerPlanJsonDatabase db, MobaEventSubscriptionRegistry eventRegistry)
        {
            var report = new MobaRuntimeValidationReport();
            ValidateGameplay(gameplay, db, eventRegistry, report);
            Report(report);
            return !report.HasErrors;
        }

        public static void ValidateGameplay(GameplayMO gameplay, TriggerPlanJsonDatabase db, MobaEventSubscriptionRegistry eventRegistry, MobaRuntimeValidationReport report)
        {
            if (report == null) return;

            if (gameplay == null)
            {
                AddError(report, "gameplay", "gameplay config is null");
                return;
            }

            if (db == null)
            {
                AddError(report, $"gameplay[{gameplay.Id}]", "trigger database is null", gameplay.Id.ToString());
                return;
            }

            var triggerIds = gameplay.TriggerIds;
            if (triggerIds == null || triggerIds.Count == 0)
            {
                AddError(report, $"gameplay[{gameplay.Id}]", "gameplay has no trigger ids", gameplay.Id.ToString());
                return;
            }

            var seen = new HashSet<int>();
            var hasTimeExpiredRule = false;
            for (int i = 0; i < triggerIds.Count; i++)
            {
                var triggerId = triggerIds[i];
                var path = $"gameplay[{gameplay.Id}].triggerIds[{i}]={triggerId}";
                if (triggerId <= 0 || !seen.Add(triggerId))
                {
                    AddError(report, path, "invalid or duplicate trigger id", triggerId.ToString());
                    continue;
                }
                if (!_TryGetRecord(db, triggerId, out var record))
                {
                    AddError(report, path, "trigger id not found in trigger database", triggerId.ToString());
                    continue;
                }

                ValidateRecord(record, eventRegistry, path, report);
                if (record.EventName == GameplayTriggerEvents.TimeExpired &&
                    record.Plan.Actions != null)
                {
                    foreach (var action in record.Plan.Actions)
                    {
                        if (action.Id.Value == TriggeringConstants.EndGameId.Value)
                            hasTimeExpiredRule = true;
                    }
                }
            }
            if (gameplay.DefaultDurationMs > 0 && !hasTimeExpiredRule)
                AddError(report, $"gameplay[{gameplay.Id}]", "timed gameplay requires gameplay.time_expired trigger with end_game action", gameplay.Id.ToString());
        }

        private static bool _TryGetRecord(TriggerPlanJsonDatabase db, int triggerId, out TriggerPlanJsonDatabase.Record record)
        {
            return db.TryGetRecordByTriggerId(triggerId, out record);
        }

        private static void ValidateRecord(
            TriggerPlanJsonDatabase.Record record,
            MobaEventSubscriptionRegistry eventRegistry,
            string path,
            MobaRuntimeValidationReport report)
        {
            var businessId = record.TriggerId == 0 ? null : record.TriggerId.ToString();
            ValidateEvent(record, eventRegistry, path, report, businessId);
            ValidatePlan(record.Plan, path, report, businessId);
        }

        private static void Report(MobaRuntimeValidationReport report)
        {
            if (report == null) return;
            if (report.Entries.Count == 0)
            {
                MobaRuntimeLog.Info(MobaRuntimeLogModule.Triggering, MobaRuntimeLogPurpose.Validation, nameof(MobaGameplayTriggerValidator), "validation completed. gameplay trigger configs are valid");
                return;
            }

            MobaRuntimeLog.Warning(MobaRuntimeLogModule.Triggering, MobaRuntimeLogPurpose.Validation, nameof(MobaGameplayTriggerValidator), "validation completed. " + report.FormatSummary());
        }

        private static bool IsGameplayRecord(TriggerPlanJsonDatabase.Record record)
        {
            return !string.IsNullOrEmpty(record.EventName)
                   && record.EventName.StartsWith("gameplay.", StringComparison.OrdinalIgnoreCase);
        }

        private static void ValidateEvent(
            TriggerPlanJsonDatabase.Record record,
            MobaEventSubscriptionRegistry eventRegistry,
            string path,
            MobaRuntimeValidationReport report,
            string businessId)
        {
            if (record.EventId == 0)
            {
                AddError(report, path, "event id is empty", businessId);
            }

            if (record.Scope != TriggerPlanScope.Global)
            {
                AddWarning(report, path, $"gameplay trigger scope is {record.Scope}; lifecycle gameplay events are normally global", businessId);
            }

            if (eventRegistry == null || !eventRegistry.TryGetArgsType(record.EventName, out var argsType) || argsType == null)
            {
                AddError(report, path, $"event '{record.EventName}' is not registered in MobaEventSubscriptionRegistry", businessId);
            }
        }

        private static void ValidatePlan(TriggerPlan<object> plan, string path, MobaRuntimeValidationReport report, string businessId)
        {
            if (plan.Actions == null || plan.Actions.Length == 0)
            {
                AddError(report, path, "gameplay trigger has no actions", businessId);
            }

            ValidatePayloadRef(plan.PredicateArg0, path + ".predicate.arg0", report, businessId);
            ValidatePayloadRef(plan.PredicateArg1, path + ".predicate.arg1", report, businessId);
            ValidatePredicateExpr(plan.PredicateExpr, path + ".predicate", report, businessId);
            ValidateActions(plan.Actions, path, report, businessId);
        }

        private static void ValidatePredicateExpr(PredicateExprPlan expr, string path, MobaRuntimeValidationReport report, string businessId)
        {
            if (expr.Nodes == null)
            {
                return;
            }

            for (int i = 0; i < expr.Nodes.Length; i++)
            {
                var node = expr.Nodes[i];
                if (node.Kind != EBoolExprNodeKind.CompareNumeric)
                {
                    continue;
                }

                ValidatePayloadRef(node.Left, $"{path}.nodes[{i}].left", report, businessId);
                ValidatePayloadRef(node.Right, $"{path}.nodes[{i}].right", report, businessId);
            }
        }

        private static void ValidateActions(ActionCallPlan[] actions, string path, MobaRuntimeValidationReport report, string businessId)
        {
            if (actions == null)
            {
                return;
            }

            for (int i = 0; i < actions.Length; i++)
            {
                var action = actions[i];
                var actionPath = $"{path}.actions[{i}]";

                if (!ActionSchemaRegistry.TryGet(action.Id, out var schema) || schema == null)
                {
                    AddError(report, actionPath, $"action schema is not registered. actionId={action.Id.Value}", businessId);
                }
                else
                {
                    var args = ToArgsArray(action);
                    var span = new ReadOnlySpan<KeyValuePair<string, ActionArgValue>>(args);
                    if (!schema.TryValidateArgs(span, out var error))
                    {
                        AddError(report, actionPath, $"action args invalid. actionId={action.Id.Value}, error={error}", businessId);
                    }
                }

                ValidatePayloadRef(action.Arg0, actionPath + ".arg0", report, businessId);
                ValidatePayloadRef(action.Arg1, actionPath + ".arg1", report, businessId);
                if (action.Args != null)
                {
                    foreach (var pair in action.Args)
                    {
                        ValidatePayloadRef(pair.Value.Ref, actionPath + "." + pair.Key, report, businessId);
                    }
                }
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

            var positional = new List<KeyValuePair<string, ActionArgValue>>(action.Arity);
            positional.Add(new KeyValuePair<string, ActionArgValue>("arg0", ActionArgValue.Of(action.Arg0, "arg0")));
            if (action.Arity > 1)
            {
                positional.Add(new KeyValuePair<string, ActionArgValue>("arg1", ActionArgValue.Of(action.Arg1, "arg1")));
            }

            return positional.ToArray();
        }

        private static void ValidatePayloadRef(NumericValueRef valueRef, string path, MobaRuntimeValidationReport report, string businessId)
        {
            if (valueRef.Kind != ENumericValueRefKind.PayloadField)
            {
                return;
            }

            if (valueRef.FieldId == 0 || !KnownPayloadFieldIds.Contains(valueRef.FieldId))
            {
                AddError(report, path, $"unknown gameplay/battle payload field id={valueRef.FieldId}", businessId);
            }
        }

        private static void AddError(MobaRuntimeValidationReport report, string path, string message, string businessId = null)
        {
            report?.Error(Source, path, message, businessId);
        }

        private static void AddWarning(MobaRuntimeValidationReport report, string path, string message, string businessId = null)
        {
            report?.Warning(Source, path, message, businessId);
        }
    }
}
