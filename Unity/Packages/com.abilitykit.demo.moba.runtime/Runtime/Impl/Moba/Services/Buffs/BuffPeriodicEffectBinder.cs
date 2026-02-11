using System;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.Triggering.Runtime;
using AbilityKit.Ability.Impl.Moba.Conponents;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    internal sealed class BuffPeriodicEffectBinder
    {
        private readonly MobaPeriodicEffectService _periodic;
        private readonly ITriggerActionRunner _actionRunner;

        public BuffPeriodicEffectBinder(MobaPeriodicEffectService periodic, ITriggerActionRunner actionRunner)
        {
            _periodic = periodic;
            _actionRunner = actionRunner;
        }

        public void TryStartPeriodicEffectByBuff(global::AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO.BuffMO buff, BuffRuntime runtime, int sourceActorId, int targetActorId)
        {
            if (_periodic == null) return;
            if (_actionRunner == null) return;
            if (buff == null || runtime == null) return;
            if (buff.OngoingEffectId <= 0) return;
            if (runtime.SourceContextId == 0) return;

            try
            {
                _periodic.Start(buff.OngoingEffectId, sourceActorId, targetActorId, ownerKey: runtime.SourceContextId);
            }
            catch (Exception ex)
            {
                Log.Exception(ex, $"[BuffPeriodicEffectBinder] TryStartPeriodicEffectByBuff exception (buffId={buff.Id}, ongoingEffectId={buff.OngoingEffectId})");
            }
        }
    }
}
