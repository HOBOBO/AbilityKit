using System;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config;
using AbilityKit.Ability.Impl.Moba;
using AbilityKit.Ability.Impl.Moba.Conponents;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.Share.Effect;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Entitas;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.Share.Impl.Moba;

namespace AbilityKit.Ability.Share.Impl.Moba.Systems.OngoingEffects
{
    [WorldSystem(order: MobaSystemOrder.OngoingEffectsTick, Phase = WorldSystemPhase.Execute)]
    public sealed class MobaOngoingEffectTickSystem : WorldSystemBase
    {
        private MobaConfigDatabase _configs;
        private IWorldClock _clock;
        private MobaEffectExecutionService _effectExec;
        private IEventBus _eventBus;
        private global::Entitas.IGroup<global::ActorEntity> _group;

        public MobaOngoingEffectTickSystem(global::Entitas.IContexts contexts, IWorldResolver services)
            : base(contexts, services)
        {
        }

        protected override void OnInit()
        {
            Services.TryResolve(out _configs);
            Services.TryResolve(out _clock);
            Services.TryResolve(out _effectExec);
            Services.TryResolve(out _eventBus);
            _group = Contexts.Actor().GetGroup(ActorMatcher.AllOf(ActorComponentsLookup.ActorId, ActorComponentsLookup.OngoingEffects));
        }

        protected override void OnExecute()
        {
            if (_clock == null) return;
            var dt = _clock.DeltaTime;
            if (dt <= 0f) return;

            if (_configs == null) return;
            if (_effectExec == null) return;

            var addMs = (int)System.Math.Round(dt * 1000f);
            if (addMs <= 0) return;

            var entities = _group.GetEntities();
            if (entities == null || entities.Length == 0) return;

            for (int i = 0; i < entities.Length; i++)
            {
                var e = entities[i];
                if (e == null || !e.hasOngoingEffects) continue;

                var list = e.ongoingEffects.Active;
                if (list == null || list.Count == 0) continue;

                var targetActorId = e.actorId.Value;

                for (int j = list.Count - 1; j >= 0; j--)
                {
                    var rt = list[j];
                    if (rt == null)
                    {
                        list.RemoveAt(j);
                        continue;
                    }

                    if (!_configs.TryGetOngoingEffect(rt.OngoingEffectId, out var cfg) || cfg == null)
                    {
                        list.RemoveAt(j);
                        continue;
                    }

                    if (!rt.Applied)
                    {
                        ExecuteEffect(cfg.OnApplyEffectId, sourceActorId: rt.SourceActorId, targetActorId: targetActorId);
                        rt.Applied = true;
                    }

                    if (rt.RemainingMs > 0)
                    {
                        rt.RemainingMs -= addMs;
                        if (rt.RemainingMs <= 0)
                        {
                            ExecuteEffect(cfg.OnRemoveEffectId, sourceActorId: rt.SourceActorId, targetActorId: targetActorId);
                            list.RemoveAt(j);
                            continue;
                        }
                    }

                    if (cfg.PeriodMs > 0 && cfg.OnTickEffectId > 0)
                    {
                        rt.NextTickMs -= addMs;
                        while (rt.NextTickMs <= 0)
                        {
                            ExecuteEffect(cfg.OnTickEffectId, sourceActorId: rt.SourceActorId, targetActorId: targetActorId);
                            rt.NextTickMs += cfg.PeriodMs;
                        }
                    }
                }
            }
        }

        private void ExecuteEffect(int effectId, int sourceActorId, int targetActorId)
        {
            if (effectId <= 0) return;
            var ctx = BuildEffectContext(sourceActorId, targetActorId);
            _effectExec.Execute(effectId, ctx, EffectExecuteMode.InternalOnly);
        }

        private MobaEffectPipelineContext BuildEffectContext(int sourceActorId, int targetActorId)
        {
            var ctx = new MobaEffectPipelineContext();
            ctx.Initialize(
                abilityInstance: null,
                sourceActorId: sourceActorId,
                targetActorId: targetActorId,
                contextKind: 0,
                sourceContextId: 0,
                worldServices: Services,
                eventBus: _eventBus);
            return ctx;
        }
    }
}

