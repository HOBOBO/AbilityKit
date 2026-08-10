using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Runtime;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private BattleSessionNetAdapter _netAdapter
        {
            get => _handles.Net.Adapter;
            set => _handles.Net.Adapter = value;
        }

        private IBattleSessionNetAdapterContext _netAdapterCtx
        {
            get => _handles.Net.Ctx;
            set => _handles.Net.Ctx = value;
        }

        private IConnection _gatewayConn => _runtime.GatewayRoom.Connection;

        private IGatewayRoomClient _gatewayClient => _runtime.GatewayRoom.Client;

        private IDispatcher _unityDispatcher
        {
            get => _handles.Dispatchers.UnityDispatcher;
            set => _handles.Dispatchers.UnityDispatcher = value;
        }

        private DedicatedThreadDispatcher _networkIoDispatcher
        {
            get => _handles.Dispatchers.NetworkIoDispatcher;
            set => _handles.Dispatchers.NetworkIoDispatcher = value;
        }
    }
}
