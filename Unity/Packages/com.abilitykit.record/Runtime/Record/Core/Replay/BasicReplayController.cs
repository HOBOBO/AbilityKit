using System;
using AbilityKit.Ability.FrameSync;

namespace AbilityKit.Core.Common.Record.Core
{
    public sealed class BasicReplayController : IReplayController
    {
        private readonly IReplayClock _clock;
        private readonly IEventTrackReader _reader;
        private readonly IReplayEventHandler _handler;

        private bool _isPlaying;

        public BasicReplayController(IReplayClock clock, IEventTrackReader reader, IReplayEventHandler handler)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _reader = reader ?? throw new ArgumentNullException(nameof(reader));
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
            _isPlaying = true;
        }

        public bool IsPlaying => _isPlaying;

        public void Play() => _isPlaying = true;

        public void Pause() => _isPlaying = false;

        public void SeekToStart()
        {
            _clock.Reset(new FrameIndex(0));
        }

        public void SeekToFrame(FrameIndex frame)
        {
            _clock.Reset(frame);
        }

        public void Tick(float deltaTime)
        {
            if (!_isPlaying) return;

            while (_clock.TryConsume(deltaTime, out var nextFrame))
            {
                if (_reader.TryGetEvents(nextFrame, out var evts) && evts != null)
                {
                    for (int i = 0; i < evts.Count; i++)
                    {
                        var e = evts[i];
                        _handler.Handle(in e);
                    }
                }

                deltaTime = 0f;
            }
        }
    }
}
