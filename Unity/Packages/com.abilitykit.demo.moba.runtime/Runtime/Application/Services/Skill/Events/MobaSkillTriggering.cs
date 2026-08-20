using AbilityKit.Core.Logging;
using AbilityKit.Core.Eventing;
using AbilityKit.Demo.Moba.Diagnostics;

namespace AbilityKit.Demo.Moba.Services
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

            public const string FailReason = "fail.reason";
        }

        public static void Publish(string eventId, SkillCastContext ctx, string failReason = null)
        {
            if (string.IsNullOrEmpty(eventId)) return;
            if (ctx == null) return;

            ResolveLogger(ctx).LogTriggerEvent(ctx.CasterActorId, ctx.SkillId, ctx.SourceContextId, eventId);

            var oldFailReason = ctx.FailReason;
            try
            {
                if (!string.IsNullOrEmpty(failReason))
                {
                    ctx.FailReason = failReason;
                }

                TryCollect(eventId, ctx);

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

                var eid = TriggeringIdUtil.GetEventEid(eventId);
                var typedKey = new EventKey<SkillCastContext>(eid);
                var objectKey = new EventKey<object>(eid);

                planBus.Publish(typedKey, ctx);
                if (planBus.HasSubscribers(objectKey))
                {
                    object boxed = ctx;
                    planBus.Publish(objectKey, in boxed);
                }
            }
            catch (System.Exception ex)
            {
                Log.Exception(ex, $"[MobaSkillTriggering] Forward to plan eventBus failed. eventId={eventId}");
            }
            finally
            {
                ctx.FailReason = oldFailReason;
            }
        }

        public static bool TryCreateDiagnosticDraft(
            string eventId,
            SkillCastContext ctx,
            out MobaBattleDiagnosticEventDraft draft)
        {
            draft = default;
            if (ctx == null || !TryMapLifecycle(eventId, out var kind, out var outcome))
            {
                return false;
            }

            var handle = ctx.RuntimeHandle;
            var runtime = handle.IsValid
                ? new BattleDiagnosticRuntimeHandle(handle.RuntimeId, handle.Generation)
                : default;
            var rootContextId = handle.RootTraceContextId != 0L
                ? handle.RootTraceContextId
                : ctx.SourceContextId;
            var summary = string.IsNullOrEmpty(ctx.FailReason)
                ? eventId
                : eventId + ": " + ctx.FailReason;

            draft = new MobaBattleDiagnosticEventDraft(
                kind,
                BattleDiagnosticEventChannel.Skill,
                outcome,
                ctx.CasterActorId,
                ctx.TargetActorId,
                ctx.SkillId,
                rootContextId,
                ctx.SourceContextId,
                runtime,
                summary: summary);
            return true;
        }

        private static void TryCollect(string eventId, SkillCastContext ctx)
        {
            try
            {
                var services = ctx.WorldServices;
                if (services == null ||
                    !services.TryResolve<IMobaBattleDiagnosticEventSink>(out var collector) ||
                    collector == null ||
                    !collector.IsEnabled(BattleDiagnosticEventChannel.Skill) ||
                    !TryCreateDiagnosticDraft(eventId, ctx, out var draft))
                {
                    return;
                }

                collector.TryCollect(in draft);
            }
            catch
            {
            }
        }

        private static bool TryMapLifecycle(
            string eventId,
            out BattleDiagnosticEventKind kind,
            out BattleDiagnosticEventOutcome outcome)
        {
            switch (eventId)
            {
                case Events.PreCastStart:
                case Events.CastStart:
                    kind = BattleDiagnosticEventKind.SkillRuntimeStarted;
                    outcome = BattleDiagnosticEventOutcome.None;
                    return true;
                case Events.PreCastComplete:
                case Events.CastComplete:
                    kind = BattleDiagnosticEventKind.SkillRuntimeEnded;
                    outcome = BattleDiagnosticEventOutcome.Succeeded;
                    return true;
                case Events.PreCastFail:
                case Events.CastFail:
                    kind = BattleDiagnosticEventKind.SkillRuntimeEnded;
                    outcome = BattleDiagnosticEventOutcome.Failed;
                    return true;
                case Events.PreCastInterrupt:
                case Events.CastInterrupt:
                    kind = BattleDiagnosticEventKind.SkillRuntimeEnded;
                    outcome = BattleDiagnosticEventOutcome.Interrupted;
                    return true;
                default:
                    kind = BattleDiagnosticEventKind.Unknown;
                    outcome = BattleDiagnosticEventOutcome.None;
                    return false;
            }
        }

        private static ISkillLogger ResolveLogger(SkillCastContext ctx)
        {
            var services = ctx != null ? ctx.WorldServices : null;
            if (services != null && services.TryResolve<ISkillLogger>(out var logger) && logger != null)
            {
                return logger;
            }

            return SkillLogger.Instance;
        }
    }
}
