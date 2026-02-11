using System;
using AbilityKit.Ability;
using AbilityKit.Ability.Share.Effect;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public sealed class MobaEffectInvokerService : IService
    {
        private readonly MobaEffectExecutionService _effects;
        private readonly IWorldResolver _services;

        public MobaEffectInvokerService(MobaEffectExecutionService effects, IWorldResolver services)
        {
            _effects = effects;
            _services = services;
        }

        public void Execute(int effectId, int sourceActorId, int targetActorId, int contextKind, long sourceContextId, IWorldResolver worldServices = null, Action<MobaEffectPipelineContext> configure = null)
        {
            if (effectId <= 0) return;
            if (_effects == null) return;

            var ctx = new MobaEffectPipelineContext();
            ctx.Initialize(
                abilityInstance: null,
                sourceActorId: sourceActorId,
                targetActorId: targetActorId,
                contextKind: contextKind,
                sourceContextId: sourceContextId,
                worldServices: worldServices ?? _services,
                eventBus: null);

            configure?.Invoke(ctx);
            _effects.Execute(effectId, ctx, AbilityKit.Ability.Impl.Moba.EffectExecuteMode.InternalOnly);
        }

        public void Execute(int effectId, IAbilityPipelineContext context)
        {
            if (effectId <= 0) return;
            if (context == null) return;
            if (_effects == null) return;

            _effects.Execute(effectId, context, AbilityKit.Ability.Impl.Moba.EffectExecuteMode.InternalOnly);
        }

        public void Dispose()
        {
        }
    }
}
