using EC = AbilityKit.Ability.EC;

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
                _binder?.Sync(_query.World.Wrap(id));
                RegisterSeekablesForEntity(id);
            }

            SeekAllToCurrentFrame();

            dirty.Clear();
        }

        private void OnEntityDestroyed(EC.EntityId id)
        {
            _ctx?.EntityLookup?.UnbindByEntityId(id);
            _binder?.OnDestroyed(id);
        }
    }
}
