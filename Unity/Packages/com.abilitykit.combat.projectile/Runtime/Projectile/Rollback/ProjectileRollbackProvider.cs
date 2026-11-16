using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using MemoryPack;

namespace AbilityKit.Combat.Projectile
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

        /// <summary>
        /// Reads the target-frame physical projectile identities without touching the live world.
        /// Relation rollback providers use this as snapshot evidence before any provider imports.
        /// </summary>
        public static HashSet<int> ReadProjectileIds(byte[] payload)
        {
            if (payload == null || payload.Length == 0)
                throw new InvalidOperationException("Missing projectile rollback payload.");

            var service = MemoryPackSerializer.Deserialize<ProjectileServiceRollbackPayload>(payload);
            if (service.Version != 1 || service.Projectiles == null || service.Projectiles.Length == 0)
                throw new InvalidOperationException("Unsupported projectile service rollback payload.");

            var world = MemoryPackSerializer.Deserialize<ProjectileWorldSnapshotPayload>(service.Projectiles);
            if (world.Version != 18 || world.Items == null)
                throw new InvalidOperationException("Unsupported projectile world rollback payload.");

            var ids = new HashSet<int>();
            foreach (var item in world.Items)
            {
                if (item.Id <= 0 || !ids.Add(item.Id))
                    throw new InvalidOperationException("Projectile rollback payload contains a duplicate or invalid projectile identity.");
            }
            return ids;
        }
    }
}
