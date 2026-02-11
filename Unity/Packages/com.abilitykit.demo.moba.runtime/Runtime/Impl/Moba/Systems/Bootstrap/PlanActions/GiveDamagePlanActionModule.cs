using System;
using AbilityKit.Ability.Impl.Moba;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.World.DI;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Ability.Impl.Moba.Systems
{
    [PlanActionModule(order: 11)]
    public sealed class GiveDamagePlanActionModule : PlanActionModuleBase
    {
        protected override string ActionName => "give_damage";
        protected override bool HasAction2 => true;

        protected override void Execute2(object args, double a0, double a1, ExecCtx<IWorldResolver> ctx)
        {
            if (!ctx.Context.TryResolve<DamagePipelineService>(out var pipeline) || pipeline == null) return;

            if (!PlanContextValueResolver.TryGetCasterActorId(args, out var attackerActorId) || attackerActorId <= 0) return;
            if (!PlanContextValueResolver.TryGetTargetActorId(args, out var targetActorId) || targetActorId <= 0) return;

            if (!PlanActionRegisterUtil.TryToFloat(a0, out var value)) return;

            var reasonParam = PlanActionRegisterUtil.ToIntRound(a1);

            var attack = new AttackInfo
            {
                AttackerActorId = attackerActorId,
                TargetActorId = targetActorId,
                DamageType = DamageType.Physical,
                CritType = CritType.None,
                ReasonKind = DamageReasonKind.Skill,
                ReasonParam = reasonParam,
                FormulaKind = DamageFormulaKind.Standard,
                OriginSource = attackerActorId,
                OriginTarget = targetActorId,
                OriginKind = EffectSourceKind.Effect,
                OriginConfigId = 0,
                OriginContextId = 0,
            };
            attack.BaseDamage.BaseValue = value;

            var result = pipeline.Execute(attack);
            if (result == null)
            {
                Log.Warning($"[Plan] give_damage pipeline returned null. attacker={attackerActorId} target={targetActorId} value={value:0.###} reasonParam={reasonParam}");
            }
        }
    }
}
