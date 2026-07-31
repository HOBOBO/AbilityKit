using System;
using System.Collections.Generic;
using AbilityKit.Protocol.Shooter;
using Svelto.ECS;

namespace AbilityKit.Demo.Shooter.Runtime
{
    public sealed class ShooterPackedSnapshotImporter
    {
        private readonly ShooterBattleState _state;
        private readonly IShooterEntityManager _entities;

        // 导入聚合容器（成员复用，避免每次 Import 新建 —— 高单位量下每次 ~30-80KB 临时分配）。
        // 单线程战斗世界内使用；预测回滚的连续多次 Import 也共享。
        private readonly Dictionary<int, ShooterSveltoPlayerComponent> _players = new();
        private readonly Dictionary<int, ShooterSveltoProjectileComponent> _projectiles = new();
        private readonly Dictionary<int, ImportedEnemy> _enemies = new();
        private readonly HashSet<int> _removedProjectiles = new();
        private readonly HashSet<int> _removedEnemies = new();

        public ShooterPackedSnapshotImporter(ShooterBattleState state, IShooterEntityManager entities)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _entities = entities ?? throw new ArgumentNullException(nameof(entities));
        }

        public bool Import(in ShooterPackedSnapshotPayload snapshot)
        {
            if (snapshot.Version <= 0)
            {
                return false;
            }

            var isDelta = (snapshot.SnapshotFlags & ShooterPackedSnapshotFlags.Delta) != 0;
            if (!isDelta)
            {
                _state.Reset(default);
                ClearImportedEntities();
            }

            _state.CurrentFrame = snapshot.Frame;

            var componentChunks = snapshot.ComponentChunks;
            if (componentChunks == null || componentChunks.Length == 0)
            {
                return snapshot.EntityCount == 0;
            }

            ImportComponentChunks(componentChunks, isDelta);

            return _entities.PlayerCount > 0 || snapshot.EntityCount == 0;
        }

        private void ImportComponentChunks(ShooterPackedComponentChunk[] componentChunks, bool isDelta = false)
        {
            var players = _players;
            var projectiles = _projectiles;
            var enemies = _enemies;
            var removedProjectiles = _removedProjectiles;
            var removedEnemies = _removedEnemies;
            players.Clear();
            projectiles.Clear();
            enemies.Clear();
            removedProjectiles.Clear();
            removedEnemies.Clear();

            for (int i = 0; i < componentChunks.Length; i++)
            {
                var chunk = componentChunks[i];
                switch (chunk.ComponentKind)
                {
                    case ShooterPackedComponentKinds.RuntimeMetadata:
                        ImportRuntimeMetadataChunk(in chunk);
                        break;
                    case ShooterPackedComponentKinds.EntityLifecycle:
                        ImportLifecycleComponentChunk(in chunk, players, projectiles, enemies, removedProjectiles, removedEnemies);
                        break;
                    case ShooterPackedComponentKinds.Transform:
                        ImportTransformComponentChunk(in chunk, players, projectiles, enemies);
                        break;
                    case ShooterPackedComponentKinds.Health:
                        ImportHealthComponentChunk(in chunk, players, enemies);
                        break;
                    case ShooterPackedComponentKinds.Score:
                        ImportScoreComponentChunk(in chunk, players);
                        break;
                    case ShooterPackedComponentKinds.ProjectileLifetime:
                        ImportProjectileLifetimeComponentChunk(in chunk, projectiles);
                        break;
                }
            }

            foreach (var projectileId in removedProjectiles)
            {
                projectiles.Remove(projectileId);
                _entities.RemoveProjectile(projectileId);
            }

            foreach (var enemyId in removedEnemies)
            {
                enemies.Remove(enemyId);
                _entities.RemoveEnemy(enemyId);
            }

            foreach (var player in players.Values)
            {
                var value = player;
                if (isDelta && _entities.HasPlayer(value.PlayerId))
                {
                    _entities.SetPlayer(in value);
                }
                else
                {
                    _entities.AddPlayer(in value);
                }
            }

            foreach (var projectile in projectiles.Values)
            {
                var value = projectile;
                if (isDelta && _entities.HasProjectile(value.BulletId))
                {
                    _entities.SetProjectile(in value);
                }
                else
                {
                    _entities.AddProjectile(in value);
                }

                _state.AdvanceBulletIdPast(value.BulletId);
            }

            foreach (var enemy in enemies.Values)
            {
                UpsertEnemy(in enemy);
            }
        }

        private void ImportRuntimeMetadataChunk(in ShooterPackedComponentChunk chunk)
        {
            var values = chunk.IntValues;
            if (values == null || values.Length < 5)
            {
                return;
            }

            _state.RestoreSnapshotMetadata(
                (ShooterBattleMatchState)values[0],
                values[1],
                values[2],
                values[3],
                values[4]);
        }

        private static void ImportLifecycleComponentChunk(
            in ShooterPackedComponentChunk chunk,
            Dictionary<int, ShooterSveltoPlayerComponent> players,
            Dictionary<int, ShooterSveltoProjectileComponent> projectiles,
            Dictionary<int, ImportedEnemy> enemies,
            HashSet<int> removedProjectiles,
            HashSet<int> removedEnemies)
        {
            var count = Math.Max(0, chunk.Count);
            for (int i = 0; i < count; i++)
            {
                var entityId = ShooterPackedSnapshotChunkCodec.GetInt(chunk.EntityIds, i);
                if (entityId <= 0) continue;

                var flags = ShooterPackedSnapshotChunkCodec.GetByte(chunk.Flags, i);
                if (chunk.EntityKind == ShooterPackedEntityKinds.Player)
                {
                    players[entityId] = new ShooterSveltoPlayerComponent
                    {
                        PlayerId = entityId,
                        AimX = 1f,
                        Hp = ShooterGameplay.DefaultPlayerHp,
                        Alive = (flags & ShooterPackedEntityFlags.Alive) != 0
                    };
                }
                else if (chunk.EntityKind == ShooterPackedEntityKinds.Projectile)
                {
                    if ((flags & ShooterPackedEntityFlags.Despawned) != 0)
                    {
                        removedProjectiles.Add(entityId);
                        projectiles.Remove(entityId);
                        continue;
                    }

                    projectiles[entityId] = new ShooterSveltoProjectileComponent
                    {
                        BulletId = entityId,
                        OwnerPlayerId = ShooterPackedSnapshotChunkCodec.GetInt(chunk.OwnerIds, i)
                    };
                }
                else if (chunk.EntityKind == ShooterPackedEntityKinds.Enemy)
                {
                    if ((flags & ShooterPackedEntityFlags.Despawned) != 0)
                    {
                        removedEnemies.Add(entityId);
                        enemies.Remove(entityId);
                        continue;
                    }

                    enemies[entityId] = new ImportedEnemy
                    {
                        EntityId = entityId,
                        Health = new ShooterSveltoHealthComponent
                        {
                            Current = 1,
                            Max = 1,
                            Alive = (byte)((flags & ShooterPackedEntityFlags.Alive) != 0 ? 1 : 0)
                        }
                    };
                }
            }
        }

        private static void ImportTransformComponentChunk(
            in ShooterPackedComponentChunk chunk,
            Dictionary<int, ShooterSveltoPlayerComponent> players,
            Dictionary<int, ShooterSveltoProjectileComponent> projectiles,
            Dictionary<int, ImportedEnemy> enemies)
        {
            var count = Math.Max(0, chunk.Count);
            for (int i = 0; i < count; i++)
            {
                var entityId = ShooterPackedSnapshotChunkCodec.GetInt(chunk.EntityIds, i);
                if (entityId <= 0) continue;

                if (chunk.EntityKind == ShooterPackedEntityKinds.Player && players.TryGetValue(entityId, out var player))
                {
                    player.X = ShooterPackedSnapshotChunkCodec.GetFloat(chunk.ValueX, i);
                    player.Y = ShooterPackedSnapshotChunkCodec.GetFloat(chunk.ValueY, i);
                    player.AimX = ShooterPackedSnapshotChunkCodec.GetFloat(chunk.ValueZ, i, 1f);
                    player.AimY = ShooterPackedSnapshotChunkCodec.GetFloat(chunk.ValueW, i);
                    players[entityId] = player;
                }
                else if (chunk.EntityKind == ShooterPackedEntityKinds.Projectile && projectiles.TryGetValue(entityId, out var projectile))
                {
                    projectile.X = ShooterPackedSnapshotChunkCodec.GetFloat(chunk.ValueX, i);
                    projectile.Y = ShooterPackedSnapshotChunkCodec.GetFloat(chunk.ValueY, i);
                    projectile.VelocityX = ShooterPackedSnapshotChunkCodec.GetPackedPairValue(chunk.Aux, i, 0);
                    projectile.VelocityY = ShooterPackedSnapshotChunkCodec.GetPackedPairValue(chunk.Aux, i, 1);
                    projectiles[entityId] = projectile;
                }
                else if (chunk.EntityKind == ShooterPackedEntityKinds.Enemy && enemies.TryGetValue(entityId, out var enemy))
                {
                    enemy.Transform = new ShooterSveltoTransformComponent
                    {
                        X = ShooterPackedSnapshotChunkCodec.GetFloat(chunk.ValueX, i),
                        Y = ShooterPackedSnapshotChunkCodec.GetFloat(chunk.ValueY, i),
                        DirectionX = ShooterPackedSnapshotChunkCodec.GetFloat(chunk.ValueZ, i, 1f),
                        DirectionY = ShooterPackedSnapshotChunkCodec.GetFloat(chunk.ValueW, i)
                    };
                    enemy.Navigation.VelocityX = ShooterPackedSnapshotChunkCodec.GetPackedPairValue(chunk.Aux, i, 0);
                    enemy.Navigation.VelocityY = ShooterPackedSnapshotChunkCodec.GetPackedPairValue(chunk.Aux, i, 1);
                    enemies[entityId] = enemy;
                }
            }
        }

        private static void ImportHealthComponentChunk(
            in ShooterPackedComponentChunk chunk,
            Dictionary<int, ShooterSveltoPlayerComponent> players,
            Dictionary<int, ImportedEnemy> enemies)
        {
            var count = Math.Max(0, chunk.Count);
            for (int i = 0; i < count; i++)
            {
                var entityId = ShooterPackedSnapshotChunkCodec.GetInt(chunk.EntityIds, i);
                if (entityId <= 0) continue;

                if (chunk.EntityKind == ShooterPackedEntityKinds.Player && players.TryGetValue(entityId, out var player))
                {
                    player.Hp = ShooterPackedSnapshotChunkCodec.GetInt(chunk.IntValues, i, player.Hp);
                    players[entityId] = player;
                }
                else if (chunk.EntityKind == ShooterPackedEntityKinds.Enemy && enemies.TryGetValue(entityId, out var enemy))
                {
                    var hp = ShooterPackedSnapshotChunkCodec.GetInt(chunk.IntValues, i, enemy.Health.Current);
                    var maxHp = ShooterPackedSnapshotChunkCodec.GetInt(chunk.Aux, i, Math.Max(enemy.Health.Max, hp));
                    enemy.Health.Current = hp;
                    enemy.Health.Max = Math.Max(maxHp, hp);
                    enemy.Health.Alive = (byte)(hp > 0 ? 1 : 0);
                    enemies[entityId] = enemy;
                }
            }
        }

        private static void ImportScoreComponentChunk(in ShooterPackedComponentChunk chunk, Dictionary<int, ShooterSveltoPlayerComponent> players)
        {
            if (chunk.EntityKind != ShooterPackedEntityKinds.Player)
            {
                return;
            }

            var count = Math.Max(0, chunk.Count);
            for (int i = 0; i < count; i++)
            {
                var entityId = ShooterPackedSnapshotChunkCodec.GetInt(chunk.EntityIds, i);
                if (entityId <= 0 || !players.TryGetValue(entityId, out var player)) continue;

                player.Score = ShooterPackedSnapshotChunkCodec.GetInt(chunk.IntValues, i, player.Score);
                players[entityId] = player;
            }
        }

        private static void ImportProjectileLifetimeComponentChunk(in ShooterPackedComponentChunk chunk, Dictionary<int, ShooterSveltoProjectileComponent> projectiles)
        {
            if (chunk.EntityKind != ShooterPackedEntityKinds.Projectile)
            {
                return;
            }

            var count = Math.Max(0, chunk.Count);
            for (int i = 0; i < count; i++)
            {
                var entityId = ShooterPackedSnapshotChunkCodec.GetInt(chunk.EntityIds, i);
                if (entityId <= 0 || !projectiles.TryGetValue(entityId, out var projectile)) continue;

                projectile.RemainingFrames = ShooterPackedSnapshotChunkCodec.GetInt(chunk.IntValues, i, projectile.RemainingFrames);
                projectile.PenetrationRemaining = ShooterPackedSnapshotChunkCodec.GetInt(chunk.Aux, i, projectile.PenetrationRemaining);
                projectile.ExplosionRadius = ShooterPackedSnapshotChunkCodec.GetFloat(chunk.ValueX, i, projectile.ExplosionRadius);
                projectile.ExplosionDamage = Math.Max(0, (int)MathF.Round(ShooterPackedSnapshotChunkCodec.GetFloat(chunk.ValueY, i, projectile.ExplosionDamage)));
                projectiles[entityId] = projectile;
            }
        }

        private void ClearImportedEntities()
        {
            _entities.Clear();
        }

        private void UpsertEnemy(in ImportedEnemy enemy)
        {
            if (enemy.EntityId <= 0)
            {
                return;
            }

            if (_entities.HasEnemy(enemy.EntityId))
            {
                _entities.SetEnemy(enemy.EntityId, in enemy.Transform, in enemy.Health);
            }
            else
            {
                _entities.AddEnemy(enemy.EntityId, in enemy.Transform, in enemy.Health);
            }

            if (_entities.SveltoContext.EntitiesDB.TryQueryMappedEntities<ShooterSveltoNavigationComponent>(
                    ShooterSveltoGroups.GameplayTargets,
                    out var navigationMapper))
            {
                navigationMapper.Entity((uint)enemy.EntityId) = enemy.Navigation;
            }
        }

        private struct ImportedEnemy
        {
            public int EntityId;
            public ShooterSveltoTransformComponent Transform;
            public ShooterSveltoHealthComponent Health;
            public ShooterSveltoNavigationComponent Navigation;
        }
    }
}
