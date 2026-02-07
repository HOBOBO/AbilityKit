using AbilityKit.Network.Abstractions;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private sealed class SessionDispatchersController
        {
            public void OnAttach(BattleSessionHandles handles)
            {
                if (handles == null) return;

                handles.Dispatchers.UnityDispatcher = UnityMainThreadDispatcher.CaptureCurrent();
                handles.Dispatchers.NetworkIoDispatcher ??= new DedicatedThreadDispatcher("GatewayNetworkThread");
            }

            public void OnDetach(BattleSessionHandles handles)
            {
                if (handles == null) return;

                handles.Dispatchers.UnityDispatcher = null;
            }
        }
    }
}
