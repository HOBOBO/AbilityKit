using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.Rollback;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Game.Battle.Requests;
using AbilityKit.Network.Abstractions;

namespace AbilityKit.Game.Battle
{
    public sealed class BattleLogicSession : IDisposable
    {
        private readonly IBattleLogicClient _client;
        private readonly IBattleLogicTransport _transport;
        private readonly IRemoteFrameStreams _remoteFrameStreams;
        private readonly BattleLogicSessionRuntime _runtime;
        private bool _streamHandlerSubscribed;
        private bool _broadcastHandlerSubscribed;
        private int _disposed;

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
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (rollbackRegistryFactory == null) throw new ArgumentNullException(nameof(rollbackRegistryFactory));
            if (runtimeFactory == null) throw new ArgumentNullException(nameof(runtimeFactory));
            if (options.Mode == BattleLogicMode.Remote && remoteTransport == null)
                throw new ArgumentNullException(nameof(remoteTransport));
            // A supplied remote transport transfers ownership to this session.
            if (options.Mode == BattleLogicMode.Remote) _transport = remoteTransport;
            try
            {
                _remoteFrameStreams = remoteFrameStreams ?? RemoteFrameStreamsFactory.Create();
                if (options.Mode == BattleLogicMode.Local)
                {
                    _runtime = runtimeFactory.CreateRuntime(options, rollbackRegistryFactory)
                        ?? throw new InvalidOperationException("Local battle logic runtime factory returned null.");
                    _transport = new InMemoryBattleLogicTransport(_runtime.Server, options.ClientId);
                }

                _client = BattleLogicClientFactory.CreateRemote(_transport);
                if (options.AutoConnect) _client.Connect();
                if (options.AutoCreateWorld)
                {
                    var create = new WorldCreateOptions(options.WorldId, options.WorldType)
                    {
                        ServiceBuilder = runtimeFactory.CreateWorldServices(options),
                    };
                    _client.CreateWorld(new CreateWorldRequest(create));
                }

                if (options.AutoJoin)
                    _client.Join(new JoinWorldRequest(options.WorldId, new PlayerId(options.PlayerId)));

                _client.FrameReceived += OnFrameReceivedForStreams;
                _streamHandlerSubscribed = true;
                _client.FrameReceived += BroadcastFrameReceived;
                _broadcastHandlerSubscribed = true;
            }
            catch (Exception startupFailure)
            {
                try { Dispose(); }
                catch (Exception cleanupFailure)
                {
                    throw new AggregateException("Battle session startup and cleanup failed.",
                        startupFailure, cleanupFailure);
                }
                throw;
            }
        }

        private event Action<FramePacket>? _frameReceivedBroadcast;

        public event Action<FramePacket> FrameReceived
        {
            add => _frameReceivedBroadcast += value;
            remove => _frameReceivedBroadcast -= value;
        }

        private void BroadcastFrameReceived(FramePacket packet)
        {
            _frameReceivedBroadcast?.Invoke(packet);
        }

        /// <summary>
        /// 重连补帧注入：把服务端 CatchUp 历史帧按与在线帧完全相同的管道喂回
        /// （远端流 + FrameReceived 订阅者），使抖动缓冲/预测/调和无差别消费。
        /// </summary>
        public void InjectRemoteFrame(FramePacket packet)
        {
            OnFrameReceivedForStreams(packet);
            BroadcastFrameReceived(packet);
        }

        /// <summary>底层网络传输（帧同步重连恢复用：鉴权状态/原始请求/推送订阅）。非 NetworkTransport 为 null。</summary>
        public AbilityKit.Network.Battle.NetworkTransport? NetworkTransport =>
            _transport as AbilityKit.Network.Battle.NetworkTransport;

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
            if (System.Threading.Interlocked.Exchange(ref _disposed, 1) != 0) return;
            System.Collections.Generic.List<Exception> failures = null;
            void Release(Action action)
            {
                try { action(); }
                catch (Exception exception)
                {
                    (failures ??= new System.Collections.Generic.List<Exception>()).Add(exception);
                }
            }

            if (_broadcastHandlerSubscribed)
                Release(() => _client.FrameReceived -= BroadcastFrameReceived);
            if (_streamHandlerSubscribed)
                Release(() => _client.FrameReceived -= OnFrameReceivedForStreams);
            if (_client != null) Release(_client.Dispose);
            if (_transport is IDisposable transport) Release(transport.Dispose);
            if (_runtime != null) Release(_runtime.Dispose);
            if (_remoteFrameStreams != null) Release(_remoteFrameStreams.Dispose);
            if (failures != null) throw new AggregateException("Battle session cleanup failed.", failures);
        }

        private void OnFrameReceivedForStreams(FramePacket packet)
        {
            _remoteFrameStreams.OnFrameReceived(packet);
        }
    }
}
