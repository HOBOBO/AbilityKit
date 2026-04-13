using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba;
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

namespace AbilityKit.Demo.Moba.Systems
{
    /// <summary>
    /// 鎵垮彈浼ゅ鐨凱lan Action妯″潡
    /// 浣跨敤寮虹被鍨嬪弬鏁?Schema API
    /// </summary>
    [PlanActionModule(order: 12)]
    public sealed class TakeDamagePlanActionModule : NamedArgsPlanActionModuleBase<TakeDamageArgs, IWorldResolver, TakeDamagePlanActionModule>
    {
        protected override ActionId ActionId => TriggeringConstants.TakeDamageId;
        protected override IActionSchema<TakeDamageArgs, IWorldResolver> Schema => TakeDamageSchema.Instance;

        protected override void Execute(object triggerArgs, TakeDamageArgs args, ExecCtx<IWorldResolver> ctx)
        {
            if (!ctx.Context.TryResolve<DamagePipelineService>(out var pipeline) || pipeline == null) return;

            if (!TryResolveTakeDamageContext(triggerArgs, out var attackerActorId, out var targetActorId, out var baseValue, out var origin))
            {
                return;
            }

            var rate = args.Rate;
            if (rate <= 0f) rate = 1f;

            var reasonParam = args.ReasonParam;

            baseValue *= rate;
            if (baseValue <= 0f) return;

            var attack = new AttackInfo
            {
                AttackerActorId = attackerActorId,
                TargetActorId = targetActorId,
                DamageType = DamageType.Physical,
                CritType = CritType.None,
                ReasonKind = DamageReasonKind.Buff,
                ReasonParam = reasonParam,
                FormulaKind = (int)DamageFormulaKind.Standard,
            };

            attack.OriginSource = origin.OriginSource ?? attackerActorId;
            attack.OriginTarget = origin.OriginTarget ?? targetActorId;
            attack.OriginKind = origin.OriginKind;
            attack.OriginConfigId = origin.OriginConfigId;
            attack.OriginContextId = origin.OriginContextId;

            attack.BaseDamage.BaseValue = baseValue;

            var result = pipeline.Execute(attack);
            if (result == null)
            {
                Log.Warning($"[Plan] take_damage pipeline returned null. attacker={attackerActorId} target={targetActorId} base={baseValue:0.###} rate={rate:0.###} reasonParam={reasonParam}");
            }
        }

        private readonly struct OriginInfo
        {
            public readonly object OriginSource;
            public readonly object OriginTarget;
            public readonly EffectSourceKind OriginKind;
            public readonly int OriginConfigId;
            public readonly long OriginContextId;

            public OriginInfo(object originSource, object originTarget, EffectSourceKind originKind, int originConfigId, long originContextId)
            {
                OriginSource = originSource;
                OriginTarget = originTarget;
                OriginKind = originKind;
                OriginConfigId = originConfigId;
                OriginContextId = originContextId;
            }
        }

        private static bool TryResolveTakeDamageContext(object args, out int attackerActorId, out int targetActorId, out float baseValue, out OriginInfo origin)
        {
            attackerActorId = 0;
            targetActorId = 0;
            baseValue = 0f;
            origin = default;

            if (args is DamageResult dr)
            {
                attackerActorId = dr.TargetActorId;
                targetActorId = dr.AttackerActorId;
                baseValue = dr.Value;
                origin = new OriginInfo(dr.OriginSource, dr.OriginTarget, dr.OriginKind, dr.OriginConfigId, dr.OriginContextId);
                return attackerActorId > 0 && targetActorId > 0;
            }

            if (args is AttackCalcInfo ac && ac.Attack != null)
            {
                attackerActorId = ac.Attack.TargetActorId;
                targetActorId = ac.Attack.AttackerActorId;
                baseValue = ac.HpDamage.Value;
                origin = new OriginInfo(ac.Attack.OriginSource, ac.Attack.OriginTarget, ac.Attack.OriginKind, ac.Attack.OriginConfigId, ac.Attack.OriginContextId);
                return attackerActorId > 0 && targetActorId > 0;
            }

            if (args is AttackInfo ai)
            {
                attackerActorId = ai.TargetActorId;
                targetActorId = ai.AttackerActorId;
                baseValue = ai.BaseDamage.Value;
                origin = new OriginInfo(ai.OriginSource, ai.OriginTarget, ai.OriginKind, ai.OriginConfigId, ai.OriginContextId);
                return attackerActorId > 0 && targetActorId > 0;
            }

            return false;
        }
    }
}
