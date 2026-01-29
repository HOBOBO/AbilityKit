using System.Collections.Generic;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.Share.Common.Pool;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Triggering;

namespace AbilityKit.Ability.Impl.Triggering.DamageActions
{
    public static class GiveDamageExecutor
    {
        public static void Execute(TriggerContext context, DamageActionSpec spec)
        {
            if (context == null) return;
            if (spec == null) return;

            var pipeline = context.Services?.GetService(typeof(DamagePipelineService)) as DamagePipelineService;
            if (pipeline == null)
            {
                Log.Warning("[Trigger] give_damage cannot resolve DamagePipelineService from DI");
                return;
            }

            var attackerObj = DamageContextResolver.ResolveAttackerObj(context, spec);
            TriggerActionArgUtil.TryResolveActorId(attackerObj, out var attackerActorId);

            var targetObj = DamageContextResolver.ResolveTargetObj(context, spec);
            var targets = DamageTargetSelection.RentTargets(context, spec, casterActorId: attackerActorId, explicitTargetObj: targetObj);
            try
            {
                if (targets == null || targets.Count == 0)
                {
                    Log.Warning("[Trigger] give_damage requires a valid target actorId");
                    return;
                }

                HashSet<int> uniqueTargets = null;
                if (targets.Count > 1)
                {
                    uniqueTargets = new HashSet<int>();
                }

                for (int i = 0; i < targets.Count; i++)
                {
                    var targetActorId = targets[i];
                    if (targetActorId <= 0) continue;

                    if (uniqueTargets != null && !uniqueTargets.Add(targetActorId)) continue;

                    var attack = new AttackInfo
                    {
                        AttackerActorId = attackerActorId,
                        TargetActorId = targetActorId,
                        DamageType = spec.DamageType,
                        CritType = spec.CritType,
                        ReasonKind = spec.ReasonKind,
                        ReasonParam = spec.ReasonParam,
                        FormulaKind = spec.FormulaKind,
                    };

                    DamageActionOriginUtil.FillOrigin(context, attack, attackerObj ?? attackerActorId, targetActorId);
                    attack.BaseDamage.BaseValue = spec.Value;

                    var result = pipeline.Execute(attack);
                    if (spec.Log)
                    {
                        var applied = result != null ? result.Value : 0f;
                        var hp = result != null ? result.TargetHp : 0f;
                        var maxHp = result != null ? result.TargetMaxHp : 0f;
                        Log.Info($"[give_damage] attacker={attackerActorId} target={targetActorId} base={spec.Value:0.###} applied={applied:0.###} hp={hp:0.###}/{maxHp:0.###} reason=({spec.ReasonKind},{spec.ReasonParam})");
                    }
                }
            }
            finally
            {
                DamageTargetSelection.Release(targets);
            }
        }
    }
}
