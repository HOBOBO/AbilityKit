#nullable enable

using System;
using AbilityKit.World.Svelto;
using Svelto.DataStructures;
using Svelto.ECS;

namespace AbilityKit.Demo.Shooter.Runtime
{
    internal sealed class ShooterEnemyWaveMovementBattleSystem : IShooterBattleSystem
    {
        private const float StopDistance = 0.75f;
        private const float StopDistanceSquared = StopDistance * StopDistance;
        private readonly ShooterBattleState _state;
        private readonly ISveltoWorldContext _context;
        private readonly ShooterEnemyWaveOptions _options;
        private readonly ShooterArenaGameplayOptions _arenaOptions;
        private ShooterSpatialTargetIndex _targetIndex => _state.PlayerTargetIndex;

        public ShooterEnemyWaveMovementBattleSystem(IShooterBattleServiceResolver services)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));

            _state = services.Resolve<ShooterBattleState>();
            _context = services.Resolve<ISveltoWorldContext>();
            _options = services.TryResolve<ShooterEnemyWaveOptions>(out var options) && options != null
                ? options
                : ShooterEnemyWaveOptions.Disabled;
            _arenaOptions = services.TryResolve<ShooterArenaGameplayOptions>(out var arenaOptions) && arenaOptions != null
                ? arenaOptions
                : ShooterArenaGameplayOptions.Disabled;
        }

        public int Order => ShooterBattleSystemOrder.EnemyWaveMovement;

        public string name => nameof(ShooterEnemyWaveMovementBattleSystem);

        public void Step(in float deltaTime)
        {
            if (!_options.Enabled || _state.MatchState != ShooterBattleMatchState.Running || deltaTime <= 0f)
            {
                return;
            }

            var playerCollection = _context.EntitiesDB.QueryEntities<ShooterSveltoPlayerComponent>(ShooterSveltoGroups.Players);
            playerCollection.Deconstruct(out NB<ShooterSveltoPlayerComponent> players, out _, out var playerCount);
            if (playerCount == 0)
            {
                return;
            }

            var enemyCollection = _context.EntitiesDB.QueryEntities<ShooterSveltoTransformComponent, ShooterSveltoHealthComponent>((ExclusiveGroupStruct)ShooterSveltoGroups.GameplayTargets);
            enemyCollection.Deconstruct(out NB<ShooterSveltoTransformComponent> enemyTransforms, out NB<ShooterSveltoHealthComponent> enemyHealths, out _, out var enemyCount);
            _targetIndex.Rebuild(players, playerCount, _state.CurrentFrame);
            if (_targetIndex.TryGetOnlyLivePlayer(out var onlyPlayer))
            {
                MoveEnemiesTowardSingleTarget(enemyTransforms, enemyHealths, enemyCount, onlyPlayer.X, onlyPlayer.Y, deltaTime);
                return;
            }

            MoveEnemiesTowardNearestPlayer(enemyTransforms, enemyHealths, enemyCount, deltaTime);
        }

        private void MoveEnemiesTowardSingleTarget(
            NB<ShooterSveltoTransformComponent> enemyTransforms,
            NB<ShooterSveltoHealthComponent> enemyHealths,
            int enemyCount,
            float targetX,
            float targetY,
            float deltaTime)
        {
            for (var i = 0; i < enemyCount; i++)
            {
                if (enemyHealths[i].Alive == 0 || enemyHealths[i].Current <= 0)
                {
                    continue;
                }

                ref var enemyTransform = ref enemyTransforms[i];
                MoveEnemyTowardTarget(ref enemyTransform, targetX, targetY, deltaTime);
            }
        }

        private void MoveEnemiesTowardNearestPlayer(
            NB<ShooterSveltoTransformComponent> enemyTransforms,
            NB<ShooterSveltoHealthComponent> enemyHealths,
            int enemyCount,
            float deltaTime)
        {
            for (var i = 0; i < enemyCount; i++)
            {
                if (enemyHealths[i].Alive == 0 || enemyHealths[i].Current <= 0)
                {
                    continue;
                }

                ref var enemyTransform = ref enemyTransforms[i];
                if (!_targetIndex.TryFindNearestPlayer(
                    enemyTransform.X,
                    enemyTransform.Y,
                    selfPlayerId: 0,
                    out _,
                    out var player,
                    out var distanceSquared))
                {
                    continue;
                }

                MoveEnemyTowardTarget(ref enemyTransform, player.X, player.Y, distanceSquared, deltaTime);
            }
        }

        private void MoveEnemyTowardTarget(ref ShooterSveltoTransformComponent enemyTransform, float targetX, float targetY, float deltaTime)
        {
            var directionX = targetX - enemyTransform.X;
            var directionY = targetY - enemyTransform.Y;
            var distanceSquared = directionX * directionX + directionY * directionY;
            MoveEnemyAlongDirection(ref enemyTransform, directionX, directionY, distanceSquared, deltaTime);
        }

        private void MoveEnemyTowardTarget(ref ShooterSveltoTransformComponent enemyTransform, float targetX, float targetY, float distanceSquared, float deltaTime)
        {
            var directionX = targetX - enemyTransform.X;
            var directionY = targetY - enemyTransform.Y;
            MoveEnemyAlongDirection(ref enemyTransform, directionX, directionY, distanceSquared, deltaTime);
        }

        private void MoveEnemyAlongDirection(ref ShooterSveltoTransformComponent enemyTransform, float directionX, float directionY, float distanceSquared, float deltaTime)
        {
            if (distanceSquared <= StopDistanceSquared)
            {
                return;
            }

            var distance = MathF.Sqrt(distanceSquared);
            directionX /= distance;
            directionY /= distance;
            enemyTransform.DirectionX = directionX;
            enemyTransform.DirectionY = directionY;

            var moveDistance = MathF.Min(distance - StopDistance, ShooterBattleTuning.EnemySpeed * deltaTime);
            enemyTransform.X += directionX * moveDistance;
            enemyTransform.Y += directionY * moveDistance;
            ShooterCircularArenaMath.Clamp(ref enemyTransform.X, ref enemyTransform.Y, _arenaOptions);
        }

    }
}
