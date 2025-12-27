using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Triggering.Runtime
{
    public sealed class TriggerInstance
    {
        public TriggerInstance(string eventId, IReadOnlyList<ITriggerCondition> conditions, IReadOnlyList<ITriggerAction> actions)
        {
            EventId = eventId ?? throw new ArgumentNullException(nameof(eventId));
            Conditions = conditions ?? throw new ArgumentNullException(nameof(conditions));
            Actions = actions ?? throw new ArgumentNullException(nameof(actions));
        }

        public string EventId { get; }
        public IReadOnlyList<ITriggerCondition> Conditions { get; }
        public IReadOnlyList<ITriggerAction> Actions { get; }
    }
}
