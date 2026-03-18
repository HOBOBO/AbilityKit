using System;
using System.Collections.Generic;
using AbilityKit.Ability.Share.Math;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    #region Event Types

    public enum SkillLifecycleEventType
    {
        // Start events
        StartRequested,
        StartSucceeded,
        StartFailed,

        // Phase events
        PhaseStarting,
        PhaseCompleted,
        PhaseFailed,

        // Complete events
        Completed,
        Cancelled,
        Interrupted,

        // Tick events
        Ticking,
        TimelineEvent,

        // Effect events
        EffectTriggered,

        // Trigger events
        TriggerEvaluated,
        TriggerExecuted,
        TriggerShortCircuited,

        // Buff events
        BuffApplied,
        BuffRemoved,
        BuffRefreshed,

        // Passive events
        PassiveRegistered,
        PassiveUnregistered,
        PassiveTriggered,
    }

    #endregion

    #region Event Data

    public class SkillLifecycleEvent
    {
        public SkillLifecycleEventType Type { get; set; }
        public int CasterActorId { get; set; }
        public int SkillId { get; set; }
        public long InstanceId { get; set; }
        public int TargetActorId { get; set; }
        public int SkillSlot { get; set; }
        public int SkillLevel { get; set; }
        public string PhaseId { get; set; }
        public float ElapsedMs { get; set; }

        public string StringParam { get; set; }
        public int IntParam { get; set; }
        public bool BoolParam { get; set; }
        public Vec3 Vec3Param { get; set; }

        public Dictionary<string, object> ExtraData { get; } = new Dictionary<string, object>();

        public static SkillLifecycleEvent Create(SkillLifecycleEventType type, int casterId, int skillId, long instanceId)
        {
            return new SkillLifecycleEvent
            {
                Type = type,
                CasterActorId = casterId,
                SkillId = skillId,
                InstanceId = instanceId,
            };
        }
    }

    #endregion

    #region Observer Interface

    public interface ISkillLifecycleObserver
    {
        void OnSkillLifecycleEvent(in SkillLifecycleEvent evt);
    }

    #endregion

    #region Event Bus / Dispatcher

    public interface ISkillLifecycleEventBus
    {
        void Subscribe(ISkillLifecycleObserver observer);
        void Unsubscribe(ISkillLifecycleObserver observer);
        void Publish(in SkillLifecycleEvent evt);
        void Clear();
    }

    public sealed class SkillLifecycleEventBus : ISkillLifecycleEventBus
    {
        private readonly List<ISkillLifecycleObserver> _observers = new List<ISkillLifecycleObserver>();
        private readonly List<ISkillLifecycleObserver> _tmp = new List<ISkillLifecycleObserver>();

        public void Subscribe(ISkillLifecycleObserver observer)
        {
            if (observer != null && !_observers.Contains(observer))
            {
                _observers.Add(observer);
            }
        }

        public void Unsubscribe(ISkillLifecycleObserver observer)
        {
            _observers.Remove(observer);
        }

        public void Publish(in SkillLifecycleEvent evt)
        {
            if (_observers.Count == 0) return;

            _tmp.Clear();
            _tmp.AddRange(_observers);

            foreach (var observer in _tmp)
            {
                try
                {
                    observer.OnSkillLifecycleEvent(in evt);
                }
                catch { }
            }
        }

        public void Clear()
        {
            _observers.Clear();
        }
    }

    #endregion

    #region Observer Adapters

    public abstract class SkillLifecycleObserverAdapter : ISkillLifecycleObserver
    {
        public virtual void OnSkillLifecycleEvent(in SkillLifecycleEvent evt)
        {
            switch (evt.Type)
            {
                case SkillLifecycleEventType.StartRequested:
                    OnStartRequested(in evt);
                    break;
                case SkillLifecycleEventType.StartSucceeded:
                    OnStartSucceeded(in evt);
                    break;
                case SkillLifecycleEventType.StartFailed:
                    OnStartFailed(in evt);
                    break;
                case SkillLifecycleEventType.PhaseStarting:
                    OnPhaseStarting(in evt);
                    break;
                case SkillLifecycleEventType.PhaseCompleted:
                    OnPhaseCompleted(in evt);
                    break;
                case SkillLifecycleEventType.PhaseFailed:
                    OnPhaseFailed(in evt);
                    break;
                case SkillLifecycleEventType.Completed:
                    OnCompleted(in evt);
                    break;
                case SkillLifecycleEventType.Cancelled:
                    OnCancelled(in evt);
                    break;
                case SkillLifecycleEventType.Interrupted:
                    OnInterrupted(in evt);
                    break;
                case SkillLifecycleEventType.Ticking:
                    OnTicking(in evt);
                    break;
                case SkillLifecycleEventType.TimelineEvent:
                    OnTimelineEvent(in evt);
                    break;
                case SkillLifecycleEventType.EffectTriggered:
                    OnEffectTriggered(in evt);
                    break;
                case SkillLifecycleEventType.TriggerEvaluated:
                    OnTriggerEvaluated(in evt);
                    break;
                case SkillLifecycleEventType.TriggerExecuted:
                    OnTriggerExecuted(in evt);
                    break;
                case SkillLifecycleEventType.TriggerShortCircuited:
                    OnTriggerShortCircuited(in evt);
                    break;
                case SkillLifecycleEventType.BuffApplied:
                    OnBuffApplied(in evt);
                    break;
                case SkillLifecycleEventType.BuffRemoved:
                    OnBuffRemoved(in evt);
                    break;
                case SkillLifecycleEventType.BuffRefreshed:
                    OnBuffRefreshed(in evt);
                    break;
                case SkillLifecycleEventType.PassiveRegistered:
                    OnPassiveRegistered(in evt);
                    break;
                case SkillLifecycleEventType.PassiveUnregistered:
                    OnPassiveUnregistered(in evt);
                    break;
                case SkillLifecycleEventType.PassiveTriggered:
                    OnPassiveTriggered(in evt);
                    break;
            }
        }

        protected virtual void OnStartRequested(in SkillLifecycleEvent evt) { }
        protected virtual void OnStartSucceeded(in SkillLifecycleEvent evt) { }
        protected virtual void OnStartFailed(in SkillLifecycleEvent evt) { }
        protected virtual void OnPhaseStarting(in SkillLifecycleEvent evt) { }
        protected virtual void OnPhaseCompleted(in SkillLifecycleEvent evt) { }
        protected virtual void OnPhaseFailed(in SkillLifecycleEvent evt) { }
        protected virtual void OnCompleted(in SkillLifecycleEvent evt) { }
        protected virtual void OnCancelled(in SkillLifecycleEvent evt) { }
        protected virtual void OnInterrupted(in SkillLifecycleEvent evt) { }
        protected virtual void OnTicking(in SkillLifecycleEvent evt) { }
        protected virtual void OnTimelineEvent(in SkillLifecycleEvent evt) { }
        protected virtual void OnEffectTriggered(in SkillLifecycleEvent evt) { }
        protected virtual void OnTriggerEvaluated(in SkillLifecycleEvent evt) { }
        protected virtual void OnTriggerExecuted(in SkillLifecycleEvent evt) { }
        protected virtual void OnTriggerShortCircuited(in SkillLifecycleEvent evt) { }
        protected virtual void OnBuffApplied(in SkillLifecycleEvent evt) { }
        protected virtual void OnBuffRemoved(in SkillLifecycleEvent evt) { }
        protected virtual void OnBuffRefreshed(in SkillLifecycleEvent evt) { }
        protected virtual void OnPassiveRegistered(in SkillLifecycleEvent evt) { }
        protected virtual void OnPassiveUnregistered(in SkillLifecycleEvent evt) { }
        protected virtual void OnPassiveTriggered(in SkillLifecycleEvent evt) { }
    }

    #endregion

    #region Static Helper

    public static class SkillLifecycleEvents
    {
        public static SkillLifecycleEvent StartRequested(int casterId, int skillId, long instanceId, int targetId, int slot, int level)
        {
            return new SkillLifecycleEvent
            {
                Type = SkillLifecycleEventType.StartRequested,
                CasterActorId = casterId,
                SkillId = skillId,
                InstanceId = instanceId,
                TargetActorId = targetId,
                SkillSlot = slot,
                SkillLevel = level,
            };
        }

        public static SkillLifecycleEvent StartSucceeded(int casterId, int skillId, long instanceId)
        {
            return new SkillLifecycleEvent
            {
                Type = SkillLifecycleEventType.StartSucceeded,
                CasterActorId = casterId,
                SkillId = skillId,
                InstanceId = instanceId,
            };
        }

        public static SkillLifecycleEvent StartFailed(int casterId, int skillId, long instanceId, string reason)
        {
            return new SkillLifecycleEvent
            {
                Type = SkillLifecycleEventType.StartFailed,
                CasterActorId = casterId,
                SkillId = skillId,
                InstanceId = instanceId,
                StringParam = reason,
            };
        }

        public static SkillLifecycleEvent PhaseStarting(int casterId, int skillId, long instanceId, string phaseId)
        {
            return new SkillLifecycleEvent
            {
                Type = SkillLifecycleEventType.PhaseStarting,
                CasterActorId = casterId,
                SkillId = skillId,
                InstanceId = instanceId,
                PhaseId = phaseId,
            };
        }

        public static SkillLifecycleEvent PhaseCompleted(int casterId, int skillId, long instanceId, string phaseId, float elapsedMs)
        {
            return new SkillLifecycleEvent
            {
                Type = SkillLifecycleEventType.PhaseCompleted,
                CasterActorId = casterId,
                SkillId = skillId,
                InstanceId = instanceId,
                PhaseId = phaseId,
                ElapsedMs = elapsedMs,
            };
        }

        public static SkillLifecycleEvent PhaseFailed(int casterId, int skillId, long instanceId, string phaseId, string reason)
        {
            return new SkillLifecycleEvent
            {
                Type = SkillLifecycleEventType.PhaseFailed,
                CasterActorId = casterId,
                SkillId = skillId,
                InstanceId = instanceId,
                PhaseId = phaseId,
                StringParam = reason,
            };
        }

        public static SkillLifecycleEvent Completed(int casterId, int skillId, long instanceId, float elapsedMs)
        {
            return new SkillLifecycleEvent
            {
                Type = SkillLifecycleEventType.Completed,
                CasterActorId = casterId,
                SkillId = skillId,
                InstanceId = instanceId,
                ElapsedMs = elapsedMs,
            };
        }

        public static SkillLifecycleEvent Cancelled(int casterId, int skillId, long instanceId, string cancelType)
        {
            return new SkillLifecycleEvent
            {
                Type = SkillLifecycleEventType.Cancelled,
                CasterActorId = casterId,
                SkillId = skillId,
                InstanceId = instanceId,
                StringParam = cancelType,
            };
        }

        public static SkillLifecycleEvent Interrupted(int casterId, int skillId, long instanceId, string phaseId)
        {
            return new SkillLifecycleEvent
            {
                Type = SkillLifecycleEventType.Interrupted,
                CasterActorId = casterId,
                SkillId = skillId,
                InstanceId = instanceId,
                PhaseId = phaseId,
            };
        }

        public static SkillLifecycleEvent Ticking(int casterId, int skillId, long instanceId, float deltaTimeMs, float elapsedMs, string state)
        {
            return new SkillLifecycleEvent
            {
                Type = SkillLifecycleEventType.Ticking,
                CasterActorId = casterId,
                SkillId = skillId,
                InstanceId = instanceId,
                ElapsedMs = elapsedMs,
                ExtraData = { ["DeltaTimeMs"] = deltaTimeMs, ["State"] = state },
            };
        }

        public static SkillLifecycleEvent TriggerEvaluated(int casterId, int skillId, long instanceId, string eventKey, bool passed)
        {
            return new SkillLifecycleEvent
            {
                Type = SkillLifecycleEventType.TriggerEvaluated,
                CasterActorId = casterId,
                SkillId = skillId,
                InstanceId = instanceId,
                StringParam = eventKey,
                BoolParam = passed,
            };
        }

        public static SkillLifecycleEvent TriggerExecuted(int casterId, int skillId, long instanceId, string eventKey, string actionType)
        {
            return new SkillLifecycleEvent
            {
                Type = SkillLifecycleEventType.TriggerExecuted,
                CasterActorId = casterId,
                SkillId = skillId,
                InstanceId = instanceId,
                StringParam = eventKey,
                ExtraData = { ["ActionType"] = actionType },
            };
        }

        public static SkillLifecycleEvent BuffApplied(int targetId, int sourceId, int buffId, int stackCount, long contextId)
        {
            return new SkillLifecycleEvent
            {
                Type = SkillLifecycleEventType.BuffApplied,
                CasterActorId = sourceId,
                TargetActorId = targetId,
                SkillId = buffId,
                InstanceId = contextId,
                IntParam = stackCount,
            };
        }

        public static SkillLifecycleEvent BuffRemoved(int targetId, int buffId, long contextId, string reason)
        {
            return new SkillLifecycleEvent
            {
                Type = SkillLifecycleEventType.BuffRemoved,
                TargetActorId = targetId,
                SkillId = buffId,
                InstanceId = contextId,
                StringParam = reason,
            };
        }

        public static SkillLifecycleEvent PassiveTriggered(int ownerId, int passiveId, long contextId, string triggerEvent, bool triggered)
        {
            return new SkillLifecycleEvent
            {
                Type = SkillLifecycleEventType.PassiveTriggered,
                CasterActorId = ownerId,
                SkillId = passiveId,
                InstanceId = contextId,
                StringParam = triggerEvent,
                BoolParam = triggered,
            };
        }

        public static SkillLifecycleEvent PassiveRegistered(int ownerId, int passiveId, long contextId)
        {
            return new SkillLifecycleEvent
            {
                Type = SkillLifecycleEventType.PassiveRegistered,
                CasterActorId = ownerId,
                SkillId = passiveId,
                InstanceId = contextId,
            };
        }

        public static SkillLifecycleEvent PassiveUnregistered(int ownerId, int passiveId, long contextId, string reason)
        {
            return new SkillLifecycleEvent
            {
                Type = SkillLifecycleEventType.PassiveUnregistered,
                CasterActorId = ownerId,
                SkillId = passiveId,
                InstanceId = contextId,
                StringParam = reason,
            };
        }
    }

    #endregion
}
