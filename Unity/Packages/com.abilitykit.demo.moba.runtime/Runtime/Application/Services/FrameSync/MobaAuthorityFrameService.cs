using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;

namespace AbilityKit.Demo.Moba.Services
{
    [WorldService(typeof(MobaAuthorityFrameService))]
    public sealed class MobaAuthorityFrameService : IService
    {
        [WorldInject] private IFrameTime _time = null;
        [WorldInject(required: false)] private IWorldAuthorityFramesSource _authorityFrames = null;

        private WorldId _worldId;
        private bool _hasWorldId;
        private int _rollbackHistoryFrames = 600;

        public int RollbackHistoryFrames => _rollbackHistoryFrames;

        public void ConfigureRollbackHistoryFrames(int frames)
        {
            if (frames < 0) throw new ArgumentOutOfRangeException(nameof(frames));
            _rollbackHistoryFrames = frames;
        }

        public void BindWorld(WorldId worldId)
        {
            _worldId = worldId;
            _hasWorldId = true;
        }

        public bool TryGetFrames(out FrameIndex confirmed, out FrameIndex predicted)
        {
            predicted = default;
            confirmed = default;

            if (_hasWorldId && _authorityFrames != null)
            {
                if (_authorityFrames.TryGetFrames(_worldId, out confirmed, out predicted))
                {
                    return true;
                }
            }

            if (_time == null) return false;

            predicted = _time.Frame;
            confirmed = predicted;
            return true;
        }

        public FrameIndex PredictedFrame
        {
            get
            {
                TryGetFrames(out _, out var p);
                return p;
            }
        }

        public FrameIndex ConfirmedFrame
        {
            get
            {
                TryGetFrames(out var c, out _);
                return c;
            }
        }

        public void Dispose()
        {
        }
    }
}
