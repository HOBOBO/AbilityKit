using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Ability.Config;
using AbilityKit.Ability.World.DI;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Core.Eventing;
using AbilityKit.Core.Mathematics;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Pipeline;
using PresentationEventArgs = AbilityKit.Demo.Moba.Triggering.PresentationEventArgs;
using AbilityKit.Demo.Moba.Systems;


namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    [PlanActionModule(order: MobaPlanActionModuleOrders.PlayPresentation)]
    public sealed class PlayPresentationPlanActionModule : MobaPlanActionModuleBase<PlayPresentationArgs, PlayPresentationPlanActionModule>
    {
        protected override IActionSchema<PlayPresentationArgs, IWorldResolver> Schema => PlayPresentationSchema.Instance;

        protected override void Execute(object triggerArgs, PlayPresentationArgs args, ExecCtx<IWorldResolver> ctx)
        {
            if (args.TemplateId <= 0)
            {
                LogRejected(ctx, "requires templateId > 0");
                return;
            }

            if (!ctx.Context.TryResolve<AbilityKit.Triggering.Eventing.IEventBus>(out var bus) || bus == null)
            {
                LogRejected(ctx, "cannot resolve IEventBus");
                return;
            }

            var input = MobaPlanActionInputResolver.ResolveEffect(triggerArgs, ctx);
            var targetActorId = input.TargetActorId;
            var casterActorId = input.CasterActorId;
            var mode = (PresentationTargetMode)args.TargetMode;

            int[] targets = null;
            Vec3[] positions = null;
            long sourceContextId = input.TraceScope.EffectContextId;

            switch (mode)
            {
                case PresentationTargetMode.Self:
                    if (casterActorId > 0)
                        targets = new[] { casterActorId };
                    break;

                case PresentationTargetMode.Source:
                    if (casterActorId > 0)
                        targets = new[] { casterActorId };
                    break;

                case PresentationTargetMode.Target:
                    if (targetActorId > 0)
                        targets = new[] { targetActorId };
                    break;

                case PresentationTargetMode.Position:
                    positions = new[] { new Vec3(args.X, args.Y, args.Z) };
                    break;

                case PresentationTargetMode.PayloadTarget:
                    if (targetActorId > 0)
                        targets = new[] { targetActorId };
                    break;
            }

            var eventName = args.Stop ? MobaPresentationTriggering.Events.Stop : MobaPresentationTriggering.Events.Play;
            var eid = TriggeringIdUtil.GetEventEid(eventName);

            var payload = new PresentationEventArgs
            {
                EventId = eventName,
                TemplateId = args.TemplateId,
                RequestKey = args.RequestKey,
                DurationMsOverride = args.DurationMs,
                Targets = targets,
                Positions = positions,
                SourceActorId = casterActorId,
                TargetActorId = targetActorId,
                SourceContextId = sourceContextId,
                TraceKind = args.Stop ? MobaTraceKind.PresentationStop : MobaTraceKind.PresentationPlay,
                Scale = args.Scale != 1 ? args.Scale : (float?)null,
                Radius = args.Radius > 0 ? args.Radius : (float?)null,
            };

            bus.Publish(new EventKey<PresentationEventArgs>(eid), in payload);
            var objectKey = new EventKey<object>(eid);
            if (bus.HasSubscribers(objectKey))
            {
                object boxedPayload = payload;
                bus.Publish(objectKey, in boxedPayload);
            }
        }
    }
}
