using AbilityKit.World.ECS;
using EC = AbilityKit.World.ECS;

namespace AbilityKit.Game.Flow
{
    public sealed partial class ConfirmedBattleViewFeature
    {
        private void RefreshDirtyViews()
        {
            if (_query?.World == null) return;

            var dirty = _confirmedCtx != null ? _confirmedCtx.DirtyEntities : null;
            if (dirty == null || dirty.Count == 0) return;

            for (int i = 0; i < dirty.Count; i++)
            {
                var id = dirty[i];
                if (!_query.World.IsAlive(id)) continue;

                var entity = _query.World.Wrap(id);
                if (!entity.TryGetRef(out AbilityKit.Game.Battle.Entity.BattleNetIdComponent netIdComp)) continue;
                if (!entity.TryGetRef(out AbilityKit.Game.Battle.Component.BattleTransformComponent t)) continue;

                _binder?.Sync(entity, _confirmedCtx);
                RegisterSeekablesForEntity(id);
            }

            SeekAllToCurrentFrame();

            dirty.Clear();
        }

        private void OnEntityDestroyed(EC.EntityDestroyed evt)
        {
            var id = evt.EntityId;
            _confirmedCtx?.EntityLookup?.UnbindByEntityId(id);
            _binder?.OnDestroyed(id);
        }
    }
}
