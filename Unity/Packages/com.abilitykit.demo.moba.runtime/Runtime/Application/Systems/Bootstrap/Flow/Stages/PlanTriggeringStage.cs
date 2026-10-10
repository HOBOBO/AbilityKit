using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Core.Eventing;
using AbilityKit.Core.Logging;
using AbilityKit.Demo.Moba.Gameplay.Triggering;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Runtime.Plan.Json;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Config.Plans;

namespace AbilityKit.Demo.Moba.Systems.Bootstrap.Flow.Stages
{
    /// <summary>
    /// PlanTriggering 安装阶段。
    /// 安装计划触发系统。
    /// </summary>
    [MobaBootstrapStage]
    public sealed class PlanTriggeringStage : MobaBootstrapStageBase
    {
        public override string Name => MobaBootstrapStageNames.PlanTriggering;

        public override string[] Dependencies => new[]
        {
            MobaBootstrapStageNames.TriggerPlans,
            MobaBootstrapStageNames.WorldInit,
        };

        protected internal override void Configure(WorldContainerBuilder builder)
        {
            // Plan action 模块位于 Application/Services/Triggering/PlanActions 下。
            // 此 bootstrap 阶段只在服务模块配置完成后触发安装。
        }

        protected internal override void Install(
            Entitas.IContexts contexts,
            Entitas.Systems systems,
            IWorldResolver services)
        {
            try
            {
                Log.Info("[PlanTriggeringStage] starting...");
                var db = services.Resolve<TriggerPlanJsonDatabase>();
                if (db == null)
                {
                    throw new InvalidOperationException("PlanTriggeringStage requires TriggerPlanJsonDatabase.");
                }

                if (!services.TryResolve<MobaEffectExecutionService>(out var effects) || effects == null)
                {
                    throw new InvalidOperationException("PlanTriggeringStage requires MobaEffectExecutionService.");
                }

                Log.Info("[PlanTriggeringStage] initializing plan actions...");
                effects.InitializePlanActions();

                RunRuntimeValidation(services);

                if (services.TryResolve<TriggerRunner<IWorldResolver>>(out var runner) && runner != null)
                {
                    RegisterGlobalPlans(services, db, runner);
                }

                Log.Info($"[PlanTriggeringStage] PlanTriggering initialized. records={db.Records?.Count ?? 0}");
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "[PlanTriggeringStage] PlanTriggering init exception");
                throw;
            }
        }

        private static void RunRuntimeValidation(IWorldResolver services)
        {
            if (services == null)
            {
                throw new InvalidOperationException("PlanTriggeringStage requires world services for runtime validation.");
            }

            if (!services.TryResolve<IMobaRuntimeValidationRegistry>(out var registry) || registry == null)
            {
                throw new InvalidOperationException("PlanTriggeringStage requires IMobaRuntimeValidationRegistry.");
            }

            var validatorContract = MobaRuntimeValidatorContract.CreateDefault();
            validatorContract.RegisterInto(registry);
            var contractValidation = validatorContract.Validate(registry);
            if (!contractValidation.Succeeded)
            {
                throw new InvalidOperationException("Runtime validator contract validation failed. " + string.Join("; ", contractValidation.Errors));
            }

            if (!services.TryResolve<IMobaRuntimeValidationRunner>(out var runner) || runner == null)
            {
                throw new InvalidOperationException("PlanTriggeringStage requires IMobaRuntimeValidationRunner.");
            }

            var context = new MobaRuntimeValidationContext(
                services,
                nameof(PlanTriggeringStage),
                MobaRuntimeValidationInvocation.Bootstrap);
            var report = runner.ValidateAll(in context);

            if (report != null && report.ShouldBlockStartup)
            {
                throw new InvalidOperationException("Runtime validation has blocking errors. " + report.FormatSummary() + "\n" + report.FormatAllEntries());
            }
        }

        private static void RegisterGlobalPlans(IWorldResolver services, TriggerPlanJsonDatabase db, TriggerRunner<IWorldResolver> runner)
        {
            var records = db.Records;
            if (records == null || records.Count == 0)
            {
                return;
            }

            services.TryResolve<MobaEventSubscriptionRegistry>(out var eventRegistry);
            var gameplayTriggerIds = new HashSet<int>();
            if (services.TryResolve<MobaConfigDatabase>(out var configs) && configs != null)
            {
                foreach (var gameplay in configs.GetAllGameplays())
                {
                    if (gameplay?.TriggerIds == null) continue;
                    foreach (var triggerId in gameplay.TriggerIds) gameplayTriggerIds.Add(triggerId);
                }
            }

            for (int i = 0; i < records.Count; i++)
            {
                var record = records[i];
                if (record.EventId == 0 || record.Scope != TriggerPlanScope.Global)
                {
                    continue;
                }

                if (gameplayTriggerIds.Contains(record.TriggerId) || IsGameplayRecord(record))
                {
                    continue;
                }

                if (eventRegistry == null)
                {
                    throw new InvalidOperationException("PlanTriggeringStage requires MobaEventSubscriptionRegistry for typed global trigger registration.");
                }

                if (string.IsNullOrEmpty(record.EventName)
                    || !eventRegistry.TryGetArgsType(record.EventName, out var argsType)
                    || argsType == null)
                {
                    throw new InvalidOperationException($"Global trigger event is not registered. triggerId={record.TriggerId} eventName={record.EventName}");
                }

                if (!argsType.IsClass)
                {
                    runner.RegisterPlan<object, IWorldResolver>(new EventKey<object>(record.EventId), record.Plan);
                    continue;
                }

                runner.RegisterPlan(record.EventId, argsType, record.Plan);
            }
        }

        private static bool IsGameplayRecord(TriggerPlanJsonDatabase.Record record)
        {
            return !string.IsNullOrEmpty(record.EventName)
                   && record.EventName.StartsWith("gameplay.", StringComparison.OrdinalIgnoreCase);
        }
    }
}
