using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host.Extensions.FrameSync;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Core.Common.Log;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Host.Extensions.Time
{
    public sealed class ServerFrameTimeModule : IHostRuntimeModule
    {
        private readonly Dictionary<WorldId, FrameTime> _times = new Dictionary<WorldId, FrameTime>();

        private readonly float _fixedDeltaSeconds;

        private FrameIndex _frame;

        private readonly Action<WorldCreateOptions> _onBeforeCreateWorld;
        private readonly Action<WorldId> _onWorldDestroyed;
        private readonly Action<FrameIndex, float> _onPostStep;

        private readonly Action<float> _onPostTick;

        private IFrameSyncDriverEvents _frameEvents;

        public ServerFrameTimeModule()
            : this(0f)
        {
        }

        public ServerFrameTimeModule(float fixedDeltaSeconds)
        {
            _fixedDeltaSeconds = fixedDeltaSeconds;
            _frame = new FrameIndex(0);
            _onBeforeCreateWorld = OnBeforeCreateWorld;
            _onWorldDestroyed = OnWorldDestroyed;
            _onPostStep = OnPostStep;

            _onPostTick = OnPostTick;
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

            _frame = new FrameIndex(0);
            if (!runtime.Features.TryGetFeature<IFrameSyncDriverEvents>(out _frameEvents) || _frameEvents == null)
            {
                _frameEvents = null;
            }

            options.BeforeCreateWorld.Add(_onBeforeCreateWorld);
            options.WorldDestroyed.Add(_onWorldDestroyed);

            if (_frameEvents != null)
            {
                _frameEvents.AddPostStep(_onPostStep);
            }
            else
            {
                options.PostTick.Add(_onPostTick);
            }
        }

        public void Uninstall(HostRuntime runtime, HostRuntimeOptions options)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (options == null) throw new ArgumentNullException(nameof(options));

            options.BeforeCreateWorld.Remove(_onBeforeCreateWorld);
            options.WorldDestroyed.Remove(_onWorldDestroyed);

            options.PostTick.Remove(_onPostTick);

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

            if (_fixedDeltaSeconds > 0f)
            {
                time.Reset(new FrameIndex(0), time: 0f, fixedDelta: _fixedDeltaSeconds);
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

        private void OnPostTick(float deltaTime)
        {
            try
            {
                _frame = new FrameIndex(_frame.Value + 1);
                OnPostStep(_frame, deltaTime);
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
            }
        }
    }
}
