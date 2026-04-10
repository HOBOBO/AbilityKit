using System.Collections.Generic;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.BattleDemo.MO;
using AbilityKit.Ability.Impl.Moba;
using AbilityKit.Ability.Share.Impl.Moba.Components;
using AbilityKit.Effect;
using AbilityKit.Core.Common.Event;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    internal sealed class BuffEventPublisher
    {
        private readonly AbilityKit.Triggering.Eventing.IEventBus _eventBus;

        public BuffEventPublisher(AbilityKit.Triggering.Eventing.IEventBus eventBus)
        {
            _eventBus = eventBus;
        }

        public void PublishApplyOrRefresh(BuffMO buff, int sourceActorId, int targetActorId, float durationSeconds, BuffRuntime runtime)
        {
            if (_eventBus == null) return;
            if (buff == null) return;

            PublishBaseEvent(MobaBuffTriggering.Events.ApplyOrRefresh, buff.Id, sourceActorId, targetActorId, durationSeconds, runtime);
        }

        public void PublishRemove(BuffMO buff, int sourceActorId, int targetActorId, BuffRuntime runtime, EffectSourceEndReason reason)
        {
            if (_eventBus == null) return;
            if (buff == null) return;

            PublishStageEvent(MobaBuffTriggering.Events.Remove, buff.OnRemoveEffects, stage: "remove", buffId: buff.Id, sourceActorId, targetActorId, runtime, reason);
        }

        public void PublishPerEffect(string baseEventId, IReadOnlyList<int> effectIds, string stage, int sourceActorId, int targetActorId, BuffRuntime runtime)
        {
            if (_eventBus == null) return;
            if (string.IsNullOrEmpty(baseEventId)) return;
            if (effectIds == null || effectIds.Count == 0) return;

            for (int i = 0; i < effectIds.Count; i++)
            {
                var effectId = effectIds[i];
                if (effectId <= 0) continue;

                var eventId = MobaBuffTriggering.Events.WithEffect(baseEventId, effectId);
                var eid = AbilityKit.Ability.Share.Impl.Moba.Services.TriggeringIdUtil.GetEventEid(eventId);
                var key = new EventKey<BuffEventArgs>(eid);
                var args = new BuffEventArgs
                {
                    EventId = eventId,
                    SourceActorId = sourceActorId,
                    TargetActorId = targetActorId,
                    BuffId = runtime != null ? runtime.BuffId : 0,
                    EffectId = effectId,
                    Stage = stage,
                    StackCount = runtime != null ? runtime.StackCount : 0,
                    DurationSeconds = 0f,
                    RemoveReason = EffectSourceEndReason.None,
                    SourceContextId = runtime != null ? runtime.SourceContextId : 0,
                    Runtime = runtime,
                };

                _eventBus.Publish(key, in args);
                object boxed = args;
                _eventBus.Publish(new EventKey<object>(eid), in boxed);
            }
        }

        public void PublishInterval(BuffMO buff, int sourceActorId, int targetActorId, BuffRuntime runtime)
        {
            if (_eventBus == null) return;
            if (buff == null) return;

            var eventId = MobaBuffTriggering.Events.Interval;
            var eid = AbilityKit.Ability.Share.Impl.Moba.Services.TriggeringIdUtil.GetEventEid(eventId);
            var key = new EventKey<BuffEventArgs>(eid);
            var args = new BuffEventArgs
            {
                EventId = eventId,
                SourceActorId = sourceActorId,
                TargetActorId = targetActorId,
                BuffId = runtime != null ? runtime.BuffId : 0,
                EffectId = 0,
                Stage = "interval",
                StackCount = runtime != null ? runtime.StackCount : 0,
                DurationSeconds = 0f,
                RemoveReason = EffectSourceEndReason.None,
                SourceContextId = runtime != null ? runtime.SourceContextId : 0,
                Runtime = runtime,
            };

            _eventBus.Publish(key, in args);
            object boxed = args;
            _eventBus.Publish(new EventKey<object>(eid), in boxed);
        }

        private void PublishBaseEvent(string eventId, int buffId, int sourceActorId, int targetActorId, float durationSeconds, BuffRuntime runtime)
        {
            var eid = AbilityKit.Ability.Share.Impl.Moba.Services.TriggeringIdUtil.GetEventEid(eventId);
            var key = new EventKey<BuffEventArgs>(eid);
            var args = new BuffEventArgs
            {
                EventId = eventId,
                SourceActorId = sourceActorId,
                TargetActorId = targetActorId,
                BuffId = buffId,
                EffectId = 0,
                Stage = null,
                StackCount = runtime != null ? runtime.StackCount : 0,
                DurationSeconds = durationSeconds,
                RemoveReason = EffectSourceEndReason.None,
                SourceContextId = runtime != null ? runtime.SourceContextId : 0,
                Runtime = runtime,
            };

            _eventBus.Publish(key, in args);
            object boxed = args;
            _eventBus.Publish(new EventKey<object>(eid), in boxed);
        }

        private void PublishStageEvent(string baseEventId, IReadOnlyList<int> effectIds, string stage, int buffId, int sourceActorId, int targetActorId, BuffRuntime runtime, EffectSourceEndReason reason)
        {
            if (_eventBus == null) return;
            if (string.IsNullOrEmpty(baseEventId)) return;

            var eventId0 = baseEventId;
            var eid0 = AbilityKit.Ability.Share.Impl.Moba.Services.TriggeringIdUtil.GetEventEid(eventId0);
            var key0 = new EventKey<BuffEventArgs>(eid0);
            var args0 = new BuffEventArgs
            {
                EventId = eventId0,
                SourceActorId = sourceActorId,
                TargetActorId = targetActorId,
                BuffId = buffId,
                EffectId = 0,
                Stage = stage,
                StackCount = runtime != null ? runtime.StackCount : 0,
                DurationSeconds = 0f,
                RemoveReason = reason,
                SourceContextId = runtime != null ? runtime.SourceContextId : 0,
                Runtime = runtime,
            };
            _eventBus.Publish(key0, in args0);
            object boxed0 = args0;
            _eventBus.Publish(new EventKey<object>(eid0), in boxed0);

            if (effectIds == null || effectIds.Count == 0) return;

            for (int i = 0; i < effectIds.Count; i++)
            {
                var effectId = effectIds[i];
                if (effectId <= 0) continue;

                var eventId = MobaBuffTriggering.Events.WithEffect(baseEventId, effectId);
                var eid = AbilityKit.Ability.Share.Impl.Moba.Services.TriggeringIdUtil.GetEventEid(eventId);
                var key = new EventKey<BuffEventArgs>(eid);
                var args = new BuffEventArgs
                {
                    EventId = eventId,
                    SourceActorId = sourceActorId,
                    TargetActorId = targetActorId,
                    BuffId = buffId,
                    EffectId = effectId,
                    Stage = stage,
                    StackCount = runtime != null ? runtime.StackCount : 0,
                    DurationSeconds = 0f,
                    RemoveReason = reason,
                    SourceContextId = runtime != null ? runtime.SourceContextId : 0,
                    Runtime = runtime,
                };

                _eventBus.Publish(key, in args);
                object boxed = args;
                _eventBus.Publish(new EventKey<object>(eid), in boxed);
            }
        }
    }
}
