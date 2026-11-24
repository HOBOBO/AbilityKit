#nullable enable
using System;
using System.Collections.Generic;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Core.Logging;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;

namespace AbilityKit.Ability.Host.Extensions.WorldStart
{
    /// <summary>
    /// 在宿主帧尾尝试启动已登记世界；完成记录只适用于当前登记的世界实例。
    /// </summary>
    public sealed class WorldAutoStartModule : IHostRuntimeModule
    {
        private readonly Dictionary<WorldId, IWorld> _completed = new Dictionary<WorldId, IWorld>();
        private readonly List<WorldId> _detached = new List<WorldId>();

        private HostRuntime? _runtime;

        private readonly Action<WorldId> _onWorldDestroyed;
        private readonly Action<float> _onPostTick;

        /// <summary>
        /// 创建自动启动模块，启动条件由各世界注册的处理器决定。
        /// </summary>
        public WorldAutoStartModule()
        {
            _onWorldDestroyed = OnWorldDestroyed;
            _onPostTick = OnPostTick;
        }

        /// <summary>
        /// 注册宿主帧尾与世界销毁回调，并清空之前的完成记录。
        /// </summary>
        public void Install(HostRuntime runtime, HostRuntimeOptions options)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (options == null) throw new ArgumentNullException(nameof(options));

            _runtime = runtime;

            _completed.Clear();

            options.WorldDestroyed.Add(_onWorldDestroyed);
            options.PostTick.Add(_onPostTick);
        }

        /// <summary>
        /// 取消宿主回调并清空完成记录，不对已经启动的世界执行额外操作。
        /// </summary>
        public void Uninstall(HostRuntime runtime, HostRuntimeOptions options)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (options == null) throw new ArgumentNullException(nameof(options));

            options.WorldDestroyed.Remove(_onWorldDestroyed);
            options.PostTick.Remove(_onPostTick);

            _runtime = null;

            _completed.Clear();
        }

        private void OnWorldDestroyed(WorldId worldId)
        {
            if (_runtime != null && _completed.TryGetValue(worldId, out var world) && !IsRegistered(_runtime, world))
            {
                _completed.Remove(worldId);
            }
        }

        private static bool IsRegistered(HostRuntime runtime, IWorld world)
        {
            return runtime.TryGetWorld(world.Id, out var current) && ReferenceEquals(current, world);
        }

        private void PruneDetachedWorlds(HostRuntime runtime)
        {
            foreach (var entry in _completed)
            {
                if (!IsRegistered(runtime, entry.Value)) _detached.Add(entry.Key);
            }

            foreach (var worldId in _detached) _completed.Remove(worldId);
            _detached.Clear();
        }

        private void OnPostTick(float deltaTime)
        {
            var runtime = _runtime;
            if (runtime == null) return;

            try
            {
                PruneDetachedWorlds(runtime);
                var worlds = runtime.Worlds?.Worlds;
                if (worlds == null || worlds.Count == 0) return;

                foreach (var kv in worlds)
                {
                    var worldId = kv.Key;
                    if (_completed.ContainsKey(worldId)) continue;

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

                    if (ok && ReferenceEquals(_runtime, runtime) && IsRegistered(runtime, world))
                    {
                        _completed[worldId] = world;
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
