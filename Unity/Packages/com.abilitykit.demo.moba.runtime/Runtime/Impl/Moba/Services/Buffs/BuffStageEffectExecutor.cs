using System.Collections.Generic;
using AbilityKit.Core.Generic;
using AbilityKit.Effect;
using AbilityKit.Ability.Impl.Moba;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.Share.Impl.Moba.Components;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    internal sealed class BuffStageEffectExecutor
    {
        private readonly MobaEffectInvokerService _invoker;

        public BuffStageEffectExecutor(MobaEffectInvokerService invoker)
        {
            _invoker = invoker;
        }

        public void Execute(IReadOnlyList<int> effectIds, int buffId, int sourceActorId, int targetActorId, long sourceContextId)
        {
            if (_invoker == null) return;
            if (effectIds == null || effectIds.Count == 0) return;

            for (int i = 0; i < effectIds.Count; i++)
            {
                var effectId = effectIds[i];
                if (effectId <= 0) continue;

                _invoker.Execute(
                    effectId: effectId,
                    sourceActorId: sourceActorId,
                    targetActorId: targetActorId,
                    contextKind: (int)EffectContextKind.Buff,
                    sourceContextId: sourceContextId,
                    configure: ctx => ctx.SetBuffId(buffId));
            }
        }
    }
}
