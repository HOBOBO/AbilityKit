using System;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.StateMachine;
using AbilityKit.Modifiers;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    [PlanActionModule(order: MobaPlanActionModuleOrders.SetHfsmParameter)]
    public sealed class SetHfsmParameterPlanActionModule : MobaPlanActionModuleBase<SetHfsmParameterArgs, SetHfsmParameterPlanActionModule>
    {
        protected override IActionSchema<SetHfsmParameterArgs, IWorldResolver> Schema => SetHfsmParameterSchema.Instance;

        protected override void Execute(object triggerArgs, SetHfsmParameterArgs args, ExecCtx<IWorldResolver> ctx)
        {
            if (ctx.Context == null || triggerArgs == null || args.ParameterId <= 0 ||
                !MobaCharacterHfsmParameterService.IsValidParameterId(args.ParameterId))
            {
                return;
            }

            var input = MobaPlanActionInputResolver.Resolve(triggerArgs, ctx);
            var actorId = input.CasterActorId > 0 ? input.CasterActorId : input.ExecutionContext.SourceActorId;
            if (actorId <= 0 || !ctx.Context.TryResolve<MobaCharacterHfsmParameterService>(out var parameters) || parameters == null)
            {
                LogRejected(ctx, $"missing caster or HFSM parameter service. parameterId={args.ParameterId}");
                return;
            }
            if (!parameters.SupportsOperation(actorId, args.ParameterId, (int)ModifierOp.Override))
            {
                LogRejected(ctx, $"parameter is not writable by the actor HFSM profile. actor={actorId}, parameter={args.ParameterId}");
                return;
            }

            var ownerId = (long)args.SourceId;
            if (ownerId == 0)
            {
                ownerId = input.ExecutionContext.OwnerContextId;
                if (ownerId == 0) ownerId = input.ExecutionContext.ParentContextId;
                if (ownerId == 0) ownerId = input.ExecutionContext.RootContextId;
                if (ownerId == 0 && input.HasExecutionScope) ownerId = input.ExecutionScope.EffectContextId;
            }
            if (ownerId <= 0)
            {
                LogRejected(ctx, "missing skill execution source context");
                return;
            }

            if (args.Toggle)
            {
                // Toggle contributions outlive one cast, so they must use an
                // authored stable source key instead of the transient execution
                // context selected above.
                if (args.SourceId <= 0 ||
                    !parameters.TryToggleOverride(actorId, args.SourceId, args.ParameterId,
                        (float)args.Value, (float)args.AlternateValue, args.SourceId, args.Priority))
                {
                    LogRejected(ctx, $"toggle requires a stable source_id and an effective parameter value matching one of the configured values. actor={actorId}, parameter={args.ParameterId}");
                    return;
                }

                MobaCharacterHfsmParameterActionRuntime.Reevaluate(ctx.Context, actorId);
                LogApplied(ctx, $"actor={actorId}, owner={args.SourceId}, parameter={args.ParameterId}, toggle={args.Value}<->{args.AlternateValue}");
                return;
            }

            var modifier = ModifierData.Override(
                ModifierKey.FromPacked((uint)args.ParameterId),
                (float)args.Value,
                sourceId: args.SourceId);
            modifier.Priority = args.Priority;
            parameters.Set(actorId, ownerId, args.ParameterId, in modifier);
            // Parameter writes are the HFSM control surface. Re-evaluate the owning
            // character immediately so state entry/exit actions and presentation
            // projections observe the authored transition in the same simulation frame.
            MobaCharacterHfsmParameterActionRuntime.Reevaluate(ctx.Context, actorId);
            LogApplied(ctx, $"actor={actorId}, owner={ownerId}, parameter={args.ParameterId}, value={args.Value}");
        }
    }

    internal static class MobaCharacterHfsmParameterActionRuntime
    {
        public static void Reevaluate(IWorldResolver services, int actorId)
        {
            if (services == null || actorId <= 0 ||
                !services.TryResolve<MobaActorLookupService>(out var actors) || actors == null ||
                !actors.TryGetActorEntity(actorId, out var actor) || actor == null ||
                !actor.hasCharacterHfsm || actor.characterHfsm.Runtime == null)
                return;

            var runtime = actor.characterHfsm.Runtime;
            // A pipeline commit can invoke its trigger before
            // MobaSkillCastInstanceSyncSystem has mirrored the active cast to
            // ECS. Read the authoritative runner directly so the immediate
            // HFSM parameter pass observes the current cast and committed slot.
            if (services.TryResolve<SkillCastCoordinator>(out var skills) && skills != null)
            {
                if (skills.TryGetLatestRunning(actorId, out var cast))
                {
                    var basicAttackSlot = actor.hasSkillLoadout
                        ? actor.skillLoadout.ActiveSkills?.Length ?? 0
                        : 0;
                    runtime.SyncCastContext(cast.InstanceId, cast.SkillId, cast.SkillSlot,
                        basicAttackSlot);
                    runtime.SyncAim(cast.AimPos, cast.AimDir);
                }
                else
                {
                    var basicAttackSlot = actor.hasSkillLoadout
                        ? actor.skillLoadout.ActiveSkills?.Length ?? 0
                        : 0;
                    runtime.SyncCastContext(0, 0, 0, basicAttackSlot);
                }
            }

            runtime.ReevaluateCurrentFrame();
        }
    }
}
