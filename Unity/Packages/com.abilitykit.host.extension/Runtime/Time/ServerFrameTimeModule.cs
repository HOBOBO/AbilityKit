using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host.Extensions.FrameSync;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Host.Extensions.Time
{
    public sealed class ServerFrameTimeModule : IHostRuntimeModule
    {
        private readonly Dictionary<WorldId, FrameTime> _times = new Dictionary<WorldId, FrameTime>();

        private readonly Action<WorldCreateOptions> _onBeforeCreateWorld;
        private readonly Action<WorldId> _onWorldDestroyed;
        private readonly Action<FrameIndex, float> _onPostStep;

        private IFrameSyncDriverEvents _frameEvents;

        public ServerFrameTimeModule()
        {
            _onBeforeCreateWorld = OnBeforeCreateWorld;
            _onWorldDestroyed = OnWorldDestroyed;
            _onPostStep = OnPostStep;
        }

        public bool TryGet(WorldId worldId, out IFrameTime time)
        {
            if (_times.TryGetValue(worldId, out var t) && t != null)
            {
                time = t;
                return true;
            }

            time = null;
            return false;
        }

        public void Install(HostRuntime runtime, HostRuntimeOptions options)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (options == null) throw new ArgumentNullException(nameof(options));

            if (!runtime.Features.TryGetFeature<IFrameSyncDriverEvents>(out _frameEvents) || _frameEvents == null)
            {
                throw new InvalidOperationException($"{nameof(ServerFrameTimeModule)} requires {nameof(IFrameSyncDriverEvents)} feature. Install {nameof(FrameSyncDriverModule)} first.");
            }

            options.BeforeCreateWorld.Add(_onBeforeCreateWorld);
            options.WorldDestroyed.Add(_onWorldDestroyed);

            _frameEvents.AddPostStep(_onPostStep);
        }

        public void Uninstall(HostRuntime runtime, HostRuntimeOptions options)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (options == null) throw new ArgumentNullException(nameof(options));

            options.BeforeCreateWorld.Remove(_onBeforeCreateWorld);
            options.WorldDestroyed.Remove(_onWorldDestroyed);

            _frameEvents?.RemovePostStep(_onPostStep);
            _frameEvents = null;
        }

        private void OnBeforeCreateWorld(WorldCreateOptions options)
        {
            if (options == null) return;

            if (options.ServiceBuilder == null)
            {
                options.ServiceBuilder = WorldServiceContainerFactory.CreateDefaultOnly();
            }

            if (!_times.TryGetValue(options.Id, out var time) || time == null)
            {
                time = new FrameTime();
                _times[options.Id] = time;
            }

            options.ServiceBuilder.RegisterInstance<IFrameTime>(time);
        }

        private void OnWorldDestroyed(WorldId worldId)
        {
            _times.Remove(worldId);
        }

        private void OnPostStep(FrameIndex frame, float deltaTime)
        {
            foreach (var kv in _times)
            {
                kv.Value?.StepTo(frame, deltaTime);
            }
        }
    }
}
