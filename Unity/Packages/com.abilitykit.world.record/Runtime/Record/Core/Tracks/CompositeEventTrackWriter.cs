using AbilityKit.Ability.FrameSync;

namespace AbilityKit.Core.Common.Record.Core
{
    public sealed class CompositeEventTrackWriter : IEventTrackWriter
    {
        private readonly IEventTrackWriter[] _writers;

        public CompositeEventTrackWriter(params IEventTrackWriter[] writers)
        {
            _writers = writers;
        }

        public void Append(FrameIndex frame, RecordEventType eventType, byte[] payload)
        {
            if (_writers == null) return;
            for (int i = 0; i < _writers.Length; i++)
            {
                _writers[i]?.Append(frame, eventType, payload);
            }
        }
    }
}
