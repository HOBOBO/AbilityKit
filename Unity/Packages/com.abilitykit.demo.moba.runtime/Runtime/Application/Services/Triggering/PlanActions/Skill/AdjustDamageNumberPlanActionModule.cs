using AbilityKit.Ability.World.DI;
using AbilityKit.Deterministic;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    [PlanActionModule(order: MobaPlanActionModuleOrders.AdjustDamageNumber)]
    public sealed class AdjustDamageNumberPlanActionModule : MobaPlanActionModuleBase<AdjustDamageNumberArgs, AdjustDamageNumberPlanActionModule>
    {
        protected override IActionSchema<AdjustDamageNumberArgs, IWorldResolver> Schema => AdjustDamageNumberSchema.Instance;

        protected override void Execute(object triggerArgs, AdjustDamageNumberArgs args, ExecCtx<IWorldResolver> ctx)
        {
            if (!(triggerArgs is AttackInfo attack) || attack == null)
            {
                MobaPlanActionDiagnostics.Rejected(ctx.Context, TriggeringConstants.Actions.AdjustDamageNumber, "payload is not AttackInfo.");
                return;
            }

            if (args.ReasonKind != DamageReasonKind.None && attack.ReasonKind != args.ReasonKind) return;
            if (args.ReasonParam != 0 && attack.ReasonParam != args.ReasonParam) return;
            if (attack.TargetActorId <= 0)
            {
                MobaPlanActionDiagnostics.Rejected(ctx.Context, TriggeringConstants.Actions.AdjustDamageNumber, $"invalid target. reason={attack.ReasonKind}:{attack.ReasonParam}");
                return;
            }

            var modifierValue = MobaResourceFixedConvert.ToFixed(args.Value);
            var hitCount = 0;
            var decayFactor = Fixed64.One;
            if (args.RepeatTargetDecayFactor > 0f)
            {
                if (!TryUpdateRepeatTargetState(attack, args, ctx, out hitCount, out decayFactor))
                {
                    return;
                }

                if (args.SkipFirstHit && hitCount <= 1)
                {
                    MobaPlanActionDiagnostics.Applied(ctx.Context, TriggeringConstants.Actions.AdjustDamageNumber, $"tracked first hit. target={attack.TargetActorId} reason={attack.ReasonKind}:{attack.ReasonParam}");
                    return;
                }

                modifierValue = args.Op == CombatNumberModifierOp.Mul ? decayFactor - Fixed64.One : decayFactor;
            }

            if (args.TargetMissingHpRatioCoefficient != 0f)
            {
                if (!TryResolveTargetMissingHpRatio(attack.TargetActorId, ctx, out var missingHpRatio))
                {
                    MobaPlanActionDiagnostics.Rejected(ctx.Context, TriggeringConstants.Actions.AdjustDamageNumber, $"cannot resolve target health. target={attack.TargetActorId}");
                    return;
                }

                modifierValue += missingHpRatio * MobaResourceFixedConvert.ToFixed(args.TargetMissingHpRatioCoefficient);
            }

            if (!TryResolveNumberValue(attack, args.NumberSlot, out var numberValue) || numberValue == null)
            {
                MobaPlanActionDiagnostics.Rejected(ctx.Context, TriggeringConstants.Actions.AdjustDamageNumber, $"unsupported number slot. slot={args.NumberSlot}");
                return;
            }

            var sourceId = args.SourceId != 0 ? args.SourceId : (args.ReasonParam != 0 ? args.ReasonParam : attack.ReasonParam);
            numberValue.Apply(new CombatNumberModifier(args.Op, modifierValue, sourceId));
            MobaPlanActionDiagnostics.Applied(ctx.Context, TriggeringConstants.Actions.AdjustDamageNumber, $"modifier applied. slot={args.NumberSlot} op={args.Op} value={MobaResourceFixedConvert.ToSingle(modifierValue):0.###} target={attack.TargetActorId} hitCount={hitCount} decay={MobaResourceFixedConvert.ToSingle(decayFactor):0.###} missingHpCoefficient={args.TargetMissingHpRatioCoefficient:0.###} reason={attack.ReasonKind}:{attack.ReasonParam}");
        }

        private static bool TryUpdateRepeatTargetState(AttackInfo attack, AdjustDamageNumberArgs args, ExecCtx<IWorldResolver> ctx, out int hitCount, out Fixed64 decayFactor)
        {
            hitCount = 0;
            decayFactor = Fixed64.One;

            if (!attack.TryGetOrigin(out var origin) || !origin.SkillRuntimeHandle.IsValid)
            {
                if (args.RequireSkillRuntime)
                {
                    MobaPlanActionDiagnostics.Rejected(ctx.Context, TriggeringConstants.Actions.AdjustDamageNumber, $"missing skill runtime. target={attack.TargetActorId} reason={attack.ReasonKind}:{attack.ReasonParam}");
                }

                return !args.RequireSkillRuntime;
            }

            var runtimeHandle = origin.SkillRuntimeHandle;
            if (!ctx.Context.TryResolve<MobaSkillCastRuntimeService>(out var runtimes) || runtimes == null || !runtimes.TryGetBlackboard(in runtimeHandle, out var blackboard) || blackboard == null)
            {
                MobaPlanActionDiagnostics.Rejected(ctx.Context, TriggeringConstants.Actions.AdjustDamageNumber, $"cannot resolve skill runtime blackboard. handle={runtimeHandle.ToString()}");
                return false;
            }

            var targetHitCountKey = CreateTargetHitCountKey(args.TargetHitCountKeyBase, attack.TargetActorId);
            blackboard.TryGetInt(in targetHitCountKey, out var previousHitCount);
            hitCount = previousHitCount + 1;
            blackboard.SetInt(in targetHitCountKey, hitCount);
            blackboard.AddInt(in MobaSkillRuntimeBlackboardKeys.HitCount, 1);
            blackboard.AddActorId(in MobaSkillRuntimeBlackboardKeys.DamagedTargets, attack.TargetActorId);

            for (var i = 1; i < hitCount; i++)
            {
                decayFactor *= MobaResourceFixedConvert.ToFixed(args.RepeatTargetDecayFactor);
            }

            blackboard.SetFloat(in MobaSkillRuntimeBlackboardKeys.DecayFactor, MobaResourceFixedConvert.ToSingle(decayFactor));
            return true;
        }

        private static bool TryResolveTargetMissingHpRatio(int targetActorId, ExecCtx<IWorldResolver> ctx, out Fixed64 missingHpRatio)
        {
            missingHpRatio = Fixed64.Zero;
            if (targetActorId <= 0
                || !ctx.Context.TryResolve<MobaActorLookupService>(out var actors)
                || actors == null
                || !actors.TryGetActorEntity(targetActorId, out var target)
                || target == null
                || !target.hasAttributeGroup
                || !target.hasResourceContainer)
            {
                return false;
            }

            var attrs = new MobaAttrs(target);
            var maxHp = attrs.MaxHp;
            if (maxHp <= 0f) return false;

            missingHpRatio = DeterministicMath.Clamp(Fixed64.One - attrs.FixedHp / MobaResourceFixedConvert.ToFixed(maxHp), Fixed64.Zero, Fixed64.One);
            return true;
        }

        private static MobaSkillRuntimeBlackboardKey CreateTargetHitCountKey(int keyBase, int targetActorId)
        {
            return new MobaSkillRuntimeBlackboardKey(
                keyBase + targetActorId,
                $"skill.targetHitCount.{targetActorId}",
                MobaSkillRuntimeValueKind.Int,
                MobaSkillRuntimeBlackboardScope.Cast);
        }

        private static bool TryResolveNumberValue(AttackInfo attack, DamageNumberSlot slot, out CombatNumberValue numberValue)
        {
            switch (slot)
            {
                case DamageNumberSlot.BaseDamage:
                    numberValue = attack.BaseDamage;
                    return true;
                case DamageNumberSlot.DamageRate:
                    numberValue = attack.DamageRate;
                    return true;
                case DamageNumberSlot.FlatBonus:
                    numberValue = attack.FlatBonus;
                    return true;
                case DamageNumberSlot.FinalDamage:
                    numberValue = attack.FinalDamage;
                    return true;
                default:
                    numberValue = null;
                    return false;
            }
        }
    }
}
