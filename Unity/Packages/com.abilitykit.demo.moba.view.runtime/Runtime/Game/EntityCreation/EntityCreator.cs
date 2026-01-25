using System;
using EC = AbilityKit.Ability.EC;
using UnityEngine;

namespace AbilityKit.Game.EntityCreation
{
    public sealed class EntityCreator : IEntityCreator
    {
        private readonly EC.EntityWorld _world;

        public EntityCreator(EC.EntityWorld world)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
        }

        public EC.Entity Create()
        {
            return _world.Create();
        }

        public EC.Entity Create(string debugName)
        {
#if UNITY_EDITOR
            return _world.Create(debugName);
#else
            return _world.Create();
#endif
        }

        public EC.Entity CreateChild(EC.Entity parent)
        {
            return parent.AddChild();
        }

        public EC.Entity CreateChild(EC.Entity parent, string debugName)
        {
#if UNITY_EDITOR
            return parent.AddChild(debugName);
#else
            return parent.AddChild();
#endif
        }

        public EC.Entity CreateChild(EC.Entity parent, int childId)
        {
            return parent.AddChild(childId);
        }

        public EC.Entity CreateChild(EC.Entity parent, int childId, string debugName)
        {
#if UNITY_EDITOR
            return parent.AddChild(childId, debugName);
#else
            return parent.AddChild(childId);
#endif
        }
    }
}
