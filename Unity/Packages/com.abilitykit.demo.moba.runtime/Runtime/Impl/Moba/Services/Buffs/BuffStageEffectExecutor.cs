using System.Collections.Generic;
using AbilityKit.Ability.Share.Effect;
using AbilityKit.Ability.Impl.Moba;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.Impl.Moba.Conponents;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    internal sealed class BuffStageEffectExecutor
    {
        private readonly MobaEffectExecutionService _effectExec;
        private readonly IWorldResolver _services;
        private readonly AbilityKit.Ability.Triggering.IEventBus _eventBus;

        public BuffStageEffectExecutor(MobaEffectExecutionService effectExec, IWorldResolver services, AbilityKit.Ability.Triggering.IEventBus eventBus)
        {
            _effectExec = effectExec;
            _services = services;
            _eventBus = eventBus;
        }

        public void Execute(IReadOnlyList<int> effectIds, int buffId, int sourceActorId, int targetActorId, long sourceContextId)
        {
            if (_effectExec == null) return;
            if (effectIds == null || effectIds.Count == 0) return;

            for (int i = 0; i < effectIds.Count; i++)
            {
                var effectId = effectIds[i];
                if (effectId <= 0) continue;

                var ctx = new MobaEffectPipelineContext();
                ctx.Initialize(
                    abilityInstance: null,
                    sourceActorId: sourceActorId,
                    targetActorId: targetActorId,
                    contextKind: (int)EffectContextKind.Buff,
                    sourceContextId: sourceContextId,
                    worldServices: _services,
                    eventBus: _eventBus);

                ctx.SharedData[MobaBuffTriggering.Args.BuffId] = buffId;

                _effectExec.Execute(effectId, ctx, EffectExecuteMode.InternalOnly);
            }
        }
    }
}
