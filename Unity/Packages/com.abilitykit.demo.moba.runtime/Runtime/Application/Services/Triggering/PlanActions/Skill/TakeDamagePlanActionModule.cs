using AbilityKit.Deterministic;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Ability.World.DI;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;
using CritType = AbilityKit.Demo.Moba.CritType;
using DamageReasonKind = AbilityKit.Demo.Moba.DamageReasonKind;
using DamageFormulaKind = AbilityKit.Demo.Moba.DamageFormulaKind;
using AbilityKit.Demo.Moba.Systems;


namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    /// <summary>
    /// 承受伤害的 Plan Action 模块，使用强类型参数 Schema API。
    /// </summary>
    [PlanActionModule(order: MobaPlanActionModuleOrders.TakeDamage)]
    public sealed class TakeDamagePlanActionModule : MobaPlanActionModuleBase<TakeDamageArgs, TakeDamagePlanActionModule>
    {
        protected override IActionSchema<TakeDamageArgs, IWorldResolver> Schema => TakeDamageSchema.Instance;

        protected override void Execute(object triggerArgs, TakeDamageArgs args, ExecCtx<IWorldResolver> ctx)
        {
            if (!ctx.Context.TryResolve<MobaCombatEffectService>(out var combat) || combat == null)
            {
                LogRejected(ctx, "cannot resolve MobaCombatEffectService.");
                return;
            }

            if (!TryResolveTakeDamageContext(triggerArgs, out var attackerActorId, out var targetActorId, out var baseValue, out var origin))
            {
                LogRejected(ctx, "cannot resolve damage context.");
                return;
            }

            var rate = args.Rate;
            if (rate <= 0f) rate = 1f;

            var reasonParam = args.ReasonParam;

            var baseFixed = MobaResourceFixedConvert.ToFixed(baseValue) * MobaResourceFixedConvert.ToFixed(rate);
            if (baseFixed <= Fixed64.Zero)
            {
                LogRejected(ctx, $"requires positive damage. attacker={attackerActorId} target={targetActorId} base={baseValue:0.###} rate={rate:0.###} reasonParam={reasonParam}");
                return;
            }

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

            if (MobaPlanActionInputResolver.TryResolveEffect(
                    triggerArgs,
                    ctx,
                    out var input))
            {
                origin = input.BuildFromOrigin(
                    in origin,
                    attackerActorId,
                    targetActorId);
            }

            attack.SetOrigin(in origin);
            attack.BaseDamage.FixedBaseValue = baseFixed;

            var result = combat.DealDamage(attack);
            if (result == null)
            {
                LogRejected(ctx, $"pipeline returned null. attacker={attackerActorId} target={targetActorId} base={baseValue:0.###} rate={rate:0.###} reasonParam={reasonParam}");
            }
        }

        private static bool TryResolveTakeDamageContext(object args, out int attackerActorId, out int targetActorId, out float baseValue, out MobaGameplayOrigin origin)
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
                ResolveFormalOrigin(dr, dr.AttackerActorId, dr.TargetActorId, dr.OriginKind, dr.OriginConfigId, dr.OriginContextId, out origin);
                return attackerActorId > 0 && targetActorId > 0;
            }

            if (args is AttackCalcInfo ac && ac.Attack != null)
            {
                attackerActorId = ac.Attack.TargetActorId;
                targetActorId = ac.Attack.AttackerActorId;
                baseValue = ac.HpDamage.Value;
                ResolveFormalOrigin(ac, ac.Attack.AttackerActorId, ac.Attack.TargetActorId, ac.Attack.OriginKind, ac.Attack.OriginConfigId, ac.Attack.OriginContextId, out origin);
                return attackerActorId > 0 && targetActorId > 0;
            }

            if (args is AttackInfo ai)
            {
                attackerActorId = ai.TargetActorId;
                targetActorId = ai.AttackerActorId;
                baseValue = ai.BaseDamage.Value;
                ResolveFormalOrigin(ai, ai.AttackerActorId, ai.TargetActorId, ai.OriginKind, ai.OriginConfigId, ai.OriginContextId, out origin);
                return attackerActorId > 0 && targetActorId > 0;
            }

            return false;
        }

        private static bool ResolveFormalOrigin(
            IMobaOriginContextProvider originProvider,
            int sourceActorId,
            int targetActorId,
            MobaTraceKind originKind,
            int originConfigId,
            long originContextId,
            out MobaGameplayOrigin origin)
        {
            if (originProvider != null && originProvider.TryGetOrigin(out origin) && origin.IsValid)
            {
                return true;
            }

            var lineageContext = new MobaTriggerLineageContext(
                EffectContextKind.Trigger,
                originKind != MobaTraceKind.None ? originKind : MobaTraceKind.DamageAttack,
                sourceActorId,
                targetActorId,
                originContextId,
                originContextId,
                originContextId,
                originConfigId);
            origin = MobaGameplayOrigin.FromLineageContext(in lineageContext);
            return origin.IsValid;
        }
    }
}
