using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host.Modules;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Host.Extensions.Time
{
    public sealed class ServerFrameTimeModule : ILogicWorldServerModule
    {
        private readonly Dictionary<WorldId, FrameTime> _times = new Dictionary<WorldId, FrameTime>();

        private readonly Action<WorldCreateOptions> _onBeforeCreateWorld;
        private readonly Action<WorldId> _onWorldDestroyed;
        private readonly Action<FrameIndex, float> _onPostStep;

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

        public void Install(LogicWorldServerOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            options.BeforeCreateWorld.Add(_onBeforeCreateWorld);
            options.WorldDestroyed.Add(_onWorldDestroyed);
            options.PostStep.Add(_onPostStep);
        }

        public void Uninstall(LogicWorldServerOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            options.BeforeCreateWorld.Remove(_onBeforeCreateWorld);
            options.WorldDestroyed.Remove(_onWorldDestroyed);
            options.PostStep.Remove(_onPostStep);
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
