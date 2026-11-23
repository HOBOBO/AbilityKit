using System;
using System.Collections.Generic;
using AbilityKit.Core.Logging;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Ability.World.Management
{
    /// <summary>
    /// 在宿主线程上管理世界实例的创建、推进与释放，不提供并发访问保证。
    /// </summary>
    public sealed class WorldManager : IWorldManager
    {
        private readonly IWorldFactory _factory;
        private readonly Dictionary<WorldId, IWorld> _worlds = new Dictionary<WorldId, IWorld>();
        private readonly Dictionary<WorldId, IWorld> _pendingCreations = new Dictionary<WorldId, IWorld>();
        private bool _closing;

        /// <summary>
        /// 使用指定工厂创建世界实例。
        /// </summary>
        public WorldManager(IWorldFactory factory)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        /// <summary>
        /// 已完成初始化且尚未摘除的世界；这是实时视图，不包含正在创建的世界。
        /// </summary>
        public IReadOnlyDictionary<WorldId, IWorld> Worlds => _worlds;

        /// <summary>
        /// 接管工厂返回的新世界，在身份校验和初始化成功后才登记；批量关闭期间拒绝创建。
        /// </summary>
        /// <exception cref="AggregateException">创建失败且补偿释放也失败。</exception>
        public IWorld Create(WorldCreateOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (_closing) throw new InvalidOperationException("Cannot create a world during batch disposal.");
            var id = options.Id;
            if (string.IsNullOrEmpty(id.Value)) throw new ArgumentException("WorldId is required", nameof(options));
            if (string.IsNullOrEmpty(options.WorldType)) throw new ArgumentException("WorldType is required", nameof(options));
            if (_worlds.ContainsKey(id) || _pendingCreations.ContainsKey(id))
                throw new InvalidOperationException($"World already exists or is being created: {id}");

            _pendingCreations.Add(id, null);
            try
            {
                var world = _factory.Create(options);
                if (world == null)
                    throw new InvalidOperationException($"World factory returned null: worldId={id}");
                if (ContainsInstance(_worlds.Values, world) || ContainsInstance(_pendingCreations.Values, world))
                    throw new InvalidOperationException($"World factory returned an instance already owned by the manager: worldId={id}");

                _pendingCreations[id] = world;
                try
                {
                    ValidateIdentity(world, id);
                    world.Initialize();
                    ValidateIdentity(world, id);
                    _worlds.Add(id, world);
                    return world;
                }
                catch (Exception creationFailure)
                {
                    try { world.Dispose(); }
                    catch (Exception cleanupFailure)
                    {
                        throw new AggregateException($"World creation and cleanup failed: worldId={id}",
                            creationFailure, cleanupFailure);
                    }
                    throw;
                }
            }
            finally
            {
                _pendingCreations.Remove(id);
            }
        }

        private static void ValidateIdentity(IWorld world, WorldId requestedId)
        {
            var actualId = world.Id;
            if (actualId != requestedId)
                throw new InvalidOperationException($"World factory identity mismatch: requested={requestedId}, actual={actualId}");
        }

        private static bool ContainsInstance(Dictionary<WorldId, IWorld>.ValueCollection worlds, IWorld candidate)
        {
            foreach (var world in worlds)
                if (ReferenceEquals(world, candidate)) return true;
            return false;
        }

        /// <summary>
        /// 查找仍登记在管理器中的世界。
        /// </summary>
        public bool TryGet(WorldId id, out IWorld world)
        {
            return _worlds.TryGetValue(id, out world);
        }

        /// <summary>
        /// 先摘除指定世界再释放；找不到时返回假，释放异常直接传播且不恢复登记。
        /// </summary>
        public bool Destroy(WorldId id)
        {
            if (!_worlds.TryGetValue(id, out var world)) return false;
            _worlds.Remove(id);
            world.Dispose();
            return true;
        }

        /// <summary>
        /// 推进已登记世界并记录单个世界的运行异常；回调期间不得修改世界集合。
        /// </summary>
        public void Tick(float deltaTime)
        {
            foreach (var kv in _worlds)
            {
                try
                {
                    kv.Value.Tick(deltaTime);
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, $"[WorldManager] World.Tick failed: worldId={kv.Key}");
                }
            }
        }

        /// <summary>
        /// 先摘除当前已登记的世界，再逐个释放；重入时直接返回，结束后可再次创建世界。
        /// </summary>
        /// <exception cref="AggregateException">至少一个世界释放失败，异常按世界身份标注。</exception>
        public void DisposeAll()
        {
            if (_closing || _worlds.Count == 0) return;

            var worlds = new List<KeyValuePair<WorldId, IWorld>>(_worlds);
            _closing = true;
            try
            {
                _worlds.Clear();
                List<Exception> failures = null;
                foreach (var entry in worlds)
                {
                    try { entry.Value.Dispose(); }
                    catch (Exception failure)
                    {
                        failures ??= new List<Exception>();
                        failures.Add(new InvalidOperationException($"World disposal failed: worldId={entry.Key}", failure));
                    }
                }

                if (failures != null) throw new AggregateException("One or more worlds failed to dispose.", failures);
            }
            finally
            {
                _closing = false;
            }
        }
    }
}
