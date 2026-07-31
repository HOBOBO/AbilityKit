using System;
using System.Collections.Generic;
using AbilityKit.World.Svelto;
using Svelto.DataStructures;
using Svelto.ECS;
using Svelto.ECS.Internal;

namespace AbilityKit.Demo.Shooter.Runtime
{
    internal interface IShooterBattleSystem : IStepEngine<float>
    {
        int Order { get; }
    }

    internal static class ShooterBattleSystemOrder
    {
        public const int BeginFrame = 0;

        public const int PlayerBotAi = 100;

        public const int EnemyWaveSpawn = 150;

        public const int EnemyMovementIntent = 170;

        public const int EnemyRvoSolve = 175;

        public const int EnemyMovementIntegration = 180;

        public const int Simulation = 200;

        public const int EnemyLifecycleCleanup = 250;

        public const int EnemyWaveAttack = 300;

        public const int MatchState = 400;
    }

    internal sealed class ShooterBattleSveltoStepEngine : IStepGroupEngine<float>
    {
        private readonly List<IShooterBattleSystem> _systems;
        private readonly List<IEngine> _engines;

        public ShooterBattleSveltoStepEngine(IEnumerable<IShooterBattleSystem> systems)
        {
            if (systems == null) throw new ArgumentNullException(nameof(systems));

            _systems = new List<IShooterBattleSystem>();
            foreach (var system in systems)
            {
                if (system != null)
                {
                    _systems.Add(system);
                }
            }

            _systems.Sort((left, right) => left.Order.CompareTo(right.Order));
            _engines = new List<IEngine>(_systems);
        }

        public string name => nameof(ShooterBattleSveltoStepEngine);

        public IReadOnlyList<IShooterBattleSystem> Systems => _systems;

        public IEnumerable<IEngine> engines => _engines;

        public void Step(in float deltaTime)
        {
            for (int i = 0; i < _systems.Count; i++)
            {
                _systems[i].Step(in deltaTime);
            }
        }
    }

    internal sealed class ShooterFrameBeginBattleSystem : IShooterBattleSystem
    {
        private readonly ShooterBattleState _state;

        public ShooterFrameBeginBattleSystem(IShooterBattleServiceResolver services)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));

            _state = services.Resolve<ShooterBattleState>();
        }

        public int Order => ShooterBattleSystemOrder.BeginFrame;

        public string name => nameof(ShooterFrameBeginBattleSystem);

        public void Step(in float deltaTime)
        {
            _state.CurrentFrame++;
            _state.Events.Clear();
        }
    }

    internal sealed class ShooterBotAiServiceBattleSystem : IShooterBattleSystem
    {
        private readonly IShooterBotAiRuntime _botAiRuntime;

        public ShooterBotAiServiceBattleSystem(IShooterBattleServiceResolver services)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));

            _botAiRuntime = services.Resolve<IShooterBotAiRuntime>();
        }

        public int Order => ShooterBattleSystemOrder.PlayerBotAi;

        public string name => nameof(ShooterBotAiServiceBattleSystem);

        public void Step(in float deltaTime)
        {
            _botAiRuntime.Tick(deltaTime);
        }
    }

    internal sealed class ShooterSimulationBattleSystem : IShooterBattleSystem
    {
        private readonly IShooterBattleSimulation _simulation;

        public ShooterSimulationBattleSystem(IShooterBattleServiceResolver services)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));

            _simulation = services.Resolve<IShooterBattleSimulation>();
        }

        public int Order => ShooterBattleSystemOrder.Simulation;

        public string name => nameof(ShooterSimulationBattleSystem);

        public void Step(in float deltaTime)
        {
            _simulation.Tick(deltaTime);
        }
    }

    internal sealed class ShooterEnemyLifecycleCleanupBattleSystem : IShooterBattleSystem
    {
        private readonly IShooterEntityManager _entities;
        private readonly ShooterBattleState _state;
        private readonly List<int> _removalBuffer = new List<int>();

        public ShooterEnemyLifecycleCleanupBattleSystem(IShooterBattleServiceResolver services)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));

            _entities = services.Resolve<IShooterEntityManager>();
            _state = services.Resolve<ShooterBattleState>();
        }

        public int Order => ShooterBattleSystemOrder.EnemyLifecycleCleanup;

        public string name => nameof(ShooterEnemyLifecycleCleanupBattleSystem);

        public void Step(in float deltaTime)
        {
            _removalBuffer.Clear();
            if (_state.PendingDefeatedEnemyRemovals.Count == 0)
            {
                return;
            }

            _removalBuffer.AddRange(_state.PendingDefeatedEnemyRemovals);
            _state.PendingDefeatedEnemyRemovals.Clear();

            _entities.BeginStructuralChanges();
            try
            {
                for (int i = 0; i < _removalBuffer.Count; i++)
                {
                    _entities.RemoveEnemy(_removalBuffer[i]);
                }
            }
            finally
            {
                _entities.EndStructuralChanges();
            }
        }
    }

    public sealed class ShooterMatchStateOptions
    {
        public static ShooterMatchStateOptions Default { get; } = new ShooterMatchStateOptions(false);

        public static ShooterMatchStateOptions NonTerminatingDefeat { get; } =
            new ShooterMatchStateOptions(true);

        public ShooterMatchStateOptions(bool continueAfterAllPlayersDefeated)
        {
            ContinueAfterAllPlayersDefeated = continueAfterAllPlayersDefeated;
        }

        public bool ContinueAfterAllPlayersDefeated { get; }
    }

    internal sealed class ShooterMatchStateBattleSystem : IShooterBattleSystem
    {
        private readonly ShooterBattleState _state;
        private readonly ISveltoWorldContext _context;
        private readonly ShooterMatchStateOptions _options;

        public ShooterMatchStateBattleSystem(IShooterBattleServiceResolver services)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));

            _state = services.Resolve<ShooterBattleState>();
            _context = services.Resolve<ISveltoWorldContext>();
            _options = services.TryResolve<ShooterMatchStateOptions>(out var options) && options != null
                ? options
                : ShooterMatchStateOptions.Default;
        }

        public int Order => ShooterBattleSystemOrder.MatchState;

        public string name => nameof(ShooterMatchStateBattleSystem);

        public void Step(in float deltaTime)
        {
            if (_state.MatchState != ShooterBattleMatchState.Running)
            {
                return;
            }

            if (_state.VictoryTargetDefeats > 0 && _state.DefeatedEnemies >= _state.VictoryTargetDefeats)
            {
                _state.TryCompleteMatch(ShooterBattleMatchState.Victory);
                return;
            }

            if (!_options.ContinueAfterAllPlayersDefeated && AreAllPlayersDefeated())
            {
                _state.TryCompleteMatch(ShooterBattleMatchState.Defeat);
                return;
            }

            if (_state.IsTimeExpired)
            {
                _state.TryCompleteMatch(ShooterBattleMatchState.Ended);
            }
        }

        private bool AreAllPlayersDefeated()
        {
            var playerCollection = _context.EntitiesDB.QueryEntities<ShooterSveltoPlayerComponent>((ExclusiveGroupStruct)ShooterSveltoGroups.Players);
            playerCollection.Deconstruct(out NB<ShooterSveltoPlayerComponent> players, out _, out var count);
            if (count == 0)
            {
                return true;
            }

            for (int i = 0; i < count; i++)
            {
                if (players[i].Alive)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
