using AbilityKit.Core.Continuous;
using AbilityKit.Demo.Moba.Runtime.Application.Services.Triggering;
using AbilityKit.Demo.Moba.Services.Buffs.Runtime;

namespace AbilityKit.Demo.Moba.Services
{
    public sealed class MobaTriggerIntervalContinuousHandler : IMobaContinuousIntervalHandler
    {
        private readonly MobaTriggerExecutionGateway _triggers;
        private readonly MobaCombatActivityService _combatActivity;

        public MobaTriggerIntervalContinuousHandler(MobaTriggerExecutionGateway triggers, MobaCombatActivityService combatActivity = null)
        {
            _triggers = triggers;
            _combatActivity = combatActivity;
        }

        public bool CanHandle(IContinuous continuous)
        {
            return continuous != null
                && continuous is IMobaContinuousExecutionContextProvider
                && !(continuous is BuffContinuousRuntime);
        }

        public void OnInterval(IContinuous continuous, IMobaContinuousPeriodicConfig periodicConfig, in MobaCombatExecutionContext executionContext)
        {
            var triggerIds = periodicConfig?.IntervalEffectIds;
            if (continuous == null || triggerIds == null || triggerIds.Count == 0) return;
            if (!CanRunInterval(continuous)) return;

            var source = continuous is MobaTriggerIntervalContinuousRuntime
                ? "continuous.trigger_interval.interval"
                : "continuous.interval";
            for (int i = 0; i < triggerIds.Count; i++)
            {
                var triggerId = triggerIds[i];
                if (triggerId <= 0) continue;

                var request = MobaTriggerExecutionRequest<IContinuous>.Create(triggerId, continuous, source);
                _triggers?.ExecuteDirectTrigger(in request);
            }
        }

        private bool CanRunInterval(IContinuous continuous)
        {
            if (!(continuous.Config is IMobaContinuousPeriodicGateConfig gate) || !gate.RequireOutOfCombat) return true;
            if (_combatActivity == null) return false;
            if (!(continuous.Config is IMobaContinuousProjectionConfig projection) || projection.OwnerActorId <= 0) return false;
            return _combatActivity.IsOutOfCombat(projection.OwnerActorId, gate.OutOfCombatSeconds);
        }
    }
}

