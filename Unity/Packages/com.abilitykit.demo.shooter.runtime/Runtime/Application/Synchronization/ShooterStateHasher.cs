using System;
using AbilityKit.World.Svelto;
using Svelto.DataStructures;
using Svelto.ECS;

namespace AbilityKit.Demo.Shooter.Runtime
{
    public sealed class ShooterStateHasher
    {
        private readonly ShooterBattleState _state;
        private readonly IShooterEntityManager _entities;
        private readonly ISveltoWorldContext _context;
        private readonly ShooterSnapshotOrderBuffer _orderBuffer = new();

        public ShooterStateHasher(ShooterBattleState state, IShooterEntityManager entities)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _entities = entities ?? throw new ArgumentNullException(nameof(entities));
            _context = _entities.SveltoContext;
        }

        public uint Compute()
        {
            unchecked
            {
                var hash = 2166136261u;
                hash = Hash(hash, 5); // hash schema version
                hash = Hash(hash, _state.CurrentFrame);
                hash = Hash(hash, (int)_state.MatchState);
                hash = Hash(hash, _state.MatchCompletedFrame);
                hash = Hash(hash, _state.DefeatedEnemies);
                hash = Hash(hash, _state.VictoryTargetDefeats);
                hash = Hash(hash, _state.TimeLimitFrames);
                hash = Hash(hash, _state.RemainingTimeFrames);

                var playerCollection = _context.EntitiesDB.QueryEntities<ShooterSveltoPlayerComponent>((ExclusiveGroupStruct)ShooterSveltoGroups.Players);
                playerCollection.Deconstruct(out NB<ShooterSveltoPlayerComponent> players, out _, out var playerCount);
                var playerOrder = _orderBuffer.CreateSortedPlayerOrder(players, playerCount);
                for (int i = 0; i < playerCount; i++)
                {
                    var player = players[playerOrder[i]];
                    hash = Hash(hash, player.PlayerId);
                    hash = Hash(hash, ShooterRuntimeSnapshotUtility.Quantize(player.X));
                    hash = Hash(hash, ShooterRuntimeSnapshotUtility.Quantize(player.Y));
                    hash = Hash(hash, ShooterRuntimeSnapshotUtility.Quantize(player.AimX));
                    hash = Hash(hash, ShooterRuntimeSnapshotUtility.Quantize(player.AimY));
                    hash = Hash(hash, player.Hp);
                    hash = Hash(hash, player.Score);
                    hash = Hash(hash, player.Alive ? 1 : 0);
                }

                var bulletCollection = _context.EntitiesDB.QueryEntities<ShooterSveltoProjectileComponent>((ExclusiveGroupStruct)ShooterSveltoGroups.Projectiles);
                bulletCollection.Deconstruct(out NB<ShooterSveltoProjectileComponent> bullets, out _, out var bulletCount);
                var bulletOrder = _orderBuffer.CreateSortedProjectileOrder(bullets, bulletCount);
                for (int i = 0; i < bulletCount; i++)
                {
                    var bullet = bullets[bulletOrder[i]];
                    hash = Hash(hash, bullet.BulletId);
                    hash = Hash(hash, bullet.OwnerPlayerId);
                    hash = Hash(hash, ShooterRuntimeSnapshotUtility.Quantize(bullet.X));
                    hash = Hash(hash, ShooterRuntimeSnapshotUtility.Quantize(bullet.Y));
                    hash = Hash(hash, ShooterRuntimeSnapshotUtility.Quantize(bullet.VelocityX));
                    hash = Hash(hash, ShooterRuntimeSnapshotUtility.Quantize(bullet.VelocityY));
                    hash = Hash(hash, bullet.RemainingFrames);
                    hash = Hash(hash, bullet.PenetrationRemaining);
                    hash = Hash(hash, ShooterRuntimeSnapshotUtility.Quantize(bullet.ExplosionRadius));
                    hash = Hash(hash, bullet.ExplosionDamage);
                }

                var enemyCollection = _context.EntitiesDB.QueryEntities<ShooterSveltoTransformComponent, ShooterSveltoHealthComponent, ShooterSveltoNavigationComponent>((ExclusiveGroupStruct)ShooterSveltoGroups.GameplayTargets);
                enemyCollection.Deconstruct(
                    out NB<ShooterSveltoTransformComponent> enemyTransforms,
                    out NB<ShooterSveltoHealthComponent> enemyHealths,
                    out NB<ShooterSveltoNavigationComponent> enemyNavigation,
                    out var enemyIds,
                    out var enemyCount);
                var enemyOrder = _orderBuffer.CreateSortedEnemyOrder(enemyIds, enemyCount);
                for (int i = 0; i < enemyCount; i++)
                {
                    var enemyIndex = enemyOrder[i];
                    var enemyId = checked((int)enemyIds[enemyIndex]);
                    var transform = enemyTransforms[enemyIndex];
                    var health = enemyHealths[enemyIndex];
                    var navigation = enemyNavigation[enemyIndex];
                    hash = Hash(hash, enemyId);
                    hash = Hash(hash, ShooterRuntimeSnapshotUtility.Quantize(transform.X));
                    hash = Hash(hash, ShooterRuntimeSnapshotUtility.Quantize(transform.Y));
                    hash = Hash(hash, ShooterRuntimeSnapshotUtility.Quantize(transform.DirectionX));
                    hash = Hash(hash, ShooterRuntimeSnapshotUtility.Quantize(transform.DirectionY));
                    hash = Hash(hash, ShooterRuntimeSnapshotUtility.Quantize(navigation.VelocityX));
                    hash = Hash(hash, ShooterRuntimeSnapshotUtility.Quantize(navigation.VelocityY));
                    hash = Hash(hash, health.Current);
                    hash = Hash(hash, health.Max);
                    hash = Hash(hash, health.Alive);
                }

                return hash;
            }
        }

        private static uint Hash(uint hash, int value)
        {
            unchecked
            {
                hash ^= (uint)value;
                hash *= 16777619u;
                return hash;
            }
        }
    }
}
