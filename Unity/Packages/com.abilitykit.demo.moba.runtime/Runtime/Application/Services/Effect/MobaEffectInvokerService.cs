using System;
using AbilityKit.Core.Logging;
using AbilityKit.Effect;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Pipeline;

namespace AbilityKit.Demo.Moba.Services
{
    using AbilityKit.Demo.Moba;
    using AbilityKit.Ability;
    [WorldService(typeof(MobaEffectInvokerService))]
    public sealed class MobaEffectInvokerService : IService
    {
        [WorldInject] private MobaEffectExecutionService _effects = null;
        [WorldInject] private IWorldResolver _services = null;

        public void Execute(int effectId, int sourceActorId, int targetActorId, int contextKind, long sourceContextId, IWorldResolver worldServices = null, Action<MobaEffectPipelineContext> configure = null)
        {
            if (effectId <= 0) return;
            if (_effects == null)
            {
                MobaRuntimeGuard.ThrowRequired(
                    worldServices ?? _services,
                    nameof(MobaEffectInvokerService),
                    "effect.invoke",
                    nameof(MobaEffectExecutionService),
                    MobaBattleExceptionDomain.Service,
                    detail: $"effectId={effectId}, source={sourceActorId}, target={targetActorId}");
            }
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
            _effects.Execute(effectId, ctx, EffectExecuteMode.InternalOnly);
        }

        public void Execute(int effectId, IAbilityPipelineContext context)
        {
            if (effectId <= 0) return;
            if (context == null) return;
            if (_effects == null)
            {
                MobaRuntimeGuard.ThrowRequired(
                    _services,
                    nameof(MobaEffectInvokerService),
                    "effect.invoke.context",
                    nameof(MobaEffectExecutionService),
                    MobaBattleExceptionDomain.Service,
                    detail: $"effectId={effectId}, context={context.GetType().Name}");
            }
            _effects.Execute(effectId, context, EffectExecuteMode.InternalOnly);
        }

        public void Dispose()
        {
        }
    }
}
