using System;
using EC = AbilityKit.World.ECS;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleViewWorldRebinder
    {
        private readonly BattleViewEntitySyncController _sync;
        private readonly BattleViewAliveEntityEnumerator _entities;

        public BattleViewWorldRebinder(
            BattleViewEntitySyncController sync,
            BattleViewAliveEntityEnumerator entities = null)
        {
            _sync = sync ?? throw new ArgumentNullException(nameof(sync));
            _entities = entities ?? new BattleViewAliveEntityEnumerator();
        }

        public void RebindAll(EC.IECWorld world)
        {
            _entities.ForEach(world, entity => _sync.Sync(entity));
        }

        public void RebindAll(EC.IECWorld world, IBattleRuntimeContext runtimeContext)
        {
            _entities.ForEach(world, entity => _sync.Sync(entity, runtimeContext));
        }
    }

    internal sealed class BattleViewAliveEntityEnumerator
    {
        public void ForEach(EC.IECWorld world, Action<EC.IEntity> visitor)
        {
            if (world == null || visitor == null) return;
            world.ForEachAlive(visitor);
        }
    }
}
