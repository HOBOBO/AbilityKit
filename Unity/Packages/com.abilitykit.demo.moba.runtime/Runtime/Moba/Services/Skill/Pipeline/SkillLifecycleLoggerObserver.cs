using System;
using AbilityKit.Core.Common.Log;

namespace AbilityKit.Demo.Moba.Services
{
    public sealed class SkillLifecycleLoggerObserver : SkillLifecycleObserverAdapter
    {
        private static SkillLifecycleLoggerObserver _instance;
        public static SkillLifecycleLoggerObserver Instance => _instance ??= new SkillLifecycleLoggerObserver();

        public bool EnableLogging { get; set; } = false;

        public override void OnSkillLifecycleEvent(in SkillLifecycleEvent evt)
        {
            if (!EnableLogging) return;
            base.OnSkillLifecycleEvent(in evt);
        }

        protected override void OnStartRequested(in SkillLifecycleEvent evt)
        {
            SkillLogger.Instance.LogSkillStart(
                evt.CasterActorId, evt.SkillId, evt.SkillSlot, evt.SkillLevel,
                evt.TargetActorId, evt.Vec3Param, default, evt.InstanceId);
        }

        protected override void OnStartSucceeded(in SkillLifecycleEvent evt)
        {
            SkillLogger.Instance.LogInfo($"StartSucceeded: Caster={evt.CasterActorId} SkillId={evt.SkillId} InstanceId={evt.InstanceId}");
        }

        protected override void OnStartFailed(in SkillLifecycleEvent evt)
        {
            SkillLogger.Instance.LogSkillFail(evt.CasterActorId, evt.SkillId, evt.InstanceId, evt.StringParam ?? "unknown");
        }

        protected override void OnPhaseStarting(in SkillLifecycleEvent evt)
        {
            SkillLogger.Instance.LogSkillStage(evt.CasterActorId, evt.SkillId, evt.InstanceId, evt.PhaseId, "Starting");
        }

        protected override void OnPhaseCompleted(in SkillLifecycleEvent evt)
        {
            SkillLogger.Instance.LogSkillStage(evt.CasterActorId, evt.SkillId, evt.InstanceId, evt.PhaseId, "Completed");
        }

        protected override void OnPhaseFailed(in SkillLifecycleEvent evt)
        {
            SkillLogger.Instance.LogSkillFail(evt.CasterActorId, evt.SkillId, evt.InstanceId, $"{evt.PhaseId}: {evt.StringParam}");
        }

        protected override void OnCompleted(in SkillLifecycleEvent evt)
        {
            SkillLogger.Instance.LogSkillComplete(evt.CasterActorId, evt.SkillId, evt.InstanceId, evt.ElapsedMs);
        }

        protected override void OnCancelled(in SkillLifecycleEvent evt)
        {
            SkillLogger.Instance.LogSkillCancel(evt.CasterActorId, evt.SkillId, evt.InstanceId, evt.StringParam ?? "unknown");
        }

        protected override void OnInterrupted(in SkillLifecycleEvent evt)
        {
            SkillLogger.Instance.LogSkillInterrupt(evt.CasterActorId, evt.SkillId, evt.InstanceId, evt.PhaseId);
        }

        protected override void OnTicking(in SkillLifecycleEvent evt)
        {
            if (evt.ExtraData.TryGetValue("DeltaTimeMs", out var dt) && evt.ExtraData.TryGetValue("State", out var state))
            {
                SkillLogger.Instance.LogSkillTick(
                    evt.CasterActorId, evt.SkillId, evt.InstanceId,
                    Convert.ToSingle(dt), evt.ElapsedMs,
                    state?.ToString() ?? "unknown");
            }
        }

        protected override void OnTriggerEvaluated(in SkillLifecycleEvent evt)
        {
            SkillLogger.Instance.LogTriggerEvaluate(
                evt.CasterActorId, evt.SkillId, evt.InstanceId,
                evt.StringParam, evt.BoolParam);
        }

        protected override void OnTriggerExecuted(in SkillLifecycleEvent evt)
        {
            var actionType = evt.ExtraData.TryGetValue("ActionType", out var at) ? at?.ToString() : null;
            SkillLogger.Instance.LogTriggerExecute(
                evt.CasterActorId, evt.SkillId, evt.InstanceId,
                evt.StringParam, actionType);
        }

        protected override void OnBuffApplied(in SkillLifecycleEvent evt)
        {
            SkillLogger.Instance.LogBuffApply(evt.TargetActorId, evt.CasterActorId, evt.SkillId, evt.IntParam, evt.InstanceId);
        }

        protected override void OnBuffRemoved(in SkillLifecycleEvent evt)
        {
            SkillLogger.Instance.LogBuffRemove(evt.TargetActorId, evt.SkillId, 0, evt.InstanceId, evt.StringParam ?? "unknown");
        }

        protected override void OnPassiveTriggered(in SkillLifecycleEvent evt)
        {
            SkillLogger.Instance.LogPassiveTrigger(evt.CasterActorId, evt.SkillId, evt.InstanceId, evt.StringParam, evt.BoolParam);
        }

        protected override void OnPassiveRegistered(in SkillLifecycleEvent evt)
        {
            SkillLogger.Instance.LogPassiveRegister(evt.CasterActorId, evt.SkillId, evt.InstanceId);
        }

        protected override void OnPassiveUnregistered(in SkillLifecycleEvent evt)
        {
            SkillLogger.Instance.LogPassiveUnregister(evt.CasterActorId, evt.SkillId, evt.InstanceId, evt.StringParam ?? "unknown");
        }
    }
}
