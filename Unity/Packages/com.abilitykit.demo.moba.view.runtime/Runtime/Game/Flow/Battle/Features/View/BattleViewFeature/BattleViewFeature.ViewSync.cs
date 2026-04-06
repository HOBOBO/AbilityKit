using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.World.ECS;
using EC = AbilityKit.World.ECS;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleViewFeature
    {
        private void RefreshDirtyViews()
        {
            if (_query?.World == null) return;

            var dirty = _ctx != null ? _ctx.DirtyEntities : null;
            if (dirty == null || dirty.Count == 0) return;

            for (int i = 0; i < dirty.Count; i++)
            {
                var id = dirty[i];
                if (!_query.World.IsAlive(id)) continue;

                var entity = _query.World.Wrap(id);
                if (!entity.TryGetRef(out BattleNetIdComponent netIdComp)) continue;
                if (!entity.TryGetRef(out BattleTransformComponent t)) continue;

                _binder?.Sync(entity, _ctx);
                RegisterSeekablesForEntity(id);
            }

            SeekAllToCurrentFrame();

            dirty.Clear();
        }

        private void OnEntityDestroyed(EC.EntityDestroyed evt)
        {
            var id = evt.EntityId;
            _ctx?.EntityLookup?.UnbindByEntityId(id);
            _binder?.OnDestroyed(id);
        }
    }
}
