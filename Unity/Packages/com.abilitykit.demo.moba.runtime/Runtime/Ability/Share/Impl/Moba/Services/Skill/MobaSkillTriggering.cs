using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Share.Effect;
using AbilityKit.Ability.Impl.Moba;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public static class MobaSkillTriggering
    {
        public static class Events
        {
            public const string PreCastStart = "skill.precast.start";
            public const string PreCastComplete = "skill.precast.complete";
            public const string PreCastFail = "skill.precast.fail";
            public const string PreCastInterrupt = "skill.precast.interrupt";

            public const string CastStart = "skill.cast.start";
            public const string CastComplete = "skill.cast.complete";
            public const string CastFail = "skill.cast.fail";
            public const string CastInterrupt = "skill.cast.interrupt";
        }

        public static class Args
        {
            public const string SkillId = MobaSkillTriggerArgs.SkillId;
            public const string SkillSlot = MobaSkillTriggerArgs.SkillSlot;
            public const string SkillLevel = MobaSkillTriggerArgs.SkillLevel;
            public const string CasterActorId = MobaSkillTriggerArgs.CasterActorId;
            public const string TargetActorId = MobaSkillTriggerArgs.TargetActorId;
            public const string AimPos = MobaSkillTriggerArgs.AimPos;
            public const string AimDir = MobaSkillTriggerArgs.AimDir;

            public const string FailReason = MobaSkillPipelineSharedKeys.FailReason;
        }

        public static void Publish(IEventBus bus, string eventId, in SkillCastRequest req, string failReason = null)
        {
            if (bus == null) return;
            if (string.IsNullOrEmpty(eventId)) return;

            // Compatibility: request does not carry runtime level, so default to 0.
            // Canonical payload for skill events is SkillCastContext.
            var ctx = SkillCastContext.FromRequest(in req, skillLevel: 0);
            Publish(bus, eventId, ctx, failReason);
        }

        public static void Publish(IEventBus bus, string eventId, SkillCastContext ctx, string failReason = null)
        {
            if (bus == null) return;
            if (string.IsNullOrEmpty(eventId)) return;
            if (ctx == null) return;

            var args = PooledTriggerArgs.Rent();
            args[EffectTriggering.Args.Source] = ctx.CasterActorId;
            args[EffectTriggering.Args.Target] = ctx.TargetActorId;
            args[EffectTriggering.Args.OriginSource] = ctx.CasterActorId;
            args[EffectTriggering.Args.OriginTarget] = ctx.TargetActorId;
            args[EffectTriggering.Args.OriginKind] = EffectSourceKind.SkillCast;
            args[EffectTriggering.Args.OriginConfigId] = ctx.SkillId;
            args[EffectTriggering.Args.OriginContextId] = ctx.SourceContextId;
            args[Args.SkillId] = ctx.SkillId;
            args[Args.SkillSlot] = ctx.SkillSlot;
            args[Args.CasterActorId] = ctx.CasterActorId;
            args[Args.TargetActorId] = ctx.TargetActorId;
            args[Args.AimPos] = ctx.AimPos;
            args[Args.AimDir] = ctx.AimDir;
            args[MobaSkillTriggerArgs.SkillLevel] = ctx.SkillLevel;

            if (!string.IsNullOrEmpty(failReason))
            {
                args[Args.FailReason] = failReason;
            }

            bus.Publish(new TriggerEvent(eventId, payload: ctx, args: args));
        }
    }
}
