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

            var args = PooledTriggerArgs.Rent();
            args[EffectTriggering.Args.Source] = req.CasterActorId;
            args[EffectTriggering.Args.Target] = req.TargetActorId;
            args[EffectTriggering.Args.OriginSource] = req.CasterActorId;
            args[EffectTriggering.Args.OriginTarget] = req.TargetActorId;
            args[EffectTriggering.Args.OriginKind] = EffectSourceKind.SkillCast;
            args[EffectTriggering.Args.OriginConfigId] = req.SkillId;
            args[Args.SkillId] = req.SkillId;
            args[Args.SkillSlot] = req.SkillSlot;
            args[Args.CasterActorId] = req.CasterActorId;
            args[Args.TargetActorId] = req.TargetActorId;
            args[Args.AimPos] = req.AimPos;
            args[Args.AimDir] = req.AimDir;

            if (!string.IsNullOrEmpty(failReason))
            {
                args[Args.FailReason] = failReason;
            }

            bus.Publish(new TriggerEvent(eventId, payload: req, args: args));
        }
    }
}
