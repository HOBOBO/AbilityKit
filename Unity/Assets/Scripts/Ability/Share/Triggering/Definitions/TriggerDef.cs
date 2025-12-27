using System;
using System.Collections.Generic;

namespace AbilityKit.Triggering.Definitions
{
    [Serializable]
    public sealed class TriggerDef
    {
        public TriggerDef(string eventId, IReadOnlyList<ConditionDef> conditions, IReadOnlyList<ActionDef> actions)
        {
            EventId = eventId ?? throw new ArgumentNullException(nameof(eventId));
            Conditions = conditions ?? throw new ArgumentNullException(nameof(conditions));
            Actions = actions ?? throw new ArgumentNullException(nameof(actions));
        }

        public string EventId { get; }
        public IReadOnlyList<ConditionDef> Conditions { get; }
        public IReadOnlyList<ActionDef> Actions { get; }
    }
}
