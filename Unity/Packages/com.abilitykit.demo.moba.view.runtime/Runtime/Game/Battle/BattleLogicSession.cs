using System;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Modules;
using AbilityKit.Ability.Share.Impl.Moba.Move;
using AbilityKit.Ability.Share.Impl.Moba.Rollback;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Impl.Moba.Worlds.Blueprints;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Entitas;
using AbilityKit.Ability.World.Management;
using AbilityKit.Ability.World.Services;
using AbilityKit.Game.Battle.Requests;
using AbilityKit.Ability.Impl.Moba.Systems;
using AbilityKit.Ability.Host.Extensions.Rollback;
using AbilityKit.Ability.Host.Extensions.Time;

namespace AbilityKit.Game.Battle
{
    public sealed class BattleLogicSession : IDisposable
    {
        private readonly BattleLogicSessionOptions _options;
        private readonly IWorldManager _worldManager;
        private readonly ILogicWorldServer _server;
        private readonly IBattleLogicClient _client;

        public ServerRollbackModule RollbackModule { get; }

        public BattleLogicSession(BattleLogicSessionOptions options, IBattleLogicTransport remoteTransport = null)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));

            if (_options.Mode == BattleLogicMode.Remote)
            {
                _worldManager = null;
                _server = null;
            }
            else
            {
                var typeRegistry = new WorldTypeRegistry()
                    .RegisterEntitasWorld(MobaLobbyWorldBlueprint.Type)
                    .RegisterEntitasWorld(MobaBattleWorldBlueprint.Type);

                var blueprints = new AbilityKit.Ability.Host.WorldBlueprints.WorldBlueprintRegistry();
                MobaWorldBlueprintsRegistration.RegisterAll(blueprints);

                var baseFactory = new RegistryWorldFactory(typeRegistry);
                var factory = new AbilityKit.Ability.Host.WorldBlueprints.WorldBlueprintWorldFactory(baseFactory, blueprints);
                _worldManager = new WorldManager(factory);

                var serverOptions = new LogicWorldServerOptions();
                var modules = new LogicWorldServerModuleHost()
                    .Add(new ServerFrameTimeModule());

                if (_options.EnableRollback)
                {
                    var history = _options.RollbackHistoryFrames;
                    if (history <= 0) history = 600;
                    var captureEvery = _options.RollbackCaptureEveryNFrames;
                    if (captureEvery <= 0) captureEvery = 30;

                    RollbackModule = new ServerRollbackModule(history, captureEvery, BuildRollbackRegistry);
                    modules.Add(RollbackModule);
                }

                modules.InstallAll(serverOptions);
                _server = new LogicWorldServer(_worldManager, serverOptions);
            }

            if (_options.Mode == BattleLogicMode.Remote)
            {
                if (remoteTransport == null) throw new ArgumentNullException(nameof(remoteTransport));
                var transport = remoteTransport;
                _client = BattleLogicClientFactory.CreateRemote(transport);
            }
            else
            {
                _client = new LocalBattleLogicClient(_server, _options.ClientId);
            }

            if (_options.AutoConnect)
            {
                _client.Connect();
            }

            if (_options.AutoCreateWorld)
            {
                WorldContainerBuilder builder = _options.WorldServices;
                if (builder == null)
                {
                    var prefixes = _options.NamespacePrefixes;

                    if (_options.ScanAllLoadedAssemblies)
                    {
                        builder = WorldServiceContainerFactory.CreateWithAttributes(
                            _options.Profile,
                            true,
                            prefixes
                        );
                    }
                    else
                    {
                        var scanAssemblies = _options.ScanAssemblies;
                        if (scanAssemblies == null || scanAssemblies.Length == 0)
                        {
                            scanAssemblies = new[]
                            {
                                typeof(WorldServiceContainerFactory).Assembly,
                                typeof(BattleLogicSession).Assembly
                            };
                        }

                        builder = WorldServiceContainerFactory.CreateWithAttributes(
                            _options.Profile,
                            scanAssemblies,
                            prefixes
                        );
                    }
                }

                var create = new WorldCreateOptions(_options.WorldId, _options.WorldType)
                {
                    ServiceBuilder = builder,
                };

                _client.CreateWorld(new CreateWorldRequest(create));
            }

            if (_options.AutoJoin)
            {
                _client.Join(new JoinWorldRequest(_options.WorldId, new PlayerId(_options.PlayerId)));
            }
        }

        private RollbackRegistry BuildRollbackRegistry(IWorld world)
        {
            var reg = new RollbackRegistry();
            if (world?.Services == null) return reg;

            if (world.Services.TryResolve<MobaActorRegistry>(out var actorReg) && actorReg != null)
            {
                reg.Register(new MobaActorTransformRollbackProvider(actorReg));
            }

            if (world.Services.TryResolve<MobaMoveService>(out var move) && move != null)
            {
                reg.Register(new MobaMoveRollbackProvider(move));
            }

            return reg;
        }

        public event Action<FramePacket> FrameReceived
        {
            add => _client.FrameReceived += value;
            remove => _client.FrameReceived -= value;
        }

        public WorldId WorldId => _client.WorldId;

        public bool TryGetWorld(out IWorld world)
        {
            world = null;
            if (_worldManager == null) return false;
            return _worldManager.TryGet(_client.WorldId, out world);
        }

        public void Connect()
        {
            _client.Connect();
        }

        public void Disconnect()
        {
            _client.Disconnect();
        }

        public void CreateWorld(CreateWorldRequest request)
        {
            _client.CreateWorld(request);
        }

        public void Join(JoinWorldRequest request)
        {
            _client.Join(request);
        }

        public void Leave(LeaveWorldRequest request)
        {
            _client.Leave(request);
        }

        public void SubmitInput(SubmitInputRequest request)
        {
            _client.SubmitInput(request);
        }

        public void Tick(float deltaTime)
        {
            _client.Tick(deltaTime);
        }

        public void Dispose()
        {
            _client?.Dispose();
            _worldManager?.DisposeAll();
        }
    }
}
