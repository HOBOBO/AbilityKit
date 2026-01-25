using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;

namespace AbilityKit.Ability.Share.Common.Projectile
{
    public sealed class ProjectileRollbackProvider : IRollbackStateProvider
    {
        public const int DefaultKey = 12001;

        private readonly IProjectileService _projectiles;

        public ProjectileRollbackProvider(IProjectileService projectiles)
        {
            _projectiles = projectiles ?? throw new ArgumentNullException(nameof(projectiles));
        }

        public int Key => DefaultKey;

        public byte[] Export(FrameIndex frame)
        {
            return _projectiles.ExportRollback(frame);
        }

        public void Import(FrameIndex frame, byte[] payload)
        {
            _projectiles.ImportRollback(frame, payload);
        }
    }
}
