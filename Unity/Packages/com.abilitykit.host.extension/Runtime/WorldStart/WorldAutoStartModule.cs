using System;
using System.Collections.Generic;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;

namespace AbilityKit.Ability.Host.Extensions.WorldStart
{
    public sealed class WorldAutoStartModule : IHostRuntimeModule
    {
        private readonly HashSet<WorldId> _completed = new HashSet<WorldId>();

        private HostRuntime _runtime;
        private HostRuntimeOptions _options;

        private readonly Action<WorldId> _onWorldDestroyed;
        private readonly Action<float> _onPostTick;

        public WorldAutoStartModule()
        {
            _onWorldDestroyed = OnWorldDestroyed;
            _onPostTick = OnPostTick;
        }

        public void Install(HostRuntime runtime, HostRuntimeOptions options)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (options == null) throw new ArgumentNullException(nameof(options));

            _runtime = runtime;
            _options = options;

            _completed.Clear();

            options.WorldDestroyed.Add(_onWorldDestroyed);
            options.PostTick.Add(_onPostTick);
        }

        public void Uninstall(HostRuntime runtime, HostRuntimeOptions options)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (options == null) throw new ArgumentNullException(nameof(options));

            options.WorldDestroyed.Remove(_onWorldDestroyed);
            options.PostTick.Remove(_onPostTick);

            _runtime = null;
            _options = null;

            _completed.Clear();
        }

        private void OnWorldDestroyed(WorldId worldId)
        {
            _completed.Remove(worldId);
        }

        private void OnPostTick(float deltaTime)
        {
            if (_runtime == null) return;

            try
            {
                var worlds = _runtime.Worlds?.Worlds;
                if (worlds == null || worlds.Count == 0) return;

                foreach (var kv in worlds)
                {
                    var worldId = kv.Key;
                    if (_completed.Contains(worldId)) continue;

                    var world = kv.Value;
                    if (world == null) continue;

                    var services = world.Services;
                    if (services == null) continue;

                    if (!services.TryResolve<IWorldAutoStartHandler>(out var handler) || handler == null) continue;

                    var ok = false;
                    try
                    {
                        ok = handler.TryAutoStart(world, deltaTime);
                    }
                    catch (Exception ex)
                    {
                        Log.Exception(ex, "[WorldAutoStartModule] IWorldAutoStartHandler.TryAutoStart failed");
                    }

                    if (ok)
                    {
                        _completed.Add(worldId);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "[WorldAutoStartModule] Tick failed");
            }
        }
    }
}
