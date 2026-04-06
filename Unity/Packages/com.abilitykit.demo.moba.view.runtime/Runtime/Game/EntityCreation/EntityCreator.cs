using System;
using AbilityKit.World.ECS;
using UnityEngine;

namespace AbilityKit.Game.EntityCreation
{
    public sealed class EntityCreator : IEntityCreator
    {
        private readonly IECWorld _world;

        public EntityCreator(IECWorld world)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
        }

        public IEntity Create()
        {
            return _world.Create();
        }

        public IEntity Create(string debugName)
        {
            return _world.Create(debugName);
        }

        public IEntity CreateChild(IEntity parent)
        {
            return _world.CreateChild(parent);
        }

        public IEntity CreateChild(IEntity parent, string debugName)
        {
            var child = _world.Create(debugName);
            child.SetParent(parent);
            return child;
        }

        public IEntity CreateChild(IEntity parent, int childId)
        {
            return _world.CreateChild(parent, childId);
        }

        public IEntity CreateChild(IEntity parent, int childId, string debugName)
        {
            var child = _world.Create(debugName);
            child.SetParent(parent, childId);
            return child;
        }
    }
}
