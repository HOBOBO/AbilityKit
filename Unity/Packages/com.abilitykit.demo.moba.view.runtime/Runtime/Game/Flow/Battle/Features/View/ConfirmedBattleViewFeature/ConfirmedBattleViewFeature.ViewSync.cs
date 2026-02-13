using EC = AbilityKit.Ability.EC;

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
                _binder?.Sync(_query.World.Wrap(id), _confirmedCtx);
                RegisterSeekablesForEntity(id);
            }

            SeekAllToCurrentFrame();

            dirty.Clear();
        }

        private void OnEntityDestroyed(EC.EntityId id)
        {
            _confirmedCtx?.EntityLookup?.UnbindByEntityId(id);
            _binder?.OnDestroyed(id);
        }
    }
}
