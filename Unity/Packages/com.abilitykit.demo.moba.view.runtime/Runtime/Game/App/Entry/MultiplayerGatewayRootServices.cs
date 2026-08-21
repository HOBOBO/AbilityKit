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
        private readonly IGatewayRoomCommandCapability _commands;
        private readonly IGatewayRoomRecoveryQueryCapability _recoveryQuery;
        private readonly IDemoRoomDirectoryClient _roomDirectory;
        private readonly IMultiplayerRoomSession _session;
        private readonly GatewayMultiplayerRoomSession _gatewaySession;
        private readonly IRoomSnapshotProvider _snapshotProvider;
        private readonly MultiplayerRoomFlowController _controller;
        private readonly ClientRoomPushSynchronizer _pushSynchronizer;
        private readonly IBattleAssetLeaseTransferSource _assetLoader;
        private readonly IMultiplayerGatewayDiagnostics _diagnostics;
        private readonly IMultiplayerGatewayRecoveryControl _recoveryControl;
        private IEntity _root;

        public MultiplayerGatewayRootServices(
            BattleGatewayConfigSO config,
            DemoMultiplayerLaunchRequest launchRequest,
            ClientRoomStore store,
            IGatewayRoomCommandCapability commands,
            IGatewayRoomRecoveryQueryCapability recoveryQuery,
            IDemoRoomDirectoryClient roomDirectory,
            IMultiplayerRoomSession session,
            GatewayMultiplayerRoomSession gatewaySession,
            IRoomSnapshotProvider snapshotProvider,
            MultiplayerRoomFlowController controller,
            ClientRoomPushSynchronizer pushSynchronizer,
            IBattleAssetLeaseTransferSource assetLoader,
            IMultiplayerGatewayDiagnostics diagnostics,
            IMultiplayerGatewayRecoveryControl recoveryControl)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _launchRequest = launchRequest;
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
            _recoveryQuery = recoveryQuery ?? throw new ArgumentNullException(nameof(recoveryQuery));
            _roomDirectory = roomDirectory ?? throw new ArgumentNullException(nameof(roomDirectory));
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _gatewaySession = gatewaySession ?? throw new ArgumentNullException(nameof(gatewaySession));
            _snapshotProvider = snapshotProvider ?? throw new ArgumentNullException(nameof(snapshotProvider));
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
            _pushSynchronizer = pushSynchronizer ?? throw new ArgumentNullException(nameof(pushSynchronizer));
            _assetLoader = assetLoader ?? throw new ArgumentNullException(nameof(assetLoader));
            _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
            _recoveryControl = recoveryControl ?? throw new ArgumentNullException(nameof(recoveryControl));
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
            root.WithRef<IGatewayRoomCommandCapability>(_commands);
            root.WithRef<IGatewayRoomRecoveryQueryCapability>(_recoveryQuery);
            root.WithRef<IDemoRoomDirectoryClient>(_roomDirectory);
            root.WithRef<IMultiplayerRoomSession>(_session);
            root.WithRef(_gatewaySession);
            root.WithRef<IRoomSnapshotProvider>(_snapshotProvider);
            root.WithRef(_controller);
            root.WithRef(_pushSynchronizer);
            root.WithRef<IBattleAssetLeaseTransferSource>(_assetLoader);
            root.WithRef<IMultiplayerGatewayDiagnostics>(_diagnostics);
            root.WithRef<IMultiplayerGatewayRecoveryControl>(_recoveryControl);
        }

        public void Withdraw()
        {
            var root = _root;
            _root = default;
            if (!root.IsValid) return;

            root.RemoveComponent(typeof(IMultiplayerGatewayRecoveryControl));
            root.RemoveComponent(typeof(IMultiplayerGatewayDiagnostics));
            root.RemoveComponent(typeof(IBattleAssetLeaseTransferSource));
            root.RemoveComponent(typeof(ClientRoomPushSynchronizer));
            root.RemoveComponent(typeof(MultiplayerRoomFlowController));
            root.RemoveComponent(typeof(IRoomSnapshotProvider));
            root.RemoveComponent(typeof(GatewayMultiplayerRoomSession));
            root.RemoveComponent(typeof(IMultiplayerRoomSession));
            root.RemoveComponent(typeof(IDemoRoomDirectoryClient));
            root.RemoveComponent(typeof(IGatewayRoomRecoveryQueryCapability));
            root.RemoveComponent(typeof(IGatewayRoomCommandCapability));
            root.RemoveComponent(typeof(ClientRoomStore));
            root.RemoveComponent(typeof(DemoMultiplayerLaunchRequest));
            root.RemoveComponent(typeof(BattleGatewayConfigSO));
        }
    }
}
