using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Core.Eventing;

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

        public static void Publish(string eventId, SkillCastContext ctx, string failReason = null)
        {
            if (string.IsNullOrEmpty(eventId)) return;
            if (ctx == null) return;

            try
            {
                var services = ctx.WorldServices;
                if (services == null)
                {
                    Log.Info($"[MobaSkillTriggering] Forward skipped: WorldServices is null. eventId={eventId}");
                    return;
                }

                if (!services.TryResolve<AbilityKit.Triggering.Eventing.IEventBus>(out var planBus) || planBus == null)
                {
                    Log.Info($"[MobaSkillTriggering] Forward skipped: plan IEventBus not found. eventId={eventId}");
                    return;
                }

                var oldFailReason = ctx.FailReason;
                if (!string.IsNullOrEmpty(failReason))
                {
                    ctx.FailReason = failReason;
                }

                var eid = AbilityKit.Triggering.Eventing.StableStringId.Get("event:" + eventId);
                planBus.Publish(new EventKey<SkillCastContext>(eid), ctx);
                Log.Info($"[MobaSkillTriggering] Forwarded to plan bus. eventId={eventId} eid={eid}");

                ctx.FailReason = oldFailReason;
            }
            catch (System.Exception ex)
            {
                Log.Exception(ex, $"[MobaSkillTriggering] Forward to plan eventBus failed. eventId={eventId}");
            }
        }
    }
}
