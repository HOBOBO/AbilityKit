using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public sealed class MobaAuthorityFrameService : IService
    {
        private readonly IWorldResolver _services;
        private readonly IFrameTime _time;

        private WorldId _worldId;
        private bool _hasWorldId;

        public MobaAuthorityFrameService(IWorldResolver services, IFrameTime time)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _time = time;
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

            if (_hasWorldId && _services != null && _services.TryResolve<IWorldAuthorityFramesSource>(out var src) && src != null)
            {
                if (src.TryGetFrames(_worldId, out confirmed, out predicted))
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
