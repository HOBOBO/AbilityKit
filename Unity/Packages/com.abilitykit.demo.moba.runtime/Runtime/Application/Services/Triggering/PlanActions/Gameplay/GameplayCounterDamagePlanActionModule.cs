using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Gameplay;
using AbilityKit.Demo.Moba.Gameplay.Triggering;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Deterministic;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Core.Logging;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    [PlanActionModule(order: MobaPlanActionModuleOrders.AdjustDamageNumber)]
    public sealed class GameplayCounterDamagePlanActionModule : MobaPlanActionModuleBase<GameplayCounterDamageArgs, GameplayCounterDamagePlanActionModule>
    {
        protected override IActionSchema<GameplayCounterDamageArgs, IWorldResolver> Schema => GameplayCounterDamageSchema.Instance;
        protected override void Execute(object triggerArgs, GameplayCounterDamageArgs args, ExecCtx<IWorldResolver> ctx)
        {
            if (!(triggerArgs is AttackInfo attack) || attack == null || args.KeyId == 0 || args.ScopePayloadFieldId == 0 || args.Threshold <= 0) return;
            if (args.ReasonKind != DamageReasonKind.None && attack.ReasonKind != args.ReasonKind) return;
            if (args.ReasonParam != 0 && attack.ReasonParam != args.ReasonParam) return;
            if (!ctx.Payloads.TryGetInt(in attack, args.ScopePayloadFieldId, out var actorId) || actorId <= 0) return;
            if (!TryResolveRequired(ctx, out MobaGameplayVariableService variables)) return;
            if (variables.Get(args.ResolveScopedKey(actorId), 0) < args.Threshold) return;
            CombatNumberValue number = args.NumberSlot == DamageNumberSlot.DamageRate ? attack.DamageRate : args.NumberSlot == DamageNumberSlot.FlatBonus ? attack.FlatBonus : args.NumberSlot == DamageNumberSlot.FinalDamage ? attack.FinalDamage : attack.BaseDamage;
            number.Apply(new CombatNumberModifier(args.Op, Fixed64.FromDouble(args.Value), args.SourceId != 0 ? args.SourceId : args.KeyId));
        }
    }

    [PlanActionModule(order: MobaPlanActionModuleOrders.AdvanceGameplayCounter + 1)]
    public sealed class ConsumeGameplayCounterPlanActionModule : MobaPlanActionModuleBase<ConsumeGameplayCounterArgs, ConsumeGameplayCounterPlanActionModule>
    {
        private const int CastEvaluatedKeyOffset = 0;
        private const int CastEmpoweredKeyOffset = 1;
        private const int CastTargetsKeyOffset = 2;
        private const int CastConsumedKeyOffset = 3;

        protected override IActionSchema<ConsumeGameplayCounterArgs, IWorldResolver> Schema => ConsumeGameplayCounterSchema.Instance;
        protected override void Execute(object triggerArgs, ConsumeGameplayCounterArgs args, ExecCtx<IWorldResolver> ctx)
        {
            if (triggerArgs is SkillCastContext cast && args.PerSkillCast)
            {
                // A projectile or delayed area may resolve after the caster has
                // changed state.  When authored on the cast-complete trigger,
                // capture the empowered decision in the cast blackboard before
                // any hit event is delivered.  The counter is consumed only by
                // the first real positive damage result below.
                InitializePerSkillCast(cast, args, ctx);
                return;
            }

            if (!(triggerArgs is DamageResult result) || result == null || args.KeyId == 0 || args.ScopePayloadFieldId == 0 || args.Threshold <= 0 || result.Value <= 0) return;
            if (args.ReasonKind != DamageReasonKind.None && result.ReasonKind != args.ReasonKind) return;
            if (args.ReasonParam != 0 && result.ReasonParam != args.ReasonParam) return;
            if (!ctx.Payloads.TryGetInt(in result, args.ScopePayloadFieldId, out var actorId) || actorId <= 0) return;
            if (!TryResolveRequired(ctx, out MobaGameplayVariableService variables)) return;
            var key = args.ResolveScopedKey(actorId);

            if (args.PerSkillCast)
            {
                ExecutePerSkillCast(result, args, ctx, variables, key, actorId);
                return;
            }

            if (variables.Get(key, 0) < args.Threshold) return;

            variables.Set(key, args.ResetValue);
            AdvanceGameplayCounterPlanActionModule.ReportCounterCue(
                ctx, args.KeyId, actorId, args.ResetValue, args.Threshold);
            if (args.TriggerId > 0 && TryResolveRequired(ctx, out MobaEffectExecutionService effects))
                effects.ExecuteRulePlan(args.TriggerId, triggerArgs);
        }

        private void ExecutePerSkillCast(
            DamageResult result,
            ConsumeGameplayCounterArgs args,
            ExecCtx<IWorldResolver> ctx,
            MobaGameplayVariableService variables,
            int counterKey,
            int actorId)
        {
            if (!result.TryGetOrigin(out var origin) || !origin.SkillRuntimeHandle.IsValid ||
                !ctx.Context.TryResolve<MobaSkillCastRuntimeService>(out var runtimes) || runtimes == null ||
                !TryGetBlackboard(runtimes, origin.SkillRuntimeHandle, out var blackboard) || blackboard == null)
            {
                // Delayed projectiles and effects can outlive their cast runtime. In
                // that case retain the generic counter contract: consume once when
                // the threshold is still present and dispatch the configured effect.
                // This keeps payload-driven mechanics functional without hero-specific
                // runtime code; a live cast blackboard remains the preferred path for
                // per-cast multi-target de-duplication.
                if (variables.Get(counterKey, 0) < args.Threshold) return;
                variables.Set(counterKey, args.ResetValue);
                AdvanceGameplayCounterPlanActionModule.ReportCounterCue(
                    ctx, args.KeyId, actorId, args.ResetValue, args.Threshold);
                if (args.TriggerId > 0 && TryResolveRequired(ctx, out MobaEffectExecutionService fallbackEffects))
                    fallbackEffects.ExecuteRulePlan(args.TriggerId, result);
                return;
            }

            // The authored counter key reserves adjacent cast-blackboard IDs for
            // capture, empowerment, affected targets, and consumption. These
            // values roll back with the skill runtime so multi-target hits consume
            // once and still apply the configured effect once to each target.
            var empoweredKey = CreateCastKey(args.KeyId + CastEmpoweredKeyOffset,
                "gameplayCounter.empowered");
            var targetSetKey = CreateCastSetKey(args.KeyId + CastTargetsKeyOffset);

            if (!blackboard.TryGetBool(in empoweredKey, out var empowered))
            {
                // Keep compatibility with plans that only invoke this action
                // from a hit trigger.  New plans should also invoke it from the
                // cast-complete trigger so this decision is made at cast time.
                empowered = variables.Get(counterKey, 0) >= args.Threshold;
                blackboard.SetBool(in empoweredKey, empowered);
            }

            if (!empowered) return;
            if (result.TargetActorId <= 0) return;

            // Reset exactly once, on the first successful hit.  This preserves
            // the pending passive when a projectile misses or its target dies
            // before collision, while the cast blackboard makes AoE consumption
            // independent of target order.
            var consumedKey = CreateCastKey(args.KeyId + CastConsumedKeyOffset,
                "gameplayCounter.consumed");
            if (!blackboard.TryGetBool(in consumedKey, out var consumed) || !consumed)
            {
                blackboard.SetBool(in consumedKey, true);
                variables.Set(counterKey, args.ResetValue);
                AdvanceGameplayCounterPlanActionModule.ReportCounterCue(
                    ctx, args.KeyId, actorId, args.ResetValue, args.Threshold);
            }

            if (!blackboard.AddActorId(in targetSetKey, result.TargetActorId))
            {
                return;
            }

            if (args.TriggerId > 0 && TryResolveRequired(ctx, out MobaEffectExecutionService effects))
            {
                effects.ExecuteRulePlan(args.TriggerId, result);
            }
        }

        private void InitializePerSkillCast(
            SkillCastContext cast,
            ConsumeGameplayCounterArgs args,
            ExecCtx<IWorldResolver> ctx)
        {
            if (cast == null || !cast.RuntimeHandle.IsValid || args.KeyId == 0 ||
                args.ScopePayloadFieldId == 0 || args.Threshold <= 0 || cast.CasterActorId <= 0 ||
                !ctx.Context.TryResolve<MobaSkillCastRuntimeService>(out var runtimes) || runtimes == null ||
                !TryGetBlackboard(runtimes, cast.RuntimeHandle, out var blackboard) || blackboard == null ||
                !TryResolveRequired(ctx, out MobaGameplayVariableService variables) || variables == null)
            {
                return;
            }

            var evaluatedKey = CreateCastKey(args.KeyId + CastEvaluatedKeyOffset,
                "gameplayCounter.evaluated");
            if (blackboard.TryGet(in evaluatedKey, out _)) return;

            var empoweredKey = CreateCastKey(args.KeyId + CastEmpoweredKeyOffset,
                "gameplayCounter.empowered");
            var counterKey = args.ResolveScopedKey(cast.CasterActorId);
            var empowered = variables.Get(counterKey, 0) >= args.Threshold;
            blackboard.SetBool(in evaluatedKey, true);
            blackboard.SetBool(in empoweredKey, empowered);
        }

        private static bool TryGetBlackboard(
            MobaSkillCastRuntimeService runtimes,
            MobaSkillCastRuntimeHandle handle,
            out MobaSkillRuntimeBlackboard blackboard)
            => runtimes.TryGetBlackboard(in handle, out blackboard);

        private static MobaSkillRuntimeBlackboardKey CreateCastKey(int id, string name)
        {
            return new MobaSkillRuntimeBlackboardKey(
                id,
                name,
                MobaSkillRuntimeValueKind.Bool,
                MobaSkillRuntimeBlackboardScope.Cast);
        }

        private static MobaSkillRuntimeBlackboardKey CreateCastSetKey(int id)
        {
            return new MobaSkillRuntimeBlackboardKey(
                id,
                "gameplayCounter.affectedTargets",
                MobaSkillRuntimeValueKind.ActorIdSet,
                MobaSkillRuntimeBlackboardScope.Cast);
        }
    }
}
