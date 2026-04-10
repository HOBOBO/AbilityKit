using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;

namespace AbilityKit.Core.Common.Record.Core
{
    [Serializable]
    public sealed class EventTrack : IEventTrackWriter, IEventTrackReader
    {
        private readonly Dictionary<int, List<RecordEvent>> _eventsByFrame = new Dictionary<int, List<RecordEvent>>();

        public Dictionary<int, List<RecordEvent>> Export()
        {
            return _eventsByFrame;
        }

        public void Append(FrameIndex frame, RecordEventType eventType, byte[] payload)
        {
            if (!_eventsByFrame.TryGetValue(frame.Value, out var list) || list == null)
            {
                list = new List<RecordEvent>(2);
                _eventsByFrame[frame.Value] = list;
            }

            list.Add(new RecordEvent(frame, eventType, payload));
        }

        public bool TryGetEvents(FrameIndex frame, out IReadOnlyList<RecordEvent> events)
        {
            if (_eventsByFrame.TryGetValue(frame.Value, out var list) && list != null)
            {
                events = list;
                return true;
            }

            events = Array.Empty<RecordEvent>();
            return false;
        }
    }
}
