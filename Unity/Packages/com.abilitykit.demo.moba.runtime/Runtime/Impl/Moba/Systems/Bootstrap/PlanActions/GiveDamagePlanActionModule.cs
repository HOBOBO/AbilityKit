using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.Core;
using AbilityKit.Ability.Share.Impl.Moba;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.World.DI;
using AbilityKit.Core.Common.Log;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;
using CritType = AbilityKit.Ability.Share.Impl.Moba.CritType;
using DamageReasonKind = AbilityKit.Ability.Share.Impl.Moba.DamageReasonKind;
using DamageFormulaKind = AbilityKit.Ability.Share.Impl.Moba.DamageFormulaKind;
using EffectSourceKind = AbilityKit.Ability.Impl.Moba.EffectSourceKind;

namespace AbilityKit.Ability.Share.Impl.Moba.Systems
{
    /// <summary>
    /// 造成伤害的Plan Action模块
    /// 使用新的具名参数 Schema API
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

            // 从 trigger payload 解析 caster/target（triggerArgs 是 SkillHitArgs 等事件 payload）
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
