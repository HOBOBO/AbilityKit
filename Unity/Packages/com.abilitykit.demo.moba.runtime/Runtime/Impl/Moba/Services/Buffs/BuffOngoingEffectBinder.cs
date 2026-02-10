using System;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.Triggering.Runtime;
using AbilityKit.Ability.Impl.Moba.Conponents;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    internal sealed class BuffOngoingEffectBinder
    {
        private readonly MobaOngoingEffectService _ongoing;
        private readonly ITriggerActionRunner _actionRunner;

        public BuffOngoingEffectBinder(MobaOngoingEffectService ongoing, ITriggerActionRunner actionRunner)
        {
            _ongoing = ongoing;
            _actionRunner = actionRunner;
        }

        public void TryStartOngoingEffectByBuff(global::AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO.BuffMO buff, BuffRuntime runtime, int sourceActorId, int targetActorId)
        {
            if (_ongoing == null) return;
            if (_actionRunner == null) return;
            if (buff == null || runtime == null) return;
            if (buff.OngoingEffectId <= 0) return;
            if (runtime.SourceContextId == 0) return;

            try
            {
                _ongoing.Start(buff.OngoingEffectId, sourceActorId, targetActorId, ownerKey: runtime.SourceContextId);
            }
            catch (Exception ex)
            {
                Log.Exception(ex, $"[BuffOngoingEffectBinder] TryStartOngoingEffectByBuff exception (buffId={buff.Id}, ongoingEffectId={buff.OngoingEffectId})");
            }
        }
    }
}
