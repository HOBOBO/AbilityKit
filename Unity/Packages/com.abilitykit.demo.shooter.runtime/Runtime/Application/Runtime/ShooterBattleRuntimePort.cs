using System;
using System.Collections.Generic;
using AbilityKit.Ability.StateSync.Aoi;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Game.Battle;
using AbilityKit.Protocol.Shooter;
using AbilityKit.World.Svelto;

namespace AbilityKit.Demo.Shooter.Runtime
{
    [WorldService(typeof(ShooterBattleRuntimePort), WorldLifetime.Singleton)]
    [WorldService(typeof(IShooterBattleRuntimePort), WorldLifetime.Singleton)]
    [WorldService(typeof(IShooterGameStartPort), WorldLifetime.Singleton)]
    [WorldService(typeof(IShooterInputPort), WorldLifetime.Singleton)]
    [WorldService(typeof(IShooterSimulationClock), WorldLifetime.Singleton)]
    [WorldService(typeof(IShooterSnapshotReadPort), WorldLifetime.Singleton)]
    [WorldService(typeof(IShooterStateHashProvider), WorldLifetime.Singleton)]
    [WorldService(typeof(IShooterPackedSnapshotPort), WorldLifetime.Singleton)]
    [WorldService(typeof(IShooterPureStateSnapshotPort), WorldLifetime.Singleton)]
    public sealed class ShooterBattleRuntimePort : IShooterBattleRuntimePort
    {
        private readonly ShooterBattleState _state;
        private readonly IShooterBattleSimulation _simulation;
        private readonly IShooterEntityManager _entities;
        private readonly IShooterBattleRules _rules;
        private readonly ShooterEnemyWaveOptions _enemyWaveOptions;
        private readonly ShooterRvoOptions _rvoOptions;
        private readonly ShooterArenaGameplayOptions _arenaOptions;
        private readonly ShooterMatchStateOptions _matchStateOptions;
        private readonly IShooterRvoNeighborAccelerationService _rvoNeighborAcceleration;
        private readonly ShooterStateSnapshotExporter _snapshotExporter;
        private readonly ShooterStateHasher _stateHasher;
        private readonly ShooterPackedSnapshotExporter _packedSnapshotExporter;
        private readonly ShooterPackedSnapshotImporter _packedSnapshotImporter;
        private readonly ShooterPackedSnapshotBytesCodec _bytesCodec;
        private readonly ShooterPureStateSnapshotExporter _pureStateSnapshotExporter;
        private readonly ShooterBotAiRuntime _botAiRuntime;
        private readonly ShooterBotAiService _botAiService;
        private readonly ShooterBattleServiceContext _services;
        private readonly ShooterBattleSveltoStepEngine _battleStepEngine;

        public ShooterBattleRuntimePort()
            : this(ShooterEntityLimitOptions.Default)
        {
        }

        public ShooterBattleRuntimePort(ShooterEntityLimitOptions entityLimits)
            : this(entityLimits, ShooterEnemyWaveOptions.Disabled)
        {
        }

        public ShooterBattleRuntimePort(ShooterEntityLimitOptions entityLimits, ShooterEnemyWaveOptions enemyWaveOptions)
            : this(entityLimits, enemyWaveOptions, ShooterArenaGameplayOptions.Disabled)
        {
        }

        public ShooterBattleRuntimePort(ShooterEntityLimitOptions entityLimits, ShooterEnemyWaveOptions enemyWaveOptions, ShooterArenaGameplayOptions arenaOptions)
            : this(entityLimits, enemyWaveOptions, ShooterRvoOptions.Default, arenaOptions)
        {
        }

        public ShooterBattleRuntimePort(
            ShooterEntityLimitOptions entityLimits,
            ShooterEnemyWaveOptions enemyWaveOptions,
            ShooterRvoOptions rvoOptions,
            ShooterArenaGameplayOptions arenaOptions)
            : this(CreateDefaultEntityManager(entityLimits), enemyWaveOptions, rvoOptions, arenaOptions)
        {
        }

        private ShooterBattleRuntimePort(
            IShooterEntityManager entities,
            ShooterEnemyWaveOptions enemyWaveOptions,
            ShooterRvoOptions rvoOptions,
            ShooterArenaGameplayOptions arenaOptions)
            : this(CreateState(entities), enemyWaveOptions, rvoOptions, arenaOptions)
        {
        }

        private ShooterBattleRuntimePort(
            ShooterBattleState state,
            ShooterEnemyWaveOptions enemyWaveOptions,
            ShooterRvoOptions rvoOptions,
            ShooterArenaGameplayOptions arenaOptions)
            : this(state, ShooterBattleRules.Default, enemyWaveOptions, rvoOptions, arenaOptions)
        {
        }

        private ShooterBattleRuntimePort(
            ShooterBattleState state,
            IShooterBattleRules rules,
            ShooterEnemyWaveOptions enemyWaveOptions,
            ShooterRvoOptions rvoOptions,
            ShooterArenaGameplayOptions arenaOptions)
            : this(state, new ShooterBattleSimulation(state, rules, arenaOptions), state.Entities, rules, enemyWaveOptions, rvoOptions, arenaOptions, ShooterMatchStateOptions.Default)
        {
        }

        public ShooterBattleRuntimePort(ShooterBattleState state, IShooterBattleSimulation simulation, IShooterEntityManager entities)
            : this(state, simulation, entities, ShooterBattleRules.Default)
        {
        }

        public ShooterBattleRuntimePort(ShooterBattleState state, IShooterBattleSimulation simulation, IShooterEntityManager entities, IShooterBattleRules rules)
            : this(state, simulation, entities, rules, ShooterEnemyWaveOptions.Disabled, ShooterRvoOptions.Default, ShooterArenaGameplayOptions.Disabled, ShooterMatchStateOptions.Default)
        {
        }

        public ShooterBattleRuntimePort(ShooterBattleState state, IShooterBattleSimulation simulation, IShooterEntityManager entities, IShooterBattleRules rules, ShooterEnemyWaveOptions enemyWaveOptions)
            : this(state, simulation, entities, rules, enemyWaveOptions, ShooterRvoOptions.Default, ShooterArenaGameplayOptions.Disabled, ShooterMatchStateOptions.Default)
        {
        }

        public ShooterBattleRuntimePort(ShooterBattleState state, IShooterBattleSimulation simulation, IShooterEntityManager entities, IShooterBattleRules rules, ShooterEnemyWaveOptions enemyWaveOptions, ShooterArenaGameplayOptions arenaOptions)
            : this(state, simulation, entities, rules, enemyWaveOptions, ShooterRvoOptions.Default, arenaOptions, ShooterMatchStateOptions.Default)
        {
        }

        public ShooterBattleRuntimePort(
            ShooterBattleState state,
            IShooterBattleSimulation simulation,
            IShooterEntityManager entities,
            IShooterBattleRules rules,
            ShooterEnemyWaveOptions enemyWaveOptions,
            ShooterArenaGameplayOptions arenaOptions,
            ShooterMatchStateOptions matchStateOptions)
            : this(state, simulation, entities, rules, enemyWaveOptions, ShooterRvoOptions.Default, arenaOptions, matchStateOptions)
        {
        }

        public ShooterBattleRuntimePort(
            ShooterBattleState state,
            IShooterBattleSimulation simulation,
            IShooterEntityManager entities,
            IShooterBattleRules rules,
            ShooterEnemyWaveOptions enemyWaveOptions,
            ShooterRvoOptions rvoOptions,
            ShooterArenaGameplayOptions arenaOptions,
            ShooterMatchStateOptions matchStateOptions)
            : this(
                state,
                simulation,
                entities,
                rules,
                enemyWaveOptions,
                rvoOptions,
                arenaOptions,
                matchStateOptions,
                ShooterNullRvoNeighborAccelerationService.Instance)
        {
        }

        public ShooterBattleRuntimePort(
            ShooterBattleState state,
            IShooterBattleSimulation simulation,
            IShooterEntityManager entities,
            IShooterBattleRules rules,
            ShooterEnemyWaveOptions enemyWaveOptions,
            ShooterRvoOptions rvoOptions,
            ShooterArenaGameplayOptions arenaOptions,
            ShooterMatchStateOptions matchStateOptions,
            IShooterRvoNeighborAccelerationService rvoNeighborAcceleration)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
            _entities = entities ?? throw new ArgumentNullException(nameof(entities));
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _enemyWaveOptions = enemyWaveOptions ?? ShooterEnemyWaveOptions.Disabled;
            _rvoOptions = rvoOptions ?? ShooterRvoOptions.Default;
            _arenaOptions = arenaOptions ?? ShooterArenaGameplayOptions.Disabled;
            _matchStateOptions = matchStateOptions ?? ShooterMatchStateOptions.Default;
            _rvoNeighborAcceleration = rvoNeighborAcceleration ?? ShooterNullRvoNeighborAccelerationService.Instance;
            _snapshotExporter = new ShooterStateSnapshotExporter(_state, _entities);
            _stateHasher = new ShooterStateHasher(_state, _entities);
            _packedSnapshotExporter = new ShooterPackedSnapshotExporter(_state, _entities, _rules, this);
            _packedSnapshotImporter = new ShooterPackedSnapshotImporter(_state, _entities);
            _bytesCodec = new ShooterPackedSnapshotBytesCodec();
            _pureStateSnapshotExporter = new ShooterPureStateSnapshotExporter(_state, this, this, _entities);
            _botAiRuntime = new ShooterBotAiRuntime(_state, _entities);
            _botAiService = new ShooterBotAiService(_botAiRuntime);
            _services = CreateServiceContext(_enemyWaveOptions);
            _battleStepEngine = new ShooterBattlePipelineFactory().Create(_services);
            _services.EnginesRoot.AddEngine(_battleStepEngine);
        }

        public BattleRuntimeStatus BattleStatus => new BattleRuntimeStatus(
            BattleRuntimeCapability.GameStart |
            BattleRuntimeCapability.Input |
            BattleRuntimeCapability.Simulation |
            BattleRuntimeCapability.SnapshotOutput |
            BattleRuntimeCapability.SnapshotInput |
            BattleRuntimeCapability.StateReadModel |
            BattleRuntimeCapability.StateHash |
            BattleRuntimeCapability.BotControl,
            MapRuntimeState(_state.MatchState));

        public bool IsStarted => _state.IsStarted;

        public ShooterBattleMatchState MatchState => _state.MatchState;

        public ShooterMatchResultSnapshot MatchResult => _state.GetMatchResult();

        public int CurrentFrame => _state.CurrentFrame;

        public ShooterStartGamePayload StartSpec => _state.StartSpec;

        public bool StartGame(in ShooterStartGamePayload spec)
        {
            _state.Reset(in spec);
            _state.VictoryTargetDefeats = _enemyWaveOptions.VictoryTargetDefeats;
            _state.SetTimeLimitFrames(_enemyWaveOptions.DurationFrames);
            _botAiService.ClearBotAi();

            var players = spec.Players ?? Array.Empty<ShooterStartPlayer>();
            _entities.BeginStructuralChanges();
            try
            {
                for (int i = 0; i < players.Length; i++)
                {
                    var player = players[i];
                    if (player.PlayerId <= 0 || _entities.HasPlayer(player.PlayerId)) continue;

                    var spawnX = player.SpawnX;
                    var spawnY = player.SpawnY;
                    ShooterCircularArenaMath.Clamp(ref spawnX, ref spawnY, _arenaOptions);

                    var component = new ShooterSveltoPlayerComponent
                    {
                        PlayerId = player.PlayerId,
                        X = spawnX,
                        Y = spawnY,
                        AimX = 1f,
                        AimY = 0f,
                        Hp = ShooterGameplay.DefaultPlayerHp,
                        Score = 0,
                        Alive = true
                    };
                    _entities.AddPlayer(in component);
                }
            }
            finally
            {
                _entities.EndStructuralChanges();
            }

            if (_entities.PlayerCount > 0)
            {
                _state.SetMatchRunning();
            }

            return _state.IsStarted;
        }

        public int SubmitInput(int frame, ShooterPlayerCommand[] commands)
        {
            return SubmitInput(frame, (IReadOnlyList<ShooterPlayerCommand>)commands);
        }

        public int SubmitInput(int frame, IReadOnlyList<ShooterPlayerCommand> commands)
        {
            if (!_state.IsStarted || commands == null || commands.Count == 0)
            {
                return 0;
            }

            var accepted = 0;
            for (int i = 0; i < commands.Count; i++)
            {
                var command = commands[i];
                if (!_entities.HasPlayer(command.PlayerId)) continue;

                _state.InputBuffer.SubmitCommand(frame, in command);
                accepted++;
            }

            return accepted;
        }

        public bool Tick(float deltaTime)
        {
            if (!_state.IsStarted)
            {
                return false;
            }

            _battleStepEngine.Step(in deltaTime);
            _state.InputBuffer.TrimToWindow(_state.CurrentFrame);
            return _state.IsStarted;
        }

        public ShooterStateSnapshotPayload GetSnapshot()
        {
            return _snapshotExporter.Export();
        }

        public ShooterStateSnapshotPayload GetSnapshotTransient()
        {
            return _snapshotExporter.ExportTransient();
        }

        public ShooterPlayerSnapshot[] GetPlayerSnapshotsTransient()
        {
            return _snapshotExporter.ExportPlayersTransient();
        }

        public uint ComputeStateHash()
        {
            return _stateHasher.Compute();
        }

        public ShooterPackedSnapshotPayload ExportPackedSnapshot(ulong worldId, bool isFullSnapshot = true, bool authorityOverride = false)
        {
            return _packedSnapshotExporter.Export(worldId, isFullSnapshot, authorityOverride);
        }

        public byte[] ExportPackedSnapshotBytes(ulong worldId, bool isFullSnapshot = true, bool authorityOverride = false)
        {
            return _bytesCodec.Export(this, worldId, isFullSnapshot, authorityOverride);
        }

        public bool ImportPackedSnapshot(in ShooterPackedSnapshotPayload snapshot)
        {
            return _packedSnapshotImporter.Import(in snapshot);
        }

        public bool ImportPackedSnapshotBytes(byte[] payload)
        {
            return _bytesCodec.Import(this, payload);
        }

        public ShooterPureStateSnapshotPayload ExportPureStateSnapshot(
            ulong worldId,
            bool isFullBaseline = true,
            ShooterPureStateSyncSettings? settings = null,
            int baselineFrame = 0,
            uint baselineHash = 0,
            ShooterPureStateInterestScope? interestScope = null,
            AoiInterestSet? aoiInterestSet = null,
            bool computeStateHash = true)
        {
            return _pureStateSnapshotExporter.Export(worldId, isFullBaseline, settings, baselineFrame, baselineHash, interestScope, aoiInterestSet, computeStateHash);
        }

        public ShooterPureStateSnapshotPayload ExportPureStateSnapshotTransient(
            ulong worldId,
            bool isFullBaseline = true,
            ShooterPureStateSyncSettings? settings = null,
            int baselineFrame = 0,
            uint baselineHash = 0,
            ShooterPureStateInterestScope? interestScope = null,
            AoiInterestSet? aoiInterestSet = null,
            bool computeStateHash = true)
        {
            return _pureStateSnapshotExporter.ExportTransient(worldId, isFullBaseline, settings, baselineFrame, baselineHash, interestScope, aoiInterestSet, computeStateHash);
        }

        public bool TryGetPlayer(int playerId, out ShooterSveltoPlayerComponent player)
        {
            return _entities.TryGetPlayer(playerId, out player);
        }

        public void SetPlayer(in ShooterSveltoPlayerComponent player)
        {
            _entities.SetPlayer(in player);
        }

        public int BotAiCount => _botAiService.BotAiCount;

        public bool MountBotAi(in ShooterBotAiMountOptions options)
        {
            return _botAiService.MountBotAi(in options);
        }

        public bool UnmountBotAi(int playerId)
        {
            return _botAiService.UnmountBotAi(playerId);
        }

        public void ClearBotAi()
        {
            _botAiService.ClearBotAi();
        }

        private static BattleRuntimeState MapRuntimeState(ShooterBattleMatchState state)
        {
            return state switch
            {
                ShooterBattleMatchState.NotStarted => BattleRuntimeState.Ready,
                ShooterBattleMatchState.Running => BattleRuntimeState.Running,
                ShooterBattleMatchState.Victory => BattleRuntimeState.Completed,
                ShooterBattleMatchState.Defeat => BattleRuntimeState.Completed,
                ShooterBattleMatchState.Ended => BattleRuntimeState.Completed,
                _ => BattleRuntimeState.Unknown,
            };
        }

        private static ShooterBattleState CreateState(IShooterEntityManager entities)
        {
            return new ShooterBattleState(entities);
        }

        private static IShooterEntityManager CreateDefaultEntityManager(ShooterEntityLimitOptions entityLimits)
        {
            return new ShooterEntityManager(new SveltoWorldContext(), entityLimits);
        }

        private ShooterBattleServiceContext CreateServiceContext(ShooterEnemyWaveOptions enemyWaveOptions)
        {
            var rvoWorkspace = new ShooterRvoWorldWorkspace(128, _rvoOptions.MaxNeighbors);
            IShooterRvoSolver rvoSolver = new ShooterManagedRvoSolver(_rvoNeighborAcceleration);
            return new ShooterBattleServiceContext(_entities.SveltoContext)
                .Add(_state)
                .Add<IShooterBattleSimulation>(_simulation)
                .Add<IShooterEntityManager>(_entities)
                .Add<IShooterBattleRules>(_rules)
                .Add<IShooterBotAiRuntime>(_botAiRuntime)
                .Add(enemyWaveOptions)
                .Add(_rvoOptions)
                .Add(rvoWorkspace)
                .Add<IShooterRvoSolver>(rvoSolver)
                .Add(_arenaOptions)
                .Add(_matchStateOptions);
        }
    }
}
