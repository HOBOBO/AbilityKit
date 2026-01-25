using System;
using System.Collections.Generic;
using EC = AbilityKit.Ability.EC;
using UnityEngine;

namespace AbilityKit.Game.EntityCreation
{
    public static class EntityGenerator
    {
        public static EC.Entity CreateRoot(EC.EntityWorld world, string debugName = null)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            var creator = new EntityCreator(world);
            return creator.Create(debugName);
        }

        public static EC.Entity Create(EC.EntityWorld world, string debugName = null)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            var creator = new EntityCreator(world);
            return creator.Create(debugName);
        }

        public static EC.Entity CreateChild(EC.Entity parent, string debugName = null)
        {
            var creator = new EntityCreator(parent.World);
            return creator.CreateChild(parent, debugName);
        }

        public static EC.Entity CreateChild(EC.Entity parent, int childId, string debugName = null)
        {
            var creator = new EntityCreator(parent.World);
            return creator.CreateChild(parent, childId, debugName);
        }
    }
}
