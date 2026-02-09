using System;
using AbilityKit.Ability;
using AbilityKit.Ability.Impl.Moba;
using AbilityKit.Ability.Impl.Moba.EffectSource;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.Share.Effect;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Triggering.Runtime;
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
        private readonly AbilityKit.Ability.Triggering.IEventBus _eventBus;
        private readonly TriggerRunner _triggers;
        private readonly MobaTriggerIndexService _index;

        private readonly IWorldResolver _services;
        private readonly TriggerPlanJsonDatabase _planDb;
        private readonly AbilityKit.Triggering.Runtime.TriggerRunner<IWorldResolver> _planRunner;
        private readonly AbilityKit.Triggering.Eventing.IEventBus _planEventBus;
        private readonly FunctionRegistry _planFunctions;
        private readonly ActionRegistry _planActions;
        private bool _planInitialized;

        public MobaEffectExecutionService(
            IWorldResolver services,
            AbilityKit.Ability.Triggering.IEventBus eventBus,
            TriggerRunner triggers,
            MobaTriggerIndexService index,
            TriggerPlanJsonDatabase planDb,
            AbilityKit.Triggering.Runtime.TriggerRunner<IWorldResolver> planRunner,
            AbilityKit.Triggering.Eventing.IEventBus planEventBus,
            FunctionRegistry planFunctions,
            ActionRegistry planActions)
        {
            _services = services;
            _eventBus = eventBus;
            _triggers = triggers;
            _index = index;
            _planDb = planDb;
            _planRunner = planRunner;
            _planEventBus = planEventBus;
            _planFunctions = planFunctions;
            _planActions = planActions;
        }

        private void EnsurePlanInitialized()
        {
            if (_planInitialized) return;
            _planInitialized = true;
            if (_planDb == null || _planRunner == null || _planActions == null) return;

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
        }

        private bool TryExecutePlanByTriggerId(int triggerId, object args)
        {
            if (triggerId <= 0) return false;
            if (_planDb == null) return false;
            if (!_planDb.TryGetPlanByTriggerId(triggerId, out var plan)) return false;

            if (_planEventBus == null || _planFunctions == null || _planActions == null)
            {
                Log.Warning($"[MobaEffectExecutionService] Plan runtime deps missing; skip plan exec. triggerId={triggerId}");
                return false;
            }

            try
            {
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

                var planned = new PlannedTrigger<object, IWorldResolver>(plan);
                var ok = planned.Evaluate(args, execCtx);
                if (ctrl.StopPropagation || ctrl.Cancel) return ok;
                if (!ok) return true;
                planned.Execute(args, execCtx);
                return true;
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

            var needInternal = mode == EffectExecuteMode.InternalOnly || mode == EffectExecuteMode.InternalThenPublishEvent;
            var needPublish = mode == EffectExecuteMode.PublishEventOnly || mode == EffectExecuteMode.InternalThenPublishEvent;

            if (needInternal && _triggers == null) return;
            if (needPublish && _eventBus == null) return;

            static void FillArgs(PooledTriggerArgs args, int effectId2, IAbilityPipelineContext ctx)
            {
                var skillId = ctx.GetSkillId();
                var casterActorId = ctx.GetCasterActorId();
                var targetActorId = ctx.GetTargetActorId();

                args[MobaSkillTriggering.Args.SkillId] = skillId;
                args[MobaSkillTriggering.Args.SkillSlot] = ctx.GetSkillSlot();
                args[MobaSkillTriggering.Args.CasterActorId] = casterActorId;
                args[MobaSkillTriggering.Args.TargetActorId] = targetActorId;
                args[MobaSkillTriggering.Args.AimPos] = ctx.GetAimPos();
                args[MobaSkillTriggering.Args.AimDir] = ctx.GetAimDir();
                args["effect.id"] = effectId2;

                args[EffectTriggering.Args.Source] = casterActorId;
                args[EffectTriggering.Args.Target] = targetActorId;

                var sourceContextId = 0L;
                try { sourceContextId = ctx.GetData<long>(MobaSkillPipelineSharedKeys.SourceContextId); }
                catch { sourceContextId = 0; }

                if (sourceContextId != 0)
                {
                    args[EffectSourceKeys.SourceContextId] = sourceContextId;
                    args[EffectTriggering.Args.OriginSource] = casterActorId;
                    args[EffectTriggering.Args.OriginTarget] = targetActorId;
                    args[EffectTriggering.Args.OriginContextId] = sourceContextId;
                    if (skillId > 0)
                    {
                        args[EffectTriggering.Args.OriginKind] = EffectSourceKind.SkillCast;
                        args[EffectTriggering.Args.OriginConfigId] = skillId;
                    }
                }
            }

            if (needInternal)
            {
                EnsurePlanInitialized();
                var args = PooledTriggerArgs.Rent();
                FillArgs(args, effectId, wrappedContext);
                RunByTriggerId(effectId, args, wrappedContext);
                args.Dispose();
            }

            if (needPublish)
            {
                var args1 = PooledTriggerArgs.Rent();
                FillArgs(args1, effectId, wrappedContext);
                _eventBus.Publish(new TriggerEvent(MobaTriggerEventIds.EffectExecute, payload: wrappedContext, args: args1));

                var args2 = PooledTriggerArgs.Rent();
                FillArgs(args2, effectId, wrappedContext);
                _eventBus.Publish(new TriggerEvent(MobaTriggerEventIds.EffectExecuteById(effectId), payload: wrappedContext, args: args2));
            }
        }

        // Active invocation: execute triggers by triggerId directly (no event subscription involved).
        // Note: This is intentionally different from publishing an event; it is used by systems like projectiles that own their triggers.
        public void ExecuteTriggerId(int triggerId, object source = null, object target = null, object payload = null, PooledTriggerArgs args = null)
        {
            if (triggerId <= 0) return;

            EnsurePlanInitialized();

            // Plan first (active triggers may have EventId=0 and are executed by TriggerId).
            if (TryExecutePlanByTriggerId(triggerId, payload)) return;

            // Legacy fallback
            if (_triggers == null) return;
            if (_index == null) return;
            if (!_index.TryGetByTriggerId(triggerId, out var list) || list == null) return;

            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                var def = e.Def;
                if (def == null) continue;

                if (!string.IsNullOrEmpty(def.EventId))
                {
                    Log.Warning($"[MobaEffectExecutionService] ExecuteTriggerId found trigger with EventId set. triggerId={triggerId}, eventId={def.EventId} (should be empty for active triggers)");
                }

                try
                {
                    _triggers.RunOnce(def, source: source, target: target, payload: payload, args: args, initialLocalVars: e.InitialLocalVars);
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, $"[MobaEffectExecutionService] ExecuteTriggerId exception: triggerId={triggerId}");
                }
            }
        }

        private void RunByTriggerId(int triggerId, PooledTriggerArgs args, IAbilityPipelineContext context)
        {
            EnsurePlanInitialized();

            // Plan first
            if (TryExecutePlanByTriggerId(triggerId, context)) return;

            if (_triggers == null) return;
            if (_index == null) return;
            if (!_index.TryGetByTriggerId(triggerId, out var list) || list == null) return;

            object caster = null;
            if (context is IEffectContext ec && ec.TryGetSkill(out var skill))
            {
                caster = skill.CasterUnit;
            }

            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                var def = e.Def;
                if (def == null) continue;

                _triggers.RunOnce(def, source: caster, target: caster, payload: context, args: args, initialLocalVars: e.InitialLocalVars);
            }
        }

        public void Dispose()
        {
        }
    }
}
