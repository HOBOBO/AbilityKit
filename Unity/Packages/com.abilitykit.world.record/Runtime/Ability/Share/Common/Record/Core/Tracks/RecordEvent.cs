using System;
using AbilityKit.Ability.FrameSync;

namespace AbilityKit.Ability.Share.Common.Record.Core
{
    [Serializable]
    public readonly struct RecordEvent
    {
        public readonly FrameIndex Frame;
        public readonly RecordEventType EventType;
        public readonly byte[] Payload;

        public RecordEvent(FrameIndex frame, RecordEventType eventType, byte[] payload)
        {
            Frame = frame;
            EventType = eventType;
            Payload = payload;
        }
    }
}
