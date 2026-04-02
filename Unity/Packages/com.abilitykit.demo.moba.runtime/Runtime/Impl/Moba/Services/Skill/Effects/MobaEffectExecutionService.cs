using System;
using AbilityKit.Ability;
using AbilityKit.Ability.Impl.Moba;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.Share.Effect;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Core.Eventing;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Runtime.Plan.Json;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public sealed class MobaEffectExecutionService : IService
    {
        private readonly IWorldResolver _services;
        private readonly TriggerPlanJsonDatabase _planDb;
        private readonly AbilityKit.Triggering.Runtime.TriggerRunner<IWorldResolver> _planRunner;
        private readonly AbilityKit.Triggering.Eventing.IEventBus _planEventBus;
        private readonly FunctionRegistry _planFunctions;
        private readonly ActionRegistry _planActions;
        private bool _planInitialized;

        public MobaEffectExecutionService(
            IWorldResolver services,
            TriggerPlanJsonDatabase planDb,
            AbilityKit.Triggering.Runtime.TriggerRunner<IWorldResolver> planRunner,
            AbilityKit.Triggering.Eventing.IEventBus planEventBus,
            FunctionRegistry planFunctions,
            ActionRegistry planActions)
        {
            _services = services;
            _planDb = planDb;
            _planRunner = planRunner;
            _planEventBus = planEventBus;
            _planFunctions = planFunctions;
            _planActions = planActions;
        }

        private void EnsurePlanInitialized()
        {
            if (_planInitialized)
            {
                Log.Info("[MobaEffectExecutionService] EnsurePlanInitialized: already initialized");
                return;
            }
            if (_planDb == null || _planRunner == null || _planActions == null)
            {
                Log.Warning($"[MobaEffectExecutionService] EnsurePlanInitialized: skipped. _planDb={_planDb != null}, _planRunner={_planRunner != null}, _planActions={_planActions != null}");
                return;
            }

            Log.Info("[MobaEffectExecutionService] EnsurePlanInitialized: starting...");

            // Register real actions (override stubs)
            try
            {
                var debugLogId = new ActionId(AbilityKit.Triggering.Eventing.StableStringId.Get("action:debug_log"));
                _planActions.Register<PlannedTrigger<object, IWorldResolver>.Action0>(
                    debugLogId,
                    static (args, ctx) =>
                    {
                        var ctxType = ctx.Context != null ? ctx.Context.GetType().Name : "<null>";
                        var argsType = args != null ? args.GetType().Name : "<null>";
                        Log.Info($"[Plan] debug_log executed. argsType={argsType}, ctxType={ctxType}");
                    },
                    isDeterministic: true);

                _planActions.Register<PlannedTrigger<object, IWorldResolver>.Action2>(
                    debugLogId,
                    static (args, a0, a1, ctx) =>
                    {
                        var msgId = (int)a0;
                        var dump = a1 >= 0.5;
                        var msg = string.Empty;
                        if (ctx.Context != null && ctx.Context.TryResolve<TriggerPlanJsonDatabase>(out var db) && db != null)
                        {
                            if (!db.TryGetString(msgId, out msg)) msg = string.Empty;
                        }

                        Log.Info($"[Plan] debug_log: {msg}");
                        if (dump)
                        {
                            var ctxType = ctx.Context != null ? ctx.Context.GetType().Name : "<null>";
                            var argsType = args != null ? args.GetType().Name : "<null>";
                            Log.Info($"[Plan] debug_log dump. argsType={argsType}, ctxType={ctxType}");
                        }
                    },
                    isDeterministic: true);
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "[MobaEffectExecutionService] EnsurePlanInitialized: register debug_log action failed");
            }

            // Register stubs first to avoid missing action errors when plans contain actions
            // that are not yet installed (real modules will override stubs).
            try
            {
                RegisterStubActionsFromPlans(_planDb, _planActions);
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "[MobaEffectExecutionService] EnsurePlanInitialized: RegisterStubActionsFromPlans failed");
            }

            // Register real action modules (override stubs).
            try
            {
                if (_services != null
                    && _services.TryResolve<AbilityKit.Ability.Impl.Moba.Systems.PlanActionModuleRegistry>(out var registry)
                    && registry != null
                    && registry.Modules != null)
                {
                    var modules = registry.Modules;
                    for (int i = 0; i < modules.Length; i++)
                    {
                        var m = modules[i];
                        if (m == null) continue;
                        try { m.Register(_planActions, _services); }
                        catch (Exception ex) { Log.Exception(ex, $"[MobaEffectExecutionService] EnsurePlanInitialized: PlanActionModule register failed. module={m.GetType().Name}"); }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "[MobaEffectExecutionService] EnsurePlanInitialized: register PlanActionModules failed");
            }

            _planInitialized = true;
        }

        private void TryRepairMissingActions()
        {
            if (_planDb == null || _planActions == null) return;

            try
            {
                RegisterStubActionsFromPlans(_planDb, _planActions);
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "[MobaEffectExecutionService] TryRepairMissingActions: RegisterStubActionsFromPlans failed");
            }

            try
            {
                if (_services != null
                    && _services.TryResolve<AbilityKit.Ability.Impl.Moba.Systems.PlanActionModuleRegistry>(out var registry)
                    && registry != null
                    && registry.Modules != null)
                {
                    var modules = registry.Modules;
                    for (int i = 0; i < modules.Length; i++)
                    {
                        var m = modules[i];
                        if (m == null) continue;
                        try { m.Register(_planActions, _services); }
                        catch (Exception ex) { Log.Exception(ex, $"[MobaEffectExecutionService] TryRepairMissingActions: PlanActionModule register failed. module={m.GetType().Name}"); }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "[MobaEffectExecutionService] TryRepairMissingActions: register PlanActionModules failed");
            }
        }

        private void TryRepairMissingActions(in AbilityKit.Triggering.Runtime.Plan.TriggerPlan<object> plan)
        {
            if (_planActions == null) return;

            try
            {
                RegisterStubActionsFromPlan(in plan, _planActions);
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "[MobaEffectExecutionService] TryRepairMissingActions(plan): RegisterStubActionsFromPlan failed");
            }

            try
            {
                if (_services != null
                    && _services.TryResolve<AbilityKit.Ability.Impl.Moba.Systems.PlanActionModuleRegistry>(out var registry)
                    && registry != null
                    && registry.Modules != null)
                {
                    var modules = registry.Modules;
                    for (int i = 0; i < modules.Length; i++)
                    {
                        var m = modules[i];
                        if (m == null) continue;
                        try { m.Register(_planActions, _services); }
                        catch (Exception ex) { Log.Exception(ex, $"[MobaEffectExecutionService] TryRepairMissingActions(plan): PlanActionModule register failed. module={m.GetType().Name}"); }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "[MobaEffectExecutionService] TryRepairMissingActions(plan): register PlanActionModules failed");
            }
        }

        private static void RegisterStubActionsFromPlan(in AbilityKit.Triggering.Runtime.Plan.TriggerPlan<object> plan, ActionRegistry actions)
        {
            if (actions == null) return;

            var calls = plan.Actions;
            if (calls == null || calls.Length == 0) return;

            for (int i = 0; i < calls.Length; i++)
            {
                var call = calls[i];
                var actionId = call.Id;
                if (actionId.Value == 0) continue;

                var hasNamedArgs = call.HasNamedArgs;
                if (hasNamedArgs)
                {
                    // 具名参数模式的 Action 不注册 stub
                    // 因为 PlanActionModule 会注册正确类型的 NamedAction<TArgs> 委托
                    // 注册 stub 会导致类型不匹配
                    continue;
                }

                // 注册传统 Action stub（向后兼容）
                var arity = call.Arity;
                switch (arity)
                {
                    case 0:
                        actions.Register<PlannedTrigger<object, IWorldResolver>.Action0>(actionId, static (args, ctx) => { }, true);
                        break;
                    case 1:
                        actions.Register<PlannedTrigger<object, IWorldResolver>.Action1>(actionId, static (args, a0, ctx) => { }, true);
                        break;
                    case 2:
                        actions.Register<PlannedTrigger<object, IWorldResolver>.Action2>(actionId, static (args, a0, a1, ctx) => { }, true);
                        break;
                }
            }
        }

        private bool TryExecutePlanByTriggerId(int triggerId, object args)
        {
            if (triggerId <= 0) return false;
            if (_planDb == null) return false;
            if (!_planDb.TryGetPlanByTriggerId(triggerId, out var plan))
            {
                Log.Warning($"[MobaEffectExecutionService] TryExecutePlanByTriggerId: triggerId={triggerId} not found in _planDb");
                return false;
            }

            Log.Info($"[MobaEffectExecutionService] TryExecutePlanByTriggerId: found plan for triggerId={triggerId}, actions count={plan.Actions?.Length ?? 0}");

            if (_planEventBus == null || _planFunctions == null || _planActions == null)
            {
                Log.Warning($"[MobaEffectExecutionService] Plan runtime deps missing; skip plan exec. triggerId={triggerId}");
                return false;
            }

            var ctrl = new AbilityKit.Triggering.Runtime.ExecutionControl();
            ctrl.Reset();

            var execCtx = new AbilityKit.Triggering.Runtime.ExecCtx<IWorldResolver>(
                context: _services,
                eventBus: _planEventBus,
                functions: _planFunctions,
                actions: _planActions,
                blackboards: null,
                payloads: null,
                idNames: null,
                numericDomains: null,
                numericFunctions: null,
                policy: default,
                control: ctrl);

            bool ExecuteOnce()
            {
                var planned = new PlannedTrigger<object, IWorldResolver>(plan);
                var ok = planned.Evaluate(args, execCtx);
                if (ctrl.StopPropagation || ctrl.Cancel) return ok;
                if (!ok) return true;
                planned.Execute(args, execCtx);
                return true;
            }

            try
            {
                return ExecuteOnce();
            }
            catch (InvalidOperationException)
            {
                // Common cause: actions not registered yet due to init timing.
                // Attempt one-time repair and retry.
                try
                {
                    TryRepairMissingActions(in plan);
                    return ExecuteOnce();
                }
                catch (Exception ex2)
                {
                    Log.Exception(ex2, $"[MobaEffectExecutionService] Plan execution failed. triggerId={triggerId}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.Exception(ex, $"[MobaEffectExecutionService] Plan execution failed. triggerId={triggerId}");
                return false;
            }
        }

        private static void RegisterStubActionsFromPlans(TriggerPlanJsonDatabase db, ActionRegistry actions)
        {
            if (db == null || actions == null) return;

            var arityById = new System.Collections.Generic.Dictionary<int, byte>();
            var records = db.Records;
            if (records == null) return;

            for (int i = 0; i < records.Count; i++)
            {
                var plan = records[i].Plan;
                var calls = plan.Actions;
                if (calls == null) continue;

                for (int j = 0; j < calls.Length; j++)
                {
                    var call = calls[j];
                    var id = call.Id.Value;
                    if (id == 0) continue;

                    if (arityById.TryGetValue(id, out var existing))
                    {
                        if (existing != call.Arity) arityById[id] = byte.MaxValue;
                    }
                    else
                    {
                        arityById[id] = call.Arity;
                    }
                }
            }

            foreach (var kv in arityById)
            {
                var actionId = new ActionId(kv.Key);
                var arity = kv.Value;
                if (arity == byte.MaxValue) continue;

                switch (arity)
                {
                    case 0:
                        actions.Register<PlannedTrigger<object, IWorldResolver>.Action0>(actionId, static (args, ctx) => { }, true);
                        break;
                    case 1:
                        actions.Register<PlannedTrigger<object, IWorldResolver>.Action1>(actionId, static (args, a0, ctx) => { }, true);
                        break;
                    case 2:
                        actions.Register<PlannedTrigger<object, IWorldResolver>.Action2>(actionId, static (args, a0, a1, ctx) => { }, true);
                        break;
                }
            }
        }

        public void Execute(int effectId, IAbilityPipelineContext context, EffectExecuteMode mode = EffectExecuteMode.InternalOnly)
        {
            if (effectId <= 0) return;
            if (context == null) return;

            var wrappedContext = EffectContextWrapper.Wrap(context);
            if (wrappedContext == null) return;

            if (mode == EffectExecuteMode.PublishEventOnly || mode == EffectExecuteMode.InternalThenPublishEvent)
            {
                Log.Warning($"[MobaEffectExecutionService] EffectExecuteMode.{mode} is not supported (legacy publish removed). effectId={effectId}");
            }

            EnsurePlanInitialized();
            if (TryExecutePlanByTriggerId(effectId, wrappedContext))
            {
                return;
            }

            Log.Warning($"[MobaEffectExecutionService] Effect execution skipped (no TriggerPlan found for triggerId={effectId}).");
        }

        public void ExecuteTriggerId(int triggerId, object payload = null)
        {
            if (triggerId <= 0) return;

            EnsurePlanInitialized();

            Log.Info($"[MobaEffectExecutionService] ExecuteTriggerId: triggerId={triggerId}, initialized={_planInitialized}");

            // Plan first (active triggers may have EventId=0 and are executed by TriggerId).
            if (TryExecutePlanByTriggerId(triggerId, payload)) return;

            Log.Warning($"[MobaEffectExecutionService] ExecuteTriggerId skipped (no TriggerPlan found for triggerId={triggerId}).");
        }

        public void Dispose()
        {
        }
    }
}
