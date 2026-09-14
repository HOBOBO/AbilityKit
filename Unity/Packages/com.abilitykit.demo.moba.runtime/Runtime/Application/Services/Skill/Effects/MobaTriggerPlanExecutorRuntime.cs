using System;
using AbilityKit.Ability.World.DI;
using AbilityKit.Core.Logging;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Payload;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Context;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Collections;
using AbilityKit.Triggering.Blackboard;
using AbilityKit.Triggering.Variables.Numeric;
using AbilityKit.Triggering.Variables.Numeric.Expression;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Demo.Moba.Services
{
    internal readonly struct MobaTriggerPlanRuntimeDependencies
    {
        public MobaTriggerPlanRuntimeDependencies(
            IWorldResolver services,
            IEventBus eventBus,
            FunctionRegistry functions,
            ActionRegistry actions,
            IPayloadAccessorRegistry payloads,
            INumericVarDomainRegistry numericDomains = null,
            INumericRpnFunctionRegistry numericFunctions = null,
            ITriggerRandomSource randomSource = null,
            ITriggerExecutionScheduler executionScheduler = null)
        {
            Services = services;
            EventBus = eventBus;
            Functions = functions;
            Actions = actions;
            Payloads = payloads;
            NumericDomains = numericDomains;
            NumericFunctions = numericFunctions;
            RandomSource = randomSource;
            ExecutionScheduler = executionScheduler;
        }

        public IWorldResolver Services { get; }
        public IEventBus EventBus { get; }
        public FunctionRegistry Functions { get; }
        public ActionRegistry Actions { get; }
        public IPayloadAccessorRegistry Payloads { get; }
        public INumericVarDomainRegistry NumericDomains { get; }
        public INumericRpnFunctionRegistry NumericFunctions { get; }
        public ITriggerRandomSource RandomSource { get; }
        public ITriggerExecutionScheduler ExecutionScheduler { get; }

        public void ValidateForExecution(string ownerName, int triggerId)
        {
            if (EventBus == null || Functions == null || Actions == null || Payloads == null)
            {
                MobaRuntimeGuard.ThrowRequired(
                    Services,
                    ownerName,
                    "trigger.plan.execute",
                    "Trigger plan runtime dependencies",
                    MobaBattleExceptionDomain.Triggering,
                    detail: $"triggerId={triggerId}, hasEventBus={EventBus != null}, hasFunctions={Functions != null}, hasActions={Actions != null}, hasPayloads={Payloads != null}");
            }

            if (Services == null)
            {
                MobaRuntimeGuard.ThrowRequired(
                    null,
                    ownerName,
                    "trigger.plan.execute",
                    nameof(IWorldResolver),
                    MobaBattleExceptionDomain.Triggering,
                    detail: $"triggerId={triggerId}");
            }
        }
    }

    internal sealed class MobaTriggerPlanEffectResolver
    {
        private readonly IWorldResolver _services;
        private readonly MobaEffectExecutionService _currentEffects;

        public MobaTriggerPlanEffectResolver(IWorldResolver services, MobaEffectExecutionService currentEffects)
        {
            _services = services;
            _currentEffects = currentEffects;
        }

        public MobaEffectExecutionService Resolve()
        {
            var currentEffects = _currentEffects;
            if (currentEffects == null && _services != null)
            {
                _services.TryResolve<MobaEffectExecutionService>(out currentEffects);
            }

            return currentEffects;
        }

        public bool ShouldRouteRulePlanThroughFormalEffectSession(out MobaEffectExecutionService currentEffects)
        {
            currentEffects = Resolve();
            return currentEffects != null && !currentEffects.TryGetCurrentTraceScope(out _);
        }
    }

    internal sealed class MobaTriggerPlanExecutionContextFactory
    {
        private readonly MobaTriggerPlanRuntimeDependencies _dependencies;
        private readonly MobaTriggerPlanEffectResolver _effects;

        public MobaTriggerPlanExecutionContextFactory(
            MobaTriggerPlanRuntimeDependencies dependencies,
            MobaTriggerPlanEffectResolver effects)
        {
            _dependencies = dependencies;
            _effects = effects;
        }

        public ExecCtx<IWorldResolver> Create(
            ExecutionControl control,
            ITriggerCollectionResolver collections = null)
        {
            var currentEffects = _effects.Resolve();
            if (collections == null && currentEffects != null)
                currentEffects.TryGetCurrentTriggerCollections(out collections);
            IBlackboardResolver blackboards = null;
            if (currentEffects == null ||
                !currentEffects.TryGetCurrentExecutionBlackboards(out blackboards))
                _dependencies.Services?.TryResolve(out blackboards);
            if (currentEffects != null &&
                currentEffects.TryGetCurrentExecutionContext(out var executionContext) &&
                executionContext.SkillRuntimeHandle.IsValid &&
                _dependencies.Services != null &&
                _dependencies.Services.TryResolve<MobaSkillCastRuntimeService>(out var runtimes) &&
                runtimes != null &&
                TryGetRuntimeBlackboard(runtimes, in executionContext, out var runtimeBlackboard))
            {
                var childContextId = executionContext.OwnerContextId != 0L
                    ? executionContext.OwnerContextId
                    : executionContext.ParentContextId;
                blackboards = new Triggering.MobaSkillRuntimeBlackboardResolver(
                    runtimeBlackboard,
                    currentEffects.CurrentEffectContextId,
                    executionContext.TargetActorId,
                    childContextId,
                    blackboards);
            }
            var context = currentEffects != null
                ? new CurrentEffectWorldResolver(_dependencies.Services, currentEffects)
                : _dependencies.Services;

            return new ExecCtx<IWorldResolver>(
                context: context,
                eventBus: _dependencies.EventBus,
                functions: _dependencies.Functions,
                actions: _dependencies.Actions,
                blackboards: blackboards,
                payloads: _dependencies.Payloads,
                idNames: null,
                numericDomains: _dependencies.NumericDomains,
                numericFunctions: _dependencies.NumericFunctions,
                policy: default,
                control: control,
                collections: collections,
                randomSource: _dependencies.RandomSource,
                executionScheduler: _dependencies.ExecutionScheduler);
        }

        private static bool TryGetRuntimeBlackboard(
            MobaSkillCastRuntimeService runtimes,
            in MobaCombatExecutionContext executionContext,
            out MobaSkillRuntimeBlackboard blackboard)
        {
            var handle = executionContext.SkillRuntimeHandle;
            return runtimes.TryGetBlackboard(in handle, out blackboard);
        }

        private sealed class CurrentEffectWorldResolver : IWorldResolver
        {
            private readonly IWorldResolver _inner;
            private readonly MobaEffectExecutionService _effects;

            public CurrentEffectWorldResolver(IWorldResolver inner, MobaEffectExecutionService effects)
            {
                _inner = inner;
                _effects = effects;
            }

            public object Resolve(Type serviceType)
            {
                if (IsCurrentEffectServiceType(serviceType)) return _effects;
                return _inner.Resolve(serviceType);
            }

            public T Resolve<T>()
            {
                if (IsCurrentEffectServiceType(typeof(T))) return (T)(object)_effects;
                return _inner.Resolve<T>();
            }

            public bool TryResolve(Type serviceType, out object instance)
            {
                if (IsCurrentEffectServiceType(serviceType))
                {
                    instance = _effects;
                    return instance != null;
                }

                return _inner.TryResolve(serviceType, out instance);
            }

            public bool TryResolve<T>(out T instance)
            {
                if (IsCurrentEffectServiceType(typeof(T)))
                {
                    instance = _effects != null ? (T)(object)_effects : default;
                    return _effects != null;
                }

                return _inner.TryResolve(out instance);
            }

            private static bool IsCurrentEffectServiceType(Type serviceType)
            {
                return serviceType == typeof(MobaEffectExecutionService)
                    || serviceType == typeof(ITriggerActionExecutionScopeObserver);
            }
        }
    }

    internal sealed class MobaTriggerPlanExecutionRunner
    {
        public bool Execute(
            TriggerPlan<object> plan,
            ITriggerPlanExecutable executionRoot,
            bool hasExecutionRoot,
            object args,
            in ExecCtx<IWorldResolver> execCtx,
            ExecutionControl control,
            bool predicateMissIsSuccess)
        {
            var planned = new PlannedTrigger<object, IWorldResolver>(plan);
            var ok = planned.Evaluate(args, execCtx);
            if (control.StopPropagation || control.Cancel)
            {
                return ok;
            }

            if (!ok)
            {
                return predicateMissIsSuccess;
            }

            if (hasExecutionRoot && executionRoot != null)
            {
                var result = executionRoot.Execute(args, in execCtx);
                return result.IsSuccess && !control.IsActionRejected;
            }

            planned.Execute(args, execCtx);
            return !control.IsActionRejected;
        }
    }
}
