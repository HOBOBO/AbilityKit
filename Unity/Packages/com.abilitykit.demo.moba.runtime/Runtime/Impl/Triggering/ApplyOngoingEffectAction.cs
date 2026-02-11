using System;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;

namespace AbilityKit.Ability.Impl.Triggering
{
    public sealed class ApplyOngoingEffectAction : ITriggerRunningAction
    {
        private readonly int _ongoingEffectId;

        public ApplyOngoingEffectAction(int ongoingEffectId)
        {
            _ongoingEffectId = ongoingEffectId;
        }

        public static ApplyOngoingEffectAction FromDef(ActionDef def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            var args = def.Args;
            var id = TriggerActionArgUtil.TryGetInt(args, "ongoingEffectId", 0);
            return new ApplyOngoingEffectAction(id);
        }

        public void Execute(TriggerContext context)
        {
            Start(context);
        }

        public IRunningAction Start(TriggerContext context)
        {
            if (_ongoingEffectId <= 0) return null;

            var svc = context?.Services?.GetService(typeof(MobaPeriodicEffectService)) as MobaPeriodicEffectService;
            if (svc == null)
            {
                Log.Warning("[Trigger] apply_ongoing_effect cannot resolve MobaPeriodicEffectService from DI");
                return null;
            }

            if (!TriggerActionArgUtil.TryResolveActorId(context?.Target, out var targetActorId) || targetActorId <= 0)
            {
                Log.Warning("[Trigger] apply_ongoing_effect requires context.Target with valid actorId");
                return null;
            }

            TriggerActionArgUtil.TryResolveActorId(context?.Source, out var sourceActorId);
            return svc.Start(_ongoingEffectId, sourceActorId, targetActorId);
        }
    }
}
