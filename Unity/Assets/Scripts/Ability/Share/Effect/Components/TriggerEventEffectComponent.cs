using System;
using System.Collections.Generic;
using AbilityKit.Ability.Triggering;

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
            Publish(context.EventBus, _applyEventId, context.Source, context.Target);
        }

        public void OnTick(in EffectExecutionContext context, EffectInstance instance)
        {
            Publish(context.EventBus, _tickEventId, context.Source, context.Target);
        }

        public void OnRemove(in EffectExecutionContext context, EffectInstance instance)
        {
            Publish(context.EventBus, _removeEventId, context.Source, context.Target);
        }

        private void Publish(IEventBus bus, string eventId, object source, object target)
        {
            if (bus == null) return;
            if (string.IsNullOrEmpty(eventId)) return;

            IReadOnlyDictionary<string, object> args = _args;
            if (args == null)
            {
                args = new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["source"] = source,
                    ["target"] = target,
                };
            }

            bus.Publish(new TriggerEvent(eventId, null, args));
        }
    }
}
