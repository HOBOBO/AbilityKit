using System;
using AbilityKit.Ability.World.DI;
using AbilityKit.Core.Logging;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Payload;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Runtime.Plan.Json;
using AbilityKit.Triggering.Collections;
using AbilityKit.Triggering.Variables.Numeric;
using AbilityKit.Triggering.Variables.Numeric.Expression;

namespace AbilityKit.Demo.Moba.Services
{
    internal sealed class MobaTriggerPlanExecutor
    {
        private readonly IWorldResolver _services;
        private readonly TriggerPlanJsonDatabase _planDb;
        private readonly MobaTriggerPlanRuntimeDependencies _dependencies;
        private readonly MobaTriggerPlanEffectResolver _effects;
        private readonly MobaTriggerPlanExecutionContextFactory _contextFactory;
        private readonly MobaTriggerPlanExecutionRunner _runner;

        public MobaTriggerPlanExecutor(
            IWorldResolver services,
            TriggerPlanJsonDatabase planDb,
            IEventBus eventBus,
            FunctionRegistry functions,
            ActionRegistry actions,
            IPayloadAccessorRegistry payloads = null,
            MobaEffectExecutionService currentEffects = null,
            INumericVarDomainRegistry numericDomains = null,
            INumericRpnFunctionRegistry numericFunctions = null)
        {
            _services = services;
            _planDb = planDb;
            if (numericDomains == null) services?.TryResolve(out numericDomains);
            if (numericFunctions == null) services?.TryResolve(out numericFunctions);
            _dependencies = new MobaTriggerPlanRuntimeDependencies(
                services,
                eventBus,
                functions,
                actions,
                payloads,
                numericDomains,
                numericFunctions);
            _effects = new MobaTriggerPlanEffectResolver(services, currentEffects);
            _contextFactory = new MobaTriggerPlanExecutionContextFactory(_dependencies, _effects);
            _runner = new MobaTriggerPlanExecutionRunner();
        }

        public bool TryGetPlan(int triggerId, out TriggerPlan<object> plan)
        {
            plan = default;
            return triggerId > 0 && _planDb != null && _planDb.TryGetPlanByTriggerId(triggerId, out plan);
        }

        public bool Execute(int triggerId, object args)
        {
            return ExecuteInternal(triggerId, args, predicateMissIsSuccess: true);
        }

        public bool ExecuteRulePlan(int triggerId, object args)
        {
            var routeThroughFormal = _effects.ShouldRouteRulePlanThroughFormalEffectSession(out var currentEffects);
            if (routeThroughFormal)
            {
                return currentEffects.ExecuteRulePlan(triggerId, args);
            }

            return ExecuteInternal(triggerId, args, predicateMissIsSuccess: false);
        }

        private bool ExecuteInternal(int triggerId, object args, bool predicateMissIsSuccess)
        {
            if (!TryGetPlan(triggerId, out var plan))
            {
                Log.Warning($"[MobaTriggerPlanExecutor] Rule plan not found. triggerId={triggerId}, hasPlanDb={_planDb != null}");
                return false;
            }

            _dependencies.ValidateForExecution(nameof(MobaTriggerPlanExecutor), triggerId);

            var ctrl = new ExecutionControl();
            ctrl.Reset();

            TriggerCollectionStore ownedCollections = null;
            ITriggerCollectionResolver collections = null;
            var currentEffects = _effects.Resolve();
            if (currentEffects == null ||
                !currentEffects.TryGetCurrentTriggerCollections(out collections))
            {
                ownedCollections = new TriggerCollectionStore();
                collections = ownedCollections;
            }
            try
            {
                var execCtx = _contextFactory.Create(ctrl, collections);
                var hasExecutionRoot = _planDb.TryGetExecutionRootByTriggerId(triggerId, out var executionRoot);
                return _runner.Execute(plan, executionRoot, hasExecutionRoot, args, in execCtx, ctrl, predicateMissIsSuccess);
            }
            catch (Exception ex)
            {
                MobaRuntimeGuard.ReportAndThrow(
                    _services,
                    ex,
                    MobaBattleExceptionDomain.Triggering,
                    "trigger.plan.execute",
                    MobaBattleExceptionSeverity.Critical,
                    detail: $"triggerId={triggerId}");
                return false;
            }
            finally
            {
                ownedCollections?.Dispose();
            }
        }

    }
}
