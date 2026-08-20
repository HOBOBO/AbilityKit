#nullable enable

using System;
using AbilityKit.World.Svelto;
using Svelto.ECS;

namespace AbilityKit.Demo.Shooter.Runtime
{
    internal enum ShooterEnemyWavePhase
    {
        Spawn = 0,
        Attack = 1
    }

    [AllowMultiple]
    internal sealed class ShooterEnemyWaveBattleSystem : IShooterBattleSystem
    {
        private readonly ShooterBattleState _state;
        private readonly IShooterEntityManager _entities;
        private readonly ISveltoWorldContext _context;
        private readonly ShooterEnemyWaveOptions _options;
        private readonly ShooterEnemyWaveProgress _progress;
        private readonly ShooterEnemyIdAllocator _idAllocator;
        private readonly ShooterEnemyWaveSpawnDirector _spawnDirector;
        private readonly ShooterEnemyWaveCombatModule _combat;
        private readonly ShooterEnemyWavePhase _phase;
        private readonly ShooterArenaGameplayOptions _arenaOptions;
        private long _lastSynchronizedImportRevision = -1;

        public ShooterEnemyWaveBattleSystem(IShooterBattleServiceResolver services)
            : this(services, ShooterEnemyWavePhase.Spawn)
        {
        }

        public ShooterEnemyWaveBattleSystem(IShooterBattleServiceResolver services, ShooterEnemyWavePhase phase)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));

            _state = services.Resolve<ShooterBattleState>();
            _entities = services.Resolve<IShooterEntityManager>();
            _context = services.Resolve<ISveltoWorldContext>();
            _options = services.TryResolve<ShooterEnemyWaveOptions>(out var options) && options != null
                ? options
                : ShooterEnemyWaveOptions.Disabled;
            _arenaOptions = services.TryResolve<ShooterArenaGameplayOptions>(out var arenaOptions) && arenaOptions != null
                ? arenaOptions
                : ShooterArenaGameplayOptions.Disabled;
            _progress = new ShooterEnemyWaveProgress(_options.Waves);
            _idAllocator = new ShooterEnemyIdAllocator();
            _spawnDirector = new ShooterEnemyWaveSpawnDirector(_options, _progress, _idAllocator, _entities, _arenaOptions);
            _combat = new ShooterEnemyWaveCombatModule(_state, _entities, _options);
            _phase = phase;
        }

        public int Order => _phase == ShooterEnemyWavePhase.Spawn ? ShooterBattleSystemOrder.EnemyWaveSpawn : ShooterBattleSystemOrder.EnemyWaveAttack;

        public string name => _phase == ShooterEnemyWavePhase.Spawn
            ? nameof(ShooterEnemyWaveBattleSystem) + ".Spawn"
            : nameof(ShooterEnemyWaveBattleSystem) + ".Attack";

        public void Step(in float deltaTime)
        {
            if (!_options.Enabled)
            {
                return;
            }

            if (_phase == ShooterEnemyWavePhase.Spawn)
            {
                TickSpawnPhase(deltaTime);
            }
            else
            {
                _combat.Tick();
            }
        }

        private void TickSpawnPhase(float deltaTime)
        {
            if (_state.CurrentFrame <= 1)
            {
                ResetWaveState();
            }
            else
            {
                SynchronizeImportedWaveState();
            }

            _spawnDirector.Tick(_state, deltaTime);
        }

        private void ResetWaveState()
        {
            var enemyIds = new int[_entities.EnemyIds.Count];
            var enemyIdIndex = 0;
            foreach (var enemyId in _entities.EnemyIds)
            {
                enemyIds[enemyIdIndex++] = enemyId;
            }

            _entities.BeginStructuralChanges();
            try
            {
                for (var i = 0; i < enemyIds.Length; i++)
                {
                    _entities.RemoveEnemy(enemyIds[i]);
                }
            }
            finally
            {
                _entities.EndStructuralChanges();
            }

            _spawnDirector.Reset();
            _lastSynchronizedImportRevision = _state.SnapshotImportRevision;
        }

        private void SynchronizeImportedWaveState()
        {
            var importRevision = _state.SnapshotImportRevision;
            if (_lastSynchronizedImportRevision == importRevision)
            {
                return;
            }

            var importedSpawnCount = SynchronizeNextEnemyIdFromExistingTargets();
            var resolvedSpawnCount = Math.Max(importedSpawnCount, _state.DefeatedEnemies + _entities.EnemyCount);
            if (resolvedSpawnCount > 0)
            {
                _idAllocator.AdvancePast(ShooterEnemyIdAllocator.FirstEnemyEntityId + resolvedSpawnCount - 1);
            }

            _spawnDirector.SynchronizeFromImportedTargets(resolvedSpawnCount);
            _lastSynchronizedImportRevision = importRevision;
        }

        private int SynchronizeNextEnemyIdFromExistingTargets()
        {
            var (_, ids, count) = _context.EntitiesDB.QueryEntities<ShooterSveltoHealthComponent>(ShooterSveltoGroups.GameplayTargets);
            var importedSpawnCount = 0;
            for (var i = 0; i < count; i++)
            {
                var entityId = checked((int)ids[i]);
                _idAllocator.AdvancePast(entityId);
                if (entityId >= ShooterEnemyIdAllocator.FirstEnemyEntityId)
                {
                    importedSpawnCount = Math.Max(importedSpawnCount, entityId - ShooterEnemyIdAllocator.FirstEnemyEntityId + 1);
                }
            }

            return importedSpawnCount;
        }
    }
}
