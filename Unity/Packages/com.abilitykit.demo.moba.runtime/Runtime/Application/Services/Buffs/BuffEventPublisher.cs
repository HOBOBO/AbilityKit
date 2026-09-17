using System.Collections.Generic;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Effect;
using AbilityKit.Core.Eventing;
using AbilityKit.Demo.Moba.Events.Buff;
using AbilityKit.Trace;

using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Buffs.Core;
using AbilityKit.Demo.Moba.Services.Buffs.Runtime;
using AbilityKit.Demo.Moba.Services.Buffs.Presentation;
using AbilityKit.Demo.Moba.Services.Buffs.Triggering;

namespace AbilityKit.Demo.Moba.Services.Buffs {
    /// <summary>
    /// Buff 事件发布器：把生命周期阶段转换成 Triggering EventBus 可消费的 BuffEventArgs。
    /// </summary>
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

        public void PublishRemove(BuffMO buff, int sourceActorId, int targetActorId, BuffRuntime runtime, TraceLifecycleReason reason)
        {
            if (_eventBus == null) return;
            if (buff == null) return;

            PublishStageEvent(MobaBuffTriggering.Events.Remove, buff.OnRemoveEffects, MobaBuffTriggering.Stages.Remove, buff.Id, sourceActorId, targetActorId, runtime, reason);
        }

        /// <summary>
        /// 为每个配置效果额外发布带 effectId 后缀的事件，便于触发器按具体效果订阅。
        /// </summary>
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
                PublishEvent(CreateArgs(eventId, runtime != null ? runtime.BuffId : 0, effectId, stage, sourceActorId, targetActorId, 0f, TraceLifecycleReason.None, runtime));
            }
        }

        public void PublishInterval(BuffMO buff, int sourceActorId, int targetActorId, BuffRuntime runtime)
        {
            if (_eventBus == null) return;
            if (buff == null) return;

            PublishEvent(CreateArgs(MobaBuffTriggering.Events.Interval, runtime != null ? runtime.BuffId : 0, 0, MobaBuffTriggering.Stages.Interval, sourceActorId, targetActorId, 0f, TraceLifecycleReason.None, runtime));
        }

        private void PublishBaseEvent(string eventId, int buffId, int sourceActorId, int targetActorId, float durationSeconds, BuffRuntime runtime)
        {
            PublishEvent(CreateArgs(eventId, buffId, 0, null, sourceActorId, targetActorId, durationSeconds, TraceLifecycleReason.None, runtime));
        }

        private void PublishStageEvent(string baseEventId, IReadOnlyList<int> effectIds, string stage, int buffId, int sourceActorId, int targetActorId, BuffRuntime runtime, TraceLifecycleReason reason)
        {
            if (_eventBus == null) return;
            if (string.IsNullOrEmpty(baseEventId)) return;

            PublishEvent(CreateArgs(baseEventId, buffId, 0, stage, sourceActorId, targetActorId, 0f, reason, runtime));

            if (effectIds == null || effectIds.Count == 0) return;

            for (int i = 0; i < effectIds.Count; i++)
            {
                var effectId = effectIds[i];
                if (effectId <= 0) continue;

                var eventId = MobaBuffTriggering.Events.WithEffect(baseEventId, effectId);
                PublishEvent(CreateArgs(eventId, buffId, effectId, stage, sourceActorId, targetActorId, 0f, reason, runtime));
            }
        }

        private static BuffEventArgs CreateArgs(string eventId, int buffId, int effectId, string stage, int sourceActorId, int targetActorId, float durationSeconds, TraceLifecycleReason reason, BuffRuntime runtime)
        {
            return new BuffEventArgs
            {
                EventId = eventId,
                SourceActorId = sourceActorId,
                TargetActorId = targetActorId,
                BuffId = buffId,
                EffectId = effectId,
                Stage = stage,
                StackCount = runtime != null ? runtime.StackCount : 0,
                DurationSeconds = durationSeconds,
                RemoveReason = reason,
                SourceContextId = runtime != null ? runtime.SourceContextId : 0,
                RuntimeContextId = runtime != null ? runtime.RuntimeContextId : 0L,
                RuntimeContextVersion = runtime != null ? runtime.RuntimeContextVersion : 0L,
                RuntimeContextIdentity = runtime != null ? runtime.RuntimeContextIdentity : default,
                Runtime = runtime,
            };
        }

        /// <summary>
        /// 同时发布强类型和 object 事件，兼容不同触发器监听方式。
        /// </summary>
        private void PublishEvent(BuffEventArgs args)
        {
            var eid = AbilityKit.Demo.Moba.Services.TriggeringIdUtil.GetEventEid(args.EventId);
            var key = new EventKey<BuffEventArgs>(eid);
            _eventBus.Publish(key, in args);
            var objectKey = new EventKey<object>(eid);
            if (_eventBus.HasSubscribers(objectKey))
            {
                object boxed = args;
                _eventBus.Publish(objectKey, in boxed);
            }
        }
    }
}

