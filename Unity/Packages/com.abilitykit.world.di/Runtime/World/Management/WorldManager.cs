using System;
using System.Collections.Generic;
using AbilityKit.Core.Common.Log;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Ability.World.Management
{
    public sealed class WorldManager : IWorldManager
    {
        private readonly IWorldFactory _factory;
        private readonly Dictionary<WorldId, IWorld> _worlds = new Dictionary<WorldId, IWorld>();

        public WorldManager(IWorldFactory factory)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        public IReadOnlyDictionary<WorldId, IWorld> Worlds => _worlds;

        public IWorld Create(WorldCreateOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (string.IsNullOrEmpty(options.Id.Value)) throw new ArgumentException("WorldId is required", nameof(options));
            if (string.IsNullOrEmpty(options.WorldType)) throw new ArgumentException("WorldType is required", nameof(options));
            if (_worlds.ContainsKey(options.Id)) throw new InvalidOperationException($"World already exists: {options.Id}");

            var world = _factory.Create(options);
            world.Initialize();
            _worlds.Add(world.Id, world);
            return world;
        }

        public bool TryGet(WorldId id, out IWorld world)
        {
            return _worlds.TryGetValue(id, out world);
        }

        public bool Destroy(WorldId id)
        {
            if (!_worlds.TryGetValue(id, out var world)) return false;
            _worlds.Remove(id);
            world.Dispose();
            return true;
        }

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

        public void DisposeAll()
        {
            foreach (var kv in _worlds)
            {
                kv.Value.Dispose();
            }
            _worlds.Clear();
        }
    }
}
