using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.Rollback;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Core.Snapshots.Routing;
using AbilityKit.Game.Battle.Requests;
using AbilityKit.Network.Abstractions;

namespace AbilityKit.Game.Battle
{
    public sealed class BattleLogicSession : IDisposable
    {
        private readonly BattleLogicSessionOptions _options;
        private readonly IBattleLogicClient _client;
        private readonly IBattleLogicTransport _transport;
        private readonly IRemoteFrameStreams _remoteFrameStreams;
        private readonly IBattleLogicRuntimeFactory _runtimeFactory;
        private readonly BattleLogicSessionRuntime _runtime;

        public ServerRollbackModule RollbackModule => _runtime?.RollbackModule;

        public BattleLogicSession(BattleLogicSessionOptions options, IBattleLogicTransport remoteTransport = null)
            : this(options, remoteTransport, new MobaRollbackRegistryFactory(), new MobaBattleLogicRuntimeFactory())
        {
        }

        internal BattleLogicSession(
            BattleLogicSessionOptions options,
            IBattleLogicTransport remoteTransport,
            IBattleRollbackRegistryFactory rollbackRegistryFactory)
            : this(options, remoteTransport, rollbackRegistryFactory, new MobaBattleLogicRuntimeFactory())
        {
        }

        internal BattleLogicSession(
            BattleLogicSessionOptions options,
            IBattleLogicTransport remoteTransport,
            IBattleRollbackRegistryFactory rollbackRegistryFactory,
            IBattleLogicRuntimeFactory runtimeFactory)
            : this(options, remoteTransport, rollbackRegistryFactory, runtimeFactory, null)
        {
        }

        internal BattleLogicSession(
            BattleLogicSessionOptions options,
            IBattleLogicTransport remoteTransport,
            IBattleRollbackRegistryFactory rollbackRegistryFactory,
            IBattleLogicRuntimeFactory runtimeFactory,
            IRemoteFrameStreams remoteFrameStreams)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            if (rollbackRegistryFactory == null) throw new ArgumentNullException(nameof(rollbackRegistryFactory));
            _runtimeFactory = runtimeFactory ?? throw new ArgumentNullException(nameof(runtimeFactory));
            _remoteFrameStreams = remoteFrameStreams ?? RemoteFrameStreamsFactory.Create();
            _runtime = _runtimeFactory.CreateRuntime(_options, rollbackRegistryFactory);

            if (_options.Mode == BattleLogicMode.Remote)
            {
                if (remoteTransport == null) throw new ArgumentNullException(nameof(remoteTransport));
                _transport = remoteTransport;
                _client = BattleLogicClientFactory.CreateRemote(remoteTransport);
            }
            else
            {
                var runtime = _runtime ?? throw new InvalidOperationException("Local battle logic runtime factory returned null.");
                var transport = new InMemoryBattleLogicTransport(runtime.Server, _options.ClientId);
                _transport = transport;
                _client = BattleLogicClientFactory.CreateRemote(transport);
            }

            if (_options.AutoConnect)
            {
                _client.Connect();
            }

            if (_options.AutoCreateWorld)
            {
                var create = new WorldCreateOptions(_options.WorldId, _options.WorldType)
                {
                    ServiceBuilder = _runtimeFactory.CreateWorldServices(_options),
                };

                _client.CreateWorld(new CreateWorldRequest(create));
            }

            if (_options.AutoJoin)
            {
                _client.Join(new JoinWorldRequest(_options.WorldId, new PlayerId(_options.PlayerId)));
            }

            _client.FrameReceived += OnFrameReceivedForStreams;
        }

        public event Action<FramePacket> FrameReceived
        {
            add => _client.FrameReceived += value;
            remove => _client.FrameReceived -= value;
        }

        public IRemoteFrameSource<RemoteInputFrame> RemoteInputFrames => _remoteFrameStreams.InputFrames;

        public IRemoteFrameSink<RemoteInputFrame> RemoteInputSink => _remoteFrameStreams.InputSink;

        public IRemoteFrameSource<RemoteSnapshotFrame> RemoteSnapshotFrames => _remoteFrameStreams.SnapshotFrames;

        public IRemoteFrameSink<RemoteSnapshotFrame> RemoteSnapshotSink => _remoteFrameStreams.SnapshotSink;

        public WorldId WorldId => _client.WorldId;

        public bool TryGetWorld(out IWorld world)
        {
            world = null;
            if (_runtime == null) return false;
            return _runtime.TryGetWorld(_client.WorldId, out world);
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
            _runtime?.Tick(deltaTime);
            _client.Tick(deltaTime);
        }

        public void Dispose()
        {
            _client.FrameReceived -= OnFrameReceivedForStreams;
            _client?.Dispose();
            (_transport as IDisposable)?.Dispose();
            _runtime?.Dispose();
            _remoteFrameStreams.Dispose();
        }

        private void OnFrameReceivedForStreams(FramePacket packet)
        {
            _remoteFrameStreams.OnFrameReceived(packet);
        }
    }
}
