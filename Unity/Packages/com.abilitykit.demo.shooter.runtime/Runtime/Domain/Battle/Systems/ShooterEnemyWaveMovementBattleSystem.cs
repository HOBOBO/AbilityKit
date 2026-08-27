#nullable enable

using System;
using AbilityKit.World.Svelto;
using Svelto.DataStructures;
using Svelto.ECS;
using Svelto.ECS.Internal;

namespace AbilityKit.Demo.Shooter.Runtime
{
    internal sealed class ShooterEnemyMovementIntentBattleSystem : IShooterBattleSystem
    {
        private const float StopDistance = 0.75f;
        private const float StopDistanceSquared = StopDistance * StopDistance;
        private readonly ShooterBattleState _state;
        private readonly ISveltoWorldContext _context;
        private readonly ShooterEnemyWaveOptions _waveOptions;
        private readonly ShooterRvoOptions _rvoOptions;
        private readonly ShooterRvoWorldWorkspace _workspace;
        private ShooterSpatialTargetIndex TargetIndex => _state.PlayerTargetIndex;

        public ShooterEnemyMovementIntentBattleSystem(IShooterBattleServiceResolver services)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));

            _state = services.Resolve<ShooterBattleState>();
            _context = services.Resolve<ISveltoWorldContext>();
            _waveOptions = services.TryResolve<ShooterEnemyWaveOptions>(out var waveOptions) && waveOptions != null
                ? waveOptions
                : ShooterEnemyWaveOptions.Disabled;
            _rvoOptions = services.TryResolve<ShooterRvoOptions>(out var rvoOptions) && rvoOptions != null
                ? rvoOptions
                : ShooterRvoOptions.Disabled;
            _workspace = services.Resolve<ShooterRvoWorldWorkspace>();
        }

        public int Order => ShooterBattleSystemOrder.EnemyMovementIntent;

        public string name => nameof(ShooterEnemyMovementIntentBattleSystem);

        public void Step(in float deltaTime)
        {
            _workspace.BeginFrame(0, _rvoOptions.MaxNeighbors);
            if (!_waveOptions.Enabled || _state.MatchState != ShooterBattleMatchState.Running || deltaTime <= 0f)
            {
                return;
            }

            var playerCollection = _context.EntitiesDB.QueryEntities<ShooterSveltoPlayerComponent>(ShooterSveltoGroups.Players);
            playerCollection.Deconstruct(out NB<ShooterSveltoPlayerComponent> players, out _, out var playerCount);
            if (playerCount == 0)
            {
                return;
            }

            var enemyCollection = _context.EntitiesDB.QueryEntities<ShooterSveltoTransformComponent, ShooterSveltoHealthComponent, ShooterSveltoNavigationComponent>((ExclusiveGroupStruct)ShooterSveltoGroups.GameplayTargets);
            enemyCollection.Deconstruct(
                out NB<ShooterSveltoTransformComponent> enemyTransforms,
                out NB<ShooterSveltoHealthComponent> enemyHealths,
                out NB<ShooterSveltoNavigationComponent> enemyNavigation,
                out NativeEntityIDs enemyIds,
                out var enemyCount);

            var activeCount = 0;
            for (var i = 0; i < enemyCount; i++)
            {
                if (enemyHealths[i].Alive != 0 && enemyHealths[i].Current > 0)
                {
                    activeCount++;
                }
            }

            _workspace.BeginFrame(activeCount, _rvoOptions.MaxNeighbors);
            var slot = 0;
            for (var i = 0; i < enemyCount; i++)
            {
                if (enemyHealths[i].Alive == 0 || enemyHealths[i].Current <= 0)
                {
                    continue;
                }

                _workspace.EntityIds[slot] = enemyIds[i];
                _workspace.SourceIndices[slot] = i;
                slot++;
            }

            _workspace.SortByEntityId();
            TargetIndex.Rebuild(players, playerCount, _state.CurrentFrame);
            var hasOnlyPlayer = TargetIndex.TryGetOnlyLivePlayer(out var onlyPlayer);
            for (var i = 0; i < activeCount; i++)
            {
                var sourceIndex = _workspace.SourceIndices[i];
                ref var transform = ref enemyTransforms[sourceIndex];
                ref var navigation = ref enemyNavigation[sourceIndex];
                var radius = navigation.Radius > 0f ? navigation.Radius : _rvoOptions.AgentRadius;
                var maxSpeed = navigation.MaxSpeed > 0f ? navigation.MaxSpeed : ShooterBattleTuning.EnemySpeed;
                navigation.Radius = radius;
                navigation.MaxSpeed = maxSpeed;

                _workspace.PositionX[i] = transform.X;
                _workspace.PositionY[i] = transform.Y;
                _workspace.VelocityX[i] = navigation.VelocityX;
                _workspace.VelocityY[i] = navigation.VelocityY;
                _workspace.Radius[i] = radius;
                _workspace.MaxSpeed[i] = maxSpeed;

                if (hasOnlyPlayer)
                {
                    SetPreferredVelocity(i, transform.X, transform.Y, onlyPlayer.X, onlyPlayer.Y, maxSpeed, deltaTime);
                }
                else if (TargetIndex.TryFindNearestPlayer(
                    transform.X,
                    transform.Y,
                    selfPlayerId: 0,
                    out _,
                    out var player,
                    out var distanceSquared))
                {
                    SetPreferredVelocity(i, transform.X, transform.Y, player.X, player.Y, distanceSquared, maxSpeed, deltaTime);
                }
            }
        }

        private void SetPreferredVelocity(
            int index,
            float x,
            float y,
            float targetX,
            float targetY,
            float maxSpeed,
            float deltaTime)
        {
            var directionX = targetX - x;
            var directionY = targetY - y;
            var distanceSquared = directionX * directionX + directionY * directionY;
            SetPreferredVelocity(index, x, y, targetX, targetY, distanceSquared, maxSpeed, deltaTime);
        }

        private void SetPreferredVelocity(
            int index,
            float x,
            float y,
            float targetX,
            float targetY,
            float distanceSquared,
            float maxSpeed,
            float deltaTime)
        {
            if (distanceSquared <= StopDistanceSquared)
            {
                return;
            }

            var distance = MathF.Sqrt(distanceSquared);
            var speed = MathF.Min(maxSpeed, (distance - StopDistance) / deltaTime);
            _workspace.PreferredVelocityX[index] = (targetX - x) / distance * speed;
            _workspace.PreferredVelocityY[index] = (targetY - y) / distance * speed;
        }
    }

    internal sealed class ShooterEnemyRvoSolveBattleSystem : IShooterBattleSystem, IShooterBattleSubstageDiagnostics
    {
        internal const string NeighborCollectionStageName = "ShooterEnemyRvoSolveBattleSystem.NeighborCollect";
        internal const string AcceleratedValidationStageName = "ShooterEnemyRvoSolveBattleSystem.AcceleratedValidation";
        internal const string OrcaSolveStageName = "ShooterEnemyRvoSolveBattleSystem.OrcaSolve";

        private readonly ShooterRvoOptions _options;
        private readonly ShooterRvoWorldWorkspace _workspace;
        private readonly IShooterRvoSolver _solver;

        public ShooterEnemyRvoSolveBattleSystem(IShooterBattleServiceResolver services)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));

            _options = services.Resolve<ShooterRvoOptions>();
            _workspace = services.Resolve<ShooterRvoWorldWorkspace>();
            _solver = services.Resolve<IShooterRvoSolver>();
        }

        public int Order => ShooterBattleSystemOrder.EnemyRvoSolve;

        public string name => nameof(ShooterEnemyRvoSolveBattleSystem);

        public Action<string, double>? StageTimingSink
        {
            get => _solver.StageTimingSink;
            set => _solver.StageTimingSink = value;
        }

        public void Step(in float deltaTime)
        {
            if (_workspace.Count == 0)
            {
                return;
            }

            if (_options.Enabled)
            {
                _solver.Solve(_workspace, _options, deltaTime);
                return;
            }

            for (var i = 0; i < _workspace.Count; i++)
            {
                _workspace.OutputVelocityX[i] = _workspace.PreferredVelocityX[i];
                _workspace.OutputVelocityY[i] = _workspace.PreferredVelocityY[i];
            }
        }
    }

    internal sealed class ShooterEnemyMovementIntegrationBattleSystem : IShooterBattleSystem
    {
        private readonly ISveltoWorldContext _context;
        private readonly ShooterRvoOptions _rvoOptions;
        private readonly ShooterArenaGameplayOptions _arenaOptions;
        private readonly ShooterRvoWorldWorkspace _workspace;

        public ShooterEnemyMovementIntegrationBattleSystem(IShooterBattleServiceResolver services)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));

            _context = services.Resolve<ISveltoWorldContext>();
            _rvoOptions = services.Resolve<ShooterRvoOptions>();
            _arenaOptions = services.TryResolve<ShooterArenaGameplayOptions>(out var arenaOptions) && arenaOptions != null
                ? arenaOptions
                : ShooterArenaGameplayOptions.Disabled;
            _workspace = services.Resolve<ShooterRvoWorldWorkspace>();
        }

        public int Order => ShooterBattleSystemOrder.EnemyMovementIntegration;

        public string name => nameof(ShooterEnemyMovementIntegrationBattleSystem);

        public void Step(in float deltaTime)
        {
            if (_workspace.Count == 0 || deltaTime <= 0f)
            {
                return;
            }

            var enemyCollection = _context.EntitiesDB.QueryEntities<ShooterSveltoTransformComponent, ShooterSveltoNavigationComponent>((ExclusiveGroupStruct)ShooterSveltoGroups.GameplayTargets);
            enemyCollection.Deconstruct(
                out NB<ShooterSveltoTransformComponent> transforms,
                out NB<ShooterSveltoNavigationComponent> navigation,
                out _,
                out _);

            var maxVelocityDelta = _rvoOptions.MaxAcceleration * deltaTime;
            for (var i = 0; i < _workspace.Count; i++)
            {
                var sourceIndex = _workspace.SourceIndices[i];
                ref var transform = ref transforms[sourceIndex];
                ref var agent = ref navigation[sourceIndex];
                var velocityX = _workspace.OutputVelocityX[i];
                var velocityY = _workspace.OutputVelocityY[i];
                if (_rvoOptions.Enabled)
                {
                    LimitVelocityDelta(agent.VelocityX, agent.VelocityY, ref velocityX, ref velocityY, maxVelocityDelta);
                }

                agent.VelocityX = velocityX;
                agent.VelocityY = velocityY;
                if (velocityX * velocityX + velocityY * velocityY > ShooterManagedRvoSolver.EpsilonSquared)
                {
                    var inverseSpeed = 1f / MathF.Sqrt(velocityX * velocityX + velocityY * velocityY);
                    transform.DirectionX = velocityX * inverseSpeed;
                    transform.DirectionY = velocityY * inverseSpeed;
                }

                transform.X += velocityX * deltaTime;
                transform.Y += velocityY * deltaTime;
                ShooterCircularArenaMath.Clamp(ref transform.X, ref transform.Y, _arenaOptions);
            }
        }

        private static void LimitVelocityDelta(
            float currentX,
            float currentY,
            ref float targetX,
            ref float targetY,
            float maximumDelta)
        {
            var deltaX = targetX - currentX;
            var deltaY = targetY - currentY;
            var deltaSquared = deltaX * deltaX + deltaY * deltaY;
            if (deltaSquared <= maximumDelta * maximumDelta)
            {
                return;
            }

            var scale = maximumDelta / MathF.Sqrt(deltaSquared);
            targetX = currentX + deltaX * scale;
            targetY = currentY + deltaY * scale;
        }
    }
}
