using System;
using AbilityKit.Ability.EC;

namespace AbilityKit.Game
{
    public static class EntityGenerator
    {
        public static Entity CreateRoot(EntityWorld world, string debugName = null)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            var creator = new EntityCreator(world);
            return creator.Create(debugName);
        }

        public static Entity Create(EntityWorld world, string debugName = null)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            var creator = new EntityCreator(world);
            return creator.Create(debugName);
        }

        public static Entity CreateChild(Entity parent, string debugName = null)
        {
            var creator = new EntityCreator(parent.World);
            return creator.CreateChild(parent, debugName);
        }

        public static Entity CreateChild(Entity parent, int childId, string debugName = null)
        {
            var creator = new EntityCreator(parent.World);
            return creator.CreateChild(parent, childId, debugName);
        }
    }
}
