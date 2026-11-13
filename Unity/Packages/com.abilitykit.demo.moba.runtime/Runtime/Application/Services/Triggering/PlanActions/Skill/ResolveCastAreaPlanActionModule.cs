using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Runtime.Application.Services.Triggering;
using AbilityKit.Demo.Moba.Services.Area;
using AbilityKit.Demo.Moba.Services.Projectile;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    public readonly struct ResolveCastAreaArgs
    {
        public readonly int TerminalTriggerId;
        public readonly int MaturationMs;
        public ResolveCastAreaArgs(int triggerId, int maturationMs)
        {
            if (triggerId <= 0 || maturationMs < 0) throw new ArgumentOutOfRangeException(nameof(triggerId));
            TerminalTriggerId = triggerId; MaturationMs = maturationMs;
        }
    }

    public sealed class ResolveCastAreaSchema : MobaPlanActionSchemaBase<ResolveCastAreaArgs>
    {
        public static readonly ResolveCastAreaSchema Instance = new ResolveCastAreaSchema();
        protected override string ActionName => "resolve_cast_area";
        public override ResolveCastAreaArgs ParseArgs(Dictionary<string, ActionArgValue> args, ExecCtx<IWorldResolver> ctx) =>
            new ResolveCastAreaArgs(ReadInt(args, ctx, 0, "terminal_trigger_id"), ReadInt(args, ctx, 0, "maturation_ms"));
        public override bool TryValidateArgs(ReadOnlySpan<KeyValuePair<string, ActionArgValue>> args, out string error) =>
            RequireAny(args, "terminal trigger", out error, "terminal_trigger_id");
    }

    [PlanActionModule(order: MobaPlanActionModuleOrders.ResolveCastArea)]
    public sealed class ResolveCastAreaPlanActionModule : MobaPlanActionModuleBase<ResolveCastAreaArgs, ResolveCastAreaPlanActionModule>
    {
        protected override IActionSchema<ResolveCastAreaArgs, IWorldResolver> Schema => ResolveCastAreaSchema.Instance;
        protected override void Execute(object triggerArgs, ResolveCastAreaArgs args, ExecCtx<IWorldResolver> ctx)
        {
            if (!MobaPlanActionInputResolver.TryResolve(triggerArgs, ctx, out var input) ||
                !ctx.Context.TryResolve<MobaSkillCastRuntimeService>(out var casts) ||
                !ctx.Context.TryResolve<MobaAreaRuntimeService>(out var areas) ||
                !ctx.Context.TryResolve<IFrameTime>(out var time) ||
                !ctx.Context.TryResolve<MobaTriggerExecutionGateway>(out var triggers)) return;
            var payload = triggerArgs is MobaTriggerConditionContext condition ? condition.Payload : triggerArgs;
            var binding = default(MobaSkillChildRecastBinding);
            MobaAreaRuntimeInfo area;
            if (payload is AreaEventArgs evt)
            {
                if (!areas.TryGetArea(evt.AreaId, out area) || !area.SkillRuntimeHandle.Equals(evt.SkillRuntimeHandle)) return;
                binding = new MobaSkillChildRecastBinding(evt.SkillRuntimeHandle,
                    new MobaSkillRuntimeChildRef(MobaSkillRuntimeChildKind.Area, area.AreaId, area.SourceContextId, area.TemplateId));
            }
            else
            {
                var handle = input.ExecutionContext.SkillRuntimeHandle;
                if (!casts.TryGetRecastArea(in handle, out binding) || !areas.TryGetArea((int)binding.Child.ChildId, out area) ||
                    !area.SkillRuntimeHandle.Equals(binding.Original) || area.SourceContextId != binding.Child.ContextId) return;
            }
            if (!casts.TryClaimChildTerminal(in binding)) return;
            // Claim before calling effects: nested callbacks and expiry contend for the same latch.
            var residenceMs = Math.Max(0f, (time.FrameToTime(time.Frame) - time.FrameToTime(new FrameIndex(area.SpawnFrame))) * 1000f);
            var terminal = new AreaEventArgs
            {
                EventId = "area.terminal", AreaId = area.AreaId, TemplateId = area.TemplateId,
                OwnerActorId = area.OwnerActorId, Frame = time.Frame.Value, Center = area.Center, Radius = area.Radius,
                CollisionLayerMask = area.CollisionLayerMask, MaxTargets = area.MaxTargets,
                SourceContextId = area.SourceContextId, RootContextId = area.RootContextId,
                OwnerContextId = area.OwnerContextId, SkillRuntimeHandle = binding.Original,
                ExecutionKind = MobaExecutionKind.AreaExpire, HasResidenceSample = true,
                ResidenceMs = residenceMs,
                ResidenceRatio = args.MaturationMs > 0 ? Math.Min(1f, residenceMs / args.MaturationMs) : 1f,
            };
            try
            {
                var request = MobaTriggerExecutionRequest<AreaEventArgs>.Create(args.TerminalTriggerId, terminal, terminal.EventId);
                triggers.ExecuteDirectTrigger(in request);
            }
            finally { areas.DespawnArea(area.AreaId); }
        }
    }
}
