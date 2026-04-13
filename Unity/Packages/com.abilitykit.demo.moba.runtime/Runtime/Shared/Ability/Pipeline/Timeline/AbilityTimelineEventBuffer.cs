using System.Collections.Generic;
using AbilityKit.Demo.Moba.ActionTimeline;

namespace AbilityKit.Ability.Share.Impl.Pipeline.Timeline
{
    public sealed class AbilityTimelineEventBuffer : IMobaTimelineEventSink
    {
        public readonly List<TriggerLogEvent> TriggerLogs = new List<TriggerLogEvent>();

        public void OnTriggerLog(float time, string message)
        {
            TriggerLogs.Add(new TriggerLogEvent(time, message));
        }

        public readonly struct TriggerLogEvent
        {
            public readonly float Time;
            public readonly string Message;

            public TriggerLogEvent(float time, string message)
            {
                Time = time;
                Message = message;
            }
        }
    }
}
