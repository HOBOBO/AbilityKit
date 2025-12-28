using System;
using AbilityKit.Ability.Server;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Entitas;
using AbilityKit.Ability.World.Management;
using AbilityKit.Ability.World.Services;
using AbilityKit.Game.Battle.Requests;

namespace AbilityKit.Game.Battle
{
    public sealed class BattleLogicSession : IDisposable
    {
        private readonly BattleLogicSessionOptions _options;
        private readonly IWorldManager _worldManager;
        private readonly ILogicWorldServer _server;
        private readonly IBattleLogicClient _client;

        public BattleLogicSession(BattleLogicSessionOptions options, IBattleLogicTransport remoteTransport = null)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));

            var registry = new WorldTypeRegistry().RegisterEntitasWorld(_options.WorldType);
            _worldManager = new WorldManager(new RegistryWorldFactory(registry));

            _server = new AbilityKit.Ability.Server.LogicWorldServer(_worldManager);

            if (_options.Mode == BattleLogicMode.Remote)
            {
                var transport = remoteTransport ?? new InMemoryBattleLogicTransport(_server, _options.ClientId);
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
                    ServiceBuilder = builder
                };

                _client.CreateWorld(new CreateWorldRequest(create));
            }

            if (_options.AutoJoin)
            {
                _client.Join(new JoinWorldRequest(_options.WorldId, new PlayerId(_options.PlayerId)));
            }
        }

        public event Action<FramePacket> FrameReceived
        {
            add => _client.FrameReceived += value;
            remove => _client.FrameReceived -= value;
        }

        public WorldId WorldId => _client.WorldId;

        public bool TryGetWorld(out IWorld world)
        {
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
