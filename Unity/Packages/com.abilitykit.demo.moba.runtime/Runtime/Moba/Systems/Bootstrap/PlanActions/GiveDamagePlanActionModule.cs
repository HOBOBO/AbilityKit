using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Ability.World.DI;
using AbilityKit.Core.Common.Log;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;
using CritType = AbilityKit.Demo.Moba.CritType;
using DamageReasonKind = AbilityKit.Demo.Moba.DamageReasonKind;
using DamageFormulaKind = AbilityKit.Demo.Moba.DamageFormulaKind;
using EffectSourceKind = AbilityKit.Demo.Moba.EffectSourceKind;

namespace AbilityKit.Demo.Moba.Systems
{
    /// <summary>
    /// 閫犳垚浼ゅ鐨凱lan Action妯″潡
    /// 浣跨敤鏂扮殑鍏峰悕鍙傛暟 Schema API
    /// </summary>
    [PlanActionModule(order: 11)]
    public sealed class GiveDamagePlanActionModule : NamedArgsPlanActionModuleBase<GiveDamageArgs, IWorldResolver, GiveDamagePlanActionModule>
    {
        protected override ActionId ActionId => TriggeringConstants.GiveDamageId;
        protected override IActionSchema<GiveDamageArgs, IWorldResolver> Schema => GiveDamageSchema.Instance;

        protected override void Execute(object triggerArgs, GiveDamageArgs args, ExecCtx<IWorldResolver> ctx)
        {
            if (!ctx.Context.TryResolve<DamagePipelineService>(out var pipeline) || pipeline == null)
                return;

            // 浠?trigger payload 瑙ｆ瀽 caster/target锛坱riggerArgs 鏄?SkillHitArgs 绛変簨浠?payload锛?
            if (!PlanContextValueResolver.TryGetCasterActorId(triggerArgs, out var attackerActorId) || attackerActorId <= 0)
                return;

            if (!PlanContextValueResolver.TryGetTargetActorId(triggerArgs, out var targetActorId) || targetActorId <= 0)
                return;

            var attack = new AttackInfo
            {
                AttackerActorId = attackerActorId,
                TargetActorId = targetActorId,
                DamageType = args.DamageType,
                CritType = CritType.None,
                ReasonKind = DamageReasonKind.Skill,
                ReasonParam = args.ReasonParam,
                FormulaKind = (int)DamageFormulaKind.Standard,
                OriginSource = attackerActorId,
                OriginTarget = targetActorId,
                OriginKind = EffectSourceKind.Effect,
                OriginConfigId = 0,
                OriginContextId = 0,
            };
            attack.BaseDamage.BaseValue = args.DamageValue;

            var result = pipeline.Execute(attack);
            if (result == null)
            {
                Log.Warning($"[Plan] give_damage pipeline returned null. attacker={attackerActorId} target={targetActorId} damage={args.DamageValue:0.###} reasonParam={args.ReasonParam}");
            }
        }
    }
}
