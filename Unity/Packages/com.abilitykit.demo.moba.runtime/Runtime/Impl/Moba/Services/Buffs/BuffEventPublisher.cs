using System.Collections.Generic;
using AbilityKit.Ability.Impl.Moba;
using AbilityKit.Ability.Impl.Moba.Conponents;
using AbilityKit.Ability.Share.Effect;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Triggering.Runtime;
using AbilityKit.Ability.Impl.Moba.EffectSource;
using AbilityKit.Core.Eventing;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    internal sealed class BuffEventPublisher
    {
        private readonly IEventBus _bus;
        private readonly AbilityKit.Triggering.Eventing.IEventBus _strongBus;
        private readonly EffectSourceRegistry _effectSource;

        public BuffEventPublisher(IEventBus bus, EffectSourceRegistry effectSource)
        {
            _bus = bus;
            _strongBus = bus as AbilityKit.Triggering.Eventing.IEventBus;
            _effectSource = effectSource;
        }

        public void PublishApplyOrRefresh(global::AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO.BuffMO buff, int sourceActorId, int targetActorId, float durationSeconds, BuffRuntime runtime)
        {
            if (_bus == null) return;
            if (buff == null) return;

            PublishBaseEvent(MobaBuffTriggering.Events.ApplyOrRefresh, buff.Id, sourceActorId, targetActorId, durationSeconds, runtime);
        }

        public void PublishRemove(global::AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO.BuffMO buff, int sourceActorId, int targetActorId, BuffRuntime runtime, EffectSourceEndReason reason)
        {
            if (_bus == null) return;
            if (buff == null) return;

            PublishStageEvent(MobaBuffTriggering.Events.Remove, buff.OnRemoveEffects, stage: "remove", buffId: buff.Id, sourceActorId, targetActorId, runtime, reason);
        }

        public void PublishPerEffect(string baseEventId, IReadOnlyList<int> effectIds, string stage, int sourceActorId, int targetActorId, BuffRuntime runtime)
        {
            if (_bus == null) return;
            if (string.IsNullOrEmpty(baseEventId)) return;
            if (effectIds == null || effectIds.Count == 0) return;

            for (int i = 0; i < effectIds.Count; i++)
            {
                var effectId = effectIds[i];
                if (effectId <= 0) continue;

                var args = PooledTriggerArgs.Rent();
                args[EffectTriggering.Args.Source] = sourceActorId;
                args[EffectTriggering.Args.Target] = targetActorId;
                args[EffectSourceKeys.SourceActorId] = sourceActorId;
                args[EffectSourceKeys.TargetActorId] = targetActorId;
                args[MobaBuffTriggering.Args.BuffId] = runtime != null ? runtime.BuffId : 0;
                args[MobaBuffTriggering.Args.EffectId] = effectId;
                args[MobaBuffTriggering.Args.Stage] = stage;
                args[MobaBuffTriggering.Args.StackCount] = runtime != null ? runtime.StackCount : 0;
                if (runtime != null && runtime.SourceContextId != 0)
                {
                    args[EffectSourceKeys.SourceContextId] = runtime.SourceContextId;
                    EffectOriginArgsHelper.FillFromRegistry(args, runtime.SourceContextId, _effectSource);
                }

                var eventId = MobaBuffTriggering.Events.WithEffect(baseEventId, effectId);
                _bus.Publish(new TriggerEvent(eventId, payload: runtime, args: args));

                var eid = AbilityKit.Triggering.Eventing.StableStringId.Get("event:" + eventId);
                var key = new EventKey<BuffEventArgs>(eid);
                var strongArgs = new BuffEventArgs
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
                if (_strongBus != null)
                {
                    _strongBus.Publish(key, in strongArgs);
                    object boxed = strongArgs;
                    _strongBus.Publish(new EventKey<object>(eid), in boxed);
                }
            }
        }

        public void PublishInterval(global::AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO.BuffMO buff, int sourceActorId, int targetActorId, BuffRuntime runtime)
        {
            if (_bus == null) return;
            if (buff == null) return;

            var args0 = PooledTriggerArgs.Rent();
            args0[EffectTriggering.Args.Source] = sourceActorId;
            args0[EffectTriggering.Args.Target] = targetActorId;
            args0[EffectSourceKeys.SourceActorId] = sourceActorId;
            args0[EffectSourceKeys.TargetActorId] = targetActorId;
            args0[MobaBuffTriggering.Args.BuffId] = runtime != null ? runtime.BuffId : 0;
            args0[MobaBuffTriggering.Args.EffectId] = 0;
            args0[MobaBuffTriggering.Args.Stage] = "interval";
            args0[MobaBuffTriggering.Args.StackCount] = runtime != null ? runtime.StackCount : 0;
            if (runtime != null && runtime.SourceContextId != 0)
            {
                args0[EffectSourceKeys.SourceContextId] = runtime.SourceContextId;
                EffectOriginArgsHelper.FillFromRegistry(args0, runtime.SourceContextId, _effectSource);
            }
            _bus.Publish(new TriggerEvent(MobaBuffTriggering.Events.Interval, payload: runtime, args: args0));

            {
                var eventId = MobaBuffTriggering.Events.Interval;
                var eid = AbilityKit.Triggering.Eventing.StableStringId.Get("event:" + eventId);
                var key = new EventKey<BuffEventArgs>(eid);
                var strongArgs = new BuffEventArgs
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
                if (_strongBus != null) _strongBus.Publish(key, in strongArgs);
            }
        }

        private void PublishBaseEvent(string eventId, int buffId, int sourceActorId, int targetActorId, float durationSeconds, BuffRuntime runtime)
        {
            var args = PooledTriggerArgs.Rent();
            args[EffectTriggering.Args.Source] = sourceActorId;
            args[EffectTriggering.Args.Target] = targetActorId;
            args[EffectSourceKeys.SourceActorId] = sourceActorId;
            args[EffectSourceKeys.TargetActorId] = targetActorId;
            args[MobaBuffTriggering.Args.BuffId] = buffId;
            args[MobaBuffTriggering.Args.EffectId] = 0;
            args[MobaBuffTriggering.Args.DurationSeconds] = durationSeconds;
            args[MobaBuffTriggering.Args.StackCount] = runtime != null ? runtime.StackCount : 0;
            if (runtime != null && runtime.SourceContextId != 0)
            {
                args[EffectSourceKeys.SourceContextId] = runtime.SourceContextId;
                EffectOriginArgsHelper.FillFromRegistry(args, runtime.SourceContextId, _effectSource);
            }

            _bus.Publish(new TriggerEvent(eventId, payload: runtime, args: args));

            {
                var eid = AbilityKit.Triggering.Eventing.StableStringId.Get("event:" + eventId);
                var key = new EventKey<BuffEventArgs>(eid);
                var strongArgs = new BuffEventArgs
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
                if (_strongBus != null) _strongBus.Publish(key, in strongArgs);
            }
        }

        private void PublishStageEvent(string baseEventId, IReadOnlyList<int> effectIds, string stage, int buffId, int sourceActorId, int targetActorId, BuffRuntime runtime, EffectSourceEndReason reason)
        {
            if (_bus == null) return;
            if (string.IsNullOrEmpty(baseEventId)) return;

            var args0 = PooledTriggerArgs.Rent();
            args0[EffectTriggering.Args.Source] = sourceActorId;
            args0[EffectTriggering.Args.Target] = targetActorId;
            args0[EffectSourceKeys.SourceActorId] = sourceActorId;
            args0[EffectSourceKeys.TargetActorId] = targetActorId;
            args0[MobaBuffTriggering.Args.BuffId] = buffId;
            args0[MobaBuffTriggering.Args.EffectId] = 0;
            args0[MobaBuffTriggering.Args.Stage] = stage;
            args0[MobaBuffTriggering.Args.StackCount] = runtime != null ? runtime.StackCount : 0;
            args0[MobaBuffTriggering.Args.RemoveReason] = (int)reason;
            if (runtime != null && runtime.SourceContextId != 0)
            {
                args0[EffectSourceKeys.SourceContextId] = runtime.SourceContextId;
                EffectOriginArgsHelper.FillFromRegistry(args0, runtime.SourceContextId, _effectSource);
            }
            _bus.Publish(new TriggerEvent(baseEventId, payload: runtime, args: args0));

            {
                var eventId = baseEventId;
                var eid = AbilityKit.Triggering.Eventing.StableStringId.Get("event:" + eventId);
                var key = new EventKey<BuffEventArgs>(eid);
                var strongArgs = new BuffEventArgs
                {
                    EventId = eventId,
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
                if (_strongBus != null) _strongBus.Publish(key, in strongArgs);
            }

            if (effectIds == null || effectIds.Count == 0) return;

            for (int i = 0; i < effectIds.Count; i++)
            {
                var effectId = effectIds[i];
                if (effectId <= 0) continue;

                var args1 = PooledTriggerArgs.Rent();
                args1[EffectTriggering.Args.Source] = sourceActorId;
                args1[EffectTriggering.Args.Target] = targetActorId;
                args1[EffectSourceKeys.SourceActorId] = sourceActorId;
                args1[EffectSourceKeys.TargetActorId] = targetActorId;
                args1[MobaBuffTriggering.Args.BuffId] = buffId;
                args1[MobaBuffTriggering.Args.EffectId] = effectId;
                args1[MobaBuffTriggering.Args.Stage] = stage;
                args1[MobaBuffTriggering.Args.StackCount] = runtime != null ? runtime.StackCount : 0;
                args1[MobaBuffTriggering.Args.RemoveReason] = (int)reason;
                if (runtime != null && runtime.SourceContextId != 0)
                {
                    args1[EffectSourceKeys.SourceContextId] = runtime.SourceContextId;
                    EffectOriginArgsHelper.FillFromRegistry(args1, runtime.SourceContextId, _effectSource);
                }

                var eventId = MobaBuffTriggering.Events.WithEffect(baseEventId, effectId);
                _bus.Publish(new TriggerEvent(eventId, payload: runtime, args: args1));

                var eid = AbilityKit.Triggering.Eventing.StableStringId.Get("event:" + eventId);
                var key = new EventKey<BuffEventArgs>(eid);
                var strongArgs = new BuffEventArgs
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
                if (_strongBus != null) _strongBus.Publish(key, in strongArgs);
            }
        }
    }
}
