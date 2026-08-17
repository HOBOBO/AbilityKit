using System;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Battle.Entity;
using EC = AbilityKit.World.ECS;

namespace AbilityKit.Game.Flow
{
    internal sealed class ViewDirtyEntityRefreshOperation
    {
        public bool Refresh(
            IBattleRuntimeContext runtimeContext,
            IBattleEntityContext entityContext,
            IBattleEntityQuery query,
            BattleViewBinder binder,
            bool requireViewComponents = false,
            Action<EC.IEntityId> onSynced = null)
        {
            if (query?.World == null) return false;

            var dirty = entityContext?.DirtyEntities;
            if (dirty == null || dirty.Count == 0) return false;

            for (int i = 0; i < dirty.Count; i++)
            {
                var id = dirty[i];
                if (!query.World.IsAlive(id)) continue;

                var entity = query.World.Wrap(id);
                if (requireViewComponents && !CanCreateView(entity)) continue;

                binder?.Sync(entity, runtimeContext);
                onSynced?.Invoke(id);
            }

            dirty.Clear();
            return true;
        }

        private bool CanCreateView(EC.IEntity entity)
        {
            if (!entity.TryGetRef(out BattleNetIdComponent netIdComp) || netIdComp == null) return false;
            if (!entity.TryGetRef(out BattleTransformComponent transform) || transform == null) return false;
            return netIdComp.NetId.Value > 0;
        }
    }
}
