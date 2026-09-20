using AbilityKit.Ability.Config;
using AbilityKit.Ability.World.DI;
using AbilityKit.Core.Eventing;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Pipeline;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;
using PresentationEventArgs = AbilityKit.Demo.Moba.Triggering.PresentationEventArgs;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    /// <summary>
    /// Executes the template DSL emit action by resolving its configured emitter and
    /// forwarding the resulting presentation request through the established event bus.
    /// </summary>
    [PlanActionModule(order: MobaPlanActionModuleOrders.Emit)]
    public sealed class EmitPlanActionModule : MobaPlanActionModuleBase<EmitArgs, EmitPlanActionModule>
    {
        protected override IActionSchema<EmitArgs, IWorldResolver> Schema => EmitSchema.Instance;

        protected override void Execute(object triggerArgs, EmitArgs args, ExecCtx<IWorldResolver> ctx)
        {
            if (args.EmitterId <= 0)
            {
                LogRejected(ctx, "requires emitterId > 0");
                return;
            }

            if (!ctx.Context.TryResolve<MobaConfigDatabase>(out var configs) || configs == null)
            {
                LogRejected(ctx, "cannot resolve MobaConfigDatabase");
                return;
            }

            if (!configs.TryGetEmitter(args.EmitterId, out EmitterMO emitter) || emitter == null)
            {
                LogRejected(ctx, $"emitter config not found. emitterId={args.EmitterId}");
                return;
            }

            if (emitter.TemplateId <= 0)
            {
                LogRejected(ctx, $"emitter has no presentation template. emitterId={args.EmitterId}");
                return;
            }

            if (!ctx.Context.TryResolve<AbilityKit.Triggering.Eventing.IEventBus>(out var bus) || bus == null)
            {
                LogRejected(ctx, "cannot resolve IEventBus");
                return;
            }

            var input = MobaPlanActionInputResolver.ResolveEffect(triggerArgs, ctx);
            var casterActorId = input.CasterActorId;
            var targetActorId = input.TargetActorId;
            var targets = targetActorId > 0 ? new[] { targetActorId } : casterActorId > 0 ? new[] { casterActorId } : null;
            var eventName = MobaPresentationTriggering.Events.Play;
            var eventId = TriggeringIdUtil.GetEventEid(eventName);
            var payload = new PresentationEventArgs
            {
                EventId = eventName,
                TemplateId = emitter.TemplateId,
                DurationMsOverride = emitter.DurationMs,
                Targets = targets,
                Positions = new[] { new Vec3(emitter.OffsetX, emitter.OffsetY, emitter.OffsetZ) },
                SourceActorId = casterActorId,
                TargetActorId = targetActorId,
                SourceContextId = input.TraceScope.EffectContextId,
                TraceKind = MobaTraceKind.PresentationPlay
            };

            bus.Publish(new EventKey<PresentationEventArgs>(eventId), in payload);
            var objectKey = new EventKey<object>(eventId);
            if (bus.HasSubscribers(objectKey))
            {
                object boxedPayload = payload;
                bus.Publish(objectKey, in boxedPayload);
            }

            LogApplied(ctx, $"emitted presentation. emitterId={args.EmitterId} templateId={emitter.TemplateId} casterActorId={casterActorId} targetActorId={targetActorId}");
        }
    }
}
