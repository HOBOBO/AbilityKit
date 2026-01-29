using System;
using System.Collections.Generic;
using AbilityKit.Ability.Share.Common.Projectile;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Share.Impl.Moba.Services.Projectile
{
    public sealed class MobaProjectileLinkService : IService
    {
        private readonly Dictionary<int, ProjectileId> _projectileByActorId = new Dictionary<int, ProjectileId>();
        private readonly Dictionary<ProjectileId, int> _actorIdByProjectile = new Dictionary<ProjectileId, int>();

        public void Link(ProjectileId projectileId, int actorId)
        {
            if (actorId <= 0) throw new ArgumentOutOfRangeException(nameof(actorId));
            _projectileByActorId[actorId] = projectileId;
            _actorIdByProjectile[projectileId] = actorId;
        }

        public bool TryGetActorId(ProjectileId projectileId, out int actorId)
        {
            return _actorIdByProjectile.TryGetValue(projectileId, out actorId);
        }

        public bool TryGetProjectileId(int actorId, out ProjectileId projectileId)
        {
            return _projectileByActorId.TryGetValue(actorId, out projectileId);
        }

        public void UnlinkByActorId(int actorId)
        {
            if (actorId <= 0) return;
            if (_projectileByActorId.TryGetValue(actorId, out var pid))
            {
                _projectileByActorId.Remove(actorId);
                _actorIdByProjectile.Remove(pid);
            }
        }

        public void UnlinkByProjectileId(ProjectileId projectileId)
        {
            if (_actorIdByProjectile.TryGetValue(projectileId, out var actorId))
            {
                _actorIdByProjectile.Remove(projectileId);
                _projectileByActorId.Remove(actorId);
            }
        }

        public void Clear()
        {
            _projectileByActorId.Clear();
            _actorIdByProjectile.Clear();
        }

        public void Dispose()
        {
            Clear();
        }
    }
}
