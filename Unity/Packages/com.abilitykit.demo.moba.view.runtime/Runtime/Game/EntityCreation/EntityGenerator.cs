using System;
using System.Collections.Generic;
using EC = AbilityKit.World.ECS;
using UnityEngine;

namespace AbilityKit.Game.EntityCreation
{
    public static class EntityGenerator
    {
        public static EC.IEntity CreateRoot(EC.IECWorld world, string debugName = null)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            var creator = new EntityCreator(world);
            return creator.Create(debugName);
        }

        public static EC.IEntity Create(EC.IECWorld world, string debugName = null)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            var creator = new EntityCreator(world);
            return creator.Create(debugName);
        }

        public static EC.IEntity CreateChild(EC.IEntity parent, string debugName = null)
        {
            var creator = new EntityCreator(parent.World);
            return creator.CreateChild(parent, debugName);
        }

        public static EC.IEntity CreateChild(EC.IEntity parent, int childId, string debugName = null)
        {
            var creator = new EntityCreator(parent.World);
            return creator.CreateChild(parent, childId, debugName);
        }
    }
}
