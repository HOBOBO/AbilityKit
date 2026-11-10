using System;
using AbilityKit.Ability;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Events.Unit;
using AbilityKit.Demo.Moba.Gameplay;
using AbilityKit.Demo.Moba.Gameplay.Triggering;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Demo.Moba.Services.StateMachine;
using AbilityKit.Modifiers;
using AbilityKit.Demo.Moba.Triggering;
using AbilityKit.Protocol.Moba.StateSync;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    [PlanActionModule(order: MobaPlanActionModuleOrders.AdvanceGameplayCounter)]
    public sealed class AdvanceGameplayCounterPlanActionModule : MobaPlanActionModuleBase<AdvanceGameplayCounterArgs, AdvanceGameplayCounterPlanActionModule>
    {
        protected override IActionSchema<AdvanceGameplayCounterArgs, IWorldResolver> Schema => AdvanceGameplayCounterSchema.Instance;

        protected override void Execute(object triggerArgs, AdvanceGameplayCounterArgs args, ExecCtx<IWorldResolver> ctx)
        {
            if (ctx.Context == null || triggerArgs == null || args.KeyId == 0 || args.ScopePayloadFieldId == 0 || args.Threshold <= 0d)
            {
                return;
            }

            if (ctx.Payloads == null || !TryReadPayloadDouble(ctx, triggerArgs, args.ScopePayloadFieldId, out var scopeRaw))
            {
                return;
            }

            var scopeValue = (int)Math.Round(scopeRaw);
            if (scopeValue == 0)
            {
                return;
            }

            if (!TryResolveRequired(ctx, out MobaGameplayVariableService variables))
            {
                return;
            }

            var scopedKey = args.ResolveScopedKey(scopeValue);
            var currentValue = variables.Get(scopedKey, 0d);
            if (args.PreserveThreshold && currentValue >= args.Threshold)
            {
                return;
            }

            var nextValue = variables.Add(scopedKey, args.Delta);
            if (nextValue < args.Threshold)
            {
                UpdateHfsmParameter(ctx, args, scopeValue, nextValue);
                ReportCounterCue(ctx, args.KeyId, scopeValue, nextValue, args.Threshold);
                return;
            }

            var thresholdValue = args.PreserveThreshold ? args.Threshold : args.ResetValue;
            variables.Set(scopedKey, thresholdValue);
            UpdateHfsmParameter(ctx, args, scopeValue, args.Threshold);
            ReportCounterCue(ctx, args.KeyId, scopeValue, thresholdValue, args.Threshold);
            if (args.TriggerId <= 0)
            {
                ClearHfsmSourceOnThreshold(ctx, args, scopeValue);
                return;
            }

            if (!TryResolveRequired(ctx, out MobaEffectExecutionService effects))
            {
                return;
            }

            effects.ExecuteRulePlan(args.TriggerId, triggerArgs);
            ClearHfsmSourceOnThreshold(ctx, args, scopeValue);
        }

        private static void ClearHfsmSourceOnThreshold(
            ExecCtx<IWorldResolver> ctx,
            AdvanceGameplayCounterArgs args,
            int actorId)
        {
            if (!args.ClearHfsmSourceOnThreshold || args.HfsmParameterId <= 0 || actorId <= 0 ||
                !MobaCharacterHfsmParameterService.IsValidParameterId(args.HfsmParameterId) ||
                !ctx.Context.TryResolve<MobaCharacterHfsmParameterService>(out var parameters) || parameters == null ||
                !parameters.SupportsOperation(actorId, args.HfsmParameterId, (int)ModifierOp.Override))
            {
                return;
            }

            // A counter source only projects the next phase while the counter is
            // in progress. Once the threshold trigger has installed the durable
            // Buff modifier, remove this source so the Buff remains authoritative.
            parameters.ClearSource(actorId, args.KeyId);
            MobaCharacterHfsmParameterActionRuntime.Reevaluate(ctx.Context, actorId);
        }

        private static void UpdateHfsmParameter(
            ExecCtx<IWorldResolver> ctx,
            AdvanceGameplayCounterArgs args,
            int actorId,
            double value)
        {
            if (args.HfsmParameterId <= 0 || actorId <= 0 ||
                !MobaCharacterHfsmParameterService.IsValidParameterId(args.HfsmParameterId) ||
                !ctx.Context.TryResolve<MobaCharacterHfsmParameterService>(out var parameters) || parameters == null ||
                !parameters.SupportsOperation(actorId, args.HfsmParameterId, (int)ModifierOp.Override))
            {
                return;
            }

            var parameterValue = value * args.HfsmValueScale + args.HfsmValueOffset;
            if (double.IsNaN(parameterValue) || double.IsInfinity(parameterValue) ||
                parameterValue > float.MaxValue || parameterValue < -float.MaxValue)
                throw new InvalidOperationException("Counter HFSM projection must be finite.");

            var modifier = ModifierData.Override(
                ModifierKey.FromPacked((uint)args.HfsmParameterId),
                (float)parameterValue,
                sourceId: args.KeyId);
            parameters.Set(actorId, args.KeyId, args.HfsmParameterId, in modifier);

            // Counter writes are gameplay state changes. If the write happens after
            // the character HFSM system has already ticked this frame (for example
            // from a synchronously committed attack effect), immediately re-evaluate
            // the actor's current hierarchy so parameter-driven state entry actions
            // can publish their slot candidate in the same frame. The HFSM runtime
            // keeps frame/time monotonic; this is only a same-frame condition pass.
            MobaCharacterHfsmParameterActionRuntime.Reevaluate(ctx.Context, actorId);
        }

        internal static void ReportCounterCue(
            ExecCtx<IWorldResolver> ctx,
            int keyId,
            int actorId,
            double value,
            double threshold)
        {
            if (keyId <= 0 || actorId <= 0 || threshold <= 0d ||
                !ctx.Context.TryResolve<MobaPresentationCueSnapshotService>(out var snapshots) || snapshots == null)
            {
                return;
            }

            var count = (int)Math.Max(0d, Math.Min((double)int.MaxValue, value));
            var max = (int)Math.Min((double)int.MaxValue, threshold);
            var key = $"gameplay-counter:{actorId}:{keyId}";
            var isActive = snapshots.TryGetActiveCue(key, out _);
            if (count == 0 && !isActive) return;
            var stage = count == 0
                ? MobaPresentationCueStage.Removed
                : isActive
                    ? MobaPresentationCueStage.StackChanged
                    : MobaPresentationCueStage.Started;
            var entry = new MobaPresentationCueSnapshotEntry
            {
                Stage = (int)stage,
                CueKind = "Buff",
                OwnerKind = "Buff",
                RequestKey = key,
                InstanceKey = key,
                SourceActorId = actorId,
                TargetActorId = actorId,
                StackCount = count,
                MaxStackCount = max,
                Lifetime = (int)PresentationCueLifetime.PersistentState,
                Domain = (int)PresentationCueDomain.Buff,
                ColorR = 1f,
                ColorG = 0.85f,
                ColorB = 0.2f,
                ColorA = 1f,
                Scale = 1f,
            };
            snapshots.Report(in entry);
        }

        private static bool TryReadPayloadDouble(ExecCtx<IWorldResolver> ctx, object triggerArgs, int fieldId, out double value)
        {
            switch (triggerArgs)
            {
                case IEffectContext effectContext:
                    // Skill effects execute with EffectContextWrapper rather than the
                    // original SkillCastContext. Preserve the authored caster scope
                    // field across that wrapper so counters work for every shot flow.
                    if (fieldId == SkillRulePayloadFields.FieldId(SkillRulePayloadFields.CasterActorId) ||
                        fieldId == SkillRulePayloadFields.LegacyFieldId(SkillRulePayloadFields.CasterActorId) ||
                        fieldId == MobaBattlePayloadFields.FieldId(MobaBattlePayloadFields.SourceActorId) ||
                        fieldId == MobaBattlePayloadFields.FieldId(MobaBattlePayloadFields.AttackerActorId))
                    {
                        value = effectContext.SourceActorId;
                        return value > 0;
                    }

                    value = default;
                    return false;
                case DamageResult damageResult:
                    return ctx.Payloads.TryGetDouble(in damageResult, fieldId, out value);
                case AttackInfo attackInfo:
                    return ctx.Payloads.TryGetDouble(in attackInfo, fieldId, out value);
                case UnitDieEventPayload unitDie:
                    return ctx.Payloads.TryGetDouble(in unitDie, fieldId, out value);
                case SkillCastContext skillCast:
                    return ctx.Payloads.TryGetDouble(in skillCast, fieldId, out value);
                default:
                    value = default;
                    return false;
            }
        }
    }
}
