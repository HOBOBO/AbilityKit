using System;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO;
using AbilityKit.Ability.Impl.Moba.Conponents;
using AbilityKit.Ability.Impl.Moba.EffectSource;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Share.Effect;
using AbilityKit.Ability.Share.Math;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Triggering.Runtime;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Entitas;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.Impl.Moba;

namespace AbilityKit.Ability.Share.Impl.Moba.Systems.Buffs
{
    [WorldSystem(order: MobaSystemOrder.BuffsRemove, Phase = WorldSystemPhase.Execute)]
    public sealed class MobaBuffRemoveSystem : WorldSystemBase
    {
        private MobaConfigDatabase _configs;
        private IEventBus _eventBus;
        private ITriggerActionRunner _actionRunner;
        private EffectSourceRegistry _effectSource;
        private IFrameTime _frameTime;
        private MobaEffectExecutionService _effectExec;

        private global::Entitas.IGroup<global::ActorEntity> _group;

        public MobaBuffRemoveSystem(global::Entitas.IContexts contexts, IWorldServices services)
            : base(contexts, services)
        {
        }

        protected override void OnInit()
        {
            Services.TryResolve(out _configs);
            Services.TryResolve(out _eventBus);
            Services.TryResolve(out _actionRunner);
            Services.TryResolve(out _effectSource);
            Services.TryResolve(out _frameTime);
            Services.TryResolve(out _effectExec);
            _group = Contexts.Actor().GetGroup(ActorMatcher.AllOf(ActorComponentsLookup.ActorId, ActorComponentsLookup.RemoveBuffRequest));
        }

        protected override void OnExecute()
        {
            var entities = _group.GetEntities();
            if (entities == null || entities.Length == 0) return;

            for (int i = 0; i < entities.Length; i++)
            {
                var e = entities[i];
                if (e == null || !e.hasActorId || !e.hasRemoveBuffRequest) continue;

                var req = e.removeBuffRequest;
                e.RemoveRemoveBuffRequest();

                if (req.BuffId <= 0) continue;
                if (!e.hasBuffs) continue;

                var list = e.buffs.Active;
                if (list == null || list.Count == 0) continue;

                for (int j = list.Count - 1; j >= 0; j--)
                {
                    var b = list[j];
                    if (b == null) continue;
                    if (b.BuffId != req.BuffId) continue;

                    try
                    {
                        if (b.SourceContextId != 0)
                        {
                            _actionRunner?.CancelByOwnerKey(b.SourceContextId);
                        }
                    }
                    catch
                    {
                    }

                    try
                    {
                        if (b.SourceContextId != 0)
                        {
                            var reason = req.Reason;
                            if (reason == EffectSourceEndReason.None) reason = EffectSourceEndReason.Dispelled;
                            _effectSource?.End(b.SourceContextId, GetFrame(), reason);
                        }
                    }
                    catch
                    {
                    }

                    if (_configs != null)
                    {
                        if (_configs.TryGetBuff(b.BuffId, out var buff) && buff != null)
                        {
                            var reason = req.Reason;
                            if (reason == EffectSourceEndReason.None) reason = EffectSourceEndReason.Dispelled;
                            PublishBuffRemove(_eventBus, _effectSource, buff, req.SourceId, e.actorId.Value, b, reason);
                            ExecuteStageEffects(buff.OnRemoveEffects, stage: "remove", sourceActorId: req.SourceId, targetActorId: e.actorId.Value);
                        }
                    }

                    if (b.SourceContextId != 0 && e.hasEffectListeners)
                    {
                        var listeners = e.effectListeners.Active;
                        if (listeners != null && listeners.Count > 0)
                        {
                            for (int k = listeners.Count - 1; k >= 0; k--)
                            {
                                var l = listeners[k];
                                if (l == null) continue;
                                if (l.SourceContextId != b.SourceContextId) continue;
                                try
                                {
                                    l.Sub?.Unsubscribe();
                                }
                                catch
                                {
                                }
                                l.Sub = null;
                                listeners.RemoveAt(k);
                            }
                        }
                    }

                    list.RemoveAt(j);
                }
            }
        }

        private static void PublishBuffRemove(IEventBus bus, EffectSourceRegistry effectSource, BuffMO buff, int sourceActorId, int targetActorId, BuffRuntime runtime, EffectSourceEndReason reason)
        {
            if (bus == null) return;
            if (buff == null) return;

            // base remove event
            {
                var args0 = PooledTriggerArgs.Rent();
                args0[EffectTriggering.Args.Source] = sourceActorId;
                args0[EffectTriggering.Args.Target] = targetActorId;
                args0[EffectSourceKeys.SourceActorId] = sourceActorId;
                args0[EffectSourceKeys.TargetActorId] = targetActorId;
                args0[MobaBuffTriggering.Args.BuffId] = runtime != null ? runtime.BuffId : 0;
                args0[MobaBuffTriggering.Args.EffectId] = 0;
                args0[MobaBuffTriggering.Args.Stage] = "remove";
                args0[MobaBuffTriggering.Args.StackCount] = runtime != null ? runtime.StackCount : 0;
                args0[MobaBuffTriggering.Args.RemoveReason] = (int)reason;
                if (runtime != null && runtime.SourceContextId != 0)
                {
                    args0[EffectSourceKeys.SourceContextId] = runtime.SourceContextId;

                    EffectOriginArgsHelper.FillFromRegistry(args0, runtime.SourceContextId, effectSource);
                }
                bus.Publish(new TriggerEvent(MobaBuffTriggering.Events.Remove, payload: runtime, args: args0));
            }

            var effects = buff.OnRemoveEffects;
            if (effects == null || effects.Count == 0) return;
            for (int i = 0; i < effects.Count; i++)
            {
                var effectId = effects[i];
                if (effectId <= 0) continue;
                var args1 = PooledTriggerArgs.Rent();
                args1[EffectTriggering.Args.Source] = sourceActorId;
                args1[EffectTriggering.Args.Target] = targetActorId;
                args1[EffectSourceKeys.SourceActorId] = sourceActorId;
                args1[EffectSourceKeys.TargetActorId] = targetActorId;
                args1[MobaBuffTriggering.Args.BuffId] = runtime != null ? runtime.BuffId : 0;
                args1[MobaBuffTriggering.Args.EffectId] = effectId;
                args1[MobaBuffTriggering.Args.Stage] = "remove";
                args1[MobaBuffTriggering.Args.StackCount] = runtime != null ? runtime.StackCount : 0;
                args1[MobaBuffTriggering.Args.RemoveReason] = (int)reason;
                if (runtime != null && runtime.SourceContextId != 0)
                {
                    args1[EffectSourceKeys.SourceContextId] = runtime.SourceContextId;

                    EffectOriginArgsHelper.FillFromRegistry(args1, runtime.SourceContextId, effectSource);
                }
                bus.Publish(new TriggerEvent(MobaBuffTriggering.Events.WithEffect(MobaBuffTriggering.Events.Remove, effectId), payload: runtime, args: args1));
            }
        }

        private void ExecuteStageEffects(System.Collections.Generic.IReadOnlyList<int> effectIds, string stage, int sourceActorId, int targetActorId)
        {
            // 涓枃娉ㄩ噴锛氱Щ闄?Buff 鏃舵墽琛?OnRemoveEffects銆?
            if (_effectExec == null) return;
            if (effectIds == null || effectIds.Count == 0) return;
            for (int i = 0; i < effectIds.Count; i++)
            {
                var effectId = effectIds[i];
                if (effectId <= 0) continue;
                var ctx = BuildEffectContext(sourceActorId, targetActorId);
                _effectExec.Execute(effectId, ctx, EffectExecuteMode.InternalOnly);
            }
        }

        private static SkillPipelineContext BuildEffectContext(int sourceActorId, int targetActorId)
        {
            // 涓枃娉ㄩ噴锛欱uff remove 闃舵 effect 鎵ц涓婁笅鏂囥€?
            var ctx = new SkillPipelineContext();
            var req = new SkillCastRequest(
                skillId: 0,
                skillSlot: 0,
                casterActorId: sourceActorId,
                targetActorId: targetActorId,
                aimPos: Vec3.Zero,
                aimDir: Vec3.Forward,
                worldServices: null,
                eventBus: null,
                casterUnit: null,
                targetUnit: null);
            ctx.Initialize(abilityInstance: null, in req);
            return ctx;
        }

        private int GetFrame()
        {
            try
            {
                return _frameTime != null ? _frameTime.Frame.Value : 0;
            }
            catch
            {
                return 0;
            }
        }
    }
}

