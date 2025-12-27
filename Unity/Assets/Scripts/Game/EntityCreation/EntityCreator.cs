using System;
using AbilityKit.Ability.EC;

namespace AbilityKit.Game
{
    public sealed class EntityCreator : IEntityCreator
    {
        private readonly EntityWorld _world;

        public EntityCreator(EntityWorld world)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
        }

        public Entity Create()
        {
            return _world.Create();
        }

        public Entity Create(string debugName)
        {
#if UNITY_EDITOR
            return _world.Create(debugName);
#else
            return _world.Create();
#endif
        }

        public Entity CreateChild(Entity parent)
        {
            return parent.AddChild();
        }

        public Entity CreateChild(Entity parent, string debugName)
        {
#if UNITY_EDITOR
            return parent.AddChild(debugName);
#else
            return parent.AddChild();
#endif
        }

        public Entity CreateChild(Entity parent, int childId)
        {
            return parent.AddChild(childId);
        }

        public Entity CreateChild(Entity parent, int childId, string debugName)
        {
#if UNITY_EDITOR
            return parent.AddChild(childId, debugName);
#else
            return parent.AddChild(childId);
#endif
        }
    }
}
