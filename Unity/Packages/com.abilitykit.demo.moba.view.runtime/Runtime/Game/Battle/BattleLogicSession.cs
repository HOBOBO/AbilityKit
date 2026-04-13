using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.FrameSync;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Worlds.Blueprints;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Core.Common.SnapshotRouting;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World;
using AbilityKit.Ability.World.Management;
using AbilityKit.Ability.World.Services;
using AbilityKit.Game.Battle.Requests;
using AbilityKit.Ability.Host.Extensions.Rollback;
using AbilityKit.Ability.Host.Extensions.Time;
using AbilityKit.Ability.Host.Extensions.WorldStart;
using AbilityKit.Network.Abstractions;

namespace AbilityKit.Game.Battle
{
    public sealed class BattleLogicSession : IDisposable
    {
        private readonly BattleLogicSessionOptions _options;
        private readonly IWorldManager _worldManager;
        private readonly HostRuntime _server;
        private readonly IBattleLogicClient _client;
        private readonly IBattleLogicTransport _transport;

        private RemoteFrameBuffer<RemoteInputFrame> _remoteInputFrames;
        private RemoteFrameBuffer<RemoteSnapshotFrame> _remoteSnapshotFrames;
        private readonly RemoteFrameAggregator _remoteFrameAggregator = new RemoteFrameAggregator();

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

                var serverOptions = new HostRuntimeOptions();
                var server = new HostRuntime(_worldManager, serverOptions);

                var modules = new HostRuntimeModuleHost()
                    .Add(new FrameSyncDriverModule())
                    .Add(new ServerFrameTimeModule())
                    .Add(new WorldAutoStartModule());

                if (_options.EnableRollback)
                {
                    var history = _options.RollbackHistoryFrames;
                    if (history <= 0) history = 600;
                    var captureEvery = _options.RollbackCaptureEveryNFrames;
                    if (captureEvery <= 0) captureEvery = 30;

                    RollbackModule = new ServerRollbackModule(history, captureEvery, BuildRollbackRegistry);
                    modules.Add(RollbackModule);
                }

                modules.InstallAll(server, serverOptions);
                _server = server;
            }

            if (_options.Mode == BattleLogicMode.Remote)
            {
                if (remoteTransport == null) throw new ArgumentNullException(nameof(remoteTransport));
                var transport = remoteTransport;
                _transport = transport;
                _client = BattleLogicClientFactory.CreateRemote(transport);
            }
            else
            {
                var transport = new InMemoryBattleLogicTransport(_server, _options.ClientId);
                _transport = transport;
                _client = BattleLogicClientFactory.CreateRemote(transport);
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

            _client.FrameReceived += OnFrameReceivedForStreams;
        }

        private RollbackRegistry BuildRollbackRegistry(IWorld world)
        {
            var reg = new RollbackRegistry();
            if (world?.Services == null) return reg;

            if (world.Services.TryResolve<MobaActorRegistry>(out var actorReg) && actorReg != null)
            {
                reg.Register(new MobaActorTransformRollbackProvider(actorReg));
            }

            if (world.Services.TryResolve<AbilityKit.Demo.Moba.Rollback.RollbackWorldRandom>(out var rng) && rng != null)
            {
                reg.Register(rng);
            }

            return reg;
        }

        public event Action<FramePacket> FrameReceived
        {
            add => _client.FrameReceived += value;
            remove => _client.FrameReceived -= value;
        }

        public IRemoteFrameSource<RemoteInputFrame> RemoteInputFrames
        {
            get
            {
                EnsureRemoteFrameStreamsCreated();
                return _remoteInputFrames;
            }
        }

        public IRemoteFrameSink<RemoteInputFrame> RemoteInputSink
        {
            get
            {
                EnsureRemoteFrameStreamsCreated();
                return _remoteInputFrames;
            }
        }

        public IRemoteFrameSource<RemoteSnapshotFrame> RemoteSnapshotFrames
        {
            get
            {
                EnsureRemoteFrameStreamsCreated();
                return _remoteSnapshotFrames;
            }
        }

        public IRemoteFrameSink<RemoteSnapshotFrame> RemoteSnapshotSink
        {
            get
            {
                EnsureRemoteFrameStreamsCreated();
                return _remoteSnapshotFrames;
            }
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
            if (_server != null)
            {
                _server.Tick(deltaTime);
            }

            _client.Tick(deltaTime);
        }

        public void Dispose()
        {
            _client.FrameReceived -= OnFrameReceivedForStreams;
            _client?.Dispose();
            (_transport as IDisposable)?.Dispose();
            _worldManager?.DisposeAll();

            _remoteInputFrames?.Dispose();
            _remoteSnapshotFrames?.Dispose();
        }

        private void EnsureRemoteFrameStreamsCreated()
        {
            _remoteInputFrames ??= new RemoteFrameBuffer<RemoteInputFrame>(initialCapacity: 256);
            _remoteSnapshotFrames ??= new RemoteFrameBuffer<RemoteSnapshotFrame>(initialCapacity: 256);
        }

        private void OnFrameReceivedForStreams(FramePacket packet)
        {
            if (packet == null) return;
            EnsureRemoteFrameStreamsCreated();

            var frame = packet.Frame.Value;

            _remoteFrameAggregator.AddPacket(packet);
            _remoteInputFrames.Add(frame, _remoteFrameAggregator.BuildInputFrame(packet.Frame));
            _remoteSnapshotFrames.Add(frame, _remoteFrameAggregator.BuildSnapshotFrame(packet.Frame));

            var trimBefore = frame - 256;
            if (trimBefore > 0)
            {
                _remoteFrameAggregator.TrimBefore(trimBefore);
                _remoteInputFrames.TrimBefore(trimBefore);
                _remoteSnapshotFrames.TrimBefore(trimBefore);
            }
        }
    }
}
