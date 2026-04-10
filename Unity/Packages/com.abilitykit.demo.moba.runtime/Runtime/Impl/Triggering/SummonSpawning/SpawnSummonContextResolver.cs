using AbilityKit.Core.Common.Log;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Core.Math;
using AbilityKit.Ability.Triggering;

namespace AbilityKit.Ability.Impl.Triggering.SummonSpawning
{
    public static class SpawnSummonContextResolver
    {
        public static bool TryResolve(in SpawnSummonSpec spec, TriggerContext context, out SpawnSummonContextData data)
        {
            data = default;
            if (context == null) return false;

            object casterObj = context.Source;
            if (!string.IsNullOrEmpty(spec.CasterKey) && context.Event.Args != null && context.Event.Args.TryGetValue(spec.CasterKey, out var cObj) && cObj != null)
            {
                casterObj = cObj;
            }

            if (!TriggerActionArgUtil.TryResolveActorId(casterObj, out var casterActorId) || casterActorId <= 0)
            {
                Log.Warning("[Trigger] spawn_summon requires a valid caster actorId");
                return false;
            }

            object targetObj = context.Target;
            if (!string.IsNullOrEmpty(spec.TargetKey) && context.Event.Args != null && context.Event.Args.TryGetValue(spec.TargetKey, out var tObj) && tObj != null)
            {
                targetObj = tObj;
            }

            var aimPos = default(Vec3);
            if (!string.IsNullOrEmpty(spec.AimPosKey) && context.Event.Args != null && context.Event.Args.TryGetValue(spec.AimPosKey, out var ap) && ap is Vec3 v3)
            {
                aimPos = v3;
            }
            else if (context.Event.Payload is SkillCastContext sc)
            {
                aimPos = sc.AimPos;
            }

            var fixedPos = default(Vec3);
            if (!string.IsNullOrEmpty(spec.FixedPosKey) && context.Event.Args != null && context.Event.Args.TryGetValue(spec.FixedPosKey, out var fp1) && fp1 is Vec3 v3f)
            {
                fixedPos = v3f;
            }
            else if (context.Event.Args != null && context.Event.Args.TryGetValue("pos", out var fp2) && fp2 is Vec3 v3b)
            {
                fixedPos = v3b;
            }
            else
            {
                fixedPos = spec.FixedPosFallback;
            }

            data = new SpawnSummonContextData(casterActorId, targetObj, in aimPos, in fixedPos);
            return true;
        }
    }
}
