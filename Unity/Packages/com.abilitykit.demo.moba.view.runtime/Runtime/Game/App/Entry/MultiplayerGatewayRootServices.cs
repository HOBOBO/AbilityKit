using System;
using AbilityKit.Demo.Common.Rooms;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Game.Battle.Shared.Assets;
using AbilityKit.Game.Flow;
using AbilityKit.Network.Abstractions;
using AbilityKit.World.ECS;

namespace AbilityKit.Game
{
    internal sealed class MultiplayerGatewayRootServices
    {
        private readonly BattleGatewayConfigSO _config;
        private readonly DemoMultiplayerLaunchRequest _launchRequest;
        private readonly ClientRoomStore _store;
        private readonly IGatewayRoomClient _client;
        private readonly IMultiplayerRoomSession _session;
        private readonly GatewayMultiplayerRoomSession _gatewaySession;
        private readonly IRoomSnapshotProvider _snapshotProvider;
        private readonly MultiplayerRoomFlowController _controller;
        private readonly ClientRoomPushSynchronizer _pushSynchronizer;
        private readonly IBattleAssetLeaseTransferSource _assetLoader;
        private readonly IMultiplayerGatewayRuntime _runtime;
        private IEntity _root;

        public MultiplayerGatewayRootServices(
            BattleGatewayConfigSO config,
            DemoMultiplayerLaunchRequest launchRequest,
            ClientRoomStore store,
            IGatewayRoomClient client,
            IMultiplayerRoomSession session,
            GatewayMultiplayerRoomSession gatewaySession,
            IRoomSnapshotProvider snapshotProvider,
            MultiplayerRoomFlowController controller,
            ClientRoomPushSynchronizer pushSynchronizer,
            IBattleAssetLeaseTransferSource assetLoader,
            IMultiplayerGatewayRuntime runtime)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _launchRequest = launchRequest;
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _gatewaySession = gatewaySession ?? throw new ArgumentNullException(nameof(gatewaySession));
            _snapshotProvider = snapshotProvider ?? throw new ArgumentNullException(nameof(snapshotProvider));
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
            _pushSynchronizer = pushSynchronizer ?? throw new ArgumentNullException(nameof(pushSynchronizer));
            _assetLoader = assetLoader ?? throw new ArgumentNullException(nameof(assetLoader));
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        }

        public bool IsPublished => _root.IsValid;

        public void Publish(IEntity root)
        {
            if (!root.IsValid) throw new ArgumentException("Gateway services require a valid root.", nameof(root));
            if (_root.IsValid) return;

            _root = root;
            root.WithRef(_config);
            if (_launchRequest != null)
            {
                root.WithRef(_launchRequest);
            }
            root.WithRef(_store);
            root.WithRef<IGatewayRoomClient>(_client);
            root.WithRef<IMultiplayerRoomSession>(_session);
            root.WithRef(_gatewaySession);
            root.WithRef<IRoomSnapshotProvider>(_snapshotProvider);
            root.WithRef(_controller);
            root.WithRef(_pushSynchronizer);
            root.WithRef<IBattleAssetLeaseTransferSource>(_assetLoader);
            root.WithRef<IMultiplayerGatewayRuntime>(_runtime);
        }

        public void Withdraw()
        {
            var root = _root;
            _root = default;
            if (!root.IsValid) return;

            root.RemoveComponent(typeof(IMultiplayerGatewayRuntime));
            root.RemoveComponent(typeof(IBattleAssetLeaseTransferSource));
            root.RemoveComponent(typeof(ClientRoomPushSynchronizer));
            root.RemoveComponent(typeof(MultiplayerRoomFlowController));
            root.RemoveComponent(typeof(IRoomSnapshotProvider));
            root.RemoveComponent(typeof(GatewayMultiplayerRoomSession));
            root.RemoveComponent(typeof(IMultiplayerRoomSession));
            root.RemoveComponent(typeof(IGatewayRoomClient));
            root.RemoveComponent(typeof(ClientRoomStore));
            root.RemoveComponent(typeof(DemoMultiplayerLaunchRequest));
            root.RemoveComponent(typeof(BattleGatewayConfigSO));
        }
    }
}
