#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.World.Svelto;

namespace AbilityKit.Demo.Shooter.Runtime
{
    [WorldService(typeof(ShooterEntityManager), WorldLifetime.Singleton)]
    [WorldService(typeof(IShooterEntityManager), WorldLifetime.Singleton)]
    public sealed class ShooterEntityManager : IShooterEntityManager
    {
        private readonly ISveltoWorldContext _context;
        private readonly ShooterEntityLimitOptions _limits;
        private readonly HashSet<int> _playerIds = new HashSet<int>();
        private readonly HashSet<int> _projectileIds = new HashSet<int>();
        private readonly HashSet<int> _enemyIds = new HashSet<int>();
        private int _structuralChangeDepth;
        private bool _hasPendingStructuralChanges;
        private long _mutationRevision;

        public ShooterEntityManager(ISveltoWorldContext context)
            : this(context, ShooterEntityLimitOptions.Default)
        {
        }

        public ShooterEntityManager(ISveltoWorldContext context, ShooterEntityLimitOptions limits)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _limits = limits;
        }

        public ISveltoWorldContext SveltoContext => _context;

        public int MaxEntityCount => _limits.MaxEntityCount;

        public int PlayerCount => _playerIds.Count;

        public int ProjectileCount => _projectileIds.Count;

        public int EnemyCount => _enemyIds.Count;

        public long MutationRevision => _mutationRevision;

        public long StructuralChangeSubmissionCount { get; private set; }

        public IReadOnlyCollection<int> PlayerIds => _playerIds;

        public IReadOnlyCollection<int> ProjectileIds => _projectileIds;

        public IReadOnlyCollection<int> EnemyIds => _enemyIds;

        public void Clear()
        {
            var removed = false;
            if (_context.EntitiesDB.ExistsAndIsNotEmpty(ShooterSveltoGroups.Players))
            {
                _context.EntityFunctions.RemoveEntitiesFromGroup(ShooterSveltoGroups.Players);
                removed = true;
            }

            if (_context.EntitiesDB.ExistsAndIsNotEmpty(ShooterSveltoGroups.Projectiles))
            {
                _context.EntityFunctions.RemoveEntitiesFromGroup(ShooterSveltoGroups.Projectiles);
                removed = true;
            }

            if (_context.EntitiesDB.ExistsAndIsNotEmpty(ShooterSveltoGroups.GameplayTargets))
            {
                _context.EntityFunctions.RemoveEntitiesFromGroup(ShooterSveltoGroups.GameplayTargets);
                removed = true;
            }

            _playerIds.Clear();
            _projectileIds.Clear();
            _enemyIds.Clear();
            AdvanceMutationRevision();

            if (removed)
            {
                SubmitStructuralChanges();
            }
        }

        public void BeginStructuralChanges()
        {
            _structuralChangeDepth++;
        }

        public void EndStructuralChanges()
        {
            if (_structuralChangeDepth <= 0)
            {
                _structuralChangeDepth = 0;
                SubmitStructuralChanges();
                return;
            }

            _structuralChangeDepth--;
            if (_structuralChangeDepth == 0 && _hasPendingStructuralChanges)
            {
                SubmitStructuralChanges();
            }
        }

        public void SubmitStructuralChanges()
        {
            if (_structuralChangeDepth > 0)
            {
                _hasPendingStructuralChanges = true;
                return;
            }

            _hasPendingStructuralChanges = false;
            _context.SubmitEntities();
            StructuralChangeSubmissionCount++;
        }

        public bool HasPlayer(int playerId)
        {
            return _playerIds.Contains(playerId);
        }

        public bool TryGetPlayer(int playerId, out ShooterSveltoPlayerComponent player)
        {
            player = default;
            if (!_playerIds.Contains(playerId))
            {
                return false;
            }

            if (!_context.EntitiesDB.TryQueryMappedEntities<ShooterSveltoPlayerComponent>(ShooterSveltoGroups.Players, out var mapper))
            {
                return false;
            }

            player = mapper.Entity((uint)playerId);
            return true;
        }

        public void AddPlayer(in ShooterSveltoPlayerComponent player)
        {
            if (player.PlayerId <= 0)
            {
                return;
            }

            if (_playerIds.Contains(player.PlayerId))
            {
                SetPlayer(in player);
                return;
            }

            if (IsEntityBudgetFull())
            {
                return;
            }

            ShooterSveltoEntityLayout.BuildPlayer(_context, in player);
            _playerIds.Add(player.PlayerId);
            AdvanceMutationRevision();
            SubmitStructuralChanges();
        }

        public void SetPlayer(in ShooterSveltoPlayerComponent player)
        {
            if (player.PlayerId <= 0)
            {
                return;
            }

            if (!_playerIds.Contains(player.PlayerId))
            {
                AddPlayer(in player);
                return;
            }

            if (!_context.EntitiesDB.TryQueryMappedEntities<ShooterSveltoPlayerComponent>(ShooterSveltoGroups.Players, out var mapper))
            {
                return;
            }

            mapper.Entity((uint)player.PlayerId) = player;
            AdvanceMutationRevision();
        }

        public void RemovePlayer(int playerId)
        {
            if (!_playerIds.Remove(playerId))
            {
                return;
            }

            _context.EntityFunctions.RemoveEntity<ShooterSveltoPlayerDescriptor>((uint)playerId, ShooterSveltoGroups.Players);
            AdvanceMutationRevision();
            SubmitStructuralChanges();
        }

        public bool HasProjectile(int bulletId)
        {
            return _projectileIds.Contains(bulletId);
        }

        public bool TryGetProjectile(int bulletId, out ShooterSveltoProjectileComponent projectile)
        {
            projectile = default;
            if (!_projectileIds.Contains(bulletId))
            {
                return false;
            }

            if (!_context.EntitiesDB.TryQueryMappedEntities<ShooterSveltoProjectileComponent>(ShooterSveltoGroups.Projectiles, out var mapper))
            {
                return false;
            }

            projectile = mapper.Entity((uint)bulletId);
            return true;
        }

        public void AddProjectile(in ShooterSveltoProjectileComponent projectile)
        {
            if (projectile.BulletId <= 0)
            {
                return;
            }

            if (_projectileIds.Contains(projectile.BulletId))
            {
                SetProjectile(in projectile);
                return;
            }

            if (IsEntityBudgetFull())
            {
                return;
            }

            ShooterSveltoEntityLayout.BuildProjectile(_context, in projectile);
            _projectileIds.Add(projectile.BulletId);
            AdvanceMutationRevision();
            SubmitStructuralChanges();
        }

        public void SetProjectile(in ShooterSveltoProjectileComponent projectile)
        {
            if (projectile.BulletId <= 0)
            {
                return;
            }

            if (!_projectileIds.Contains(projectile.BulletId))
            {
                AddProjectile(in projectile);
                return;
            }

            if (!_context.EntitiesDB.TryQueryMappedEntities<ShooterSveltoProjectileComponent>(ShooterSveltoGroups.Projectiles, out var mapper))
            {
                return;
            }

            mapper.Entity((uint)projectile.BulletId) = projectile;
            AdvanceMutationRevision();
        }

        public void RemoveProjectile(int bulletId)
        {
            if (!_projectileIds.Remove(bulletId))
            {
                return;
            }

            _context.EntityFunctions.RemoveEntity<ShooterSveltoProjectileDescriptor>((uint)bulletId, ShooterSveltoGroups.Projectiles);
            AdvanceMutationRevision();
            SubmitStructuralChanges();
        }

        public bool HasEnemy(int enemyId)
        {
            return _enemyIds.Contains(enemyId);
        }

        public bool TryGetEnemy(int enemyId, out ShooterSveltoTransformComponent transform, out ShooterSveltoHealthComponent health)
        {
            transform = default;
            health = default;
            if (!_enemyIds.Contains(enemyId))
            {
                return false;
            }

            var entityId = (uint)enemyId;
            if (!_context.EntitiesDB.TryQueryMappedEntities<ShooterSveltoTransformComponent>(ShooterSveltoGroups.GameplayTargets, out var transformMapper) ||
                !_context.EntitiesDB.TryQueryMappedEntities<ShooterSveltoHealthComponent>(ShooterSveltoGroups.GameplayTargets, out var healthMapper))
            {
                return false;
            }

            transform = transformMapper.Entity(entityId);
            health = healthMapper.Entity(entityId);
            return true;
        }

        public void AddEnemy(int enemyId, in ShooterSveltoTransformComponent transform, in ShooterSveltoHealthComponent health)
        {
            var navigation = default(ShooterSveltoNavigationComponent);
            AddEnemy(enemyId, in transform, in health, in navigation);
        }

        public void AddEnemy(
            int enemyId,
            in ShooterSveltoTransformComponent transform,
            in ShooterSveltoHealthComponent health,
            in ShooterSveltoNavigationComponent navigation)
        {
            if (enemyId <= 0)
            {
                return;
            }

            if (_enemyIds.Contains(enemyId))
            {
                SetEnemy(enemyId, in transform, in health);
                return;
            }

            if (IsEntityBudgetFull())
            {
                return;
            }

            ShooterSveltoEntityLayout.BuildGameplayTarget(_context, (uint)enemyId, in transform, in health, in navigation);
            _enemyIds.Add(enemyId);
            AdvanceMutationRevision();
            SubmitStructuralChanges();
        }

        public void SetEnemy(int enemyId, in ShooterSveltoTransformComponent transform, in ShooterSveltoHealthComponent health)
        {
            if (enemyId <= 0)
            {
                return;
            }

            if (!_enemyIds.Contains(enemyId))
            {
                AddEnemy(enemyId, in transform, in health);
                return;
            }

            var entityId = (uint)enemyId;
            if (!_context.EntitiesDB.TryQueryMappedEntities<ShooterSveltoTransformComponent>(ShooterSveltoGroups.GameplayTargets, out var transformMapper) ||
                !_context.EntitiesDB.TryQueryMappedEntities<ShooterSveltoHealthComponent>(ShooterSveltoGroups.GameplayTargets, out var healthMapper))
            {
                return;
            }

            transformMapper.Entity(entityId) = transform;
            healthMapper.Entity(entityId) = health;
            AdvanceMutationRevision();
        }

        public void RemoveEnemy(int enemyId)
        {
            if (!_enemyIds.Remove(enemyId))
            {
                return;
            }

            _context.EntityFunctions.RemoveEntity<ShooterSveltoGameplayTargetDescriptor>((uint)enemyId, ShooterSveltoGroups.GameplayTargets);
            AdvanceMutationRevision();
            SubmitStructuralChanges();
        }

        private void AdvanceMutationRevision()
        {
            unchecked
            {
                _mutationRevision++;
            }
        }

        private bool IsEntityBudgetFull()
        {
            return PlayerCount + ProjectileCount + EnemyCount >= MaxEntityCount;
        }
    }
}
