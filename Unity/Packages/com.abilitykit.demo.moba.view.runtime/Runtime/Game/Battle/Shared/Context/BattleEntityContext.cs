using System.Collections.Generic;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Game.Battle.Entity;
using EC = AbilityKit.World.ECS;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleEntityContext
    {
        private long _bindingGeneration;

        public WorldId RuntimeWorldId { get; set; }
        public bool HasRuntimeWorldId { get; set; }
        public EC.IEntity EntityNode { get; set; }
        public EC.IECWorld EntityWorld { get; set; }
        public BattleEntityLookup EntityLookup { get; set; }
        public BattleEntityFactory EntityFactory { get; set; }
        public IBattleEntityQuery EntityQuery { get; set; }
        public List<EC.IEntityId> DirtyEntities { get; set; }

        public long Bind(
            EC.IEntity node,
            EC.IECWorld world,
            BattleEntityLookup lookup,
            BattleEntityFactory factory,
            IBattleEntityQuery query,
            List<EC.IEntityId> dirtyEntities = null)
        {
            _bindingGeneration++;
            EntityNode = node;
            EntityWorld = world;
            EntityLookup = lookup;
            EntityFactory = factory;
            EntityQuery = query;
            DirtyEntities = dirtyEntities;
            return _bindingGeneration;
        }

        public bool ClearBinding(long bindingGeneration, bool destroyCollections = false)
        {
            if (bindingGeneration != _bindingGeneration)
            {
                return false;
            }

            ClearEntityRuntime(destroyCollections);
            _bindingGeneration++;
            return true;
        }

        public void Reset(bool destroyCollections)
        {
            RuntimeWorldId = default;
            HasRuntimeWorldId = false;
            ClearEntityRuntime(destroyCollections);
            _bindingGeneration++;
        }

        private void ClearEntityRuntime(bool destroyCollections)
        {
            EntityNode = default;
            EntityWorld = null;
            EntityLookup = null;
            EntityFactory = null;
            EntityQuery = null;

            if (destroyCollections)
            {
                DirtyEntities = null;
            }
            else
            {
                DirtyEntities?.Clear();
            }
        }
    }
}
