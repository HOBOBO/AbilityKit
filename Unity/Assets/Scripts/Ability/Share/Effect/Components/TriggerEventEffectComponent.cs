using System;
using System.Collections.Generic;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Share.Effect;

namespace AbilityKit.Ability.Share.Effect.Components
{
    public sealed class TriggerEventEffectComponent : IEffectComponent
    {
        private readonly string _applyEventId;
        private readonly string _tickEventId;
        private readonly string _removeEventId;

        private readonly IReadOnlyDictionary<string, object> _args;

        public TriggerEventEffectComponent(
            string applyEventId = null,
            string tickEventId = null,
            string removeEventId = null,
            IReadOnlyDictionary<string, object> args = null)
        {
            _applyEventId = applyEventId;
            _tickEventId = tickEventId;
            _removeEventId = removeEventId;
            _args = args;
        }

        public void OnApply(in EffectExecutionContext context, EffectInstance instance)
        {
            Publish(context.EventBus, _applyEventId, context.Source, context.Target, instance);
        }

        public void OnTick(in EffectExecutionContext context, EffectInstance instance)
        {
            Publish(context.EventBus, _tickEventId, context.Source, context.Target, instance);
        }

        public void OnRemove(in EffectExecutionContext context, EffectInstance instance)
        {
            Publish(context.EventBus, _removeEventId, context.Source, context.Target, instance);
        }

        private void Publish(IEventBus bus, string eventId, object source, object target, EffectInstance instance)
        {
            if (bus == null) return;
            if (string.IsNullOrEmpty(eventId)) return;

            var args = PooledTriggerArgs.Rent();
            if (_args != null)
            {
                foreach (var kv in _args)
                {
                    if (kv.Key == null) continue;
                    args[kv.Key] = kv.Value;
                }
            }

            args[EffectTriggering.Args.Source] = source;
            args[EffectTriggering.Args.Target] = target;
            args[EffectTriggering.Args.Spec] = instance?.Spec;
            args[EffectTriggering.Args.Instance] = instance;
            args[EffectTriggering.Args.InstanceId] = instance != null ? instance.Id : 0;
            args[EffectTriggering.Args.StackCount] = instance != null ? instance.StackCount : 0;
            args[EffectTriggering.Args.ElapsedSeconds] = instance != null ? instance.ElapsedSeconds : 0f;
            args[EffectTriggering.Args.RemainingSeconds] = instance != null ? instance.RemainingSeconds : 0f;

            bus.Publish(new TriggerEvent(eventId, instance, args));
        }
    }
}
