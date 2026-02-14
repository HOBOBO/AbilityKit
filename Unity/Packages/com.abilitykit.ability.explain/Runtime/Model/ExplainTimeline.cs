using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Explain
{
    [Serializable]
    public sealed class ExplainTimeline
    {
        public List<ExplainTimelineEvent> Events = new List<ExplainTimelineEvent>();
    }

    [Serializable]
    public sealed class ExplainTimelineEvent
    {
        public string EventId;

        public string OwnerNodeId;

        public float TimeSeconds;

        public string Title;

        public ExplainSeverity Severity;

        public ExplainSourceRef Source;

        public NavigationTarget NavigateTo;

        public static ExplainTimelineEvent Create(string ownerNodeId, float timeSeconds, string title)
        {
            return new ExplainTimelineEvent
            {
                EventId = Guid.NewGuid().ToString("N"),
                OwnerNodeId = ownerNodeId,
                TimeSeconds = timeSeconds,
                Title = title,
                Severity = ExplainSeverity.None
            };
        }
    }
}
